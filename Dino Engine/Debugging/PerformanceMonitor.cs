

namespace Dino_Engine.Debug
{
    public class PerformanceMonitor
    {
        private Dictionary<string, TaskTracker> _taskTrackers = new Dictionary<string, TaskTracker>();

        public PerformanceMonitor()
        {

        }

        public void startTask(string name)
        {


            if (_taskTrackers.TryGetValue(name, out TaskTracker tracker))
            {
                tracker.Start();
            } else
            {
                TaskTracker newTracker = new TaskTracker(name);
                _taskTrackers.Add(name, newTracker);
                newTracker.Start();
            }
        }

        public void finishTask(string name)
        {
            if (_taskTrackers.TryGetValue(name, out TaskTracker tracker))
            {
                tracker.Finish();
            } else
            {
                throw new Exception("Trying to finish a performance task that does not excist");
            }
        }

        public void FinishFrame()
        {
            foreach (var kvp in _taskTrackers)
            {
                kvp.Value.FinishFrame();
            }
        }

        public void FinishSecond()
        {
            foreach (var kvp in _taskTrackers)
            {
                kvp.Value.FinishSecond();
            }
            StatusReportDump();
        }

        public void StatusReportDump()
        {
            List<TaskTracker> tasks = _taskTrackers.Values.ToList();

            tasks.Sort((a, b) => b.averageTimePerTaskLastSecond.CompareTo(a.averageTimePerTaskLastSecond));

            Console.WriteLine("\nPERFORMANCE REPORT (GPU): \n");

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
            _taskTrackers.Clear();
        }
    }
}
