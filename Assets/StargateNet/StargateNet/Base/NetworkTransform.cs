using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior
{
    [Networked] public Vector3 Position { get; set; }
    [Networked] public Vector3 Rotation { get; set; }
    [SerializeField] public Transform renderTransform;
    private Vector3 _renderPosition;

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        this.transform.position = this.Position;
        this.transform.rotation = Quaternion.Euler(this.Rotation);
    }

    public override void NetworkRender(SgNetworkGalaxy galaxy)
    {
        if (this.renderTransform != null)
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
        if (fromObjectMeta.networkId != this.Entity.networkId.refValue) return;

        // position lerp
        float alpha = interpolation.Alpha;
        int* fromPositionPtr = (int*)fromSnapshot.NetworkStates.pools[this.Entity.poolId].dataPtr + this.Entity.entityBlockWordSize + stateBlockIdx;
        int* toPositionPtr = (int*)toSnapshot.NetworkStates.pools[this.Entity.poolId].dataPtr + this.Entity.entityBlockWordSize + stateBlockIdx;
        Vector3 fromPosition = StargateNetUtil.GetVector3(fromPositionPtr);
        Vector3 toPosition = StargateNetUtil.GetVector3(toPositionPtr);
        Vector3 renderPosition = Vector3.Lerp(fromPosition, toPosition, alpha);
        renderTransform.position = renderPosition;

        // rotation lerp
        int* fromRotationPtr = fromPositionPtr + 3;
        int* toRotationPtr = toPositionPtr + 3;
        Vector3 fromRotation = StargateNetUtil.GetVector3(fromRotationPtr);
        Vector3 toRotation = StargateNetUtil.GetVector3(toRotationPtr);
        Vector3 renderRotation = Vector3.Lerp(fromRotation, toRotation, alpha);
        renderTransform.rotation = Quaternion.Euler(renderRotation);  
    }
}