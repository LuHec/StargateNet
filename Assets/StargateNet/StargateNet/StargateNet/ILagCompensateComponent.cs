using UnityEngine;

namespace StargateNet
{
    public interface ILagCompensateComponent
    {
        void Init(StargateEngine engine, int maxNetworkObjects);

        bool NetworkRaycast(
            Vector3 origin,
            Vector3 direction,
            int inputSource,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask);
    }
}