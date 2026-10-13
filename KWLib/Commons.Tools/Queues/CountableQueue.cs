
namespace KW.Commons.Tools.Queues;

/// <summary>
/// 限长队列 先入先出 填满后追加自动删除较早的数据
/// </summary>
/// <typeparam name="T"></typeparam>
public class CountableQueue<T>
{
    protected int _HeadIndex, _TailIndex, _TotalCount, _MaxCount;
    protected T[] _Values = new T[0];
    protected object _LockObj = new object();

    public int Count => _TotalCount;

    public bool IsFull => (Count == _MaxCount);

    public CountableQueue(int maxLength)
    {
        _Values = new T[maxLength];
        _HeadIndex = _TotalCount = 0;
        _TailIndex = -1;
        _MaxCount = maxLength;
    }

    public CountableQueue(int maxLength, IEnumerable<T> collection)
    {
        _Values = new T[maxLength];
        _TailIndex = -1;
        _HeadIndex = _TotalCount = 0;
        _MaxCount = maxLength;

        foreach (T item in collection)
        {
            Enqueue(item);
        }
    }

    public void Clear()
    {
        lock (_LockObj)
        {
            _HeadIndex = _TotalCount = 0;
            _TailIndex = -1;
        }
    }

    public T[] ToArray()
    {
        lock (_LockObj)
        {
            if (_TotalCount == 0)
            {
                return new T[0];
            }

            T[] array = new T[_TotalCount];

            if (_HeadIndex <= _TailIndex)
            {
                Array.Copy(_Values, _HeadIndex, array, 0, _TotalCount);
            }
            else if (_HeadIndex > _TailIndex)
            {
                Array.Copy(_Values, _HeadIndex, array, 0, _MaxCount - _HeadIndex);
                Array.Copy(_Values, 0, array, _MaxCount - _HeadIndex, _TailIndex + 1);
            }

            return array;
        }
    }

    public T Max()
    {
        lock (_LockObj)
        {
            return _Values.Max();
        }
    }

    public virtual void Enqueue(T data)
    {
        lock (_LockObj)
        {
            //队列已满 从头部移除一个
            if (_TotalCount == _Values.Length)
            {
                _HeadIndex++;
                _TotalCount--;
            }

            if (_HeadIndex >= _MaxCount)
            {
                _HeadIndex = 0;
            }

            //尾部超出总长 回到头部
            if (++_TailIndex >= _MaxCount)
            {
                _TailIndex = 0;
            }

            _Values[_TailIndex] = data;
            _TotalCount++;
        }
    }

    public virtual T Dequeue()
    {
        lock (_LockObj)
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Empty Queue");
            }

            var result = _Values[_HeadIndex];

            //头部移除
            _HeadIndex++;
            _TotalCount--;

            //队尾归0
            if (_HeadIndex == _Values.Length)
            {
                _HeadIndex = 0;
            }

            return result;
        }
    }

    public virtual T Peek()
    {
        return _Values[_HeadIndex];
    }

    /// <summary>
    /// 一次性写入多个元素
    /// </summary>
    /// <param name="data">要写入的数据数组</param>
    /// <param name="offset">数据偏移量</param>
    /// <param name="count">要写入的元素数量</param>
    public void Write(T[] data, int offset, int count)
    {
        lock (_LockObj)
        {
            if (count > _MaxCount - _TotalCount)
            {
                // 缓冲区不足，丢弃旧数据
                int discard = count - (_MaxCount - _TotalCount);
                for (int i = 0; i < discard; i++)
                {
                    Dequeue();
                }
            }

            for (int i = 0; i < count; i++)
            {
                Enqueue(data[offset + i]);
            }
        }
    }

    /// <summary>
    /// 一次性读取所有元素并清空队列
    /// </summary>
    /// <returns>所有元素组成的数组</returns>
    public T[] ReadAll()
    {
        lock (_LockObj)
        {
            T[] result = ToArray();
            Clear();
            return result;
        }
    }
}
