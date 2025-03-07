﻿using System.Collections.Generic;
using System.Linq;
using Riptide.Utils;

namespace StargateNet
{
    public class ServerSimulation : Simulation
    {
        internal ClientData[] clientDatas;

        internal ServerSimulation(StargateEngine engine) : base(engine)
        {
            this.clientDatas = new ClientData[engine.ConfigData.maxClientCount];
            for (int i = 0; i < this.clientDatas.Length; i++)
            {
                this.clientDatas[i] = new ClientData(this, engine.ConfigData.savedSnapshotsCount);
            }
        }

        internal override void PreFixedUpdate()
        {
            this.ConsumeInputs(this.engine.SimTick);
            this.currentInput = this.CreateInput(this.engine.SimTick, this.engine.SimTick, 0, Tick.InvalidTick);
            foreach (var clientData in clientDatas) 
            {
                if(clientData.Started)
                    clientData.PrepareCurrentInput(this.engine.Tick);
            }
            this.engine.WorldState.CurrentSnapshot.CopyTo(this.previousState);
        }

        internal override void PostFixedUpdate()
        {
            this.RecycleInput(this.currentInput);
            this.currentInput = null;
            // 清除本帧使用的input
            for (int i = 0; i < this.clientDatas.Length; i++)
            {
                ClientData clientData = this.clientDatas[i];
                if (clientData.Started)
                {
                    clientData.ClearInput(this);
                }
            }

            // 复原DirtyMap
            foreach (var pair in this.entitiesTable)
            {
                Entity entity = pair.Value;
                entity.TickReset();
            }

            this.engine.Monitor.tick = this.engine.SimTick.tickValue;
        }

        /// <summary>
        /// FixedUpdate开始前，剔除掉多余的Input
        /// </summary>
        /// <param name="targetTick"></param>
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
                            ticks += array[j].clientTargetTick;
                        }
                    }

                    // RiptideLogger.Log(LogType.Warning,
                    //     $"ServerTick:{this.engine.SimTick}，{ticks}  input count:{clientDatas[i].clientInput.Count}, Client ID: {i}");
                    while (clientInput.Count > 0 && clientInput.Peek().clientTargetTick < targetTick)
                    {
                        var input = clientInput.Dequeue();
                        if (input.clientTargetTick < targetTick)
                        {
                            this.RecycleInput(input);
                        }
                    }


                    // RiptideLogger.Log(LogType.Warning,
                    // $"ServerTick:{this.engine.SimTick}, ClientInput targetTick:{this.clientDatas[i].currentInput?.targetTick}, input count:{clientDatas[i].clientInput.Count}, Client ID: {i}");
                }
            }
        }

        public bool FetchInput<T>(out T input, int inputSource) where T : unmanaged, INetworkInput
        {
            input = default(T);
            if (inputSource == -1 || inputSource >= this.clientDatas.Length || !this.clientDatas[inputSource].Started)
                return false;
            ClientData clientData = this.clientDatas[inputSource];
            if (clientData.CurrentInput == null) return false;
            List<InputBlock> inputBlocks = clientData.CurrentInput.inputBlocks;
            string typeName = typeof(T).Name;
            if (!typeNameToTypeTable.TryGetValue(typeName, out var inputType)) return false;
            for (int i = 0; i < inputBlocks.Count; i++)
            {
                if (inputBlocks[i].type == inputType)
                {
                    input = inputBlocks[i].Get<T>();
                    return true;
                }
            }

            return false;
        }

        public SimulationInput GetSimulationInput(int inputSource)
        {
            if (inputSource < 0 || inputSource >= this.clientDatas.Length || !this.clientDatas[inputSource].Started) return null;
            return this.clientDatas[inputSource].CurrentInput;
        }
    }
}