namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal int* networkObjectMap; // networkObject的销毁和创建不会预测
        internal void* worldState;
    }
}