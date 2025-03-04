using System;
using UnityEngine;

namespace StargateNet
{
    public class LagCompensateComponent : ILagCompensateComponent
    {
        public StargateEngine Engine { get; private set; }
        private const string CompensatedComponentName = "CompensatedComponent";
        private int _compLayerMask;
        private RaycastHit[] _raycastHits;

        public void Init(StargateEngine engine, int maxNetworkObjects)
        {
            this.Engine = engine;
            this._compLayerMask = LayerMask.GetMask(CompensatedComponentName);
            _raycastHits = new RaycastHit[maxNetworkObjects];
        }

        public bool NetworkRaycast(Vector3 origin,
            Vector3 direction,
            int inputSource,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask)
        {
            // 去除延迟补偿的layerMask
            layerMask &= ~_compLayerMask;
            if (this.Engine.IsClient) return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask);

            SimulationInput input = this.Engine.ServerSimulation.GetSimulationInput(inputSource);
            Tick clientRemoteFromTick =
                input.clientRemoteFromTick; // clientRemoteFromTick对应的Snapshot是客户端按下调用RayCast时其他RemoteObject的Tick
            float alpha = input.clientInterpolationAlpha;
            Snapshot fromSnapshot = this.Engine.WorldState.GetHistoryTick(this.Engine.Tick - clientRemoteFromTick);
            Snapshot toSnapshot = this.Engine.WorldState.GetHistoryTick(this.Engine.Tick - clientRemoteFromTick - 1);
            if (fromSnapshot != null && toSnapshot != null)
            {
                // 进行延迟补偿时只和延迟补偿的层级做碰撞
                int length = Physics.RaycastNonAlloc(origin, direction, this._raycastHits, maxDistance, this._compLayerMask);

                StartLagCompensation(this._raycastHits, length, inputSource, fromSnapshot, toSnapshot, alpha);
                var hit = Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask);
                GizmoTimerDrawer.Instance?.DrawWireSphereWithTimer(hitInfo.point, .5f, 3f, Color.red);
                EndLagCompensation(this._raycastHits, length, inputSource, this.Engine.WorldState.CurrentSnapshot);
                return hit;
            }
            else
                return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask);
        }

        /// <summary>
        /// 将所有延迟补偿组件被打到的物体回滚并根据客户端alpha进行插值
        /// </summary>
        /// <param name="hitResults"></param>
        /// <param name="length"></param>
        /// <param name="casterInputSource"></param>
        /// <param name="fromSnapshot"></param>
        /// <param name="toSnapshot"></param>
        /// <param name="alpha"></param>
        private unsafe void StartLagCompensation(RaycastHit[] hitResults, int length, int casterInputSource,
            Snapshot fromSnapshot, Snapshot toSnapshot, float alpha)
        {
            for (int i = 0; i < length; i++)
            {
                var hitResult = hitResults[i];
                GameObject compensateTarget = hitResult.collider.gameObject;
                if (compensateTarget == null) continue;
                // 
                GizmoTimerDrawer.Instance?.DrawWireSphereWithTimer(hitResult.point, 1f, 3f, Color.blue);
                if (!compensateTarget.TryGetComponent(out CompensateCollider compensateCollider)) continue;
                GameObject target = compensateTarget.transform.parent.gameObject;
                if(target == null) continue;
                if (target.TryGetComponent(out NetworkObject networkObject) && target.TryGetComponent(out NetworkTransform networkTransform))
                {
                    Entity compEntity = networkObject.Entity;
                    if (compEntity.InputSource == casterInputSource) continue;
                    // 先将数据写入到Snapshot中,不然被回滚的物体这帧的运动就被覆盖掉了
                    networkTransform.SerializeToNetcode();
                    
                    int metaIdx = compEntity.WorldMetaId;
                    int stateBlockIdx = (int)compEntity.GetStateBlockIdx(networkTransform.StateBlock);
                    if (fromSnapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue &&
                        toSnapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue)
                    {
                        // position lerp
                        int* fromPositionPtr = (int*)fromSnapshot.NetworkStates.pools[compEntity.PoolId].dataPtr +
                                               compEntity.EntityBlockWordSize + stateBlockIdx;
                        int* toPositionPtr = (int*)toSnapshot.NetworkStates.pools[compEntity.PoolId].dataPtr +
                                             compEntity.EntityBlockWordSize + stateBlockIdx;
                        Vector3 fromPosition = StargateNetUtil.GetVector3(fromPositionPtr);
                        Vector3 toPosition = StargateNetUtil.GetVector3(toPositionPtr);
                        Vector3 renderPosition = Vector3.Lerp(fromPosition, toPosition, alpha);

                        // rotation lerp
                        int* fromRotationPtr = fromPositionPtr + 3;
                        int* toRotationPtr = toPositionPtr + 3;
                        Vector3 fromRotation = StargateNetUtil.GetVector3(fromRotationPtr);
                        Vector3 toRotation = StargateNetUtil.GetVector3(toRotationPtr);
                        Quaternion fromQuat = Quaternion.Euler(fromRotation);
                        Quaternion toQuat = Quaternion.Euler(toRotation);
                        Quaternion renderRotationQuat = Quaternion.Slerp(fromQuat, toQuat, alpha);

                        target.transform.position = renderPosition;
                        target.transform.rotation = renderRotationQuat;
                        
                        GizmoTimerDrawer.Instance?.DrawWireSphereWithTimer(renderPosition, .5f, 3f, Color.yellow);
                    }
                }
            }
            Physics.SyncTransforms();
        }

        private unsafe void EndLagCompensation(RaycastHit[] hitResults, int length, int casterInputSource,
            Snapshot snapshot)
        {
            for (int i = 0; i < length; i++)
            {
                var hitResult = hitResults[i];
                GameObject compensateTarget = hitResult.collider.gameObject;
                if (compensateTarget == null) continue;
                GizmoTimerDrawer.Instance?.DrawWireSphereWithTimer(hitResult.point, .5f, 3f, Color.blue);
                if (!compensateTarget.TryGetComponent(out CompensateCollider compensateCollider)) continue;
                GameObject target = compensateTarget.transform.parent.gameObject;
                if(target == null) continue;
                if (target.TryGetComponent(out NetworkObject networkObject) && target.TryGetComponent(out NetworkTransform networkTransform))
                {
                    Entity compEntity = networkObject.Entity;
                    if (compEntity.InputSource == casterInputSource) continue;
                    int metaIdx = compEntity.WorldMetaId;
                    int stateBlockIdx = (int)compEntity.GetStateBlockIdx(networkTransform.StateBlock);
                    if (snapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue)
                    {
                        int* positionPtr = (int*)snapshot.NetworkStates.pools[compEntity.PoolId].dataPtr +
                                           compEntity.EntityBlockWordSize + stateBlockIdx;
                        int* rotationPtr = positionPtr + 3;
                        Vector3 position = StargateNetUtil.GetVector3(positionPtr);
                        Vector3 rotation = StargateNetUtil.GetVector3(rotationPtr);
                        target.transform.position = position;
                        target.transform.rotation = Quaternion.Euler(rotation);
                    }
                }
            } 
            Physics.SyncTransforms();
        }
    }
}