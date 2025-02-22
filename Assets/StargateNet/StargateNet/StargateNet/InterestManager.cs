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

        internal readonly int boundX = 10;
        internal readonly int boundY = 10;
        internal readonly int boundZ = 10;
        internal readonly int VIEW_RANGE = 500; // 可视范围
        internal const float DRAW_DURATION = 200000000f; // 绘制持续时间

        public InterestManager(int maxEntities, StargateEngine engine)
        {
            this.engine = engine;
            this.simulationList = new List<Entity>(maxEntities);
            this.boundX = this.boundY = this.boundZ = engine.ConfigData.AoIBound;
            this.VIEW_RANGE = engine.ConfigData.WorldSize;
            DrawInterestGrid();
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

        private void DrawInterestGrid()
        {
            Color gridColor = Color.green; 
            int halfRange = VIEW_RANGE / 2;
            int blocksX = halfRange / boundX;
            int blocksY = halfRange / boundY;
            int blocksZ = halfRange / boundZ;

            // 绘制X方向的线
            for (int z = -blocksZ; z <= blocksZ; z++)
            {
                for (int y = -blocksY; y <= blocksY; y++)
                {
                    Vector3 start = new Vector3(-halfRange, y * boundY, z * boundZ);
                    Vector3 end = new Vector3(halfRange, y * boundY, z * boundZ);
                    GizmoTimerDrawer.Instance.DrawLineWithTimer(start, end, DRAW_DURATION, gridColor);
                }
            }

            // 绘制Y方向的线
            for (int x = -blocksX; x <= blocksX; x++)
            {
                for (int z = -blocksZ; z <= blocksZ; z++)
                {
                    Vector3 start = new Vector3(x * boundX, -halfRange, z * boundZ);
                    Vector3 end = new Vector3(x * boundX, halfRange, z * boundZ);
                    GizmoTimerDrawer.Instance.DrawLineWithTimer(start, end, DRAW_DURATION, gridColor);
                }
            }

            // 绘制Z方向的线
            for (int x = -blocksX; x <= blocksX; x++)
            {
                for (int y = -blocksY; y <= blocksY; y++)
                {
                    Vector3 start = new Vector3(x * boundX, y * boundY, -halfRange);
                    Vector3 end = new Vector3(x * boundX, y * boundY, halfRange);
                    GizmoTimerDrawer.Instance.DrawLineWithTimer(start, end, DRAW_DURATION, gridColor);
                }
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