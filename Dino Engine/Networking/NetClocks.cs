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

            // After a stall the hero shouldn't lurch through a second of ticks, and the server wouldn't want them.
            return Math.Min(due, 8);
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
    /// </summary>
    public sealed class InterpolationClock
    {
        private const double SnapBeyond = 30.0;         // Ticks.
        private const double MostSlew = 0.1;
        private const double SlewGain = 0.1;
        private const double ForgetFastest = 0.1;       // Ticks a second.
        private const double JitterResponse = 0.05;
        private const double LossMemory = 8.0;          // Seconds.

        private double _local;
        private double _fastest;
        private double _jitter;
        private double _interval = 2.0;
        private uint _lastTick;
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
            _jitter = 0.0;
            _sinceGap = LossMemory;
        }

        public void OnSnapshot(uint tick, int sendInterval)
        {
            _interval = sendInterval;
            double offset = tick - _local;

            if (!Started)
            {
                Started = true;
                _fastest = offset;
                RenderTick = tick - Delay;
            }
            else
            {
                if (tick > _lastTick + sendInterval * 1.5) _sinceGap = 0.0;
                if (offset > _fastest) _fastest = offset;
                _jitter += (_fastest - offset - _jitter) * JitterResponse;
            }
            _lastTick = tick;
        }

        public void Advance(double seconds)
        {
            double ticks = seconds * NetTime.TicksPerSecond;
            _local += ticks;
            _sinceGap += seconds;
            if (!Started) return;

            // The fastest a snapshot has ever come is the best estimate of the path. Paths change, so it is slowly forgotten.
            _fastest -= seconds * ForgetFastest;

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
