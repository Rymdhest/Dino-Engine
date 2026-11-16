using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The small things the halls are full of, each built where it's put: what's drunk from and poured from, what's played with, what a barn keeps, and what gives light, with the
    /// woods and cloths they're made of. The Gaming House and the Forge both draw on it (`using static`), so that a lantern is the same lantern in either.
    /// </summary>
    internal static class HallProps
    {
        private const float FloorTop = Hall.FloorTop;

        // ---- What it's made of. Its frame is dark with age and smoke, its boards weathered every one differently, and what was made for it since is oak.

        // Hewn timber, its grain running along it: the stripped wood of a trunk, stained dark.
        internal static VertexMaterial Frame => new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(112, 84, 60));
        internal static VertexMaterial FrameDark => new VertexMaterial(MaterialCatalog.BarkBare.Index, new Colour(84, 62, 46));
        internal static VertexMaterial Oak => Hall.Timber(146, 104, 66);
        internal static VertexMaterial Walnut => Hall.Timber(96, 64, 42);
        internal static VertexMaterial Pale => Hall.Timber(184, 150, 108);
        internal static VertexMaterial Iron => Hall.Iron(78);
        internal static VertexMaterial Rubble => Hall.Stone(170, 162, 148);
        internal static VertexMaterial Dressed => Hall.Ashlar(196, 188, 172);
        internal static VertexMaterial Soot => Hall.Flat(27, 24, 23);
        internal static VertexMaterial Gold => Hall.Metal(214, 168, 70);
        internal static VertexMaterial SilverCoin => Hall.Metal(190, 192, 198);
        // Hay is a plain gold with its stalks laid on it: thatch, which is the nearest material, reads as a piece of roof.
        internal static VertexMaterial Hay => Hall.Flat(178, 136, 54);
        internal static VertexMaterial HayDark => Hall.Flat(142, 104, 40);
        internal static VertexMaterial HayPale => Hall.Flat(204, 164, 78);
        internal static VertexMaterial Twine => Hall.Flat(96, 74, 46);
        internal static VertexMaterial Sacking => Hall.Cloth(156, 134, 96);
        internal static VertexMaterial Stoneware => Hall.Brick(176, 150, 128);
        internal static VertexMaterial Chalk => Hall.Flat(214, 210, 198);
        internal static VertexMaterial Slate => Hall.Flat(42, 46, 46);

        /// <summary>A board of the old barn: grey-brown, no two alike.</summary>
        internal static VertexMaterial Weathered(WorldRandom random)
        {
            float shade = random.Range(0.8f, 1.12f);
            float warm = random.Range(-9f, 9f);
            return Hall.Timber((int)((124f + warm) * shade), (int)(108f * shade), (int)((92f - warm) * shade));
        }

        internal static readonly Vector3 Teal = new Vector3(28, 96, 98);
        internal static readonly Vector3 Green = new Vector3(36, 86, 54);
        internal static readonly Vector3 Saffron = new Vector3(190, 130, 40);
        internal static readonly Vector3 Blue = new Vector3(44, 58, 116);
        internal static readonly Vector3 Cream = new Vector3(208, 198, 170);
        internal static readonly Vector3 Blossom = new Vector3(190, 120, 132);
        internal static readonly Vector3[] Festive = { Teal, Saffron, Cream, Blue, Blossom, Green };

        internal static VertexMaterial Dyed(Vector3 colour) => Hall.ClothOf(colour);
        internal static VertexMaterial PaintedOf(Vector3 colour) => Hall.Painted((int)colour.X, (int)colour.Y, (int)colour.Z);

        // ---- Drinking.

        internal static void Mug(MeshBuilder b, Vector3 foot, VertexMaterial material, float turn = 0f, float size = 1f)
        {
            Vector3 out_ = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            b.Tube(foot, foot + Vector3.UnitY * 0.32f * size, 0.13f * size, 0.115f * size, 8, material);
            b.Beam(foot + out_ * 0.12f * size + Vector3.UnitY * 0.26f * size, foot + out_ * 0.22f * size + Vector3.UnitY * 0.16f * size, 0.025f * size, material);
            b.Beam(foot + out_ * 0.22f * size + Vector3.UnitY * 0.16f * size, foot + out_ * 0.12f * size + Vector3.UnitY * 0.06f * size, 0.025f * size, material);
        }

        internal static void Bottle(MeshBuilder b, Vector3 foot, Vector3 glass, float height)
        {
            VertexMaterial material = Hall.Mirror((int)glass.X, (int)glass.Y, (int)glass.Z);
            b.Tube(foot, foot + Vector3.UnitY * height * 0.62f, 0.1f, 0.1f, 7, material, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.62f, foot + Vector3.UnitY * height * 0.8f, 0.1f, 0.04f, 7, material, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.8f, foot + Vector3.UnitY * height, 0.04f, 0.04f, 6, material);
            b.Tube(foot + Vector3.UnitY * (height - 0.01f), foot + Vector3.UnitY * (height + 0.05f), 0.035f, 0.03f, 5, Hall.Flat(150, 116, 76));
        }

        internal static readonly Vector3[] Glass = { new(40, 86, 50), new(124, 72, 26), new(70, 44, 30), new(150, 146, 118), new(36, 54, 92), new(30, 70, 66) };

        internal static void Jug(MeshBuilder b, Vector3 foot, VertexMaterial material, float size = 1f)
        {
            b.Tube(foot, foot + Vector3.UnitY * 0.3f * size, 0.13f * size, 0.2f * size, 9, material, capped: false);
            b.Tube(foot + Vector3.UnitY * 0.3f * size, foot + Vector3.UnitY * 0.5f * size, 0.2f * size, 0.11f * size, 9, material, capped: false);
            b.Tube(foot + Vector3.UnitY * 0.5f * size, foot + Vector3.UnitY * 0.66f * size, 0.11f * size, 0.13f * size, 9, material);
            b.Beam(foot + new Vector3(0.12f, 0.6f, 0f) * size, foot + new Vector3(0.28f, 0.42f, 0f) * size, 0.03f * size, material);
            b.Beam(foot + new Vector3(0.28f, 0.42f, 0f) * size, foot + new Vector3(0.19f, 0.22f, 0f) * size, 0.03f * size, material);
        }

        // ---- Playing.

        // A card on a table, face down in the house's teal or face up with its pips.
        internal static void Card(MeshBuilder b, Vector3 at, float yaw, bool faceUp, int suit = 0)
        {
            Hall.At(b, at, yaw, () =>
            {
                b.Box(Vector3.Zero, new Vector3(0.14f, 0.006f, 0.2f), faceUp ? Hall.Flat(226, 216, 194) : Hall.Flat(30, 84, 88));
                if (!faceUp)
                {
                    b.Box(new Vector3(0f, 0.008f, 0f), new Vector3(0.1f, 0.002f, 0.16f), Hall.Flat(204, 184, 120));
                    b.Box(new Vector3(0f, 0.011f, 0f), new Vector3(0.07f, 0.002f, 0.13f), Hall.Flat(30, 84, 88));
                    return;
                }
                VertexMaterial ink = suit % 2 == 0 ? Hall.Flat(34, 30, 28) : Hall.Flat(168, 78, 34);
                b.Box(new Vector3(0f, 0.008f, 0f), new Vector3(0.045f, 0.002f, 0.065f), ink);
                b.Box(new Vector3(-0.09f, 0.008f, -0.15f), new Vector3(0.02f, 0.002f, 0.025f), ink);
                b.Box(new Vector3(0.09f, 0.008f, 0.15f), new Vector3(0.02f, 0.002f, 0.025f), ink);
            });
        }

        // A stack of coins, mostly gold, the odd one fallen beside it.
        internal static void Coins(MeshBuilder b, Vector3 foot, int count, WorldRandom random)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 at = foot + new Vector3(random.Range(-0.014f, 0.014f), i * 0.034f, random.Range(-0.014f, 0.014f));
                b.Tube(at, at + Vector3.UnitY * 0.03f, 0.1f, 0.1f, 8, i % 4 == 3 ? SilverCoin : Gold);
            }
            if (random.Chance(0.6f))
            {
                Vector3 fallen = foot + new Vector3(random.Range(0.2f, 0.3f), 0f, random.Range(-0.15f, 0.15f));
                b.Tube(fallen, fallen + Vector3.UnitY * 0.03f, 0.1f, 0.1f, 8, Gold);
            }
        }

        // Coins spilt flat, as winnings pushed across a table are.
        internal static void Spill(MeshBuilder b, Vector3 at, float reach, int count, WorldRandom random)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 coin = at + new Vector3(random.Range(-reach, reach), random.Chance(0.3f) ? 0.03f : 0f, random.Range(-reach, reach) * 0.7f);
                b.Tube(coin, coin + Vector3.UnitY * 0.03f, 0.1f, 0.1f, 8, random.Chance(0.25f) ? SilverCoin : Gold);
            }
        }

        internal static void Die(MeshBuilder b, Vector3 at, float half, float yaw) => HallShells.Die(b, at, half, yaw);

        // A leather cup for dice, standing or tipped over with its mouth along `spill`.
        internal static void DiceCup(MeshBuilder b, Vector3 foot, Vector3? spill = null)
        {
            VertexMaterial hide = Hall.Leather(76, 44, 26), band = Hall.Leather(104, 66, 38);
            if (spill is Vector3 way)
            {
                Vector3 from = foot + Vector3.UnitY * 0.2f, to = from + way.Normalized() * 0.46f;
                b.Tube(to, from, 0.2f, 0.17f, 10, hide);
                b.Tube(to - way.Normalized() * 0.05f, to, 0.21f, 0.21f, 10, band, capped: false);
                return;
            }
            b.Tube(foot + Vector3.UnitY * 0.46f, foot, 0.21f, 0.17f, 10, hide);
            b.Tube(foot + Vector3.UnitY * 0.41f, foot + Vector3.UnitY * 0.46f, 0.215f, 0.215f, 10, band, capped: false);
        }

        // ---- Light.

        // A candle in a pewter dish.
        internal static void DishCandle(MeshBuilder b, Vector3 foot, List<AssetLight> lights, float height = 0.32f, float light = 0f)
        {
            b.Tube(foot, foot + Vector3.UnitY * 0.04f, 0.17f, 0.19f, 10, Hall.Pewter);
            Hall.Candle(b, foot + Vector3.UnitY * 0.04f, height, 0.06f, lights);
            if (light > 0f) lights.Add(AssetLight.Candles(foot + Vector3.UnitY * (height + 0.5f)) with { Intensity = light, Radius = 9f, Shadow = false });
        }

        // A candle stuck in the neck of an empty bottle, wax run down its shoulders.
        internal static void BottleCandle(MeshBuilder b, Vector3 foot, List<AssetLight> lights, float light = 0f)
        {
            Vector3 glass = Glass[0];
            VertexMaterial material = Hall.Mirror((int)glass.X, (int)glass.Y, (int)glass.Z);
            b.Tube(foot, foot + Vector3.UnitY * 0.3f, 0.11f, 0.11f, 7, material, capped: false);
            b.Tube(foot + Vector3.UnitY * 0.3f, foot + Vector3.UnitY * 0.42f, 0.11f, 0.045f, 7, material);
            b.Tube(foot + Vector3.UnitY * 0.36f, foot + Vector3.UnitY * 0.44f, 0.075f, 0.05f, 6, Hall.Wax);
            Hall.Candle(b, foot + Vector3.UnitY * 0.42f, 0.24f, 0.04f, lights);
            if (light > 0f) lights.Add(AssetLight.Candles(foot + Vector3.UnitY * 1.1f) with { Intensity = light, Radius = 9f, Shadow = false });
        }

        /// <summary>
        /// A lantern of iron and horn about <paramref name="at"/>, the middle of its panes, hung by its ring. Small enough to sit inside the near plane of its own shadow map
        /// (PointLightRenderer.ShadowNearPlane), or its frame would shut its light in.
        /// </summary>
        internal static void Lantern(MeshBuilder b, Vector3 at, List<AssetLight> lights, float intensity, float radius, bool shadow = true)
        {
            VertexMaterial iron = Hall.Iron(68);
            b.Box(at, new Vector3(0.19f, 0.28f, 0.19f), Hall.Glow(1f, 0.5f, 0.15f, 1.5f));
            b.Box(at + Vector3.UnitY * 0.32f, new Vector3(0.26f, 0.045f, 0.26f), iron);
            b.Tube(at + Vector3.UnitY * 0.36f, at + Vector3.UnitY * 0.55f, 0.21f, 0.04f, 4, iron);
            b.Box(at - Vector3.UnitY * 0.32f, new Vector3(0.24f, 0.04f, 0.24f), iron, closeBottom: true);
            foreach (float x in new[] { -0.19f, 0.19f })
            {
                foreach (float z in new[] { -0.19f, 0.19f }) b.Beam(at + new Vector3(x, -0.3f, z), at + new Vector3(x, 0.3f, z), 0.02f, iron);
            }
            if (intensity > 0f) lights.Add(AssetLight.Lantern(at) with { Intensity = intensity, Radius = radius, Shadow = shadow });
        }

        // An iron arm out of something upright, curled at its end, with a lantern hung from it.
        internal static void LanternArm(MeshBuilder b, Vector3 fixedAt, Vector3 out_, float reach, List<AssetLight> lights, float intensity, float radius)
        {
            VertexMaterial iron = Hall.Iron(68);
            Vector3 end = fixedAt + out_ * reach;
            b.Beam(fixedAt, end, 0.035f, iron);
            b.Beam(fixedAt - Vector3.UnitY * 0.7f, fixedAt + out_ * reach * 0.62f, 0.028f, iron);
            b.Beam(end, end + out_ * 0.1f + Vector3.UnitY * 0.12f, 0.03f, iron);
            b.Tube(end - Vector3.UnitY * 0.02f, end - Vector3.UnitY * 0.32f, 0.018f, 0.018f, 4, iron);
            Lantern(b, end - Vector3.UnitY * 0.86f, lights, intensity, radius);
        }

        // ---- What a barn keeps.

        /// <summary>A cask lying along a line, its head at <paramref name="to"/>, with its hoops and a paler head set in its chime.</summary>
        internal static void Cask(MeshBuilder b, Vector3 from, Vector3 to, float radius, VertexMaterial? staves = null)
        {
            VertexMaterial wood = staves ?? Hall.Wood(138, 98, 62);
            VertexMaterial hoop = Hall.Iron(84);
            Vector3 axis = (to - from).Normalized();
            Vector3 middle = (from + to) * 0.5f;
            b.Tube(middle, from, radius, radius * 0.86f, 14, wood);
            b.Tube(middle, to, radius, radius * 0.86f, 14, wood);
            b.Tube(to - axis * 0.02f, to + axis * 0.012f, radius * 0.78f, radius * 0.78f, 14, Hall.Timber(176, 138, 96));
            float length = (to - from).Length;
            foreach (float t in new[] { 0.08f, 0.3f, 0.7f, 0.92f })
            {
                float swell = radius * (0.86f + 0.28f * MathF.Min(t, 1f - t)) + 0.014f;
                Vector3 at = from + axis * (length * t);
                b.Tube(at - axis * 0.045f, at + axis * 0.045f, swell, swell, 14, hoop, capped: false);
            }
        }

        // A tap in a cask's head, and the peg that turns it.
        internal static void Spigot(MeshBuilder b, Vector3 onHead, Vector3 out_)
        {
            b.Tube(onHead, onHead + out_ * 0.26f, 0.05f, 0.04f, 6, Hall.Copper);
            b.Beam(onHead + out_ * 0.14f - Vector3.UnitY * 0.02f, onHead + out_ * 0.14f + Vector3.UnitY * 0.16f, 0.022f, Hall.Timber(150, 112, 74));
            b.Beam(onHead + out_ * 0.24f, onHead + out_ * 0.24f - Vector3.UnitY * 0.1f, 0.022f, Hall.Copper);
        }

        internal static void Sack(MeshBuilder b, Vector3 foot, float radius, float height, WorldRandom random)
        {
            VertexMaterial cloth = random.Chance(0.5f) ? Sacking : Hall.Cloth(140, 118, 84);
            Vector3 lean = new Vector3(random.Range(-0.1f, 0.1f), 0f, random.Range(-0.1f, 0.1f));
            b.Tube(foot, foot + Vector3.UnitY * height * 0.45f + lean * 0.4f, radius * 0.86f, radius, 9, cloth, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.45f + lean * 0.4f, foot + Vector3.UnitY * height * 0.82f + lean, radius, radius * 0.5f, 9, cloth, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.82f + lean, foot + Vector3.UnitY * height + lean * 1.3f, radius * 0.2f, radius * 0.36f, 7, cloth);
            b.Tube(foot + Vector3.UnitY * height * 0.8f + lean, foot + Vector3.UnitY * height * 0.85f + lean, radius * 0.24f, radius * 0.24f, 6, Twine, capped: false);
        }

        internal static void Crate(MeshBuilder b, Vector3 foot, Vector3 half, float yaw, WorldRandom random)
        {
            Hall.At(b, foot, yaw, () =>
            {
                VertexMaterial boards = Weathered(random);
                VertexMaterial edge = FrameDark;
                b.Box(Vector3.UnitY * half.Y, half, boards);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f }) b.Box(new Vector3(sx * (half.X - 0.05f), half.Y, sz * (half.Z - 0.05f)), new Vector3(0.065f, half.Y + 0.006f, 0.065f), edge);
                }
                foreach (float y in new[] { 0.09f, half.Y * 2f - 0.09f }) b.Box(new Vector3(0f, y, half.Z + 0.012f), new Vector3(half.X, 0.07f, 0.014f), edge);
                Hall.Board(b, new Vector3(-half.X + 0.08f, 0.16f, half.Z + 0.014f), new Vector3(half.X - 0.08f, half.Y * 2f - 0.16f, half.Z + 0.014f), Vector3.UnitY, 0.06f, 0.012f, edge);
            });
        }

        /// <summary>A bale of hay on <paramref name="foot"/>, bound twice with twine, its long side along x before it's turned.</summary>
        internal static void Bale(MeshBuilder b, Vector3 foot, float yaw, WorldRandom random, float size = 1f)
        {
            Vector3 half = new Vector3(1.05f, 0.48f, 0.56f) * size;
            Hall.At(b, foot, yaw, () =>
            {
                b.Box(Vector3.UnitY * half.Y, half, random.Chance(0.5f) ? Hay : HayDark, closeBottom: true);
                foreach (float x in new[] { -0.45f, 0.45f }) b.Box(new Vector3(x * size, half.Y, 0f), new Vector3(0.025f, half.Y + 0.012f, half.Z + 0.012f), Twine);
                // The lie of the stalks along it, on its top and on the side the camera sees.
                for (int strand = 0; strand < 6; strand++)
                {
                    float across = (strand + 0.5f) / 6f * 2f - 1f + random.Range(-0.06f, 0.06f);
                    VertexMaterial shade = strand % 2 == 0 ? HayPale : HayDark;
                    b.Box(new Vector3(random.Range(-0.05f, 0.05f), half.Y * 2f + 0.004f, across * half.Z * 0.92f), new Vector3(half.X * random.Range(0.8f, 0.97f), 0.004f, 0.022f), shade);
                    b.Box(new Vector3(random.Range(-0.05f, 0.05f), half.Y * (1f + across * 0.9f), half.Z + 0.004f), new Vector3(half.X * random.Range(0.8f, 0.97f), 0.022f, 0.004f), shade);
                }
                // Stalks standing out of its ends and its top, so that it isn't a brick.
                for (int i = 0; i < 12; i++)
                {
                    float side = random.Chance(0.5f) ? 1f : -1f;
                    Vector3 at = random.Chance(0.5f)
                        ? new Vector3(side * half.X, random.Range(0.1f, half.Y * 2f - 0.1f), random.Range(-half.Z, half.Z) * 0.9f)
                        : new Vector3(random.Range(-half.X, half.X) * 0.9f, half.Y * 2f, random.Range(-half.Z, half.Z) * 0.9f);
                    Vector3 way = at.Y >= half.Y * 2f - 0.01f ? new Vector3(random.Range(-0.4f, 0.4f), 1f, random.Range(-0.4f, 0.4f)) : new Vector3(side, random.Range(-0.3f, 0.5f), random.Range(-0.4f, 0.4f));
                    b.Beam(at - way.Normalized() * 0.05f, at + way.Normalized() * random.Range(0.12f, 0.26f), 0.014f, Hall.Flat(206, 180, 110));
                }
            });
        }

        /// <summary>Loose hay heaped on <paramref name="foot"/>: low mounds one on another, with stalks standing out of them.</summary>
        internal static void HayHeap(MeshBuilder b, Vector3 foot, float radius, float height, WorldRandom random)
        {
            b.Tube(foot, foot + Vector3.UnitY * height * 0.55f, radius, radius * 0.72f, 11, HayDark, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.55f, foot + Vector3.UnitY * height, radius * 0.72f, radius * 0.2f, 11, Hay);
            for (int i = 0; i < 5; i++)
            {
                float a = random.Range(0f, MathF.Tau);
                Vector3 at = foot + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * radius * random.Range(0.45f, 0.85f);
                float r = radius * random.Range(0.3f, 0.5f);
                b.Tube(at, at + Vector3.UnitY * height * random.Range(0.3f, 0.6f), r, r * 0.25f, 8, random.Chance(0.5f) ? Hay : HayDark);
            }
            Stalks(b, foot + Vector3.UnitY * height * 0.4f, radius * 0.8f, 26, random);
        }

        // Stalks of straw standing out of something every way.
        internal static void Stalks(MeshBuilder b, Vector3 about, float reach, int count, WorldRandom random)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 way = new Vector3(random.Range(-1f, 1f), random.Range(0.1f, 1f), random.Range(-1f, 1f)).Normalized();
                Vector3 from = about + new Vector3(way.X, 0f, way.Z) * reach * random.Range(0.3f, 1f);
                b.Beam(from, from + way * random.Range(0.2f, 0.42f), 0.014f, random.Chance(0.5f) ? Hall.Flat(210, 184, 112) : Hall.Flat(184, 158, 92));
            }
        }

        /// <summary>Straw trodden about a floor between <paramref name="min"/> and <paramref name="max"/>, only where <paramref name="keep"/> says, if it's given.</summary>
        internal static void Strewn(MeshBuilder b, Vector2 min, Vector2 max, int count, WorldRandom random, Func<Vector2, bool>? keep = null)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 at = new Vector2(random.Range(min.X, max.X), random.Range(min.Y, max.Y));
                float turn = random.Range(0f, MathF.PI);
                float length = random.Range(0.08f, 0.2f);
                VertexMaterial straw = random.Chance(0.5f) ? Hall.Flat(170, 144, 88) : Hall.Flat(146, 122, 74);
                if (keep != null && !keep(at)) continue;
                Vector3 way = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)) * length;
                Vector3 middle = new Vector3(at.X, FloorTop + 0.014f, at.Y);
                Hall.Board(b, middle - way, middle + way, Vector3.Cross(way, Vector3.UnitY), 0.012f, 0.007f, straw);
            }
        }

        // A pitchfork, its tines up, from where its shaft stands to where its head is.
        internal static void Pitchfork(MeshBuilder b, Vector3 foot, Vector3 head)
        {
            Vector3 along = (head - foot).Normalized();
            Vector3 across = Vector3.Cross(along, MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
            b.Tube(foot, head, 0.035f, 0.03f, 6, Hall.Timber(160, 122, 80));
            b.Beam(head - across * 0.16f, head + across * 0.16f, 0.022f, Iron);
            foreach (float s in new[] { -0.16f, 0f, 0.16f }) b.Beam(head + across * s, head + across * s * 1.15f + along * 0.48f, 0.018f, Iron);
        }

        // A cart wheel about `hub`, its axle along `axle`: felloes, an iron tyre, spokes and a nave.
        internal static void CartWheel(MeshBuilder b, Vector3 hub, Vector3 axle, float radius, VertexMaterial wood, int spokes = 10, bool tyre = true)
        {
            axle = axle.Normalized();
            Vector3 u = Vector3.Cross(axle, MathF.Abs(axle.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
            Vector3 v = Vector3.Cross(axle, u).Normalized();
            const int Segments = 20;
            Vector3 On(float t, float r) => hub + (u * MathF.Cos(t) + v * MathF.Sin(t)) * r;
            for (int i = 0; i < Segments; i++)
            {
                float t0 = MathF.Tau * i / Segments, t1 = MathF.Tau * (i + 1) / Segments;
                Hall.Board(b, On(t0, radius - 0.1f), On(t1, radius - 0.1f), axle, 0.09f, 0.1f, wood);
                if (tyre) Hall.Board(b, On(t0, radius + 0.012f), On(t1, radius + 0.012f), axle, 0.1f, 0.014f, Hall.Iron(72));
            }
            for (int i = 0; i < spokes; i++)
            {
                float t = MathF.Tau * (i + 0.5f) / spokes;
                Hall.Board(b, On(t, 0.2f), On(t, radius - 0.18f), axle, 0.05f, 0.04f, wood);
            }
            b.Tube(hub - axle * 0.2f, hub + axle * 0.2f, 0.24f, 0.2f, 10, wood);
            b.Tube(hub + axle * 0.2f, hub + axle * 0.26f, 0.1f, 0.08f, 8, Hall.Iron(72));
        }

        // Antlers on a shield of oak, hung on a wall that faces `out_`.
        internal static void Antlers(MeshBuilder b, Vector3 onWall, Vector3 out_, float size = 1f)
        {
            Vector3 across = Vector3.Cross(Vector3.UnitY, out_).Normalized();
            VertexMaterial bone = Hall.Bone(214, 200, 170);
            // The shield, and the cap of skull the antlers grow from.
            Hall.Board(b, onWall + out_ * 0.04f - Vector3.UnitY * 0.5f * size, onWall + out_ * 0.04f + Vector3.UnitY * 0.42f * size, across, 0.36f * size, 0.04f, Walnut);
            Hall.Tri(b, onWall + out_ * 0.08f - across * 0.36f * size - Vector3.UnitY * 0.5f * size, onWall + out_ * 0.08f + across * 0.36f * size - Vector3.UnitY * 0.5f * size,
                onWall + out_ * 0.08f - Vector3.UnitY * 0.86f * size, out_, Walnut);
            b.Box(onWall + out_ * 0.16f + Vector3.UnitY * 0.05f * size, new Vector3(MathF.Abs(across.X) * 0.2f + MathF.Abs(out_.X) * 0.1f, 0.16f, MathF.Abs(across.Z) * 0.2f + MathF.Abs(out_.Z) * 0.1f) * size, bone);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 root = onWall + out_ * 0.2f + across * s * 0.14f * size + Vector3.UnitY * 0.18f * size;
                Vector3 p1 = root + (across * s * 0.5f + Vector3.UnitY * 0.34f + out_ * 0.16f) * size;
                Vector3 p2 = p1 + (across * s * 0.42f + Vector3.UnitY * 0.52f + out_ * 0.1f) * size;
                Vector3 p3 = p2 + (across * s * 0.06f + Vector3.UnitY * 0.56f - out_ * 0.04f) * size;
                b.Tube(root, p1, 0.07f * size, 0.06f * size, 6, bone, capped: false);
                b.Tube(p1, p2, 0.06f * size, 0.045f * size, 6, bone, capped: false);
                b.Tube(p2, p3, 0.045f * size, 0.012f, 6, bone);
                // Tines, each reaching up and forward off the beam.
                b.Tube(root + (p1 - root) * 0.5f, root + (p1 - root) * 0.5f + (Vector3.UnitY * 0.34f + out_ * 0.3f) * size, 0.04f * size, 0.01f, 5, bone);
                b.Tube(p1, p1 + (Vector3.UnitY * 0.46f + out_ * 0.24f - across * s * 0.06f) * size, 0.045f * size, 0.01f, 5, bone);
                b.Tube(p1 + (p2 - p1) * 0.55f, p1 + (p2 - p1) * 0.55f + (Vector3.UnitY * 0.44f + out_ * 0.12f - across * s * 0.12f) * size, 0.04f * size, 0.01f, 5, bone);
            }
        }

        // A pennant hung from a point of a line, drawn from both sides, as the cut draws whatever's seen from behind as a section.
        internal static void Pennant(MeshBuilder b, Vector3 from, Vector3 to, float drop, VertexMaterial cloth)
        {
            Vector3 point = (from + to) * 0.5f - Vector3.UnitY * drop;
            Vector3 thick = Vector3.Cross(to - from, Vector3.UnitY).Normalized() * 0.006f;
            b.Triangle(from + thick, to + thick, point + thick, cloth);
            b.Triangle(to - thick, from - thick, point - thick, cloth);
        }

        /// <summary>A line of pennants between two points, sagging, in the Haven's colours.</summary>
        internal static void Bunting(MeshBuilder b, Vector3 from, Vector3 to, float sag, int count, int first, float drop = 0.62f)
        {
            Vector3 On(float t) => Vector3.Lerp(from, to, t) - Vector3.UnitY * sag * 4f * t * (1f - t);
            VertexMaterial cord = Hall.Flat(58, 46, 34);
            for (int i = 0; i < count; i++)
            {
                Vector3 a = On(i / (float)count), c = On((i + 1) / (float)count);
                b.Beam(a, c, 0.012f, cord);
                Vector3 gap = (c - a) * 0.12f;
                Pennant(b, a + gap, c - gap, drop, Dyed(Festive[(first + i) % Festive.Length]));
            }
        }
    }
}
