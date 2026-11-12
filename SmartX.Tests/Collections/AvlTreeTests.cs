using SmartX.Api.Domain.Collections;

namespace SmartX.Tests.Collections;

public class AvlTreeTests
{
    private static double WorstAvlHeight(int n) => 1.4405 * Math.Log2(n + 2);

    [Fact]
    public void EmptyTree_HasNoEntries()
    {
        var tree = new AvlTree<int, string>();

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Height);
        Assert.False(tree.TryGetMin(out _, out _));
        Assert.False(tree.TryGetMax(out _, out _));
        Assert.False(tree.TryRemoveMin(out _, out _));
        Assert.Empty(tree.Range(0, 100));
        Assert.True(tree.IsValid(out var problem), problem);
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void SortedInsertions_StayBalanced_AtEverySize(int n)
    {
        // Timestamps arrive in increasing order: the worst case for a plain BST, which would degrade to a linked list.
        var tree = new AvlTree<int, int>();
        for (var i = 0; i < n; i++) tree.Add(i, i);

        Assert.Equal(n, tree.Count);
        Assert.True(tree.Height <= WorstAvlHeight(n), $"Height {tree.Height} exceeds the AVL bound for {n} nodes.");
        Assert.True(tree.Height >= tree.MinimumPossibleHeight);
    }

    [Fact]
    public void SortedInsertions_ProduceAValidTree()
    {
        var tree = new AvlTree<int, int>();
        for (var i = 0; i < 5_000; i++) tree.Add(i, i);

        Assert.True(tree.IsValid(out var problem), problem);
    }

    [Fact]
    public void DescendingInsertions_StayBalanced()
    {
        var tree = new AvlTree<int, int>();
        for (var i = 5_000; i > 0; i--) tree.Add(i, i);

        Assert.True(tree.IsValid(out var problem), problem);
        Assert.True(tree.Height <= WorstAvlHeight(5_000));
    }

    [Fact]
    public void RandomInsertions_StayValidThroughout()
    {
        var rnd = new Random(2026);
        var tree = new AvlTree<int, int>();

        for (var i = 1; i <= 4_000; i++)
        {
            tree.Add(rnd.Next(0, 1_000_000), i);
            if (i % 250 == 0)
                Assert.True(tree.IsValid(out var problem), $"After {i} inserts: {problem}");
        }

        Assert.Equal(4_000, tree.Count);
    }

    [Fact]
    public void LeftRightZigZag_IsStraightenedByADoubleRotation()
    {
        var tree = new AvlTree<int, int>();
        tree.Add(3, 3);
        tree.Add(1, 1);
        tree.Add(2, 2);

        Assert.Equal(2, tree.Height);
        Assert.Equal(new[] { 1, 2, 3 }, tree.InOrder().Select(e => e.Key));
        Assert.True(tree.IsValid(out var problem), problem);
    }

    [Fact]
    public void RightLeftZigZag_IsStraightenedByADoubleRotation()
    {
        var tree = new AvlTree<int, int>();
        tree.Add(1, 1);
        tree.Add(3, 3);
        tree.Add(2, 2);

        Assert.Equal(2, tree.Height);
        Assert.Equal(new[] { 1, 2, 3 }, tree.InOrder().Select(e => e.Key));
        Assert.True(tree.IsValid(out var problem), problem);
    }

    [Fact]
    public void DuplicateKeys_KeepInsertionOrder()
    {
        var tree = new AvlTree<int, string>();
        tree.Add(5, "a");
        tree.Add(5, "b");
        tree.Add(5, "c");
        tree.Add(1, "x");
        tree.Add(9, "y");

        Assert.Equal(new[] { "x", "a", "b", "c", "y" }, tree.InOrder().Select(e => e.Value));
        Assert.Equal(5, tree.Count);
        Assert.True(tree.IsValid(out var problem), problem);
    }

    [Fact]
    public void Range_MatchesLinq_OnRandomDataWithDuplicates()
    {
        var rnd = new Random(1234);
        var items = Enumerable.Range(0, 3_000).Select(i => (Key: rnd.Next(0, 500), Id: i)).ToList();

        var tree = new AvlTree<int, int>();
        foreach (var (key, id) in items) tree.Add(key, id);

        for (var q = 0; q < 200; q++)
        {
            var a = rnd.Next(-20, 520);
            var b = rnd.Next(-20, 520);
            var (low, high) = a <= b ? (a, b) : (b, a);

            var expected = items.Where(x => x.Key >= low && x.Key <= high)
                .OrderBy(x => x.Key)          // OrderBy is stable, so duplicates stay in insertion order
                .Select(x => x.Id)
                .ToList();
            var actual = tree.Range(low, high).Select(e => e.Value).ToList();

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void RangeDescending_IsTheExactMirrorOfRange()
    {
        var rnd = new Random(99);
        var tree = new AvlTree<int, int>();
        for (var i = 0; i < 2_000; i++) tree.Add(rnd.Next(0, 300), i);

        for (var q = 0; q < 100; q++)
        {
            var a = rnd.Next(-10, 310);
            var b = rnd.Next(-10, 310);
            var (low, high) = a <= b ? (a, b) : (b, a);

            var ascending = tree.Range(low, high).Select(e => e.Value).ToList();
            var descending = tree.RangeDescending(low, high).Select(e => e.Value).ToList();

            ascending.Reverse();
            Assert.Equal(ascending, descending);
        }
    }

    [Fact]
    public void Range_IncludesBothBoundaries()
    {
        var tree = new AvlTree<int, int>();
        for (var i = 1; i <= 10; i++) tree.Add(i, i);

        Assert.Equal(new[] { 3, 4, 5, 6 }, tree.Range(3, 6).Select(e => e.Key));
        Assert.Equal(new[] { 3, 4, 5, 6 }, tree.RangeDescending(3, 6).Select(e => e.Key).OrderBy(k => k));
    }

    [Fact]
    public void Range_WithNothingInsideOrReversedBounds_IsEmpty()
    {
        var tree = new AvlTree<int, int>();
        for (var i = 1; i <= 10; i++) tree.Add(i * 10, i);

        Assert.Empty(tree.Range(11, 19));     // gap between keys
        Assert.Empty(tree.Range(200, 300));   // beyond the end
        Assert.Empty(tree.Range(-50, -1));    // before the start
        Assert.Empty(tree.Range(60, 20));     // from > to
        Assert.Empty(tree.RangeDescending(60, 20));
    }

    [Fact]
    public void Range_OnTimestamps_AnswersTheBriefsExampleQuery()
    {
        // "Moisture readings for Node A between 14:00 and 16:00" without scanning a flat list.
        var day = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
        var tree = new AvlTree<DateTime, double>();
        for (var minute = 0; minute < 24 * 60; minute++) tree.Add(day.AddMinutes(minute), minute);

        var window = tree.Range(day.AddHours(14), day.AddHours(16)).ToList();

        Assert.Equal(121, window.Count); // 14:00 and 16:00 both inclusive
        Assert.Equal(day.AddHours(14), window[0].Key);
        Assert.Equal(day.AddHours(16), window[^1].Key);
        Assert.Equal(840, window[0].Value);
    }

    [Fact]
    public void TryGetMinAndMax_ReturnTheExtremes()
    {
        var tree = new AvlTree<int, string>();
        foreach (var k in new[] { 50, 20, 80, 10, 90, 30 }) tree.Add(k, $"v{k}");

        Assert.True(tree.TryGetMin(out var minKey, out var minValue));
        Assert.Equal(10, minKey);
        Assert.Equal("v10", minValue);

        Assert.True(tree.TryGetMax(out var maxKey, out var maxValue));
        Assert.Equal(90, maxKey);
        Assert.Equal("v90", maxValue);
    }

    [Fact]
    public void TryRemoveMin_DrainsInAscendingOrder_AndStaysBalanced()
    {
        var rnd = new Random(7);
        var tree = new AvlTree<int, int>();
        foreach (var k in Enumerable.Range(1, 500).OrderBy(_ => rnd.Next())) tree.Add(k, k);

        var removed = new List<int>();
        while (tree.TryRemoveMin(out var key, out _))
        {
            removed.Add(key);
            if (removed.Count % 50 == 0)
                Assert.True(tree.IsValid(out var problem), $"After removing {removed.Count}: {problem}");
        }

        Assert.Equal(Enumerable.Range(1, 500), removed);
        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Height);
    }

    [Fact]
    public void MinimumPossibleHeight_IsTheCeilingOfLog2()
    {
        var tree = new AvlTree<int, int>();
        Assert.Equal(0, tree.MinimumPossibleHeight);

        for (var i = 0; i < 7; i++) tree.Add(i, i);
        Assert.Equal(3, tree.MinimumPossibleHeight); // 7 nodes fit exactly in a perfect tree of height 3

        tree.Add(7, 7);
        Assert.Equal(4, tree.MinimumPossibleHeight);
    }
}
