using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class RingQueue<T>
    {
        private T[] _continer;
        private int _mHead = -1;
        private int _mTail = -1;
        private int _mCount = 0;
        private int _size = 0;
        public int Count => _mCount;
        
        public RingQueue(int size = 8)
        {
            _continer = new T[size];
            _size = size;
        }

        public bool IsFull => _mCount == _size; 

        public void Resize(int size)
        {
            if (size < _mCount)
            {
                int t = _mCount - size;
                while (t > 0)
                {
                    DeQueue();
                    t--;
                }
            }
            Array.Resize(ref _continer, size);
        }

        public void EnQueue(T val)
        {
            if (_mCount == _continer.Length)
            {
                DeQueue();
            }
            _mTail = (_mTail + 1) % _continer.Length;
            _continer[_mTail] = val;   
            _mCount++;
            if (_mCount >= _continer.Length) _mCount = _continer.Length - 1;
        }

        public T DeQueue()
        {
            if (_mCount == 0) throw new Exception("Empty RingQueue");
            T res = _continer[_mHead];
            _mHead = (_mHead + 1) % _continer.Length;
            _mCount--;
            return res;
        }

        public void PopFront()
        {
            if (_mCount == 0) return;
            _mHead = (_mHead + 1) % _continer.Length;
            _mCount--;
        }

        public void Clear()
        {
            this._mHead = -1;
            this._mTail = -1;
            this._mCount = 0;
        }

        public T this[int index]
        {
            get
            {
                if (index > _mCount) throw new Exception("Out of index");
                return  _continer[(_mHead + index) % _continer.Length];
            }
        }

        public T Last => this[this._mCount];
    }
}