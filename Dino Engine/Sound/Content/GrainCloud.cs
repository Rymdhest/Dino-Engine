using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// A swarm of tiny sounds, for a live sound that is made of events: drops, crackles, bubbles. The wavelets are made ahead of time with the
    /// full toolkit, and playing one is only adding it in, so thousands a second cost almost nothing on the audio thread.
    /// </summary>
    public sealed class GrainCloud
    {
        private struct Playing
        {
            public float[] Samples;
            public int Position;
            public float Left, Right;
        }

        private readonly Playing[] _playing;
        private int _count;

        public GrainCloud(int mostAtOnce = 96)
        {
            _playing = new Playing[mostAtOnce];
        }

        /// <summary>Sets one off, from the left at -1 to the right at 1. If too many are going, it's lost, which nobody could hear anyway.</summary>
        public void Trigger(float[] wavelet, float gain, float pan = 0f)
        {
            if (_count >= _playing.Length) return;
            float angle = (Math.Clamp(pan, -1f, 1f) + 1f) * (MathF.PI * 0.25f);
            _playing[_count++] = new Playing { Samples = wavelet, Left = MathF.Cos(angle) * gain, Right = MathF.Sin(angle) * gain };
        }

        /// <summary>Adds everything playing into the block.</summary>
        public void Process(Span<float> left, Span<float> right)
        {
            int frames = left.Length;
            for (int g = _count - 1; g >= 0; g--)
            {
                ref Playing grain = ref _playing[g];
                float[] samples = grain.Samples;
                int count = Math.Min(frames, samples.Length - grain.Position);
                for (int i = 0; i < count; i++)
                {
                    float value = samples[grain.Position + i];
                    left[i] += value * grain.Left;
                    right[i] += value * grain.Right;
                }
                grain.Position += count;
                if (grain.Position >= samples.Length) _playing[g] = _playing[--_count];
            }
        }

        /// <summary>
        /// Makes a set of wavelets from a recipe. Each is rendered with no mastering, as a live sound sets its own level, and peaks at one.
        /// </summary>
        public static float[][] Make(ulong seed, int count, Action<Take> recipe)
        {
            var wavelets = new float[count][];
            for (int i = 0; i < count; i++)
            {
                using var take = new Take(seed + (ulong)i * 7919UL, stereo: false);
                recipe(take);
                SoundBuffer buffer = take.Finish(new Mastering { Target = null, Ceiling = 0f, Highpass = 20f, TailFloor = -60f, FadeOut = 0.002f, Stage = -100f });
                float peak = 0f;
                for (int s = 0; s < buffer.Length; s++) peak = MathF.Max(peak, MathF.Abs(buffer.Left[s]));
                float scale = peak > 0f ? 1f / peak : 0f;
                var wavelet = new float[buffer.Length];
                for (int s = 0; s < buffer.Length; s++) wavelet[s] = buffer.Left[s] * scale;
                wavelets[i] = wavelet;
            }
            return wavelets;
        }
    }
}
