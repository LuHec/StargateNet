namespace StargateNet
{
    /// <summary>
    ///  Network Engine, client and server run in same way
    /// </summary>
    public class NetworkGalaxy
    {
        public SgNetEngine Engine { private set; get; }
        public SgTransport Transport { private set; get; }

        public NetworkGalaxy()
        {
            Transport = transport;
        }

        public void Init(SgTransport transport)
        {
            Engine = new SgNetEngine();
        }
    }
}

