using System.Collections.Generic;

namespace StargateNet
{
    public class InterestManager
    {
        public readonly List<Entity> simulationList = new(32);

        public InterestManager()
        {
            
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