namespace ForcePlateDevTool.Services;

public sealed class BoundedQueue<T>
{
    private readonly object _gate = new();
    private readonly Queue<T> _queue = new();
    private readonly int _capacity;
    private long _droppedCount;

    public BoundedQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public long DroppedCount => Interlocked.Read(ref _droppedCount);

    public void Enqueue(T item)
    {
        lock (_gate)
        {
            if (_queue.Count >= _capacity)
            {
                _queue.Dequeue();
                Interlocked.Increment(ref _droppedCount);
            }

            _queue.Enqueue(item);
        }
    }

    public bool TryDequeue(out T item)
    {
        lock (_gate)
        {
            if (_queue.Count == 0)
            {
                item = default!;
                return false;
            }

            item = _queue.Dequeue();
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _queue.Clear();
        }
    }
}