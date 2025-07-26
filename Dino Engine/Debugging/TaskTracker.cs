using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Debug
{
    public class TaskTracker
    {
        private const int QueryDelay = 4; // Delay by 4 frames
        private const int BufferSize = 8; // Ring buffer size

        private string _name;
        public string Name => _name;

        private int[] queries = new int[BufferSize];
        private int currentFrameIndex = 0;

        private long lastResolvedGpuTime = 0;
        private long timeSpentThisSecond = 0;
        private int framesThisSecond = 0;

        public long averageTimePerTaskLastSecond = 0;

        public TaskTracker(string name)
        {
            _name = name;
            for (int i = 0; i < BufferSize; i++)
                GL.GenQueries(1, out queries[i]);
        }

        public void Start()
        {
            GL.BeginQuery(QueryTarget.TimeElapsed, queries[currentFrameIndex]);
        }

        public void Finish()
        {
            GL.EndQuery(QueryTarget.TimeElapsed);
        }

        public void FinishFrame()
        {
            int readIndex = (currentFrameIndex + BufferSize - QueryDelay) % BufferSize;

            GL.GetQueryObject(queries[readIndex], GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available != 0)
            {
                GL.GetQueryObject(queries[readIndex], GetQueryObjectParam.QueryResult, out long timeElapsed);
                lastResolvedGpuTime = timeElapsed;
            }

            timeSpentThisSecond += lastResolvedGpuTime;
            framesThisSecond++;

            currentFrameIndex = (currentFrameIndex + 1) % BufferSize;
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

        public void Cleanup()
        {
            foreach (var q in queries)
                GL.DeleteQuery(q);
        }
    }
}
