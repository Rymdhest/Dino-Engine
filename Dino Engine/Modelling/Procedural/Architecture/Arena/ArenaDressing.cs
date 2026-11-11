using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Arena
{
    /// <summary>
    /// What stands about an arena and says whose it is: square pylons of dressed stone carrying braziers or the Reich's banner, braziers of black iron and brass, masts with the banner hung from a crossbar,
    /// the bay a waygate stands in, flights of steps down the stands, and lamps on their own. Model y = 0 is where people stand at its foot, and +z is its front. Headless.
    /// </summary>
    internal static class ArenaDressing
    {
        private const float Foot = -0.5f;

        // ---------------------------------------------------------------- Pylons

        /// <summary>
        /// A pylon: a plinth, a square shaft in courses that narrows a little as it rises, a band of a deeper course near its head and a capital slab, with the mark in brass on its front, and on top a
        /// brazier (<paramref name="crown"/> 1) or a pole carrying the Reich's banner (2).
        /// </summary>
        public static void Pylon(MeshBuilder b, ArenaStoneKind stone, float width, float height, int crown, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("pylon");
            float half = width * 0.5f;
            const float PlinthHigh = 0.75f, Cap = 0.34f;
            ArenaStone.Block(b, new Vector3(0f, (Foot + PlinthHigh) * 0.5f, 0f), new Vector3(half + 0.18f, (PlinthHigh - Foot) * 0.5f, half + 0.18f), 0f, ArenaStone.BigChamfer, ArenaStone.DressedFacing(stone, random.Fork("plinth"), 0f));
            ArenaStone.Block(b, new Vector3(0f, PlinthHigh + 0.06f, 0f), new Vector3(half + 0.08f, 0.06f, half + 0.08f), 0f, 0.03f, ArenaStone.DressedFacing(stone, random.Fork("base mould"), PlinthHigh));

            float shaftFrom = PlinthHigh + 0.12f, shaftTo = height - Cap - 0.5f;
            int courses = Math.Max(3, (int)MathF.Round((shaftTo - shaftFrom) / 0.55f));
            float rise = (shaftTo - shaftFrom) / courses;
            for (int c = 0; c < courses; c++)
            {
                float y0 = shaftFrom + c * rise;
                float taper = 1f - 0.06f * (y0 - shaftFrom) / MathF.Max(shaftTo - shaftFrom, 0.1f);
                float h = half * taper;
                // Long and short in turn on each face, as quoins are, so no joint runs up a corner.
                if (c % 2 == 0)
                {
                    ArenaStone.Block(b, new Vector3(0f, y0 + rise * 0.5f, 0f), new Vector3(h, rise * 0.5f - 0.004f, h), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(c), y0));
                }
                else
                {
                    foreach (float s in new[] { -1f, 1f })
                    {
                        ArenaStone.Block(b, new Vector3(s * h * 0.5f, y0 + rise * 0.5f, 0f), new Vector3(h * 0.5f - 0.004f, rise * 0.5f - 0.004f, h), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(c * 7 + (s > 0f ? 3 : 5)), y0));
                    }
                }
            }
            float bandAt = shaftTo;
            float headHalf = half * 0.95f;
            ArenaStone.Block(b, new Vector3(0f, bandAt + 0.25f, 0f), new Vector3(headHalf + 0.06f, 0.25f, headHalf + 0.06f), 0f, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork("band"), bandAt));
            // Its cap, in four slabs, as one face of rock that size reads as crazy paving.
            float capHalf = headHalf + 0.22f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    ArenaStone.Block(b, new Vector3(sx * capHalf * 0.5f, height - Cap * 0.5f, sz * capHalf * 0.5f), new Vector3(capHalf * 0.5f - 0.006f, Cap * 0.5f, capHalf * 0.5f - 0.006f), 0f, ArenaStone.Chamfer,
                        ArenaStone.DressedFacing(stone, random.Fork(sx > 0f ? sz > 0f ? "cap a" : "cap b" : sz > 0f ? "cap c" : "cap d"), height, 0.2f));
                }
            }

            // The mark on its front, in brass, a little below the band.
            float markY = bandAt - 0.75f;
            float frontZ = half * (1f - 0.06f * (markY - shaftFrom) / MathF.Max(shaftTo - shaftFrom, 0.1f));
            ArenaStone.Mark(b, new Vector3(0f, markY, frontZ + 0.006f), Vector3.UnitX, Vector3.UnitY, width * 0.74f, 0.02f, ArenaStone.Brass(random.Fork("mark")), ArenaStone.Hearting(stone));

            Vector3 top = new Vector3(0f, height, 0f);
            if (crown == 1) BrazierOn(b, top, MathF.Min(width * 0.62f, 1.2f), random.Fork("brazier"), lights);
            if (crown == 2) BannerPole(b, top, height, random.Fork("banner"));
        }

        // A brass bowl on a squat iron ring, coals in it and its fire burning above, and the light it throws.
        private static void BrazierOn(MeshBuilder b, Vector3 at, float radius, WorldRandom random, List<AssetLight> lights)
        {
            VertexMaterial iron = ArenaStone.Iron(random);
            VertexMaterial brass = ArenaStone.Brass(random);
            b.Tube(at, at + Vector3.UnitY * 0.22f, radius * 0.5f, radius * 0.42f, 10, iron);
            for (int k = 0; k < 4; k++)
            {
                float a = k * MathF.PI * 0.5f + MathF.PI * 0.25f;
                Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                b.Tube(at + d * radius * 0.4f + Vector3.UnitY * 0.1f, at + d * radius * 0.92f + Vector3.UnitY * 0.5f, 0.05f, 0.04f, 4, iron);
            }
            Bowl(b, at + Vector3.UnitY * 0.2f, radius, 0.55f, brass, iron);
            Vector3 fire = at + Vector3.UnitY * 0.62f;
            lights.Add(AssetLight.Burning(fire, radius * 1.35f));
            lights.Add(ArenaStone.BrazierLight(fire + Vector3.UnitY * 0.9f));
        }

        // A bowl rising from its foot to a rim of `radius`, coals heaped in it, and a band of iron round its rim.
        private static void Bowl(MeshBuilder b, Vector3 foot, float radius, float depth, VertexMaterial brass, VertexMaterial iron)
        {
            b.Tube(foot, foot + Vector3.UnitY * depth * 0.35f, radius * 0.3f, radius * 0.78f, 14, brass);
            b.Tube(foot + Vector3.UnitY * depth * 0.35f, foot + Vector3.UnitY * depth, radius * 0.78f, radius, 14, brass, capped: false);
            b.Tube(foot + Vector3.UnitY * (depth - 0.05f), foot + Vector3.UnitY * (depth + 0.03f), radius * 1.03f, radius * 1.03f, 14, iron, capped: false);
            b.Tube(foot + Vector3.UnitY * depth * 0.55f, foot + Vector3.UnitY * (depth * 0.82f), radius * 0.9f, radius * 0.55f, 10, ArenaStone.Coals);
            // Coals heaped on the embers, dark, so that what glows is what's between them.
            var heap = new Dino_Engine.World.WorldRandom((uint)(radius * 1000f) ^ 0xC0A1u);
            int coals = Math.Max(5, (int)(radius * 14f));
            for (int i = 0; i < coals; i++)
            {
                float a = heap.Float() * MathF.Tau, r = MathF.Sqrt(heap.Float()) * radius * 0.72f;
                Vector3 at = foot + new Vector3(MathF.Cos(a) * r, depth * (0.8f - 0.18f * r / radius) + heap.Range(-0.02f, 0.04f), MathF.Sin(a) * r);
                float size = radius * heap.Range(0.1f, 0.18f);
                Dino_Engine.Modelling.Procedural.Architecture.Waygates.Masonry.Box(b, at - new Vector3(size, size * 0.6f, size), at + new Vector3(size, size * 0.6f, size), size * 0.3f, ArenaStone.Coal);
            }
        }

        // A pole of iron on a pylon's head with a crossbar near its top, the Reich's banner hung from that, and a brass spike over it all.
        private static void BannerPole(MeshBuilder b, Vector3 at, float pylonHeight, WorldRandom random)
        {
            VertexMaterial iron = ArenaStone.Iron(random);
            float pole = 5.2f;
            b.Tube(at, at + Vector3.UnitY * pole, 0.09f, 0.07f, 6, iron);
            b.Tube(at + Vector3.UnitY * pole, at + Vector3.UnitY * (pole + 0.7f), 0.12f, 0.0f, 6, ArenaStone.Brass(random));
            Vector3 bar = at + Vector3.UnitY * (pole - 0.3f) + Vector3.UnitZ * 0.16f;
            ArenaWalls.HungBanner(b, bar, Vector3.UnitZ, 1.5f, MathF.Min(pole + pylonHeight * 0.35f, 5.4f), random);
        }

        // ---------------------------------------------------------------- Braziers

        /// <summary>A brazier on its own: an iron tripod with a brass bowl (<paramref name="form"/> 0), or a brass bowl on a squat drum of stone (1), <paramref name="size"/> times the usual.</summary>
        public static void Brazier(MeshBuilder b, int form, float size, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("brazier");
            VertexMaterial iron = ArenaStone.Iron(random);
            VertexMaterial brass = ArenaStone.Brass(random);
            float s = size;
            float rim = 0.75f * s;
            Vector3 bowlFoot;
            if (form == 1)
            {
                ArenaStone.Block(b, new Vector3(0f, (Foot + 0.4f * s) * 0.5f, 0f), new Vector3(0.62f, (0.4f * s - Foot) * 0.5f, 0.62f) * new Vector3(s, 1f, s), MathF.PI * 0.25f, ArenaStone.BigChamfer,
                    ArenaStone.DressedFacing(ArenaStoneKind.Basalt, random.Fork("drum"), 0f));
                b.Tube(new Vector3(0f, 0.4f * s, 0f), new Vector3(0f, 1.1f * s, 0f), 0.42f * s, 0.32f * s, 8, ArenaStone.Dressed(ArenaStoneKind.Basalt, random.Fork("shaft"), 0.5f));
                bowlFoot = new Vector3(0f, 1.1f * s, 0f);
            }
            else
            {
                float top = 1.55f * s;
                for (int k = 0; k < 3; k++)
                {
                    float a = k * MathF.Tau / 3f;
                    Vector3 d = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                    // Each leg splays out to a clawed foot.
                    Vector3 head = new Vector3(0f, top, 0f) + d * 0.22f * s;
                    Vector3 knee = new Vector3(0f, top * 0.45f, 0f) + d * 0.48f * s;
                    Vector3 foot = d * 0.62f * s + Vector3.UnitY * 0.04f;
                    b.Tube(head, knee, 0.055f * s, 0.05f * s, 5, iron);
                    b.Tube(knee, foot, 0.05f * s, 0.045f * s, 5, iron);
                    b.Tube(foot, foot + d * 0.16f * s - Vector3.UnitY * 0.04f, 0.05f * s, 0.01f, 4, iron);
                }
                b.Tube(new Vector3(0f, top * 0.42f, 0f), new Vector3(0f, top * 0.48f, 0f), 0.5f * s, 0.5f * s, 10, iron, capped: false);
                bowlFoot = new Vector3(0f, top - 0.1f * s, 0f);
            }
            Bowl(b, bowlFoot, rim, 0.6f * s, brass, iron);
            Vector3 fire = bowlFoot + Vector3.UnitY * 0.6f * s;
            lights.Add(AssetLight.Burning(fire, rim * 1.3f));
            lights.Add(ArenaStone.BrazierLight(fire + Vector3.UnitY * 0.9f));
        }

        // ---------------------------------------------------------------- Banners

        /// <summary>
        /// The Reich's banner: on a mast of timber bound in iron, on a footing of dressed stone, hung from a crossbar near its top (<paramref name="form"/> 0), or hung on its own from a rod at y = 0 against
        /// a wall's face (1). It faces +z.
        /// </summary>
        public static void Banner(MeshBuilder b, float mast, float length, float width, int form, uint seed)
        {
            var random = new WorldRandom(seed).Fork("banner");
            if (form == 1)
            {
                ArenaWalls.HungBanner(b, Vector3.Zero, Vector3.UnitZ, width, length, random);
                return;
            }
            VertexMaterial iron = ArenaStone.Iron(random);
            ArenaStone.Block(b, new Vector3(0f, (Foot + 0.6f) * 0.5f, 0f), new Vector3(0.5f, (0.6f - Foot) * 0.5f, 0.5f), 0f, ArenaStone.BigChamfer, ArenaStone.DressedFacing(ArenaStoneKind.Basalt, random.Fork("footing"), 0.3f));
            b.Tube(new Vector3(0f, 0.6f, 0f), new Vector3(0f, mast, 0f), 0.17f, 0.12f, 8, ArenaStone.Timber(random, 0.8f));
            for (float y = 1.2f; y < mast - 0.6f; y += 2.1f) b.Tube(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.12f, 0f), 0.185f, 0.18f, 8, iron, capped: false);
            b.Tube(new Vector3(0f, mast, 0f), new Vector3(0f, mast + 0.8f, 0f), 0.13f, 0f, 6, ArenaStone.Brass(random));
            Vector3 bar = new Vector3(0f, mast - 0.45f, 0.2f);
            b.Tube(new Vector3(0f, mast - 0.45f, 0.02f), bar + Vector3.UnitZ * 0.05f, 0.06f, 0.06f, 4, iron);
            ArenaWalls.HungBanner(b, bar, Vector3.UnitZ, width, length, random);
        }

        // ---------------------------------------------------------------- The waygate's bay

        /// <summary>
        /// The bay a waygate stands in, in an arena's wall: its back wall and two sides of dressed stone, open towards +z (the floor) and to the sky, the sides ending in squared piers a little taller than
        /// the walls, each carrying a brazier. The waygate itself is the land's, and stands in it.
        /// </summary>
        public static void Bay(MeshBuilder b, ArenaStoneKind stone, float width, float depth, float height, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("bay");
            float half = width * 0.5f;
            const float Thick = 1.4f;
            // The back, its face towards +z at -depth.
            b.Place(new Vector3(-half, 0f, -depth), 0f);
            ArenaWalls.Build(b, WallLine.Straight(width), ArenaWallForm.Podium, stone, height, Thick, height - 0.9f, ArenaTrim.Pilasters, seed + 3u, lights);
            b.ResetPlacement();
            // The sides, each facing in across the bay.
            foreach (float side in new[] { -1f, 1f })
            {
                // On +x the wall's face looks towards -x, so it runs towards +z from the back; on -x it looks towards +x and runs back from the mouth.
                if (side > 0f) b.Place(new Vector3(half, 0f, -depth), -MathF.PI * 0.5f);
                else b.Place(new Vector3(-half, 0f, 0f), MathF.PI * 0.5f);
                // Whatever's beside a bay steps up the tiers along it, so its sides hold nothing up and are finished on both faces.
                ArenaWalls.Build(b, WallLine.Straight(depth), ArenaWallForm.Podium, stone, height, Thick, -1f, ArenaTrim.None, seed + (side > 0f ? 11u : 23u), lights);
                b.ResetPlacement();
            }
        }

        /// <summary>Where a bay's two braziers stand, on the floor at its mouth, either side, in its own frame.</summary>
        public static IEnumerable<Vector3> BayBraziers(float width) => new[] { new Vector3(-width * 0.5f + 0.9f, 0f, 1.4f), new Vector3(width * 0.5f - 0.9f, 0f, 1.4f) };

        // ---------------------------------------------------------------- Steps

        /// <summary>A flight of steps rising towards -z, for looking at: each step a row of long blocks, nosings a little proud, between low cheeks.</summary>
        public static void Stair(MeshBuilder b, ArenaStoneKind stone, float width, int count, float rise, float tread, uint seed)
        {
            var random = new WorldRandom(seed).Fork("stair");
            float half = width * 0.5f;
            for (int i = 0; i < count; i++)
            {
                WorldRandom step = random.Fork(i);
                float front = -i * tread + 0.03f, back = -(i + 1) * tread - 0.3f;
                float top = ArenaStone.Ground + (i + 1) * rise, bottom = ArenaStone.Ground + i * rise - 0.32f;
                float x = -half - step.Range(0f, 0.9f);
                while (x < half)
                {
                    float piece = step.Range(1.2f, 2f);
                    float x0 = MathF.Max(x, -half), x1 = MathF.Min(x + piece, half);
                    if (x1 - x0 > 0.25f) PlazaWalls.Lay(b, PlazaPieces.Corners(new Vector3(x0 + 0.006f, bottom, front), new Vector3(x1 - 0.006f, top, back)), 0.045f, ArenaStone.DressedFacing(stone, step.Fork((int)(x * 10f)), top, 0.16f));
                    x += piece;
                }
            }
            float length = count * tread;
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side * half, x1 = side * (half + 0.42f);
                PlazaWalls.Lay(b, new[]
                {
                    new Vector3(MathF.Min(x0, x1), -0.4f, 0.1f), new Vector3(MathF.Max(x0, x1), -0.4f, 0.1f), new Vector3(MathF.Max(x0, x1), ArenaStone.Ground + 0.35f, 0.1f), new Vector3(MathF.Min(x0, x1), ArenaStone.Ground + 0.35f, 0.1f),
                    new Vector3(MathF.Min(x0, x1), -0.4f, -length), new Vector3(MathF.Max(x0, x1), -0.4f, -length), new Vector3(MathF.Max(x0, x1), ArenaStone.Ground + count * rise + 0.35f, -length), new Vector3(MathF.Min(x0, x1), ArenaStone.Ground + count * rise + 0.35f, -length),
                }, ArenaStone.Chamfer, ArenaStone.DressedFacing(stone, random.Fork(side > 0f ? "east cheek" : "west cheek"), 1f, 0.14f));
            }
        }

        // ---------------------------------------------------------------- What's left of the fights before

        private static readonly Vector3 OldBlood = new Vector3(0.2f, 0.085f, 0.055f);     // Dried into sand, brown more than red.
        private static readonly Vector3 BoneColour = new Vector3(0.78f, 0.72f, 0.6f);

        /// <summary>
        /// What's left on an arena's floor: old blood soaked dark into the sand in a spread of blotches round a heart of it (<paramref name="form"/> 0), or the bones of what was fed to the tyrant, long bones,
        /// ribs and a skull of something big, half sunk in the sand (1), <paramref name="size"/> times the usual. Flat to the ground, as a fight goes over it.
        /// </summary>
        public static void Remains(MeshBuilder b, int form, float size, uint seed)
        {
            var random = new WorldRandom(seed).Fork("remains");
            float lie = ArenaStone.Ground + 0.025f;
            if (form == 2)
            {
                // Straw trodden into a pen's mud: flat stalks every which way, thickest in the middle of the patch.
                int stalks = (int)(140 * size * size);
                for (int i = 0; i < stalks; i++)
                {
                    float a = random.Float() * MathF.Tau, r = MathF.Sqrt(random.Float()) * 2.4f * size;
                    Vector3 at = new Vector3(MathF.Cos(a) * r, lie + random.Range(0f, 0.03f), MathF.Sin(a) * r);
                    float heading = random.Float() * MathF.Tau, length = random.Range(0.35f, 0.8f);
                    Vector3 along = new Vector3(MathF.Cos(heading), 0f, MathF.Sin(heading)) * length * 0.5f;
                    Vector3 across = new Vector3(-MathF.Sin(heading), 0f, MathF.Cos(heading)) * 0.035f;
                    VertexMaterial straw = new VertexMaterial(Textures.MaterialCatalog.Thatch.Index, new Util.Colour(new Vector3(0.66f, 0.54f, 0.28f) * random.Range(0.6f, 1.05f)));
                    b.Quad(at - along - across, at - along + across, at + along + across, at + along - across, straw);
                }
                return;
            }
            if (form == 0)
            {
                Blotch(b, Vector3.UnitY * lie, 1.3f * size, random.Fork("heart"), 0.9f);
                int splashes = random.Int(2, 5);
                for (int i = 0; i < splashes; i++)
                {
                    float a = random.Float() * MathF.Tau, r = random.Range(1.1f, 2.2f) * size;
                    Blotch(b, new Vector3(MathF.Cos(a) * r, lie + 0.002f * (i + 1), MathF.Sin(a) * r), random.Range(0.25f, 0.5f) * size, random.Fork(i), random.Range(0.85f, 1.05f));
                }
                return;
            }
            VertexMaterial bone = new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Util.Colour(BoneColour * random.Range(0.85f, 1f)));
            VertexMaterial old = new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Util.Colour(BoneColour * 0.62f));
            float s = size;
            // A few long bones, each a shaft with a knuckle at either end, lying every which way and sunk a little.
            int longs = random.Int(3, 6);
            for (int i = 0; i < longs; i++)
            {
                float a = random.Float() * MathF.Tau;
                Vector3 middle = new Vector3(random.Range(-1.2f, 1.2f) * s, lie + 0.04f * s, random.Range(-1.2f, 1.2f) * s);
                Vector3 along = new Vector3(MathF.Cos(a), random.Range(-0.08f, 0.08f), MathF.Sin(a)) * random.Range(0.5f, 0.95f) * s;
                VertexMaterial which = random.Chance(0.3f) ? old : bone;
                b.Tube(middle - along, middle + along, 0.075f * s, 0.065f * s, 6, which);
                foreach (float end in new[] { -1f, 1f }) Knuckle(b, middle + along * end * 1.05f, 0.12f * s, which);
            }
            // Ribs of a big animal, half a cage of arcs coming up out of the sand.
            Vector3 spine = new Vector3(random.Range(-0.6f, 0.6f) * s, lie, random.Range(-0.6f, 0.6f) * s);
            float turn = random.Float() * MathF.Tau;
            Vector3 down = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            Vector3 side = new Vector3(-down.Z, 0f, down.X);
            int ribs = random.Int(4, 7);
            for (int r = 0; r < ribs; r++)
            {
                Vector3 root = spine + down * ((r - ribs * 0.5f) * 0.32f * s);
                Vector3 previous = root;
                const int Steps = 5;
                for (int k = 1; k <= Steps; k++)
                {
                    float t = k / (float)Steps;
                    float arc = MathF.Sin(t * MathF.PI * 0.85f);
                    Vector3 point = root + side * (t * 1.1f * s) + Vector3.UnitY * (arc * 0.55f * s * (1f - r * 0.05f)) - Vector3.UnitY * (t * t * 0.3f * s);
                    b.Tube(previous, point, 0.045f * s, 0.035f * s, 5, bone);
                    previous = point;
                }
            }
            b.Tube(spine - down * (ribs * 0.2f * s), spine + down * (ribs * 0.2f * s), 0.09f * s, 0.08f * s, 6, old);
            // And a skull, long-snouted, on its side in the sand.
            Vector3 skull = new Vector3(random.Range(-1.4f, 1.4f) * s, lie + 0.12f * s, random.Range(-1.4f, 1.4f) * s);
            float face = random.Float() * MathF.Tau;
            Vector3 snout = new Vector3(MathF.Cos(face), 0f, MathF.Sin(face));
            b.Tube(skull - snout * 0.35f * s, skull + snout * 0.45f * s, 0.26f * s, 0.11f * s, 7, bone);
            Knuckle(b, skull - snout * 0.38f * s, 0.27f * s, bone);
            b.Tube(skull + new Vector3(0f, -0.14f * s, 0f) - snout * 0.2f * s, skull + new Vector3(0f, -0.16f * s, 0f) + snout * 0.42f * s, 0.07f * s, 0.05f * s, 6, old);
        }

        // A blotch of old blood: a fan whose rim wanders smoothly in and out, as anything soaking into sand spreads, in two shades, darker at its heart.
        private static void Blotch(MeshBuilder b, Vector3 centre, float radius, WorldRandom random, float darkness)
        {
            const int Sides = 28;
            float p1 = random.Float() * MathF.Tau, p2 = random.Float() * MathF.Tau, p3 = random.Float() * MathF.Tau;
            float w1 = random.Range(0.12f, 0.22f), w2 = random.Range(0.06f, 0.12f), w3 = random.Range(0.03f, 0.06f);
            float stretch = random.Range(0.7f, 1f), lean = random.Float() * MathF.PI;
            Vector3 Rim(float a)
            {
                float r = radius * (1f + w1 * MathF.Sin(2f * a + p1) + w2 * MathF.Sin(3f * a + p2) + w3 * MathF.Sin(7f * a + p3));
                float x = MathF.Cos(a) * r, z = MathF.Sin(a) * r * stretch;
                return centre + new Vector3(x * MathF.Cos(lean) - z * MathF.Sin(lean), 0f, x * MathF.Sin(lean) + z * MathF.Cos(lean));
            }
            VertexMaterial heart = new VertexMaterial(Textures.MaterialCatalog.Flat.Index, new Util.Colour(OldBlood * darkness * 0.72f));
            VertexMaterial edge = new VertexMaterial(Textures.MaterialCatalog.Flat.Index, new Util.Colour(OldBlood * darkness));
            for (int k = 0; k < Sides; k++)
            {
                float a0 = k * MathF.Tau / Sides, a1 = (k + 1) * MathF.Tau / Sides;
                Vector3 p0 = Rim(a0), q1 = Rim(a1);
                Vector3 i0 = Vector3.Lerp(centre, p0, 0.6f) + Vector3.UnitY * 0.001f, i1 = Vector3.Lerp(centre, q1, 0.6f) + Vector3.UnitY * 0.001f;
                // Wound to face up: seen from above, angles in x and z run clockwise, so round the rim the other way.
                b.Triangle(centre + Vector3.UnitY * 0.001f, i1, i0, heart);
                b.Quad(i0, i1, q1, p0, edge);
            }
        }

        private static void Knuckle(MeshBuilder b, Vector3 at, float radius, VertexMaterial material)
        {
            b.Surface(6, 4, (u, v) =>
            {
                float theta = u * MathF.Tau, phi = v * MathF.PI;
                return at + new Vector3(MathF.Cos(theta) * MathF.Sin(phi) * radius, -MathF.Cos(phi) * radius * 0.8f, -MathF.Sin(theta) * MathF.Sin(phi) * radius);
            }, material);
        }

        // ---------------------------------------------------------------- Lamps

        /// <summary>
        /// A light on its own: a brazier's fire with nothing round it (<paramref name="form"/> 0, for a brazier that's part of something too big to hold its own light), a torch on a bracket out of a wall
        /// whose face is at z = 0 (1), an Ossuary lamp of cold light on an iron pole (2), or a cluster of candles (3).
        /// </summary>
        public static void Lamp(MeshBuilder b, int form, uint seed, List<AssetLight> lights)
        {
            var random = new WorldRandom(seed).Fork("lamp");
            switch (form)
            {
                case 0:
                    lights.Add(ArenaStone.BrazierLight(new Vector3(0f, 0.9f, 0f)));
                    break;
                case 1:
                    ArenaWalls.Torch(b, Vector3.Zero, Vector3.UnitZ, random, lights);
                    break;
                case 2:
                {
                    VertexMaterial iron = ArenaStone.Iron(random);
                    ArenaStone.Block(b, new Vector3(0f, -0.1f, 0f), new Vector3(0.32f, 0.3f, 0.32f), 0.3f, 0.04f, ArenaStone.Dressed(ArenaStoneKind.Basalt, random, 0f));
                    b.Tube(new Vector3(0f, 0.2f, 0f), new Vector3(0f, 4.4f, 0f), 0.07f, 0.05f, 6, iron);
                    b.Tube(new Vector3(0f, 4.3f, 0f), new Vector3(0.75f, 4.55f, 0f), 0.04f, 0.04f, 4, iron);
                    Vector3 hang = new Vector3(0.75f, 3.9f, 0f);
                    b.Tube(new Vector3(0.75f, 4.55f, 0f), hang + Vector3.UnitY * 0.36f, 0.015f, 0.015f, 4, iron);
                    b.Box(hang + Vector3.UnitY * 0.33f, new Vector3(0.2f, 0.03f, 0.2f), iron);
                    b.Box(hang, new Vector3(0.15f, 0.3f, 0.15f), ArenaStone.Glow(0.35f, 1f, 0.78f, 2.2f));
                    b.Box(hang - Vector3.UnitY * 0.32f, new Vector3(0.2f, 0.03f, 0.2f), iron);
                    lights.Add(AssetLight.Glow(hang, new Vector3(0.4f, 1f, 0.8f), 55f, 15f));
                    break;
                }
                case 5:
                    lights.Add(AssetLight.StainedGlass(Vector3.Zero, Vector3.UnitZ, new Vector3(1f, 0.32f, 0.12f)) with { Intensity = 160f, Radius = 18f });
                    break;
                case 4:
                {
                    // The Institute's lamp: amber glass in a brass cage on an iron bracket out of a wall, its light the colour of what it's named for.
                    VertexMaterial iron = ArenaStone.Iron(random), brass = ArenaStone.Brass(random);
                    b.Box(Vector3.Zero, new Vector3(0.14f, 0.3f, 0.06f), iron);
                    b.Tube(new Vector3(0f, 0.1f, 0.04f), new Vector3(0f, 0.25f, 0.7f), 0.035f, 0.035f, 4, iron);
                    Vector3 glass = new Vector3(0f, -0.15f, 0.72f);
                    b.Tube(new Vector3(0f, 0.25f, 0.7f), glass + Vector3.UnitY * 0.35f, 0.02f, 0.02f, 4, iron);
                    b.Box(glass + Vector3.UnitY * 0.32f, new Vector3(0.2f, 0.035f, 0.2f), brass);
                    b.Box(glass, new Vector3(0.15f, 0.28f, 0.15f), ArenaStone.Glow(1f, 0.55f, 0.12f, 2f));
                    foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f }) b.Box(glass + new Vector3(x * 0.17f, 0f, z * 0.17f), new Vector3(0.02f, 0.3f, 0.02f), brass);
                    b.Box(glass - Vector3.UnitY * 0.31f, new Vector3(0.19f, 0.03f, 0.19f), brass);
                    lights.Add(AssetLight.Glow(glass, new Vector3(1f, 0.55f, 0.12f), 60f, 15f) with { Shadow = true });
                    break;
                }
                default:
                {
                    VertexMaterial wax = new VertexMaterial(Textures.MaterialCatalog.Wax.Index, new Util.Colour(0.86f, 0.82f, 0.7f));
                    int candles = random.Int(5, 9);
                    for (int i = 0; i < candles; i++)
                    {
                        float a = random.Float() * MathF.Tau, r = MathF.Sqrt(random.Float()) * 0.45f;
                        float h = random.Range(0.25f, 0.8f), w = random.Range(0.05f, 0.09f);
                        Vector3 foot = new Vector3(MathF.Cos(a) * r, ArenaStone.Ground - 0.02f, MathF.Sin(a) * r);
                        b.Tube(foot, foot + Vector3.UnitY * h, w, w * 0.92f, 6, wax);
                        lights.Add(AssetLight.Burning(foot + Vector3.UnitY * h, w * 0.8f));
                    }
                    lights.Add(AssetLight.VigilCandles(new Vector3(0f, 0.8f, 0f)));
                    break;
                }
            }
        }
    }
}
