namespace Dino_Engine.Sound.Dsp
{
    /// <summary>
    /// The last thing a sound goes through. It looks ahead so that its gain is already down when a peak arrives, which means nothing
    /// gets past the ceiling and it never has to clip. It keeps a running minimum of the gain each sample needs over the look-ahead,
    /// then smooths that with a moving average of the same length: every sample is covered by a gain low enough for it, so
    /// the guarantee holds exactly, and the gain still moves smoothly enough not to be heard as distortion.
    /// </summary>
    public sealed class Limiter
    {
        private readonly int _lookahead;
        private readonly float[] _delayLeft;
        private readonly float[] _delayRight;
        private readonly long[] _queueIndex;
        private readonly float[] _queueGain;
        private readonly float[] _history;
        private readonly float _ceiling;
        private readonly float _release;
        private int _head, _tail;
        private long _position;
        private int _delayPosition;
        private int _historyPosition;
        private double _historySum;
        private float _held = 1f;

        /// <summary>Lowest gain it has used since this was last read, for a meter.</summary>
        public float Reduction { get; set; } = 1f;

        public Limiter(float ceilingDecibels = -1f, float lookaheadSeconds = 0.0015f, float releaseSeconds = 0.12f)
        {
            _lookahead = Math.Max((int)(lookaheadSeconds * AudioFormat.SampleRate), 2);
            _ceiling = AudioFormat.DecibelsToGain(ceilingDecibels);
            _release = 1f - MathF.Exp(-1f / (releaseSeconds * AudioFormat.SampleRate));
            _delayLeft = new float[_lookahead];
            _delayRight = new float[_lookahead];
            _queueIndex = new long[_lookahead + 1];
            _queueGain = new float[_lookahead + 1];
            _history = new float[_lookahead];
            Array.Fill(_history, 1f);
            _historySum = _lookahead;
        }

        /// <summary>How many samples late what comes out is.</summary>
        public int Latency => _lookahead - 1;

        /// <summary>Forgets what it's holding, for when something that's no number at all got into it. Allocates nothing.</summary>
        public void Clear()
        {
            Array.Clear(_delayLeft);
            Array.Clear(_delayRight);
            Array.Clear(_history);
            _historySum = 0.0;
            _head = _tail = 0;
            _held = 1f;
        }

        public void Process(Span<float> left, Span<float> right)
        {
            int count = left.Length;
            int capacity = _queueGain.Length;

            for (int i = 0; i < count; i++)
            {
                float inLeft = left[i];
                float inRight = right[i];
                float level = MathF.Max(MathF.Abs(inLeft), MathF.Abs(inRight));
                float needed = level > _ceiling ? _ceiling / level : 1f;

                // The smallest gain of the last while: a queue of values, each smaller than those before it.
                while (_head != _tail)
                {
                    int last = (_tail + capacity - 1) % capacity;
                    if (_queueGain[last] < needed) break;
                    _tail = last;
                }
                _queueGain[_tail] = needed;
                _queueIndex[_tail] = _position;
                _tail = (_tail + 1) % capacity;
                while (_queueIndex[_head] <= _position - _lookahead) _head = (_head + 1) % capacity;

                // Down at once; back up gently.
                float floor = _queueGain[_head];
                _held = floor < _held ? floor : _held + (floor - _held) * _release;

                _historySum += _held - _history[_historyPosition];
                _history[_historyPosition] = _held;
                _historyPosition = (_historyPosition + 1) % _lookahead;
                float gain = (float)(_historySum / _lookahead);

                float outLeft = _delayLeft[_delayPosition];
                float outRight = _delayRight[_delayPosition];
                _delayLeft[_delayPosition] = inLeft;
                _delayRight[_delayPosition] = inRight;
                _delayPosition = (_delayPosition + 1) % (_lookahead - 1);

                left[i] = outLeft * gain;
                right[i] = outRight * gain;
                if (gain < Reduction) Reduction = gain;
                _position++;
            }

            // Summing gains for ever would slowly drift.
            _historySum = 0.0;
            for (int i = 0; i < _lookahead; i++) _historySum += _history[i];
        }
    }
}
