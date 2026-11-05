using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Mixing
{
    /// <summary>
    /// The takes of one sound that are ready to play. Takes that have been heard enough are swapped for fresh ones while the game runs,
    /// so a sound keeps changing all session and the same footstep never comes round again. The audio thread only ever reads
    /// this: a take is swapped by replacing the slot with one finished object, so neither side needs a lock, and a take still
    /// playing lives on for as long as its voice holds it. A sound starts with a single take, so that it's ready the first time, and
    /// only fills up once it has been asked for: what a game never plays costs it one take and no more, however big the catalogue grows.
    /// </summary>
    internal sealed class SoundPool
    {
        private readonly SoundBuffer?[] _takes;
        private readonly int[] _plays;
        private int _last = -1;
        private volatile bool _wanted;

        public readonly SoundDefinition Definition;

        // Kept on the audio thread, for the definition's manners.
        public long LastStart = long.MinValue / 2;
        public int Playing;

        public SoundPool(SoundDefinition definition)
        {
            Definition = definition;
            _takes = new SoundBuffer?[Math.Max(definition.Takes, 1)];
            _plays = new int[_takes.Length];
        }

        public int Slots => _takes.Length;

        /// <summary>A take to play, never the one just played unless it's the only one. Null until the first is made. Audio thread only.</summary>
        public SoundBuffer? Next(SoundRandom random)
        {
            _wanted = true;
            int count = _takes.Length;
            int start = random.Int(count);
            int fallback = -1;
            for (int i = 0; i < count; i++)
            {
                int slot = (start + i) % count;
                if (Volatile.Read(ref _takes[slot]) == null) continue;
                if (slot == _last)
                {
                    fallback = slot;
                    continue;
                }
                return Take(slot);
            }
            return fallback >= 0 ? Take(fallback) : null;
        }

        private SoundBuffer Take(int slot)
        {
            _last = slot;
            Interlocked.Increment(ref _plays[slot]);
            return _takes[slot]!;
        }

        /// <summary>A slot that wants a take, by preference an empty one, or -1. Worker threads only.</summary>
        public int NeedsTake()
        {
            int slots = _wanted ? _takes.Length : 1;
            for (int slot = 0; slot < slots; slot++)
            {
                if (Volatile.Read(ref _takes[slot]) == null) return slot;
            }
            for (int slot = 0; slot < slots; slot++)
            {
                if (Volatile.Read(ref _plays[slot]) >= Definition.PlaysPerTake) return slot;
            }
            return -1;
        }

        public void Store(int slot, SoundBuffer take)
        {
            Volatile.Write(ref _takes[slot], take);
            Volatile.Write(ref _plays[slot], 0);
        }

        public bool Ready
        {
            get
            {
                foreach (SoundBuffer? take in _takes)
                {
                    if (take != null) return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Makes takes, on background threads. It starts by making one of everything as fast as the machine allows, the most urgent sounds first,
    /// which happens while a world is being grown anyway. After that it's a quiet job on one low priority thread, filling the sounds
    /// that get played and replacing takes that have been heard, at a pace that can't be noticed.
    /// </summary>
    internal sealed class SoundFactory : IDisposable
    {
        private const int MostPerSecond = 40;

        private readonly SoundPool[] _pools;
        private readonly Thread[] _workers;
        private readonly CancellationTokenSource _stop = new();
        private int _nextSeed = Environment.TickCount;
        private int _filling;
        private int _position;

        public int TakesMade;

        /// <summary>Whether every sound has at least one take.</summary>
        public bool Ready { get; private set; }

        public SoundFactory(IEnumerable<SoundPool> pools)
        {
            _pools = pools.OrderByDescending(p => p.Definition.Priority).ToArray();
            int workers = Math.Clamp(Environment.ProcessorCount / 2, 1, 6);
            _workers = new Thread[workers];
            _filling = workers;
            for (int i = 0; i < workers; i++)
            {
                _workers[i] = new Thread(Work)
                {
                    Name = $"Sound factory {i}",
                    IsBackground = true,
                    Priority = ThreadPriority.BelowNormal,
                };
                _workers[i].Start(i == 0);
            }
        }

        private void Work(object? keepGoing)
        {
            try
            {
                Fill();
                if (Interlocked.Decrement(ref _filling) == 0)
                {
                    Ready = true;
                    Console.WriteLine($"{TakesMade} takes of {_pools.Length} sounds ready.");
                }
                if (!(bool)keepGoing!) return;

                // Not until the others are done, or a slot still being filled looks like one that wants filling.
                while (Volatile.Read(ref _filling) > 0) _stop.Token.WaitHandle.WaitOne(5);
                Refresh();
            }
            catch (OperationCanceledException)
            {
            }
        }

        // One take of everything, so that nothing is ever silent the first time it's wanted.
        private void Fill()
        {
            while (true)
            {
                int index = Interlocked.Increment(ref _position) - 1;
                if (index >= _pools.Length) return;
                _stop.Token.ThrowIfCancellationRequested();
                int slot = _pools[index].NeedsTake();
                if (slot >= 0) Make(_pools[index], slot);
            }
        }

        private void Refresh()
        {
            while (!_stop.IsCancellationRequested)
            {
                bool any = false;
                foreach (SoundPool pool in _pools)
                {
                    int slot = pool.NeedsTake();
                    if (slot < 0) continue;
                    Make(pool, slot);
                    any = true;
                    _stop.Token.WaitHandle.WaitOne(1000 / MostPerSecond);
                }
                if (!any) _stop.Token.WaitHandle.WaitOne(100);
            }
        }

        private void Make(SoundPool pool, int slot)
        {
            ulong seed = (ulong)(uint)Interlocked.Increment(ref _nextSeed) * 0x9E3779B97F4A7C15UL;
            try
            {
                pool.Store(slot, pool.Definition.Render(seed));
                Interlocked.Increment(ref TakesMade);
            }
            catch (Exception e)
            {
                // A recipe gone wrong costs that sound, not the game. Silence in its place keeps it from being tried for ever.
                Console.WriteLine($"Sound '{pool.Definition.Id}' failed: {e.Message}");
                pool.Store(slot, new SoundBuffer(new float[1], null, 1));
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            foreach (Thread worker in _workers) worker.Join(500);
        }
    }
}
