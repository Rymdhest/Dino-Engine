using System.Runtime.CompilerServices;

namespace Dino_Engine.ECS.ECS_Architecture
{
    public struct BitMask : IEquatable<BitMask>
    {
        public static BitMask Empty => new BitMask();
        private ulong[] bitBlocks;

        public BitMask()
        {
            int bitCount = ComponentTypeRegistry.Count;
            int blocks = (bitCount + 63) / 64;
            bitBlocks = new ulong[blocks];
        }

        private BitMask(ulong[] blocks)
        {
            bitBlocks = blocks;
        }
        public BitMask(params Type[] components)
        {
            int bitCount = ComponentTypeRegistry.Count;
            int blocks = (bitCount + 63) / 64;
            bitBlocks = new ulong[blocks];
            foreach (Type type in components)
            {
                int bit = ComponentTypeRegistry.GetId(type);
                int block = bit / 64;
                int offset = bit % 64;
                bitBlocks[block] |= 1UL << offset;
            }
        }


        public void ClearBit(int bitIndex)
        {
            int block = bitIndex / 64;
            int offset = bitIndex % 64;
            bitBlocks[block] &= ~(1UL << offset);
        }

        public void SetBit(int bitIndex)
        {
            int block = bitIndex / 64;
            int offset = bitIndex % 64;
            bitBlocks[block] |= 1UL << offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitMask WithBit(int bit)
        {
            BitMask withCopy = Copy();
            withCopy.SetBit(bit);
            return withCopy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BitMask WithoutBit(int bit)
        {
            BitMask withoutCopy = Copy();
            withoutCopy.ClearBit(bit);
            return withoutCopy;
        }
        public BitMask Copy()
        {
            ulong[] copy = (ulong[])bitBlocks.Clone();
            return new BitMask(copy);
        }

        public bool ContainsAll(BitMask other)
        {
            for (int i = 0; i < bitBlocks.Length; i++)
            {
                if ((bitBlocks[i] & other.bitBlocks[i]) != other.bitBlocks[i])
                    return false;
            }
            return true;
        }
        public bool IntersectsAny(BitMask other)
        {
            for (int i = 0; i < bitBlocks.Length; i++)
            {
                if ((bitBlocks[i] & other.bitBlocks[i]) != 0)
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int bit)
        {
            int block = bit / 64;
            int offset = bit % 64;
            if (block >= bitBlocks.Length) return false;
            return (bitBlocks[block] & (1UL << offset)) != 0;
        }

        public bool Equals(BitMask other)
        {
            if (bitBlocks.Length != other.bitBlocks.Length) return false;
            for (int i = 0; i < bitBlocks.Length; i++)
                if (bitBlocks[i] != other.bitBlocks[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is BitMask other && Equals(other);
        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < bitBlocks.Length; i++)
                hash = hash * 31 + bitBlocks[i].GetHashCode();
            return hash;
        }

        public static bool operator ==(BitMask a, BitMask b) => a.Equals(b);
        public static bool operator !=(BitMask a, BitMask b) => !a.Equals(b);
    }

}
