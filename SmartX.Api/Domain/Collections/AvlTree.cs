namespace SmartX.Api.Domain.Collections;

public class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private sealed class Node
    {
        public readonly TKey Key;
        public readonly TValue Value;
        public int Height = 1;
        public Node? Left;
        public Node? Right;

        public Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    private Node? _root;

    public int Count { get; private set; }

    public int Height => HeightOf(_root);

    // ------------------------------------------------------------------ insert

    public void Add(TKey key, TValue value)
    {
        _root = Insert(_root, key, value);
        Count++;
    }

    private static Node Insert(Node? node, TKey key, TValue value)
    {
        if (node is null) return new Node(key, value);

        // Equal keys go right, which keeps duplicates in insertion order.
        if (key.CompareTo(node.Key) < 0)
            node.Left = Insert(node.Left, key, value);
        else
            node.Right = Insert(node.Right, key, value);

        return Rebalance(node);
    }

    // ----------------------------------------------------------------- balance

    private static int HeightOf(Node? node) => node?.Height ?? 0;

    private static void UpdateHeight(Node node) =>
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));

    private static Node Rebalance(Node node)
    {
        UpdateHeight(node);
        var balance = HeightOf(node.Left) - HeightOf(node.Right);

        if (balance > 1) // left-heavy
        {
            if (HeightOf(node.Left!.Left) < HeightOf(node.Left.Right))
                node.Left = RotateLeft(node.Left);   // left-right case: straighten the zig-zag first
            return RotateRight(node);
        }

        if (balance < -1) // right-heavy
        {
            if (HeightOf(node.Right!.Right) < HeightOf(node.Right.Left))
                node.Right = RotateRight(node.Right); // right-left case
            return RotateLeft(node);
        }

        return node;
    }

    private static Node RotateRight(Node y)
    {
        var x = y.Left!;
        y.Left = x.Right;
        x.Right = y;
        UpdateHeight(y);
        UpdateHeight(x);
        return x;
    }

    private static Node RotateLeft(Node x)
    {
        var y = x.Right!;
        x.Right = y.Left;
        y.Left = x;
        UpdateHeight(x);
        UpdateHeight(y);
        return y;
    }

    // ------------------------------------------------------------ min / remove

    public bool TryGetMin(out TKey key, out TValue value)
    {
        var n = _root;
        if (n is null) { key = default!; value = default!; return false; }
        while (n.Left is not null) n = n.Left;
        key = n.Key; value = n.Value;
        return true;
    }

    public bool TryGetMax(out TKey key, out TValue value)
    {
        var n = _root;
        if (n is null) { key = default!; value = default!; return false; }
        while (n.Right is not null) n = n.Right;
        key = n.Key; value = n.Value;
        return true;
    }

    public bool TryRemoveMin(out TKey key, out TValue value)
    {
        if (_root is null) { key = default!; value = default!; return false; }

        _root = RemoveMin(_root, out var min);
        Count--;
        key = min.Key;
        value = min.Value;
        return true;
    }

    private static Node? RemoveMin(Node node, out Node min)
    {
        if (node.Left is null)
        {
            min = node;
            return node.Right;
        }

        node.Left = RemoveMin(node.Left, out min);
        return Rebalance(node);
    }

    // ------------------------------------------------------------------ ranges

    public IEnumerable<KeyValuePair<TKey, TValue>> Range(TKey from, TKey to)
    {
        if (from.CompareTo(to) > 0) yield break;

        // Path to the first node >= from. Every node pushed is a candidate whose left side may still hold more.
        var pending = new Stack<Node>();
        for (var n = _root; n is not null;)
        {
            if (n.Key.CompareTo(from) >= 0) { pending.Push(n); n = n.Left; }
            else n = n.Right;
        }

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.Key.CompareTo(to) > 0) yield break; // everything after this is later still

            yield return new KeyValuePair<TKey, TValue>(node.Key, node.Value);

            for (var c = node.Right; c is not null; c = c.Left) pending.Push(c);
        }
    }

    /// <summary>The same range, newest first. Mirror image of <see cref="Range"/>.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> RangeDescending(TKey from, TKey to)
    {
        if (from.CompareTo(to) > 0) yield break;

        var pending = new Stack<Node>();
        for (var n = _root; n is not null;)
        {
            if (n.Key.CompareTo(to) <= 0) { pending.Push(n); n = n.Right; }
            else n = n.Left;
        }

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node.Key.CompareTo(from) < 0) yield break;

            yield return new KeyValuePair<TKey, TValue>(node.Key, node.Value);

            for (var c = node.Left; c is not null; c = c.Right) pending.Push(c);
        }
    }

    /// <summary>Every entry in key order.</summary>
    public IEnumerable<KeyValuePair<TKey, TValue>> InOrder()
    {
        var pending = new Stack<Node>();
        for (var n = _root; n is not null; n = n.Left) pending.Push(n);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return new KeyValuePair<TKey, TValue>(node.Key, node.Value);
            for (var c = node.Right; c is not null; c = c.Left) pending.Push(c);
        }
    }

    // -------------------------------------------------------------- diagnostics

    /// <summary>The smallest height any binary tree holding <see cref="Count"/> entries could have.</summary>
    public int MinimumPossibleHeight => Count == 0 ? 0 : (int)Math.Ceiling(Math.Log2(Count + 1));

    public bool IsValid(out string? problem)
    {
        problem = null;
        var count = 0;
        var ok = Check(_root, ref count, ref problem, out _);
        if (!ok) return false;

        if (count != Count)
        {
            problem = $"Count is {Count} but the tree holds {count} nodes.";
            return false;
        }

        // Local parent/child checks cannot catch a whole subtree sitting on the wrong side, so also
        // confirm the full in-order sequence never goes backwards.
        TKey? previous = default;
        var first = true;
        foreach (var entry in InOrder())
        {
            if (!first && entry.Key.CompareTo(previous!) < 0)
            {
                problem = $"In-order sequence goes backwards at key {entry.Key} (after {previous}).";
                return false;
            }
            previous = entry.Key;
            first = false;
        }
        return true;
    }

    private static bool Check(Node? node, ref int count, ref string? problem, out int height)
    {
        height = 0;
        if (node is null) return true;

        count++;
        if (!Check(node.Left, ref count, ref problem, out var left)) return false;
        if (!Check(node.Right, ref count, ref problem, out var right)) return false;

        if (node.Left is not null && node.Left.Key.CompareTo(node.Key) > 0)
        {
            problem = $"Left child {node.Left.Key} is greater than its parent {node.Key}.";
            return false;
        }
        if (node.Right is not null && node.Right.Key.CompareTo(node.Key) < 0)
        {
            problem = $"Right child {node.Right.Key} is smaller than its parent {node.Key}.";
            return false;
        }

        height = 1 + Math.Max(left, right);
        if (node.Height != height)
        {
            problem = $"Node {node.Key} stores height {node.Height} but its real height is {height}.";
            return false;
        }
        if (Math.Abs(left - right) > 1)
        {
            problem = $"Node {node.Key} is unbalanced: left height {left}, right height {right}.";
            return false;
        }
        return true;
    }
}