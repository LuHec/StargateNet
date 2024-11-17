using System;
using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class EntityMetaManager
    {
        internal readonly int maxEntities;
        internal StargateEngine engine;
        internal Dictionary<int, NetworkObjectMeta> changedMetas = new(32);
        private int _worldIdCounter = -1;
        private Queue<int> _recycledWorldIdx = new(32);

        public EntityMetaManager(int maxEntities, StargateEngine engine)
        {
            this.maxEntities = maxEntities;
            this.engine = engine;
        }

        public int RequestWorldIdx()
        {
            if (this._recycledWorldIdx.Count == 0)
            {
                if (this._worldIdCounter == this.maxEntities)
                    throw new Exception("Entities count is out of range");
                return ++this._worldIdCounter;
            }
            else return this._recycledWorldIdx.Dequeue();
        }

        public void ReturnWorldIdx(int idx)
        {
            this._recycledWorldIdx.Enqueue(idx);
        }

        // ---------- Client ---------- //
        public unsafe void OnMetaChanged()
        {
            Simulation simulation = this.engine.Simulation;
            foreach (var pair in this.changedMetas)
            {
                int metaId = pair.Key;
                NetworkObjectMeta remoteMeta = pair.Value;
                // TODO:判断id是不是相同的，如果不是就生成/销毁
                NetworkObjectMeta localMeta = this.engine.WorldState.CurrentSnapshot.worldObjectMeta[metaId];
                if (remoteMeta.networkId != localMeta.networkId || remoteMeta.destroyed)
                {
                    // 移除Current Entity
                    this.engine.ClientDestroy(localMeta.networkId);
                    simulation.DrainPaddingRemovedEntity();
                }
                else if (localMeta.networkId != remoteMeta.networkId || localMeta.destroyed == false)
                {
                    this.engine.ClinetSpawn(remoteMeta.networkId, metaId, remoteMeta.prefabId, Vector3.zero,
                        Quaternion.identity);
                    
                }
            }

            this.changedMetas.Clear();
        }
    }
}