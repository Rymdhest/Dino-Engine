using OpenTK.Graphics.OpenGL;
using System.Diagnostics;

namespace Dino_Engine.Debug
{
    public class CPUTask : TaskTracker
    {

        private Stopwatch stopwatch;

        public CPUTask(string name) : base(name)
        {
            stopwatch = new Stopwatch();
        }

        public void Cleanup()
        {
        }

        public override void StartTask()
        {
            stopwatch.Restart();
        }

        protected override long CalcTimeTimeFinishTask()
        {
            stopwatch.Stop();
            long time = (long)(((double)stopwatch.ElapsedTicks / Stopwatch.Frequency) * 1000000000.0);
            stopwatch.Reset();
            return time;
        }
    }
}
