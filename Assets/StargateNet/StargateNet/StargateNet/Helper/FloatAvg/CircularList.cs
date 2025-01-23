namespace StargateNet
{
    internal class CircularList<T>
    {
        private int m_First;
        private int m_Count;
        private T[] m_Elements;

        public CircularList(int capacity) => this.m_Elements = new T[capacity];

        public int Capacity => this.m_Elements.Length;

        public int Count => this.m_Count;

        public void Add(T item)
        {
            this.m_Elements[(this.m_First + this.m_Count) % this.m_Elements.Length] = item;
            if (this.m_Count == this.m_Elements.Length)
                this.m_First = (this.m_First + 1) % this.m_Elements.Length;
            else
                ++this.m_Count;
        }

        public void Clear()
        {
            this.m_First = 0;
            this.m_Count = 0;
        }

        public T this[int i]
        {
            get => this.m_Elements[(this.m_First + i) % this.m_Elements.Length];
            set => this.m_Elements[(this.m_First + i) % this.m_Elements.Length] = value;
        }

        public T[] GetArray() => this.m_Elements;

        public int HeadIndex => this.m_First;

        public void Reset(int headIndex, int count)
        {
            this.m_First = headIndex;
            this.m_Count = count;
        }
    }
}