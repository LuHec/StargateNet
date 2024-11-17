using System.Collections.Generic;

namespace StargateNet
{
    public class InterestManager
    {
        internal StargateEngine engine;
        internal List<Entity> simulationList; // 实际会被执行的单元

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
                    if (currentSnapshot.worldObjectMeta[entity.worldMetaId].destroyed) break;
                    netScript.NetworkUpdate();
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
                    if (currentSnapshot.worldObjectMeta[entity.worldMetaId].destroyed) break;
                    netScript.NetworkRender();
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
                    if (currentSnapshot.worldObjectMeta[entity.worldMetaId].destroyed) break;
                    netScript.NetworkFixedUpdate();
                }
            }
        }
    }
}