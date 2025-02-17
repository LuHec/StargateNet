using System;

namespace StargateNet
{
    internal struct InputBlock
    {
        internal readonly unsafe byte* inputBlockPtr;
        internal readonly int inputSizeBytes;
        internal readonly int type;

        public unsafe InputBlock(byte* ptr, int inputSizeBytes, int type)
        {
            this.inputBlockPtr = ptr;
            this.inputSizeBytes = inputSizeBytes;
            this.type = type;
        }

        internal unsafe void SetInput<T>(T value) where T : unmanaged
        {
            T* ptr = (T*)this.inputBlockPtr;
            *ptr = value;
        }

        internal unsafe void CopyTo(InputBlock inputBlock)
        {
            if (inputBlock.type != this.type) return;
            for (int i = 0; i < inputBlock.inputSizeBytes; i++)
            {
                inputBlock.inputBlockPtr[i] = this.inputBlockPtr[i];
            }
        }

        internal unsafe void CopyByte(int index, byte data)
        {
            if (index >= inputSizeBytes)
            {
                throw new Exception("Input Out of range.");
            }

            inputBlockPtr[index] = data;
        }

        internal unsafe void Clear()
        {
            if (this.inputBlockPtr != null)
                MemoryAllocation.Clear(this.inputBlockPtr, this.inputSizeBytes);
        }

        internal unsafe T Get<T>() where T : unmanaged
        {
            T result = *(T*)this.inputBlockPtr;
            return result;
        }
    }
}