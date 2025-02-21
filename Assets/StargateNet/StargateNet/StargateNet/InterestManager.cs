using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class InterestManager
    {
        internal StargateEngine engine;
        internal List<Entity> simulationList; // 实际会被执行的单元
        internal Dictionary<InterestBlock, List<NetworkObjectRef>> interestBlockMap = new(128); // 每个InterestBlock对应的Entity列表,每帧都会重新构造
        private Queue<List<NetworkObjectRef>> areaPool = new(128);

        private int boundX = 100;
        private int boundY = 100;
        private int boundZ = 100;
        Vector3 worldPoint = new Vector3(0, 0, 0);


        public InterestManager(int maxEntities, StargateEngine engine)
        {
            this.engine = engine;
            simulationList = new List<Entity>(maxEntities);
        }
        
        public unsafe void ExecuteNetworkUpdate()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entityObject.NetworkScripts)
                {
                    if (currentSnapshot.IsObjectDestroyed(entity.worldMetaId)) break;
                    netScript.NetworkUpdate(this.engine.SgNetworkGalaxy);
                }
            }
        }

        public unsafe void ExecuteNetworkRender()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entityObject.NetworkScripts)
                {
                    if (currentSnapshot.IsObjectDestroyed(entity.worldMetaId)) break;
                    netScript.NetworkRender(this.engine.SgNetworkGalaxy);
                }
            }
        }
        
        public unsafe void ExecuteNetworkFixedUpdate()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entityObject.NetworkScripts)
                {
                    if (currentSnapshot.IsObjectDestroyed(entity.worldMetaId)) break;
                    netScript.NetworkFixedUpdate(this.engine.SgNetworkGalaxy);
                }
            }
        }

        public void SerializeToNetcode()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entityObject.NetworkScripts)
                {
                    if (currentSnapshot.IsObjectDestroyed(entity.worldMetaId)) break;
                    netScript.SerializeToNetcode();
                }
            }
        }
        
        public void DeserializeToGameCode()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entityObject.NetworkScripts)
                {
                    if (currentSnapshot.IsObjectDestroyed(entity.worldMetaId)) break;
                    netScript.DeserializeToGameCode();
                }
            }
        }

        internal void CalculateAOI()
        {
            foreach (var list in interestBlockMap.Values)
            {
                list.Clear();
                areaPool.Enqueue(list);
            }
            interestBlockMap.Clear();

            foreach (var entity in simulationList)
            {
                Transform transform = entity.entityObject.transform;
                // 使用向下取整的方式处理负数坐标
                InterestBlock block = new InterestBlock
                {
                    xIndex = (int)Mathf.Floor(transform.position.x / (float)boundX),
                    yIndex = (int)Mathf.Floor(transform.position.y / (float)boundY),
                    zIndex = (int)Mathf.Floor(transform.position.z / (float)boundZ)
                };

                if (!interestBlockMap.TryGetValue(block, out var entityList))
                {
                    entityList = areaPool.Count > 0 
                        ? areaPool.Dequeue() 
                        : new List<NetworkObjectRef>(16);
                    interestBlockMap[block] = entityList;
                }
                
                entityList.Add(entity.networkId);
            }
        }

        // 建议添加清理方法
        public void Clear()
        {
            foreach (var list in interestBlockMap.Values)
            {
                list.Clear();
                areaPool.Enqueue(list);
            }
            interestBlockMap.Clear();
            simulationList.Clear();
        }
    }
}