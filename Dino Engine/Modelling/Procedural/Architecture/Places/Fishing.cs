using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A fishing village (PLACES.md), whose catch the Reich takes for its broods: the jetty of planks on posts run out to where the water's deep enough for a boat, its posts long where the bottom
    /// falls away, bollards and a lantern at its end; the boats, clinker built in the land's own paint, moored at the jetty or drawn up on the shingle; the nets hung to dry with their floats; the
    /// fish split and hung on racks; the tarred smokehouse with its smoke; a boat on the stocks, its ribs up and half its planks on; the pots stacked by the water; and the capstan the boats are
    /// hauled up by. Headless.
    /// </summary>
    internal static class Fishing
    {
        /// <summary>
        /// A jetty out along +z from the shore at z = 0, its deck at y = 0 (the builder stands it a little over the sea), on posts in pairs down past the bottom, which falls from the shore to the
        /// depth the id says at its end; a head across its end if it's long, bollards with rope round them, a ladder down into the water and a lantern on a post. Numbers: length, width, depth.
        /// </summary>
        public static void Jetty(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(0), 6f, 34f), width = Math.Clamp(spec.Metres(1), 1.6f, 3.6f), depth = Math.Clamp(spec.Metres(2), 0.3f, 3f);
            VertexMaterial post = Tarred(random, 1.1f), beam = Weathered(random, 0.85f), rope = Rope(random);
            const float Above = 0.9f, Spacing = 2.3f;
            bool head = length > 13f && random.Chance(0.7f);
            float headWidth = head ? width * random.Range(2.2f, 3f) : width;
            float half = width * 0.5f;
            // Where the bottom is under a point along it: from a little under the shore's edge to the depth at its end.
            float Bottom(float z) => -Above - MathHelper.Lerp(-0.4f, depth, Math.Clamp(z / length, 0f, 1f)) - 0.9f;
            int pairs = Math.Max(2, (int)(length / Spacing) + 1);
            for (int i = 0; i < pairs; i++)
            {
                float z = i * (length / (pairs - 1));
                bool atHead = head && z > length - 0.01f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float x = s * (atHead ? headWidth * 0.5f : half);
                    Kit.Pole(b, new Vector3(x, Bottom(z), z), new Vector3(x, -0.12f, z), random.Range(0.13f, 0.16f), post);
                }
                // The bearer across the pair, under the deck.
                Kit.Plank(b, new Vector3(-half - 0.2f, -0.22f, z), new Vector3(half + 0.2f, -0.22f, z), Vector3.UnitY, 0.2f, 0.18f, beam);
                // Braced across under it where it's deep.
                if (Bottom(z) < -2.2f && i % 2 == 1) Kit.Plank(b, new Vector3(-half, -0.35f, z + 0.12f), new Vector3(half, Bottom(z) * 0.6f, z + 0.12f), Vector3.UnitZ, 0.14f, 0.08f, beam);
            }
            // The stringers along it, and the deck of planks across them, a few loose, a few gone.
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * (half - 0.15f), -0.12f, -0.2f), new Vector3(s * (half - 0.15f), -0.12f, length + 0.2f), Vector3.UnitY, 0.18f, 0.16f, beam);
            for (float z = 0.12f; z < length; z += 0.32f)
            {
                if (random.Chance(0.03f)) continue;
                float jut = random.Range(-0.05f, 0.05f);
                Kit.Plank(b, new Vector3(-half + jut, -0.03f, z), new Vector3(half + jut + random.Range(-0.04f, 0.06f), -0.03f, z + random.Range(-0.02f, 0.02f)), Vector3.UnitY, 0.28f, 0.06f, Weathered(random, random.Range(0.9f, 1.08f)));
            }
            if (head)
            {
                float zh = length - 1.4f;
                foreach (float s in new[] { -1f, 1f })
                {
                    float x = s * headWidth * 0.5f;
                    Kit.Pole(b, new Vector3(x, Bottom(zh), zh), new Vector3(x, -0.12f, zh), 0.15f, post);
                }
                Kit.Plank(b, new Vector3(-headWidth * 0.5f, -0.22f, zh), new Vector3(headWidth * 0.5f, -0.22f, zh), Vector3.UnitY, 0.2f, 0.18f, beam);
                Kit.Plank(b, new Vector3(-headWidth * 0.5f, -0.22f, length), new Vector3(headWidth * 0.5f, -0.22f, length), Vector3.UnitY, 0.2f, 0.18f, beam);
                for (float x = -headWidth * 0.5f + 0.15f; x < headWidth * 0.5f; x += 0.32f)
                {
                    if (MathF.Abs(x) < half) continue;
                    Kit.Plank(b, new Vector3(x, -0.03f, zh - 0.3f), new Vector3(x, -0.03f, length + 0.25f), Vector3.UnitY, 0.28f, 0.06f, Weathered(random, random.Range(0.9f, 1.08f)));
                }
            }
            // Bollards at its end with rope coiled round, the ladder down off its side, and the lantern on its post.
            float endHalf = headWidth * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 bollard = new Vector3(s * (endHalf - 0.25f), 0f, length - 0.3f);
                Kit.Lathe(b, bollard - Vector3.UnitY * 0.05f, Vector3.UnitY, 0.6f, Kit.Profile((0f, 0.17f), (0.7f, 0.15f), (0.85f, 0.2f), (1f, 0.18f)), 10, 3, post);
                if (random.Chance(0.6f)) Kit.Lathe(b, bollard + Vector3.UnitY * 0.2f, Vector3.UnitY, 0.18f, Kit.Profile((0f, 0.21f), (0.5f, 0.23f), (1f, 0.21f)), 10, 2, rope);
            }
            float ladderX = (random.Chance(0.5f) ? -1f : 1f) * (endHalf + 0.12f);
            foreach (float dz in new[] { -0.28f, 0.28f }) Kit.Pole(b, new Vector3(ladderX, 0.5f, length - 2f + dz), new Vector3(ladderX, Bottom(length) * 0.7f, length - 2f + dz), 0.04f, post);
            for (float y = 0.2f; y > Bottom(length) * 0.7f; y -= 0.32f) Kit.Pole(b, new Vector3(ladderX, y, length - 2.28f), new Vector3(ladderX, y, length - 1.72f), 0.03f, post);
            Vector3 lamp = new Vector3(-ladderX * 0.9f, 0f, length - 0.6f);
            Kit.Pole(b, lamp - Vector3.UnitY * 0.1f, lamp + Vector3.UnitY * 2.4f, 0.07f, post);
            Kit.Pole(b, lamp + Vector3.UnitY * 2.3f, lamp + new Vector3(-MathF.Sign(lamp.X) * 0.45f, 2.3f, 0f), 0.04f, post);
            Kit.Lantern(b, lamp + new Vector3(-MathF.Sign(lamp.X) * 0.45f, 1.95f, 0f), LandIron(random), lights, 0.8f);
        }

        /// <summary>
        /// A clinker-built fishing boat along z, bow towards +z: its strakes in the land's paint over a white band at the waterline, dark inside, its thwarts, gunwales, stem and stern posts,
        /// oars in it and a heap of net; afloat with its waterline at y = 0, or drawn up on the shingle on its keel, or lying over on its side. Numbers: length, form.
        /// </summary>
        /// <summary>
        /// Which way a boat on its side has fallen along x (+1 or -1) and how broad it is, as <see cref="Boat"/> draws them, for what's solid about it (Hitboxes): it draws the
        /// same first numbers from the same stream, so keep the two in step.
        /// </summary>
        public static (float Side, float Beam) Lying(in PlaceSpec spec)
        {
            var random = new WorldRandom(spec.Seed).Fork(spec.Kind.ToString());
            float beam = Math.Clamp(spec.Metres(0), 3.5f, 9f) * random.Range(0.32f, 0.38f);
            random.Range(0.4f, 0.48f);
            random.Int(BoatPaints.Length);
            random.Range(0.9f, 1.08f);
            random.Range(0.85f, 1f);
            random.Range(0.9f, 1.08f);
            random.Range(0.88f, 1.06f);
            return (random.Chance(0.5f) ? -1f : 1f, beam);
        }

        public static void Boat(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            // What's solid about one on its side follows the numbers drawn here up to its heel (Lying): keep them in step.
            float length = Math.Clamp(spec.Metres(0), 3.5f, 9f);
            int form = Math.Clamp(spec.Int(1), 0, 2);
            float beam = length * random.Range(0.32f, 0.38f), depth = beam * random.Range(0.4f, 0.48f);
            VertexMaterial paint = Painted(BoatPaints[random.Int(BoatPaints.Length)] * random.Range(0.9f, 1.08f)), band = Painted(BoatWhite * random.Range(0.85f, 1f));
            VertexMaterial inside = Timber(random, 0.55f), wood = Weathered(random);
            // Afloat, its waterline at 0; drawn up, its keel on the ground and tipped a little; on its side, over by most of a right angle.
            float heel = form switch { 0 => 0f, 1 => random.Range(-0.12f, 0.12f), _ => (random.Chance(0.5f) ? 1f : -1f) * random.Range(1f, 1.2f) };
            float c = MathF.Cos(heel), s = MathF.Sin(heel);
            const int Lengthwise = 10, Strakes = 5;
            Vector3 Hull(float t, float k, float side)
            {
                // t from stern (0) to bow (1); k from the keel (0) to the gunwale (1); its sides fuller amidships and rising to the ends.
                float plan = MathF.Pow(MathF.Max(0f, 1f - MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 2.4f)), 0.6f);
                float sheer = 0.18f * MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 2f);
                float halfWidth = beam * 0.5f * plan * MathF.Pow(k, 0.55f) + 0.02f;
                return new Vector3(side * halfWidth, depth * (k + sheer * k), (t - 0.5f) * length);
            }
            // Afloat its waterline is at 0; on the shingle its lowest point, whichever that is as it lies, is just in the ground.
            float lowest = float.MaxValue;
            for (int i = 0; i <= Lengthwise; i++)
            {
                for (int j = 0; j <= Strakes; j++)
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 p = Hull(i / (float)Lengthwise, j / (float)Strakes, side);
                        lowest = MathF.Min(lowest, p.X * s + p.Y * c);
                    }
                }
            }
            float lift = form == 0 ? -depth * 0.45f : Ground - 0.06f - lowest;
            Vector3 Pose(Vector3 p) => new Vector3(p.X * c - p.Y * s, p.X * s + p.Y * c + lift, p.Z);
            for (int i = 0; i < Lengthwise; i++)
            {
                for (int j = 0; j < Strakes; j++)
                {
                    float t0 = i / (float)Lengthwise, t1 = (i + 1f) / Lengthwise, k0 = j / (float)Strakes, k1 = (j + 1f) / Strakes;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 a = Pose(Hull(t0, k0, side)), cc = Pose(Hull(t1, k0, side)), d = Pose(Hull(t1, k1, side)), e = Pose(Hull(t0, k1, side));
                        Vector3 outward = Pose(new Vector3(side, -0.4f, 0f)) - Pose(Vector3.Zero);
                        Ships.Along(b, a, cc, d, e, outward, j <= 1 ? band : paint);
                        // Its inside, a hair in, dark.
                        Vector3 inward = -outward;
                        Vector3 shrink = Pose(Hull(t0, k0, side) * new Vector3(0.94f, 1f, 0.97f) + Vector3.UnitY * 0.03f);
                        Ships.Along(b, shrink, Pose(Hull(t1, k0, side) * new Vector3(0.94f, 1f, 0.97f) + Vector3.UnitY * 0.03f), Pose(Hull(t1, k1, side) * new Vector3(0.94f, 1f, 0.97f) + Vector3.UnitY * 0.03f), Pose(Hull(t0, k1, side) * new Vector3(0.94f, 1f, 0.97f) + Vector3.UnitY * 0.03f), inward, inside);
                    }
                }
            }
            // Its gunwales, its stem and stern posts, its keel, and its thwarts across.
            foreach (float side in new[] { -1f, 1f })
            {
                var rail = new List<Vector3>();
                for (int i = 0; i <= Lengthwise; i++) rail.Add(Pose(Hull(i / (float)Lengthwise, 1f, side) + Vector3.UnitY * 0.03f));
                Kit.Bend(b, rail, 0.05f, 0.05f, wood);
            }
            Kit.Plank(b, Pose(new Vector3(0f, -0.06f, length * 0.5f - 0.1f)), Pose(new Vector3(0f, depth * 1.25f, length * 0.5f + 0.05f)), Pose(Vector3.UnitX) - Pose(Vector3.Zero), 0.1f, 0.12f, wood);
            Kit.Plank(b, Pose(new Vector3(0f, -0.06f, -length * 0.5f + 0.1f)), Pose(new Vector3(0f, depth * 1.2f, -length * 0.5f - 0.02f)), Pose(Vector3.UnitX) - Pose(Vector3.Zero), 0.1f, 0.12f, wood);
            Kit.Plank(b, Pose(new Vector3(0f, -0.04f, -length * 0.46f)), Pose(new Vector3(0f, -0.04f, length * 0.46f)), Pose(Vector3.UnitY) - Pose(Vector3.Zero), 0.1f, 0.1f, wood);
            int thwarts = length > 6f ? 3 : 2;
            for (int k = 0; k < thwarts; k++)
            {
                float t = 0.3f + 0.4f * k / Math.Max(thwarts - 1, 1);
                Vector3 l = Hull(t, 0.8f, -1f), r = Hull(t, 0.8f, 1f);
                Kit.Plank(b, Pose(l * new Vector3(0.94f, 1f, 1f)), Pose(r * new Vector3(0.94f, 1f, 1f)), Pose(Vector3.UnitY) - Pose(Vector3.Zero), 0.22f, 0.05f, wood);
            }
            if (form == 2) return;
            // Oars along its thwarts, and the net heaped in its stern, brown and wet.
            foreach (float side in new[] { -0.2f, 0.2f })
            {
                Kit.Pole(b, Pose(new Vector3(side * beam, depth * 0.82f, -length * 0.38f)), Pose(new Vector3(side * beam * 0.8f, depth * 0.85f, length * 0.32f)), 0.035f, wood);
            }
            Kit.Heap(b, Pose(new Vector3(0f, depth * 0.28f, -length * 0.3f)), new Vector2(beam * 0.32f, length * 0.12f), depth * 0.4f, 0f, Net(random, 0.85f), random, 3, 10);
        }

        /// <summary>Nets hung to dry over a rail on poles along x, falling either side nearly to the ground, the floats along their tops and their weights along their foot. Numbers: length.</summary>
        public static void NetRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 12f);
            VertexMaterial pole = Weathered(random), net = Net(random), dark = Net(random, 0.75f);
            const float High = 2.6f;
            int poles = Math.Max(2, (int)(length / 3.2f) + 1);
            for (int i = 0; i < poles; i++)
            {
                float x = -length * 0.5f + i * length / (poles - 1);
                Kit.Pole(b, new Vector3(x, -0.3f, 0f), new Vector3(x + random.Range(-0.05f, 0.05f), High + 0.1f, random.Range(-0.04f, 0.04f)), 0.07f, pole);
            }
            Kit.Pole(b, new Vector3(-length * 0.5f - 0.2f, High, 0f), new Vector3(length * 0.5f + 0.2f, High, 0f), 0.05f, pole);
            // The nets in hangs between the poles, each falling a little further one side than the other, sagging between.
            for (int i = 0; i < poles - 1; i++)
            {
                float x0 = -length * 0.5f + i * length / (poles - 1) + 0.1f, x1 = -length * 0.5f + (i + 1) * length / (poles - 1) - 0.1f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float drop = random.Range(1.7f, 2.3f), out_ = random.Range(0.25f, 0.5f);
                    Kit.Cloth(b, new Vector3(x0, High, side * 0.05f), new Vector3(x1, High, side * 0.05f), new Vector3(x1, High - drop, side * out_), new Vector3(x0, High - drop, side * out_), 0.25f, 4, net, dark);
                    for (float x = x0 + 0.3f; x < x1; x += 0.55f) Kit.Lathe(b, new Vector3(x, High + 0.02f, side * 0.07f), Vector3.UnitY, 0.08f, Kit.Profile((0f, 0.06f), (0.5f, 0.08f), (1f, 0.06f)), 6, 2, Flat(new Vector3(0.48f, 0.36f, 0.2f)));
                }
            }
        }

        /// <summary>Fish split and hung to dry in pairs from the rails of a rack of poles along x, silver going brown. Numbers: length.</summary>
        public static void FishRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 9f);
            VertexMaterial pole = Weathered(random, 0.95f);
            const float High = 2.3f;
            // An A at each end, and between where it's long, and rails along at three heights.
            int frames = Math.Max(2, (int)(length / 3f) + 1);
            for (int i = 0; i < frames; i++)
            {
                float x = -length * 0.5f + i * length / (frames - 1);
                Kit.Pole(b, new Vector3(x, -0.2f, -0.7f), new Vector3(x, High + 0.15f, 0f), 0.06f, pole);
                Kit.Pole(b, new Vector3(x, -0.2f, 0.7f), new Vector3(x, High + 0.15f, 0f), 0.06f, pole);
            }
            float[] rails = { High, High - 0.65f, High - 1.3f };
            for (int r = 0; r < rails.Length; r++)
            {
                float z = r == 0 ? 0f : (r % 2 == 1 ? -1f : 1f) * (0.7f * (1f - rails[r] / (High + 0.15f)) + 0.02f);
                Kit.Pole(b, new Vector3(-length * 0.5f - 0.1f, rails[r], z), new Vector3(length * 0.5f + 0.1f, rails[r], z), 0.035f, pole);
                for (float x = -length * 0.5f + 0.2f; x < length * 0.5f - 0.1f; x += random.Range(0.22f, 0.32f))
                {
                    if (random.Chance(0.12f)) continue;
                    float size = random.Range(0.32f, 0.5f);
                    Vector3 colour = Vector3.Lerp(new Vector3(0.5f, 0.5f, 0.48f), new Vector3(0.42f, 0.3f, 0.18f), random.Float());
                    Fish(b, new Vector3(x, rails[r] - 0.04f, z), size, colour, random);
                }
            }
        }

        // A fish split and hung by its tail: a flat shape tapering to the tail at the top, its belly side pale, hanging down from the rail.
        private static void Fish(MeshBuilder b, Vector3 tail, float size, Vector3 colour, WorldRandom random)
        {
            float w = size * 0.28f, sway = random.Range(-0.08f, 0.08f);
            Vector3 head = tail + new Vector3(sway, -size, 0f);
            Vector3 mid = Vector3.Lerp(tail, head, 0.55f);
            VertexMaterial skin = new VertexMaterial(Textures.MaterialCatalog.Hide.Index, new Colour(colour));
            foreach (float face in new[] { -1f, 1f })
            {
                Vector3 n = new Vector3(0f, 0f, face);
                Kit.Face(b, tail + new Vector3(-w * 0.35f, 0f, 0.01f * face), tail + new Vector3(w * 0.35f, 0f, 0.01f * face), mid + new Vector3(w, 0f, 0.01f * face), mid + new Vector3(-w, 0f, 0.01f * face), n, skin);
                Kit.Face(b, mid + new Vector3(-w, 0f, 0.01f * face), mid + new Vector3(w, 0f, 0.01f * face), head + new Vector3(0f, 0f, 0.01f * face), n, skin);
            }
        }

        /// <summary>A smokehouse of tarred boards under a turf or board roof, the smoke louvre along its ridge (its chimney), its low door, and the split wood stacked against its side.</summary>
        public static void Smokehouse(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float w = random.Range(2.6f, 3.4f), d = random.Range(2.2f, 2.8f), wall = random.Range(1.9f, 2.3f), ridge = wall + random.Range(1.1f, 1.5f);
            VertexMaterial boards = Tarred(random, 1.15f), trim = Weathered(random, 0.8f), roof = random.Chance(0.6f) ? Turf(0.9f) : Weathered(random, 0.75f);
            Kit.Span(b, new Vector3(-w * 0.5f, -0.2f, -d * 0.5f), new Vector3(w * 0.5f, wall, d * 0.5f), 0.02f, boards);
            // Its gables, and the roof down both sides from the ridge.
            foreach (float z in new[] { -d * 0.5f, d * 0.5f }) Kit.Face(b, new Vector3(-w * 0.5f, wall, z), new Vector3(w * 0.5f, wall, z), new Vector3(0f, ridge, z), new Vector3(0f, 0f, z), boards);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 eave = new Vector3(side * (w * 0.5f + 0.3f), wall - 0.15f, 0f), top = new Vector3(side * 0.12f, ridge, 0f);
                Kit.Face(b, eave + new Vector3(0f, 0f, -d * 0.5f - 0.3f), eave + new Vector3(0f, 0f, d * 0.5f + 0.3f), top + new Vector3(0f, 0f, d * 0.5f + 0.3f), top + new Vector3(0f, 0f, -d * 0.5f - 0.3f), new Vector3(side, 1.5f, 0f), roof);
            }
            // The louvre along its ridge, a little roof of its own over a gap the smoke comes out of.
            Kit.Span(b, new Vector3(-0.35f, ridge - 0.05f, -d * 0.3f), new Vector3(0.35f, ridge + 0.3f, d * 0.3f), 0.01f, Dark);
            foreach (float side in new[] { -1f, 1f }) Kit.Face(b, new Vector3(side * 0.55f, ridge + 0.25f, -d * 0.35f), new Vector3(side * 0.55f, ridge + 0.25f, d * 0.35f), new Vector3(0f, ridge + 0.55f, d * 0.35f), new Vector3(0f, ridge + 0.55f, -d * 0.35f), new Vector3(side, 1.5f, 0f), trim);
            // Its door, low, on its front, and the wood stacked against its side.
            Kit.Span(b, new Vector3(-0.42f, 0f, d * 0.5f), new Vector3(0.42f, 1.5f, d * 0.5f + 0.05f), 0.01f, trim);
            for (int row = 0; row < 4; row++)
            {
                for (float z = -d * 0.45f; z < d * 0.45f; z += 0.24f) Kit.Pole(b, new Vector3(w * 0.5f + 0.08f, 0.12f + row * 0.22f, z), new Vector3(w * 0.5f + 0.62f, 0.12f + row * 0.22f, z + random.Range(-0.03f, 0.03f)), 0.1f, Timber(random, random.Range(0.8f, 1.1f)));
            }
        }

        /// <summary>Where a smokehouse's smoke leaves it: along the louvre on its ridge.</summary>
        public static Vector3[] SmokeOf() => new[] { new Vector3(0f, 4f, 0f) };

        /// <summary>A boat being built on the stocks: its keel on blocks, its stem and stern posts up, its ribs standing, half its strakes on, the rest of its planks stacked by it. Numbers: length.</summary>
        public static void BoatStocks(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 4f, 9f);
            float beam = length * 0.36f, depth = beam * 0.45f;
            VertexMaterial fresh = Fresh(random), weathered = Weathered(random), paint = Painted(BoatPaints[random.Int(BoatPaints.Length)]);
            const float Up = 0.55f;
            foreach (float z in new[] { -length * 0.35f, 0f, length * 0.35f }) Kit.Span(b, new Vector3(-0.3f, -0.1f, z - 0.2f), new Vector3(0.3f, Up - 0.05f, z + 0.2f), 0.02f, weathered);
            Kit.Plank(b, new Vector3(0f, Up, -length * 0.5f), new Vector3(0f, Up, length * 0.5f), Vector3.UnitY, 0.16f, 0.14f, fresh);
            Kit.Plank(b, new Vector3(0f, Up, length * 0.5f), new Vector3(0f, Up + depth * 1.3f, length * 0.55f), Vector3.UnitX, 0.12f, 0.14f, fresh);
            Kit.Plank(b, new Vector3(0f, Up, -length * 0.5f), new Vector3(0f, Up + depth * 1.2f, -length * 0.53f), Vector3.UnitX, 0.12f, 0.14f, fresh);
            int ribs = (int)(length / 0.55f);
            for (int i = 1; i < ribs; i++)
            {
                float t = i / (float)ribs;
                float plan = MathF.Pow(MathF.Max(0f, 1f - MathF.Pow(MathF.Abs(t - 0.5f) * 2f, 2.4f)), 0.6f);
                float z = (t - 0.5f) * length;
                foreach (float side in new[] { -1f, 1f })
                {
                    var rib = new List<Vector3>();
                    for (int k = 0; k <= 4; k++)
                    {
                        float kk = k / 4f;
                        rib.Add(new Vector3(side * (beam * 0.5f * plan * MathF.Pow(kk, 0.55f) + 0.02f), Up + depth * kk, z));
                    }
                    Kit.Bend(b, rib, 0.035f, 0.03f, fresh);
                }
                // The strakes on so far, up from the keel.
                if (i < ribs - 1)
                {
                    float t1 = (i + 1) / (float)ribs;
                    float plan1 = MathF.Pow(MathF.Max(0f, 1f - MathF.Pow(MathF.Abs(t1 - 0.5f) * 2f, 2.4f)), 0.6f);
                    float z1 = (t1 - 0.5f) * length;
                    for (int k = 0; k < 2; k++)
                    {
                        float k0 = k / 5f, k1 = (k + 1) / 5f;
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector3 P(float pl, float kk, float zz) => new Vector3(side * (beam * 0.5f * pl * MathF.Pow(kk, 0.55f) + 0.04f), Up + depth * kk, zz);
                            Ships.Along(b, P(plan, k0, z), P(plan1, k0, z1), P(plan1, k1, z1), P(plan, k1, z), new Vector3(side, -0.3f, 0f), k == 0 ? paint : fresh);
                        }
                    }
                }
            }
            for (int k = 0; k < 6; k++) Kit.Plank(b, new Vector3(beam + 0.5f, 0.05f + k * 0.05f, -length * 0.4f), new Vector3(beam + 0.5f, 0.05f + k * 0.05f, length * 0.4f), Vector3.UnitY, 0.22f, 0.04f, fresh);
        }

        /// <summary>Creels and pots stacked by the water, wicker over hoops with net at their ends, and the floats that mark where they're set, in the boats' paint.</summary>
        public static void Pots(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wicker = Straw(random, 0.75f), net = Net(random, 0.9f);
            int count = random.Int(5, 9);
            for (int k = 0; k < count; k++)
            {
                int row = k < 4 ? 0 : (k < 7 ? 1 : 2);
                int inRow = row == 0 ? Math.Min(count, 4) : (row == 1 ? Math.Min(count - 4, 3) : count - 7);
                int index = row == 0 ? k : (row == 1 ? k - 4 : k - 7);
                float x = (index - (inRow - 1) * 0.5f) * 0.72f;
                float y = row * 0.46f;
                Vector3 foot = new Vector3(x + random.Range(-0.04f, 0.04f), y, random.Range(-0.05f, 0.05f));
                // Its hoops along z and the wicker over them, a half cylinder on a flat base.
                for (int h = 0; h < 3; h++)
                {
                    float z = -0.35f + h * 0.35f;
                    var hoop = new List<Vector3>();
                    for (int a = 0; a <= 6; a++)
                    {
                        float angle = MathF.PI * a / 6f;
                        hoop.Add(foot + new Vector3(MathF.Cos(angle) * 0.32f, MathF.Sin(angle) * 0.42f, z));
                    }
                    Kit.Bend(b, hoop, 0.02f, 0.02f, wicker);
                }
                Kit.Lathe(b, foot + new Vector3(0f, 0.21f, -0.36f), Vector3.UnitZ, 0.72f, Kit.Profile((0f, 0.21f), (0.5f, 0.23f), (1f, 0.21f)), 7, 2, wicker);
                Kit.Face(b, foot + new Vector3(-0.3f, 0f, 0.37f), foot + new Vector3(0.3f, 0f, 0.37f), foot + new Vector3(0f, 0.4f, 0.37f), Vector3.UnitZ, net);
            }
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(random.Range(-1.4f, 1.4f), Ground + 0.12f, random.Range(0.6f, 1.1f));
                Kit.Lathe(b, at - Vector3.UnitY * 0.12f, Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.06f), (0.4f, 0.15f), (1f, 0.04f)), 8, 3, Painted(BoatPaints[random.Int(BoatPaints.Length)]));
            }
        }

        /// <summary>The capstan the boats are hauled up the shingle by: its drum on a timber frame staked down, its bars out, and its rope run out along +z to the water.</summary>
        public static void Capstan(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Weathered(random), dark = Timber(random, 0.7f), rope = Rope(random);
            foreach (float x in new[] { -0.9f, 0.9f }) Kit.Plank(b, new Vector3(x, Ground + 0.05f, -1.2f), new Vector3(x, Ground + 0.05f, 1.2f), Vector3.UnitY, 0.25f, 0.2f, dark);
            foreach (float z in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(-1.1f, Ground + 0.15f, z), new Vector3(1.1f, Ground + 0.15f, z), Vector3.UnitY, 0.2f, 0.18f, dark);
            Kit.Lathe(b, new Vector3(0f, Ground, 0f), Vector3.UnitY, 1.1f, Kit.Profile((0f, 0.42f), (0.2f, 0.32f), (0.7f, 0.3f), (0.85f, 0.4f), (1f, 0.38f)), 10, 4, wood);
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathF.PI * 0.5f + random.Range(-0.1f, 0.1f);
                Vector3 dir = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Kit.Pole(b, new Vector3(0f, Ground + 0.95f, 0f) + dir * 0.3f, new Vector3(0f, Ground + 0.95f, 0f) + dir * 1.8f, 0.05f, wood);
            }
            // The rope wound on it, and run out to the water.
            Kit.Lathe(b, new Vector3(0f, Ground + 0.3f, 0f), Vector3.UnitY, 0.35f, Kit.Profile((0f, 0.35f), (1f, 0.35f)), 10, 2, rope);
            Kit.Rope(b, new Vector3(0f, Ground + 0.45f, 0.35f), new Vector3(random.Range(-0.4f, 0.4f), Ground + 0.03f, 6f), 0.05f, 0.04f, rope, 6);
            foreach (float x in new[] { -1.3f, 1.3f }) Kit.Pole(b, new Vector3(x, -0.3f, -1.4f), new Vector3(x * 0.92f, Ground + 0.35f, -1.25f), 0.06f, dark);
        }
    }
}
