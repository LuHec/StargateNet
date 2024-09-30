namespace StargateNet
{
    /// <summary>
    /// A networked object isn't actually a GameObject;
    /// It's merely conceptual, existing to represent an entity within a networking context.
    /// </summary>
    public sealed class Entity
    {
        public SgNetworkEngine engine;
        public INetworkEntity entity;           // A GameObject which implement INetworkEntity
        public readonly int entityBlockSize;    // Networked Field Size 
    }
    
    
}