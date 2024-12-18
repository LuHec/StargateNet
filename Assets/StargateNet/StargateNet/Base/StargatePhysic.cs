using UnityEngine;

namespace StargateNet
{
    public class StargatePhysic
    {
        public bool IsPhysic2D { private set; get; }

        public StargatePhysic(bool isPhysic2D)
        {
            this.IsPhysic2D = isPhysic2D;
        }

        public void Simulate(float deltaTime)
        {
            if (this.IsPhysic2D)
                Physics2D.Simulate(deltaTime);
            else
                Physics.Simulate(deltaTime);
        }
    }
}