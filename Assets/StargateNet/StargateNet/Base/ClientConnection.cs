using System.Collections.Generic;
using Riptide;

namespace StargateNet
{
    public class ClientConnection
    {
        internal bool connected = false;
        internal bool losePacket = false;
        internal ClientData clientData;
        internal Connection connection;
        internal Tick lastAckTick = Tick.InvalidTick;
        internal List<InterestGroup> interestGroup = new(1);
        internal StargateEngine engine;

        public ClientConnection(StargateEngine engine)
        {
            this.engine = engine;
        }

        public void Reset()
        {
            this.connected = false;
            this.losePacket = false;
            this.clientData = null;
            this.connection = null;
            this.lastAckTick = Tick.InvalidTick;
        }

        public unsafe void WritePacket(Message message)
        {
            // TODO:写入信息，这里后续要加入每个客户端各自的剔除
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;
            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && entity.dirty && worldState.CurrentSnapshot != null &&
                    !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    int* state = entity.stateBlock;
                    int* dirtyMap = entity.dirtyMap;
                    message.AddInt(worldIdx);
                    for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                    {
                        if (dirtyMap[idx] == 0) continue;
                        message.AddInt(idx);
                        message.AddInt(state[idx]);
                    }
                    message.AddInt(-1); // 终止符号
                }
            }
        }
    }
}