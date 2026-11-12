using SmartX.Api.Domain.Collections;

namespace SmartX.Tests.Collections;

public class RingBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonPositiveCapacity_IsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(capacity));
    }

    [Fact]
    public void PartiallyFilled_ReturnsItemsOldestFirst()
    {
        var buffer = new RingBuffer<int>(5);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(new[] { 1, 2 }, buffer.ToArray());
    }

    [Fact]
    public void WhenFull_TheOldestEntryIsOverwritten()
    {
        var buffer = new RingBuffer<int>(3);
        for (var i = 1; i <= 5; i++) buffer.Add(i);

        Assert.Equal(3, buffer.Count);
        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(new[] { 3, 4, 5 }, buffer.ToArray());
    }

    [Fact]
    public void Enumeration_MatchesToArray()
    {
        var buffer = new RingBuffer<string>(4);
        foreach (var s in new[] { "a", "b", "c", "d", "e", "f" }) buffer.Add(s);

        Assert.Equal(buffer.ToArray(), buffer.ToList());
    }
}
