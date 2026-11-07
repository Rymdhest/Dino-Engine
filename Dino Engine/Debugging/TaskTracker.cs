using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Debug
{
    public abstract class TaskTracker
    {

        private string _name;
        public string Name => _name;

        private long timeSpentThisSecond = 0;
        protected long timeSpentThisFrame = 0;
        private int framesThisSecond = 0;

        public long averageTimePerTaskLastSecond = 0;
        /// <summary>Time and frames since <see cref="ResetTotals"/>, for a report over a whole run.</summary>
        public long TotalTime { get; private set; }
        public long TotalFrames { get; private set; }
        /// <summary>Time in the last finished frame, for finding out what a slow one was doing.</summary>
        public long LastFrameTime { get; private set; }

        public TaskTracker(string name)
        {
            _name = name;
        }

        public abstract void StartTask();

        protected abstract long CalcTimeTimeFinishTask();
        public void FinishTask()
        {
            timeSpentThisFrame += CalcTimeTimeFinishTask();
        }

        public virtual void FinishFrame()
        {
            timeSpentThisSecond += timeSpentThisFrame;
            LastFrameTime = timeSpentThisFrame;
            TotalTime += timeSpentThisFrame;
            TotalFrames++;
            timeSpentThisFrame = 0;
            framesThisSecond++;
        }

        public void FinishSecond()
        {
            if (framesThisSecond > 0)
                averageTimePerTaskLastSecond = timeSpentThisSecond / framesThisSecond;
            else
                averageTimePerTaskLastSecond = 0;

            timeSpentThisSecond = 0;
            framesThisSecond = 0;
        }

        public void ResetTotals()
        {
            TotalTime = 0;
            TotalFrames = 0;
        }

        public virtual void Cleanup() { }
    }
}
