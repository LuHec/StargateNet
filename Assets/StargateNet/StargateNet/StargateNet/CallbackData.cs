using System;

namespace StargateNet
{
    public struct CallbackData : IEquatable<CallbackData>
    {
        public bool Equals(CallbackData other)
        {
            return true;
        }

        public T GetCurrentData<T>()
        {
            return default(T);
        }

        public T GetPreviousData<T>()
        {
            return default(T);
        }
    }
}