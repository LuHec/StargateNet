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
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var pair in this.changedMetas)
            {
                int metaId = pair.Key;
                NetworkObjectMeta remoteMeta = pair.Value;
                // 与服务端id不同或者服务端删除了这个物体，客户端销毁
                NetworkObjectMeta localMeta = currentSnapshot.GetWorldObjectMeta(metaId);
                if (remoteMeta.networkId != localMeta.networkId || remoteMeta.destroyed)
                {
                    this.engine.ClientDestroy(localMeta.networkId);
                }

                // 如果服务端生成新的物体，客户端也生成
                if (remoteMeta.networkId != localMeta.networkId && !remoteMeta.destroyed)
                {
                    this.engine.ClientSpawn(remoteMeta.networkId, metaId, remoteMeta.prefabId, remoteMeta.inputSource,
                        Vector3.zero,
                        Quaternion.identity);
                }
            }

            this.changedMetas.Clear();
        }
    }
}