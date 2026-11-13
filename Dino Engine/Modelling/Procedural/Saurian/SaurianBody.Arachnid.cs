using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    // ---- What walks on eight: an abdomen swollen behind a narrow waist, a body like a low shield with eight legs round its edge, a short neck and a hunter's head. The knees stand over
    //      the back and the feet far out round it, and it's built that way, as it stands, which is where its skin bends best from. Its line runs as any other's does, from the tip of the
    //      abdomen, which is the tail's bones, through the waist, which is the hips, to the back of the skull.
    public sealed partial class SaurianBody
    {
        // Where each pair of legs leaves the body, along it from the waist forward as a share of its length, which way each foot stands from there, from straight ahead, and how far
        // out, as a share of the span. Front legs reach forward and the hind ones back, and both are the longest, as a spider's are.
        private static readonly (float At, float Angle, float Reach)[] Pairs =
        {
            (0.86f, 0.62f, 1.06f),     // The forelegs.
            (0.62f, 1.25f, 0.96f),
            (0.38f, 1.9f, 0.94f),
            (0.14f, 2.5f, 1.04f),      // The hind legs.
        };

        private void BuildArachnidAxis(Genes g)
        {
            SaurianSpecies s = Species;
            float skull = g.Range("skull", s.Skull) * Length;
            float neck = g.Range("neck", s.Neck) * Length;
            float trunk = g.Range("trunk", s.Trunk) * Length;
            float abdomen = MathF.Max(Length - skull - neck - trunk, Length * 0.2f);

            ChestDepth = g.Range("girth", s.Girth) * Length;
            ChestWidth = ChestDepth * g.Range("breadth", s.Breadth);
            BellyHang = 0f;
            HipJoint = g.Range("hip", s.Hip) * Length;
            float swell = g.Range("abdomen", s.Abdomen) * Length;
            float waist = ChestDepth * 0.42f;

            // The body is carried level and low, the abdomen tilted up behind it and rounding down to its tip, and the head held out in front and a little down, the way it's going.
            Vector3 hip = new Vector3(0f, HipJoint, 0f);
            Vector3 shoulder = hip + new Vector3(0f, MathF.Sin(g.Range("back slope", s.BackSlope)), 1f) * trunk;
            float rise = g.Range("neck rise", s.NeckRise);
            Vector3 neckRoot = shoulder + new Vector3(0f, ChestDepth * 0.15f, ChestDepth * 0.2f);
            Vector3 occiput = neckRoot + new Vector3(0f, MathF.Sin(rise), MathF.Cos(rise)) * neck;
            float headPitch = g.Range("head pitch", 0.12f, 0.24f);
            Vector3 headWay = new Vector3(0f, -MathF.Sin(headPitch), MathF.Cos(headPitch));
            float tilt = g.Range("abdomen tilt", 0.18f, 0.32f);
            Vector3 tip = hip + new Vector3(0f, MathF.Sin(tilt) * abdomen * 0.6f, -abdomen);

            var points = new List<Vector3>();
            void Curve(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int count, bool first)
            {
                for (int i = first ? 0 : 1; i <= count; i++)
                {
                    float t = i / (float)count, u = 1f - t;
                    points.Add(a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t));
                }
            }

            const int TailPoints = 40, TrunkPoints = 22, RootPoints = 4, NeckPoints = 8, HeadPoints = 14;
            Vector3 over = new Vector3(0f, swell * 0.55f, 0f);
            Curve(tip, tip + new Vector3(0f, swell * 0.2f, abdomen * 0.3f) + over * 0.6f, hip + new Vector3(0f, swell * 0.5f, -abdomen * 0.42f), hip, TailPoints, true);
            Curve(hip, hip + (shoulder - hip) * 0.33f, shoulder - (shoulder - hip) * 0.33f, shoulder, TrunkPoints, false);
            Curve(shoulder, shoulder + (neckRoot - shoulder) * 0.5f, neckRoot - new Vector3(0f, 0f, ChestDepth * 0.08f), neckRoot, RootPoints, false);
            Curve(neckRoot, neckRoot + (occiput - neckRoot) * 0.33f, occiput - headWay * neck * 0.3f, occiput, NeckPoints, false);
            Curve(occiput, occiput + headWay * skull * 0.33f, occiput + headWay * skull * 0.66f, occiput + headWay * skull, HeadPoints, false);

            var lengths = new float[points.Count];
            for (int i = 1; i < points.Count; i++) lengths[i] = lengths[i - 1] + (points[i] - points[i - 1]).Length;
            HipAt = lengths[TailPoints];
            ShoulderAt = lengths[TailPoints + TrunkPoints];
            NeckAt = lengths[TailPoints + TrunkPoints + RootPoints];
            HeadAt = lengths[TailPoints + TrunkPoints + RootPoints + NeckPoints];
            float headEnd = lengths[^1];

            // The abdomen is the tail's bones, evenly down it, and the body from the waist to the neck is hips, back and chest, one rigid piece as a spider's is.
            float run = HipAt;
            TailRootAt = HipAt - run * 0.1f;
            Tail1At = HipAt - run * 0.3f;
            Tail2At = HipAt - run * 0.5f;
            Tail3At = HipAt - run * 0.68f;
            TipAt = HipAt - run * 0.85f;
            SpineAt = HipAt + (ShoulderAt - HipAt) * 0.45f;
            ChestAt = HipAt + (ShoulderAt - HipAt) * 0.9f;
            NeckTopAt = NeckAt + (HeadAt - NeckAt) * 0.55f;
            Neck1At = Neck2At = Neck3At = NeckAt;
            float neckGirth = ChestDepth * g.Range("neck girth", s.NeckGirth);
            float wideRatio = ChestWidth / ChestDepth;

            float Deep(float along)
            {
                if (along <= HipAt)
                {
                    // An egg, fullest a little behind its middle, narrowing hard into the waist and rounding off at the tip.
                    float t = Math.Clamp(along / MathF.Max(HipAt, 1e-3f), 0f, 1f);
                    float egg = MathF.Pow(MathF.Max(MathF.Sin(MathF.PI * MathF.Pow(t, 0.8f)), 0f), 0.55f);
                    return MathF.Max(waist + (swell - waist) * egg, Length * 0.012f);
                }
                if (along <= ShoulderAt)
                {
                    float t = (along - HipAt) / MathF.Max(ShoulderAt - HipAt, 1e-3f);
                    return MathHelper.Lerp(waist, ChestDepth, Smooth(0f, 0.3f, t)) * (1f - 0.12f * Smooth(0.7f, 1f, t));
                }
                if (along <= NeckAt) return MathHelper.Lerp(ChestDepth * 0.88f, neckGirth, Smooth(ShoulderAt, NeckAt, along));
                if (along <= HeadAt) return MathHelper.Lerp(neckGirth, neckGirth * 0.8f, Smooth(NeckAt, HeadAt, along));
                return neckGirth * 0.8f * (1f - 0.5f * Smooth(HeadAt, headEnd, along));
            }

            // The abdomen as round as it's deep, the body a low shield twice as wide, and the neck rounding out to the head.
            float Wide(float along)
            {
                float d = Deep(along);
                if (along <= HipAt) return d * 1.04f;
                if (along <= ShoulderAt) return d * MathHelper.Lerp(1.1f, wideRatio, Smooth(HipAt, HipAt + (ShoulderAt - HipAt) * 0.3f, along));
                return d * MathHelper.Lerp(wideRatio * 0.8f, 0.9f, Smooth(ShoulderAt, HeadAt, along));
            }

            Axis = new SaurianAxis(points.ToArray(), Deep, Wide);
        }

        // Eight legs round the edge of the body, each built as it stands: the thigh up and out from the body to a knee that stands over the back, the shin down and out to an ankle a little
        // off the ground, and the foot down from there to its claw on the ground, far out round the body.
        private void BuildArachnidLegs(Genes g)
        {
            SaurianSpecies s = Species;
            float span = g.Range("span", s.Span) * Length;
            float knee = g.Range("knee", s.Knee) * Length;
            float ankle = HipJoint * g.Range("ankle", 0.3f, 0.38f);
            float thigh = ChestDepth * g.Range("thigh", s.Thigh);
            float shin = ChestDepth * g.Range("shin", s.Shin);
            float spread = g.Range("spread", -0.06f, 0.06f);

            for (int pair = 0; pair < 4; pair++)
            {
                (float at, float angle, float reach) = Pairs[pair];
                float along = HipAt + (ShoulderAt - HipAt) * at;
                Vector3 centre = Axis.At(along);
                float wide = Axis.WideAt(along);
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? 1f : -1f;
                    float turn = angle + spread * (pair - 1.5f);
                    Vector3 out_ = new Vector3(x * MathF.Sin(turn), 0f, MathF.Cos(turn));
                    var leg = new SaurianLeg { Side = x, Pair = pair + 1, Front = pair == 0 };
                    leg.Hip = centre + new Vector3(x * wide * 0.82f, -Axis.DeepAt(along) * 0.3f, 0f);
                    Vector3 foot = new Vector3(leg.Hip.X, 0f, leg.Hip.Z) + out_ * (span * reach);
                    leg.Ball = new Vector3(foot.X, ankle * 0.06f, foot.Z);
                    leg.Ankle = leg.Ball - out_ * (ankle * 0.32f) + new Vector3(0f, ankle * 0.94f, 0f);
                    float run = (new Vector3(leg.Ankle.X, 0f, leg.Ankle.Z) - new Vector3(leg.Hip.X, 0f, leg.Hip.Z)).Length;
                    leg.Knee = new Vector3(leg.Hip.X, 0f, leg.Hip.Z) + out_ * (run * g.Range("knee at", 0.34f, 0.4f)) + new Vector3(0f, knee * (0.95f + 0.1f * (pair == 0 ? 1f : 0f)), 0f);
                    leg.HipRadius = thigh * (pair == 0 || pair == 3 ? 1.05f : 1f);
                    leg.KneeRadius = shin;
                    leg.AnkleRadius = shin * 0.62f;
                    leg.BallRadius = shin * 0.34f;
                    leg.ToeLength = ankle * 0.25f;

                    // Hind pair first, forelegs next, as on four, then the two between, as the rig has them.
                    int index = pair switch { 3 => side, 0 => 2 + side, 1 => 4 + side, _ => 6 + side };
                    Legs[index] = leg;
                }
            }
        }
    }
}
