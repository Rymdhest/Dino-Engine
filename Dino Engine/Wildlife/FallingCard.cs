namespace Dino_Engine.Wildlife
{
    /// <summary>
    /// How something flat falls through air, solved once and read back. It's the model of Andersen, Pesavento and Wang (2005), "Analysis of transitions between fluttering, tumbling and steady
    /// descent of falling cards", equations 5.1 to 5.3 with 4.7 to 4.9 and their constants, which is a card's three degrees of freedom under added mass, a lift with a rotational term, and a drag
    /// that depends on the angle of attack. What that buys over a wobble is the part nobody would think to write: a card glides, slows, rises a little at the turn and swings back, and
    /// which of those it does depends on one number, its dimensionless moment of inertia.
    /// The equations don't depend on where a card is, and wind only moves the frame they're solved in, so one solution serves every leaf: it's a table of how far a card has come, and a
    /// leaf is only somewhere in it. That costs a lookup and can't blow up, whatever the frame rate is.
    /// </summary>
    public sealed class FallingCard
    {
        private const double Lift = 1.2;                // C_T
        private const double Spin = Math.PI;            // C_R
        private const double DragMean = 1.4;            // A
        private const double DragByAngle = 1.0;         // B
        private const double TorqueLinear = 0.2;        // mu_1
        private const double TorqueQuadratic = 0.2;     // mu_2

        private const double SolveStep = 0.01;
        private const int SolveStepsPerSample = 8;
        private const int Samples = 3072;
        private const int SettleSamples = 1024;         // A card lets go of how it was released in less than this.

        /// <summary>Dimensionless time between samples.</summary>
        public const float SampleTime = (float)(SolveStep * SolveStepsPerSample);

        private readonly float[] _x = new float[Samples];
        private readonly float[] _y = new float[Samples];
        private readonly float[] _angle = new float[Samples];
        private readonly int _loopFrom;
        private readonly float _loopX, _loopY, _loopAngle;

        /// <summary>The dimensionless moment of inertia this was solved for.</summary>
        public readonly float Inertia;

        /// <summary>How fast it comes down once it's settled, in its own units.</summary>
        public readonly float MeanFall;

        /// <summary>How fast it goes sideways once it's settled, in its own units. Tumbling glides away; fluttering stays over one place.</summary>
        public readonly float MeanDrift;

        /// <summary>Swings from side to side, gliding between turns: most leaves, and paper.</summary>
        public static readonly FallingCard Flutter = new FallingCard(1.1f);

        /// <summary>Goes over and over, and away to one side.</summary>
        public static readonly FallingCard Tumble = new FallingCard(1.4f);

        /// <summary>Some of each, by turns.</summary>
        public static readonly FallingCard Mixed = new FallingCard(1.6f);

        /// <summary>Hardly swings at all, and comes down broadside: a petal.</summary>
        public static readonly FallingCard Sink = new FallingCard(3f);

        private FallingCard(float inertia)
        {
            Inertia = inertia;
            double vx = 0.0, vy = 0.0, angle = 0.35, spin = 0.0;
            double x = 0.0, y = 0.0;

            for (int sample = 0; sample < Samples; sample++)
            {
                _x[sample] = (float)x;
                _y[sample] = (float)y;
                _angle[sample] = (float)angle;
                for (int i = 0; i < SolveStepsPerSample; i++) Advance(inertia, SolveStep, ref vx, ref vy, ref angle, ref spin, ref x, ref y);
            }

            // Past the end it carries on from the settled part, from whichever moment there comes closest to how the last one looks, so that what's periodic stays so.
            int last = Samples - 1;
            float bestMatch = float.PositiveInfinity;
            _loopFrom = SettleSamples;
            for (int sample = SettleSamples; sample < Samples - 256; sample++)
            {
                float angleOff = Wrap(_angle[sample] - _angle[last]);
                float stepX = (_x[sample + 1] - _x[sample]) - (_x[last] - _x[last - 1]);
                float stepY = (_y[sample + 1] - _y[sample]) - (_y[last] - _y[last - 1]);
                float match = angleOff * angleOff + (stepX * stepX + stepY * stepY) * 40f;
                if (match >= bestMatch) continue;
                bestMatch = match;
                _loopFrom = sample;
            }
            _loopX = _x[last] - _x[_loopFrom];
            _loopY = _y[last] - _y[_loopFrom];
            _loopAngle = _angle[last] - _angle[_loopFrom];

            float span = (last - _loopFrom) * SampleTime;
            MeanFall = -_loopY / span;
            MeanDrift = _loopX / span;
        }

        private static float Wrap(float angle)
        {
            angle %= MathF.Tau;
            if (angle > MathF.PI) angle -= MathF.Tau;
            if (angle < -MathF.PI) angle += MathF.Tau;
            return angle;
        }

        // Fourth order, as this runs once and the table is what everything else stands on.
        private static void Advance(double inertia, double h, ref double vx, ref double vy, ref double angle, ref double spin, ref double x, ref double y)
        {
            Slope(inertia, vx, vy, angle, spin, out double ax1, out double ay1, out double as1);
            double dx1 = vx * Math.Cos(angle) - vy * Math.Sin(angle), dy1 = vx * Math.Sin(angle) + vy * Math.Cos(angle);

            double vx2 = vx + ax1 * h * 0.5, vy2 = vy + ay1 * h * 0.5, angle2 = angle + spin * h * 0.5, spin2 = spin + as1 * h * 0.5;
            Slope(inertia, vx2, vy2, angle2, spin2, out double ax2, out double ay2, out double as2);
            double dx2 = vx2 * Math.Cos(angle2) - vy2 * Math.Sin(angle2), dy2 = vx2 * Math.Sin(angle2) + vy2 * Math.Cos(angle2);

            double vx3 = vx + ax2 * h * 0.5, vy3 = vy + ay2 * h * 0.5, angle3 = angle + spin2 * h * 0.5, spin3 = spin + as2 * h * 0.5;
            Slope(inertia, vx3, vy3, angle3, spin3, out double ax3, out double ay3, out double as3);
            double dx3 = vx3 * Math.Cos(angle3) - vy3 * Math.Sin(angle3), dy3 = vx3 * Math.Sin(angle3) + vy3 * Math.Cos(angle3);

            double vx4 = vx + ax3 * h, vy4 = vy + ay3 * h, angle4 = angle + spin3 * h, spin4 = spin + as3 * h;
            Slope(inertia, vx4, vy4, angle4, spin4, out double ax4, out double ay4, out double as4);
            double dx4 = vx4 * Math.Cos(angle4) - vy4 * Math.Sin(angle4), dy4 = vx4 * Math.Sin(angle4) + vy4 * Math.Cos(angle4);

            double sixth = h / 6.0;
            vx += (ax1 + 2.0 * ax2 + 2.0 * ax3 + ax4) * sixth;
            vy += (ay1 + 2.0 * ay2 + 2.0 * ay3 + ay4) * sixth;
            angle += (spin + 2.0 * spin2 + 2.0 * spin3 + spin4) * sixth;
            spin += (as1 + 2.0 * as2 + 2.0 * as3 + as4) * sixth;
            x += (dx1 + 2.0 * dx2 + 2.0 * dx3 + dx4) * sixth;
            y += (dy1 + 2.0 * dy2 + 2.0 * dy3 + dy4) * sixth;
        }

        // Equations 5.1 to 5.3, for a card much thinner than it's wide, in the frame that turns with it.
        private static void Slope(double inertia, double vx, double vy, double angle, double spin, out double ax, out double ay, out double angular)
        {
            double speedSquared = vx * vx + vy * vy;
            double speed = Math.Sqrt(speedSquared);
            double circulation = 2.0 / Math.PI * ((speed > 1e-9 ? -Lift * vx * vy / speed : 0.0) + Spin * spin);
            double drag = speedSquared > 1e-18 ? 1.0 / Math.PI * (DragMean - DragByAngle * (vx * vx - vy * vy) / speedSquared) * speed : 0.0;
            double torque = (TorqueLinear + TorqueQuadratic * Math.Abs(spin)) * spin;

            ax = ((inertia + 1.0) * spin * vy - circulation * vy - Math.Sin(angle) - drag * vx) / inertia;
            ay = (-inertia * spin * vx + circulation * vx - Math.Cos(angle) - drag * vy) / (inertia + 1.0);
            angular = (-vx * vy - torque) / (0.25 * (inertia + 0.5));
        }

        /// <summary>
        /// Where a card has got to after <paramref name="time"/> of its own units, from where it was let go. Forever, as past the end of the table it goes on from where it settled.
        /// <paramref name="x"/> is across and <paramref name="y"/> up, both in half widths of the card, and <paramref name="angle"/> is how far it's turned.
        /// </summary>
        public void At(float time, out float x, out float y, out float angle)
        {
            float place = time / SampleTime;
            float loops = 0f;
            int last = Samples - 1;
            if (place >= last)
            {
                float length = last - _loopFrom;
                float past = place - last;
                loops = MathF.Floor(past / length) + 1f;
                place = _loopFrom + (past - (loops - 1f) * length);
            }

            int index = Math.Min((int)place, last - 1);
            float t = place - index;
            x = _x[index] + (_x[index + 1] - _x[index]) * t + loops * _loopX;
            y = _y[index] + (_y[index + 1] - _y[index]) * t + loops * _loopY;
            angle = _angle[index] + (_angle[index + 1] - _angle[index]) * t + loops * _loopAngle;
        }
    }
}
