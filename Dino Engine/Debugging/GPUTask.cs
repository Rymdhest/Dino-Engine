using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Debug
{
    public class GPUTask : TaskTracker
    {
        private const int QueryDelay = 4; // Delay by 4 frames
        private const int BufferSize = 8; // Ring buffer size



        private int[] queries = new int[BufferSize];
        private int currentFrameIndex = 0;

        private long lastResolvedGpuTime = 0;

        public GPUTask(string name) : base(name)
        {
            for (int i = 0; i < BufferSize; i++)
                GL.GenQueries(1, out queries[i]);
        }

        public void Cleanup()
        {
            foreach (var q in queries)
                GL.DeleteQuery(q);
        }

        public override void StartTask()
        {
            GL.BeginQuery(QueryTarget.TimeElapsed, queries[currentFrameIndex]);
        }

        protected override long CalcTimeTimeFinishTask()
        {
            GL.EndQuery(QueryTarget.TimeElapsed);
            int readIndex = (currentFrameIndex + BufferSize - QueryDelay) % BufferSize;

            GL.GetQueryObject(queries[readIndex], GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available != 0)
            {
                GL.GetQueryObject(queries[readIndex], GetQueryObjectParam.QueryResult, out long timeElapsed);
                lastResolvedGpuTime = timeElapsed;
            }
            currentFrameIndex = (currentFrameIndex + 1) % BufferSize;

            return lastResolvedGpuTime;
        }
    }
}
