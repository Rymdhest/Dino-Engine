

namespace Dino_Engine.Debug
{
    public class PerformanceMonitor
    {
        private Dictionary<string, TaskTracker> GPUTasks = new Dictionary<string, TaskTracker>();
        private Dictionary<string, TaskTracker> CPUTasks = new Dictionary<string, TaskTracker>();

        public PerformanceMonitor()
        {

        }

        public IReadOnlyCollection<TaskTracker> GPUTaskTrackers => GPUTasks.Values;
        public IReadOnlyCollection<TaskTracker> CPUTaskTrackers => CPUTasks.Values;

        /// <summary>Whether passes are timed on the GPU. A timer query can stop the GPU overlapping one pass with the next, so it's only on while someone is reading the results.
        /// Changed between frames, as a pass started with it on has to finish with it on.</summary>
        public bool MeasureGPU { get; set; }

        /// <summary>Keeps <see cref="MeasureGPU"/> on with no window open, for a run that writes a report.</summary>
        public bool AlwaysMeasureGPU { get; set; }

        /// <summary>What was counted in the frame before this one: draw calls, triangles and the like.</summary>
        public IReadOnlyDictionary<string, long> LastFrameCounts => _lastCounts;
        private Dictionary<string, long> _lastCounts = new Dictionary<string, long>();
        private Dictionary<string, long> _counts = new Dictionary<string, long>();

        /// <summary>Whether <see cref="Count"/> keeps what it's told, for a run that writes a report.</summary>
        public bool Counting { get; set; }

        /// <summary>Adds to a number that's reported per frame.</summary>
        public void Count(string name, long amount)
        {
            if (!Counting) return;
            _counts.TryGetValue(name, out long sofar);
            _counts[name] = sofar + amount;
        }

        /// <summary>Starts the totals afresh, for a report over a stretch of a run.</summary>
        public void ResetTotals()
        {
            foreach (TaskTracker task in GPUTasks.Values) task.ResetTotals();
            foreach (TaskTracker task in CPUTasks.Values) task.ResetTotals();
        }
        public void startCPUTask(string name)
        {
            if (CPUTasks.TryGetValue(name, out TaskTracker task))
            {
                task.StartTask();
            }
            else
            {
                CPUTask newTracker = new CPUTask(name);
                CPUTasks.Add(name, newTracker);
                newTracker.StartTask();
            }
        }
        public void startGPUTask(string name)
        {
            if (!MeasureGPU) return;
            if (GPUTasks.TryGetValue(name, out TaskTracker task))
            {
                task.StartTask();
            } else
            {
                GPUTask newTracker = new GPUTask(name);
                GPUTasks.Add(name, newTracker);
                newTracker.StartTask();
            }
        }
        public void finishCPUTask(string name)
        {
            if (CPUTasks.TryGetValue(name, out TaskTracker task))
            {
                task.FinishTask();
            }
            else
            {
                throw new Exception("Trying to finish a performance task that does not excist");
            }
        }
        public void finishGPUTask(string name)
        {
            if (!MeasureGPU) return;
            if (GPUTasks.TryGetValue(name, out TaskTracker task))
            {
                task.FinishTask();
            } else
            {
                throw new Exception("Trying to finish a performance task that does not excist");
            }
        }

        public void FinishFrame()
        {
            (_lastCounts, _counts) = (_counts, _lastCounts);
            _counts.Clear();

            foreach (var kvp in GPUTasks)
            {
                kvp.Value.FinishFrame();
            }

            foreach (var kvp in CPUTasks)
            {
                kvp.Value.FinishFrame();
            }
        }

        public void FinishSecond()
        {
            foreach (var kvp in GPUTasks)
            {
                kvp.Value.FinishSecond();
            }
            foreach (var kvp in CPUTasks)
            {
                kvp.Value.FinishSecond();
            }
            if (MeasureGPU) StatusReportDump(GPUTasks.Values.ToList<TaskTracker>());
            //StatusReportDump(CPUTasks.Values.ToList<TaskTracker>());
        }

        public void StatusReportDump(List<TaskTracker> tasks)
        {
            tasks.Sort((a, b) => b.averageTimePerTaskLastSecond.CompareTo(a.averageTimePerTaskLastSecond));

            Console.WriteLine("\nPERFORMANCE REPORT: \n");

            double total = 0;

            foreach (TaskTracker task in tasks)
            {
                double milliseconds = task.averageTimePerTaskLastSecond / 1_000_000.0;
                total += milliseconds;
                Console.WriteLine($" {milliseconds.ToString("0.###")} MS : {task.Name}");
            }
            Console.WriteLine($"TOTA : {total} MS");
        }

        public void cleanUp()
        {
            GPUTasks.Clear();
            CPUTasks.Clear();
        }
    }
}
