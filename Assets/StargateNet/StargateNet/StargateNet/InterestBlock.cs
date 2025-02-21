using System;

namespace StargateNet
{
    public struct InterestBlock : IEquatable<InterestBlock>
    {
        public int xIndex;
        public int yIndex;
        public int zIndex;
    
        
        public override bool Equals(object obj)
        {            
            return obj is InterestBlock block && Equals(block);
        }

        public bool Equals(InterestBlock other)
        {
            return xIndex == other.xIndex &&
                   yIndex == other.yIndex &&
                   zIndex == other.zIndex ;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(xIndex, yIndex, zIndex);
        }

        public static bool operator ==(InterestBlock left, InterestBlock right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InterestBlock left, InterestBlock right)
        {
            return !(left == right);
        }
    }
}