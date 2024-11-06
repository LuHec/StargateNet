using System.Collections.Generic;

namespace StargateNet
{
    /// <summary>
    /// 用于存储每一帧特定客户端的同步对象。暂时先不管分包同步的问题
    /// </summary>
    public class InterestGroup
    {
        internal StargateEngine engine;
        internal List<Entity> entities = new(32);

        internal InterestGroup(StargateEngine engine)
        {
            this.engine = engine;
        }
    }
}