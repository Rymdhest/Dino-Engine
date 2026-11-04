using OpenTK.Windowing.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Core
{
    internal class DeltaFrameTimeTracker
    {
        private Stopwatch _frameStopWatch = new Stopwatch();
        private Stopwatch _secondStopWatch = new Stopwatch();
        private float _delta = 0f;
        private int _framesLastSecond = 0;
        private int _framesCurrentSecond = 0;
        private float _totalTime = 0f;
        public float Delta { get => _delta; }
        public float TotalTime { get => _totalTime; }
        public int FramesLastSecond { get => _framesLastSecond; }

        /// <summary>When set, every frame lasts exactly this long, however long it really took.</summary>
        public float? FixedStep { get; set; }

        /// <summary>Winds the clock forward, as if the engine had been running for that much longer.</summary>
        public void Skip(float seconds) => _totalTime += seconds;

        public DeltaFrameTimeTracker()
        {
            _secondStopWatch.Start();
            _frameStopWatch.Start();
        }


        public void update()
        {
            _delta = FixedStep ?? (float)_frameStopWatch.Elapsed.TotalSeconds;
            _frameStopWatch.Restart();

            if (_secondStopWatch.Elapsed.TotalMilliseconds >= 1000.0)
            {
                Engine.PerformanceMonitor.FinishSecond();
                _framesLastSecond = _framesCurrentSecond;
                _framesCurrentSecond = 0;
               _secondStopWatch.Restart();

                Console.WriteLine("FPS; "+FramesLastSecond);

            }
            _framesCurrentSecond++;
            _totalTime += _delta;
        }
    }
}
