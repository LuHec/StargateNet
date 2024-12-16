using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior
{
    [Networked] public Vector3 Position { get; set; }
    [SerializeField] public Transform renderTransform;
    private Vector3 _renderPosition;
    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        this.transform.position = this.Position;
    }

    public override void NetworkRender(SgNetworkGalaxy galaxy)
    {
        this.Render(galaxy);
    }

    private unsafe void Render(SgNetworkGalaxy galaxy)
    {
        bool isServer = galaxy.IsServer;
        Interpolation interpolation = galaxy.Engine.InterpolationLocal;
        if (!interpolation.HasSnapshot) return;
        
        // 获取内存偏移量
        int stateBlockIdx = (int)this.Entity.GetStateBlockIdx(this.StateBlock);
        // 获取FromState的数值
        Snapshot fromSnapshot = interpolation.FromSnapshot;
        Snapshot toSnapshot = interpolation.ToSnapshot;
        //排除前FromSnapshot不存在的物体
        var fromObjectMeta = fromSnapshot.GetWorldObjectMeta(this.Entity.worldMetaId);
        if(fromObjectMeta.networkId != this.Entity.networkId.refValue) return;

        float alpha = interpolation.Alpha;
        int* toPositionPtr = (int*)toSnapshot.NetworkStates.pools[this.Entity.poolId].dataPtr + this.Entity.entityBlockWordSize + stateBlockIdx;
        Vector3 targetPosition = StargateNetUtil.GetVector3(toPositionPtr);
        int* fromPositionPtr = (int*)fromSnapshot.NetworkStates.pools[this.Entity.poolId].dataPtr + this.Entity.entityBlockWordSize + stateBlockIdx;
        Vector3 fromPosition = StargateNetUtil.GetVector3(fromPositionPtr);
        Vector3 lerpPosition = Vector3.Lerp(fromPosition, targetPosition, alpha);
        renderTransform.position = lerpPosition;
    }
}
  