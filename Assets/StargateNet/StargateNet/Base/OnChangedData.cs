namespace StargateNet
{
    public struct OnChangedData
    {
        internal unsafe int* previousValue;
        internal unsafe int* currentValue;
        
        // public T GetPreviousValue
    }
}