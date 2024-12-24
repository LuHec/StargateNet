using System;
using UnityEngine;

namespace StargateNet
{
    public class LagCompensate
    {
        internal StargateEngine Engine { get; private set; }
        private const string CompensatedComponentName = "CompensatedComponent";
        private readonly int _compLayerMask;
        private RaycastHit[] _raycastHits;

        public LagCompensate(StargateEngine engine)
        {
            this.Engine = engine;
            this._compLayerMask = LayerMask.NameToLayer(CompensatedComponentName);
            _raycastHits = new RaycastHit[engine.ConfigData.maxNetworkObjects];
        }

        internal unsafe bool NetworkRaycast(Vector3 origin,
            Vector3 direction,
            int inputSource,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask)
        {
            // 首先要去除延迟补偿的layerMask
            layerMask = layerMask & ~(1 << _compLayerMask);
            if (this.Engine.IsClient) return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask);

            SimulationInput input = this.Engine.ServerSimulation.GetSimulationInput(inputSource);
            Tick clientAuthorTick = input.clientAuthorTick; // clientAuthorTick对应的Snapshot是客户端按下调用RayCast时其他RemoteObject的状态
            float alpha = input.clientInterpolationAlpha;
            Snapshot fromSnapshot = this.Engine.WorldState.GetHistoryTick(this.Engine.Tick - clientAuthorTick);
            Snapshot toSnapshot = this.Engine.WorldState.GetHistoryTick(this.Engine.Tick - clientAuthorTick - 1);
            if (fromSnapshot != null && toSnapshot != null)
            {
                int length = Physics.RaycastNonAlloc(origin, direction, this._raycastHits, maxDistance, this._compLayerMask);

                StartLagCompensation(this._raycastHits, length, inputSource, fromSnapshot, toSnapshot, alpha);
                var hit = Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask);
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
        private unsafe void StartLagCompensation(RaycastHit[] hitResults, int length, int casterInputSource, Snapshot fromSnapshot, Snapshot toSnapshot, float alpha)
        {
            for (int i = 0; i < length; i++)
            {
                var hitResult = hitResults[i];
                GameObject go = hitResult.collider.gameObject;
                if (go == null) return;
                if (go.TryGetComponent(out NetworkObject networkObject) && go.TryGetComponent(out CompensateCollider compensateCollider) &&
                    go.TryGetComponent(out NetworkTransform networkTransform))
                {
                    Entity compEntity = networkObject.Entity;
                    if (compEntity.inputSource == casterInputSource) continue;
                    int metaIdx = compEntity.worldMetaId;
                    int stateBlockIdx = (int)compEntity.GetStateBlockIdx(networkTransform.StateBlock);
                    if (fromSnapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue &&
                        toSnapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue)
                    {
                        // position lerp
                        int* fromPositionPtr = (int*)fromSnapshot.NetworkStates.pools[compEntity.poolId].dataPtr + compEntity.entityBlockWordSize + stateBlockIdx;
                        int* toPositionPtr = (int*)toSnapshot.NetworkStates.pools[compEntity.poolId].dataPtr + compEntity.entityBlockWordSize + stateBlockIdx;
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

                        go.transform.position = renderPosition;
                        go.transform.rotation = renderRotationQuat;
                    }
                }
            }
        }

        private unsafe void EndLagCompensation(RaycastHit[] hitResults, int length, int casterInputSource, Snapshot snapshot)
        {
            for (int i = 0; i < length; i++)
            {
                var hitResult = hitResults[i];
                GameObject go = hitResult.collider.gameObject;
                if (go == null) return;
                if (go.TryGetComponent(out NetworkObject networkObject) && go.TryGetComponent(out NetworkTransform networkTransform))
                {
                    Entity compEntity = networkObject.Entity;
                    if (compEntity.inputSource == casterInputSource) continue;
                    int metaIdx = compEntity.worldMetaId;
                    int stateBlockIdx = (int)compEntity.GetStateBlockIdx(networkTransform.StateBlock);
                    if (snapshot.GetWorldObjectMeta(metaIdx).networkId == networkObject.NetworkId.refValue)
                    {
                        int* positionPtr = (int*)snapshot.NetworkStates.pools[compEntity.poolId].dataPtr + compEntity.entityBlockWordSize + stateBlockIdx;
                        int* rotationPtr = positionPtr + 3;
                        Vector3 position = StargateNetUtil.GetVector3(positionPtr);
                        Vector3 rotation = StargateNetUtil.GetVector3(rotationPtr);
                        go.transform.position = position;
                        go.transform.rotation = Quaternion.Euler(rotation);
                    }
                }
            }
        }
    }
}