using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior
{
    [Networked] public Vector3 Position { get; set; }
    [Networked] public Vector3 Rotation { get; set; }

    [Header("Basic Settings")] [SerializeField]
    public Transform renderTransform;

    [Header("Client Lerp Settings")] 
    [SerializeField]
    private float _clienTeleportDistance = 10f; // 触发强拉的距离

    public override void NetworkRender(SgNetworkGalaxy galaxy)
    {
        if (this.renderTransform != null)
            this.Render(galaxy);
    }

    public override void SerializeToNetcode()
    {
        this.Position = this.transform.position;
        this.Rotation = this.transform.rotation.eulerAngles;
    }

    public override void DeserializeToGameCode()
    {
        this.transform.position = this.Position;
        this.transform.rotation = Quaternion.Euler(this.Rotation);
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
        // 使用四元数插值来处理旋转，避免350->10度这种情况
        Quaternion fromQuat = Quaternion.Euler(fromRotation);
        Quaternion toQuat = Quaternion.Euler(toRotation);
        Quaternion renderRotationQuat = Quaternion.Slerp(fromQuat, toQuat, alpha);
        renderTransform.rotation = renderRotationQuat;
    }
}