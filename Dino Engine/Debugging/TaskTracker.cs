using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Debug
{
    public class TaskTracker
    {
        private string _name;
        public string Name { get => _name; set => _name = value; }
        private Stopwatch _stopwatch;
        public long lastTime = 0;

        public TaskTracker(string name)
        {
            _name = name;
            _stopwatch = Stopwatch.StartNew();
        }

        public void restart()
        {
            _stopwatch.Restart();
        }

        public void finish(bool print)
        {
            _stopwatch.Stop();
            lastTime = _stopwatch.ElapsedMilliseconds;
            if (print)
            {
                Console.WriteLine($"Finished {Name} in {lastTime} MS");
            }
        }
    }
}
