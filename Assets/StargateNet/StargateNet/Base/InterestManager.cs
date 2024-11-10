using System.Collections.Generic;

namespace StargateNet
{
    public class InterestManager
    {
        internal List<Entity> simulationList; // 实际会被执行的单元

        public InterestManager(int maxEntities)
        {
            simulationList = new List<Entity>(maxEntities);
        }
        
        public void ExecuteNetworkUpdate()
        {
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entity.NetworkScripts)
                {
                    netScript.NetworkUpdate();
                }
            }
        }

        public void ExecuteNetworkRender()
        {
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entity.NetworkScripts)
                {
                    netScript.NetworkRender();
                }
            }
        }
        
        public void ExecuteNetworkFixedUpdate()
        {
            foreach (var entity in simulationList)
            {
                foreach (var netScript in entity.entity.NetworkScripts)
                {
                    netScript.NetworkFixedUpdate();
                }
            }
        }
    }
}