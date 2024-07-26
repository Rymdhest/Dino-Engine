using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Debug
{
    public class PerformanceMonitor
    {
        private Dictionary<string, TaskTracker> _taskTrackers = new Dictionary<string, TaskTracker>();

        public PerformanceMonitor()
        {

        }

        public TaskTracker startTask(string name, bool print = false)
        {
            if (print)
            {
                Console.WriteLine($"Starting {name}");
            }

            if (_taskTrackers.TryGetValue(name, out TaskTracker tracker))
            {
                tracker.restart();
                return tracker;
            } else
            {
                TaskTracker newTracker = new TaskTracker(name);
                _taskTrackers.Add(name, newTracker);
                return newTracker;
            }
        }

        public void finishTask(TaskTracker tracker, bool print = false)
        {
            finishTask(tracker.Name, print);
        }

        public void finishTask(string name, bool print = false)
        {
            if (_taskTrackers.TryGetValue(name, out TaskTracker tracker))
            {
                tracker.finish(print);
            }
        }

        public void StatusReportDump()
        {
            List<TaskTracker> tasks = _taskTrackers.Values.ToList();

            tasks.Sort((a, b) => b.lastTime.CompareTo(a.lastTime));

            Console.WriteLine("\nPERFORMANCE REPORT: \n");

            foreach (TaskTracker task in tasks)
            {
                double milliseconds = task.lastTime;
                Console.WriteLine($" {milliseconds} MS : {task.Name}");
            }
        }

        public void clear()
        {
            _taskTrackers.Clear();
        }
    }
}
