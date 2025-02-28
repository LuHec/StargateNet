using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class NetworkRPCManager
    {
        internal bool NeedSendRpc => this.pramsToSend.Count > 0;
        internal bool NeedCallRpc => this.pramsToReceive.Count > 0;
        private Dictionary<int, NetworkStaticRpcEvent> staticRPCs = new(32);
        private StargateEngine _engine;
        private StargateAllocator rpcAllocator;

        /// <summary>
        /// 最终写入用于发送的Buffer
        /// </summary>
        private ReadWriteBuffer rpcSenderBuffer;

        /// <summary>
        /// 在发起调用RPC时，用来写入参数
        /// </summary>
        private ReadWriteBuffer rpcPramWriter;

        /// <summary>
        /// 暂存写入的信息
        /// </summary>
        internal List<NetworkRPCPram> pramsToSend = new(16);
        internal List<NetworkRPCPram> pramsToReceive = new(16);

        public NetworkRPCManager(StargateEngine engine, StargateAllocator rpcAllocator)
        {
            this._engine = engine;
            this.rpcAllocator = rpcAllocator;
            this.rpcSenderBuffer = new ReadWriteBuffer(1024);
            this.rpcPramWriter = new ReadWriteBuffer(1024);
        }

        internal unsafe void ClearSendedRpc()
        {
            for (int i = 0; i < this.pramsToSend.Count; i++)
            {
                NetworkRPCPram pram = this.pramsToSend[i];
                this.rpcAllocator.Free(pram.prams);
            }
            this.pramsToSend.Clear();
        }

        internal unsafe void ClearReceivedRpc()
        {
            for (int i = 0; i < this.pramsToReceive.Count; i++)
            {
                NetworkRPCPram pram = this.pramsToReceive[i];
                this.rpcAllocator.Free(pram.prams);
            }
            this.pramsToReceive.Clear();
        }

        internal unsafe NetworkRPCPram RequireRpcPramToReceive(int byteSize)
        {
            NetworkRPCPram pram = new NetworkRPCPram()
            {
                prams = (byte*)this.rpcAllocator.Malloc(byteSize),
                pramsBytes = byteSize
            };
            return pram;
        }

        internal void AddRpcPramToReceive(NetworkRPCPram pram)
        {
            this.pramsToReceive.Add(pram);
        }

        public void CallStaticRpc()
        {
            for(int i = 0; i < this.pramsToReceive.Count; i++)
            {
                NetworkRPCPram pram = this.pramsToReceive[i];
                if (this.staticRPCs.TryGetValue(pram.rpcId, out NetworkStaticRpcEvent rpcEvent))
                {
                    Entity entity = this._engine.Simulation.entitiesTable[new NetworkObjectRef(pram.entityId)];
                    if(entity == null) continue;
                    NetworkBehavior behavior = entity.networkBehaviors[pram.scriptId];
                    rpcEvent.Invoke(behavior, pram);
                }
            }
        }

        private bool CanCallRpc(NetworkRPCFrom networkRPCFrom)
        {
            return networkRPCFrom == NetworkRPCFrom.ClientCall && this._engine.IsClient
                   || networkRPCFrom == NetworkRPCFrom.ServerCall && this._engine.IsServer;
        }

        private void AddStaticRPC(int rpcId, NetworkStaticRpcEvent rpcEvent)
        {
            this.staticRPCs.TryAdd(rpcId, rpcEvent);
        }

        /// <summary>
        /// 准备开始写入RPC参数，会检查是否有权限调用RPC，如果返回true必须和EndWrite成对出现
        /// </summary>
        /// <param name="networkRPCFrom"></param>
        /// <param name="entityId"></param>
        /// <param name="scriptId"></param>
        /// <param name="rpcId">函数对应的ID</param>
        /// <returns></returns>
        public unsafe void StartWrite(int networkRPCFrom, NetworkObjectRef entityId, int scriptId, int rpcId, int paramsBytes)
        {
            if (!CanCallRpc((NetworkRPCFrom)networkRPCFrom)) throw new Exception("Rpc rights error");
            rpcPramWriter.Clear();
            NetworkRPCPram writePram = new NetworkRPCPram()
            {
                entityId = entityId.refValue,
                scriptId = scriptId,
                rpcId = rpcId,
                prams = (byte*)this.rpcAllocator.Malloc(paramsBytes),
                pramsBytes = paramsBytes
            };
            this.pramsToSend.Add(writePram);
        }

        public unsafe void WriteRPCPram(void* data, int byteSize)
        {
            byte* ptr = (byte*)data;
            for (int i = 0; i < byteSize; i++)
            {
                this.rpcPramWriter.AddByte(ptr[i]);
            }
        }

        /// <summary>
        /// 和StartWrite必须成对出现
        /// </summary>
        public unsafe void EndWrite()
        {
            NetworkRPCPram writePram = this.pramsToSend[^1];
            for (int i = 0; i < writePram.pramsBytes; i++)
            {
                writePram.prams[i] = this.rpcPramWriter.Get()[i];
            }

            this.rpcPramWriter.Clear();
        }
    }
}