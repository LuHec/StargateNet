namespace StargateNet
{
    public struct CallbackWrapper
    {
        internal int invokeDurResim; // int便于IL代码编写
        internal int behaviorIndex;
        internal int propertyIndex;
        internal int propertyWordIndex;
        internal CallbackEvent callbackEvent;

        public CallbackWrapper(int invokeDurResim, int behaviorIndex, int propertyIndex, int propertyWordIndex, CallbackEvent callbackEvent)
        {
            this.invokeDurResim = invokeDurResim;
            this.behaviorIndex = behaviorIndex;
            this.propertyIndex = propertyIndex;
            this.propertyWordIndex = propertyWordIndex;
            this.callbackEvent = callbackEvent;
        }
    }
}