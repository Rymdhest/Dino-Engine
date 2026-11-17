using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Seats
{
    /// <summary>
    /// What a commander's seat is built with (SEATS.md), beyond what every place with a story is (<see cref="Kit"/>): walling laid in courses with its trim in dressed blocks, pointed arches and
    /// the wedge stones round them, battlements, round towers, roofs that weather in patches, cloth that hangs and moves, and fires big enough to light a yard. Walling is the rubble
    /// material, whose courses run level only on a face laid out with its texture's axes given, so every wall here is a <see cref="MeshBuilder.Panel"/> or a block with no arris taken off;
    /// what's dressed is a block of its own no longer than a stone is, as a long face of dressed stone reads as crazy paving. Colours are albedos under a sun of about 13. Headless.
    /// </summary>
    internal static class SeatKit
    {
        public const float Ground = PlaceMaterials.Ground;

        // How much paler than the cut stone an id carries each material is drawn, as each has an albedo of its own: found by eye, from the game's camera.
        private const float WallingLift = 1.25f;
        private const float AshlarLift = 1.5f;

        /// <summary>The Reich's own stone, where it built rather than took: dark, close-grained granite (as its cathedral is), per mille.</summary>
        public static readonly (int R, int G, int B) ReichStone = (300, 292, 300);

        /// <summary>A stone's colour from three numbers of an id, starting at <paramref name="first"/>.</summary>
        public static Vector3 StoneOf(in PlaceSpec spec, int first) => StoneColour(spec.Int(first), spec.Int(first + 1), spec.Int(first + 2));

        /// <summary>Rubble walling in a stone's colour, laid in courses.</summary>
        public static VertexMaterial Walling(Vector3 stone, WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.StoneWall.Index, new Colour(stone * (WallingLift * shade * random.Range(0.97f, 1.03f))));

        /// <summary>A dressed block of the same stone, a little paler than the walling it trims, darker towards the ground.</summary>
        public static VertexMaterial Ashlar(Vector3 stone, WorldRandom random, float y = 3f, float shade = 1f) => new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(stone * (AshlarLift * shade * Grime(y) * random.Range(0.92f, 1.06f))));

        /// <summary>A dressed block whose top has weathered paler than its faces.</summary>
        public static Func<Vector3, VertexMaterial> AshlarFacing(Vector3 stone, WorldRandom random, float y = 3f, float shade = 1f)
        {
            VertexMaterial face = Ashlar(stone, random, y, shade);
            VertexMaterial top = Tint(face, new Vector3(1.12f, 1.11f, 1.06f));
            return n => n.Y > 0.6f ? top : face;
        }

        /// <summary>Setts of the same stone, for a wall's walk and a terrace's edge.</summary>
        public static VertexMaterial Paving(Vector3 stone, float shade = 1f) => new VertexMaterial(MaterialCatalog.Setts.Index, new Colour(stone * (1.5f * shade)));

        /// <summary>Slate, pale and a little blue, as a dark roof is a hole from the game's camera.</summary>
        public static Vector3 SlateColour => new Vector3(0.5f, 0.53f, 0.6f);

        public static VertexMaterial Slate(float shade = 1f) => new VertexMaterial(MaterialCatalog.Slate.Index, new Colour(SlateColour * shade));

        /// <summary>Lead, on a ridge, a finial or a little roof.</summary>
        public static VertexMaterial Lead(WorldRandom random) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(new Vector3(0.3f, 0.31f, 0.34f) * random.Range(0.92f, 1.06f)));

        /// <summary>Shingles gone silver in the weather.</summary>
        public static VertexMaterial Shingles(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(new Vector3(0.52f, 0.47f, 0.4f) * shade * random.Range(0.94f, 1.05f)));

        /// <summary>A roof's colour by where a point of it is: paler and drier up the slope, greener towards the eaves, in soft patches, so that it's never one flat colour.</summary>
        public static Func<Vector3, Colour> Weathering(Vector3 colour, WorldRandom random, float eaves, float ridge, float moss = 0.5f)
        {
            var noise = new WorldNoise(random.Fork("weathering"));
            return p =>
            {
                float patch = noise.Fbm01(p.X * 1.7f + p.Y, p.Z * 1.7f - p.Y, 5f, 2);
                float up = Math.Clamp((p.Y - eaves) / MathF.Max(ridge - eaves, 0.1f), 0f, 1f);
                Vector3 c = colour * (0.86f + 0.26f * patch) * (0.92f + 0.12f * up);
                float green = moss * (1f - up) * (1f - up) * Math.Clamp(patch * 1.6f - 0.3f, 0f, 1f);
                return new Colour(Vector3.Lerp(c, new Vector3(0.3f, 0.36f, 0.22f), green * 0.6f));
            };
        }

        // ---------------------------------------------------------------- Walls

        /// <summary>
        /// A quad of walling whose first two corners are its foot, left to right as it's seen from outside, so that its courses run level: <paramref name="a"/> and <paramref name="c"/> along its
        /// foot, then <paramref name="d"/> above c and <paramref name="e"/> above a.
        /// </summary>
        public static void Courses(MeshBuilder b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, VertexMaterial walling) => b.Quad(a, c, d, e, walling);

        /// <summary>A box of walling from its least corner to its most, its top in <paramref name="top"/> if that's given, its courses level all round.</summary>
        public static void WallBox(MeshBuilder b, Vector3 min, Vector3 max, VertexMaterial walling, VertexMaterial? top = null)
        {
            VertexMaterial cap = top ?? walling;
            Kit.Span(b, min, max, 0f, n => n.Y > 0.5f ? cap : walling);
        }

        // ---------------------------------------------------------------- Arches

        /// <summary>
        /// A point of a pointed arch <paramref name="halfWidth"/> across, <paramref name="t"/> along it from its left springing to its right: x from its middle and y above its springing.
        /// <paramref name="sharp"/> is how pointed it is: 0 is a half circle and 1 an equilateral arch.
        /// </summary>
        public static Vector2 ArchPoint(float halfWidth, float sharp, float t)
        {
            float rho = halfWidth * (1f + sharp);
            float apex = MathF.Acos(-sharp / (1f + sharp));
            float along = MathF.Min(t, 1f - t) * 2f;
            float a = MathF.PI - (MathF.PI - apex) * along;
            float x = sharp * halfWidth + rho * MathF.Cos(a);
            return new Vector2(t <= 0.5f ? x : -x, rho * MathF.Sin(a));
        }

        /// <summary>How high above its springing a pointed arch's crown is.</summary>
        public static float ArchRise(float halfWidth, float sharp) => ArchPoint(halfWidth, sharp, 0.5f).Y;

        /// <summary>How high above its springing a pointed arch is, <paramref name="x"/> from its middle: nothing outside it.</summary>
        public static float ArchHeightAt(float halfWidth, float sharp, float x)
        {
            float rho = halfWidth * (1f + sharp), reach = MathF.Abs(x) + sharp * halfWidth;
            return reach >= rho ? 0f : MathF.Sqrt(rho * rho - reach * reach);
        }

        /// <summary>
        /// A wall's face with an arched opening in it: a face laid out by <paramref name="origin"/>, <paramref name="u"/> and <paramref name="v"/> (as a <see cref="MeshBuilder.Panel"/> is), from
        /// <paramref name="u0"/> to <paramref name="u1"/> along and up to <paramref name="v1"/>, seen from the side cross(u, v) faces; its opening <paramref name="halfWidth"/> either side of
        /// <paramref name="centre"/>, from <paramref name="sill"/> up to an arch springing at <paramref name="spring"/>. The wall above the arch is laid in upright strips up to its top.
        /// </summary>
        public static void ArchedPanel(MeshBuilder b, Vector3 origin, Vector3 u, Vector3 v, float u0, float u1, float v1, float centre, float halfWidth, float sill, float spring, float sharp, VertexMaterial walling, int steps = 10, float v0 = 0f)
        {
            var bounds = new Vector4(u0, u1, v0, v1);
            b.Panel(origin, u, v, u0, centre - halfWidth, v0, v1, walling, bounds);
            b.Panel(origin, u, v, centre + halfWidth, u1, v0, v1, walling, bounds);
            if (sill > v0) b.Panel(origin, u, v, centre - halfWidth, centre + halfWidth, v0, sill, walling, bounds);
            for (int k = 0; k < steps; k++)
            {
                Vector2 p0 = ArchPoint(halfWidth, sharp, k / (float)steps), p1 = ArchPoint(halfWidth, sharp, (k + 1f) / steps);
                Vector3 a = origin + u * (centre + p0.X) + v * (spring + p0.Y), c = origin + u * (centre + p1.X) + v * (spring + p1.Y);
                Vector3 at = origin + u * (centre + p0.X) + v * v1, ct = origin + u * (centre + p1.X) + v * v1;
                b.PlaneQuad(a, c, ct, at, walling, origin, u, v, bounds);
            }
        }

        /// <summary>
        /// The wedge stones round an arch laid out as <see cref="ArchedPanel"/>'s is, and the long and short stones down its jambs to <paramref name="sill"/>: each a block of its own,
        /// <paramref name="deep"/> across its face, standing <paramref name="proud"/> of the wall and going <paramref name="into"/> it.
        /// </summary>
        public static void ArchStones(MeshBuilder b, Vector3 origin, Vector3 u, Vector3 v, float centre, float halfWidth, float sill, float spring, float sharp, float deep, float proud, float into, Vector3 stone, WorldRandom random, int count = 11)
        {
            Vector3 n = Vector3.Cross(u, v).Normalized();
            Vector3 At(Vector2 p, float out_) => origin + u * (centre + p.X) + v * (spring + p.Y) + n * out_;
            float outer = (halfWidth + deep) / halfWidth;
            for (int k = 0; k < count; k++)
            {
                Vector2 p0 = ArchPoint(halfWidth, sharp, k / (float)count), p1 = ArchPoint(halfWidth, sharp, (k + 1f) / count);
                Vector2 q0 = p0 * outer, q1 = p1 * outer;
                float lift = k == count / 2 ? 0.06f : 0f;     // The keystone a little prouder than the rest.
                Kit.Solid(b, new[] { At(p0, proud + lift), At(p1, proud + lift), At(q1, proud + lift), At(q0, proud + lift), At(p0, -into), At(p1, -into), At(q1, -into), At(q0, -into) }, 0.035f, AshlarFacing(stone, random, spring + p0.Y));
            }
            // The jambs, long and short by turns.
            int courses = Math.Max(1, (int)MathF.Round((spring - sill) / 0.75f));
            float course = (spring - sill) / courses;
            for (int i = 0; i < courses; i++)
            {
                float y0 = sill + i * course, y1 = y0 + course;
                float width = deep + (i % 2 == 0 ? 0.28f : 0f);
                foreach (float side in new[] { -1f, 1f })
                {
                    float x0 = centre + side * halfWidth, x1 = centre + side * (halfWidth + width);
                    Vector3 P(float x, float y, float out_) => origin + u * x + v * y + n * out_;
                    Kit.Solid(b, new[] { P(x0, y0 + 0.01f, proud), P(x1, y0 + 0.01f, proud), P(x1, y1 - 0.01f, proud), P(x0, y1 - 0.01f, proud), P(x0, y0 + 0.01f, -into), P(x1, y0 + 0.01f, -into), P(x1, y1 - 0.01f, -into), P(x0, y1 - 0.01f, -into) }, 0.035f, AshlarFacing(stone, random, y0));
                }
            }
        }

        /// <summary>What fills an arched opening, a little behind the wall's face: glass lit from inside, a door's dark, or anything flat. Laid out as <see cref="ArchedPanel"/>'s opening is.</summary>
        public static void ArchFill(MeshBuilder b, Vector3 origin, Vector3 u, Vector3 v, float centre, float halfWidth, float sill, float spring, float sharp, float behind, VertexMaterial fill, int steps = 8)
        {
            Vector3 n = Vector3.Cross(u, v).Normalized();
            Vector3 P(float x, float y) => origin + u * (centre + x) + v * y - n * behind;
            Kit.Face(b, P(-halfWidth, sill), P(halfWidth, sill), P(halfWidth, spring), P(-halfWidth, spring), n, fill);
            for (int k = 0; k < steps; k++)
            {
                Vector2 p0 = ArchPoint(halfWidth, sharp, k / (float)steps), p1 = ArchPoint(halfWidth, sharp, (k + 1f) / steps);
                Kit.Face(b, P(0f, spring), P(p0.X, spring + p0.Y), P(p1.X, spring + p1.Y), n, fill);
            }
        }

        /// <summary>
        /// A tall window in a wall laid out as <see cref="ArchedPanel"/>'s is: its glass lit from inside or dark, a mullion up its middle and a bar across it at its springing, in iron, as the
        /// Reich has barred every window it took.
        /// </summary>
        public static void Window(MeshBuilder b, Vector3 origin, Vector3 u, Vector3 v, float centre, float halfWidth, float sill, float spring, float sharp, bool lit, Vector3 stone, WorldRandom random)
        {
            Vector3 n = Vector3.Cross(u, v).Normalized();
            // Deep, and not strong: glass that glows pale goes white from the game's camera.
            ArchFill(b, origin, u, v, centre, halfWidth, sill, spring, sharp, 0.16f, lit ? Glow(1f, 0.5f, 0.15f, random.Range(0.36f, 0.52f)) : Glass(new Vector3(0.08f, 0.09f, 0.11f)));
            ArchStones(b, origin, u, v, centre, halfWidth, sill, spring, sharp, 0.32f, 0.07f, 0.2f, stone, random, 7);
            VertexMaterial iron = Iron(random);
            Vector3 P(float x, float y, float out_) => origin + u * (centre + x) + v * y + n * out_;
            float top = spring + ArchRise(halfWidth, sharp);
            Kit.Box(b, P(0f, (sill + top) * 0.5f, -0.05f), u, v, n, new Vector3(0.05f, (top - sill) * 0.5f, 0.05f), 0f, iron);
            Kit.Box(b, P(0f, spring, -0.05f), u, v, n, new Vector3(halfWidth, 0.05f, 0.05f), 0f, iron);
            // Its sill, a dressed stone standing out to shed the rain.
            Kit.Box(b, P(0f, sill - 0.14f, 0.1f), u, v, n, new Vector3(halfWidth + 0.4f, 0.14f, 0.22f), 0.03f, AshlarFacing(stone, random, sill));
        }

        // ---------------------------------------------------------------- Battlements

        /// <summary>
        /// A parapet with merlons along a line: from <paramref name="from"/> to <paramref name="to"/> along the middle of its foot, <paramref name="thick"/> through, its parapet
        /// <paramref name="parapet"/> high in walling and its merlons <paramref name="merlon"/> higher in dressed stone, each with its coping, a crenel between each and the next. A merlon is
        /// left out wherever <paramref name="skip"/> says of how far along it stands.
        /// </summary>
        public static void Battlements(MeshBuilder b, Vector3 from, Vector3 to, float thick, float parapet, float merlon, Vector3 stone, WorldRandom random, float period = 2.4f, Func<float, bool>? skip = null)
        {
            Vector3 along = to - from;
            float length = along.Length;
            if (length < 0.6f) return;
            along /= length;
            Vector3 across = Vector3.Cross(Vector3.UnitY, along).Normalized();
            VertexMaterial walling = Walling(stone, random, 0.98f);
            Kit.Box(b, (from + to) * 0.5f + Vector3.UnitY * (parapet * 0.5f), along, Vector3.UnitY, across, new Vector3(length * 0.5f, parapet * 0.5f, thick * 0.5f), 0f, walling);
            int count = Math.Max(1, (int)MathF.Round(length / period));
            float bay = length / count;
            for (int i = 0; i < count; i++)
            {
                float at = (i + 0.5f) * bay;
                if (skip != null && skip(at)) continue;
                Vector3 foot = from + along * at + Vector3.UnitY * parapet;
                float half = bay * 0.29f;
                Kit.Box(b, foot + Vector3.UnitY * (merlon * 0.5f), along, Vector3.UnitY, across, new Vector3(half, merlon * 0.5f, thick * 0.5f), 0.05f, AshlarFacing(stone, random, 6f));
                Kit.Box(b, foot + Vector3.UnitY * (merlon + 0.1f), along, Vector3.UnitY, across, new Vector3(half + 0.09f, 0.1f, thick * 0.5f + 0.09f), 0.04f, AshlarFacing(stone, random, 8f, 1.06f));
            }
        }

        /// <summary>A row of corbels under an overhang: small dressed blocks along a line, each standing <paramref name="reach"/> out of the wall towards <paramref name="outward"/>.</summary>
        public static void Corbels(MeshBuilder b, Vector3 from, Vector3 to, Vector3 outward, float reach, Vector3 stone, WorldRandom random, float spacing = 1.2f)
        {
            Vector3 along = to - from;
            float length = along.Length;
            if (length < spacing) return;
            along /= length;
            int count = Math.Max(1, (int)(length / spacing));
            VertexMaterial ashlar = Ashlar(stone, random, 6f, 0.92f);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = from + along * ((i + 0.5f) * length / count);
                Kit.Box(b, at + outward * (reach * 0.5f) - Vector3.UnitY * 0.28f, along, Vector3.UnitY, outward, new Vector3(0.2f, 0.28f, reach * 0.5f), 0f, ashlar);
            }
        }

        /// <summary>A course of dressed blocks along a line, each no longer than a stone is: a coping, a string course, a cornice. <paramref name="half"/> is half its height and half its depth.</summary>
        public static void Course(MeshBuilder b, Vector3 from, Vector3 to, Vector2 half, Vector3 stone, WorldRandom random, float shade = 1f, float piece = 1.5f)
        {
            Vector3 along = to - from;
            float length = along.Length;
            if (length < 0.2f) return;
            along /= length;
            Vector3 across = MathF.Abs(along.Y) < 0.9f ? Vector3.Cross(Vector3.UnitY, along).Normalized() : Vector3.UnitZ;
            Vector3 up = Vector3.Cross(along, across).Normalized();
            int count = Math.Max(1, (int)MathF.Round(length / piece));
            float each = length / count;
            for (int i = 0; i < count; i++)
            {
                Vector3 centre = from + along * ((i + 0.5f) * each);
                Kit.Box(b, centre, along, up, across, new Vector3(each * 0.5f - 0.008f, half.X, half.Y), 0.03f, AshlarFacing(stone, random, centre.Y, shade));
            }
        }

        // ---------------------------------------------------------------- Round towers

        /// <summary>
        /// A round tower's drum, from below the ground at <paramref name="foot"/> to its top <paramref name="height"/> above it: battered out at its foot, a string course round it, slits up
        /// it, and its head corbelled out a little. Returns the radius of its head, which is what stands on it.
        /// </summary>
        public static float Drum(MeshBuilder b, Vector3 foot, float radius, float height, Vector3 stone, WorldRandom random, int around = 16, float batter = 0.8f, bool slits = true)
        {
            VertexMaterial walling = Walling(stone, random);
            float total = height + 0.8f;
            float head = radius + 0.4f;
            Kit.Lathe(b, foot - Vector3.UnitY * 0.8f, Vector3.UnitY, total, t =>
            {
                float y = t * total - 0.8f;
                if (y > height - 1.6f) return radius + 0.4f * Math.Clamp((y - (height - 1.6f)) / 0.5f, 0f, 1f);
                return radius + batter * MathF.Pow(Math.Clamp(1f - y / (height * 0.22f), 0f, 1f), 1.6f);
            }, around, 10, walling, openEnd: true);
            // The string course under its head, and one a third of the way up.
            foreach (float y in new[] { height - 1.75f, height * 0.36f })
            {
                b.Tube(foot + Vector3.UnitY * (y - 0.14f), foot + Vector3.UnitY * (y + 0.14f), radius + 0.11f, radius + 0.11f, around, Ashlar(stone, random, y, 1.04f), capped: false);
            }
            if (slits)
            {
                VertexMaterial dark = Dark;
                for (int k = 0; k < 5; k++)
                {
                    float a = k * MathF.Tau / 5f + random.Range(-0.2f, 0.2f);
                    float y = height * (k % 2 == 0 ? 0.52f : 0.68f);
                    Vector3 outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), tangent = new Vector3(-MathF.Sin(a), 0f, MathF.Cos(a));
                    Vector3 c = foot + outward * (radius + 0.03f) + Vector3.UnitY * y;
                    Kit.Face(b, c - tangent * 0.13f - Vector3.UnitY * 0.8f, c + tangent * 0.13f - Vector3.UnitY * 0.8f, c + tangent * 0.13f + Vector3.UnitY * 0.8f, c - tangent * 0.13f + Vector3.UnitY * 0.8f, outward, dark);
                }
            }
            return head;
        }

        /// <summary>The paved top of a drum and the merlons round it.</summary>
        public static void DrumHead(MeshBuilder b, Vector3 top, float head, Vector3 stone, WorldRandom random, int around = 16)
        {
            // Its floor, a little over the drum's own end and falling from its middle, as a roof that has to shed rain does. A model is drawn from both sides, so nothing
            // here may lie over it: the parapet's top is a ring of its own, not a lathe's end, which is closed with a disc.
            Kit.Lathe(b, top + Vector3.UnitY * 0.05f, Vector3.UnitY, 0.05f, Kit.Profile((0f, head - 0.3f), (1f, 0f)), around, 1, Paving(stone, 0.95f));
            // The parapet, a ring of walling, and the merlons on it.
            VertexMaterial walling = Walling(stone, random, 0.98f), within = Tint(walling, new Vector3(0.8f));
            Kit.Lathe(b, top - Vector3.UnitY * 0.2f, Vector3.UnitY, 1.3f, _ => head, around, 1, walling, openEnd: true);
            for (int k = 0; k < around; k++)
            {
                float a0 = k * MathF.Tau / around, a1 = (k + 1) * MathF.Tau / around;
                Vector3 d0 = new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)), d1 = new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1));
                Vector3 rim = top + Vector3.UnitY * 1.1f;
                Kit.Face(b, rim + d0 * head, rim + d1 * head, rim + d1 * (head - 0.55f), rim + d0 * (head - 0.55f), Vector3.UnitY, walling);
                Kit.Face(b, top + d0 * (head - 0.55f), top + d1 * (head - 0.55f), rim + d1 * (head - 0.55f), rim + d0 * (head - 0.55f), -(d0 + d1), within);
            }
            int count = Math.Max(6, (int)MathF.Round(MathF.Tau * head / 2.5f));
            for (int i = 0; i < count; i++)
            {
                float a = (i + 0.5f) * MathF.Tau / count;
                Vector3 outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)), tangent = new Vector3(-MathF.Sin(a), 0f, MathF.Cos(a));
                Vector3 foot = top + outward * (head - 0.28f) + Vector3.UnitY * 1.1f;
                float half = MathF.Tau * head / count * 0.29f;
                Kit.Box(b, foot + Vector3.UnitY * 0.8f, tangent, Vector3.UnitY, outward, new Vector3(half, 0.8f, 0.3f), 0.05f, AshlarFacing(stone, random, 7f));
                Kit.Box(b, foot + Vector3.UnitY * 1.7f, tangent, Vector3.UnitY, outward, new Vector3(half + 0.09f, 0.1f, 0.39f), 0.04f, AshlarFacing(stone, random, 9f, 1.06f));
            }
        }

        /// <summary>A cone of a roof on a round tower, its eaves out past the wall, a finial of lead on its point.</summary>
        public static void Cone(MeshBuilder b, Vector3 eaves, float radius, float height, VertexMaterial roof, WorldRandom random, int around = 12)
        {
            Kit.Lathe(b, eaves, Vector3.UnitY, height, Kit.Profile((0f, radius), (0.12f, radius * 0.84f), (1f, 0.06f)), around, 5, roof);
            VertexMaterial lead = Lead(random);
            Kit.Lathe(b, eaves + Vector3.UnitY * (height - 0.25f), Vector3.UnitY, 1.3f, Kit.Profile((0f, 0.2f), (0.2f, 0.26f), (0.4f, 0.1f), (1f, 0.02f)), 6, 4, lead);
        }

        // ---------------------------------------------------------------- Fire

        /// <summary>
        /// A beacon: an iron basket of logs burning on a stem, big enough to be seen across a land. Its foot is at <paramref name="at"/>, and its light reaches <paramref name="radius"/>.
        /// </summary>
        public static void Beacon(MeshBuilder b, Vector3 at, float size, WorldRandom random, List<AssetLight> lights, float intensity = 130f, float radius = 24f)
        {
            VertexMaterial iron = Iron(random);
            Vector3 bowl = at + Vector3.UnitY * (1.3f * size);
            Kit.Pole(b, at, bowl - Vector3.UnitY * (0.3f * size), 0.11f * size, iron);
            for (int i = 0; i < 3; i++)
            {
                float a = i * MathF.Tau / 3f + 0.4f;
                Kit.Pole(b, at + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (0.55f * size), at + Vector3.UnitY * (0.75f * size), 0.05f * size, iron);
            }
            Kit.Lathe(b, bowl - Vector3.UnitY * (0.35f * size), Vector3.UnitY, 0.6f * size, Kit.Profile((0f, 0.2f * size), (0.35f, 0.62f * size), (1f, 0.8f * size)), 10, 3, iron, openEnd: true);
            Kit.Heap(b, bowl + Vector3.UnitY * (0.05f * size), new Vector2(0.7f * size), 0.16f * size, 0f, Coals, random, 2, 8);
            VertexMaterial charred = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.16f, 0.13f, 0.11f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * MathF.PI * 0.5f + random.Range(-0.3f, 0.3f);
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Kit.Pole(b, bowl + d * (0.65f * size) + Vector3.UnitY * (0.1f * size), bowl - d * (0.2f * size) + Vector3.UnitY * (0.62f * size), 0.09f * size, charred, 0.06f * size);
            }
            lights.Add(AssetLight.Burning(bowl + Vector3.UnitY * (0.12f * size), 1.05f * size));
            lights.Add(AssetLight.Campfire(bowl + Vector3.UnitY * (0.9f * size)) with { Intensity = intensity, Radius = radius });
        }

        /// <summary>A torch on an iron bracket out of a wall, burning: <paramref name="at"/> is where its bracket leaves the wall, and <paramref name="outward"/> the way it stands off it.</summary>
        public static void WallTorch(MeshBuilder b, Vector3 at, Vector3 outward, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial iron = Iron(random);
            Vector3 head = at + outward * 0.55f + Vector3.UnitY * 0.5f;
            Kit.Pole(b, at, at + outward * 0.55f, 0.04f, iron);
            Kit.Pole(b, at + outward * 0.55f - Vector3.UnitY * 0.1f, head, 0.05f, iron);
            Kit.Lathe(b, head, Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.07f), (1f, 0.18f)), 6, 1, iron, openEnd: true);
            lights.Add(AssetLight.Burning(head + Vector3.UnitY * 0.25f, 0.4f));
            lights.Add(AssetLight.Lantern(head + Vector3.UnitY * 0.6f + outward * 0.3f) with { Intensity = 75f, Radius = 15f, Flicker = 0.24f, FlickerSpeed = 9f });
        }

        // ---------------------------------------------------------------- Cloth

        /// <summary>
        /// One of the Reich's long banners hung from a bar: oxblood, swallow-tailed, its mark in brass on the face towards <paramref name="facing"/> and on its back, hanging from
        /// <paramref name="top"/> (the middle of its bar), <paramref name="width"/> across and <paramref name="drop"/> long. It swings from its bar in the wind, all of it as one
        /// (<see cref="MeshBuilder.Hang"/>).
        /// </summary>
        public static void Banner(MeshBuilder b, Vector3 top, Vector3 facing, float width, float drop, WorldRandom random, float markSize = -1f)
        {
            int first = b.VertexCount;
            Vector3 n = facing.Normalized();
            Vector3 right = Vector3.Cross(Vector3.UnitY, n).Normalized();
            // Deeper than the Reich's cloth is elsewhere: a banner hangs face on to the sun, and oxblood in the sun goes scarlet.
            VertexMaterial cloth = Oxblood(random.Range(0.66f, 0.76f));
            float hw = width * 0.5f;
            float phase = random.Float() * 6f;
            Vector3 P(float x, float y) => top + right * x - Vector3.UnitY * y + n * (0.05f * MathF.Sin(y * 1.3f + x * 2.1f + phase) * MathF.Min(y / 1.5f, 1f));
            const int Rows = 7;
            float body = drop * 0.86f;
            for (int j = 0; j < Rows; j++)
            {
                float y0 = body * j / Rows, y1 = body * (j + 1f) / Rows;
                for (int i = 0; i < 2; i++)
                {
                    float x0 = -hw + i * hw, x1 = x0 + hw;
                    Kit.Sheet(b, P(x0, y1), P(x1, y1), P(x1, y0), P(x0, y0), cloth, cloth);
                }
            }
            // Its two tails.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 a = P(s * hw, body), c = P(0f, body), tip = P(s * hw, drop);
                Kit.Face(b, a, c, tip, n, cloth);
                Kit.Face(b, a, c, tip, -n, cloth);
            }
            float size = markSize > 0f ? markSize : width * 0.78f;
            VertexMaterial brass = Brass(random);
            Arena.ArenaStone.Mark(b, top - Vector3.UnitY * (body * 0.42f) + n * 0.06f, right, Vector3.UnitY, size, 0.015f, brass, cloth);
            Arena.ArenaStone.Mark(b, top - Vector3.UnitY * (body * 0.42f) - n * 0.06f, -right, Vector3.UnitY, size, 0.015f, brass, cloth);
            // A brass band along its foot, above the tails.
            Kit.Sheet(b, P(-hw, body - 0.1f) + n * 0.02f, P(hw, body - 0.1f) + n * 0.02f, P(hw, body - 0.32f) + n * 0.02f, P(-hw, body - 0.32f) + n * 0.02f, brass, brass);
            b.Hang(first, top, top - Vector3.UnitY * drop, 0.42f, 0.9f, 0.45f);
        }
    }
}
