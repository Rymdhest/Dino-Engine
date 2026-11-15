using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Debug
{
    /// <summary>
    /// The GPU's own clock at the start and end of every frame, read back a few frames late so that reading never waits. What the passes' timers
    /// add up to leaves out whatever runs between them, and says nothing of the GPU sitting idle while the CPU builds the next frame: this says how
    /// long a frame really kept the GPU, from its first command to its last, and how long the GPU stood idle between one frame and the next.
    /// </summary>
    public static class GpuFrameClock
    {
        private const int Ring = 8;
        private const int Delay = 4;

        private static int[]? _starts, _ends;
        private static int _frame;
        private static long _lastEnd = -1;

        /// <summary>Since <see cref="Reset"/>: frames counted, and the sums of their spans and of the idle time before each, in nanoseconds.</summary>
        public static long Frames, SpanNanoseconds, IdleNanoseconds;

        public static void Reset()
        {
            Frames = SpanNanoseconds = IdleNanoseconds = 0;
            _lastEnd = -1;
        }

        public static void FrameStarts()
        {
            if (_starts == null)
            {
                _starts = new int[Ring];
                _ends = new int[Ring];
                GL.GenQueries(Ring, _starts);
                GL.GenQueries(Ring, _ends);
            }
            GL.QueryCounter(_starts[_frame % Ring], QueryCounterTarget.Timestamp);
        }

        public static void FrameEnds()
        {
            if (_starts == null || _ends == null) return;
            GL.QueryCounter(_ends[_frame % Ring], QueryCounterTarget.Timestamp);
            _frame++;
            if (_frame <= Delay) return;

            int read = (_frame - 1 - Delay) % Ring;
            GL.GetQueryObject(_ends[read], GetQueryObjectParam.QueryResultAvailable, out int available);
            if (available == 0) return;     // Late: skipped rather than waited for, and the next idle time isn't counted.
            GL.GetQueryObject(_starts[read], GetQueryObjectParam.QueryResult, out long start);
            GL.GetQueryObject(_ends[read], GetQueryObjectParam.QueryResult, out long end);
            if (_lastEnd >= 0 && start > _lastEnd) IdleNanoseconds += start - _lastEnd;
            SpanNanoseconds += end - start;
            Frames++;
            _lastEnd = end;
        }
    }
}
