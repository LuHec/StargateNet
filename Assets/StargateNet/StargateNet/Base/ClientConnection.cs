using System.Collections.Generic;
using Riptide;

namespace StargateNet
{
    public class ClientConnection
    {
        internal bool connected = false;
        internal ClientData clientData;
        internal Connection connection;
        internal Tick lastAckTick = Tick.InvalidTick;
        internal List<InterestGroup> interestGroup = new(1);
    }
}