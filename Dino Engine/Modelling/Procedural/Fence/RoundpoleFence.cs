using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>
    /// The north's fence of poles laid on a slant (a gärdesgård): stakes driven in pairs, each pair bound together at three heights by a band of twisted withies, and
    /// long thin poles, split or whole, laid between the stakes of every pair slanting the way the fence runs, each resting on the bands and on the poles under it, so
    /// that the stack is a row of parallel slants with daylight between them. It needs no nail and keeps the grazing in, and it's what a northern field and a northern
    /// lane are fenced with.
    /// A span is a bay of two stake intervals with a pair of stakes in its middle, and a post is a pair: the poles run on through the posts as if they weren't there,
    /// as the style's poles are the same lines in every span, so where one span's poles leave a post the next span's take them up at the same heights. A pole's foot
    /// is in the ground and its head stands a little over the stack, by an amount each of the style's poles keeps wherever it is, and only a head that's well inside a
    /// span may differ from one span to the next.
    /// </summary>
    internal static class RoundpoleFence
    {
        private static readonly float[] Along = { 0f, 0.5f, 1f };

        public static void Build(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            // The game looks down on a fence at 67 degrees, which squashes what stands to four tenths of its height, so the poles are laid steeper than most parishes lay
            // them and are fewer and thicker, each its own grey: at thirty degrees and close together they read from up here as a flat band, and these read as slants.
            float height = style.Range("height", 2.85f, 3.15f);                 // The top of the stack.
            float slope = MathF.Tan(style.Range("slant", 0.82f, 0.96f));        // About fifty degrees.
            float poleRadius = style.Range("pole", 0.1f, 0.125f);
            float stakeRadius = style.Range("stake", 0.065f, 0.08f);
            float stakeOver = style.Range("stake over", 0.55f, 0.95f);          // How far the stakes stand over the stack.
            float lean = style.Range("stake lean", 0.01f, 0.07f);               // A pair's tops lean together as the bands draw them.
            bool crossed = style.Chance("crossed stakes", 0.28f);               // Or cross over the top, as some parishes set them.
            bool split = style.Chance("split poles", 0.6f);                     // Poles split in half, or whole thin trunks.
            float rise = style.Range("pole rise", 0.72f, 0.92f);                // How far apart the poles of the stack are, up a stake.
            float gap = poleRadius + stakeRadius + 0.012f;                      // Half the room between a pair's stakes, which the poles pass through.
            Vector3 wood = FenceLook.Weathered(style) * style.Range("pole shade", 1.12f, 1.3f);    // Silver, as old spruce goes.
            Vector3 stakeWood = wood * style.Range("stake shade", 0.78f, 0.9f);
            var poles = new Surface(FenceMaterials.Weathered, wood, 1.4f);
            var stakes = new Surface(FenceMaterials.Weathered, stakeWood, 1.2f);
            var withy = new Surface(FenceMaterials.Bark, new Vector3(0.36f, 0.3f, 0.24f) * style.Range("withy shade", 0.85f, 1.1f), 0.5f);
            float[] bands = { height * style.Range("low band", 0.2f, 0.27f), height * style.Range("middle band", 0.5f, 0.57f), height * style.Range("high band", 0.82f, 0.9f) };

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("stakes");
                if (job.IsEnd)
                {
                    // Where a run stops or turns: two pairs set square, so that the poles coming in from either way are held, and the stakes stouter and taller.
                    float g = gap * 1.05f;
                    var corners = new[] { new Vector2(g, g), new Vector2(-g, g), new Vector2(-g, -g), new Vector2(g, -g) };
                    foreach (Vector2 c in corners) Stake(b, new Vector3(c.X, 0f, c.Y), new Vector2(-c.X, -c.Y) * 0.4f * lean / g, stakeRadius * 1.18f, height + stakeOver + 0.15f, job.Sink, crossed, stakes, random);
                    foreach (float y in bands) Band(b, corners, y + random.Range(-0.04f, 0.04f), stakeRadius * 1.18f, withy);
                    return;
                }
                StakePair(b, 0f, gap, stakeRadius, height + stakeOver, lean, crossed, job.Sink, bands, stakes, withy, random);
                return;
            }

            // The poles: as many to a bay as the stack's spacing makes it, so that a bay is a whole number of them and the next span takes them up where this one leaves them.
            float bay = job.Bay;
            float pitch = rise / slope;
            int perBay = Math.Max(4, (int)MathF.Round(bay / pitch));
            pitch = bay / perBay;
            float phase = style.Range("phase", 0f, 1f) * pitch;
            float run = (height + job.Sink + 0.5f) / slope;
            WorldRandom own = job.Own.Stream("poles");
            int first = (int)MathF.Floor((-run - phase) / pitch) - 1, last = (int)MathF.Ceiling((bay - phase) / pitch) + 1;
            for (int j = first; j <= last; j++)
            {
                int slot = ((j % perBay) + perBay) % perBay;
                Genes pole = style.Part("pole").Part(slot);
                float x0 = phase + j * pitch;                                   // Where its line comes out of the ground.
                float over = pole.Range("over", -0.1f, 0.45f);
                float headX = x0 + (height + over) / slope;
                float footX = x0 - job.Sink / slope;
                // A head well inside the span may lie a little further on or short here than in the next span along.
                if (headX > 0.35f && headX < bay - 0.35f) headX += own.Range(-0.2f, 0.2f);
                float from = MathF.Max(footX, 0f), to = MathF.Min(headX, bay);
                if (to - from < 0.03f) continue;

                float radius = poleRadius * pole.Range("thick", 0.84f, 1.16f);
                float side = pole.Range("side", -0.018f, 0.018f);
                Vector2[] section = split ? HalfRound(radius, pole.Range("turn", -0.7f, 0.7f) + (pole.Chance("flat up", 0.5f) ? MathF.PI : 0f)) : Round(radius, pole.Stream("round"));
                Vector3 foot = new Vector3(from, (from - x0) * slope, side);
                Vector3 head = new Vector3(to, (to - x0) * slope, side);
                float bow = (to - from) * own.Range(0.004f, 0.012f);
                // Each pole a grey of its own, as each was cut and has weathered on its own: what draws the slants from up here.
                Surface surface = poles.Tinted(pole.Range("tone", 0.72f, 1.2f) * own.Range(0.94f, 1.06f));
                bool cutFoot = footX < 0f;                                       // Cut where the span starts, as at the end of a run; otherwise in the ground.
                b.Prism(foot, head, Vector3.UnitY, section, surface, Along, t => (new Vector3(0f, -bow * MathF.Sin(MathF.PI * t), 0f), 1f, 0f),
                    capFoot: cutFoot, capHead: true, endColour: FenceLook.EndGrain(surface.Colour));
            }

            // The pair of stakes in the middle of the bay, standing upright on its slope.
            int mark = b.Mark;
            StakePair(b, bay * 0.5f, gap, stakeRadius, height + stakeOver, lean, crossed, job.Sink, bands, stakes, withy, own);
            b.Upright(mark, bay * 0.5f);
        }

        // Two stakes either side of the poles' line at x, bound at each band's height.
        private static void StakePair(FenceBuilder b, float x, float gap, float radius, float top, float lean, bool crossed, float sink, float[] bands, Surface stakes, Surface withy, WorldRandom random)
        {
            Stake(b, new Vector3(x, 0f, gap), new Vector2(0f, -lean), radius, top, sink, crossed, stakes, random);
            Stake(b, new Vector3(x, 0f, -gap), new Vector2(0f, lean), radius, top, sink, crossed, stakes, random);
            var pair = new[] { new Vector2(x, gap), new Vector2(x, -gap) };
            foreach (float y in bands) Band(b, pair, y + random.Range(-0.04f, 0.04f), radius, withy);
        }

        // A stake, driven into the ground, its top leaning by `lean` (x, z) towards its partner or, crossed, over past it.
        private static void Stake(FenceBuilder b, Vector3 at, Vector2 lean, float radius, float top, float sink, bool crossed, Surface surface, WorldRandom random)
        {
            float tall = top + random.Range(-0.12f, 0.12f);
            Vector2 tip = crossed ? lean * 3.2f : lean;
            Vector3 head = new Vector3(at.X + tip.X + random.Range(-0.03f, 0.03f), tall, at.Z + tip.Y);
            b.Log(new Vector3(at.X, -sink - 0.3f, at.Z), head, radius * 1.05f, radius * 0.86f, 7, surface.Tinted(random.Range(0.86f, 1.06f)), (uint)random.Int(int.MaxValue),
                headEnd: LogEnd.Cut, rough: 0.1f, rings: 2, endColour: FenceLook.EndGrain(surface.Colour));
        }

        // A band of twisted withies round a set of stakes at a height, as a loop round the outside of them all.
        private static void Band(FenceBuilder b, Vector2[] stakes, float y, float stakeRadius, Surface surface)
        {
            const int PerArc = 4, Sides = 3;
            const float Thick = 0.026f;
            float reach = stakeRadius + Thick;
            Vector2 centre = Vector2.Zero;
            foreach (Vector2 s in stakes) centre += s;
            centre /= stakes.Length;
            Vector2[] order = stakes.OrderBy(s => MathF.Atan2(s.Y - centre.Y, s.X - centre.X)).ToArray();
            int m = order.Length;

            // Round each stake from the outward normal of the edge before it to the outward normal of the edge after it.
            var path = new List<Vector3>();
            for (int i = 0; i < m; i++)
            {
                Vector2 previous = order[(i - 1 + m) % m], here = order[i], next = order[(i + 1) % m];
                Vector2 into = here - previous, onward = next - here;
                float a0 = MathF.Atan2(-into.X, into.Y), a1 = MathF.Atan2(-onward.X, onward.Y);
                if (a1 < a0) a1 += MathF.Tau;
                for (int k = 0; k <= PerArc; k++)
                {
                    float a = a0 + (a1 - a0) * k / PerArc;
                    // Drawn tight, and a little twisted up and down as it goes round.
                    path.Add(new Vector3(here.X + reach * MathF.Cos(a), y + 0.025f * MathF.Sin(a * 2f + i), here.Y + reach * MathF.Sin(a)));
                }
            }

            int count = path.Count;
            int start = b.Mesh.VertexCount;
            for (int k = 0; k < count; k++)
            {
                Vector3 tangent = (path[(k + 1) % count] - path[(k - 1 + count) % count]).Normalized();
                Vector3 across = Vector3.Cross(tangent, Vector3.UnitY).Normalized();
                for (int s = 0; s < Sides; s++)
                {
                    float angle = s * MathF.Tau / Sides;
                    Vector3 direction = across * MathF.Cos(angle) + Vector3.UnitY * MathF.Sin(angle);
                    b.Mesh.Add(new FloraVertex { Position = path[k] + direction * Thick, Normal = direction, Tangent = tangent, Colour = surface.Colour * (0.85f + 0.15f * MathF.Sin(k * 1.7f + s)), UV = new Vector2(k * 0.12f, s / (float)Sides), Material = surface.Material, Joint = -1 });
                }
            }
            for (int k = 0; k < count; k++)
            {
                int k1 = (k + 1) % count;
                for (int s = 0; s < Sides; s++)
                {
                    int s1 = (s + 1) % Sides;
                    int a = start + k * Sides + s, c = start + k1 * Sides + s1, d = start + k1 * Sides + s, e = start + k * Sides + s1;
                    Wound(b, a, d, c);
                    Wound(b, a, c, e);
                }
            }
        }

        // A triangle of vertices already in the mesh, turned to face the way its corners' normals do.
        private static void Wound(FenceBuilder b, int a, int c, int d)
        {
            FloraVertex va = b.Mesh.Vertices[a], vc = b.Mesh.Vertices[c], vd = b.Mesh.Vertices[d];
            Vector3 face = Vector3.Cross(vc.Position - va.Position, vd.Position - va.Position);
            if (Vector3.Dot(face, va.Normal + vc.Normal + vd.Normal) >= 0f) b.Mesh.Triangle(a, c, d);
            else b.Mesh.Triangle(a, d, c);
        }

        // A whole pole's section: six sides, none quite the same.
        private static Vector2[] Round(float radius, WorldRandom random)
        {
            var points = new Vector2[6];
            float spin = random.Range(0f, MathF.Tau);
            for (int i = 0; i < points.Length; i++)
            {
                float angle = spin + i * MathF.Tau / points.Length;
                float reach = radius * random.Range(0.93f, 1.05f);
                points[i] = new Vector2(MathF.Cos(angle) * reach, MathF.Sin(angle) * reach);
            }
            return points;
        }

        // A split pole's section: round on its back and flat where it was riven, turned as it was laid.
        private static Vector2[] HalfRound(float radius, float turn)
        {
            const int Arc = 5;
            var points = new Vector2[Arc + 1];
            float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
            for (int i = 0; i <= Arc; i++)
            {
                float angle = MathF.PI * i / Arc;
                Vector2 p = new Vector2(MathF.Cos(angle) * radius * 1.15f, MathF.Sin(angle) * radius * 0.95f - radius * 0.38f);
                points[i] = new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
            }
            return points;
        }
    }
}
