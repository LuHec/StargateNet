using System;

namespace StargateNet
{
    public class NetworkCallBackAttribute : Attribute
    {
        internal string propName;
        internal bool invokeDurResim;
        
        public NetworkCallBackAttribute(string propertyName, bool invokeDuringResim)
        {
            this.propName = propertyName;
            this.invokeDurResim = invokeDuringResim;
        }
    }
}