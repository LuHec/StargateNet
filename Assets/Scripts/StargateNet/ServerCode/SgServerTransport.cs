using Riptide;
using UnityEngine;

namespace StargateNet
{
    public class SgServerTransport : SgTransport
    {
        public override bool IsServer => true;
        public override bool IsClient => false;

        public ushort Port { private set; get; }
        public ushort MaxClientCount { private set; get; }
        public Server Server { private set; get; }

        public override void TransportCreate()
        {
            this.Server = new Server();
        }

        public void StartServer(ushort port, ushort maxClientCount)
        {
            this.Port = port;
            this.MaxClientCount = maxClientCount;
            this.Server.Start(port, maxClientCount);
        }

        public override void TransportUpdate()
        {
            this.Server.Update();
        }

        public override void SendMessage()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.sync);
            message.AddString("Hello");

            this.Server.SendToAll(message);
        }
        
        [MessageHandler((ushort)ServerToClientId.sync)]
        public static void Sync(Message message)
        {
            Debug.Log(1111);
        }


        public override void OnQuit()
        {
            this.Server.Stop();
        }
    }
}