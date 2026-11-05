using Dino_Engine.Sound.Dsp;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// A sound with no end that goes on being made while it plays, on the audio thread: wind, rain, fire, the sea. Unlike a take it can
    /// answer what's happening right now, through its parameters. Nothing here may allocate or take a lock.
    /// </summary>
    public abstract class LiveSound
    {
        protected readonly SoundRandom Random;

        // Parameters are told to it at the game's pace and eased towards, so nothing in it ever steps.
        private readonly float[] _targets;
        private readonly float[] _values;
        private readonly float _ease;
        private bool _begun;

        protected const int ControlRate = AudioFormat.SampleRate / 64;

        protected LiveSound(int parameters, ulong seed, float easeSeconds = 0.25f)
        {
            Random = new SoundRandom(seed);
            _targets = new float[parameters];
            _values = new float[parameters];
            _ease = 1f - MathF.Exp(-1f / (easeSeconds * ControlRate));
        }

        /// <summary>Called through the command queue, on the audio thread.</summary>
        public void SetTarget(int parameter, float value)
        {
            if ((uint)parameter >= (uint)_targets.Length) return;
            _targets[parameter] = value;
            if (!_begun) _values[parameter] = value;
        }

        protected float Value(int parameter) => _values[parameter];

        /// <summary>What a parameter was last told, not eased, for what's a count of things that have happened rather than a level.</summary>
        protected float Target(int parameter) => _targets[parameter];

        /// <summary>Fills a block of stereo. Called only on the audio thread.</summary>
        public void Render(Span<float> left, Span<float> right)
        {
            _begun = true;
            int done = 0;
            while (done < left.Length)
            {
                int count = Math.Min(64, left.Length - done);
                for (int i = 0; i < _values.Length; i++) _values[i] += (_targets[i] - _values[i]) * _ease;
                Control();
                Process(left.Slice(done, count), right.Slice(done, count));
                done += count;
            }
        }

        /// <summary>Called at <see cref="ControlRate"/> a second: work out whatever changes slowly.</summary>
        protected virtual void Control()
        {
        }

        /// <summary>Writes (not adds) up to 64 samples.</summary>
        protected abstract void Process(Span<float> left, Span<float> right);
    }

    /// <summary>A continuous sound the game can start. See <see cref="LiveSound"/>.</summary>
    public sealed class LiveDefinition
    {
        public readonly string Id;
        public readonly Func<ulong, LiveSound> Create;

        public SoundBus Bus = SoundBus.Ambience;
        public bool Positional;             // Otherwise it surrounds the listener, as weather does.
        public float NearDistance = 12f;
        public float FarDistance = 60f;
        public float ReverbSend = 0.1f;
        public int Priority = 80;
        public float Gain = 1f;
        public string[] Parameters = Array.Empty<string>();     // Names, for tools.
        public float[] Defaults = Array.Empty<float>();         // What they start at when a tool plays this.

        public LiveDefinition(string id, Func<ulong, LiveSound> create)
        {
            Id = id;
            Create = create;
        }

        public override string ToString() => Id;
    }
}
