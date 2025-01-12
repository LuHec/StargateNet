using System;

namespace StargateNet
{
    public class PropCallbackData : IEquatable<PropCallbackData>
    {
        public bool Equals(PropCallbackData other)
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