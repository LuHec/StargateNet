using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class MinHeap<T>
    {
        private List<T> _heap = new();
        private readonly Func<T, float> _getPriority;

        public int Count => _heap.Count;
        public bool IsEmpty => _heap.Count == 0;

        public MinHeap(Func<T, float> getPriority)
        {
            _getPriority = getPriority;
        }

        public void Push(T item)
        {
            _heap.Add(item);
            HeapifyUp(_heap.Count - 1);
        }

        public T Pop()
        {
            if (IsEmpty)
                throw new InvalidOperationException("Heap is empty");

            T result = _heap[0];
            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            
            if (!IsEmpty)
                HeapifyDown(0);

            return result;
        }

        public T Peek()
        {
            if (IsEmpty)
                throw new InvalidOperationException("Heap is empty");
            return _heap[0];
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_getPriority(_heap[index]) >= _getPriority(_heap[parentIndex]))
                    break;
                
                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            while (true)
            {
                int smallest = index;
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;

                if (leftChild < _heap.Count && 
                    _getPriority(_heap[leftChild]) < _getPriority(_heap[smallest]))
                {
                    smallest = leftChild;
                }

                if (rightChild < _heap.Count && 
                    _getPriority(_heap[rightChild]) < _getPriority(_heap[smallest]))
                {
                    smallest = rightChild;
                }

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int i, int j)
        {
            T temp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = temp;
        }
    }
}