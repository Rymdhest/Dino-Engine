using System.Text;

namespace Dino_Engine.Networking
{
    /// <summary>A message that doesn't hold what it should. Anything read off the network can be wrong, so this is an ordinary event, not a bug.</summary>
    public sealed class NetReadException : Exception
    {
        public NetReadException(string message) : base(message)
        {
        }
    }

    /// <summary>Packs values into as few bits as they need. Nothing in it is byte aligned, least significant bit first.</summary>
    public sealed class BitWriter
    {
        private byte[] _buffer;
        private int _length;
        private ulong _scratch;
        private int _scratchBits;

        public BitWriter(int capacity = 1536)
        {
            _buffer = new byte[capacity];
        }

        public int BitLength => _length * 8 + _scratchBits;
        public int ByteLength => _length + (_scratchBits + 7) / 8;

        public void Clear()
        {
            _length = 0;
            _scratch = 0;
            _scratchBits = 0;
        }

        public void WriteBits(uint value, int count)
        {
            ulong kept = count >= 32 ? value : value & ((1u << count) - 1u);
            _scratch |= kept << _scratchBits;
            _scratchBits += count;
            while (_scratchBits >= 8)
            {
                if (_length == _buffer.Length) Array.Resize(ref _buffer, _buffer.Length * 2);
                _buffer[_length++] = (byte)_scratch;
                _scratch >>= 8;
                _scratchBits -= 8;
            }
        }

        public void WriteBool(bool value) => WriteBits(value ? 1u : 0u, 1);

        public void WriteByte(byte value) => WriteBits(value, 8);

        public void WriteUInt(uint value) => WriteBits(value, 32);

        public void WriteInt(int value) => WriteBits((uint)value, 32);

        public void WriteFloat(float value) => WriteBits(BitConverter.SingleToUInt32Bits(value), 32);

        /// <summary>Small numbers small: seven bits at a time, with a bit after each to say whether more follow.</summary>
        public void WriteVarUInt(uint value)
        {
            while (value >= 0x80u)
            {
                WriteBits((value & 0x7Fu) | 0x80u, 8);
                value >>= 7;
            }
            WriteBits(value, 8);
        }

        public void WriteVarULong(ulong value)
        {
            while (value >= 0x80ul)
            {
                WriteBits((uint)(value & 0x7Ful) | 0x80u, 8);
                value >>= 7;
            }
            WriteBits((uint)value, 8);
        }

        public void WriteVarInt(int value) => WriteVarUInt(ZigZag(value));

        /// <summary>A change in a value: most are tiny, so its size is said first, in two bits.</summary>
        public void WriteDelta(int delta)
        {
            uint value = ZigZag(delta);
            if (value < 1u << 4)
            {
                WriteBits(0u, 2);
                WriteBits(value, 4);
            }
            else if (value < 1u << 8)
            {
                WriteBits(1u, 2);
                WriteBits(value, 8);
            }
            else if (value < 1u << 16)
            {
                WriteBits(2u, 2);
                WriteBits(value, 16);
            }
            else
            {
                WriteBits(3u, 2);
                WriteBits(value, 32);
            }
        }

        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteVarUInt((uint)bytes.Length);
            foreach (byte b in bytes) WriteBits(b, 8);
        }

        /// <summary>Copies what another writer holds onto the end of this one.</summary>
        public void Append(BitWriter other)
        {
            for (int i = 0; i < other._length; i++) WriteBits(other._buffer[i], 8);
            if (other._scratchBits > 0) WriteBits((uint)other._scratch, other._scratchBits);
        }

        /// <summary>The finished bytes, as a new array. The last byte is padded with zeroes.</summary>
        public byte[] ToArray()
        {
            byte[] result = new byte[ByteLength];
            Array.Copy(_buffer, result, _length);
            if (_scratchBits > 0) result[_length] = (byte)_scratch;
            return result;
        }

        private static uint ZigZag(int value) => (uint)((value << 1) ^ (value >> 31));
    }

    /// <summary>Reads what a <see cref="BitWriter"/> wrote. Reading past the end throws <see cref="NetReadException"/>, and so does anything else that can't be true.</summary>
    public sealed class BitReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _position;
        private ulong _scratch;
        private int _scratchBits;

        public BitReader(byte[] data) : this(data, 0, data.Length)
        {
        }

        public BitReader(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));
            _data = data;
            _position = offset;
            _end = offset + length;
        }

        public uint ReadBits(int count)
        {
            while (_scratchBits < count)
            {
                if (_position >= _end) throw new NetReadException("Read past the end of a message.");
                _scratch |= (ulong)_data[_position++] << _scratchBits;
                _scratchBits += 8;
            }
            uint value = count >= 32 ? (uint)_scratch : (uint)_scratch & ((1u << count) - 1u);
            _scratch >>= count;
            _scratchBits -= count;
            return value;
        }

        public bool ReadBool() => ReadBits(1) != 0;

        public byte ReadByte() => (byte)ReadBits(8);

        public uint ReadUInt() => ReadBits(32);

        public int ReadInt() => (int)ReadBits(32);

        public float ReadFloat() => BitConverter.UInt32BitsToSingle(ReadBits(32));

        public uint ReadVarUInt()
        {
            uint value = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                uint part = ReadBits(8);
                value |= (part & 0x7Fu) << shift;
                if ((part & 0x80u) == 0) return value;
            }
            throw new NetReadException("A number runs on too long.");
        }

        public ulong ReadVarULong()
        {
            ulong value = 0;
            for (int shift = 0; shift < 70; shift += 7)
            {
                ulong part = ReadBits(8);
                value |= (part & 0x7Ful) << shift;
                if ((part & 0x80ul) == 0) return value;
            }
            throw new NetReadException("A number runs on too long.");
        }

        public int ReadVarInt() => UnZigZag(ReadVarUInt());

        public int ReadDelta()
        {
            uint size = ReadBits(2);
            uint value = ReadBits(size switch { 0u => 4, 1u => 8, 2u => 16, _ => 32 });
            return UnZigZag(value);
        }

        public string ReadString(int mostBytes)
        {
            uint length = ReadVarUInt();
            if (length > mostBytes) throw new NetReadException($"A string of {length} bytes where at most {mostBytes} belong.");
            byte[] bytes = new byte[length];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)ReadBits(8);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>A count of things still to come, checked against what could possibly fit in what is left.</summary>
        public int ReadCount(int most)
        {
            uint count = ReadVarUInt();
            if (count > most) throw new NetReadException($"A count of {count} where at most {most} belong.");
            return (int)count;
        }

        private static int UnZigZag(uint value) => (int)(value >> 1) ^ -(int)(value & 1u);
    }
}
