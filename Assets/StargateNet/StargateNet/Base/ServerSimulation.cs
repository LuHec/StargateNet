using System.Collections.Generic;
using System.Linq;
using Riptide.Utils;

namespace StargateNet
{
    public class ServerSimulation : Simulation
    {
        internal ClientData[] clientDatas;

        internal ServerSimulation(SgNetworkEngine engine) : base(engine)
        {
            this.clientDatas = new ClientData[engine.ConfigData.maxClientCount];
            for (int i = 0; i < this.clientDatas.Length; i++)
            {
                this.clientDatas[i] = new ClientData(engine.ConfigData.savedSnapshotsCount);
            }
        }

        internal override void PreFixedUpdate()
        {
            ConsumeInputs(this.engine.simTick);
        }

        internal override void PostFixedUpdate()
        {
            for (int i = 0; i < clientDatas.Length; i++)
            {
                if (clientDatas[i].Started)
                {
                    RecycleInput(clientDatas[i].currentInput);
                    clientDatas[i].currentInput = CreateInput(Tick.InvalidTick, Tick.InvalidTick);
                }
            }

            this.engine.Monitor.tick = this.engine.simTick.tickValue;
        }

        private void ConsumeInputs(Tick targetTick)
        {
            for (int i = 0; i < clientDatas.Length; i++)
            {
                if (clientDatas[i].Started)
                {
                    Queue<SimulationInput> clientInput = clientDatas[i].clientInput;
                    string ticks = "";
                    if (clientInput.Count > 0)
                    {
                        var array = clientInput.ToArray();
                        for (int j = 0; j < array.Length; j++)
                        {
                            ticks += ',';
                            ticks += array[j].targetTick;
                        }
                    }

                    RiptideLogger.Log(LogType.Warning,
                        $"ServerTick:{this.engine.simTick}, {ticks} input count:{clientDatas[i].clientInput.Count}, Client ID: {i}");
                    while (clientInput.Count > 0 && clientInput.Peek().targetTick <= targetTick)
                    {
                        var input = clientInput.Dequeue();
                        if (input.targetTick < targetTick)
                        {
                            RecycleInput(input);
                        }
                        else if (input.targetTick == targetTick)
                        {
                            RecycleInput(clientDatas[i].currentInput);
                            clientDatas[i].currentInput = input;
                        }
                    }


                    RiptideLogger.Log(LogType.Warning,
                        $"ServerTick:{this.engine.simTick}, ClientInput targetTick:{this.clientDatas[i].currentInput.targetTick}, input count:{clientDatas[i].clientInput.Count}, Client ID: {i}");
                }
            }
        }
    }
}