

namespace Dino_Engine.Debug
{
    public class PerformanceMonitor
    {
        private Dictionary<string, TaskTracker> GPUTasks = new Dictionary<string, TaskTracker>();
        private Dictionary<string, TaskTracker> CPUTasks = new Dictionary<string, TaskTracker>();

        public PerformanceMonitor()
        {

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
            //StatusReportDump(GPUTasks.Values.ToList<TaskTracker>());
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
