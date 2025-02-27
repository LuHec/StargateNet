using System;
using System.Collections.Generic;

namespace StargateNet
{
    public class NetworkRPCManager
    {
        private Dictionary<int, NetworkStaticRpcEvent> staticRPCs = new(32);
        private StargateEngine _engine;
        private StargateAllocator rpcAllocator;

        /// <summary>
        /// 最终写入用于发送的Buffer
        /// </summary>
        private ReadWriteBuffer rpcSenderBuffer;

        /// <summary>
        /// 用于接收的buffer
        /// </summary>
        private ReadWriteBuffer rpcReceiveBuffer;

        /// <summary>
        /// 在发起调用RPC时，用来写入参数
        /// </summary>
        private ReadWriteBuffer rpcPramWriter;

        /// <summary>
        /// 暂存写入的信息
        /// </summary>
        private List<NetworkRPCPram> pramsToSend = new(16);

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