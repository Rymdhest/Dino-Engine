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

        public virtual void Cleanup() { }
    }
}
