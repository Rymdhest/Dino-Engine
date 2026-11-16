using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Halls.Basilica;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The Archive's displays, one for each record a player's heroes keep, each one thing that says what it keeps at a glance from the camera, on a pedestal of the hall's own
    /// marble with a plate at its foot: a tyrant's skull for the tyrants felled, a boulder split by a blade for the hardest blow, an hourglass for the swiftest land. Each is
    /// built facing +z, towards whoever reads it. Those against the end wall are seen from in front, so they stand up against a back; those against the side walls are seen
    /// from beside and above, so they're round, or lie open to the sky. What the record is comes from the game when someone walks up (UI/ArchivePlaques.cs).
    /// Each has one soft light, set well out in front of it and over it: a light within a metre and a half of marble washes it white.
    /// </summary>
    internal static class Exhibits
    {
        private static VertexMaterial Bone => Hall.Bone(210, 194, 160);
        private static VertexMaterial OldBone => Hall.Bone(172, 154, 122);
        private static VertexMaterial Hollow => Hall.Flat(30, 26, 24);
        private static VertexMaterial Velvet => Hall.Cloth(22, 56, 78);
        private static VertexMaterial Steel => Hall.Metal(198, 200, 208);
        private static VertexMaterial Gold => Hall.Metal(222, 176, 76);
        private static VertexMaterial Parchment => Hall.Flat(206, 190, 150);

        public static void Build(MeshBuilder b, ExhibitKind kind, Vector2 half, float h, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            switch (kind)
            {
                case ExhibitKind.TyrantsFelled: TyrantSkull(b, half, h, lights); break;
                case ExhibitKind.FoesSlain: Trophies(b, half, h, lights, random); break;
                case ExhibitKind.HardestBlow: SplitStone(b, half, h, lights, random); break;
                case ExhibitKind.UniquesFound: Treasury(b, half, h, lights); break;
                case ExhibitKind.SwiftestLand: Hourglass(b, half, h, lights); break;
                case ExhibitKind.Falls: Memorial(b, half, h, lights, random); break;
                case ExhibitKind.LandsFreed: MapTable(b, half, h, lights, random); break;
                case ExhibitKind.GoldGathered: Hoard(b, half, h, lights, random); break;
                default: Climb(b, half, h, lights); break;
            }
        }

        // ---- What they share.

        /// <summary>A pedestal of the hall's marble: a foot of teal, a line of gold, a die of white and a cap. Its top is at <paramref name="height"/>.</summary>
        private static void Pedestal(MeshBuilder b, Vector2 half, float height, Vector3 at = default)
        {
            float foot = MathF.Min(0.3f, height * 0.3f), cap = MathF.Min(0.16f, height * 0.2f);
            b.Box(at + new Vector3(0f, foot * 0.5f, 0f), new Vector3(half.X, foot * 0.5f, half.Y), Teal);
            b.Box(at + new Vector3(0f, foot + 0.03f, 0f), new Vector3(half.X - 0.06f, 0.03f, half.Y - 0.06f), Gilt);
            float dieFrom = foot + 0.06f, dieTo = height - cap;
            b.Box(at + new Vector3(0f, (dieFrom + dieTo) * 0.5f, 0f), new Vector3(half.X - 0.14f, (dieTo - dieFrom) * 0.5f, half.Y - 0.14f), White);
            b.Box(at + new Vector3(0f, height - cap * 0.5f, 0f), new Vector3(half.X - 0.03f, cap * 0.5f, half.Y - 0.03f), White);
        }

        /// <summary>The same, round.</summary>
        private static void RoundPedestal(MeshBuilder b, float radius, float height)
        {
            float foot = MathF.Min(0.3f, height * 0.3f), cap = MathF.Min(0.16f, height * 0.2f);
            b.Tube(Vector3.Zero, Vector3.UnitY * foot, radius, radius, 24, Teal);
            b.Tube(Vector3.UnitY * foot, Vector3.UnitY * (foot + 0.06f), radius - 0.06f, radius - 0.06f, 24, Gilt);
            b.Tube(Vector3.UnitY * (foot + 0.06f), Vector3.UnitY * (height - cap), radius - 0.14f, radius - 0.14f, 24, White, capped: false);
            b.Tube(Vector3.UnitY * (height - cap), Vector3.UnitY * height, radius - 0.03f, radius - 0.03f, 24, White);
        }

        /// <summary>The plate at a display's foot, where its record would be cut: a slanted tablet of teal in a frame of gold, three lines on it. <paramref name="foot"/> is the middle of its lower edge.</summary>
        private static void Plate(MeshBuilder b, Vector3 foot, float halfWide = 0.62f)
        {
            Vector3 rise = new Vector3(0f, 0.42f, -0.3f);
            Vector3 normal = Vector3.Cross(Vector3.UnitX, rise).Normalized();
            if (normal.Y < 0f) normal = -normal;
            Hall.Board(b, foot - normal * 0.03f, foot + rise - normal * 0.03f, Vector3.UnitX, halfWide + 0.06f, 0.03f, Gilt);
            Hall.Board(b, foot + rise * 0.1f + normal * 0.012f, foot + rise * 0.9f + normal * 0.012f, Vector3.UnitX, halfWide, 0.014f, DeepTeal);
            for (int line = 0; line < 3; line++)
            {
                Vector3 at = foot + rise * (0.74f - line * 0.24f) + normal * 0.03f;
                Hall.Board(b, at - Vector3.UnitX * (halfWide * (0.72f - line * 0.14f)), at + Vector3.UnitX * (halfWide * (0.72f - line * 0.14f)), rise, 0.026f, 0.005f, PaleGilt);
            }
        }

        // Its one light.
        private static void Lit(List<AssetLight> lights, Vector3 at, float intensity = 20f, float radius = 10f)
        {
            lights.Add(AssetLight.Glow(at, new Vector3(1f, 0.84f, 0.62f), intensity, radius));
        }

        /// <summary>A block any way up: its eight corners put through <paramref name="m"/>, each face wound to face out. For what's built tilted, which a box can't be.</summary>
        private static void Solid(MeshBuilder b, Func<Vector3, Vector3> m, Vector3 centre, Vector3 half, VertexMaterial material)
        {
            Vector3 C(float x, float y, float z) => m(centre + new Vector3(x * half.X, y * half.Y, z * half.Z));
            Vector3 middle = m(centre);
            void Side(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) => Hall.Face(b, p0, p1, p2, p3, (p0 + p2) * 0.5f - middle, material);
            Side(C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1));
            Side(C(1, -1, -1), C(-1, -1, -1), C(-1, 1, -1), C(1, 1, -1));
            Side(C(1, -1, 1), C(1, -1, -1), C(1, 1, -1), C(1, 1, 1));
            Side(C(-1, -1, -1), C(-1, -1, 1), C(-1, 1, 1), C(-1, 1, -1));
            Side(C(-1, 1, 1), C(1, 1, 1), C(1, 1, -1), C(-1, 1, -1));
            Side(C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1));
        }

        // ---- Tyrants felled.

        /// <summary>
        /// The end of the nave: a dais of three steps against the wall, a pedestal on it, and on that a great tray of dark velvet in a frame of walnut and gold, tilted up
        /// towards whoever stands before it, with the skull of a tyrant lying on it on its side, half as long again as a person is tall, its jaws open. On its side, as the
        /// camera looks down from behind whoever reads it: so it sees the skull as everyone knows one, from beside, the eye, the holes before and behind it, and every tooth.
        /// </summary>
        private static void TyrantSkull(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            const float Step = 0.22f, Tread = 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float inset = Tread * i, top = Step * (i + 1);
                float front = half.Y - inset, back = -half.Y;
                // Its rise in teal and its tread in white, so that the steps are seen from above.
                b.Box(new Vector3(0f, top - Step * 0.5f - 0.02f, (front + back) * 0.5f), new Vector3(half.X - inset, Step * 0.5f - 0.02f, (front - back) * 0.5f), Teal);
                b.Box(new Vector3(0f, top - 0.02f, (front + back) * 0.5f), new Vector3(half.X - inset + 0.02f, 0.02f, (front - back) * 0.5f + 0.01f), White);
                // A line of gold along the nose of each step, front and sides.
                float lineY = top + 0.004f;
                b.Box(new Vector3(0f, lineY, front - 0.07f), new Vector3(half.X - inset, 0.004f, 0.03f), Gilt);
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * (half.X - inset - 0.07f), lineY, (front + back) * 0.5f), new Vector3(0.03f, 0.004f, (front - back) * 0.5f), Gilt);
            }
            float floor = Step * 3f;
            // A panel of teal let into the top of the dais, before the pedestal.
            b.Box(new Vector3(0f, floor + 0.004f, 1.3f), new Vector3(2.2f, 0.004f, 0.45f), Teal);
            b.Box(new Vector3(0f, floor + 0.008f, 1.3f), new Vector3(2.08f, 0.004f, 0.33f), DeepTeal);

            Vector3 stand = new Vector3(0f, floor, -0.75f);
            const float PedestalTop = 1.7f;
            Pedestal(b, new Vector2(3.2f, 1.3f), PedestalTop, stand);
            Plate(b, stand + new Vector3(0f, 0.5f, 1.34f), 0.9f);

            // The tray: its foot on the front of the pedestal and its head held up behind on two posts of gold.
            const float Tilt = 0.84f, Scale = 1.5f;
            float cos = MathF.Cos(Tilt), sin = MathF.Sin(Tilt);
            Vector3 up = new Vector3(0f, cos, -sin);            // Up the tray, towards its head.
            Vector3 face = new Vector3(0f, sin, cos);           // Out of it, towards the camera.
            Vector3 foot = new Vector3(0f, floor + PedestalTop + 0.14f, 0.5f);
            const float Long = 4.4f, Wide = 3.35f;
            Vector3 head = foot + up * Long;
            Hall.Board(b, foot - face * 0.08f, head - face * 0.08f, Vector3.UnitX, Wide, 0.08f, Walnut);
            Hall.Board(b, foot + up * 0.16f + face * 0.01f, head - up * 0.16f + face * 0.01f, Vector3.UnitX, Wide - 0.16f, 0.012f, Velvet);
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, foot + Vector3.UnitX * (s * (Wide - 0.07f)) + face * 0.035f, head + Vector3.UnitX * (s * (Wide - 0.07f)) + face * 0.035f, Vector3.UnitX, 0.07f, 0.035f, Gilt);
            Hall.Board(b, foot - Vector3.UnitX * Wide + up * 0.07f + face * 0.035f, foot + Vector3.UnitX * Wide + up * 0.07f + face * 0.035f, up, 0.07f, 0.035f, Gilt);
            Hall.Board(b, head - Vector3.UnitX * Wide - up * 0.07f + face * 0.035f, head + Vector3.UnitX * Wide - up * 0.07f + face * 0.035f, up, 0.07f, 0.035f, Gilt);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 under = foot + up * (Long * 0.72f) - face * 0.16f + Vector3.UnitX * (s * 2.3f);
                Vector3 on = new Vector3(under.X, floor + PedestalTop, stand.Z - 0.55f);
                b.Tube(on, on + Vector3.UnitY * 0.12f, 0.26f, 0.2f, 10, Gilt);
                b.Tube(on + Vector3.UnitY * 0.12f, under, 0.09f, 0.07f, 8, Gilt, capped: false);
            }

            // The skull, lying on it: its snout along the tray to the right, the roof of it towards the tray's head and its teeth towards its foot.
            Vector3 hold = foot + up * 2.5f + face * (0.82f * Scale) - Vector3.UnitX * 0.3f;
            Vector3 M(Vector3 p)
            {
                p *= Scale;
                // Snout, +z, to the right; then laid back, so that its near side faces out of the tray.
                Vector3 turned = new Vector3(p.Z, p.Y, -p.X);
                return hold + new Vector3(turned.X, turned.Y * cos + turned.Z * sin, -turned.Y * sin + turned.Z * cos);
            }
            Skull(b, M, Scale);
            Lit(lights, new Vector3(0f, 7.6f, half.Y + 1.6f), 46f, 14f);
        }

        // An eight-sided section of the skull: its sides, roof and floor, with the corners between taken off.
        private static Vector3[] Section(float z, float w, float top, float bottom)
        {
            float c = MathF.Min(w, top - bottom) * 0.3f;
            return new[]
            {
                new Vector3(-w, bottom + c, z), new Vector3(-w, top - c, z), new Vector3(-w + c, top, z), new Vector3(w - c, top, z),
                new Vector3(w, top - c, z), new Vector3(w, bottom + c, z), new Vector3(w - c, bottom, z), new Vector3(-w + c, bottom, z),
            };
        }

        // A tyrant's skull, its snout along +z and its roof up, a little under four metres long before it's scaled: tall and wide behind the eyes, a deep snout, blunt
        // at its end, the holes in its sides dark, a jaw hung open under it, and its teeth.
        private static void Skull(MeshBuilder b, Func<Vector3, Vector3> m, float scale)
        {
            VertexMaterial bone = Bone, old = OldBone, hollow = Hollow;
            (float z, float w, float top, float bottom)[] sections =
            {
                (-1.78f, 0.5f, 0.74f, -0.1f), (-1.5f, 0.78f, 1.02f, -0.3f), (-1.05f, 0.84f, 1.08f, -0.4f), (-0.6f, 0.7f, 1f, -0.42f), (-0.15f, 0.56f, 0.84f, -0.42f),
                (0.45f, 0.5f, 0.7f, -0.4f), (1.05f, 0.44f, 0.6f, -0.38f), (1.55f, 0.37f, 0.5f, -0.36f), (1.95f, 0.29f, 0.38f, -0.33f), (2.18f, 0.18f, 0.2f, -0.28f),
            };
            // How wide it is at a point along it, for what's set into its sides.
            float WidthAt(float z)
            {
                for (int i = 0; i + 1 < sections.Length; i++)
                {
                    if (z <= sections[i + 1].z) return MathHelper.Lerp(sections[i].w, sections[i + 1].w, Math.Clamp((z - sections[i].z) / (sections[i + 1].z - sections[i].z), 0f, 1f));
                }
                return sections[^1].w;
            }
            Vector3[] last = Section(sections[0].z, sections[0].w, sections[0].top, sections[0].bottom);
            Vector3 lastMiddle = new Vector3(0f, (sections[0].top + sections[0].bottom) * 0.5f, sections[0].z);
            // Its back, closed.
            for (int k = 0; k < 8; k++) Hall.Tri(b, m(lastMiddle), m(last[k]), m(last[(k + 1) % 8]), m(lastMiddle - Vector3.UnitZ) - m(lastMiddle), old);
            for (int i = 1; i < sections.Length; i++)
            {
                var s = sections[i];
                Vector3[] next = Section(s.z, s.w, s.top, s.bottom);
                Vector3 middle = new Vector3(0f, (s.top + s.bottom) * 0.5f, s.z);
                Vector3 axis = m((lastMiddle + middle) * 0.5f);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p0 = m(last[k]), p1 = m(last[(k + 1) % 8]), p2 = m(next[(k + 1) % 8]), p3 = m(next[k]);
                    // The roof and the cheeks a little paler than what's between them and under.
                    Hall.Face(b, p0, p1, p2, p3, (p0 + p2) * 0.5f - axis, k == 2 || k == 0 || k == 4 ? bone : old);
                }
                last = next;
                lastMiddle = middle;
            }
            for (int k = 0; k < 8; k++) Hall.Tri(b, m(lastMiddle), m(last[k]), m(last[(k + 1) % 8]), m(lastMiddle + Vector3.UnitZ) - m(lastMiddle), bone);

            foreach (float s in new[] { -1f, 1f })
            {
                // In each side: the eye, a tall hole narrow at its foot; the hole behind it; the long one before it; and the nostril.
                // Each an oval on the skull's side, standing just clear of it where it's widest.
                void Hole(float z, float y, float halfLong, float halfTall)
                {
                    float x = s * (MathF.Max(WidthAt(z - halfLong), MathF.Max(WidthAt(z), WidthAt(z + halfLong))) + 0.012f);
                    Vector3 centre = new Vector3(x, y, z);
                    Vector3 outward = m(centre + Vector3.UnitX * s) - m(centre);
                    const int Round = 12;
                    for (int k = 0; k < Round; k++)
                    {
                        float a0 = MathF.Tau * k / Round, a1 = MathF.Tau * (k + 1) / Round;
                        Hall.Tri(b, m(centre), m(centre + new Vector3(0f, MathF.Sin(a0) * halfTall, MathF.Cos(a0) * halfLong)), m(centre + new Vector3(0f, MathF.Sin(a1) * halfTall, MathF.Cos(a1) * halfLong)), outward, hollow);
                    }
                }
                Hole(-0.72f, 0.62f, 0.2f, 0.24f);
                Hole(-0.72f, 0.24f, 0.11f, 0.16f);
                Hole(-1.3f, 0.34f, 0.17f, 0.34f);
                Hole(0.16f, 0.26f, 0.38f, 0.26f);
                Hole(0.72f, 0.16f, 0.16f, 0.14f);
                Hole(1.82f, 0.12f, 0.13f, 0.08f);
                // The two great holes in its roof, behind the eyes.
                Solid(b, m, new Vector3(s * 0.38f, 1.07f, -1.2f), new Vector3(0.2f, 0.02f, 0.24f), hollow);
                // The brow over each eye, standing out.
                Solid(b, m, new Vector3(s * 0.74f, 1f, -0.7f), new Vector3(0.14f, 0.1f, 0.28f), bone);
            }
            // The rough ridge down the snout.
            for (int i = 0; i < 5; i++)
            {
                float t0 = i / 5f, t1 = (i + 0.8f) / 5f;
                Vector3 from = new Vector3(0f, MathHelper.Lerp(0.9f, 0.42f, t0), MathHelper.Lerp(-0.25f, 1.8f, t0)), to = new Vector3(0f, MathHelper.Lerp(0.9f, 0.42f, t1), MathHelper.Lerp(-0.25f, 1.8f, t1));
                Hall.Board(b, m(from), m(to), m(from + Vector3.UnitX) - m(from), 0.11f * scale, 0.035f * scale, old);
            }

            // Upper teeth, long and curved back, along the edge of the upper jaw, the longest of them under the eye's front.
            VertexMaterial tooth = Hall.Bone(238, 230, 208);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                float z = MathHelper.Lerp(-0.3f, 2.06f, t);
                float w = WidthAt(z) - 0.05f;
                float y = MathHelper.Lerp(-0.42f, -0.31f, t);
                float length = 0.36f + 0.2f * MathF.Sin(t * MathF.PI) + 0.07f * MathF.Sin(i * 2.3f + 0.4f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 root = new Vector3(s * w, y, z);
                    b.Tube(m(root), m(root + new Vector3(0f, -length, -0.09f)), 0.07f * scale, 0.006f * scale, 6, tooth);
                }
            }

            // The lower jaw, hung from under the back of the head and dropped open: two deep bars meeting at the chin, with their own teeth.
            float open = MathHelper.DegreesToRadians(19f);
            float jc = MathF.Cos(open), js = MathF.Sin(open);
            Vector3 hinge = new Vector3(0f, -0.3f, -1.3f);
            Vector3 J(Vector3 p) => m(hinge + new Vector3(p.X, p.Y * jc - p.Z * js, p.Y * js + p.Z * jc));
            (float z, float w, float deep)[] jaw = { (-0.1f, 0.76f, 0.36f), (0.9f, 0.62f, 0.32f), (1.8f, 0.48f, 0.25f), (2.7f, 0.32f, 0.2f), (3.25f, 0.14f, 0.16f) };
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i + 1 < jaw.Length; i++)
                {
                    var a = jaw[i];
                    var c = jaw[i + 1];
                    Vector3 from = new Vector3(s * a.w, -0.2f, a.z), to = new Vector3(s * c.w, -0.22f, c.z);
                    Hall.Board(b, J(from), J(to), J(from + Vector3.UnitY) - J(from), (a.deep + c.deep) * 0.5f * scale, 0.08f * scale, i % 2 == 0 ? old : bone);
                }
                for (int i = 0; i < 9; i++)
                {
                    float t = i / 8f;
                    float z = MathHelper.Lerp(0.8f, 3.1f, t);
                    float w = MathHelper.Lerp(0.62f, 0.17f, t);
                    Vector3 root = new Vector3(s * w, 0.04f, z);
                    b.Tube(J(root), J(root + new Vector3(0f, 0.3f + 0.08f * MathF.Sin(i * 1.7f), -0.05f)), 0.06f * scale, 0.006f * scale, 6, tooth);
                }
            }
            Solid(b, J, new Vector3(0f, -0.22f, 3.25f), new Vector3(0.16f, 0.16f, 0.1f), old);
        }
        // ---- Foes slain.

        /// <summary>
        /// What was taken off the fallen, kept as a collector keeps things: a tall board of dark velvet in a frame of walnut and gold, and on it, each on its own pin, claws
        /// and teeth in rows, the smallest at the foot and the greatest at the head, with a tyrant's two great claws crossed over them all.
        /// </summary>
        private static void Trophies(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            const float Foot = 1.1f;
            Pedestal(b, half, Foot);
            Plate(b, new Vector3(0f, Foot, half.Y - 0.55f));
            float back = -half.Y + 0.32f;
            float top = h - 0.5f;
            float boardHalf = half.X - 0.3f;
            b.Box(new Vector3(0f, (Foot + top) * 0.5f, back), new Vector3(boardHalf, (top - Foot) * 0.5f, 0.14f), Walnut);
            b.Box(new Vector3(0f, (Foot + top) * 0.5f, back + 0.15f), new Vector3(boardHalf - 0.18f, (top - Foot) * 0.5f - 0.18f, 0.012f), Velvet);
            // A line of gold round the velvet, and a cornice over it all.
            float face = back + 0.17f;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * (boardHalf - 0.2f), (Foot + top) * 0.5f, face), new Vector3(0.03f, (top - Foot) * 0.5f - 0.17f, 0.012f), Gilt);
                b.Box(new Vector3(0f, (Foot + top) * 0.5f + s * ((top - Foot) * 0.5f - 0.2f), face), new Vector3(boardHalf - 0.2f, 0.03f, 0.012f), Gilt);
            }
            b.Box(new Vector3(0f, top + 0.1f, back + 0.04f), new Vector3(boardHalf + 0.12f, 0.1f, 0.22f), Walnut);
            b.Box(new Vector3(0f, top + 0.24f, back + 0.04f), new Vector3(boardHalf + 0.18f, 0.04f, 0.27f), Gilt);
            Hall.Tri(b, new Vector3(-boardHalf * 0.7f, top + 0.28f, back + 0.2f), new Vector3(boardHalf * 0.7f, top + 0.28f, back + 0.2f), new Vector3(0f, top + 0.62f, back + 0.2f), Vector3.UnitZ, Walnut);
            b.Tube(new Vector3(0f, top + 0.38f, back + 0.2f), new Vector3(0f, top + 0.38f, back + 0.26f), 0.1f, 0.08f, 8, Gilt);

            // The rows.
            const int Rows = 5;
            float fieldFrom = Foot + 0.5f, fieldTo = top - 1.5f;
            for (int row = 0; row < Rows; row++)
            {
                float y = MathHelper.Lerp(fieldFrom, fieldTo, row / (float)(Rows - 1)) + 0.25f;
                int count = 8 - row;
                float size = 0.2f + row * 0.075f;
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * 0.5f) * (boardHalf * 1.62f / count);
                    Vector3 pin = new Vector3(x, y, face + 0.02f);
                    b.Box(pin, new Vector3(0.035f, 0.035f, 0.02f), Gilt);
                    float lean = random.Range(-0.12f, 0.12f);
                    if ((i + row) % 3 == 0)
                    {
                        // A tooth: straight, to a point.
                        b.Tube(pin + Vector3.UnitZ * 0.03f, pin + new Vector3(lean * size, -size * 1.25f, 0.05f), size * 0.2f, 0.006f, 5, OldBone);
                    }
                    else
                    {
                        // A claw: curved, hanging point down.
                        Vector3 knee = pin + new Vector3(lean * size + size * 0.16f, -size * 0.75f, 0.06f);
                        b.Tube(pin + Vector3.UnitZ * 0.03f, knee, size * 0.24f, size * 0.16f, 6, Bone, capped: false);
                        b.Tube(knee, pin + new Vector3(lean * size - size * 0.12f, -size * 1.35f, 0.05f), size * 0.16f, 0.006f, 6, Bone);
                    }
                }
            }
            // A tyrant's two claws, crossed, at its head.
            float crownY = top - 0.75f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 root = new Vector3(s * 0.62f, crownY - 0.36f, face + 0.08f), knee = new Vector3(-s * 0.05f, crownY + 0.08f, face + 0.12f), tip = new Vector3(-s * 0.58f, crownY + 0.3f, face + 0.1f);
                b.Tube(root, knee, 0.15f, 0.11f, 7, Bone, capped: false);
                b.Tube(knee, tip, 0.11f, 0.008f, 7, Bone);
                b.Tube(root - new Vector3(s * 0.06f, 0.05f, 0f), root, 0.17f, 0.16f, 7, Gilt);
            }
            Lit(lights, new Vector3(0f, h * 0.62f, half.Y + 1.7f));
        }

        // ---- The hardest blow.

        /// <summary>
        /// A boulder split clean through by one blow, its halves fallen a little apart with their cut faces pale, and the greatsword that did it standing in the cleft. Turned
        /// a little on its pedestal, so that the cleft and the flat of the blade are both seen.
        /// </summary>
        private static void SplitStone(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            const float Foot = 0.9f;
            Pedestal(b, half, Foot);
            Plate(b, new Vector3(0f, Foot, half.Y - 0.55f));
            VertexMaterial rock = Hall.Rough(142), cut = Hall.Rough(204);
            Hall.At(b, new Vector3(0f, Foot, -0.15f), MathHelper.DegreesToRadians(32f), () =>
            {
                const float Rx = 1.05f, Ry = 1.25f, Rz = 0.98f, Gap = 0.13f, Lean = 0.2f;
                foreach (float s in new[] { -1f, 1f })
                {
                    // Half a lump, its outside round and uneven, flat where it sits, and leaning away from the other.
                    Vector3 Lump(float a, float e)
                    {
                        float bumps = 1f + 0.09f * MathF.Sin(a * 3f + s * 1.3f) * MathF.Cos(e * 2f) + 0.07f * MathF.Sin(e * 5f + a * 2f + s);
                        Vector3 p = new Vector3(MathF.Cos(a) * MathF.Cos(e) * Rx, MathF.Sin(e) * Ry, -MathF.Sin(a) * MathF.Cos(e) * Rz) * bumps;
                        float y = MathF.Max(p.Y + Ry * 0.82f, 0f);
                        return new Vector3(p.X + s * (Gap + Lean * y / (Ry * 2f)), y, p.Z);
                    }
                    float from = s > 0f ? -MathF.PI * 0.5f : MathF.PI * 0.5f;
                    b.Surface(10, 10, (u, v) => Lump(from + u * MathF.PI, (v - 0.5f) * MathF.PI), rock);
                    // Its cut face: from its middle out to its rim, which is where the round outside meets the cut.
                    Vector3 middle = new Vector3(s * (Gap + Lean * 0.4f), Ry * 0.8f, 0f);
                    const int Rim = 20;
                    Vector3 RimAt(int k)
                    {
                        float t = MathF.Tau * k / Rim;
                        // Round the cut: up the front of it and down the back.
                        float e = MathF.Asin(Math.Clamp(MathF.Sin(t), -1f, 1f));
                        return Lump(MathF.Cos(t) >= 0f ? -MathF.PI * 0.5f : MathF.PI * 0.5f, e) with { X = s * (Gap + Lean * MathF.Max(MathF.Sin(e) * Ry + Ry * 0.82f, 0f) / (Ry * 2f)) };
                    }
                    for (int k = 0; k < Rim; k++) Hall.Tri(b, middle, RimAt(k), RimAt(k + 1), new Vector3(-s, 0f, 0f), cut);
                }
                // Chips of it, where they fell.
                for (int i = 0; i < 9; i++)
                {
                    float size = random.Range(0.06f, 0.14f);
                    Vector3 at = new Vector3(random.Range(-0.35f, 0.35f), size * 0.5f, random.Range(-1.15f, 1.15f));
                    Hall.At(b, at, random.Range(0f, 3f), () => b.Box(Vector3.Zero, new Vector3(size, size * 0.5f, size * 0.8f), i % 2 == 0 ? rock : cut));
                }

                // The sword, edge down in the cleft, the flat of it along the cut: a blade of bright steel, a guard and pommel of gold, a grip bound in teal.
                float blade = 0.55f, guard = 3.05f;
                Hall.Board(b, new Vector3(0f, blade, 0f), new Vector3(0f, guard, 0f), Vector3.UnitZ, 0.19f, 0.035f, Steel);
                Hall.Board(b, new Vector3(0f, blade + 0.2f, 0f), new Vector3(0f, guard - 0.1f, 0f), Vector3.UnitZ, 0.04f, 0.045f, Hall.Metal(150, 152, 160));
                Hall.Board(b, new Vector3(0f, guard, -0.62f), new Vector3(0f, guard, 0.62f), Vector3.UnitY, 0.08f, 0.07f, Gold);
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(0f, guard - 0.05f, s * 0.62f), new Vector3(0.09f, 0.12f, 0.07f), Gold);
                b.Tube(new Vector3(0f, guard + 0.08f, 0f), new Vector3(0f, guard + 0.95f, 0f), 0.075f, 0.062f, 8, Hall.Leather(26, 74, 80));
                b.Tube(new Vector3(0f, guard + 0.95f, 0f), new Vector3(0f, guard + 1.02f, 0f), 0.11f, 0.15f, 8, Gold, capped: false);
                b.Tube(new Vector3(0f, guard + 1.02f, 0f), new Vector3(0f, guard + 1.22f, 0f), 0.15f, 0.07f, 8, Gold);
            });
            Lit(lights, new Vector3(0f, h * 0.7f, half.Y + 1.7f));
        }

        // ---- Uniques found.

        /// <summary>
        /// A treasury laid open: three steps of dark velvet rising to a back of walnut under an arch of gold, and on each step what's been found, every piece on its own
        /// stand with its stone alight, in the amber a unique is known by. Nothing is behind glass or under a lid, as the camera looks down into it.
        /// </summary>
        private static void Treasury(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            const float Foot = 1.1f;
            Pedestal(b, half, Foot);
            Plate(b, new Vector3(0f, Foot, half.Y - 0.5f));
            float back = -half.Y + 0.3f;
            float wide = half.X - 0.3f;
            float top = h - 1.2f;
            // The back, and its arch.
            b.Box(new Vector3(0f, (Foot + top) * 0.5f, back), new Vector3(wide, (top - Foot) * 0.5f, 0.14f), Walnut);
            b.Box(new Vector3(0f, (Foot + top) * 0.5f, back + 0.15f), new Vector3(wide - 0.16f, (top - Foot) * 0.5f - 0.1f, 0.012f), Velvet);
            const int Arc = 12;
            Vector3 spring = new Vector3(0f, top, back);
            for (int i = 0; i < Arc; i++)
            {
                float a0 = MathF.PI * i / Arc, a1 = MathF.PI * (i + 1) / Arc;
                Vector3 p0 = spring + new Vector3(MathF.Cos(a0) * wide, MathF.Sin(a0) * wide * 0.62f, 0f), p1 = spring + new Vector3(MathF.Cos(a1) * wide, MathF.Sin(a1) * wide * 0.62f, 0f);
                Hall.Tri(b, spring + Vector3.UnitZ * 0.14f, p0 + Vector3.UnitZ * 0.14f, p1 + Vector3.UnitZ * 0.14f, Vector3.UnitZ, Walnut);
                Hall.Tri(b, spring - Vector3.UnitZ * 0.14f, p0 - Vector3.UnitZ * 0.14f, p1 - Vector3.UnitZ * 0.14f, -Vector3.UnitZ, Walnut);
                Hall.Face(b, p0 - Vector3.UnitZ * 0.14f, p1 - Vector3.UnitZ * 0.14f, p1 + Vector3.UnitZ * 0.14f, p0 + Vector3.UnitZ * 0.14f, (p0 + p1) * 0.5f - spring, Walnut);
                Hall.Board(b, p0 + Vector3.UnitZ * 0.17f, p1 + Vector3.UnitZ * 0.17f, (p0 + p1) * 0.5f - spring, 0.07f, 0.03f, Gilt);
            }
            // The star a unique falls under, in the arch.
            Vector3 star = spring + new Vector3(0f, wide * 0.27f, 0.16f);
            for (int i = 0; i < 8; i++)
            {
                float a = MathF.Tau * i / 8f;
                Vector3 ray = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f);
                Hall.Tri(b, star + new Vector3(-ray.Y, ray.X, 0f) * 0.07f, star - new Vector3(-ray.Y, ray.X, 0f) * 0.07f, star + ray * (i % 2 == 0 ? 0.52f : 0.3f), Vector3.UnitZ, Gilt);
            }
            b.Tube(star, star + Vector3.UnitZ * 0.06f, 0.12f, 0.08f, 8, Hall.Gem(1f, 0.5f, 0.12f, 1.3f));

            // The steps, and what's on them.
            const int Steps = 3;
            float depth = (half.Y * 2f - 0.75f) / Steps;
            for (int step = 0; step < Steps; step++)
            {
                float stepTop = Foot + 0.42f + step * 0.5f;
                float z = half.Y - 0.3f - depth * (step + 0.5f);
                b.Box(new Vector3(0f, (Foot + stepTop) * 0.5f, z), new Vector3(wide - 0.05f, (stepTop - Foot) * 0.5f, depth * 0.5f), Walnut);
                b.Box(new Vector3(0f, stepTop + 0.012f, z), new Vector3(wide - 0.09f, 0.012f, depth * 0.5f - 0.04f), Velvet);
                b.Box(new Vector3(0f, stepTop - 0.03f, z + depth * 0.5f + 0.006f), new Vector3(wide - 0.05f, 0.022f, 0.008f), Gilt);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = new Vector3((i - 1.5f) * (wide * 0.5f), stepTop + 0.024f, z);
                    Treasure(b, at, step * 4 + i);
                }
            }
            Lit(lights, new Vector3(0f, h * 0.7f, half.Y + 1.7f), 24f, 10f);
        }

        // One thing found, on its stand: twelve of them, no two alike.
        private static void Treasure(MeshBuilder b, Vector3 at, int which)
        {
            Vector3[] ambers = { new(1f, 0.52f, 0.12f), new(1f, 0.66f, 0.2f), new(0.98f, 0.42f, 0.1f) };
            Vector3 c = ambers[which % ambers.Length];
            VertexMaterial stone = Hall.Gem(c.X, c.Y, c.Z, 1.25f);
            switch (which % 6)
            {
                case 0:     // A ring, on a little post.
                    b.Tube(at, at + Vector3.UnitY * 0.16f, 0.07f, 0.05f, 6, Gilt);
                    for (int k = 0; k < 8; k++)
                    {
                        float a0 = MathF.Tau * k / 8f, a1 = MathF.Tau * (k + 1) / 8f;
                        b.Beam(at + new Vector3(MathF.Cos(a0) * 0.13f, 0.29f + MathF.Sin(a0) * 0.13f, 0f), at + new Vector3(MathF.Cos(a1) * 0.13f, 0.29f + MathF.Sin(a1) * 0.13f, 0f), 0.025f, Gold);
                    }
                    b.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(0.055f), stone);
                    break;
                case 1:     // An amulet, on a cushion.
                    b.Box(at + Vector3.UnitY * 0.05f, new Vector3(0.22f, 0.05f, 0.17f), Hall.Cloth(30, 90, 94));
                    b.Tube(at + Vector3.UnitY * 0.1f, at + Vector3.UnitY * 0.14f, 0.12f, 0.1f, 8, Gold);
                    b.Tube(at + Vector3.UnitY * 0.14f, at + Vector3.UnitY * 0.17f, 0.075f, 0.05f, 8, stone);
                    break;
                case 2:     // A dagger, on two rests.
                    foreach (float s in new[] { -1f, 1f }) b.Box(at + new Vector3(s * 0.16f, 0.06f, 0f), new Vector3(0.02f, 0.06f, 0.05f), Gilt);
                    b.Box(at + new Vector3(0.07f, 0.135f, 0f), new Vector3(0.24f, 0.012f, 0.045f), Steel);
                    b.Box(at + new Vector3(-0.2f, 0.135f, 0f), new Vector3(0.02f, 0.02f, 0.09f), Gold);
                    b.Box(at + new Vector3(-0.29f, 0.135f, 0f), new Vector3(0.07f, 0.022f, 0.03f), Walnut);
                    b.Box(at + new Vector3(-0.38f, 0.135f, 0f), new Vector3(0.035f), stone);
                    break;
                case 3:     // A stone, held up in a claw of gold.
                    b.Tube(at, at + Vector3.UnitY * 0.07f, 0.1f, 0.08f, 6, Gilt);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = MathF.Tau * k / 3f;
                        b.Beam(at + Vector3.UnitY * 0.07f, at + new Vector3(MathF.Cos(a) * 0.09f, 0.3f, MathF.Sin(a) * 0.09f), 0.018f, Gold);
                    }
                    HallFurniture.Sphere(b, at + Vector3.UnitY * 0.32f, 0.1f, 8, 6, stone);
                    break;
                case 4:     // A cup.
                    b.Tube(at, at + Vector3.UnitY * 0.04f, 0.1f, 0.09f, 8, Gold);
                    b.Tube(at + Vector3.UnitY * 0.04f, at + Vector3.UnitY * 0.2f, 0.03f, 0.03f, 6, Gold, capped: false);
                    b.Tube(at + Vector3.UnitY * 0.2f, at + Vector3.UnitY * 0.4f, 0.05f, 0.13f, 10, Gold, capped: false);
                    b.Tube(at + Vector3.UnitY * 0.36f, at + Vector3.UnitY * 0.38f, 0.115f, 0.115f, 10, stone);
                    break;
                default:    // A circlet.
                    b.Box(at + Vector3.UnitY * 0.04f, new Vector3(0.2f, 0.04f, 0.2f), Hall.Cloth(30, 90, 94));
                    b.Tube(at + Vector3.UnitY * 0.08f, at + Vector3.UnitY * 0.2f, 0.16f, 0.17f, 10, Gold, capped: false);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = MathF.Tau * k / 5f + 0.3f;
                        b.Tube(at + new Vector3(MathF.Cos(a) * 0.165f, 0.2f, MathF.Sin(a) * 0.165f), at + new Vector3(MathF.Cos(a) * 0.17f, 0.31f, MathF.Sin(a) * 0.17f), 0.03f, 0.004f, 4, Gold);
                    }
                    b.Box(at + new Vector3(0f, 0.15f, 0.17f), new Vector3(0.035f), stone);
                    break;
            }
        }

        // ---- The swiftest land.

        /// <summary>
        /// A great hourglass, hung in a cradle by its waist so that it can be turned over, and caught part way through being turned: tipped far out into the room, across
        /// the camera's view from where it stands against a side wall, so that the whole length of it is seen. Upright, or tipped towards the camera, it's seen end on, as
        /// a plate of wood. Two plates of walnut rimmed in gold on three posts, and between them the two bulbs of glass, pale, with the sand showing gold through them:
        /// most of it in one, and the last of it in the neck of the other.
        /// </summary>
        private static void Hourglass(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            float r = MathF.Min(half.X, half.Y);
            const float Foot = 0.7f;
            RoundPedestal(b, r, Foot);
            Plate(b, new Vector3(0f, Foot, r - 0.5f), 0.5f);

            // Built upright about its waist and then tipped, its head towards +z.
            const float Tip = 1.18f;
            const float Tall = 3.3f;
            float plate = r * 0.5f;
            float cos = MathF.Cos(Tip), sin = MathF.Sin(Tip);
            Vector3 pivot = new Vector3(0f, Foot + Tall * 0.5f * cos + plate * sin + 0.2f, 0.12f);
            Vector3 T(float x, float y, float z) => pivot + new Vector3(x, y * cos - z * sin, y * sin + z * cos);
            float glass = Tall * 0.5f - 0.14f;
            foreach (float s in new[] { -1f, 1f })
            {
                float y0 = s * Tall * 0.5f, y1 = s * glass;
                b.Tube(T(0f, MathF.Min(y0, y1), 0f), T(0f, MathF.Max(y0, y1), 0f), plate, plate, 20, Walnut);
                b.Tube(T(0f, MathF.Min(y0, y1) + 0.04f, 0f), T(0f, MathF.Max(y0, y1) - 0.04f, 0f), plate + 0.035f, plate + 0.035f, 20, Gilt, capped: false);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.5f;
                float x = MathF.Cos(a) * (plate - 0.1f), z = MathF.Sin(a) * (plate - 0.1f);
                b.Tube(T(x, -glass, z), T(x, glass, z), 0.055f, 0.055f, 8, Gilt, capped: false);
                foreach (float y in new[] { -glass + 0.1f, 0f, glass - 0.1f }) b.Tube(T(x, y - 0.06f, z), T(x, y + 0.06f, z), 0.1f, 0.1f, 8, Gilt);
            }

            // The glass: each bulb swelling from the neck to its plate, in bands, so that where the sand lies against it the glass is the sand's gold. Most of it has
            // run into the bulb towards the floor; the last of it is in the neck of the other.
            float bulb = plate - 0.2f;
            const int Bands = 7;
            float Radius(float t) => 0.1f + (bulb - 0.1f) * MathF.Sin(MathF.Min(t, 1f) * MathF.PI * 0.5f);     // t from the neck, 0, to a plate, 1.
            VertexMaterial clear = Hall.Flat(178, 214, 220), sand = Hall.Flat(214, 168, 84);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < Bands; i++)
                {
                    float t0 = i / (float)Bands, t1 = (i + 1) / (float)Bands;
                    bool full = s < 0f ? t0 >= 0.42f : t1 <= 0.3f;
                    Vector3 from = T(0f, s * t0 * glass, 0f), to = T(0f, s * t1 * glass, 0f);
                    b.Tube(s > 0f ? from : to, s > 0f ? to : from, s > 0f ? Radius(t0) : Radius(t1), s > 0f ? Radius(t1) : Radius(t0), 16, full ? sand : clear, capped: false);
                }
            }
            b.Tube(T(0f, -0.07f, 0f), T(0f, 0.07f, 0f), 0.14f, 0.14f, 10, Gilt, capped: false);
            // Ribs of silver down the glass.
            for (int rib = 0; rib < 6; rib++)
            {
                float a = MathF.Tau * rib / 6f;
                foreach (float s in new[] { -1f, 1f })
                {
                    for (int i = 0; i < Bands; i++)
                    {
                        float t0 = i / (float)Bands, t1 = (i + 1) / (float)Bands;
                        b.Beam(T(MathF.Cos(a) * (Radius(t0) + 0.01f), s * t0 * glass, MathF.Sin(a) * (Radius(t0) + 0.01f)), T(MathF.Cos(a) * (Radius(t1) + 0.01f), s * t1 * glass, MathF.Sin(a) * (Radius(t1) + 0.01f)), 0.014f, Silver);
                    }
                }
            }

            // The cradle: two legs either side, spread fore and aft, and the pin through the waist.
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pin = pivot + Vector3.UnitX * (s * (plate + 0.16f));
                foreach (float z in new[] { -0.6f, 0.6f })
                {
                    Vector3 on = new Vector3(pin.X, Foot, pivot.Z + z);
                    b.Tube(on, on + Vector3.UnitY * 0.08f, 0.12f, 0.1f, 8, Gilt);
                    b.Tube(on + Vector3.UnitY * 0.08f, pin, 0.075f, 0.06f, 8, Gilt, capped: false);
                }
                b.Tube(pivot + Vector3.UnitX * (s * 0.13f), pin + Vector3.UnitX * (s * 0.1f), 0.05f, 0.05f, 6, Gilt);
                b.Tube(pin + Vector3.UnitX * (s * 0.02f), pin + Vector3.UnitX * (s * 0.14f), 0.13f, 0.1f, 8, Gilt);
            }
            Lit(lights, new Vector3(0f, h + 1.8f, half.Y + 1.6f), 20f, 9.5f);
        }
        // ---- The fallen.

        /// <summary>
        /// A stone for every time a hero fell: a slab of dark marble, round at its head, lines of gold cut in it and the Haven's blossom over them, a wreath leaning at
        /// its foot, and a row of candles burning before it, some of them low.
        /// </summary>
        private static void Memorial(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            const float Foot = 0.55f;
            Pedestal(b, half, Foot);
            b.Box(new Vector3(0f, Foot + 0.1f, 0.42f), new Vector3(half.X - 0.3f, 0.1f, 0.42f), White);
            float wide = half.X * 0.64f, thick = 0.2f;
            float z = -half.Y + 0.45f;
            float shoulder = h - wide * 0.7f - 0.1f;
            VertexMaterial slab = DeepTeal;
            b.Box(new Vector3(0f, (Foot + shoulder) * 0.5f, z), new Vector3(wide, (shoulder - Foot) * 0.5f, thick), slab);
            // Its head, round.
            const int Arc = 14;
            Vector3 centre = new Vector3(0f, shoulder, z);
            for (int i = 0; i < Arc; i++)
            {
                float a0 = MathF.PI * i / Arc, a1 = MathF.PI * (i + 1) / Arc;
                Vector3 p0 = centre + new Vector3(MathF.Cos(a0) * wide, MathF.Sin(a0) * wide * 0.7f, 0f), p1 = centre + new Vector3(MathF.Cos(a1) * wide, MathF.Sin(a1) * wide * 0.7f, 0f);
                Vector3 front = Vector3.UnitZ * thick;
                Hall.Tri(b, centre + front, p0 + front, p1 + front, Vector3.UnitZ, slab);
                Hall.Tri(b, centre - front, p0 - front, p1 - front, -Vector3.UnitZ, slab);
                Hall.Face(b, p0 - front, p1 - front, p1 + front, p0 + front, (p0 + p1) * 0.5f - centre, slab);
            }
            // The blossom at its head, and the lines under it, each of a few lengths, as names are.
            float face = z + thick + 0.012f;
            Vector3 heart = new Vector3(0f, shoulder + wide * 0.12f, face);
            for (int petal = 0; petal < 5; petal++)
            {
                float a = MathF.Tau * petal / 5f + MathF.PI * 0.5f;
                Vector3 out_ = new Vector3(MathF.Cos(a), MathF.Sin(a), 0f), side = new Vector3(-out_.Y, out_.X, 0f);
                Hall.Face(b, heart + out_ * 0.08f, heart + out_ * 0.24f + side * 0.13f, heart + out_ * 0.4f, heart + out_ * 0.24f - side * 0.13f, Vector3.UnitZ, Blossom);
            }
            b.Box(heart, new Vector3(0.06f, 0.06f, 0.012f), Gilt);
            for (int line = 0; line < 7; line++)
            {
                float y = shoulder - 0.42f - line * 0.3f;
                if (y < Foot + 0.9f) break;
                float x = -wide * 0.74f;
                while (x < wide * 0.7f)
                {
                    float length = random.Range(0.18f, 0.42f);
                    if (x + length > wide * 0.76f) break;
                    b.Box(new Vector3(x + length * 0.5f, y, face), new Vector3(length * 0.5f, 0.032f, 0.008f), Gilt);
                    x += length + 0.1f;
                }
            }
            // A wreath against it: leaves in a ring, blossom among them.
            VertexMaterial leaf = new VertexMaterial(MaterialCatalog.FoliageWhite.Index, new Colour(46, 88, 40));
            Vector3 wreath = new Vector3(-wide * 0.3f, Foot + 0.85f, z + thick + 0.2f);
            for (int i = 0; i < 20; i++)
            {
                float a = MathF.Tau * i / 20f;
                Vector3 p = wreath + new Vector3(MathF.Cos(a) * 0.52f, MathF.Sin(a) * 0.52f, -MathF.Sin(a) * 0.1f);
                b.Box(p, new Vector3(0.1f, 0.09f, 0.07f), leaf);
                if (i % 4 == 1) b.Box(p + Vector3.UnitZ * 0.07f, new Vector3(0.06f), Hall.Flat(212, 150, 162));
            }
            // Candles along the step before it.
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4f) * ((half.X - 0.55f) / 4f);
                float tall = 0.22f + 0.34f * MathF.Abs(MathF.Sin(i * 2.4f + 0.7f));
                Vector3 foot = new Vector3(x, Foot + 0.2f, 0.52f + (i % 2) * 0.16f);
                b.Tube(foot, foot + Vector3.UnitY * 0.03f, 0.1f, 0.09f, 8, Gilt);
                Hall.Candle(b, foot + Vector3.UnitY * 0.03f, tall, 0.055f, lights, lit: i % 4 != 2);
            }
            lights.Add(AssetLight.VigilCandles(new Vector3(0f, Foot + 1.9f, half.Y + 0.9f)) with { Intensity = 26f, Radius = 11f, Shadow = false });
        }

        // ---- Lands freed.

        /// <summary>
        /// The chart of the lands, on a table of marble tilted towards whoever stands before it: parchment in a frame of walnut, the lands drawn on it, those set free
        /// green with a little flag in each, a compass in its corner, and more charts rolled in an urn beside it. It lies open to the sky, which is how it's seen.
        /// </summary>
        private static void MapTable(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            // Two piers of marble, and the top across them.
            foreach (float x in new[] { -half.X + 0.5f, half.X - 0.5f })
            {
                b.Box(new Vector3(x, 0.14f, -0.1f), new Vector3(0.36f, 0.14f, half.Y - 0.35f), Teal);
                b.Box(new Vector3(x, 0.3f, -0.1f), new Vector3(0.31f, 0.03f, half.Y - 0.4f), Gilt);
                b.Box(new Vector3(x, 1.08f, -0.1f), new Vector3(0.24f, 0.76f, half.Y - 0.5f), White);
            }
            Vector3 lo = new Vector3(0f, 1.78f, half.Y - 0.3f), hi = new Vector3(0f, h - 0.5f, -half.Y + 0.25f);
            Vector3 v = (hi - lo).Normalized();
            Vector3 n = Vector3.Cross(Vector3.UnitX, v).Normalized();
            if (n.Y < 0f) n = -n;
            float length = (hi - lo).Length;
            float wide = half.X - 0.05f;
            Hall.Board(b, lo - n * 0.1f, hi - n * 0.1f, Vector3.UnitX, wide, 0.1f, Walnut);
            Hall.Board(b, lo + v * 0.14f + n * 0.008f, hi - v * 0.14f + n * 0.008f, Vector3.UnitX, wide - 0.16f, 0.008f, Parchment);
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, lo + Vector3.UnitX * (s * (wide - 0.05f)) + n * 0.03f, hi + Vector3.UnitX * (s * (wide - 0.05f)) + n * 0.03f, Vector3.UnitX, 0.05f, 0.03f, Gilt);
            Hall.Board(b, lo - Vector3.UnitX * wide + n * 0.03f + v * 0.05f, lo + Vector3.UnitX * wide + n * 0.03f + v * 0.05f, v, 0.05f, 0.03f, Gilt);
            Hall.Board(b, hi - Vector3.UnitX * wide + n * 0.03f - v * 0.05f, hi + Vector3.UnitX * wide + n * 0.03f - v * 0.05f, v, 0.05f, 0.03f, Gilt);

            // The sea's edge drawn round the lands, and the lands: every third still the Reich's.
            Vector3 On(float x, float along, float lift) => lo + Vector3.UnitX * x + v * along + n * lift;
            void Patch(float x, float along, float halfX, float halfAlong, float lift, VertexMaterial material)
            {
                Hall.Face(b, On(x - halfX, along - halfAlong, lift), On(x + halfX, along - halfAlong, lift), On(x + halfX, along + halfAlong, lift), On(x - halfX, along + halfAlong, lift), n, material);
            }
            Patch(0f, length * 0.5f, wide - 0.4f, length * 0.5f - 0.36f, 0.02f, Hall.Flat(156, 178, 176));
            for (int i = 0; i < 10; i++)
            {
                float x = ((i % 5) - 2f) * (wide * 0.36f) + random.Range(-0.1f, 0.1f);
                float along = length * (i < 5 ? 0.34f : 0.68f) + random.Range(-0.12f, 0.12f);
                bool freed = i % 3 != 2;
                float hx = random.Range(0.2f, 0.3f), ha = random.Range(0.16f, 0.26f);
                Patch(x, along, hx, ha, 0.026f, freed ? Hall.Flat(98, 146, 84) : Hall.Flat(168, 138, 100));
                Patch(x + hx * 0.5f, along + ha * 0.6f, hx * 0.6f, ha * 0.5f, 0.027f, freed ? Hall.Flat(98, 146, 84) : Hall.Flat(168, 138, 100));
                if (!freed) continue;
                Vector3 pin = On(x, along, 0.03f);
                b.Beam(pin, pin + n * 0.5f, 0.014f, Gold);
                Hall.Board(b, pin + n * 0.42f, pin + n * 0.42f + Vector3.UnitX * 0.24f, n, 0.07f, 0.006f, Hall.Cloth(28, 104, 110));
            }
            // The compass drawn in its corner.
            Vector3 rose = On(wide - 0.75f, 0.62f, 0.03f);
            for (int i = 0; i < 8; i++)
            {
                float a = MathF.Tau * i / 8f;
                Vector3 ray = Vector3.UnitX * MathF.Cos(a) + v * MathF.Sin(a), side = Vector3.UnitX * -MathF.Sin(a) + v * MathF.Cos(a);
                Hall.Tri(b, rose + side * 0.045f, rose - side * 0.045f, rose + ray * (i % 2 == 0 ? 0.3f : 0.17f), n, i % 2 == 0 ? Hall.Flat(40, 72, 82) : Hall.Flat(190, 140, 60));
            }
            // Charts rolled in an urn at its side.
            Vector3 urn = new Vector3(half.X - 0.42f, 0f, half.Y - 0.36f);
            Hall.Turned(b, urn, Teal, 12, (0f, 0.26f), (0.08f, 0.3f), (0.2f, 0.2f), (0.75f, 0.34f), (0.95f, 0.3f));
            b.Tube(urn + Vector3.UnitY * 0.93f, urn + Vector3.UnitY * 0.99f, 0.32f, 0.32f, 12, Gilt, capped: false);
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.Tau * i / 5f;
                Vector3 foot = urn + new Vector3(MathF.Cos(a) * 0.13f, 0.5f, MathF.Sin(a) * 0.13f);
                b.Tube(foot, foot + new Vector3(MathF.Cos(a) * 0.14f, 1.25f, MathF.Sin(a) * 0.14f), 0.07f, 0.07f, 6, Parchment);
            }
            Lit(lights, new Vector3(0f, h + 2.2f, half.Y + 1.3f), 22f, 9.5f);
        }

        // ---- Gold gathered.

        /// <summary>
        /// A chest of walnut bound in silver, thrown open on its pedestal and heaped over its rim with gold: coin spilling down its front and across the marble, a cup
        /// and a circlet half buried, and a few cut stones.
        /// </summary>
        private static void Hoard(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            const float Foot = 0.7f;
            Pedestal(b, half, Foot);
            Plate(b, new Vector3(0f, Foot, half.Y - 0.5f));
            Vector2 chest = new Vector2(half.X - 0.5f, half.Y * 0.44f);
            float rim = Foot + 0.95f;
            Vector3 at = new Vector3(0f, 0f, -0.3f);
            b.Box(at + new Vector3(0f, (Foot + rim) * 0.5f, 0f), new Vector3(chest.X, (rim - Foot) * 0.5f, chest.Y), Walnut);
            foreach (float x in new[] { -chest.X + 0.22f, 0f, chest.X - 0.22f }) b.Box(at + new Vector3(x, (Foot + rim) * 0.5f, 0f), new Vector3(0.07f, (rim - Foot) * 0.5f + 0.012f, chest.Y + 0.02f), Silver);
            b.Box(at + new Vector3(0f, rim - 0.2f, chest.Y + 0.03f), new Vector3(0.12f, 0.14f, 0.03f), Silver);
            // The lid, thrown back.
            Vector3 hingeAt = at + new Vector3(0f, rim, -chest.Y);
            Vector3 up = new Vector3(0f, 0.86f, -0.5f).Normalized();
            Hall.Board(b, hingeAt, hingeAt + up * (chest.Y * 1.9f), Vector3.UnitX, chest.X, 0.07f, Walnut);
            foreach (float x in new[] { -chest.X + 0.22f, 0f, chest.X - 0.22f }) Hall.Board(b, hingeAt + Vector3.UnitX * x, hingeAt + Vector3.UnitX * x + up * (chest.Y * 1.9f), Vector3.UnitX, 0.07f, 0.085f, Silver);

            // The gold: heaped over the rim, highest in the middle, and run down the front.
            for (int i = 0; i < 90; i++)
            {
                float x = random.Range(-chest.X + 0.14f, chest.X - 0.14f), z = random.Range(-chest.Y + 0.12f, chest.Y - 0.1f);
                float mound = (1f - MathF.Abs(x) / chest.X) * (1f - MathF.Abs(z) / chest.Y);
                Vector3 coin = at + new Vector3(x, rim - 0.08f + mound * 0.42f + random.Range(0f, 0.06f), z);
                Hall.At(b, coin, random.Range(0f, 3f), () => b.Tube(Vector3.Zero, new Vector3(random.Range(-0.02f, 0.02f), 0.035f, 0f), 0.11f, 0.11f, 7, Gold));
            }
            for (int i = 0; i < 30; i++)
            {
                float t = random.Float();
                Vector3 coin = new Vector3(random.Range(-0.5f, 0.5f) * (1f + t), MathHelper.Lerp(rim - 0.1f, Foot + 0.02f, MathF.Min(t * 1.6f, 1f)), at.Z + chest.Y + 0.06f + t * (half.Y - chest.Y + 0.2f) * 0.8f);
                if (MathF.Abs(coin.X) > half.X - 0.2f || coin.Z > half.Y - 0.72f) continue;
                b.Tube(coin, coin + Vector3.UnitY * 0.035f, 0.11f, 0.11f, 7, Gold);
            }
            // A cup and a circlet in it, and stones.
            Vector3 cup = at + new Vector3(-0.55f, rim + 0.3f, 0.05f);
            b.Tube(cup, cup + new Vector3(-0.05f, 0.3f, 0f), 0.05f, 0.16f, 10, Gold, capped: false);
            Vector3 crown = at + new Vector3(0.5f, rim + 0.32f, -0.05f);
            b.Tube(crown, crown + new Vector3(0.03f, 0.14f, 0f), 0.2f, 0.21f, 10, Gold, capped: false);
            for (int k = 0; k < 6; k++)
            {
                float a = MathF.Tau * k / 6f;
                b.Tube(crown + new Vector3(MathF.Cos(a) * 0.2f, 0.14f, MathF.Sin(a) * 0.2f), crown + new Vector3(MathF.Cos(a) * 0.21f, 0.28f, MathF.Sin(a) * 0.21f), 0.035f, 0.004f, 4, Gold);
            }
            Vector3[] stones = { new(0.2f, 0.75f, 0.9f), new(0.25f, 0.9f, 0.5f), new(0.95f, 0.5f, 0.75f), new(1f, 0.55f, 0.15f) };
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = stones[i % stones.Length];
                b.Box(at + new Vector3(random.Range(-0.8f, 0.8f), rim + 0.3f + random.Range(0f, 0.1f), random.Range(-0.2f, 0.25f)), new Vector3(0.065f), Hall.Gem(c.X, c.Y, c.Z, 1.1f));
            }
            Lit(lights, new Vector3(0f, h + 2.2f, half.Y + 1.3f), 22f, 9.5f);
        }

        // ---- The deepest land.

        /// <summary>
        /// The climb, in marble: seven steps one on another, the same from every side, a stone alight in each face of each like a waygate's lights, and on the topmost a
        /// little waygate with its way open, which is where the next land is.
        /// </summary>
        private static void Climb(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            const float Foot = 0.5f;
            Pedestal(b, half, Foot);
            Plate(b, new Vector3(0f, Foot, half.Y - 0.36f), 0.5f);
            const int Steps = 7;
            float gate = 1.15f;
            float rise = (h - Foot - gate) / Steps;
            float widest = MathF.Min(half.X, half.Y) - 0.42f;
            for (int i = 0; i < Steps; i++)
            {
                float w = widest * (1f - i * 0.115f);
                float y = Foot + i * rise;
                b.Box(new Vector3(0f, y + rise * 0.5f, 0f), new Vector3(w, rise * 0.5f, w), i % 2 == 0 ? Grey : White);
                b.Box(new Vector3(0f, y + rise - 0.02f, 0f), new Vector3(w + 0.012f, 0.02f, w + 0.012f), Gilt);
                VertexMaterial stone = Hall.Gem(0.3f, 0.82f, 1f, 1.2f);
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(0f, y + rise * 0.45f, s * (w + 0.01f)), new Vector3(0.1f, 0.1f, 0.025f), stone);
                    b.Box(new Vector3(s * (w + 0.01f), y + rise * 0.45f, 0f), new Vector3(0.025f, 0.1f, 0.1f), stone);
                }
            }
            // The gate at the top: two posts and an arch of teal marble, and the way in it.
            float topY = Foot + Steps * rise;
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * 0.3f, topY + 0.38f, 0f), new Vector3(0.08f, 0.38f, 0.09f), Teal);
            const int Arc = 6;
            for (int i = 0; i < Arc; i++)
            {
                float a0 = MathF.PI * i / Arc, a1 = MathF.PI * (i + 1) / Arc;
                Vector3 p0 = new Vector3(MathF.Cos(a0) * 0.3f, topY + 0.76f + MathF.Sin(a0) * 0.3f, 0f), p1 = new Vector3(MathF.Cos(a1) * 0.3f, topY + 0.76f + MathF.Sin(a1) * 0.3f, 0f);
                Hall.Board(b, p0, p1, Vector3.UnitZ, 0.09f, 0.08f, Teal);
            }
            b.Box(new Vector3(0f, topY + 0.42f, 0f), new Vector3(0.22f, 0.42f, 0.02f), Hall.Glow(0.25f, 0.85f, 0.95f, 1.1f));
            Hall.Tri(b, new Vector3(-0.22f, topY + 0.84f, 0.02f), new Vector3(0.22f, topY + 0.84f, 0.02f), new Vector3(0f, topY + 1f, 0.02f), Vector3.UnitZ, Hall.Glow(0.25f, 0.85f, 0.95f, 1.1f));
            Hall.Tri(b, new Vector3(-0.22f, topY + 0.84f, -0.02f), new Vector3(0.22f, topY + 0.84f, -0.02f), new Vector3(0f, topY + 1f, -0.02f), -Vector3.UnitZ, Hall.Glow(0.25f, 0.85f, 0.95f, 1.1f));
            lights.Add(AssetLight.Glow(new Vector3(0f, h * 0.8f, half.Y + 1.6f), new Vector3(0.78f, 0.88f, 1f), 20f, 9.5f));
        }
    }
}
