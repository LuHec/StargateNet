using System;

namespace StargateNet
{
    public class ReadWriteBuffer
    {
        public readonly long bufferBytes; // 缓冲区大小（字节）
        private unsafe byte* _buffer; // 缓冲区起始指针
        private unsafe byte* _writePosition; // 当前写入位置
        private unsafe byte* _readPosition; // 当前读取位置
        private int _bitPositionWrite; // 当前写入操作的位（0到7）
        private int _bitPositionRead; // 当前读取操作的位（0到7）

        private const int BitsPerByte = sizeof(byte) * 8; 
        private const int BitsPerInt = sizeof(int) * 8;  
        private const int BitsPerDouble = sizeof(double) * 8;

        public unsafe ReadWriteBuffer(long bufferBytes)
        {
            this.bufferBytes = bufferBytes;
            this._buffer = (byte*)MemoryAllocation.Malloc(this.bufferBytes); 
            this._writePosition = _buffer; 
            this._readPosition = _buffer;
            this._bitPositionWrite = 0; 
            this._bitPositionRead = 0; 
        }

        public unsafe void* Get()
        {
            return this._buffer;
        }

        #region Add 方法

        public unsafe void AddBool(bool value)
        {
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite >= bufferBytes * BitsPerByte)
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
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite + BitsPerByte > bufferBytes * BitsPerByte)
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
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite + BitsPerInt > bufferBytes * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }

            for (int i = 0; i < BitsPerInt; i++)
            {
                bool bitValue = (value & (1 << i)) != 0;
                AddBool(bitValue); // 按位存储整数
            }
        }

        public unsafe void AddDouble(double value)
        {
            if ((_writePosition - _buffer) * BitsPerByte + _bitPositionWrite + BitsPerDouble > bufferBytes * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }


            long bits = BitConverter.DoubleToInt64Bits(value);
            for (int i = 0; i < BitsPerDouble; i++)
            {
                bool bitValue = (bits & (1L << i)) != 0;
                AddBool(bitValue); // 按位存储double的64位表示
            }
        }

        #endregion

        #region Get 方法

        public unsafe bool GetBool()
        {
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead >= bufferBytes * BitsPerByte)
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
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead + BitsPerByte > bufferBytes * BitsPerByte)
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
            if (((_readPosition - _buffer) * BitsPerByte + _bitPositionRead + BitsPerInt) > (bufferBytes * BitsPerByte))
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

        public unsafe double GetDouble()
        {
            if ((_readPosition - _buffer) * BitsPerByte + _bitPositionRead + BitsPerDouble > bufferBytes * BitsPerByte)
            {
                throw new InvalidOperationException("Buffer underflow!");
            }

            long bits = 0;
            for (int i = 0; i < BitsPerDouble; i++)
            {
                bool bitValue = GetBool(); // 按位获取数据
                if (bitValue)
                {
                    bits |= (1L << i); // 设置对应位
                }
            }

            // Convert the 64-bit bit pattern back to a double
            return BitConverter.Int64BitsToDouble(bits);
        }

        #endregion

        public unsafe long RemainingSpace => bufferBytes - (_writePosition - _buffer);

        public unsafe void Reset()
        {
            _writePosition = _buffer; // 重置指针到缓冲区开始位置
            _readPosition = _buffer; // 重置读取指针
            _bitPositionWrite = 0; // 重置写入位指针
            _bitPositionRead = 0; // 重置读取位指针
            MemoryAllocation.Clear(_buffer, bufferBytes);
        }

        public unsafe void ResetRead()
        {
            _readPosition = _buffer;
            _bitPositionRead = 0;
        }

        public unsafe bool ReadEOF()
        {
            return (_readPosition - _buffer) * 8 + _bitPositionRead >= (_writePosition - _buffer) * 8 + _bitPositionWrite;
        }

        /// <summary>
        /// 返回已经读过的字节
        /// </summary>
        /// <returns>bytes</returns>
        public unsafe long BytesReadPosition()
        {
            return _readPosition - _buffer;
        }

        /// <summary>
        /// 向下取整，只在发包准备分包时使用
        /// </summary>
        /// <returns></returns>
        public unsafe long ReadRemainBytes()
        {
            if (_readPosition > _writePosition) return 0;
            if (_readPosition == _writePosition && _bitPositionRead >= _bitPositionWrite) return 0;
            long readRemainBits = (_writePosition + _bitPositionWrite - _readPosition) * 8 + _bitPositionRead;
            return (readRemainBits + 7) / 8;
        }

        /// <summary>
        /// 向上取整
        /// </summary>
        /// <returns></returns>
        public unsafe long GetUsedBytes()
        {
            long usedBits = (_writePosition - _buffer) * 8 + _bitPositionWrite;
            return (usedBits + 7) / 8;
        }

        public unsafe void CopyTo(ReadWriteBuffer dest, int from, int bytes)
        {
            if (dest.bufferBytes - from < bytes || this.bufferBytes < bytes)
            {
                throw new InvalidOperationException("Buffer overflow!");
            }

            for (int i = 0; i < bytes; i++)
            {
                dest._buffer[i + from] = this._buffer[i];
            }
        }

        public unsafe void SetSize(int bytes, int bits)
        {
            this._writePosition = this._buffer + bytes;
            this._bitPositionWrite = bits;
        }
    }
}