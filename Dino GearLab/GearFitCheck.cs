using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_GearLab
{
    /// <summary>
    /// Whether what's worn stays where <see cref="GearFit"/> says it may, so that pieces made one at a time and never knowing what else is worn can't meet: every vertex of every piece, across
    /// everything a spec can say, measured from the body it's on against the bands of the region it's in. Bands are what keep two places apart, so a piece inside its own can't come through
    /// another's. Exit code 1 if any piece leaves its band. See GEAR.md.
    /// </summary>
    internal static class GearFitCheck
    {
        // What a polygon's flat between its corners and a strap's own thickness may take off a band.
        private const float Slack = 0.006f;

        private readonly record struct Fault(string Why, float By);

        public static int Run(Options options)
        {
            int count = options.Int("count", 600);
            int failures = 0;
            failures += Check(GearKind.Head, count, Head);
            failures += Check(GearKind.Body, count, Body);
            failures += Check(GearKind.Legs, count, Legs);
            failures += Check(GearKind.Feet, count, Feet);
            failures += Check(GearKind.Hands, count, Hands);
            Console.WriteLine(failures == 0 ? "All fit" : $"{failures} failed");
            return failures == 0 ? 0 : 1;
        }

        private static int Check(GearKind kind, int count, Func<Vector3, Fault?> rule)
        {
            var faults = new Dictionary<string, (int pieces, float worst, ulong seed, Vector3 at, string part)>();
            var parts = new Dictionary<string, Dictionary<string, int>>();
            int bad = 0;
            for (int i = 0; i < count; i++)
            {
                GearSpec spec = GearCheck.Sample(kind, (ulong)i * 2654435761UL + 7, i);
                GearMesh made = GearForge.Make(spec, new Dictionary<string, int>());
                var here = new Dictionary<string, (float by, Vector3 at, string part)>();
                for (int v = 0; v < made.VertexCount; v++)
                {
                    Vector3 position = made.Vertices[v].position;
                    if (rule(position) is not Fault fault) continue;
                    string part = PartOf(made, v);
                    parts.TryAdd(fault.Why, new Dictionary<string, int>());
                    if (!here.ContainsKey(fault.Why + "|" + part)) parts[fault.Why][part] = parts[fault.Why].GetValueOrDefault(part) + 1;
                    here.TryAdd(fault.Why + "|" + part, (0f, position, part));
                    if (!here.TryGetValue(fault.Why, out var seen) || fault.By > seen.by) here[fault.Why] = (fault.By, position, part);
                }
                if (here.Count > 0) bad++;
                foreach ((string why, (float by, Vector3 at, string part)) in here)
                {
                    if (why.Contains('|')) continue;
                    faults.TryGetValue(why, out var sum);
                    bool worse = by > sum.worst;
                    faults[why] = (sum.pieces + 1, MathF.Max(sum.worst, by), worse ? spec.Seed : sum.seed, worse ? at : sum.at, worse ? part : sum.part);
                }
            }
            Console.WriteLine($"  {(bad == 0 ? "ok  " : "FAIL")}  {kind,-6} {count} made, {bad} out of their bands");
            foreach ((string why, var f) in faults.OrderByDescending(f => f.Value.pieces))
            {
                string from = string.Join(", ", parts[why].OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}"));
                Console.WriteLine($"          {f.pieces,4} {why}, by up to {f.worst * 100f:0.0} cm (worst: seed {f.seed}, {f.part}, at {f.at.X:0.000}, {f.at.Y:0.000}, {f.at.Z:0.000}); in {from}");
            }
            return bad == 0 ? 0 : 1;
        }

        // The innermost named part that made a vertex.
        private static string PartOf(GearMesh made, int vertex)
        {
            string found = "unnamed";
            int span = int.MaxValue;
            foreach ((string name, int from, int to) in made.Parts ?? new())
            {
                if (vertex >= from && vertex < to && to - from < span)
                {
                    found = name;
                    span = to - from;
                }
            }
            return found;
        }

        // How far out from a leg's surface, the leg standing straight down from the hip.
        private static float FromLeg(Vector3 p)
        {
            float axis = MathF.Sign(p.X == 0f ? 1f : p.X) * BodyForm.HipX;
            float depth = 1f + 0.04f * Math.Clamp((p.Y - BodyForm.Knee) / 0.15f, 0f, 1f);
            float dx = p.X - axis, dz = p.Z / depth;
            return MathF.Sqrt(dx * dx + dz * dz) - BodyForm.Leg(MathF.Min(p.Y, BodyForm.Hip));
        }

        // How far out from the nearer leg's surface, either side.
        private static float FromLegs(Vector3 p)
        {
            float depth = 1f + 0.04f * Math.Clamp((p.Y - BodyForm.Knee) / 0.15f, 0f, 1f);
            float leg = BodyForm.Leg(MathF.Min(p.Y, BodyForm.Hip));
            float dz = p.Z / depth;
            float left = MathF.Sqrt((p.X - BodyForm.HipX) * (p.X - BodyForm.HipX) + dz * dz), right = MathF.Sqrt((p.X + BodyForm.HipX) * (p.X + BodyForm.HipX) + dz * dz);
            return MathF.Min(left, right) - leg;
        }

        // How far out from the body's surface, as a garment is laid: what's added to both its half width and its half depth.
        private static float FromBody(Vector3 p)
        {
            float t = BodyForm.Torso(MathF.Max(p.Y, 1.9f)), d = t * BodyForm.Depth;
            float low = -d * 0.9f, high = 2f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (low + high) * 0.5f;
                float a = p.X / (t + mid), b = p.Z / (d + mid);
                if (a * a + b * b > 1f) low = mid;
                else high = mid;
            }
            return (low + high) * 0.5f;
        }

        // How far out from an arm's surface, the arm out along x in the bind pose, and how far along it, 0 at the shoulder and 1 at the wrist.
        private static (float out_, float along) FromArm(Vector3 p)
        {
            float along = (MathF.Abs(p.X) - BodyForm.ShoulderX) / BodyForm.ArmLength;
            float dy = p.Y - BodyForm.Shoulder;
            return (MathF.Sqrt(dy * dy + p.Z * p.Z) - BodyForm.Arm(Math.Clamp(along, 0f, 1f)), along);
        }

        private static Fault? Head(Vector3 p)
        {
            if (p.Y < GearFit.NeckLow - Slack) return new Fault("below the neck's lowest", GearFit.NeckLow - p.Y);
            float across = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
            if (p.Y < GearFit.HelmLow - Slack && across > GearFit.NeckTuck + Slack) return new Fault("wider than the neck's tuck below the jaw, where the collar is", across - GearFit.NeckTuck);
            if (p.Y < GearFit.ShoulderTop - Slack && MathF.Abs(p.X) > GearFit.HeadSide + Slack) return new Fault("across into a shoulder's plate", MathF.Abs(p.X) - GearFit.HeadSide);
            return null;
        }

        private static Fault? Body(Vector3 p)
        {
            if (p.Y > GearFit.ShoulderTop + Slack) return new Fault("above a brim's lowest", p.Y - GearFit.ShoulderTop);
            float ax = MathF.Abs(p.X);
            if (p.Y > GearFit.CollarTop + Slack && ax < GearFit.PauldronIn - Slack) return new Fault("above the collar's top", p.Y - GearFit.CollarTop);

            // On the forearm, past an elbow's plate: fitted, inside a glove's cuff, or belled, outside it.
            (float arm, float along) = FromArm(p);
            if (along > 0.59f && MathF.Abs(p.Y - BodyForm.Shoulder) < 0.6f && arm > GearFit.SleeveFitted + Slack && arm < GearFit.SleeveBell - Slack)
            {
                return new Fault("a sleeve where a glove's cuff goes", MathF.Min(arm - GearFit.SleeveFitted, GearFit.SleeveBell - arm));
            }

            // Round the neck, never inside the collar's opening, where a helm's neck goes.
            if (p.Y > GearFit.CollarLow + Slack && ax < GearFit.PauldronIn)
            {
                float across = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
                if (across < GearFit.CollarInner - Slack) return new Fault("inside the collar's opening", GearFit.CollarInner - across);
            }

            // At the waist, outside the legs' seat.
            if (p.Y > BodyForm.Hip + 0.08f && p.Y < GearFit.LegsTop && ax < BodyForm.ShoulderX)
            {
                float out_ = FromBody(p);
                if (out_ < GearFit.BodyBase - Slack) return new Fault("inside the legs' seat at the waist", GearFit.BodyBase - out_);
            }

            // Below the hips, clear of both legs in whatever they have on.
            if (p.Y < BodyForm.Hip - 0.12f)
            {
                float legs = FromLegs(p);
                if (legs < GearFit.LegsShell + Slack * 0.5f) return new Fault("in a leg's way below the hips", GearFit.LegsShell - legs);
            }
            return null;
        }

        private static Fault? Legs(Vector3 p)
        {
            if (p.Y > GearFit.LegsTop + Slack) return new Fault("above the legs' top", p.Y - GearFit.LegsTop);
            if (p.Y > BodyForm.Hip - 0.08f)
            {
                if (p.Y > BodyForm.Hip + 0.08f && MathF.Abs(p.X) < BodyForm.ShoulderX)
                {
                    float waist = FromBody(p);
                    if (waist > GearFit.WaistUnder + Slack) return new Fault("out past the body's piece at the waist", waist - GearFit.WaistUnder);
                }
                return null;
            }
            float d = FromLeg(p);
            // Below the seat, which goes round both.
            if (p.Y < 1.75f && MathF.Abs(p.X) < 0.004f - 0.001f) return new Fault("past the middle into the other leg's", 0.004f - MathF.Abs(p.X));
            // Between the legs, where each leg's things are drawn in flat short of the middle, the bare legs cross and aren't drawn under a covering.
            if (MathF.Abs(p.X) > 0.06f && d < GearFit.LegsInner - Slack) return new Fault("inside the leg", GearFit.LegsInner - d);
            if (p.Y < GearFit.KneeLow && d > GearFit.LegsUnder + Slack) return new Fault("out past a boot's inside below the knee", d - GearFit.LegsUnder);
            if (p.Y < GearFit.BootTop + 0.02f && d > GearFit.LegsUnder + Slack && d < GearFit.BootTopOuter + 0.004f) return new Fault("where a boot's top is", MathF.Min(d - GearFit.LegsUnder, GearFit.BootTopOuter + 0.004f - d));
            // Inside what a skirt clears: the hem's ellipse, less its clearance.
            Vector2 hem = GearFit.HemAt(p.Y) - new Vector2(GearFit.HemClear * 0.5f);
            float e = MathF.Sqrt(p.X * p.X / (hem.X * hem.X) + p.Z * p.Z / (hem.Y * hem.Y));
            if (e > 1f) return new Fault("out past what a skirt clears", (e - 1f) * hem.Y);
            return null;
        }

        private static Fault? Feet(Vector3 p)
        {
            if (p.Y > GearFit.BootTop + 0.02f) return new Fault("above a boot's top", p.Y - GearFit.BootTop);
            // Below this is the shoe, round the foot and the ankle inside it, and forward of the shin is the foot.
            if (p.Y < 0.4f || p.Z > 0.26f) return null;
            float d = FromLeg(p);
            if (MathF.Abs(p.X) > 0.06f && d < GearFit.BootInner - Slack) return new Fault("inside the leg's covering", GearFit.BootInner - d);
            if (MathF.Abs(p.X) < GearFit.SideStrap - 0.001f) return new Fault("past the middle into the other foot's", GearFit.SideStrap - MathF.Abs(p.X));
            float most = p.Y > GearFit.KneeLow ? GearFit.BootTopOuter : p.Y <= GearFit.CuffLow + 0.02f ? GearFit.CuffOuter : GearFit.BootOuter;
            if (d > most + Slack) return new Fault("out past a boot's band", d - most);
            return null;
        }

        private static Fault? Hands(Vector3 p)
        {
            if (MathF.Abs(p.X) > BodyForm.WristX - 0.06f) return null;
            (float out_, float along) = FromArm(p);
            if (out_ < GearFit.GloveInner - Slack) return new Fault("inside a fitted sleeve", GearFit.GloveInner - out_);
            if (out_ > GearFit.GloveOuter + Slack) return new Fault("out past a glove's band", out_ - GearFit.GloveOuter);
            float reach = GearFit.ArmAt(GearFit.CuffReach);
            if (along < reach - 0.01f) return new Fault("up the arm past a cuff's reach", (reach - along) * BodyForm.ArmLength);
            return null;
        }
    }
}
