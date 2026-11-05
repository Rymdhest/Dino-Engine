using Dino_Engine.Sound.Content;
using Dino_Engine.Sound.Synthesis;
using System.Diagnostics;

namespace Dino_SoundLab
{
    /// <summary>
    /// Makes sounds with no window and no game, to look at them, listen to them and check that they hold up.
    ///   render  [--out folder] [--only text] [--takes N] [--seed N]            Writes takes of every sound as WAV files, to be listened to.
    ///   live    [--out folder] [--only text] [--seconds N] [--set a,b,c]       The same for sounds that go on, at the given parameters.
    ///   sheet   [--out file.png] [--only text] [--columns N]                   A spectrogram of every sound, labelled, on one picture.
    ///   check   [--only text]                                                  Everything that can be known without ears. Exit code 1 on any problem.
    ///   size                                                                   What every sound costs in memory to keep ready.
    ///   tunes   [--count N] [--show N] [--mode m] [--meter 4|3|6|12] [--style s]   Writes tunes, prints some, and compares them with real ones.
    ///   notes   [--out folder]                                                 One note of each instrument, to compare with recordings.
    ///   music   [--mood m] [--intensity v] [--sweep true] [--story true] [--seconds N] [--seed N] [--out file.wav]   The band, with a list of every note beside it.
    ///   musiccheck [--count N]                                                 Everything about the music that can be known without ears. Exit code 1 on any problem.
    ///   device  [--seconds N]                                                  Plays quietly on the real device and listens to what reaches it. Exit code 1 if nothing does.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "render";
            var options = new Options(args);
            switch (command)
            {
                case "render": return Render(options);
                case "device": return DeviceCheck.Run(options);
                case "live": return RenderLive(options);
                case "check": return SoundCheck.Run(options);
                case "size": return Size();
                case "tunes": return MusicLab.Tunes(options);
                case "notes": return MusicLab.Notes(options);
                case "music": return MusicLab.Play(options);
                case "musiccheck": return MusicCheck.Run(options);
                case "sheet": return Spectrogram.Sheet(options);
                default:
                    Console.Error.WriteLine($"Unknown command '{command}'.");
                    return 2;
            }
        }

        // Runs each continuous sound for a while, with what a tool would start it at unless told otherwise.
        private static int RenderLive(Options options)
        {
            string folder = options.Text("out") ?? "sounds";
            string? only = options.Text("only");
            float seconds = options.Float("seconds", 20f);
            string? values = options.Text("set");
            ulong seed = (ulong)options.Int("seed", 1);

            foreach (LiveDefinition definition in SoundCatalog.AllLive)
            {
                if (only != null && !definition.Id.Contains(only)) continue;
                LiveSound sound = definition.Create(seed * 7919UL + 3UL);
                float[] parameters = definition.Defaults;
                if (values != null) parameters = values.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                for (int i = 0; i < parameters.Length; i++) sound.SetTarget(i, parameters[i]);

                int frames = (int)(seconds * Dino_Engine.Sound.Dsp.AudioFormat.SampleRate);
                var left = new float[frames];
                var right = new float[frames];
                var clock = Stopwatch.StartNew();
                for (int done = 0; done < frames; done += 240)
                {
                    int count = Math.Min(240, frames - done);
                    sound.Render(left.AsSpan(done, count), right.AsSpan(done, count));
                }
                double spent = clock.Elapsed.TotalSeconds;
                string name = options.Text("name") ?? definition.Id;
                WavFile.Write(Path.Combine(folder, name + ".wav"), left, right);
                float peak = 0f;
                foreach (float v in left) peak = MathF.Max(peak, MathF.Abs(v));
                Console.WriteLine($"{name}: {spent / seconds * 100.0:0.00}% of a core, peak {Dino_Engine.Sound.Dsp.AudioFormat.GainToDecibels(peak):0.0} dBFS, {Dino_Engine.Sound.Dsp.Loudness.Integrated(left, right):0.0} LUFS");
            }
            return 0;
        }

        // What every sound costs to keep ready, biggest first.
        private static int Size()
        {
            var costs = new List<(string id, double megabytes, float seconds)>();
            foreach (SoundDefinition definition in SoundCatalog.All)
            {
                SoundBuffer take = definition.Render(5);
                double bytes = (double)take.Length * 4 * (take.IsStereo ? 2 : 1) * definition.Takes;
                costs.Add((definition.Id, bytes / 1048576.0, take.Seconds));
            }
            foreach (var cost in costs.OrderByDescending(c => c.megabytes).Take(12)) Console.WriteLine($"{cost.megabytes,7:0.00} MB  {cost.seconds,5:0.00} s  {cost.id}");
            Console.WriteLine($"{costs.Sum(c => c.megabytes):0.0} MB with every sound in use, for {costs.Count} sounds");
            return 0;
        }

        private static int Render(Options options)
        {
            string folder = options.Text("out") ?? "sounds";
            string? only = options.Text("only");
            int count = options.Int("takes", 3);
            ulong seed = (ulong)options.Int("seed", 1);
            var clock = Stopwatch.StartNew();
            int made = 0;
            foreach (SoundDefinition definition in SoundCatalog.All)
            {
                if (only != null && !definition.Id.Contains(only)) continue;
                for (int i = 0; i < count; i++)
                {
                    SoundBuffer take = definition.Render(seed * 1000 + (ulong)i);
                    WavFile.Write(Path.Combine(folder, $"{definition.Id}.{i}.wav"), take);
                    made++;
                }
            }
            Console.WriteLine($"{made} takes in {clock.Elapsed.TotalSeconds:0.00} s");
            return 0;
        }
    }

    internal sealed class Options
    {
        private readonly Dictionary<string, string> _values = new();

        public Options(string[] args)
        {
            for (int i = 1; i + 1 < args.Length; i += 2) _values[args[i].TrimStart('-')] = args[i + 1];
        }

        public string? Text(string name) => _values.TryGetValue(name, out string? value) ? value : null;
        public int Int(string name, int fallback) => _values.TryGetValue(name, out string? value) ? int.Parse(value) : fallback;
        public float Float(string name, float fallback) => _values.TryGetValue(name, out string? value) ? float.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
    }
}
