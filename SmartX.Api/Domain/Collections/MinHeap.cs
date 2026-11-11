namespace SmartX.Api.Domain.Collections;

public sealed class MinHeap<T>
{
    private T[] _items;
    private readonly IComparer<T> _comparer;

    public MinHeap(int capacity = 16, IComparer<T>? comparer = null)
    {
        _items = new T[Math.Max(capacity, 1)];
        _comparer = comparer ?? Comparer<T>.Default;
    }

    public int Count { get; private set; }
    public bool IsEmpty => Count == 0;

    public void Push(T item)
    {
        if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        _items[Count] = item;
        SiftUp(Count);
        Count++;
    }

    public T Peek() =>
        Count > 0 ? _items[0] : throw new InvalidOperationException("The heap is empty.");

    public T Pop()
    {
        if (Count == 0) throw new InvalidOperationException("The heap is empty.");

        var top = _items[0];
        Count--;
        if (Count > 0)
        {
            _items[0] = _items[Count];
            SiftDown(0);
        }
        _items[Count] = default!; // release the reference
        return top;
    }

    public bool TryPop(out T item)
    {
        if (Count == 0)
        {
            item = default!;
            return false;
        }
        item = Pop();
        return true;
    }

    // Hole-based sifting: move parents/children into the hole and write the moving item once at the end.
    private void SiftUp(int index)
    {
        var item = _items[index];
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (_comparer.Compare(item, _items[parent]) >= 0) break;
            _items[index] = _items[parent];
            index = parent;
        }
        _items[index] = item;
    }

    private void SiftDown(int index)
    {
        var item = _items[index];
        var half = Count / 2; // indexes at or above this have no children
        while (index < half)
        {
            var child = 2 * index + 1;
            var right = child + 1;
            if (right < Count && _comparer.Compare(_items[right], _items[child]) < 0) child = right;
            if (_comparer.Compare(_items[child], item) >= 0) break;
            _items[index] = _items[child];
            index = child;
        }
        _items[index] = item;
    }
}