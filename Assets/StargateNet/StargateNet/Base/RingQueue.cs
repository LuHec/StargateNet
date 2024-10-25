using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class RingQueue<T>
    {
        private T[] continer;
        private int m_Head = -1;
        private int m_Tail = -1;
        private int m_Size = 0;

        public int Size => m_Size;
        
        public RingQueue(int size = 8)
        {
            continer = new T[size];
        }

        public void Resize(int size)
        {
            if (size < m_Size)
            {
                int t = m_Size - size;
                while (t > 0)
                {
                    DeQueue();
                    t--;
                }
            }
            Array.Resize(ref continer, size);
        }

        public void EnQueue(T val)
        {
            if (m_Size == continer.Length)
            {
                DeQueue();
            }
            m_Tail = (m_Tail + 1) % continer.Length;
            continer[m_Tail] = val;   
            m_Size++;
            if (m_Size >= continer.Length) m_Size = continer.Length - 1;
        }

        public T DeQueue()
        {
            if (m_Size == 0) throw new Exception("Empty RingQueue");
            T res = continer[m_Head];
            m_Head = (m_Head + 1) % continer.Length;
            m_Size--;
            return res;
        }

        public T this[int index]
        {
            get
            {
                if (index > m_Size) throw new Exception("Out of index");
                return  continer[(m_Head + index) % continer.Length];
            }
        }
    }
}