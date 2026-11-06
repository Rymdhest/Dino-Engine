using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Synthesis;

namespace Dino_Engine.Sound.Content
{
    /// <summary>
    /// The sound of places: everything that goes on without end and changes with what's happening. All of it is made live, as a real wind
    /// has no loop point, and is built to the statistics of recordings of the real thing, since a texture is recognised by its statistics, not its
    /// details (McDermott and Simoncelli). A wind heard here is the very wind that's bending the grass.
    /// </summary>
    [SoundCollection]
    public static class Soundscape
    {
        public static readonly LiveDefinition Wind = SoundCatalog.Add(new LiveDefinition("live.wind", seed => new WindSound(seed))
        {
            Gain = 0.56f,
            Parameters = new[] { "Speed", "Gust", "Trees", "Grass", "Exposure", "Crest" },
            Defaults = new[] { 6f, 0.5f, 0.3f, 0.5f, 0.3f, 0.4f },
        });

        public static readonly LiveDefinition Rain = SoundCatalog.Add(new LiveDefinition("live.rain", seed => new RainSound(seed))
        {
            Parameters = new[] { "Intensity", "Trees", "Water" },
            Defaults = new[] { 0.6f, 0.2f, 0.1f },
        });

        public static readonly LiveDefinition Drips = SoundCatalog.Add(new LiveDefinition("live.drips", seed => new DripSound(seed))
        {
            Parameters = new[] { "Rate", "Water" },
            Defaults = new[] { 0.5f, 0.3f },
        });

        public static readonly LiveDefinition Fire = SoundCatalog.Add(new LiveDefinition("live.fire", seed => new FireSound(seed))
        {
            Positional = true,
            NearDistance = 7f,
            FarDistance = 44f,
            ReverbSend = 0.08f,
            Parameters = new[] { "Size" },
            Defaults = new[] { 0.5f },
        });

        public static readonly LiveDefinition Ocean = SoundCatalog.Add(new LiveDefinition("live.ocean", seed => new OceanSound(seed))
        {
            Parameters = new[] { "Near", "Swell", "Side", "Wave" },
            Defaults = new[] { 0.8f, 0.6f, 0f, 0f },
        });

        public static readonly LiveDefinition Lapping = SoundCatalog.Add(new LiveDefinition("live.lapping", seed => new LappingSound(seed))
        {
            Parameters = new[] { "Near", "Chop", "Side" },
            Defaults = new[] { 0.8f, 0.4f, 0f },
        });

        public static readonly LiveDefinition Stream = SoundCatalog.Add(new LiveDefinition("live.stream", seed => new StreamSound(seed))
        {
            Positional = true,
            NearDistance = 8f,
            FarDistance = 50f,
            Parameters = new[] { "Flow" },
            Defaults = new[] { 0.6f },
        });
    }

    /// <summary>
    /// Air moving over land. Wind has no sound of its own: what's heard is everything in its way. So this follows what's seen, not a sound of its own. It's told the speed of the wind that's
    /// bending the plants around the hero and how much of a wave is passing over them now, and what it makes is what those plants do: leaves and blades rustling, each a small event, which
    /// is why it gets busier as it gets stronger, where a filtered noise would only get louder. A breeze is a few of them going by and very little else. Air itself only gets a voice in a
    /// real wind, and low down, as measured wind has most of its sound under 2 kHz and little above 4: a noise with its top left in is a hiss, however quiet it's made.
    /// </summary>
    public sealed class WindSound : LiveSound
    {
        public const int Speed = 0;     // Metres a second, gusts and all.
        public const int Gust = 1;      // 0..1, from the wind field at the listener.
        public const int Trees = 2;     // 0..1 how much canopy is around.
        public const int Grass = 3;     // 0..1 how much tall grass is around.
        public const int Exposure = 4;  // 0..1 how bare the place is: it's in the open that wind howls.
        public const int Crest = 5;     // 0..1 how much of a wave is going through the plants here, as seen.

        private const float Reference = 8f;     // Metres a second that the levels below are given at.

        private readonly float[][] _leaves;
        private readonly float[][] _blades;
        private readonly GrainCloud _cloud = new(192);
        private PinkNoise _pinkLeft, _pinkRight;
        private Svf _bodyLeft, _bodyRight, _rumbleLeft, _rumbleRight;
        private Svf _howlOne, _howlTwo, _howlThree;
        private Wander _sideDrift, _pressure, _howlDrift;
        private float _bodyGain, _rumbleGain, _howlGain, _leafRate, _bladeRate, _grainGain, _sideLeft = 1f, _sideRight = 1f;

        public WindSound(ulong seed) : base(6, seed, 0.35f)
        {
            _leaves = GrainCloud.Make(seed, 24, Leaf);
            _blades = GrainCloud.Make(seed + 1, 24, Blade);
            _sideDrift.Set(3.4f, ControlRate);
            _pressure.Set(5.5f, ControlRate);
            _howlDrift.Set(2.2f, ControlRate);
        }

        private static void Leaf(Take take)
        {
            SoundRandom random = take.Random;
            float decay = random.LogRange(0.002f, 0.012f);
            take.Mix(take.White(decay * 7f).Bandpass(random.LogRange(500f, 2000f), 1.3f).Lowpass(2600f).Percussive(0.0005f, decay));
        }

        private static void Blade(Take take)
        {
            SoundRandom random = take.Random;
            float decay = random.LogRange(0.004f, 0.02f);
            take.Mix(take.White(decay * 7f).Bandpass(random.LogRange(700f, 2400f), 0.9f).Lowpass(3000f).Percussive(0.002f, decay));
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float speed = MathF.Max(Value(Speed), 0f);
            float exposure = Value(Exposure);

            // A wave passing through the plants is heard passing, and between waves they rest. That swell is the very one on screen, and only a little of anything else is added to it.
            float moving = speed * (0.7f + 0.55f * Value(Crest) + 0.15f * Value(Gust)) * MathF.Max(1f + _pressure.Next(Random, Step) * 0.12f, 0.2f);
            float strength = moving / Reference;

            float side = _sideDrift.Next(Random, Step) * 0.22f;
            _sideLeft = 1f + side;
            _sideRight = 1f - side;

            // Aerodynamic sound goes up steeply with speed: a light air is hardly there, a gale is a wall.
            _bodyGain = 0.05f * MathF.Pow(strength, 2f);
            float bodyPitch = 300f + moving * 42f;
            _bodyLeft.Set(bodyPitch, 0.6f);
            _bodyRight.Set(bodyPitch * 1.09f, 0.6f);

            float gale = MathF.Max(moving - 7f, 0f) / 8f;
            _rumbleGain = 0.035f * gale * gale * (0.15f + 0.85f * exposure);
            _rumbleLeft.Set(70f + moving * 3f, 0.9f);
            _rumbleRight.Set(78f + moving * 3f, 0.9f);

            // How many leaves and blades are stirring, and how hard, both going up with the speed.
            float stirring = MathF.Pow(strength, 1.3f);
            _leafRate = 700f * Value(Trees) * stirring;
            _bladeRate = 800f * Value(Grass) * stirring;
            _grainGain = 0.024f * MathF.Pow(strength, 0.75f);

            // The howl only comes with real wind, and in the open. Its pitch is a Strouhal frequency, so it follows the gusts, as the
            // measured one does: a narrow band a few hundred hertz wide wandering about 700 Hz, with a faint second harmonic.
            float howl = Math.Clamp((moving - 10f) / 8f, 0f, 1.4f) * exposure;
            _howlGain = 0.13f * howl * howl;
            float wobble = 1f + _howlDrift.Next(Random, Step) * 0.14f;
            float pitch = (240f + moving * 34f) * wobble;
            _howlOne.Set(pitch, 26f);
            _howlTwo.Set(pitch * 1.52f, 30f);
            _howlThree.Set(pitch * 2.03f, 22f);
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                float pinkLeft = _pinkLeft.Next(Random.Bipolar());
                float pinkRight = _pinkRight.Next(Random.Bipolar());
                float shared = (pinkLeft + pinkRight) * 0.5f;

                float howl = (_howlOne.Bandpass(shared) + _howlTwo.Bandpass(shared) * 0.4f + _howlThree.Bandpass(shared) * 0.2f) * _howlGain;

                float l = _bodyLeft.Bandpass(pinkLeft) * _bodyGain * _sideLeft;
                float r = _bodyRight.Bandpass(pinkRight) * _bodyGain * _sideRight;
                l += _rumbleLeft.Bandpass(pinkLeft) * _rumbleGain;
                r += _rumbleRight.Bandpass(pinkRight) * _rumbleGain;

                left[i] = l + howl * 0.8f;
                right[i] = r + howl;
            }

            float seconds = left.Length / (float)AudioFormat.SampleRate;
            Stir(_leaves, _leafRate * seconds);
            Stir(_blades, _bladeRate * seconds);
            _cloud.Process(left, right);
        }

        private void Stir(float[][] wavelets, float expected)
        {
            while (expected > 0f)
            {
                if (Random.Float() < expected)
                {
                    // Most are faint and a long way off, and now and then one's close.
                    float near = Random.Float();
                    _cloud.Trigger(wavelets[Random.Int(wavelets.Length)], _grainGain * (0.3f + 1.6f * near * near * near), Random.Bipolar());
                }
                expected -= 1f;
            }
        }
    }

    /// <summary>
    /// Rain is a texture of a great many small impacts, so it's a steady wash of the ones too many to count, with the near drops laid over it one at a time. What's heard of it is what it lands on, as
    /// the wind is what's in its way: a low, wide patter on the ground, a brighter one with more separate ticks on leaves, and on water a softer rush with bubbles in it. The top of the spectrum is
    /// kept down, as a noise with its top left in is a hiss, and this has to sit under a whole fight.
    /// </summary>
    public sealed class RainSound : LiveSound
    {
        public const int Intensity = 0; // 0..1 a shower to a downpour.
        public const int Trees = 1;     // 0..1 how much of it lands on leaves.
        public const int Water = 2;     // 0..1 how much of it lands on water.

        private readonly float[][] _hardDrops;
        private readonly float[][] _leafDrops;
        private readonly float[][] _waterDrops;
        private readonly GrainCloud _cloud = new(160);
        private PinkNoise _pinkLeft, _pinkRight;
        private Svf _groundLeft, _groundRight, _leafLeft, _leafRight, _waterLeft, _waterRight, _lowLeft, _lowRight;
        private OnePole _topLeft, _topRight;
        private Wander _swell;
        private float _onGround, _onLeaves, _onWater, _low, _hardRate, _leafRate, _waterRate, _dropGain;

        public RainSound(ulong seed) : base(3, seed, 1.5f)
        {
            _hardDrops = GrainCloud.Make(seed, 24, HardDrop);
            _leafDrops = GrainCloud.Make(seed + 1, 16, LeafDrop);
            _waterDrops = GrainCloud.Make(seed + 2, 24, WaterDrop);
            _swell.Set(4f, ControlRate);
            _groundLeft.Set(620f, 0.42f);
            _groundRight.Set(700f, 0.42f);
            _leafLeft.Set(2300f, 0.55f);
            _leafRight.Set(2700f, 0.55f);
            _waterLeft.Set(1150f, 0.7f);
            _waterRight.Set(1300f, 0.7f);
            _lowLeft.Set(160f, 0.7f);
            _lowRight.Set(180f, 0.7f);
            _topLeft.SetCutoff(5200f);
            _topRight.SetCutoff(5600f);
        }

        private static void HardDrop(Take take)
        {
            SoundRandom random = take.Random;
            float decay = random.LogRange(0.0008f, 0.004f);
            take.Mix(take.White(decay * 7f).Bandpass(random.LogRange(800f, 3400f), 1.1f).Lowpass(5200f).Percussive(0.00008f, decay));
        }

        private static void LeafDrop(Take take)
        {
            SoundRandom random = take.Random;
            float decay = random.LogRange(0.002f, 0.009f);
            take.Mix(take.White(decay * 7f).Bandpass(random.LogRange(1200f, 5200f), 1.5f).Percussive(0.0002f, decay));
        }

        private static void WaterDrop(Take take)
        {
            SoundRandom random = take.Random;
            Signal drop = take.Track(0.05f);
            take.Mix(take.White(0.004f).Highpass(1800f).Percussive(0.00005f, 0.0006f), 0f, -4f);
            Liquid.Bubble(drop, random.Range(0.002f, 0.012f), random.LogRange(0.00035f, 0.0016f), 1f, random.Range(0.1f, 0.4f));
            take.Mix(drop);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float intensity = Value(Intensity);
            float swell = 1f + _swell.Next(Random, Step) * 0.3f;
            float level = MathF.Pow(intensity, 1.55f) * swell * 0.26f;

            float trees = Value(Trees);
            float water = Value(Water);
            float ground = MathF.Max(1f - trees - water, 0f);
            _onGround = level * ground;
            _onLeaves = level * trees * 0.8f;
            _onWater = level * water * 0.9f;
            _low = 0.06f * MathF.Pow(intensity, 2f) * swell;
            _dropGain = 0.006f + 0.011f * intensity;

            float count = 90f + 1900f * intensity * intensity;
            _hardRate = count * ground;
            _leafRate = count * trees * 1.2f;
            _waterRate = count * water * 0.8f;
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                float l = _pinkLeft.Next(Random.Bipolar());
                float r = _pinkRight.Next(Random.Bipolar());
                float washLeft = _groundLeft.BandpassNormal(l) * _onGround + _leafLeft.BandpassNormal(l) * _onLeaves + _waterLeft.BandpassNormal(l) * _onWater;
                float washRight = _groundRight.BandpassNormal(r) * _onGround + _leafRight.BandpassNormal(r) * _onLeaves + _waterRight.BandpassNormal(r) * _onWater;
                left[i] = _topLeft.Lowpass(washLeft) + _lowLeft.Lowpass(l) * _low;
                right[i] = _topRight.Lowpass(washRight) + _lowRight.Lowpass(r) * _low;
            }

            float seconds = left.Length / (float)AudioFormat.SampleRate;
            Fall(_hardDrops, _hardRate, seconds, 1f);
            Fall(_leafDrops, _leafRate, seconds, 1.1f);
            Fall(_waterDrops, _waterRate, seconds, 0.8f);
            _cloud.Process(left, right);
        }

        private void Fall(float[][] drops, float rate, float seconds, float level)
        {
            float expected = rate * seconds;
            while (expected > 0f)
            {
                if (Random.Float() < expected)
                {
                    // Most are far away and small, and now and then one lands right by the ear.
                    float size = Random.Float();
                    float gain = _dropGain * level * (0.25f + size * size * size * size * size * size * 3f);
                    _cloud.Trigger(drops[Random.Int(drops.Length)], gain, Random.Bipolar());
                }
                expected -= 1f;
            }
        }
    }

    /// <summary>
    /// Water coming off things, as it's drawn (Weather/Drip.glsl): a few places nearby, each with a beat of its own that it keeps to, as a drip does, so it's heard as this one and that one rather than as
    /// a texture. Most of what lands, lands on something and only taps. What lands in standing water plinks, as the drop drags a bubble under with it, which is why those carry so much further. This is
    /// what's left to hear when the rain stops, so it has to stay small.
    /// </summary>
    public sealed class DripSound : LiveSound
    {
        public const int Rate = 0;      // 0..1 how many of their turns come to a drop.
        public const int Water = 1;     // 0..1 how much of it lands in standing water.

        private const int Places = 7;

        private readonly float[][] _plinks;
        private readonly float[][] _taps;
        private readonly GrainCloud _cloud = new(16);
        private readonly float[] _period = new float[Places];
        private readonly float[] _until = new float[Places];
        private readonly float[] _pan = new float[Places];
        private readonly float[] _gain = new float[Places];
        private readonly int[] _grain = new int[Places];
        private float _rate, _water;

        public DripSound(ulong seed) : base(2, seed, 1.5f)
        {
            _plinks = GrainCloud.Make(seed, 12, Plink);
            _taps = GrainCloud.Make(seed + 1, 12, Tap);
            for (int i = 0; i < Places; i++)
            {
                _period[i] = Random.Range(0.6f, 2.8f);
                _until[i] = Random.Range(0f, _period[i]);
                _pan[i] = Random.Bipolar() * 0.8f;
                _gain[i] = Random.LogRange(0.025f, 0.1f);
                _grain[i] = Random.Int(12);
            }
        }

        private static void Plink(Take take)
        {
            SoundRandom random = take.Random;
            Signal drop = take.Track(0.12f);
            take.Mix(take.White(0.003f).Highpass(1500f).Percussive(0.00005f, 0.0005f), 0f, -10f);
            Liquid.Bubble(drop, random.Range(0.001f, 0.004f), random.LogRange(0.001f, 0.0035f), 1f, random.Range(0.15f, 0.5f));
            take.Mix(drop);
        }

        private static void Tap(Take take)
        {
            SoundRandom random = take.Random;
            float decay = random.LogRange(0.004f, 0.012f);
            take.Mix(take.White(decay * 6f).Bandpass(random.LogRange(450f, 1500f), 1.4f).Lowpass(3800f).Percussive(0.0002f, decay));
        }

        protected override void Control()
        {
            _rate = Value(Rate);
            _water = Value(Water);
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            left.Clear();
            right.Clear();
            float seconds = left.Length / (float)AudioFormat.SampleRate;
            for (int i = 0; i < Places; i++)
            {
                _until[i] -= seconds;
                if (_until[i] > 0f) continue;
                _until[i] += _period[i];
                if (Random.Float() >= _rate) continue;
                float[][] drops = Random.Float() < _water ? _plinks : _taps;
                _cloud.Trigger(drops[_grain[i]], _gain[i], _pan[i]);
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>
    /// Fire, after Farnell's three parts: the low roar of the gases, the hiss of the flame, and the crackle of wood splitting as water
    /// inside it boils. A recording says the crackle is far sparser and sharper than one would guess, its envelope kurtosis near a thousand, a run of
    /// clicks with silence between, in clusters, and the roar sits almost entirely under 300 Hz.
    /// </summary>
    public sealed class FireSound : LiveSound
    {
        public const int Size = 0;      // 0..1 a candle to a bonfire.

        private readonly float[][] _crackles;
        private readonly float[][] _pops;
        private readonly GrainCloud _cloud = new(48);
        private PinkNoise _pinkLeft, _pinkRight;
        private BrownNoise _brownLeft, _brownRight;
        private Svf _roarLeft, _roarRight, _flapLeft, _flapRight, _hissLeft, _hissRight;
        private Wander _flame, _rate, _breath;
        private float _roar, _flap, _hiss, _crackleRate, _crackleGain, _burst, _untilBurst = 1f;

        public FireSound(ulong seed) : base(1, seed, 1f)
        {
            _crackles = GrainCloud.Make(seed, 40, Crackle);
            _pops = GrainCloud.Make(seed + 1, 12, Pop);
            _flame.Set(0.3f, ControlRate);
            _rate.Set(3f, ControlRate);
            _breath.Set(1.2f, ControlRate);
            _roarLeft.Set(125f, 0.8f);
            _roarRight.Set(140f, 0.8f);
            _flapLeft.Set(34f, 1.8f);
            _flapRight.Set(38f, 1.8f);
            _hissLeft.Set(900f, 0.35f);
            _hissRight.Set(1200f, 0.35f);
        }

        private static void Crackle(Take take)
        {
            SoundRandom random = take.Random;

            // A click as short as a sample or two: the bright, thin line that runs up the spectrum in a recording.
            float decay = random.LogRange(0.00012f, 0.0009f);
            take.Mix(take.White(decay * 8f + 0.0004f).Highpass(random.LogRange(600f, 3000f)).Percussive(0.00002f, decay));
        }

        private static void Pop(Take take)
        {
            SoundRandom random = take.Random;

            // A knot of wood going off: a real crack with something of a body, and a spray of smaller ones after it.
            take.Mix(take.White(0.012f).Highpass(500f).Percussive(0.00003f, 0.0014f));
            var blow = new Blow { Material = Material.Wood, Shape = ModalShape.Solid, Fundamental = random.LogRange(700f, 1700f), Modes = 10, Tilt = -1f, Rattle = 1f, Noise = -3f, NoiseLow = 300f };
            take.Mix(Impact.Of(take, blow, 0.05f).Normalize(0f), 0f, -7f);
            take.Mix(Grains.Settle(take, 0.06f, 9f, 0.015f, new GrainStyle { LowHz = 2000f, HighHz = 12000f, MinDecay = 0.00015f, MaxDecay = 0.0008f, Partials = 1, Noise = 0.8f, Spread = 18f }).Normalize(0f), 0.001f, -9f);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float size = Value(Size);
            float lick = 1f + _flame.Next(Random, Step) * 0.6f;
            float breath = 1f + _breath.Next(Random, Step) * 0.35f;

            _roar = (0.35f + 0.65f * size) * 0.05f * (0.3f + lick * lick) * breath;
            _flap = (0.3f + 0.7f * size) * 0.13f * lick * lick;
            _hiss = 0.03f * (0.5f + size) * lick * lick * breath;

            // How much is catching at the moment wanders, which makes the crackle come and go.
            float mood = 0.5f + 0.5f * _rate.Next(Random, Step);
            _crackleRate = (8f + 38f * size) * (0.15f + mood * mood * 2.4f);
            _crackleGain = 0.3f + 0.4f * size;
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                float whiteLeft = Random.Bipolar();
                float whiteRight = Random.Bipolar();
                float l = _brownLeft.Next(whiteLeft);
                float r = _brownRight.Next(whiteRight);
                float pinkLeft = _pinkLeft.Next(whiteLeft);
                float pinkRight = _pinkRight.Next(whiteRight);

                left[i] = _roarLeft.Lowpass(l) * _roar + _flapLeft.Bandpass(l) * _flap * 0.1f + _hissLeft.BandpassNormal(pinkLeft) * _hiss;
                right[i] = _roarRight.Lowpass(r) * _roar + _flapRight.Bandpass(r) * _flap * 0.1f + _hissRight.BandpassNormal(pinkRight) * _hiss;
            }

            float seconds = left.Length / (float)AudioFormat.SampleRate;
            float expected = _crackleRate * seconds;
            if (_burst > 0f)
            {
                // Inside a cluster they come fast.
                _burst -= seconds;
                expected += 90f * seconds;
            }
            while (expected > 0f)
            {
                if (Random.Float() < expected)
                {
                    // Heavy tailed, as the size of anything breaking is: a few loud ones and a great many faint.
                    float power = Random.Float();
                    float gain = _crackleGain * 0.075f / MathF.Pow(MathF.Max(power, 0.0009f), 0.7f);
                    _cloud.Trigger(_crackles[Random.Int(_crackles.Length)], MathF.Min(gain, 1.1f), Random.Range(-0.6f, 0.6f));
                }
                expected -= 1f;
            }

            _untilBurst -= seconds;
            if (_untilBurst <= 0f)
            {
                _untilBurst = Random.Exponential(1.6f);
                if (Random.Chance(0.3f)) _cloud.Trigger(_pops[Random.Int(_pops.Length)], Random.Range(0.3f, 0.85f) * _crackleGain, Random.Range(-0.5f, 0.5f));
                _burst = Random.Range(0.03f, 0.14f);
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>
    /// The sea, as the waves it's made of. Each wave builds as a low swell, breaks in a wide crash, and then the foam, a vast number of tiny bubbles,
    /// fizzes up the beach long after, which is why the top of the spectrum arrives last and leaves last in a recording. A few run at once along
    /// the shore, at different places, so that it is never one thing happening in the middle.
    /// </summary>
    public sealed class OceanSound : LiveSound
    {
        public const int Near = 0;      // 0..1 from far inland to standing at the water's edge.
        public const int Swell = 1;     // 0..1 how big the waves are.
        public const int Side = 2;      // -1 the water's to the left, 1 to the right, 0 ahead or all around.
        public const int Arrivals = 3;  // Counts up as each wave reaches the shore on screen, so that what's heard breaks when what's seen does. Left at nothing, the sound keeps its own time.

        private const int Waves = 3;

        private sealed class Wave
        {
            public float Age, Length, Pan, Size, Break;
            public Svf Crash, Foam;
            public OnePole Roar;
            public float RoarLevel, CrashLevel, FoamLevel, Colour;
            public bool Alive;
        }

        private readonly Wave[] _waves = new Wave[Waves];
        private PinkNoise _pinkLeft, _pinkRight;
        private BrownNoise _brownLeft, _brownRight;
        private Svf _bedLeft, _bedRight;
        private OnePole _distanceLeft, _distanceRight;
        private Wander _wander;
        private float _bed, _untilWave, _lastWave;
        private bool _begun;
        private float _near;

        public OceanSound(ulong seed) : base(4, seed, 2f)
        {
            for (int i = 0; i < Waves; i++) _waves[i] = new Wave();
            _wander.Set(6f, ControlRate);
            _bedLeft.Set(1200f, 0.4f);
            _bedRight.Set(1500f, 0.4f);
        }

        private void Begin(Wave wave)
        {
            float swell = Value(Swell);
            wave.Alive = true;
            wave.Age = 0f;
            wave.Size = Random.Range(0.5f, 1f) * (0.45f + 0.55f * swell);
            wave.Length = Random.Range(7f, 12f) * (0.8f + 0.5f * swell);
            wave.Break = wave.Length * Random.Range(0.22f, 0.32f);
            wave.Pan = Random.Range(-0.75f, 0.75f);
            wave.Roar.SetCutoff(Random.Range(140f, 240f));
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            _near = Value(Near);
            float wander = 1f + _wander.Next(Random, Step) * 0.25f;
            _bed = 0.075f * (0.4f + 0.6f * _near) * wander;

            // Air and distance take the top: a beach half a mile off is a slow rumble, and only that.
            float cutoff = 500f * MathF.Pow(36f, _near);
            _distanceLeft.SetCutoff(cutoff);
            _distanceRight.SetCutoff(cutoff);

            // Whoever arrives at a beach finds waves already breaking on it.
            if (!_begun)
            {
                _begun = true;
                Begin(_waves[0]);
                _waves[0].Age = _waves[0].Break + 0.1f;
                Begin(_waves[1]);
                _untilWave = Random.Range(2f, 5f);
            }

            float told = Target(Arrivals);
            _untilWave -= Step;
            if (told != _lastWave || told == 0f && _untilWave <= 0f)
            {
                _lastWave = told;
                _untilWave = Random.Range(3.5f, 8f);
                foreach (Wave wave in _waves)
                {
                    if (wave.Alive) continue;
                    Begin(wave);
                    break;
                }
            }

            foreach (Wave wave in _waves)
            {
                if (!wave.Alive) continue;
                wave.Age += Step;
                if (wave.Age >= wave.Length)
                {
                    wave.Alive = false;
                    wave.RoarLevel = wave.CrashLevel = wave.FoamLevel = 0f;
                    continue;
                }

                float t = wave.Age;
                float after = t - wave.Break;

                // Swelling towards the break, a low push of water.
                float rise = Curve01(t / wave.Break);
                wave.RoarLevel = wave.Size * 0.2f * (after < 0f ? rise * rise : MathF.Exp(-after / (wave.Length * 0.22f)));

                // The crash: quick up and slow away, starting wide and low and opening out.
                wave.CrashLevel = after < 0f ? 0f : wave.Size * MathF.Min(after / 0.45f, 1f) * MathF.Exp(-after / (wave.Length * 0.16f)) * 0.8f;
                float brightness = after < 0f ? 0f : MathF.Min(after / 1.4f, 1f);
                wave.Crash.Set(450f + 2200f * brightness * wave.Size, 0.55f);

                // Foam, rising as the crash goes and lasting longest.
                float foamAge = after - 0.6f;
                wave.FoamLevel = foamAge < 0f ? 0f : wave.Size * MathF.Min(foamAge / 1.1f, 1f) * MathF.Exp(-foamAge / (wave.Length * 0.27f)) * 0.42f;
                wave.Foam.Set(5200f, 0.5f);
            }
        }

        private static float Curve01(float x)
        {
            x = Math.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            float near = _near;
            float top = near * near;

            // A coast is a long thing, so however far to one side it is, some of it is in the other ear.
            float side = Value(Side) * 0.6f;
            float toLeft = 1f - side;
            float toRight = 1f + side;

            for (int i = 0; i < left.Length; i++)
            {
                float whiteLeft = Random.Bipolar();
                float whiteRight = Random.Bipolar();
                float pinkLeft = _pinkLeft.Next(whiteLeft);
                float pinkRight = _pinkRight.Next(whiteRight);
                float brownLeft = _brownLeft.Next(whiteLeft);
                float brownRight = _brownRight.Next(whiteRight);

                float l = _bedLeft.Lowpass(pinkLeft + brownLeft * 0.25f) * _bed;
                float r = _bedRight.Lowpass(pinkRight + brownRight * 0.25f) * _bed;

                foreach (Wave wave in _waves)
                {
                    if (!wave.Alive) continue;
                    float shared = (pinkLeft + pinkRight) * 0.5f;
                    float roar = wave.Roar.Lowpass((brownLeft + brownRight) * 0.5f) * wave.RoarLevel;
                    float crash = wave.Crash.Bandpass(shared) * wave.CrashLevel * 0.85f;
                    float foam = wave.Foam.BandpassNormal(whiteLeft + whiteRight) * wave.FoamLevel * 0.42f * top;

                    // Each wave breaks at its own place along the shore; a loud sound is not a mono one, though, so it stays wide.
                    float sum = roar + crash + foam;
                    float balance = wave.Pan * 0.5f;
                    l += sum * (1f - balance);
                    r += sum * (1f + balance);
                }

                left[i] = _distanceLeft.Lowpass(l) * toLeft;
                right[i] = _distanceRight.Lowpass(r) * toRight;
            }
        }
    }

    /// <summary>
    /// Small water at the edge of a pond, a lake or a calm bay: no surf, only wavelets the wind has raised reaching the bank. Each one is a soft slap and a gulp of air caught under the lip of
    /// it, a run of low bubbles and a trickle draining back, and they come quicker and harder as the wind gets up. Every so often something breaks the surface out on the water.
    /// </summary>
    public sealed class LappingSound : LiveSound
    {
        public const int Near = 0;      // 0..1 from out of earshot to standing at the edge.
        public const int Chop = 1;      // 0..1 how much the wind has raised.
        public const int Side = 2;      // -1 the water's to the left, 1 to the right.

        private readonly float[][] _laps;
        private readonly float[][] _plops;
        private readonly GrainCloud _cloud = new(12);
        private PinkNoise _pinkLeft, _pinkRight;
        private Svf _bedLeft, _bedRight;
        private Wander _bedDrift;
        private float _untilLap = 0.4f, _untilPlop = 8f, _bed, _near;

        public LappingSound(ulong seed) : base(3, seed, 1f)
        {
            _laps = GrainCloud.Make(seed, 14, Lap);
            _plops = GrainCloud.Make(seed + 1, 5, Plop);
            _bedDrift.Set(3f, ControlRate);
            _bedLeft.Set(620f, 0.5f);
            _bedRight.Set(760f, 0.5f);
        }

        private static void Lap(Take take)
        {
            SoundRandom random = take.Random;
            float length = random.Range(0.28f, 0.5f);

            take.Mix(take.Pink(length).Bandpass(Curve.Glide(random.Range(700f, 1100f), 240f, length * 0.7f), 0.8f).Shape(Curve.Hump(length * 0.18f, length)).Normalize(0f), 0f, -3f);

            Curve energy = Curve.Settle(1f, 0f, length * 0.4f);
            take.Mix(Liquid.Bubbles(take, length * 1.4f, energy * 240f, energy, 0.0012f, 0.0075f, 2.1f, 0.2f).Normalize(0f), 0.03f, -4f);

            float drain = length * 1.6f;
            take.Mix(take.Pink(drain).Bandpass(2600f, 0.6f).Shape(Curve.Hump(drain * 0.4f, drain)).Normalize(0f), length * 0.5f, -24f);
            Curve trickle = Curve.Hump(drain * 0.4f, drain);
            take.Mix(Liquid.Bubbles(take, drain, trickle * 45f, trickle, 0.0005f, 0.002f, 2.4f, 0.25f).Normalize(0f), length * 0.5f, -13f);
        }

        private static void Plop(Take take)
        {
            SoundRandom random = take.Random;
            Signal plop = take.Track(0.4f);
            Liquid.Bubble(plop, 0.004f, random.LogRange(0.006f, 0.011f), 1f, 0.45f);
            take.Mix(plop);
            Curve energy = Curve.Settle(1f, 0f, 0.07f);
            take.Mix(Liquid.Bubbles(take, 0.3f, energy * 200f, energy, 0.0008f, 0.003f, 2.3f, 0.2f).Normalize(0f), 0.03f, -11f);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            _near = Value(Near);
            float chop = Value(Chop);
            float reach = _near * _near;
            _bed = 0.032f * reach * (0.5f + chop) * (1f + _bedDrift.Next(Random, Step) * 0.3f);

            _untilLap -= Step;
            if (_untilLap <= 0f)
            {
                _untilLap = Random.Range(0.6f, 2.2f) / (0.7f + chop);
                float side = Value(Side) * 0.5f + Random.Range(-0.5f, 0.5f);
                _cloud.Trigger(_laps[Random.Int(_laps.Length)], reach * Random.Range(0.16f, 0.32f) * (0.55f + chop * 0.9f), side);
            }

            _untilPlop -= Step;
            if (_untilPlop <= 0f)
            {
                _untilPlop = Random.Range(9f, 34f);
                _cloud.Trigger(_plops[Random.Int(_plops.Length)], reach * Random.Range(0.03f, 0.1f), Value(Side) * 0.5f + Random.Range(-0.4f, 0.4f));
            }
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            float bed = _bed;
            for (int i = 0; i < left.Length; i++)
            {
                left[i] = _bedLeft.BandpassNormal(_pinkLeft.Next(Random.Bipolar())) * bed;
                right[i] = _bedRight.BandpassNormal(_pinkRight.Next(Random.Bipolar())) * bed;
            }
            _cloud.Process(left, right);
        }
    }

    /// <summary>
    /// Running water, as van den Doel has it: a stream of bubbles of all sizes, each ringing at its Minnaert frequency, over a soft wash
    /// of the ones too many to tell apart. Measured streams hold steady over time, unlike the sea, with sharp little blips high up.
    /// </summary>
    public sealed class StreamSound : LiveSound
    {
        public const int Flow = 0;      // 0..1 a trickle to a torrent.

        private readonly float[][] _bubbles;
        private readonly GrainCloud _cloud = new(192);
        private PinkNoise _pinkLeft, _pinkRight;
        private Svf _washLeft, _washRight;
        private Wander _surge;
        private float _rate, _wash, _bubbleGain;

        public StreamSound(ulong seed) : base(1, seed, 1f)
        {
            _bubbles = GrainCloud.Make(seed, 72, OneBubble);
            _surge.Set(2f, ControlRate);
            _washLeft.Set(3600f, 0.28f);
            _washRight.Set(4200f, 0.28f);
        }

        private static void OneBubble(Take take)
        {
            SoundRandom random = take.Random;
            Signal bubble = take.Track(0.06f);
            Liquid.Bubble(bubble, 0.0005f, random.LogRange(0.00022f, 0.016f), 1f, random.Range(0.05f, 0.35f));
            take.Mix(bubble);
        }

        protected override void Control()
        {
            const float Step = 1f / ControlRate;
            float flow = Value(Flow);
            float surge = 1f + _surge.Next(Random, Step) * 0.22f;
            _wash = 0.1f * (0.3f + 0.7f * flow) * surge;
            _rate = (200f + 1800f * flow) * surge;
            _bubbleGain = 0.012f + 0.012f * flow;
        }

        protected override void Process(Span<float> left, Span<float> right)
        {
            for (int i = 0; i < left.Length; i++)
            {
                left[i] = _washLeft.BandpassNormal(_pinkLeft.Next(Random.Bipolar())) * _wash;
                right[i] = _washRight.BandpassNormal(_pinkRight.Next(Random.Bipolar())) * _wash;
            }

            float expected = _rate * (left.Length / (float)AudioFormat.SampleRate);
            while (expected > 0f)
            {
                if (Random.Float() < expected)
                {
                    float size = Random.Float();
                    _cloud.Trigger(_bubbles[Random.Int(_bubbles.Length)], _bubbleGain * (0.3f + size * size * size * size * 5f), Random.Bipolar());
                }
                expected -= 1f;
            }
            _cloud.Process(left, right);
        }
    }
}
