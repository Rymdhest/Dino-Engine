using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace Dino_Engine.Particles
{
    /// <summary>One particle as the GPU keeps it. The layout must match struct Particle in Shaders/Particles/Particle.glsl (std430, 96 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuParticle
    {
        public Vector3 Position;
        public float Age;           // Seconds.
        public Vector3 Velocity;
        public float Life;          // Seconds it lives for in all.
        public Vector3 Axis;        // Where a Segment ends, from Position. The ground's normal, once it has landed.
        public float Size;          // Metres across, at birth.
        public float Angle;         // Radians.
        public float Spin;          // Radians a second. The age it landed at, once it has.
        public float Seed;          // 0 to 1, its own: which flipbook, where in it, how it flickers.
        public uint Style;          // Its place in the style table, and flags above StyleBits.
        public Vector4 Ground;      // The plane of the ground under where it was born (normal, distance), or 0 for none.
        public Vector4 Tint;        // rgb times its colour, a times its strength.

        public const int Bytes = 96;
        public const int StyleBits = 16;
    }

    /// <summary>How much a particle matters when there's no room for everything. What's on the ground and in the air for its own sake gives way first.</summary>
    public enum ParticlePriority
    {
        Ambient,    // Motes, smoke from far chimneys: only while the pool is well short of full.
        Normal,
        Vital,      // What says something has just happened in a fight.
    }

    /// <summary>
    /// Where every live particle is in the GPU's buffer. Each particle's life is fixed when it's born, so the CPU knows to the frame when every slot comes free and how many are live,
    /// and nothing has to be read back: new particles go into the lowest free slots, so the live ones stay packed at the start and the GPU only works through as far as the highest.
    /// Particles born this frame wait in <see cref="Spawns"/> until the renderer sends them.
    /// </summary>
    public sealed class ParticlePool
    {
        public readonly int Capacity;
        public readonly int SpawnCapacity;

        private readonly MinHeap<int> _free;
        private readonly MinHeap<(double Death, int Slot)> _deaths;
        private readonly bool[] _live;

        public readonly GpuParticle[] Spawns;
        public readonly int[] SpawnSlots;
        public int SpawnCount { get; private set; }

        /// <summary>Seconds the particles have lived through: it stops while they're paused.</summary>
        public double Clock { get; private set; }
        public int Live { get; private set; }
        public int HighWater { get; private set; }
        public int Dropped { get; private set; }

        public ParticlePool(int capacity, int spawnCapacity)
        {
            Capacity = capacity;
            SpawnCapacity = spawnCapacity;
            _free = new MinHeap<int>(capacity);
            _deaths = new MinHeap<(double, int)>(capacity);
            _live = new bool[capacity];
            for (int i = 0; i < capacity; i++) _free.Push(i);
            Spawns = new GpuParticle[spawnCapacity];
            SpawnSlots = new int[spawnCapacity];
        }

        /// <summary>Share of the pool a particle of this priority may fill.</summary>
        public static float Room(ParticlePriority priority) => priority switch
        {
            ParticlePriority.Ambient => 0.6f,
            ParticlePriority.Normal => 0.92f,
            _ => 1f,
        };

        public bool TrySpawn(in GpuParticle particle, ParticlePriority priority = ParticlePriority.Normal)
        {
            float left = particle.Life - particle.Age;
            if (!(left > 0f)) return false;
            if (SpawnCount >= SpawnCapacity || _free.Count == 0 || Live >= Capacity * Room(priority))
            {
                Dropped++;
                return false;
            }

            int slot = _free.Pop();
            _live[slot] = true;
            Live++;
            if (slot >= HighWater) HighWater = slot + 1;
            _deaths.Push((Clock + left, slot));

            Spawns[SpawnCount] = particle;
            SpawnSlots[SpawnCount] = slot;
            SpawnCount++;
            return true;
        }

        /// <summary>Moves the clock on and frees the slots of everything whose life is over. The GPU ages particles by the same step.</summary>
        public void Advance(float seconds)
        {
            Clock += seconds;
            while (_deaths.Count > 0 && _deaths.Peek().Death <= Clock)
            {
                int slot = _deaths.Pop().Slot;
                _live[slot] = false;
                _free.Push(slot);
                Live--;
            }
            while (HighWater > 0 && !_live[HighWater - 1]) HighWater--;
        }

        /// <summary>The renderer has sent this frame's new particles.</summary>
        public void Sent()
        {
            SpawnCount = 0;
            Dropped = 0;
        }

        /// <summary>Forgets every particle, as when a world is left. The GPU's copies are still alive in the slots, so the renderer has to wipe them (<see cref="Wiped"/>).</summary>
        public void Clear()
        {
            while (_deaths.Count > 0)
            {
                int slot = _deaths.Pop().Slot;
                _live[slot] = false;
                _free.Push(slot);
            }
            Live = 0;
            HighWater = 0;
            SpawnCount = 0;
            NeedsWipe = true;
        }

        /// <summary>Whether what's on the GPU has to be wiped before anything new is sent.</summary>
        public bool NeedsWipe { get; private set; }

        public void Wiped() => NeedsWipe = false;

        private sealed class MinHeap<T> where T : IComparable<T>
        {
            private readonly T[] _items;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new T[capacity];
            }

            public T Peek() => _items[0];

            public void Push(T item)
            {
                int i = Count++;
                _items[i] = item;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_items[parent].CompareTo(_items[i]) <= 0) break;
                    (_items[parent], _items[i]) = (_items[i], _items[parent]);
                    i = parent;
                }
            }

            public T Pop()
            {
                T top = _items[0];
                _items[0] = _items[--Count];
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    if (left >= Count) break;
                    int smallest = left + 1 < Count && _items[left + 1].CompareTo(_items[left]) < 0 ? left + 1 : left;
                    if (_items[i].CompareTo(_items[smallest]) <= 0) break;
                    (_items[i], _items[smallest]) = (_items[smallest], _items[i]);
                    i = smallest;
                }
                return top;
            }
        }
    }
}
