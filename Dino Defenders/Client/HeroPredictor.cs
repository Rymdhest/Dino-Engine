using Dino_Defenders.Gameplay;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// The one thing a client works out for itself: where its own hero is, right now, without waiting to hear from the server. It
    /// runs the same <see cref="HeroRules.Step"/> the server does, on the same commands, and keeps what it worked out for each tick.
    /// When the server's word on a tick comes back, it is compared to what was predicted for that tick. Nearly always they
    /// match, and nothing happens. If not, the server is right: the hero goes back to what it said, and every command since is
    /// run again from there. So a client can be wrong for a moment, but can't stay wrong, and what it shows can't wander
    /// away from the truth.
    /// </summary>
    public sealed class HeroPredictor
    {
        private const int Ring = 256;
        private const float SmoothingRate = 14f;    // Per second: a correction is gone in a fraction of a second.
        private const float LongestSmoothed = 5f;   // Metres. Further than this and it's better to just be there.

        private readonly HeroCommand[] _commands = new HeroCommand[Ring];
        private readonly HeroSimState[] _states = new HeroSimState[Ring];
        private readonly uint[] _stateTicks = new uint[Ring];
        private HeroSimState _current;
        private HeroSimState _previous;
        private Vector2 _correction;
        private uint _newest;

        public HeroSimState Current => _current;

        /// <summary>Times the server has disagreed, and by how far at most, in metres.</summary>
        public int Corrections { get; private set; }
        public float WorstCorrection { get; private set; }
        public int Compared { get; private set; }

        public void ForgetWorst() => WorstCorrection = 0f;

        public void Reset(in HeroSimState state, uint tick)
        {
            _current = state;
            _previous = state;
            _correction = Vector2.Zero;
            _newest = tick;
            Remember(state, tick);
        }

        /// <summary>Runs one new tick. What it returns happens only here: running it again in a correction is silent.</summary>
        public HeroStepEvents Step(in HeroCommand command, HeroKit kit, CharacterMover mover)
        {
            _previous = _current;
            HeroStepEvents events = HeroRules.Step(ref _current, command, kit, mover);
            _commands[command.Tick % Ring] = command;
            _newest = command.Tick;
            Remember(_current, command.Tick);
            return events;
        }

        private void Remember(in HeroSimState state, uint tick)
        {
            _states[tick % Ring] = state;
            _stateTicks[tick % Ring] = tick;
        }

        /// <summary>The server's word on <paramref name="tick"/>. Returns whether it disagreed.</summary>
        public bool Reconcile(uint tick, in HeroSimState truth, HeroKit kit, CharacterMover mover)
        {
            Compared++;
            bool known = _stateTicks[tick % Ring] == tick;
            if (known && _states[tick % Ring].Matches(truth))
            {
                _current.TakeOrders(truth);
                return false;
            }

            Vector2 before = _current.Position;
            bool moved = truth.Moves != _current.Moves;
            bool stunned = known && (truth.Held > 0f) != (_states[tick % Ring].Held > 0f);
            Rewind(tick, truth, kit, mover);

            // Being put somewhere by the server isn't a wrong guess, however far it is, and nor is being stopped by a stun, as nobody can see one coming.
            float error = (before - _current.Position).Length;
            if (known && !moved && !stunned && error > 1e-3f)
            {
                Corrections++;
                WorstCorrection = MathF.Max(WorstCorrection, error);
            }

            // Being moved on purpose isn't a mistake to smooth over, and a small slip shouldn't show as a jump.
            _correction = moved ? Vector2.Zero : _correction + before - _current.Position;
            if (_correction.Length > LongestSmoothed) _correction = Vector2.Zero;
            return true;
        }

        /// <summary>The clock has jumped. Works things out afresh from the server's word, up to the new <paramref name="newest"/>.</summary>
        public void Jump(uint tick, in HeroSimState truth, uint newest, HeroKit kit, CharacterMover mover)
        {
            _newest = newest;
            Rewind(tick, truth, kit, mover);
        }

        private void Rewind(uint tick, in HeroSimState truth, HeroKit kit, CharacterMover mover)
        {
            _current = truth;
            _previous = truth;
            Remember(truth, tick);

            // Far enough behind and there's nothing left to replay, and the server's word is simply where things stand.
            if (_newest <= tick || _newest - tick >= Ring)
            {
                _newest = Math.Max(_newest, tick);
                return;
            }

            HeroCommand last = default;
            for (uint t = tick + 1; t <= _newest; t++)
            {
                HeroCommand command = _commands[t % Ring];
                if (command.Tick != t)
                {
                    // Nothing was sent for this tick. The server does the same thing in that case: more of the same.
                    command = last;
                    command.Tick = t;
                    command.Buttons &= ~HeroButtons.Presses;
                }
                last = command;

                _previous = _current;
                HeroRules.Step(ref _current, command, kit, mover);
                Remember(_current, t);
            }
        }

        /// <summary>Lets a correction fade.</summary>
        public void Smooth(float deltaTime)
        {
            _correction *= MathF.Exp(-SmoothingRate * deltaTime);
        }

        /// <summary>Where to draw the hero: between the last two ticks, with what's left of any correction.</summary>
        public Vector2 DrawnPosition(float alpha)
        {
            return Vector2.Lerp(_previous.Position, _current.Position, alpha) + _correction;
        }

        public float DrawnYaw(float alpha)
        {
            float difference = MathF.IEEERemainder(_current.Yaw - _previous.Yaw, MathF.Tau);
            return _previous.Yaw + difference * alpha;
        }
    }
}
