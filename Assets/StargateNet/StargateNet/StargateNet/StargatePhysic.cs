using UnityEngine;

namespace StargateNet
{
    public class StargatePhysic
    {
        internal bool IsPhysic2D { private set; get; }
        internal StargateEngine Engine { private set; get; }
        internal PhysicsScene Physics { private set; get; }

        public StargatePhysic(StargateEngine engine, bool isPhysic2D, PhysicsScene physics)
        {
            this.Physics = physics;
            this.Engine = engine;
            this.IsPhysic2D = isPhysic2D;
        }

        internal void Simulate(float deltaTime)
        {
            if (this.IsPhysic2D)
                Physics2D.Simulate(deltaTime);
            else
                Physics.Simulate(deltaTime);
        }

        
    }
}