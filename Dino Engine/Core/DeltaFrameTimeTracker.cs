﻿using OpenTK.Windowing.Desktop;
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
        public float Delta { get => _delta; }
        public int FramesLastSecond { get => _framesLastSecond; }

        public DeltaFrameTimeTracker()
        {
            _secondStopWatch.Start();
            _frameStopWatch.Start();
        }


        public void update()
        {
            _delta = (float)_frameStopWatch.Elapsed.TotalSeconds;
            _frameStopWatch.Restart();

            if (_secondStopWatch.Elapsed.TotalMilliseconds >= 1000.0)
            {
                _framesLastSecond = _framesCurrentSecond;
                _framesCurrentSecond = 0;
               _secondStopWatch.Restart();

                //Console.WriteLine(FramesLastSecond);

            }
            _framesCurrentSecond++;
        }
    }
}
