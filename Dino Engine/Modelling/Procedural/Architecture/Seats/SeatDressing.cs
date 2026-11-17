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
    /// What stands about any commander's seat (SEATS.md): the great bowls of fire round the yard its tyrant waits in, the Reich's long banners, the trough the tyrant's fed from and the ring
    /// it was chained by, what the commander has taken of the land heaped by the door, the piers a fence ends on at a gate, and the land's lord in stone, pulled down. The Ossuary's fires
    /// are its own: the cold green it raises bones with, the blue a ghost is called by, the sick yellow-green of its bile. Model +z is a piece's front. Headless.
    /// </summary>
    internal static class SeatDressing
    {
        public const int FireReich = 0, FireOssuary = 1, FireGhost = 2, FireBile = 3;

        public static readonly Vector3 OssuaryLight = new Vector3(0.35f, 1f, 0.82f);
        public static readonly Vector3 GhostLight = new Vector3(0.42f, 0.7f, 1f);
        public static readonly Vector3 BileLight = new Vector3(0.74f, 1f, 0.2f);

        /// <summary>The light of a fire of the Ossuary's, by whose it is.</summary>
        public static Vector3 ColdOf(int whose) => whose switch
        {
            FireGhost => GhostLight,
            FireBile => BileLight,
            _ => OssuaryLight,
        };

        /// <summary>Tongues of a light that nothing burns in, rising from <paramref name="at"/>: the Ossuary's fires, which give no heat.</summary>
        public static void ColdFlame(MeshBuilder b, Vector3 at, float size, Vector3 colour, WorldRandom random, int tongues = 5, float strength = 2.2f)
        {
            VertexMaterial glow = Glow(colour.X, colour.Y, colour.Z, strength);
            for (int k = 0; k < tongues; k++)
            {
                float a = k * MathF.Tau / tongues + random.Range(-0.3f, 0.3f), r = k == 0 ? 0f : size * random.Range(0.25f, 0.5f);
                Vector3 foot = at + new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
                Vector3 lean = Vector3.UnitY + new Vector3(random.Range(-0.18f, 0.18f), 0f, random.Range(-0.18f, 0.18f));
                float tall = size * (k == 0 ? 1.7f : random.Range(0.8f, 1.3f));
                Kit.Lathe(b, foot, lean, tall, Kit.Profile((0f, size * 0.2f), (0.3f, size * 0.26f), (1f, 0f)), 6, 3, glow);
            }
        }

        /// <summary>
        /// A great bowl of fire on a plinth of dressed stone, as stand round the yard a tyrant waits in, so that it's lit like a stage: the Reich's a bowl of black iron rimmed in brass,
        /// logs burning in it; the Ossuary's the same iron, and in it one of its cold lights.
        /// </summary>
        public static void GreatBrazier(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 1.6f, 5f);
            int whose = spec.Int(1);
            float plinth = height * 0.42f;
            Vector3 stone = whose == FireReich ? new Vector3(0.3f, 0.29f, 0.3f) : new Vector3(0.22f, 0.22f, 0.24f);
            Kit.Lathe(b, new Vector3(0f, -0.6f, 0f), Vector3.UnitY, plinth + 0.6f, Kit.Profile((0f, 1.3f), (0.2f, 1.22f), (0.22f, 1.08f), (0.8f, 1.02f), (0.82f, 1.2f), (1f, 1.2f)), 8, 6, Dressed(stone, random, 0.9f));
            VertexMaterial iron = Iron(random), brass = Brass(random);
            Kit.Lathe(b, new Vector3(0f, plinth, 0f), Vector3.UnitY, height - plinth, Kit.Profile((0f, 0.46f), (0.28f, 0.2f), (0.5f, 0.42f), (0.8f, 0.98f), (1f, 1.18f)), 12, 6, iron, openEnd: true);
            b.Tube(new Vector3(0f, height - 0.07f, 0f), new Vector3(0f, height + 0.05f, 0f), 1.2f, 1.2f, 12, brass, capped: false);
            // Rings to carry it by, as everything of the Reich's is made to be moved.
            foreach (float s in new[] { -1f, 1f }) b.Tube(new Vector3(s * 1.1f, height - 0.75f, -0.08f), new Vector3(s * 1.1f, height - 0.75f, 0.08f), 0.24f, 0.24f, 8, brass, capped: false);
            Vector3 bowl = new Vector3(0f, height - 0.3f, 0f);
            if (whose == FireReich)
            {
                Kit.Heap(b, bowl, new Vector2(0.98f), 0.22f, 0f, Coals, random, 2, 10);
                VertexMaterial charred = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.16f, 0.13f, 0.11f));
                for (int i = 0; i < 5; i++)
                {
                    float a = i * MathF.Tau / 5f + random.Range(-0.3f, 0.3f);
                    Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                    Kit.Pole(b, bowl + d * 0.95f + Vector3.UnitY * 0.12f, bowl - d * 0.25f + Vector3.UnitY * 0.8f, 0.11f, charred, 0.07f);
                }
                lights.Add(AssetLight.Burning(bowl + Vector3.UnitY * 0.2f, 1.5f));
                // Eight of these stand round a yard, and every metre a light reaches is paid for in every pixel there: 25 m each was six times the keep's light pass.
                lights.Add(AssetLight.Campfire(bowl + Vector3.UnitY * 1.3f) with { Intensity = 135f, Radius = 19f });
                return;
            }
            Vector3 cold = ColdOf(whose);
            Kit.Heap(b, bowl, new Vector2(0.98f), 0.14f, 0f, Glow(cold.X, cold.Y, cold.Z, 1.5f), random, 2, 10);
            ColdFlame(b, bowl + Vector3.UnitY * 0.1f, 1f, cold, random);
            lights.Add(AssetLight.Glow(bowl + Vector3.UnitY * 1.1f, cold, 80f, 21f) with { Flicker = 0.14f, FlickerSpeed = 2.2f });
        }

        /// <summary>
        /// One of the Reich's long banners: on a standard of its own, a mast of black iron on a stone foot with a brass point, the cloth hung from a yard across it; or hung on a bar off a
        /// wall behind it (the wall at its z = 0). It hangs towards +z, and swings.
        /// </summary>
        public static void LongBanner(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 3f, 16f), width = Math.Clamp(spec.Metres(1), 0.8f, 4f), drop = Math.Clamp(spec.Metres(2), 1.5f, 12f);
            VertexMaterial iron = Iron(random), brass = Brass(random);
            float hw = width * 0.5f;
            if (spec.Int(3) == 1)
            {
                Kit.Pole(b, new Vector3(-hw - 0.2f, height, 0.45f), new Vector3(hw + 0.2f, height, 0.45f), 0.06f, iron);
                foreach (float s in new[] { -1f, 1f })
                {
                    Kit.Pole(b, new Vector3(s * (hw + 0.1f), height, -0.1f), new Vector3(s * (hw + 0.1f), height, 0.45f), 0.045f, iron);
                    Kit.Pole(b, new Vector3(s * (hw + 0.1f), height - 0.7f, -0.05f), new Vector3(s * (hw + 0.1f), height, 0.4f), 0.035f, iron);
                }
                SeatKit.Banner(b, new Vector3(0f, height - 0.05f, 0.45f), Vector3.UnitZ, width, drop, random);
                return;
            }
            Kit.Span(b, new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.55f, 0.5f), 0.06f, Dressed(new Vector3(0.3f, 0.29f, 0.3f), random, 0.5f));
            Kit.Pole(b, new Vector3(0f, 0.5f, 0f), new Vector3(0f, height, 0f), 0.13f, iron, 0.09f);
            b.Tube(new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0.85f, 0f), 0.19f, 0.15f, 8, brass, capped: false);
            Kit.Pole(b, new Vector3(-hw - 0.25f, height - 0.5f, 0.16f), new Vector3(hw + 0.25f, height - 0.5f, 0.16f), 0.055f, iron);
            foreach (float s in new[] { -1f, 1f }) Kit.Lathe(b, new Vector3(s * (hw + 0.25f), height - 0.5f, 0.16f), new Vector3(s, 0f, 0f), 0.22f, Kit.Profile((0f, 0.09f), (1f, 0f)), 6, 1, brass);
            Kit.Lathe(b, new Vector3(0f, height, 0f), Vector3.UnitY, 0.8f, Kit.Profile((0f, 0.11f), (0.3f, 0.2f), (0.5f, 0.1f), (1f, 0f)), 6, 3, brass);
            SeatKit.Banner(b, new Vector3(0f, height - 0.55f, 0.24f), Vector3.UnitZ, width, drop, random);
        }

        /// <summary>The trough a tyrant's fed from, along z: riveted iron on stone bearers, and round it the ground dark with what's been spilled.</summary>
        public static void FeedTrough(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 9f);
            Fortress.Trough(b, Vector3.Zero, 0f, length, random);
            Kit.Heap(b, new Vector3(0.6f, Ground - 0.02f, length * 0.2f), new Vector2(2.4f, length * 0.6f), 0.05f, 0.3f, Flat(new Vector3(0.13f, 0.03f, 0.025f)), random, 2, 12);
            VertexMaterial bone = Bone(random, 0.95f);
            for (int k = 0; k < 5; k++)
            {
                Vector3 at = new Vector3(random.Range(1.3f, 2.8f) * (random.Chance(0.5f) ? 1f : -1f), Ground + 0.09f, random.Range(-length * 0.5f, length * 0.5f));
                float a = random.Float() * MathF.Tau, l = random.Range(0.5f, 1.3f);
                Kit.Pole(b, at, at + new Vector3(MathF.Cos(a) * l, 0f, MathF.Sin(a) * l), 0.08f, bone, 0.05f);
            }
        }

        /// <summary>
        /// The ring a tyrant was chained by, in the middle of its yard: a block of stone set in the ground, a staple of iron through it, the ring, and its chain run out along +z to
        /// where it broke, every link as long as a forearm. Nothing of it stands as high as a knee.
        /// </summary>
        public static void YardRing(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 12f);
            VertexMaterial iron = Iron(random), rust = Rusted(random);
            Kit.Span(b, new Vector3(-1.1f, -0.6f, -1.1f), new Vector3(1.1f, Ground + 0.22f, 1.1f), 0.07f, Dressed(new Vector3(0.3f, 0.29f, 0.3f), random, 0.3f));
            float y = Ground + 0.34f;
            var staple = new List<Vector3> { new Vector3(-0.4f, Ground + 0.1f, 0f), new Vector3(-0.3f, y + 0.4f, 0f), new Vector3(0.3f, y + 0.4f, 0f), new Vector3(0.4f, Ground + 0.1f, 0f) };
            Kit.Bend(b, staple, 0.11f, 0.11f, iron);
            const int Sides = 12;
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                Vector3 c = new Vector3(0f, y + 0.12f, 0.75f);
                b.Tube(c + new Vector3(MathF.Cos(a0) * 0.62f, 0.25f * MathF.Sin(a0), MathF.Sin(a0) * 0.62f), c + new Vector3(MathF.Cos(a1) * 0.62f, 0.25f * MathF.Sin(a1), MathF.Sin(a1) * 0.62f), 0.1f, 0.1f, 6, iron, capped: false);
            }
            // The chain, wandering a little as it lies, and its last link snapped open.
            Vector3 from = new Vector3(0f, y, 1.5f);
            int runs = Math.Max(2, (int)(length / 2.2f));
            for (int i = 0; i < runs; i++)
            {
                Vector3 to = new Vector3(random.Range(-0.9f, 0.9f) + (i + 1) * 0.25f, y, 1.5f + (i + 1) * (length / runs));
                Kit.Chain(b, from, to, 0.02f, 0.58f, i == runs - 1 ? rust : iron);
                from = to;
            }
            var snapped = new List<Vector3> { from, from + new Vector3(0.3f, 0.05f, 0.35f), from + new Vector3(0.1f, 0.1f, 0.7f) };
            Kit.Bend(b, snapped, 0.075f, 0.06f, rust);
        }

        /// <summary>
        /// What a commander has taken of the land, heaped by the door until the ships come for it: sacks of the tithe's grain, chests with the Reich's iron on them, one open on what's in
        /// it, a bell out of somewhere's tower, rolls of cloth in the land's colours, and the candlesticks off somebody's table.
        /// </summary>
        public static void Hoard(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float size = Math.Clamp(spec.Metres(0), 2f, 6f);
            VertexMaterial sack = Canvas(random, 0.95f), wood = Timber(random, 0.85f), iron = Iron(random);
            int sacks = (int)(size * 2.2f);
            for (int k = 0; k < sacks; k++)
            {
                float a = random.Float() * MathF.Tau, r = size * 0.5f * MathF.Sqrt(random.Float());
                bool top = k > sacks / 2;
                Kit.Sack(b, new Vector3(MathF.Cos(a) * r - size * 0.22f, Ground + (top ? 0.42f : 0f), MathF.Sin(a) * r * 0.7f), random.Float() * MathF.Tau, top || random.Chance(0.4f), random.Range(0.62f, 0.8f), sack);
            }
            // Chests, one of them open.
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(size * (0.18f + 0.26f * k) - size * 0.1f, Ground + 0.36f, random.Range(-0.4f, 0.6f) + (k == 1 ? 0.7f : 0f));
                float yaw = random.Range(-0.4f, 0.4f);
                Kit.Crate(b, at, yaw, new Vector3(0.62f, 0.36f, 0.42f), wood, iron, random, k == 2);
                if (k != 1) continue;
                // Its lid thrown back, and the gold in it.
                float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
                Vector3 ax = new Vector3(c, 0f, -s), az = new Vector3(s, 0f, c);
                Kit.Box(b, at + Vector3.UnitY * 0.62f - az * 0.5f, ax, new Vector3(0f, 0.35f, 0f) - az * 0.94f, new Vector3(0f, 0.94f, 0f) + az * 0.35f, new Vector3(0.62f, 0.05f, 0.42f), 0.01f, wood);
                Kit.Heap(b, at + Vector3.UnitY * 0.3f, new Vector2(0.5f, 0.32f), 0.14f, yaw, Glow(1f, 0.7f, 0.18f, 0.55f), random, 2, 8);
            }
            // A bell on its side, green with its years.
            VertexMaterial bronze = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.24f, 0.3f, 0.22f));
            Vector3 bell = new Vector3(-size * 0.42f, Ground + 0.62f, size * 0.3f);
            Kit.Lathe(b, bell, new Vector3(0.8f, 0.12f, 0.58f), 1.15f, Kit.Profile((0f, 0.14f), (0.12f, 0.3f), (0.6f, 0.42f), (0.9f, 0.58f), (1f, 0.66f)), 10, 5, bronze, openEnd: true);
            // Rolls of cloth, in the colours the land dyes.
            Vector3[] dyes = { new Vector3(0.14f, 0.2f, 0.34f), new Vector3(0.18f, 0.28f, 0.16f), new Vector3(0.5f, 0.34f, 0.1f), new Vector3(0.42f, 0.4f, 0.34f) };
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = new Vector3(random.Range(-size * 0.3f, size * 0.4f), Ground + 0.16f + (k > 1 ? 0.3f : 0f), -size * 0.3f + k * 0.1f);
                float a = random.Range(-0.3f, 0.3f), l = random.Range(1.4f, 2.1f);
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Kit.Pole(b, at - d * (l * 0.5f), at + d * (l * 0.5f), 0.17f, new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(dyes[k] * random.Range(0.9f, 1.1f))));
            }
            // Candlesticks, fallen.
            VertexMaterial pewter = new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.36f, 0.36f, 0.34f));
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(random.Range(-size * 0.2f, size * 0.5f), Ground + 0.08f, random.Range(size * 0.25f, size * 0.5f));
                float a = random.Float() * MathF.Tau;
                Kit.Lathe(b, at, new Vector3(MathF.Cos(a), 0.05f, MathF.Sin(a)), 0.7f, Kit.Profile((0f, 0.16f), (0.1f, 0.05f), (0.8f, 0.04f), (0.9f, 0.1f), (1f, 0.08f)), 6, 5, pewter);
            }
        }

        /// <summary>How far each way from its middle a fallen lord's plinth stands, and how high.</summary>
        public const float PlinthHalf = 1.7f, PlinthHigh = 2.25f;

        /// <summary>
        /// The land's lord in stone, pulled down: the plinth still standing with his feet on it and the Reich's mark hung over his name, and the rest of him lying where the ropes left
        /// him, broken at the waist and the neck, his sword arm apart from him and his head rolled off, the ropes still round his shoulders.
        /// </summary>
        public static void FallenLord(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 4f, 9f);
            Vector3 stone = SeatKit.StoneOf(spec, 1);
            VertexMaterial figure = SeatKit.Ashlar(stone, random, 3f, 1.12f);
            for (int i = 0; i < 3; i++)
            {
                float half = PlinthHalf - i * 0.2f;
                Kit.Span(b, new Vector3(-half, i * 0.75f - (i == 0 ? 0.6f : 0f), -half), new Vector3(half, (i + 1) * 0.75f - 0.012f, half), 0.06f, SeatKit.AshlarFacing(stone, random, i * 0.75f));
            }
            foreach (float s in new[] { -1f, 1f }) Kit.Lathe(b, new Vector3(s * 0.4f, PlinthHigh - 0.02f, 0.1f), Vector3.UnitY, random.Range(0.45f, 0.8f), Kit.Profile((0f, 0.3f), (1f, 0.24f)), 8, 1, figure);
            Kit.Mark(b, new Vector3(0f, 1.15f, PlinthHalf - 0.16f), Vector3.UnitX, Vector3.UnitY, 1.25f, 0.04f, random);

            // The figure, lying away from the plinth on his back: built round, along z in a frame of his own, and laid flat, as a man lying down is broader than he's deep and nothing
            // of him may stand over a knee.
            float turn = random.Range(0.45f, 1f) * (random.Chance(0.5f) ? 1f : -1f);
            Vector3 dir = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn)), side = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn));
            const float Squash = 0.6f, Axis = 0.98f;
            float legs = length * 0.48f, torso = length * 0.34f;
            b.Place(dir * 2.6f + new Vector3(0f, Ground, 0f), turn);
            b.Stretch = new Vector3(1f, Squash, 1f);
            Vector3 Y = Vector3.UnitY, Z = Vector3.UnitZ, X = Vector3.UnitX;
            Kit.Lathe(b, new Vector3(0f, Axis, 0f), Z, legs, Kit.Profile((0f, 0.46f), (0.25f, 0.74f), (1f, 0.86f)), 12, 4, figure);
            // Broken at the waist, and what's above it lying a little askew.
            float skew = random.Range(0.14f, 0.3f) * MathF.Sign(turn);
            Vector3 up = new Vector3(MathF.Sin(skew), 0f, MathF.Cos(skew)), across = new Vector3(MathF.Cos(skew), 0f, -MathF.Sin(skew));
            Vector3 waist = new Vector3(0.3f * MathF.Sign(turn), Axis, legs + 0.55f);
            Kit.Lathe(b, waist, up, torso, Kit.Profile((0f, 0.84f), (0.45f, 0.9f), (0.8f, 0.96f), (1f, 0.42f)), 12, 4, figure);
            Kit.Box(b, waist + up * (torso * 0.82f), across, Y, up, new Vector3(1.5f, 0.5f, 0.5f), 0.16f, figure);
            // An arm still on him, along his side; the other, with the sword, lying apart from him.
            Kit.Pole(b, waist + up * (torso * 0.8f) + across * 1.42f, waist + up * (torso * 0.06f) + across * 1.2f, 0.3f, figure, 0.24f);
            Vector3 arm = waist + up * (torso * 0.45f) - across * 3.4f - Y * 0.55f;
            Kit.Pole(b, arm, arm + Z * 1.9f, 0.3f, figure, 0.24f);
            Kit.Box(b, arm + Z * 3.9f - Y * 0.1f, X, Y, Z, new Vector3(0.2f, 0.12f, 2f), 0.03f, figure);
            Kit.Box(b, arm + Z * 1.95f - Y * 0.1f, X, Y, Z, new Vector3(0.7f, 0.14f, 0.14f), 0.03f, figure);
            // His head, rolled off, the circlet still on it.
            Vector3 head = waist + up * (torso + 1.9f) + across * random.Range(1f, 2f) * -MathF.Sign(turn) - Y * 0.14f;
            Kit.Ball(b, head, new Vector3(0.72f, 0.78f, 0.74f), 10, 6, figure);
            b.Tube(head + new Vector3(0f, 0.3f, 0.5f), head + new Vector3(0f, 0.36f, 0.72f), 0.64f, 0.6f, 10, figure, capped: false);
            for (int k = 0; k < 5; k++)
            {
                float a = (k - 2) * 0.5f;
                Kit.Box(b, head + new Vector3(MathF.Sin(a) * 0.62f, 0.34f + MathF.Cos(a) * 0.3f, 0.86f), X, Y, Z, new Vector3(0.1f, 0.1f, 0.2f), 0.02f, figure);
            }
            b.ResetPlacement();
            // The ropes he was pulled down with, still on him.
            VertexMaterial rope = Rope(random);
            Vector3 chest = dir * (2.6f + legs + 0.55f + torso * 0.7f) + new Vector3(0f, Ground + 1.05f, 0f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = chest + side * ((k - 1) * 0.5f);
                Vector3 end = at + dir * random.Range(3f, 5f) + side * ((k - 1) * random.Range(1f, 2f));
                end.Y = Ground + 0.05f;
                Kit.Rope(b, at, end, 0.3f, 0.05f, rope, 5);
            }
            // What broke off him.
            VertexMaterial chips = SeatKit.Ashlar(stone, random, 0.4f, 0.95f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 at = dir * random.Range(1.4f, 3.4f + legs) + side * random.Range(-1.8f, 1.8f);
                at.Y = Ground;
                Kit.Heap(b, at, new Vector2(random.Range(0.4f, 0.9f)), random.Range(0.12f, 0.25f), random.Float(), chips, random, 2, 7);
            }
        }

        /// <summary>
        /// A pier of the Reich's building, where a fence of its iron ends at a gate: dark granite in courses, or brick, an iron cap, and on it a lantern or a bowl of fire. Its mark is on
        /// the face towards +z.
        /// </summary>
        public static void GatePier(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 2.5f, 9f);
            int form = spec.Int(1);
            bool brick = spec.Int(2) == 1;
            VertexMaterial iron = Iron(random), brass = Brass(random);
            if (brick)
            {
                VertexMaterial bricks = new VertexMaterial(MaterialCatalog.Brick.Index, new Colour(new Vector3(0.36f, 0.3f, 0.28f) * random.Range(0.94f, 1.05f)));
                SeatKit.WallBox(b, new Vector3(-0.85f, -0.6f, -0.85f), new Vector3(0.85f, height, 0.85f), bricks);
                Kit.Span(b, new Vector3(-1.02f, -0.6f, -1.02f), new Vector3(1.02f, 0.9f, 1.02f), 0.05f, Dressed(new Vector3(0.27f, 0.27f, 0.28f), random, 0.6f));
            }
            else
            {
                int courses = Math.Max(3, (int)(height / 0.8f));
                float course = height / courses;
                for (int i = 0; i < courses; i++)
                {
                    float half = 0.85f + (i == 0 ? 0.16f : i % 2 == 0 ? 0.04f : 0f);
                    float y0 = i * course - (i == 0 ? 0.6f : 0f);
                    Kit.Span(b, new Vector3(-half, y0, -half), new Vector3(half, (i + 1) * course - 0.012f, half), 0.05f, Dressed(new Vector3(0.3f, 0.29f, 0.3f) * random.Range(0.94f, 1.06f), random, y0));
                }
            }
            Kit.Span(b, new Vector3(-1.05f, height, -1.05f), new Vector3(1.05f, height + 0.26f, 1.05f), 0.03f, iron);
            Kit.Mark(b, new Vector3(0f, height * 0.62f, 0.9f), Vector3.UnitX, Vector3.UnitY, 1.05f, 0.04f, random);
            Vector3 top = new Vector3(0f, height + 0.26f, 0f);
            if (form == 1)
            {
                Kit.Lathe(b, top, Vector3.UnitY, 0.8f, Kit.Profile((0f, 0.3f), (0.3f, 0.2f), (0.6f, 0.55f), (1f, 0.8f)), 10, 4, iron, openEnd: true);
                Kit.Heap(b, top + Vector3.UnitY * 0.6f, new Vector2(0.68f), 0.16f, 0f, Coals, random, 2, 8);
                lights.Add(AssetLight.Burning(top + Vector3.UnitY * 0.75f, 1f));
                lights.Add(AssetLight.Campfire(top + Vector3.UnitY * 1.6f) with { Intensity = 110f, Radius = 21f });
                return;
            }
            // A lantern on an arm of iron, out over the way in.
            Kit.Pole(b, top, top + Vector3.UnitY * 1.5f, 0.07f, iron);
            Kit.Pole(b, top + Vector3.UnitY * 1.4f, top + new Vector3(0f, 1.4f, 1.1f), 0.05f, iron);
            Kit.Pole(b, top + Vector3.UnitY * 0.6f, top + new Vector3(0f, 1.38f, 0.7f), 0.035f, iron);
            Kit.Lantern(b, top + new Vector3(0f, 1.38f, 1.05f), brass, lights, 1.6f);
        }
    }
}
