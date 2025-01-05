using System;
using System.Collections.Generic;
using UnityEngine;

public class PriorityQueue<T> where T : IComparable<T>
{
    private List<T> heap;

    public PriorityQueue()
    {
        heap = new List<T>();
    }

    // 向队列中添加元素
    public void Enqueue(T item)
    {
        heap.Add(item); // 将新元素添加到堆的末尾
        HeapifyUp(heap.Count - 1); // 保持堆的性质
    }

    // 从队列中移除并返回最小的元素（根节点）
    public T Dequeue()
    {
        if (heap.Count == 0) throw new InvalidOperationException("Queue is empty.");

        // 将根节点（最小值）与最后一个元素交换
        T root = heap[0];
        heap[0] = heap[^1];
        heap.RemoveAt(heap.Count - 1); // 删除最后一个元素

        // 重新调整堆
        HeapifyDown(0);

        return root;
    }

    // 查看队列中最小的元素（根节点），不移除
    public T Peek()
    {
        if (heap.Count == 0) throw new InvalidOperationException("Queue is empty.");
        return heap[0];
    }

    // 判断队列是否为空
    public bool IsEmpty()
    {
        return heap.Count == 0;
    }

    // 获取队列中的元素数量
    public int Count()
    {
        return heap.Count;
    }

    // 通过索引获取堆中的元素（调试用）
    public T GetAt(int index)
    {
        return heap[index];
    }

    // 将元素上浮到正确的位置，保持堆的性质
    private void HeapifyUp(int index)
    {
        int parentIndex = (index - 1) / 2;
        if (index > 0 && heap[index].CompareTo(heap[parentIndex]) < 0)
        {
            Swap(index, parentIndex);
            HeapifyUp(parentIndex);
        }
    }

    // 将元素下沉到正确的位置，保持堆的性质
    private void HeapifyDown(int index)
    {
        int leftChildIndex = 2 * index + 1;
        int rightChildIndex = 2 * index + 2;
        int smallest = index;

        if (leftChildIndex < heap.Count && heap[leftChildIndex].CompareTo(heap[smallest]) < 0)
        {
            smallest = leftChildIndex;
        }

        if (rightChildIndex < heap.Count && heap[rightChildIndex].CompareTo(heap[smallest]) < 0)
        {
            smallest = rightChildIndex;
        }

        if (smallest != index)
        {
            Swap(index, smallest);
            HeapifyDown(smallest);
        }
    }

    // 交换两个元素的位置
    private void Swap(int i, int j)
    {
        (heap[i], heap[j]) = (heap[j], heap[i]);
    }
}