using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Tests;

public class BoundedQueueTests
{
    [Fact]
    public void Enqueue_WhenFull_DropsOldestItem()
    {
        var queue = new BoundedQueue<int>(capacity: 2);

        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);

        Assert.Equal(1, queue.DroppedCount);

        Assert.True(queue.TryDequeue(out int first));
        Assert.Equal(2, first);

        Assert.True(queue.TryDequeue(out int second));
        Assert.Equal(3, second);

        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void Clear_RemovesQueuedItems()
    {
        var queue = new BoundedQueue<int>(capacity: 2);

        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Clear();

        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void Constructor_RejectsInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BoundedQueue<int>(0));
    }
}