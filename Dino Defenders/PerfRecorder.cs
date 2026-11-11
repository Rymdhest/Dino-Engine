using System.Globalization;
using System.Text;
using Dino_Engine.Core;
using Dino_Engine.Debug;
using OpenTK.Graphics.OpenGL4;

namespace Dino_Defenders
{
    /// <summary>
    /// Measures a stretch of a run and writes what it found to a file, so that two builds, or two machines, can be held up side by side:
    /// how long frames really took, how long each pass took on the GPU and each task on the CPU, and what was counted.
    /// </summary>
    internal sealed class PerfRecorder
    {
        private const float SlowFrameMilliseconds = 18f;
        private const int MostSlowFrames = 40;

        private readonly string _path;
        private readonly int _startFrame;
        private readonly int _frames;
        private readonly List<float> _frameTimes = new();
        private readonly Dictionary<string, long> _counts = new();
        private readonly List<string> _slowFrames = new();
        private readonly List<string> _collections = new();
        private int _collections0;
        private int _collections1;
        private int _collections2;
        private int _collectionsSeen;
        private double _pausedBefore;
        private bool _running;
        private int _evictionsBefore;
        private int _evictedBefore;

        public bool Finished { get; private set; }

        public PerfRecorder(string path, int startFrame, int frames)
        {
            _path = path;
            _startFrame = startFrame;
            _frames = frames;
        }

        /// <summary>Call once a frame, with the frame's number and how long it took.</summary>
        public void Frame(int frame, float delta)
        {
            if (Finished || frame < _startFrame) return;
            if (!_running)
            {
                _running = true;
                Engine.PerformanceMonitor.ResetTotals();
                (_collections0, _collections1, _collections2) = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
                _collectionsSeen = _collections0;
                _pausedBefore = GC.GetTotalPauseDuration().TotalMilliseconds;
                (_evictionsBefore, _evictedBefore) = (VideoMemory(0x904A), VideoMemory(0x904B));
                return;
            }

            _frameTimes.Add(delta * 1000f);
            double paused = GC.GetTotalPauseDuration().TotalMilliseconds;
            if (delta * 1000f > SlowFrameMilliseconds && _slowFrames.Count < MostSlowFrames) _slowFrames.Add(DescribeSlowFrame(delta, paused - _pausedBefore));
            _pausedBefore = paused;
            if (GC.CollectionCount(0) != _collectionsSeen)
            {
                _collectionsSeen = GC.CollectionCount(0);
                _collections.Add(DescribeCollection());
            }
            foreach (var (name, amount) in Engine.PerformanceMonitor.LastFrameCounts)
            {
                _counts.TryGetValue(name, out long sofar);
                _counts[name] = sofar + amount;
            }

            if (_frameTimes.Count < _frames) return;
            Finished = true;
            File.WriteAllText(_path, Write());
        }

        // GL_NVX_gpu_memory_info, in kilobytes or a count, or 0 on a card without it.
        private static int VideoMemory(int name)
        {
            int value = GL.GetInteger((GetPName)name);
            return GL.GetError() == ErrorCode.NoError ? value : 0;
        }

        // What took the time in a frame that was slow, as the averages hide what stutters.
        private string DescribeSlowFrame(float delta, double collecting)
        {
            IEnumerable<string> tasks = Engine.PerformanceMonitor.CPUTaskTrackers
                .OrderByDescending(t => t.LastFrameTime)
                .Take(4)
                .Select(t => FormattableString.Invariant($"{t.Name} {t.LastFrameTime / 1_000_000.0:0.0}"));
            string collections = FormattableString.Invariant($"collecting {collecting:0.0}, ");
            return FormattableString.Invariant($"{delta * 1000f,8:0.0}: frame {_frameTimes.Count}, ") + collections + string.Join(", ", tasks);
        }

        // What a pause was spent on, which says more than how many there were.
        private string DescribeCollection()
        {
            GCMemoryInfo last = GC.GetGCMemoryInfo(GCKind.Any);
            const double megabyte = 1024.0 * 1024.0;
            string kind = last.Concurrent ? "in the background" : last.Compacted ? "compacting" : "sweeping";
            return FormattableString.Invariant($"    frame {_frameTimes.Count}: gen {last.Generation} {kind}, paused {last.PauseDurations[0].TotalMilliseconds:0.0} ms, promoted {last.PromotedBytes / megabyte:0.0} MB, heap {last.HeapSizeBytes / megabyte:0} MB, {last.PinnedObjectsCount} pinned");
        }

        private string Write()
        {
            var text = new StringBuilder();
            CultureInfo culture = CultureInfo.InvariantCulture;
            List<float> sorted = _frameTimes.OrderBy(t => t).ToList();
            float At(float share) => sorted[Math.Min((int)(sorted.Count * share), sorted.Count - 1)];

            float average = _frameTimes.Average();
            text.AppendLine(culture, $"{Engine.Resolution.X} x {Engine.Resolution.Y}, {_frameTimes.Count} frames");
            text.AppendLine(culture, $"FPS {1000f / average:0.0}");
            text.AppendLine(culture, $"Frame ms: average {average:0.00}, median {At(0.5f):0.00}, 95% {At(0.95f):0.00}, 99% {At(0.99f):0.00}, worst {sorted[^1]:0.00}");

            // NVIDIA alone says how much video memory is left and how much the driver had to move out to system memory, where every pass that reads it crawls.
            int videoMemory = VideoMemory(0x9048);
            if (videoMemory > 0)
                text.AppendLine(culture, $"Video memory: {videoMemory / 1024} MB, {VideoMemory(0x9049) / 1024} MB free, {VideoMemory(0x904A) - _evictionsBefore} evictions ({(VideoMemory(0x904B) - _evictedBefore) / 1024} MB) while measuring, {VideoMemory(0x904B) / 1024} MB evicted in all");

            Section(text, "GPU", Engine.PerformanceMonitor.GPUTaskTrackers);
            Section(text, "CPU", Engine.PerformanceMonitor.CPUTaskTrackers);

            text.AppendLine();
            text.AppendLine(culture, $"Collections: {GC.CollectionCount(0) - _collections0} gen 0, {GC.CollectionCount(1) - _collections1} gen 1, {GC.CollectionCount(2) - _collections2} gen 2");
            foreach (string collection in _collections) text.AppendLine(collection);
            text.AppendLine(culture, $"{_frameTimes.Count(t => t > SlowFrameMilliseconds)} frames over {SlowFrameMilliseconds} ms:");
            foreach (string frame in _slowFrames) text.AppendLine(frame);

            text.AppendLine();
            text.AppendLine("Per frame:");
            foreach (var (name, total) in _counts.OrderBy(c => c.Key, StringComparer.Ordinal))
                text.AppendLine(culture, $"{total / (double)_frameTimes.Count,14:#,0} {name}");
            return text.ToString();
        }

        private static void Section(StringBuilder text, string title, IEnumerable<TaskTracker> tasks)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            var rows = tasks
                .Where(t => t.TotalFrames > 0)
                .Select(t => (t.Name, Milliseconds: t.TotalTime / (double)t.TotalFrames / 1_000_000.0))
                .OrderByDescending(t => t.Milliseconds)
                .ToList();

            text.AppendLine();
            text.AppendLine(culture, $"{title} total {rows.Sum(r => r.Milliseconds):0.000} ms");
            foreach (var (name, milliseconds) in rows)
            {
                if (milliseconds >= 0.005) text.AppendLine(culture, $"{milliseconds,8:0.000} {name}");
            }
        }
    }
}
