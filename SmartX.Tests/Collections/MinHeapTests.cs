using SmartX.Api.Domain.Collections;

namespace SmartX.Tests.Collections;

public class MinHeapTests
{
    [Fact]
    public void Pop_ReturnsItemsInAscendingOrder()
    {
        var rnd = new Random(5);
        var values = Enumerable.Range(0, 2_000).Select(_ => rnd.Next(-1_000, 1_000)).ToList();

        var heap = new MinHeap<int>();
        foreach (var v in values) heap.Push(v);

        var popped = new List<int>();
        while (heap.TryPop(out var next)) popped.Add(next);

        Assert.Equal(values.OrderBy(v => v), popped);
        Assert.True(heap.IsEmpty);
    }

    [Fact]
    public void SmallInitialCapacity_GrowsAsNeeded()
    {
        var heap = new MinHeap<int>(capacity: 1);
        for (var i = 100; i > 0; i--) heap.Push(i);

        Assert.Equal(100, heap.Count);
        Assert.Equal(1, heap.Pop());
        Assert.Equal(2, heap.Pop());
    }

    [Fact]
    public void EmptyHeap_PopAndPeekThrow_AndTryPopReturnsFalse()
    {
        var heap = new MinHeap<int>();

        Assert.Throws<InvalidOperationException>(() => heap.Pop());
        Assert.Throws<InvalidOperationException>(() => heap.Peek());
        Assert.False(heap.TryPop(out var item));
        Assert.Equal(0, item);
    }

    [Fact]
    public void Peek_ReturnsTheMinimumWithoutRemovingIt()
    {
        var heap = new MinHeap<int>();
        heap.Push(8);
        heap.Push(3);
        heap.Push(5);

        Assert.Equal(3, heap.Peek());
        Assert.Equal(3, heap.Peek());
        Assert.Equal(3, heap.Count);
    }

    [Fact]
    public void DuplicateValues_AreAllReturned()
    {
        var heap = new MinHeap<int>();
        foreach (var v in new[] { 4, 2, 4, 2, 4 }) heap.Push(v);

        var popped = new List<int>();
        while (heap.TryPop(out var next)) popped.Add(next);

        Assert.Equal(new[] { 2, 2, 4, 4, 4 }, popped);
    }

    [Fact]
    public void CustomComparer_CanTurnItIntoAMaxHeap()
    {
        var heap = new MinHeap<int>(comparer: Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (var v in new[] { 3, 9, 1, 7 }) heap.Push(v);

        Assert.Equal(9, heap.Pop());
        Assert.Equal(7, heap.Pop());
    }

    [Fact]
    public void InterleavedPushAndPop_AlwaysReturnsTheCurrentMinimum()
    {
        var rnd = new Random(42);
        var heap = new MinHeap<int>();
        var reference = new List<int>();

        for (var step = 0; step < 3_000; step++)
        {
            if (reference.Count == 0 || rnd.NextDouble() < 0.6)
            {
                var v = rnd.Next(0, 500);
                heap.Push(v);
                reference.Add(v);
            }
            else
            {
                var expected = reference.Min();
                reference.Remove(expected);
                Assert.Equal(expected, heap.Pop());
            }
        }

        Assert.Equal(reference.Count, heap.Count);
    }

    [Fact]
    public void Tuples_OrderByTheirFirstThenSecondComponent()
    {
        var heap = new MinHeap<(double Cost, string Id)>();
        heap.Push((2.5, "b"));
        heap.Push((1.0, "z"));
        heap.Push((2.5, "a"));

        Assert.Equal((1.0, "z"), heap.Pop());
        Assert.Equal((2.5, "a"), heap.Pop());
        Assert.Equal((2.5, "b"), heap.Pop());
    }
}
