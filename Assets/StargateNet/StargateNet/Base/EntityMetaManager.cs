using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class EntityMetaManager
    {
        internal readonly int maxEntites;
        private int _worldIdCounter = -1;
        private Queue<int> _recycledWorldIdx = new(32);

        public EntityMetaManager(int maxEntites)
        {
            this.maxEntites = maxEntites;
        }

        public int RequestWorldIdx()
        {
            if (this._recycledWorldIdx.Count == 0)
            {
                if (this._worldIdCounter == this.maxEntites)
                    throw new Exception("Entities count is out of range");
                return ++this._worldIdCounter;
            }
            else return this._recycledWorldIdx.Dequeue();
        }

        public void ReturnWorldIdx(int idx)
        {
            this._recycledWorldIdx.Enqueue(idx);
        }
    }
}