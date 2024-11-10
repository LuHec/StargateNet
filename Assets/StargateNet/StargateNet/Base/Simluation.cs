using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal StargateEngine engine;

        internal Dictionary<NetworkObjectRef, Entity>
            EntitiesTable { private set; get; } // 记录当前的Entities，但并不直接执行这些实例

        internal unsafe int* networkRefMap;
        internal List<Entity> paddingToAddEntities = new(32); // 待加入模拟的实体，用于延迟添加到模拟列表。Entity会在这之前就被添加到table中
        internal List<Entity> paddingToRemoveEntities = new(32);
        protected Queue<SimulationInput> inputPool = new();


        internal Simulation(StargateEngine engine)
        {
            this.engine = engine;
        }

        internal unsafe Entity CreatEntity(NetworkObject networkObject, NetworkObjectRef networkObjectRef)
        {
            // 用本帧的资源来分配
            StargateAllocator allocator = this.engine.WorldState.CurrentSnapshot.networkState;
            NetworkBehavior[] networkBehaviors = networkObject.GetComponents<NetworkBehavior>();
            Entity entity = new Entity(networkObjectRef, this.engine, networkObject);
            int byteSize = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                byteSize += networkBehaviors[i].StateBlockSize;
            }

            // 给每个脚本切割内存和bitmap
            int offset = 0;
            if (allocator.AddPool(networkObjectRef.refValue, byteSize * 2))
            {
                int* data = (int*)allocator.pools[networkObjectRef.refValue].data;
                int* state = data;
                int* bitmap = data + byteSize / 4;
                entity.Initialize(state, bitmap, byteSize);
                for (int i = 0; i < networkBehaviors.Length; i++)
                {
                    networkBehaviors[i].StateBlock = data + offset;
                    offset += networkBehaviors[i].StateBlockSize / 4;
                }
            }

            return entity;
        }

        internal void AddEntity(NetworkObject networkObject, NetworkObjectRef networkObjectRef)
        {
            Entity entity = this.CreatEntity(networkObject, networkObjectRef);
        }

        internal void RemoveEntity(NetworkObjectRef networkObjectRef)
        {
        }

        internal virtual void PreUpdate()
        {
        }

        internal virtual void PreFixedUpdate()
        {
        }

        internal virtual void PostFixedUpdate()
        {
        }

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        internal void FixedUpdate()
        {
            // 对于客户端，先在这里处理回滚，然后再模拟下一帧
            this.PreFixedUpdate();
            this.ExecuteNetworkFixedUpdate();
            this.PostFixedUpdate();
        }

        internal void ExecuteNetworkUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkUpdate();
        }

        internal void ExecuteNetworkRender()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkRender();
        }

        internal void ExecuteNetworkFixedUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkFixedUpdate();
        }

        internal SimulationInput CreateInput(Tick srvTick, Tick targetTick)
        {
            if (inputPool.Count == 0)
            {
                inputPool.Enqueue(new SimulationInput());
            }

            SimulationInput resInput = inputPool.Dequeue();
            resInput.srvTick = srvTick;
            resInput.targetTick = targetTick;
            return resInput;
        }

        internal void RecycleInput(SimulationInput input)
        {
            this.inputPool.Enqueue(input);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="networkEntity"></param>
        internal virtual void AddToSimulation(INetworkEntity networkEntity)
        {
        }
    }
}