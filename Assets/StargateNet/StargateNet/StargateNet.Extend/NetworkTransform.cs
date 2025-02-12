using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior, IClientSimulationCallbacks
{
    [Replicated]
    public Vector3 Position { get; set; }

    [Replicated]
    public Vector3 Rotation { get; set; }

    [Header("Basic Settings")] [SerializeField]
    public Transform renderTransform;

    [Header("Client Settings")] [SerializeField]
    private bool needCorrect = true;

    [SerializeField]
    private float errorMagnitude = 1.8f;

    [SerializeField, Range(0f, 10f)]
    private float correctionMultiplier = 1.28f;

    private TransformErrorCorrect _corrector;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        if (this.IsClient && needCorrect && this._corrector == null)
        {
            this._corrector = new TransformErrorCorrect();
            this._corrector.Init(this.transform.position, this.transform.rotation);
        }

        if (this.IsClient)
        {
            galaxy.Engine.ClientSimulation.AddClientSimulationCallbacks(this);
        }
    }

    public override void NetworkDestroy(SgNetworkGalaxy galaxy)
    {
        if (this.IsClient)
        {
            galaxy.Engine.ClientSimulation.RemoveClientSimulationCallbacks(this);
        }
    }

    public void OnPreRollBack()
    {
        this._corrector?.OnPreRollback(this.transform.position, this.transform.rotation);
    }

    public void OnPostResimulation()
    {
        this._corrector?.OnPostResimulation();
    }


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
        Interpolation interpolation = isServer ? galaxy.Engine.InterpolationLocal : IsLocalPlayer() ? galaxy.Engine.InterpolationLocal : galaxy.Engine.InterpolationRemote;
        if (!interpolation.HasSnapshot) return;
        // 获取内存偏移量
        int stateBlockIdx = (int)this.Entity.GetStateBlockIdx(this.StateBlock);
        // 获取FromState的数值
        Snapshot fromSnapshot = interpolation.FromSnapshot;
        Snapshot toSnapshot = interpolation.ToSnapshot;

        if (!fromSnapshot.snapshotTick.IsValid || !toSnapshot.snapshotTick.IsValid) return;
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


        // rotation lerp
        int* fromRotationPtr = fromPositionPtr + 3;
        int* toRotationPtr = toPositionPtr + 3;
        Vector3 fromRotation = StargateNetUtil.GetVector3(fromRotationPtr);
        Vector3 toRotation = StargateNetUtil.GetVector3(toRotationPtr);
        // 使用四元数插值来处理旋转，避免350->10度这种情况
        Quaternion fromQuat = Quaternion.Euler(fromRotation);
        Quaternion toQuat = Quaternion.Euler(toRotation);
        Quaternion renderRotationQuat = Quaternion.Slerp(fromQuat, toQuat, alpha);


        // 上方得出正确的插值位置，这里调和实际的位置，尽可能去靠近正确的插值位置
        if (this.IsClient && this._corrector != null)
        {
            _corrector.Render(ref renderPosition, ref renderRotationQuat, errorMagnitude, correctionMultiplier);
        }


        renderTransform.position = renderPosition;
        renderTransform.rotation = renderRotationQuat;
    }
}