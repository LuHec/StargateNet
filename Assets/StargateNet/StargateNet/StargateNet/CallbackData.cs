using System;

namespace StargateNet
{
    public struct CallbackData : IEquatable<CallbackData>
    {
        internal CallbackEvent Event;
        internal unsafe int* previousData;
        internal int offset; // 暂时用不上
        internal int propertyIdx;
        internal int wordSize;
        internal IStargateNetworkScript behaviour;

        public unsafe CallbackData(CallbackEvent @event, int* previousData, int offset, int propertyIdx, int wordSize, IStargateNetworkScript behaviour)
        {
            Event = @event;
            this.previousData = previousData;
            this.offset = offset;
            this.propertyIdx = propertyIdx;
            this.wordSize = wordSize;
            this.behaviour = behaviour;
        }

        public override bool Equals(object obj)
        {
            return obj is CallbackData data && Equals(data);
        }

        public override unsafe int GetHashCode()
        {
            return (offset, (ulong)previousData, propertyIdx).GetHashCode();
        }

        public unsafe bool Equals(CallbackData other)
        {
            return other.offset == offset && other.previousData == previousData && other.Event == Event;
        }
        
        public unsafe T GetPreviousData<T>() where T : unmanaged
        {
            T result = default(T);
            if (previousData != null)
            {
                result = *(T*)previousData;
            }
            return result;
        }
    }
}