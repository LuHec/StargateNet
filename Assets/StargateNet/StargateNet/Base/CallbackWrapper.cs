namespace StargateNet
{
    public struct CallbackWrapper
    {
        internal int invokeDurResim; // int便于IL代码编写
        internal int behaviorIndex;
        internal int propertyIndex;
        internal int propertyWordSize;
        internal CallbackEvent callbackEvent;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="invokeDurResim"></param>
        /// <param name="behaviorIndex">暂时弃置</param>
        /// <param name="propertyIndex"></param>
        /// <param name="propertyWordSize"></param>
        /// <param name="callbackEvent"></param>
        public CallbackWrapper(int invokeDurResim, int behaviorIndex, int propertyIndex, int propertyWordSize, CallbackEvent callbackEvent)
        {
            this.invokeDurResim = invokeDurResim;
            this.behaviorIndex = behaviorIndex;
            this.propertyIndex = propertyIndex;
            this.propertyWordSize = propertyWordSize;
            this.callbackEvent = callbackEvent;
        }
    }
}