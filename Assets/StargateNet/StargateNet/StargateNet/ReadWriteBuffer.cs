using System;

namespace StargateNet
{
    public class ReadWriteBuffer
    {
        public readonly long bufferSize;        // 缓冲区大小（字节）
        private unsafe byte* _buffer;           // 缓冲区起始指针
        private unsafe byte* _writePosition;  // 当前写入位置
        private unsafe byte* _readPosition;     // 当前读取位置
        private int _bitPositionWrite;          // 当前写入操作的位（0到7）
        private int _bitPositionRead;           // 当前读取操作的位（0到7）

        private const int BitsPerByte = sizeof(byte) * 8;      // 每字节8位
        private const int BitsPerInt = sizeof(int) * 8;        // 每整数32位

        public unsafe ReadWriteBuffer(long bufferSize)
        {
            this.bufferSize = bufferSize;
            this._buffer = (byte*)MemoryAllocation.Malloc(this.bufferSize); // 分配字节数组
            this._writePosition = _buffer; // 初始化时指针指向缓冲区的开始位置
            this._readPosition = _buffer; // 读取指针初始化为缓冲区开始
            this._bitPositionWrite = 0; // 初始化写入位指针
            this._bitPositionRead = 0;  // 初始化读取位指针
        }

        #region Add 方法

        public unsafe void AddBool(bool value)
        {
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite >= bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }

            byte bitMask = (byte)(1 << _bitPositionWrite); // 计算当前位的位置
            if (value)
            {
                *(_writePosition) |= bitMask; // 设置相应位为1
            }
            else
            {
                *(_writePosition) &= (byte)~bitMask; // 清除相应位为0
            }

            _bitPositionWrite++; // 移动到下一个位

            if (_bitPositionWrite > 7)
            {
                _bitPositionWrite = 0; // 重置位指针
                _writePosition++; // 移动到下一个 byte
            }
        }

        public unsafe void AddByte(byte value)
        {
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite + BitsPerByte > bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }

            for (int i = 0; i < BitsPerByte; i++)
            {
                bool bitValue = (value & (1 << i)) != 0;
                AddBool(bitValue); // 按位添加字节
            }
        }

        public unsafe void AddInt(int value)
        {
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite + BitsPerInt > bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }

            for (int i = 0; i < BitsPerInt; i++)
            {
                bool bitValue = (value & (1 << i)) != 0;
                AddBool(bitValue); // 按位存储整数
            }
        }

        #endregion

        #region Get 方法

        public unsafe bool GetBool()
        {
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead >= bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer underflow!");
            }

            byte bitMask = (byte)(1 << _bitPositionRead); // 计算当前位的位置
            bool value = (*(_readPosition) & bitMask) != 0; // 检查当前位的值

            _bitPositionRead++; // 移动到下一个位

            if (_bitPositionRead > 7)
            {
                _bitPositionRead = 0; // 重置位指针
                _readPosition++; // 移动到下一个 byte
            }

            return value;
        }

        public unsafe byte GetByte()
        {
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead + BitsPerByte > bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer underflow!");
            }

            byte result = 0;
            for (int i = 0; i < BitsPerByte; i++)
            {
                bool bitValue = GetBool(); // 按位获取数据
                if (bitValue)
                {
                    result |= (byte)(1 << i); // 设置字节的对应位
                }
            }

            return result;
        }

        public unsafe int GetInt()
        {
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead + BitsPerInt > bufferSize * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer underflow!");
            }

            int result = 0;
            for (int i = 0; i < BitsPerInt; i++)
            {
                bool bitValue = GetBool(); // 按位获取数据
                if (bitValue)
                {
                    result |= (1 << i); // 设置整数的对应位
                }
            }

            return result;
        }

        #endregion

        public unsafe long RemainingSpace => bufferSize - (_writePosition - _buffer);

        public unsafe void Reset()
        {
            _writePosition = _buffer; // 重置指针到缓冲区开始位置
            _readPosition = _buffer; // 重置读取指针
            _bitPositionWrite = 0; // 重置写入位指针
            _bitPositionRead = 0;  // 重置读取位指针
        }

        public unsafe bool EOF()
        {
            return (_readPosition - _buffer) * 8 + _bitPositionRead >= (_writePosition - _buffer) * 8 + _bitPositionWrite;
        }

        public unsafe long GetUsedBytes()
        {
            long usedBits = (_writePosition - _buffer) * 8 + _bitPositionWrite;
            return (usedBits + 7) / 8;
        }
    }
}
