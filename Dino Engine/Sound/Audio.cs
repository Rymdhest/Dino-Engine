using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Dsp;
using Dino_Engine.Sound.Mixing;
using Dino_Engine.Sound.Output;
using Dino_Engine.Sound.Synthesis;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Dino SoundLab")]

namespace Dino_Engine.Sound
{
    /// <summary>A continuous sound that's been started. Keep it to move it, change it and stop it. A default one does nothing, safely.</summary>
    public readonly struct SoundHandle
    {
        internal readonly int Id;

        internal SoundHandle(int id)
        {
            Id = id;
        }

        public bool IsValid => Id != 0;

        /// <summary>Sets a parameter, which the sound eases towards.</summary>
        public void Set(int parameter, float value)
        {
            if (Id != 0) Audio.Post(new AudioCommand { Kind = CommandKind.SetParameter, Voice = Id, X = parameter, Y = value });
        }

        public void Move(Vector3 position, Vector3 velocity = default)
        {
            if (Id != 0) Audio.Post(new AudioCommand { Kind = CommandKind.Move, Voice = Id, A = position, B = velocity });
        }

        /// <summary>Sets how loud it is, 1 being as made.</summary>
        public void SetGain(float gain)
        {
            if (Id != 0) Audio.Post(new AudioCommand { Kind = CommandKind.SetGain, Voice = Id, X = gain });
        }

        public void Stop(float fadeSeconds = 0.3f)
        {
            if (Id != 0) Audio.Post(new AudioCommand { Kind = CommandKind.Stop, Voice = Id, X = fadeSeconds });
        }
    }

    /// <summary>
    /// The engine's sound, from the game's side: say what happens and where, and the rest is taken care of. Everything here is safe to
    /// call from anywhere, costs next to nothing and never waits. Without <see cref="Start"/>, as on a server with no window,
    /// nothing happens and nothing goes wrong, so game code needn't ask.
    /// <code>
    /// Audio.Play(Impacts.Flesh, position);                 // Somewhere in the world.
    /// Audio.Play(Interface.Click);                         // In the player's head.
    /// SoundHandle fire = Audio.Start(Soundscape.Fire, at); // Going on until it's stopped.
    /// </code>
    /// See SOUND.md for how to add sounds.
    /// </summary>
    public static class Audio
    {
        private static Mixer? _mixer;
        private static AudioOutput? _output;
        private static SoundFactory? _factory;
        private static Recorder? _recorder;
        private static readonly Dictionary<SoundDefinition, SoundPool> Pools = new();
        private static float _masterVolume = 1f;
        private static readonly float[] BusVolumes = Enumerable.Repeat(1f, Enum.GetValues<SoundBus>().Length).ToArray();
        private static (int id, LiveSound? sound) _lastLive;

        public static Mixer? Mixer => _mixer;
        public static AudioOutput? Output => _output;

        /// <summary>Whether every sound has a take ready. A sound played before this may not be heard.</summary>
        public static bool Ready => _factory?.Ready ?? false;

        /// <summary>How many takes have been made so far this session.</summary>
        public static int TakesMade => _factory?.TakesMade ?? 0;

        /// <summary>Starts sound. <paramref name="device"/> false mixes with nothing to play on, for a machine run with no one listening.</summary>
        public static void Start(bool device = true)
        {
            if (_mixer != null) return;

            var mixer = new Mixer();
            foreach (SoundDefinition definition in SoundCatalog.All) Pools[definition] = new SoundPool(definition);

            _factory = new SoundFactory(Pools.Values);
            _mixer = mixer;
            _output = new AudioOutput(mixer.Render, silent: !device);
        }

        public static void Stop()
        {
            StopRecording();
            _output?.Dispose();
            _factory?.Dispose();
            _output = null;
            _factory = null;
            _mixer = null;
            Pools.Clear();
        }

        /// <summary>Everything's overall level, from 0 to 1.</summary>
        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = value;
                if (_mixer != null) _mixer.MasterVolume = value;
            }
        }

        public static void SetVolume(SoundBus bus, float volume)
        {
            BusVolumes[(int)bus] = volume;
            Post(new AudioCommand { Kind = CommandKind.BusVolume, Voice = (int)bus, X = volume });
        }

        public static float GetVolume(SoundBus bus) => BusVolumes[(int)bus];

        /// <summary>
        /// Where sound is heard from. For a camera far above its hero the ears belong near the hero, or everything is equally far off, but
        /// left and right belong to the camera, as that's what left and right mean on a screen.
        /// </summary>
        public static void SetListener(Vector3 position, Vector3 right, Vector3 forward, Vector3 up, Vector3 velocity = default)
        {
            Post(new AudioCommand { Kind = CommandKind.Listener, A = position, B = right, C = forward, D = up, E = velocity });
        }

        /// <summary>Sets the space everything is heard in. It's reached gradually, so a border between two places can't be heard.</summary>
        public static void SetEnvironment(in ReverbSettings settings)
        {
            Post(new AudioCommand { Kind = CommandKind.Environment, Reverb = settings });
        }

        /// <summary>Puts the world behind glass, from 0 not at all to 1: for menus and for being stunned.</summary>
        public static void Muffle(float amount)
        {
            Post(new AudioCommand { Kind = CommandKind.Muffle, X = amount });
        }

        /// <summary>Plays a sound in the player's head, at no place.</summary>
        public static void Play(SoundDefinition definition, float decibels = 0f, float pitch = 1f)
        {
            Trigger(definition, default, default, false, decibels, pitch);
        }

        /// <summary>Plays a sound at a place. <paramref name="velocity"/> matters to a definition with Doppler.</summary>
        public static void Play(SoundDefinition definition, Vector3 position, float decibels = 0f, float pitch = 1f, Vector3 velocity = default)
        {
            Trigger(definition, position, velocity, true, decibels, pitch);
        }

        private static void Trigger(SoundDefinition definition, Vector3 position, Vector3 velocity, bool positional, float decibels, float pitch)
        {
            Mixer? mixer = _mixer;
            if (mixer == null || !Pools.TryGetValue(definition, out SoundPool? pool)) return;
            mixer.Post(new AudioCommand
            {
                Kind = CommandKind.Play,
                Voice = mixer.Reserve(),
                Payload = pool,
                A = position,
                B = velocity,
                X = AudioFormat.DecibelsToGain(decibels),
                Y = pitch,
                Flag = positional,
            });
        }

        /// <summary>Starts a sound with no end, somewhere or everywhere.</summary>
        public static SoundHandle Start(LiveDefinition definition, Vector3? position = null, float gain = 1f, ulong seed = 0)
        {
            Mixer? mixer = _mixer;
            if (mixer == null) return default;

            // Made here, not on the audio thread, as making things allocates.
            LiveSound source = definition.Create(seed != 0 ? seed : (ulong)Environment.TickCount64 * 2654435761UL + 1);
            int id = mixer.Reserve();
            _lastLive = (id, source);
            mixer.Post(new AudioCommand
            {
                Kind = CommandKind.Start,
                Voice = id,
                Payload = source,
                Extra = definition,
                A = position ?? default,
                X = gain,
                Flag = position != null,
            });
            return new SoundHandle(id);
        }

        /// <summary>The sound just started by <see cref="Start"/>, for one that takes more than parameters.</summary>
        internal static LiveSound? LiveOf(SoundHandle handle) => _lastLive.id == handle.Id ? _lastLive.sound : null;

        /// <summary>Everything but the interface goes, for leaving a world.</summary>
        public static void StopWorld(float fadeSeconds = 0.5f)
        {
            Post(new AudioCommand { Kind = CommandKind.StopAll, X = fadeSeconds });
        }

        internal static void Post(in AudioCommand command) => _mixer?.Post(command);

        /// <summary>Saves everything played from now on to a WAV file, for checking what a scripted run sounded like.</summary>
        public static void Record(string path)
        {
            if (_mixer == null) return;
            _recorder = new Recorder(path);
            _mixer.Tap = _recorder.Write;
        }

        public static void StopRecording()
        {
            if (_mixer != null) _mixer.Tap = null;
            _recorder?.Dispose();
            _recorder = null;
        }
    }
}
