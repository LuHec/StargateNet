namespace StargateNet
{
    public enum Protocol : ushort
    {
        ToServer = 1,
        ToClient = 2,
    }

    public enum ToServerProtocol : uint
    {
        ConnectRequest = 0,
        Input,
    }

    public enum ToClientProtocol : uint
    {
        ConnectReply = 0,
        Snapshot,
    }
}