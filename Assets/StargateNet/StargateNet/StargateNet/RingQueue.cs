using System;
using System.Collections;
using System.Collections.Generic;

namespace StargateNet
{
    internal class RingQueue<T> : IEnumerable<T>, IEnumerable
    {
        private readonly T[] _elements;
        private int _start;
        private int _end;
        private int _count;
        private readonly int _capacity;

        public T this[int i] => this._elements[(this._start + i) % this._capacity];

        public RingQueue(int count)
        {
            this._elements = new T[count];
            this._capacity = count;
        }

        public void Enqueue(T element)
        {
            if (this._count == this._capacity)
                throw new ArgumentException();
            this._elements[this._end] = element;
            this._end = (this._end + 1) % this._capacity;
            ++this._count;
        }

        public void FastClear()
        {
            this._start = 0;
            this._end = 0;
            this._count = 0;
        }

        public int Count => this._count;

        public T First => this._elements[this._start];

        public T Last => this._elements[(this._start + this._count - 1) % this._capacity];

        public bool IsFull => this._count == this._capacity;

        public void RemoveFromStart(int count)
        {
            if (count > this._capacity || count > this._count)
                throw new ArgumentException();
            this._start = (this._start + count) % this._capacity;
            this._count -= count;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int counter = this._start; counter != this._end; counter = (counter + 1) % this._capacity)
                yield return this._elements[counter];
        }

        IEnumerator IEnumerable.GetEnumerator() => (IEnumerator) this.GetEnumerator();
    }
}