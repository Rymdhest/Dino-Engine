namespace Dino_Engine.Networking
{
    /// <summary>
    /// The client's clock for its own hero. It runs ahead of the server, by just enough that a command reaches the server shortly
    /// before the tick it is for. The server reports how much spare time commands are arriving with, and this drifts faster or
    /// slower to keep that small but positive: a few percent either way, which nobody can feel. Latency needn't be measured at
    /// all, and when the line gets worse it opens the margin by itself.
    /// </summary>
    public sealed class CommandClock
    {
        private const int TargetSpare = 1;          // Ticks a command should arrive early by, at worst.
        private const double MostDilation = 0.05;
        private const double Gain = 0.015;          // Dilation per tick of error.
        private const int JumpBeyond = 20;          // Further out than this, drifting would take too long.
        private const int MostCatchUp = 30;
        private const double JumpAfter = 1.5;       // Seconds it has to stay that far out. A stall passes by itself, and chasing one would be worse than it was.

        private double _accumulator;
        private double _farOutFor;

        private bool _farOut;

        /// <summary>The tick the next command is for.</summary>
        public uint Tick { get; private set; }

        /// <summary>1 is the server's speed.</summary>
        public double Rate { get; private set; } = 1.0;

        /// <summary>How far into the next tick this frame falls, for drawing between two ticks.</summary>
        public float Alpha => (float)(_accumulator / NetTime.TickSecondsPrecise);

        public bool Running { get; private set; }

        public void Start(uint tick)
        {
            Tick = tick;
            _accumulator = 0.0;
            Rate = 1.0;
            Running = true;
        }

        public void Stop() => Running = false;

        /// <summary>Lets time pass. Returns how many ticks are due now.</summary>
        public int Advance(double seconds)
        {
            if (!Running) return 0;
            _farOutFor = _farOut ? _farOutFor + seconds : 0.0;
            _accumulator += seconds * Rate;

            int due = 0;
            while (_accumulator >= NetTime.TickSecondsPrecise)
            {
                _accumulator -= NetTime.TickSecondsPrecise;
                due++;
            }

            // A slow frame is caught up on in full, as a tick costs next to nothing and the server is waiting for those commands. Only
            // a long stall is given up on: the time is lost, and the clock finds its way back.
            return Math.Min(due, MostCatchUp);
        }

        public void Next() => Tick++;

        /// <summary>
        /// The server's report: commands reached it with at worst <paramref name="spare"/> ticks to go. Returns how many ticks the clock
        /// jumped by, which is nearly always 0.
        /// </summary>
        public int OnFeedback(int spare)
        {
            int error = spare - TargetSpare;

            _farOut = Math.Abs(error) > JumpBeyond;
            if (_farOut && _farOutFor >= JumpAfter)
            {
                _farOut = false;
                _farOutFor = 0.0;
                Rate = 1.0;
                int jump = -error;
                Tick = (uint)Math.Max((long)Tick + jump, 1L);
                return jump;
            }

            // Being too far ahead costs a little latency; being behind costs a mispredicted hero. So it runs from one harder than the other.
            double wanted = error > 0 ? -error * Gain : -error * Gain * 2.0;
            if (error == 0 || error == 1) wanted = 0.0;
            Rate = 1.0 + Math.Clamp(wanted, -MostDilation, MostDilation);
            return 0;
        }
    }

    /// <summary>
    /// The clock everything else is drawn by. It stays behind the newest snapshot by just enough that a next one is almost always
    /// already here to blend towards. That margin is measured, not assumed: it is the send interval plus what arrival times
    /// actually vary by, wider once snapshots have been seen going missing. On one machine it is a few hundredths of a second.
    /// Everything is measured over the last moment, never the whole game, and a stall on either end doesn't count as the line:
    /// whatever is learned from one is how far in the past the world is drawn, so every blow and swing shows that late for good.
    /// </summary>
    public sealed class InterpolationClock
    {
        private const double SnapBeyond = 30.0;         // Ticks.
        private const double MostSlew = 0.1;
        private const double SlewGain = 0.1;
        private const int Spans = 3;                    // The fastest path is the best of these, the newest still filling.
        private const double SpanSeconds = 0.5;
        private const double JitterResponse = 0.05;
        private const double JitterSpread = 1.5;        // From the change between arrivals to how far they run behind the fastest: a third of the range, to a half.
        private const double MostJitter = 30.0;         // Ticks. A change greater than this is a stall, and waiting for another would cost more than it did.
        private const double LossMemory = 8.0;          // Seconds.

        private readonly double[] _spans = new double[Spans];
        private int _span;
        private double _spanAge;
        private double _local;
        private double _fastest;
        private double _lastOffset;
        private double _jitter;
        private double _interval = 2.0;
        private uint _lastTick;
        private bool _heard;
        private double _sinceGap = LossMemory;

        public bool Started { get; private set; }

        /// <summary>The moment being drawn, in server ticks.</summary>
        public double RenderTick { get; private set; }

        /// <summary>How far behind the server's newest word that is, in ticks.</summary>
        public double Delay
        {
            get
            {
                double gapMargin = _sinceGap < LossMemory ? _interval : 0.0;
                return _interval + 1.0 + gapMargin + _jitter * 2.0;
            }
        }

        public double Jitter => _jitter;

        public void Reset()
        {
            Started = false;
            _lastTick = 0;
            _heard = false;
            _jitter = 0.0;
            _sinceGap = LossMemory;
        }

        public void OnSnapshot(uint tick, int sendInterval)
        {
            _interval = sendInterval;

            if (!Started)
            {
                Started = true;
                Array.Fill(_spans, double.NegativeInfinity);
                _lastOffset = tick - _local;
                RenderTick = tick - Delay;
            }
            else if (tick > _lastTick + sendInterval * 1.5)
            {
                _sinceGap = 0.0;
            }
            _lastTick = tick;
            _heard = true;
        }

        // How the newest snapshot compares to this client's own time. It is measured here, once a frame's time has been counted, and only
        // for the newest: those that came with it were waiting on this client, not on the line. Taken as they are read, a frame of
        // three seconds has its snapshots look three seconds early, as they arrive before the time that passed has been counted.
        private void Measure()
        {
            _heard = false;
            double offset = _lastTick - _local;
            if (offset > _spans[_span]) _spans[_span] = offset;

            _fastest = double.NegativeInfinity;
            foreach (double span in _spans) _fastest = Math.Max(_fastest, span);

            // Jitter is how much one arrival differs from the last, not how far behind the fastest it is: when the path moves for good
            // that's one change, where every arrival would run behind the fastest until the window had forgotten it.
            double change = Math.Abs(offset - _lastOffset);
            _lastOffset = offset;
            if (change <= MostJitter) _jitter += (change * JitterSpread - _jitter) * JitterResponse;
        }

        public void Advance(double seconds)
        {
            double ticks = seconds * NetTime.TicksPerSecond;
            _local += ticks;
            _sinceGap += seconds;
            if (!Started) return;

            // The fastest a snapshot has come lately is the best estimate of the path. Paths change, and when a server loses time the
            // whole of it moves for good, so this can't remember for long.
            _spanAge += seconds;
            if (_spanAge >= SpanSeconds)
            {
                _spanAge = 0.0;
                _span = (_span + 1) % Spans;
                _spans[_span] = double.NegativeInfinity;
            }

            if (_heard) Measure();

            double target = _local + _fastest - Delay;
            double error = target - RenderTick;
            if (Math.Abs(error) > SnapBeyond)
            {
                RenderTick = target;
                return;
            }
            RenderTick += ticks * (1.0 + Math.Clamp(error * SlewGain, -MostSlew, MostSlew));
        }
    }
}
