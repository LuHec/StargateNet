using System.Collections.Generic;

namespace StargateNet
{
    /// <summary>
    /// 用于存储每一帧客户端所在的区块
    /// </summary>   
    public class InterestGroup
    {
        internal StargateEngine engine;
        internal List<Entity> entities = new(32);

        internal InterestGroup(StargateEngine engine)
        {
            this.engine = engine;
        }

        internal void CaculateGroups()
        {
            
        }
    }
}