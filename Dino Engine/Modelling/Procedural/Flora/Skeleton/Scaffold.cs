using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>The way a stem bends as it grows. All of it is per metre, so that a stem keeps its character at any length.</summary>
    public struct StemCurve
    {
        public float Wander;    // Radians a metre it strays by, as slow waves.
        public float Waves;     // How many of those waves a stem has along it.
        public float Rise;      // Radians a metre it turns up by, or down below 0, towards the tip. Weber and Penn's AttractionUp.
        public float RiseFrom;  // How far along it starts to, 0 to 1.
        public float Sag;       // Radians a metre it turns down by, until then: the weight of a bough that lifts again at its end.
    }

    /// <summary>
    /// The architecture of a tree, stem by stem: the recursive model of Weber and Penn (1995), which is what's still the best at saying what a species is. A stem is a curve with
    /// children along it, each set by what level it's on: how many, where, at what angle and how long. Limbs stop at the edge of the crown instead of being cut there, as a stem has
    /// to know how long it is before its children can be placed (their section 4.6).
    /// </summary>
    internal static class Scaffold
    {
        public const float Step = 0.8f;     // Metres between points of a stem.

        /// <summary>Grows a stem from a point and returns its nodes. The first of them is <paramref name="parent"/>'s child.</summary>
        public static List<int> Stem(PlantGraph graph, int parent, int order, Vector3 from, Vector3 direction, float length, in StemCurve curve, WorldRandom random, float step = Step)
        {
            int steps = Math.Max((int)MathF.Round(length / step), 2);
            float stepLength = length / steps;
            uint seedA = (uint)random.NextULong();
            uint seedB = (uint)random.NextULong();

            direction = direction.Normalized();
            Vector3 side = Perpendicular(direction);
            Vector3 other = Vector3.Cross(direction, side);

            var nodes = new List<int>(steps + 1);
            Vector3 at = from;
            int last = graph.Add(at, parent, order);
            nodes.Add(last);
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                float wave = t * curve.Waves;
                direction += (side * FloraNoise.Wave(wave, seedA) + other * FloraNoise.Wave(wave, seedB)) * (curve.Wander * stepLength);
                float level = MathF.Sqrt(1f - MathF.Min(direction.Y * direction.Y / direction.LengthSquared, 1f));
                direction += Vector3.UnitY * ((t >= curve.RiseFrom ? curve.Rise : -curve.Sag) * stepLength * level);
                direction.Normalize();

                at += direction * stepLength;
                last = graph.Add(at, last, order);
                nodes.Add(last);
            }
            return nodes;
        }

        public static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 other = MathF.Abs(direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            return Vector3.Cross(other, direction).Normalized();
        }

        /// <summary>A direction turned away from <paramref name="axis"/> by <paramref name="angle"/>, on the side that <paramref name="around"/> says.</summary>
        public static Vector3 Away(Vector3 axis, Vector3 reference, float around, float angle)
        {
            Vector3 side = Vector3.Cross(axis, reference);
            if (side.LengthSquared < 1e-6f) side = Perpendicular(axis);
            side.Normalize();
            Vector3 other = Vector3.Cross(axis, side);
            Vector3 outward = side * MathF.Cos(around) + other * MathF.Sin(around);
            return (axis * MathF.Cos(angle) + outward * MathF.Sin(angle)).Normalized();
        }

        /// <summary>The limbs of one level along one stem. Returns the stems that came of it, each with what's on them, for the level after.</summary>
        public static List<List<int>> Limbs(PlantGraph graph, List<int> stem, LimbForm form, Envelope envelope, float scale, Genes genes)
        {
            WorldRandom random = genes.Stream("limbs");
            var limbs = new List<List<int>>();

            float stemLength = 0f;
            var along = new float[stem.Count];
            for (int i = 1; i < stem.Count; i++)
            {
                stemLength += (graph.Nodes[stem[i]].Position - graph.Nodes[stem[i - 1]].Position).Length;
                along[i] = stemLength;
            }
            if (stemLength < 0.1f) return limbs;

            int groups = form.Spacing > 0f
                ? Math.Max((int)(stemLength * (form.To - form.From) / (form.Spacing * random.Range(0.85f, 1.2f))), 1)
                : Math.Max((int)MathF.Round(random.Range(form.Count)), 0);
            int perGroup = Math.Max(form.Whorl, 1);
            float azimuth = random.Float() * MathF.Tau;
            float divergence = form.Divergence * (MathF.PI / 180f);

            for (int group = 0; group < groups; group++)
            {
                float t = groups > 1 ? (float)group / (groups - 1) : 0.5f;
                t = Math.Clamp(t + random.Range(-1f, 1f) * form.Jitter / groups, 0f, 1f);
                float wanted = (form.From + (form.To - form.From) * t) * stemLength;

                int index = 1;
                while (index < stem.Count - 1 && along[index] < wanted) index++;
                int parent = stem[index - 1];
                Vector3 at = graph.Nodes[parent].Position;
                Vector3 axis = (graph.Nodes[stem[index]].Position - at).Normalized();

                for (int member = 0; member < perGroup; member++)
                {
                    if (random.Float() < form.Missing) continue;
                    float around = azimuth + member * (MathF.Tau / perGroup) + random.Range(-1f, 1f) * form.DivergenceJitter;
                    float angle = form.Angle.Lerp(t) + random.Range(-1f, 1f) * form.AngleJitter;
                    Vector3 direction = Away(axis, Vector3.UnitY, around, angle);

                    float length = form.Length.Lerp(random.Float()) * form.Profile.At(t) * scale;
                    if (form.OfParent) length *= stemLength - along[index - 1] * form.Shrink;
                    if (form.Fit) length = Reach(envelope, at, direction, length, form.FitRoom);
                    if (length < form.Shortest) continue;

                    var curve = new StemCurve
                    {
                        Wander = form.Wander,
                        Waves = form.Waves,
                        Rise = form.Rise.Lerp(t),
                        RiseFrom = form.RiseFrom,
                        Sag = form.Sag,
                    };
                    List<int> limb = Stem(graph, parent, graph.Nodes[parent].Order + 1, at, direction, length, curve, random.Fork(group * 16 + member), form.StepLength);
                    limbs.Add(limb);
                }
                azimuth += divergence;
            }
            return limbs;
        }

        // How far a limb can go along a line before it leaves the crown.
        private static float Reach(Envelope envelope, Vector3 from, Vector3 direction, float length, float room)
        {
            const int Probes = 12;
            for (int i = 1; i <= Probes; i++)
            {
                float distance = length * i / Probes;
                if (envelope.Inside(from + direction * distance) < -room) return length * (i - 1) / Probes;
            }
            return length;
        }
    }

    /// <summary>One value that goes from one number at the bottom of a stem to another at the top, by way of a third.</summary>
    public readonly struct Ramp
    {
        public readonly float Start;
        public readonly float Middle;
        public readonly float End;

        public Ramp(float start, float end) : this(start, (start + end) * 0.5f, end)
        {
        }

        public Ramp(float start, float middle, float end)
        {
            Start = start;
            Middle = middle;
            End = end;
        }

        public static implicit operator Ramp(float value) => new Ramp(value, value, value);

        public float At(float t) => t < 0.5f ? MathHelper.Lerp(Start, Middle, t * 2f) : MathHelper.Lerp(Middle, End, t * 2f - 1f);

        public float Lerp(float t) => At(t);
    }
}
