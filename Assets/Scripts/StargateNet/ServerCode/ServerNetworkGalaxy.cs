using Riptide;
using UnityEngine;

namespace StargateNet
{
    public class ServerNetworkGalaxy : NetworkGalaxy
    {
        public override int CurrentTick { get; protected set; }
        public override bool IsServer => true;
        public override bool IsClient => false;

        public ushort Port { private set; get; }
        public ushort MaxClientCount { private set; get; }
        public Server Server { private set; get; }

        public ServerNetworkGalaxy(ushort port, ushort maxClientCount)
        {
            Port = port;
            MaxClientCount = maxClientCount;
        }

        public override void NetworkStart()
        {
            Server = new Server();
        }

        public override void NetworkUpdate()
        {
            Server.Update();
            CurrentTick++;
            if (CurrentTick % 200 == 0)
            {
                SendMessage();
            }
        }

        public override void Connect()
        {
            Server.Start(Port, MaxClientCount);
        }

        public override void SendMessage()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.sync);
            message.AddString("Hello");

            Server.SendToAll(message);
        }
        
        [MessageHandler((ushort)ServerToClientId.sync)]
        public static void Sync(Message message)
        {
            Debug.Log(1111);
        }


        public override void OnQuit()
        {
            Server.Stop();
        }
    }
}