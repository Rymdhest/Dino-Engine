using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// Everything that can stand in a hall, each built in its own frame to the numbers of its <see cref="HallPiece"/>: its middle on the floor at the origin, its front towards +z, reaching
    /// <c>Half</c> each way and as tall as its <c>Height</c>, so that what's drawn is what the world takes as solid. The world is about two and a half times real size, as a person is 4.7 m
    /// tall: a table is 1.95 m high, a chair's seat 1.1 m, a candle half a metre.
    /// </summary>
    internal static class HallFurniture
    {
        public static void Build(MeshBuilder b, in HallPiece piece, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            Vector2 half = piece.Half;
            float r = half.X;
            float h = piece.Height;
            switch (piece.Kind)
            {
                case PieceKind.Bellows: Bellows(b, half, h); break;
                case PieceKind.Anvil: Anvil(b, half, h); break;
                case PieceKind.Trough: Trough(b, half, h); break;
                case PieceKind.ToolRack: ToolRack(b, half, h, random); break;
                case PieceKind.CoalBin: CoalBin(b, half, h, random); break;
                case PieceKind.Workbench: Workbench(b, half, h, lights, random); break;
                case PieceKind.WeaponRack: WeaponRack(b, half, h, random); break;
                case PieceKind.ArmourStand: ArmourStand(b, r, h); break;
                case PieceKind.Shelves: StockShelves(b, half, h, random); break;
                case PieceKind.Grindstone: Grindstone(b, half, h); break;
                case PieceKind.LeatherTable: LeatherTable(b, half, h, random); break;
                case PieceKind.Firewood: Firewood(b, half, h, random); break;
                case PieceKind.Casks: Casks(b, r, h, random); break;
                case PieceKind.Crates: Crates(b, half, h, random); break;
                case PieceKind.GamingTable: GamingTable(b, half, h, lights, random); break;
                case PieceKind.Chair: Chair(b, r, h, piece.Variant); break;
                case PieceKind.Stool: Stool(b, r, h); break;
                case PieceKind.Bar: Bar(b, half, h, random); break;
                case PieceKind.BarShelves: BarShelves(b, half, h, random); break;
                case PieceKind.Fireplace: Fireplace(b, half, h, lights, kit); break;
                case PieceKind.Armchair: Armchair(b, r, h, piece.Variant); break;
                case PieceKind.RoundTable: RoundTable(b, r, h, piece.Variant, lights, random); break;
                case PieceKind.Stage: Stage(b, half, h); break;
                case PieceKind.Rug: Rug(b, half, piece.Variant); break;
                case PieceKind.Planter: Planter(b, r, h, random); break;
                case PieceKind.Plinth: Exhibits.Build(b, (ExhibitKind)piece.Variant, half, h, kit, lights, random); break;
                case PieceKind.Bookcase: Bookcase(b, half, h, random); break;
                case PieceKind.ReadingDesk: ReadingDesk(b, half, h, lights, random); break;
                case PieceKind.CandleStand: CandleStand(b, r, h, lights); break;
                case PieceKind.Bench: Bench(b, half, h); break;
                case PieceKind.Globe: Globe(b, r, h); break;
                case PieceKind.Lectern: Lectern(b, half, h); break;
            }
        }

        // ---- The Forge.

        private static void Bellows(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial frame = Hall.Wood(104, 74, 50);
            VertexMaterial hide = Hall.Leather(96, 62, 38);
            // A trestle for it to lie on, sloping down to the fire.
            foreach (float z in new[] { -half.Y + 0.25f, half.Y - 0.25f })
            {
                foreach (float x in new[] { -half.X + 0.2f, half.X - 0.2f }) b.Box(new Vector3(x, 0.55f, z), new Vector3(0.1f, 0.55f, 0.1f), frame);
                b.Box(new Vector3(0f, 1.05f, z), new Vector3(half.X, 0.08f, 0.1f), frame);
            }
            // Two boards with folds of leather between, pointing their nozzle at the fire (-x).
            float y = 1.2f;
            for (int i = 0; i < 4; i++)
            {
                float inset = 0.06f * (i % 2);
                b.Box(new Vector3(0.1f, y + 0.12f + i * 0.16f, 0f), new Vector3(half.X - 0.2f - inset, 0.08f, half.Y - 0.35f - inset), hide);
            }
            b.Box(new Vector3(0.1f, y + 0.04f, 0f), new Vector3(half.X - 0.1f, 0.06f, half.Y - 0.2f), frame);
            b.Box(new Vector3(0.1f, y + 0.82f, 0f), new Vector3(half.X - 0.1f, 0.06f, half.Y - 0.2f), frame);
            b.Tube(new Vector3(-half.X + 0.15f, y + 0.45f, 0f), new Vector3(-half.X - 0.65f, y + 0.25f, 0f), 0.16f, 0.08f, 6, Hall.Iron(80));
            // The lever that works it, on a post, with a chain down to the top board.
            b.Box(new Vector3(half.X - 0.3f, h * 0.5f + 0.5f, -half.Y + 0.3f), new Vector3(0.12f, h * 0.5f + 0.5f, 0.12f), frame);
            b.Beam(new Vector3(half.X + 0.4f, h + 0.7f, -half.Y + 0.3f), new Vector3(-0.2f, h + 0.95f, -half.Y + 0.3f), 0.07f, frame);
            b.Beam(new Vector3(0f, h + 0.92f, -half.Y + 0.3f), new Vector3(0.1f, y + 0.85f, -half.Y + 0.5f), 0.02f, Hall.Iron(76));
        }

        private static void Anvil(MeshBuilder b, Vector2 half, float h)
        {
            // A stump of oak, end grain up, with the anvil on it: its waist, its face, its horn and its heel.
            VertexMaterial bark = new VertexMaterial(MaterialCatalog.BarkFurrowed.Index, new Colour(110, 92, 76));
            float stump = h - 0.75f;
            b.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, stump, 0f), 0.62f, 0.55f, 10, bark);
            b.Tube(new Vector3(0f, stump - 0.01f, 0f), new Vector3(0f, stump + 0.01f, 0f), 0.55f, 0.55f, 10, Hall.Wood(170, 132, 92));
            VertexMaterial steel = Hall.Metal(92, 92, 96);
            b.Box(new Vector3(0f, stump + 0.12f, 0f), new Vector3(0.42f, 0.12f, 0.3f), steel);
            b.Box(new Vector3(0f, stump + 0.36f, 0f), new Vector3(0.26f, 0.14f, 0.2f), steel);
            b.Box(new Vector3(0f, stump + 0.62f, 0f), new Vector3(0.62f, 0.13f, 0.26f), steel);
            b.Box(new Vector3(0f, stump + 0.755f, 0f), new Vector3(0.6f, 0.006f, 0.24f), Hall.Metal(170, 170, 176));
            b.Tube(new Vector3(0.6f, stump + 0.66f, 0f), new Vector3(half.X + 0.3f, stump + 0.7f, 0f), 0.2f, 0.02f, 8, steel);
            b.Box(new Vector3(-0.72f, stump + 0.66f, 0f), new Vector3(0.12f, 0.08f, 0.22f), steel);
            // A hammer laid on its face, and tongs leaning on the stump.
            b.Beam(new Vector3(-0.3f, stump + 0.82f, 0.05f), new Vector3(0.45f, stump + 0.82f, 0.25f), 0.04f, Hall.Wood(150, 112, 74));
            b.Box(new Vector3(-0.36f, stump + 0.84f, 0.03f), new Vector3(0.07f, 0.07f, 0.14f), Hall.Iron(78));
            b.Beam(new Vector3(0.3f, 0.05f, 0.62f), new Vector3(0.2f, stump + 0.1f, 0.45f), 0.03f, Hall.Iron(72));
            b.Beam(new Vector3(0.42f, 0.05f, 0.6f), new Vector3(0.28f, stump + 0.12f, 0.44f), 0.03f, Hall.Iron(72));
        }

        private static void Trough(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial stone = Hall.Stone(150, 144, 134);
            b.Box(new Vector3(0f, 0.12f, 0f), new Vector3(half.X, 0.12f, half.Y), stone);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(0f, h * 0.5f, s * (half.Y - 0.12f)), new Vector3(half.X, h * 0.5f, 0.12f), stone);
                b.Box(new Vector3(s * (half.X - 0.12f), h * 0.5f, 0f), new Vector3(0.12f, h * 0.5f, half.Y - 0.24f), stone);
            }
            b.Box(new Vector3(0f, h - 0.2f, 0f), new Vector3(half.X - 0.24f, 0.01f, half.Y - 0.24f), Hall.Mirror(46, 58, 60));
            // A blade cooling in it, and a bucket beside.
            b.Beam(new Vector3(-0.6f, h - 0.35f, 0f), new Vector3(0.9f, h + 0.25f, 0.05f), 0.03f, Hall.Iron(70));
            Vector3 bucket = new Vector3(half.X + 0.5f, 0f, 0.3f);
            b.Tube(bucket, bucket + Vector3.UnitY * 0.7f, 0.32f, 0.38f, 9, Hall.Wood(122, 88, 58));
            b.Tube(bucket + Vector3.UnitY * 0.2f, bucket + Vector3.UnitY * 0.26f, 0.34f, 0.34f, 9, Hall.Iron(80));
        }

        private static void ToolRack(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial board = Hall.Wood(118, 86, 58);
            VertexMaterial iron = Hall.Iron(74);
            VertexMaterial handle = Hall.Wood(150, 112, 74);
            b.Box(new Vector3(0f, h * 0.5f + 0.4f, -half.Y + 0.08f), new Vector3(half.X, h * 0.5f - 0.4f, 0.08f), board);
            foreach (float x in new[] { -half.X + 0.12f, half.X - 0.12f }) b.Box(new Vector3(x, h * 0.5f, -half.Y + 0.15f), new Vector3(0.12f, h * 0.5f, 0.12f), Hall.DarkOak);
            for (int row = 0; row < 2; row++)
            {
                float y = h - 1.1f - row * 1.7f;
                b.Box(new Vector3(0f, y + 0.25f, -half.Y + 0.22f), new Vector3(half.X - 0.2f, 0.05f, 0.05f), Hall.DarkOak);
                for (float x = -half.X + 0.45f; x < half.X - 0.3f; x += random.Range(0.36f, 0.5f))
                {
                    float length = random.Range(0.9f, 1.4f);
                    Vector3 top = new Vector3(x, y + 0.28f, -half.Y + 0.3f);
                    switch (random.Int(3))
                    {
                        case 0:     // Tongs.
                            b.Beam(top, top + new Vector3(-0.06f, -length, 0.05f), 0.02f, iron);
                            b.Beam(top, top + new Vector3(0.06f, -length, 0.05f), 0.02f, iron);
                            break;
                        case 1:     // A hammer, head up.
                            b.Beam(top - Vector3.UnitY * 0.1f, top - Vector3.UnitY * length * 0.8f, 0.03f, handle);
                            b.Box(top - Vector3.UnitY * 0.05f + Vector3.UnitZ * 0.02f, new Vector3(0.16f, 0.06f, 0.06f), iron);
                            break;
                        default:    // A file or a punch.
                            b.Beam(top, top - Vector3.UnitY * length * 0.6f, 0.025f, random.Chance(0.5f) ? iron : handle);
                            break;
                    }
                }
            }
        }

        private static void CoalBin(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial planks = Hall.Wood(96, 70, 48);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * (half.X - 0.08f), h * 0.5f, 0f), new Vector3(0.08f, h * 0.5f, half.Y), planks);
                b.Box(new Vector3(0f, h * 0.5f, s * (half.Y - 0.08f)), new Vector3(half.X, h * 0.5f, 0.08f), planks);
            }
            VertexMaterial coal = Hall.Flat(28, 26, 26);
            for (int i = 0; i < 40; i++)
            {
                float x = random.Range(-half.X + 0.25f, half.X - 0.25f), z = random.Range(-half.Y + 0.25f, half.Y - 0.25f);
                float mound = 1f - (x * x / (half.X * half.X) + z * z / (half.Y * half.Y)) * 0.6f;
                float s = random.Range(0.1f, 0.2f);
                b.Box(new Vector3(x, h * 0.55f + mound * 0.45f, z), new Vector3(s, s * 0.7f, s), coal);
            }
            b.Beam(new Vector3(0.2f, h * 0.9f, 0.1f), new Vector3(half.X + 0.4f, h + 0.9f, 0.3f), 0.03f, Hall.Wood(150, 112, 74));
            b.Box(new Vector3(0.05f, h * 0.88f, 0.05f), new Vector3(0.2f, 0.03f, 0.18f), Hall.Iron(76));
        }

        /// <summary>
        /// The crafting bench, where gear is worked on: a heavy oak bench with a leg vice, a rack of tools on the wall behind it, a blade being worked and a helmet waiting, a lamp to work by,
        /// and a box of the stones that crafting asks for, glowing. Its front is towards +z, which the hall turns to face the room.
        /// </summary>
        private static void Workbench(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial oak = Hall.Wood(140, 100, 66);
            VertexMaterial dark = Hall.Wood(86, 60, 40);
            VertexMaterial iron = Hall.Iron(78);
            float top = h;
            // The top, thick as a beam, its front edge worn pale.
            b.Box(new Vector3(0f, top - 0.11f, 0.15f), new Vector3(half.X, 0.11f, half.Y - 0.35f), oak);
            b.Box(new Vector3(0f, top - 0.11f, half.Y - 0.16f), new Vector3(half.X, 0.115f, 0.06f), Hall.Wood(176, 136, 92));
            // Legs, stretchers, and a shelf low down with what doesn't fit on top.
            foreach (float x in new[] { -half.X + 0.25f, half.X - 0.25f })
            {
                foreach (float z in new[] { -half.Y + 0.5f, half.Y - 0.35f }) b.Box(new Vector3(x, (top - 0.2f) * 0.5f, z), new Vector3(0.16f, (top - 0.2f) * 0.5f, 0.16f), dark);
                b.Box(new Vector3(x, 0.35f, 0.07f), new Vector3(0.1f, 0.08f, half.Y - 0.45f), dark);
            }
            b.Box(new Vector3(0f, 0.5f, 0.07f), new Vector3(half.X - 0.3f, 0.05f, half.Y - 0.5f), dark);
            for (int i = 0; i < 4; i++) b.Box(new Vector3(-half.X + 0.9f + i * 0.85f, 0.7f, random.Range(-0.2f, 0.4f)), new Vector3(random.Range(0.18f, 0.34f), random.Range(0.12f, 0.2f), random.Range(0.18f, 0.3f)), random.Pick(new[] { Hall.Leather(80, 54, 34), Hall.Iron(80), Hall.Wood(130, 96, 62), Hall.Cloth(150, 140, 120) }));

            // A leg vice at the left end: its screw, its handle, and its jaw.
            b.Box(new Vector3(-half.X + 0.35f, top * 0.55f, half.Y - 0.08f), new Vector3(0.14f, top * 0.55f, 0.08f), oak);
            b.Tube(new Vector3(-half.X + 0.35f, top - 0.35f, half.Y - 0.3f), new Vector3(-half.X + 0.35f, top - 0.35f, half.Y + 0.35f), 0.06f, 0.06f, 6, iron);
            b.Beam(new Vector3(-half.X + 0.05f, top - 0.35f, half.Y + 0.3f), new Vector3(-half.X + 0.65f, top - 0.35f, half.Y + 0.3f), 0.035f, Hall.Wood(150, 112, 74));

            // The rack of tools on the wall behind it.
            b.Box(new Vector3(0f, top + 1.5f, -half.Y + 0.06f), new Vector3(half.X - 0.1f, 1.3f, 0.06f), Hall.Wood(112, 82, 56));
            for (float x = -half.X + 0.4f; x < half.X - 0.3f; x += 0.42f)
            {
                Vector3 peg = new Vector3(x, top + 2.2f + random.Range(-0.1f, 0.3f), -half.Y + 0.16f);
                float length = random.Range(0.5f, 1.1f);
                if (random.Chance(0.5f))
                {
                    b.Beam(peg, peg - Vector3.UnitY * length, 0.025f, Hall.Wood(150, 112, 74));
                    b.Box(peg - Vector3.UnitY * 0.04f, new Vector3(0.12f, 0.05f, 0.05f), iron);
                }
                else b.Beam(peg, peg - Vector3.UnitY * length, 0.02f, iron);
            }

            // What's on it: a blade being worked, held in the vice's end, a helmet waiting, a whetstone, pliers, rivets, a book of notes, and a lamp.
            b.Box(new Vector3(-0.6f, top + 0.03f, 0.2f), new Vector3(1.25f, 0.025f, 0.11f), Hall.Metal(196, 198, 204));
            b.Box(new Vector3(0.75f, top + 0.06f, 0.2f), new Vector3(0.15f, 0.05f, 0.32f), Hall.Metal(150, 150, 156));
            b.Beam(new Vector3(0.9f, top + 0.06f, 0.2f), new Vector3(1.4f, top + 0.06f, 0.2f), 0.045f, Hall.Leather(70, 44, 28));
            Hall.At(b, new Vector3(1.9f, top, -0.3f), 0.6f, () =>
            {
                b.Tube(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0.42f, 0f), 0.36f, 0.34f, 10, Hall.Metal(170, 172, 178));
                b.Tube(new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.62f, 0f), 0.34f, 0.12f, 10, Hall.Metal(170, 172, 178));
                b.Box(new Vector3(0f, 0.22f, 0.33f), new Vector3(0.28f, 0.04f, 0.04f), Hall.Flat(20, 20, 22));
                b.Box(new Vector3(0f, 0.3f, 0.36f), new Vector3(0.04f, 0.16f, 0.03f), Hall.Metal(170, 172, 178));
            });
            b.Box(new Vector3(-2.2f, top + 0.05f, -0.2f), new Vector3(0.24f, 0.05f, 0.1f), Hall.Rough(120));
            b.Box(new Vector3(2.6f, top + 0.04f, 0.35f), new Vector3(0.32f, 0.04f, 0.24f), Hall.Leather(70, 40, 24));
            b.Box(new Vector3(2.6f, top + 0.085f, 0.35f), new Vector3(0.3f, 0.005f, 0.22f), Hall.Paper);
            for (int i = 0; i < 8; i++) b.Box(new Vector3(-1.4f + random.Range(-0.3f, 0.3f), top + 0.02f, 0.45f + random.Range(-0.1f, 0.1f)), new Vector3(0.025f), Hall.Iron(90));

            // A box of the stones a crafter works with, each with its own light.
            Vector3 box = new Vector3(-2.9f, top, 0.3f);
            b.Box(box + Vector3.UnitY * 0.09f, new Vector3(0.36f, 0.09f, 0.26f), Hall.Wood(96, 66, 44));
            Vector3[] glows = { new(0.35f, 0.85f, 1f), new(0.85f, 0.4f, 1f), new(1f, 0.62f, 0.2f), new(0.45f, 1f, 0.6f) };
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = glows[i % glows.Length];
                b.Box(box + new Vector3(-0.22f + (i % 3) * 0.22f, 0.21f, -0.1f + (i / 3) * 0.2f), new Vector3(0.07f, 0.06f, 0.07f), Hall.Gem(c.X, c.Y, c.Z, 1.4f));
            }
            lights.Add(AssetLight.Glow(box + Vector3.UnitY * 0.5f, new Vector3(0.6f, 0.75f, 1f), 9f, 6f));

            // The lamp: a candle in a little iron lantern, open at the front.
            Vector3 lamp = new Vector3(half.X - 0.55f, top, -0.35f);
            b.Box(lamp + Vector3.UnitY * 0.03f, new Vector3(0.2f, 0.03f, 0.2f), iron);
            foreach (float x in new[] { -0.17f, 0.17f }) b.Beam(lamp + new Vector3(x, 0.05f, -0.17f), lamp + new Vector3(x, 0.6f, -0.17f), 0.015f, iron);
            b.Box(lamp + Vector3.UnitY * 0.62f, new Vector3(0.22f, 0.03f, 0.22f), iron);
            b.Tube(lamp + Vector3.UnitY * 0.64f, lamp + Vector3.UnitY * 0.8f, 0.2f, 0.03f, 4, iron);
            Hall.Candle(b, lamp + Vector3.UnitY * 0.06f, 0.26f, 0.05f, lights);
            lights.Add(AssetLight.Candles(lamp + Vector3.UnitY * 0.5f) with { Intensity = 28f, Radius = 12f });
        }

        private static void WeaponRack(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial frame = Hall.Wood(110, 78, 52);
            VertexMaterial steel = Hall.Metal(178, 180, 186);
            b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(half.X, 0.2f, half.Y), frame);
            foreach (float x in new[] { -half.X + 0.12f, half.X - 0.12f }) b.Box(new Vector3(x, h * 0.5f, -half.Y + 0.15f), new Vector3(0.1f, h * 0.5f, 0.1f), frame);
            b.Box(new Vector3(0f, h - 0.1f, -half.Y + 0.15f), new Vector3(half.X, 0.1f, 0.1f), frame);
            b.Box(new Vector3(0f, h * 0.55f, -half.Y + 0.2f), new Vector3(half.X, 0.08f, 0.12f), frame);
            for (float x = -half.X + 0.4f; x < half.X - 0.3f; x += 0.42f)
            {
                switch (random.Int(3))
                {
                    case 0:     // A sword, point down in the base.
                        b.Box(new Vector3(x, 1.6f, 0f), new Vector3(0.05f, 1.3f, 0.015f), steel);
                        b.Box(new Vector3(x, 2.95f, 0f), new Vector3(0.24f, 0.04f, 0.04f), Hall.Iron(80));
                        b.Beam(new Vector3(x, 3f, 0f), new Vector3(x, 3.5f, 0f), 0.04f, Hall.Leather(70, 44, 28));
                        break;
                    case 1:     // A spear.
                        b.Beam(new Vector3(x, 0.3f, 0f), new Vector3(x, h - 0.2f, -0.1f), 0.035f, Hall.Wood(150, 112, 74));
                        b.Tube(new Vector3(x, h - 0.2f, -0.1f), new Vector3(x, h + 0.35f, -0.12f), 0.07f, 0.005f, 4, steel);
                        break;
                    default:    // An axe, head up.
                        b.Beam(new Vector3(x, 0.3f, 0f), new Vector3(x, 2.7f, -0.05f), 0.04f, Hall.Wood(150, 112, 74));
                        b.Box(new Vector3(x + 0.15f, 2.55f, -0.05f), new Vector3(0.18f, 0.16f, 0.02f), steel);
                        break;
                }
            }
            // A round shield hung on the top rail.
            b.Tube(new Vector3(0f, h - 0.9f, -half.Y + 0.28f), new Vector3(0f, h - 0.9f, -half.Y + 0.36f), 0.6f, 0.6f, 14, Hall.Wood(124, 88, 56));
            b.Tube(new Vector3(0f, h - 0.9f, -half.Y + 0.36f), new Vector3(0f, h - 0.9f, -half.Y + 0.46f), 0.16f, 0.1f, 10, Hall.Metal(160, 160, 166));
        }

        private static void ArmourStand(MeshBuilder b, float r, float h)
        {
            VertexMaterial wood = Hall.Wood(118, 84, 56);
            b.Tube(Vector3.Zero, new Vector3(0f, 0.18f, 0f), r, r * 0.92f, 10, wood);
            b.Tube(new Vector3(0f, 0.18f, 0f), new Vector3(0f, h - 1.2f, 0f), 0.08f, 0.07f, 6, wood);
            b.Beam(new Vector3(-0.85f, h - 1.55f, 0f), new Vector3(0.85f, h - 1.55f, 0f), 0.06f, wood);
            // A breastplate on it, mail beneath, and a helmet on top.
            VertexMaterial plate = Hall.Metal(176, 178, 184);
            VertexMaterial mail = new VertexMaterial(MaterialCatalog.Mail.Index, new Colour(150, 152, 158));
            b.Tube(new Vector3(0f, h - 3.4f, 0f), new Vector3(0f, h - 2.2f, 0f), 0.62f, 0.66f, 12, mail);
            b.Tube(new Vector3(0f, h - 2.4f, 0f), new Vector3(0f, h - 1.5f, 0f), 0.68f, 0.56f, 12, plate);
            b.Box(new Vector3(0f, h - 1.95f, 0.6f), new Vector3(0.05f, 0.42f, 0.04f), Hall.Metal(200, 202, 208));
            foreach (float s in new[] { -1f, 1f })
            {
                b.Tube(new Vector3(s * 0.62f, h - 1.6f, 0f), new Vector3(s * 0.95f, h - 1.75f, 0f), 0.3f, 0.26f, 8, plate);
            }
            b.Tube(new Vector3(0f, h - 1.45f, 0f), new Vector3(0f, h - 1.2f, 0f), 0.16f, 0.14f, 6, wood);
            b.Tube(new Vector3(0f, h - 1.25f, 0f), new Vector3(0f, h - 0.6f, 0f), 0.42f, 0.4f, 10, plate);
            b.Tube(new Vector3(0f, h - 0.6f, 0f), new Vector3(0f, h - 0.2f, 0f), 0.4f, 0.1f, 10, plate);
            b.Box(new Vector3(0f, h - 0.85f, 0.39f), new Vector3(0.3f, 0.04f, 0.04f), Hall.Flat(18, 18, 20));
        }

        private static void StockShelves(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(112, 80, 54);
            Shelving(b, half, h, wood, 4, out float[] levels);
            foreach (float y in levels.Take(levels.Length - 1))
            {
                for (float x = -half.X + 0.35f; x < half.X - 0.3f; x += random.Range(0.45f, 0.8f))
                {
                    switch (random.Int(4))
                    {
                        case 0:     // Bars of iron, stacked.
                            for (int i = 0; i < 3; i++) b.Box(new Vector3(x, y + 0.07f + i * 0.13f, 0f), new Vector3(0.2f, 0.06f, half.Y * 0.6f), Hall.Iron(96));
                            break;
                        case 1:     // A coil of rope.
                            b.Tube(new Vector3(x, y, 0f), new Vector3(x, y + 0.3f, 0f), 0.28f, 0.26f, 10, Hall.Cloth(150, 126, 88));
                            break;
                        case 2:     // A jar.
                            b.Tube(new Vector3(x, y, 0f), new Vector3(x, y + 0.42f, 0f), 0.16f, 0.13f, 8, Hall.Brick(168, 112, 76));
                            break;
                        default:    // Folded hides.
                            b.Box(new Vector3(x, y + 0.12f, 0f), new Vector3(0.3f, 0.12f, half.Y * 0.7f), Hall.Leather(110, 78, 50));
                            break;
                    }
                }
            }
        }

        // Uprights, a back and shelves, its front open towards +z. The heights of the tops of the shelves come back in `levels`.
        private static void Shelving(MeshBuilder b, Vector2 half, float h, VertexMaterial wood, int shelves, out float[] levels)
        {
            b.Box(new Vector3(0f, h * 0.5f, -half.Y + 0.05f), new Vector3(half.X, h * 0.5f, 0.05f), wood);
            foreach (float x in new[] { -half.X + 0.07f, half.X - 0.07f }) b.Box(new Vector3(x, h * 0.5f, 0f), new Vector3(0.07f, h * 0.5f, half.Y), wood);
            levels = new float[shelves + 1];
            for (int i = 0; i <= shelves; i++)
            {
                float y = 0.15f + (h - 0.4f) * i / shelves;
                levels[i] = y + 0.05f;
                b.Box(new Vector3(0f, y, 0f), new Vector3(half.X, 0.05f, half.Y), wood);
            }
            b.Box(new Vector3(0f, h - 0.1f, 0.04f), new Vector3(half.X + 0.08f, 0.1f, half.Y + 0.04f), wood);
        }

        private static void Grindstone(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial frame = Hall.Wood(112, 80, 54);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * (half.X - 0.15f), 0.75f, 0f), new Vector3(0.12f, 0.75f, half.Y - 0.1f), frame);
                b.Beam(new Vector3(s * (half.X - 0.15f), 0.1f, -half.Y + 0.15f), new Vector3(s * (half.X - 0.15f), 1.4f, 0f), 0.08f, frame);
                b.Beam(new Vector3(s * (half.X - 0.15f), 0.1f, half.Y - 0.15f), new Vector3(s * (half.X - 0.15f), 1.4f, 0f), 0.08f, frame);
            }
            // The wheel, turning on an axle, with the trough of water it runs through.
            float y = h - 1.05f;
            b.Tube(new Vector3(-0.22f, y, 0f), new Vector3(0.22f, y, 0f), 1f, 1f, 18, Hall.Rough(170));
            b.Tube(new Vector3(-half.X + 0.05f, y, 0f), new Vector3(half.X + 0.35f, y, 0f), 0.06f, 0.06f, 6, Hall.Iron(80));
            b.Beam(new Vector3(half.X + 0.35f, y, 0f), new Vector3(half.X + 0.35f, y - 0.5f, 0.3f), 0.04f, Hall.Iron(80));
            b.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.18f, 0.6f), Hall.Wood(96, 70, 48));
            b.Box(new Vector3(0f, 0.47f, 0f), new Vector3(0.36f, 0.01f, 0.5f), Hall.Mirror(46, 56, 58));
        }

        private static void LeatherTable(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            Table(b, half, h, Hall.Wood(132, 96, 64), Hall.Wood(96, 68, 46));
            // A hide spread out, straps coiled, buckles, a knife and an awl.
            b.Box(new Vector3(-0.4f, h + 0.015f, 0f), new Vector3(half.X * 0.55f, 0.015f, half.Y * 0.6f), Hall.Leather(128, 88, 54));
            for (int i = 0; i < 3; i++)
            {
                float x = half.X * 0.45f + i * 0.08f;
                b.Box(new Vector3(x, h + 0.03f + i * 0.02f, -0.3f + i * 0.22f), new Vector3(0.5f, 0.012f, 0.06f), Hall.Leather(70, 44, 26));
            }
            for (int i = 0; i < 5; i++) b.Box(new Vector3(half.X * 0.6f + random.Range(-0.3f, 0.3f), h + 0.02f, 0.4f + random.Range(-0.15f, 0.15f)), new Vector3(0.06f, 0.01f, 0.05f), Hall.Metal(170, 172, 178));
            b.Beam(new Vector3(-half.X * 0.2f, h + 0.03f, half.Y * 0.7f), new Vector3(half.X * 0.05f, h + 0.03f, half.Y * 0.75f), 0.02f, Hall.Metal(190, 192, 198));
        }

        /// <summary>A plain heavy table: a top a hand thick and four legs, braced.</summary>
        public static void Table(MeshBuilder b, Vector2 half, float h, VertexMaterial top, VertexMaterial legs)
        {
            b.Box(new Vector3(0f, h - 0.09f, 0f), new Vector3(half.X, 0.09f, half.Y), top);
            foreach (float x in new[] { -half.X + 0.25f, half.X - 0.25f })
            {
                foreach (float z in new[] { -half.Y + 0.22f, half.Y - 0.22f }) Hall.Leg(b, new Vector3(x, 0f, z), h - 0.18f, 0.16f, legs);
            }
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(half.X - 0.25f, 0.06f, 0.06f), legs);
            b.Box(new Vector3(0f, h - 0.3f, half.Y - 0.22f), new Vector3(half.X - 0.3f, 0.1f, 0.04f), legs);
            b.Box(new Vector3(0f, h - 0.3f, -half.Y + 0.22f), new Vector3(half.X - 0.3f, 0.1f, 0.04f), legs);
        }

        private static void Firewood(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial bark = new VertexMaterial(MaterialCatalog.BarkFurrowed.Index, new Colour(116, 100, 86));
            VertexMaterial ends = Hall.Wood(172, 136, 96);
            for (float y = 0.18f; y < h - 0.1f; y += 0.32f)
            {
                float offset = ((int)(y / 0.32f) % 2) * 0.16f;
                for (float x = -half.X + 0.18f + offset; x < half.X - 0.15f; x += 0.34f)
                {
                    float r = random.Range(0.12f, 0.16f);
                    Vector3 back = new Vector3(x, y, -half.Y + 0.05f), front = new Vector3(x, y, half.Y - random.Range(0f, 0.15f));
                    b.Tube(back, front, r, r, 5, bark, capped: false);
                    b.Tube(front - Vector3.UnitZ * 0.01f, front + Vector3.UnitZ * 0.02f, r * 0.96f, r * 0.96f, 5, ends);
                }
            }
        }

        private static void Casks(MeshBuilder b, float r, float h, WorldRandom random)
        {
            int count = r > 1f ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                float a = MathF.Tau * i / count + 0.4f;
                float cr = count == 1 ? r * 0.9f : r * 0.48f;
                Vector3 foot = count == 1 ? Vector3.Zero : new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (r - cr);
                Barrel(b, foot, h * random.Range(0.92f, 1f), cr);
            }
        }

        /// <summary>A barrel standing on <paramref name="foot"/>: staves bellied out, hoops, a lid.</summary>
        public static void Barrel(MeshBuilder b, Vector3 foot, float height, float radius)
        {
            VertexMaterial staves = Hall.Wood(132, 94, 60);
            VertexMaterial hoop = Hall.Iron(84);
            b.Tube(foot, foot + Vector3.UnitY * height * 0.5f, radius * 0.86f, radius, 12, staves, capped: false);
            b.Tube(foot + Vector3.UnitY * height * 0.5f, foot + Vector3.UnitY * height, radius, radius * 0.86f, 12, staves);
            foreach (float t in new[] { 0.1f, 0.33f, 0.67f, 0.9f })
            {
                float rr = radius * (t < 0.5f ? 0.86f + 0.28f * t : 0.86f + 0.28f * (1f - t)) + 0.012f;
                b.Tube(foot + Vector3.UnitY * (height * t - 0.04f), foot + Vector3.UnitY * (height * t + 0.04f), rr, rr, 12, hoop, capped: false);
            }
        }

        private static void Crates(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            float low = h * 0.5f;
            Crate(b, new Vector3(-half.X * 0.45f, 0f, 0f), new Vector3(half.X * 0.52f, low * 0.5f, half.Y * 0.9f));
            Crate(b, new Vector3(half.X * 0.5f, 0f, -0.1f), new Vector3(half.X * 0.46f, low * 0.5f, half.Y * 0.8f));
            Crate(b, new Vector3(-half.X * 0.3f, low, 0.05f), new Vector3(half.X * 0.45f, (h - low) * 0.5f, half.Y * 0.7f));
        }

        private static void Crate(MeshBuilder b, Vector3 foot, Vector3 half)
        {
            VertexMaterial boards = Hall.Wood(156, 120, 82);
            VertexMaterial edge = Hall.Wood(110, 80, 54);
            b.Box(foot + Vector3.UnitY * half.Y, half, boards);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f }) b.Box(foot + new Vector3(sx * (half.X - 0.05f), half.Y, sz * (half.Z - 0.05f)), new Vector3(0.06f, half.Y + 0.005f, 0.06f), edge);
            }
            b.Beam(foot + new Vector3(-half.X + 0.05f, 0.05f, half.Z + 0.01f), foot + new Vector3(half.X - 0.05f, half.Y * 2f - 0.05f, half.Z + 0.01f), 0.04f, edge);
        }

        // ---- The Gaming House.

        /// <summary>
        /// The great table: an oval of green cloth in a carved rim on six turned legs, and on it everything a game of chance needs, dice and cups, cards dealt and fanned, stacks of coin and a
        /// box of more, and candles at either end.
        /// </summary>
        private static void GamingTable(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(102, 66, 42);
            VertexMaterial carved = Hall.Wood(84, 54, 34);
            VertexMaterial felt = Hall.Cloth(32, 92, 56);
            float top = h;
            float r = half.Y;
            float straight = half.X - r;
            // The rim and the cloth: a stadium of two half rounds and a straight between.
            Stadium(b, straight, r, top - 0.18f, top, wood, 18);
            Stadium(b, straight, r - 0.35f, top + 0.005f, top + 0.012f, felt, 18, open: true);
            Stadium(b, straight, r + 0.06f, top - 0.32f, top - 0.18f, carved, 18);
            // An apron below the rim, carved with a band, and six legs.
            Stadium(b, straight - 0.2f, r - 0.35f, top - 0.62f, top - 0.32f, carved, 14);
            foreach ((float x, float z) in new[] { (-straight, -r + 0.6f), (-straight, r - 0.6f), (straight, -r + 0.6f), (straight, r - 0.6f), (0f, -r + 0.5f), (0f, r - 0.5f) })
            {
                Hall.Leg(b, new Vector3(x, 0f, z), top - 0.6f, 0.2f, carved);
            }

            // On the cloth.
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = new Vector3(random.Range(-1.4f, 1.4f), top + 0.13f, random.Range(-0.8f, 0.9f));
                GamblingDie(b, at, 0.11f, random.Range(0f, MathF.Tau));
            }
            foreach (float x in new[] { -2.1f, 2.3f })
            {
                Vector3 cup = new Vector3(x, top + 0.01f, random.Range(-0.6f, 0.4f));
                b.Tube(cup, cup + Vector3.UnitY * 0.45f, 0.17f, 0.21f, 10, Hall.Leather(70, 40, 24), capped: false);
                b.Tube(cup + Vector3.UnitY * 0.42f, cup + Vector3.UnitY * 0.46f, 0.215f, 0.215f, 10, Hall.Leather(90, 56, 32), capped: false);
            }
            // Cards dealt to each seat, and a fan of them in the dealer's place.
            foreach (Vector2 seat in new[] { new Vector2(-3f, 0.4f), new Vector2(3f, 0.4f), new Vector2(-1.4f, 1.55f), new Vector2(1.4f, 1.55f) })
            {
                for (int i = 0; i < 3; i++) Card(b, new Vector3(seat.X + i * 0.12f, top + 0.016f + i * 0.004f, seat.Y + random.Range(-0.05f, 0.05f)), random.Range(-0.4f, 0.4f), random.Chance(0.4f));
                Coins(b, new Vector3(seat.X + 0.55f, top + 0.012f, seat.Y - 0.35f), random.Int(3, 8), random);
            }
            for (int i = 0; i < 7; i++) Card(b, new Vector3(-0.3f + i * 0.09f, top + 0.016f + i * 0.003f, -1.55f), -0.6f + i * 0.2f, true);
            for (int i = 0; i < 4; i++) Coins(b, new Vector3(-0.9f + i * 0.5f, top + 0.012f, -1.05f), random.Int(6, 14), random);

            // The bank: a small iron-bound box at the dealer's side, open, full of coin.
            Vector3 bank = new Vector3(1.6f, top, -1.35f);
            b.Box(bank + Vector3.UnitY * 0.18f, new Vector3(0.42f, 0.18f, 0.28f), Hall.Wood(96, 62, 40));
            b.Box(bank + Vector3.UnitY * 0.34f, new Vector3(0.38f, 0.02f, 0.24f), Hall.Metal(214, 170, 74));
            b.Box(bank + new Vector3(0f, 0.55f, -0.3f), new Vector3(0.42f, 0.22f, 0.025f), Hall.Wood(96, 62, 40));

            // Candles at either end, in heavy sticks.
            foreach (float x in new[] { -straight - r * 0.55f, straight + r * 0.55f })
            {
                Vector3 foot = new Vector3(x, top, 0f);
                Hall.Turned(b, foot, Hall.Pewter, 8, (0f, 0.2f), (0.06f, 0.18f), (0.1f, 0.06f), (0.5f, 0.05f), (0.56f, 0.12f), (0.6f, 0.1f));
                Hall.Candle(b, foot + Vector3.UnitY * 0.6f, 0.42f, 0.065f, lights);
                lights.Add(AssetLight.Candles(foot + Vector3.UnitY * 1.3f) with { Intensity = 26f, Radius = 13f });
            }
        }

        // A flat oval: two half rounds joined by straights, with a top face and a rim, from y0 to y1.
        private static void Stadium(MeshBuilder b, float straight, float r, float y0, float y1, VertexMaterial material, int segments, bool open = false)
        {
            var outline = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                float a = -MathF.PI * 0.5f + MathF.PI * i / segments;
                outline.Add(new Vector3(straight + MathF.Cos(a) * r, 0f, MathF.Sin(a) * r));
            }
            for (int i = 0; i <= segments; i++)
            {
                float a = MathF.PI * 0.5f + MathF.PI * i / segments;
                outline.Add(new Vector3(-straight + MathF.Cos(a) * r, 0f, MathF.Sin(a) * r));
            }
            // The top, as a fan from the middle, wound to face up.
            Vector3 middle = new Vector3(0f, y1, 0f);
            Vector3 origin = new Vector3(-straight - r, y1, -r);
            Vector4 bounds = new Vector4(0f, (straight + r) * 2f, -r * 2f, 0f);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i] + Vector3.UnitY * y1, c = outline[(i + 1) % outline.Count] + Vector3.UnitY * y1;
                b.PlaneTriangle(middle, c, a, material, origin, Vector3.UnitX, -Vector3.UnitZ, bounds);
            }
            if (open) return;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector3 a = outline[i], c = outline[(i + 1) % outline.Count];
                b.Quad(c + Vector3.UnitY * y0, a + Vector3.UnitY * y0, a + Vector3.UnitY * y1, c + Vector3.UnitY * y1, material);
            }
        }

        private static void GamblingDie(MeshBuilder b, Vector3 at, float half, float yaw) => HallShells.Die(b, at, half, yaw);

        // A card lying on the table, face down or face up with a mark.
        private static void Card(MeshBuilder b, Vector3 at, float yaw, bool faceUp)
        {
            Hall.At(b, at, yaw, () =>
            {
                b.Box(Vector3.Zero, new Vector3(0.13f, 0.004f, 0.19f), faceUp ? Hall.Flat(222, 212, 190) : Hall.Flat(40, 70, 110));
                if (faceUp) b.Box(new Vector3(0f, 0.005f, 0f), new Vector3(0.05f, 0.002f, 0.07f), Hall.Flat(150, 40, 40));
                else b.Box(new Vector3(0f, 0.005f, 0f), new Vector3(0.1f, 0.002f, 0.16f), Hall.Flat(200, 180, 120));
            });
        }

        // A stack of coins, the odd one fallen beside it.
        private static void Coins(MeshBuilder b, Vector3 foot, int count, WorldRandom random)
        {
            VertexMaterial gold = Hall.Metal(214, 168, 70);
            VertexMaterial silver = Hall.Metal(190, 192, 198);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = foot + new Vector3(random.Range(-0.012f, 0.012f), i * 0.034f, random.Range(-0.012f, 0.012f));
                b.Tube(at, at + Vector3.UnitY * 0.03f, 0.1f, 0.1f, 8, i % 4 == 3 ? silver : gold);
            }
            if (random.Chance(0.6f)) b.Tube(foot + new Vector3(0.24f, 0f, 0.1f), foot + new Vector3(0.24f, 0.03f, 0.1f), 0.1f, 0.1f, 8, gold);
        }

        private static void Chair(MeshBuilder b, float r, float h, int variant)
        {
            VertexMaterial wood = Hall.Wood(98, 64, 42);
            Vector3 cushion = Hall.Cloths[variant % 3 == 0 ? 1 : variant % 3 == 1 ? 0 : 3];
            float seat = 1.1f;
            float w = r * 0.78f;
            foreach (float x in new[] { -w + 0.1f, w - 0.1f })
            {
                foreach (float z in new[] { -w + 0.1f, w - 0.1f }) Hall.Leg(b, new Vector3(x, 0f, z), seat - 0.06f, 0.07f, wood);
                b.Box(new Vector3(x, h * 0.5f + 0.3f, -w + 0.1f), new Vector3(0.07f, h * 0.5f - 0.3f, 0.07f), wood);
            }
            b.Box(new Vector3(0f, seat, 0f), new Vector3(w, 0.06f, w), wood);
            b.Box(new Vector3(0f, seat + 0.1f, 0.03f), new Vector3(w - 0.08f, 0.06f, w - 0.1f), Hall.ClothOf(cushion));
            // A carved back: top rail, a splat and a lower rail.
            b.Box(new Vector3(0f, h - 0.12f, -w + 0.1f), new Vector3(w + 0.04f, 0.12f, 0.06f), wood);
            b.Box(new Vector3(0f, (h + seat) * 0.5f, -w + 0.1f), new Vector3(w * 0.32f, (h - seat) * 0.5f - 0.2f, 0.04f), wood);
            b.Box(new Vector3(0f, seat + 0.45f, -w + 0.1f), new Vector3(w, 0.05f, 0.05f), wood);
        }

        private static void Stool(MeshBuilder b, float r, float h)
        {
            VertexMaterial wood = Hall.Wood(126, 90, 60);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.3f;
                b.Beam(new Vector3(MathF.Cos(a) * r * 0.9f, 0f, MathF.Sin(a) * r * 0.9f), new Vector3(MathF.Cos(a) * r * 0.45f, h - 0.1f, MathF.Sin(a) * r * 0.45f), 0.05f, wood);
            }
            b.Tube(new Vector3(0f, h - 0.12f, 0f), new Vector3(0f, h, 0f), r * 0.85f, r * 0.85f, 10, wood);
        }

        private static void Bar(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial front = Hall.Wood(92, 60, 40);
            VertexMaterial top = Hall.Wood(124, 84, 54);
            VertexMaterial trim = Hall.Wood(70, 46, 30);
            // A panelled front, a thick top that overhangs it, and a rail for feet.
            b.Box(new Vector3(0f, (h - 0.15f) * 0.5f, 0.05f), new Vector3(half.X - 0.05f, (h - 0.15f) * 0.5f, half.Y - 0.2f), front);
            int panels = Math.Max(2, (int)(half.X * 2f / 1.6f));
            for (int i = 0; i < panels; i++)
            {
                float x = -half.X + 0.2f + (half.X * 2f - 0.4f) * (i + 0.5f) / panels;
                b.Box(new Vector3(x, h * 0.45f, half.Y - 0.12f), new Vector3((half.X * 2f - 0.4f) / panels * 0.5f - 0.12f, h * 0.32f, 0.04f), trim);
            }
            b.Box(new Vector3(0f, h - 0.08f, 0.08f), new Vector3(half.X + 0.12f, 0.08f, half.Y + 0.06f), top);
            b.Tube(new Vector3(-half.X, 0.35f, half.Y + 0.15f), new Vector3(half.X, 0.35f, half.Y + 0.15f), 0.05f, 0.05f, 6, Hall.Copper);
            // On it: mugs, a jug, a cloth, a plate of bread.
            for (float x = -half.X + 0.6f; x < half.X - 0.4f; x += random.Range(0.7f, 1.4f))
            {
                if (random.Chance(0.3f)) continue;
                Vector3 at = new Vector3(x, h, random.Range(-0.2f, 0.3f));
                if (random.Chance(0.75f)) Mug(b, at, random.Chance(0.5f) ? Hall.Pewter : Hall.Brick(170, 120, 84));
                else
                {
                    b.Tube(at, at + Vector3.UnitY * 0.5f, 0.16f, 0.12f, 8, Hall.Brick(160, 112, 76));
                    b.Tube(at + Vector3.UnitY * 0.5f, at + Vector3.UnitY * 0.62f, 0.12f, 0.09f, 8, Hall.Brick(160, 112, 76));
                }
            }
            b.Box(new Vector3(half.X * 0.5f, h + 0.01f, 0.1f), new Vector3(0.35f, 0.01f, 0.25f), Hall.Cloth(206, 196, 168));
            b.Tube(new Vector3(-half.X * 0.45f, h, 0.05f), new Vector3(-half.X * 0.45f, h + 0.05f, 0.05f), 0.36f, 0.34f, 12, Hall.Wood(150, 112, 74));
            for (int i = 0; i < 3; i++) b.Box(new Vector3(-half.X * 0.45f + (i - 1) * 0.15f, h + 0.1f, 0.05f), new Vector3(0.08f, 0.05f, 0.14f), Hall.Wood(196, 150, 96));
        }

        // A mug with a handle.
        private static void Mug(MeshBuilder b, Vector3 foot, VertexMaterial material)
        {
            b.Tube(foot, foot + Vector3.UnitY * 0.3f, 0.12f, 0.11f, 8, material);
            b.Beam(foot + new Vector3(0.11f, 0.24f, 0f), foot + new Vector3(0.2f, 0.15f, 0f), 0.025f, material);
            b.Beam(foot + new Vector3(0.2f, 0.15f, 0f), foot + new Vector3(0.11f, 0.06f, 0f), 0.025f, material);
        }

        private static void BarShelves(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(86, 56, 38);
            Shelving(b, half, h, wood, 4, out float[] levels);
            // A cask rack across the bottom, bottles and mugs and plates above.
            for (float x = -half.X + 0.6f; x < half.X - 0.5f; x += 1.15f)
            {
                Vector3 c = new Vector3(x, levels[0] + 0.45f, 0f);
                b.Tube(c - Vector3.UnitZ * 0.32f, c + Vector3.UnitZ * 0.32f, 0.42f, 0.42f, 12, Hall.Wood(130, 92, 58));
                b.Tube(c + Vector3.UnitZ * 0.32f, c + Vector3.UnitZ * 0.36f, 0.4f, 0.4f, 12, Hall.Wood(160, 120, 80));
                b.Tube(c + new Vector3(0f, -0.15f, 0.36f), c + new Vector3(0f, -0.15f, 0.48f), 0.04f, 0.04f, 5, Hall.Copper);
            }
            Vector3[] glass = { new(40, 86, 50), new(120, 70, 24), new(70, 40, 30), new(160, 150, 120), new(36, 50, 90) };
            for (int shelf = 1; shelf < levels.Length - 1; shelf++)
            {
                float y = levels[shelf];
                for (float x = -half.X + 0.3f; x < half.X - 0.25f; x += random.Range(0.22f, 0.4f))
                {
                    switch (random.Int(5))
                    {
                        case 0:
                        case 1:
                        case 2:
                            Vector3 c = random.Pick(glass);
                            float bh = random.Range(0.42f, 0.62f);
                            Vector3 foot = new Vector3(x, y, random.Range(-0.1f, 0.1f));
                            b.Tube(foot, foot + Vector3.UnitY * bh * 0.7f, 0.1f, 0.1f, 7, Hall.Mirror((int)c.X, (int)c.Y, (int)c.Z));
                            b.Tube(foot + Vector3.UnitY * bh * 0.7f, foot + Vector3.UnitY * bh, 0.09f, 0.035f, 7, Hall.Mirror((int)c.X, (int)c.Y, (int)c.Z));
                            break;
                        case 3:
                            Mug(b, new Vector3(x, y, 0.1f), Hall.Pewter);
                            break;
                        default:
                            b.Tube(new Vector3(x, y + 0.36f, -half.Y + 0.15f), new Vector3(x, y + 0.36f, -half.Y + 0.19f), 0.34f, 0.34f, 14, random.Chance(0.5f) ? Hall.Pewter : Hall.Brick(190, 150, 110));
                            x += 0.3f;
                            break;
                    }
                }
            }
        }

        private static void Fireplace(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, HallKit kit)
        {
            VertexMaterial stone = Hall.Stone(170, 160, 144);
            VertexMaterial dark = Hall.Flat(30, 26, 24);
            float opening = half.X * 0.62f, openTop = h * 0.62f;
            // The surround: jambs, a lintel, a mantel shelf, and the breast up to the top of the walls behind it.
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * (opening + (half.X - opening) * 0.5f), openTop * 0.5f, 0f), new Vector3((half.X - opening) * 0.5f, openTop * 0.5f, half.Y), stone);
            b.Box(new Vector3(0f, openTop + 0.3f, 0f), new Vector3(half.X, 0.3f, half.Y), stone);
            b.Box(new Vector3(0f, openTop + 0.68f, 0.08f), new Vector3(half.X + 0.2f, 0.09f, half.Y + 0.1f), Hall.Wood(96, 64, 42));
            b.Box(new Vector3(0f, (openTop + 9f) * 0.5f + 0.4f, -half.Y * 0.4f), new Vector3(half.X - 0.3f, (9f - openTop) * 0.5f - 0.4f, half.Y * 0.6f), kit.InnerWall);
            // The firebox, soot black, with its hearth stone and the fire.
            b.Box(new Vector3(0f, openTop * 0.5f, -half.Y + 0.1f), new Vector3(opening, openTop * 0.5f, 0.1f), dark);
            b.Box(new Vector3(0f, 0.08f, half.Y * 0.2f), new Vector3(half.X + 0.4f, 0.08f, half.Y + 0.6f), Hall.Stone(140, 132, 120));
            foreach (float s in new[] { -0.45f, 0.45f }) b.Box(new Vector3(s * opening, 0.3f, 0f), new Vector3(0.05f, 0.22f, half.Y * 0.6f), Hall.Iron(70));
            VertexMaterial log = new VertexMaterial(MaterialCatalog.BarkFurrowed.Index, new Colour(90, 74, 60));
            b.Tube(new Vector3(-opening * 0.7f, 0.42f, -0.1f), new Vector3(opening * 0.7f, 0.42f, 0.05f), 0.16f, 0.15f, 6, log);
            b.Tube(new Vector3(-opening * 0.6f, 0.62f, 0.15f), new Vector3(opening * 0.5f, 0.66f, -0.2f), 0.14f, 0.13f, 6, log);
            b.Box(new Vector3(0f, 0.25f, 0f), new Vector3(opening * 0.55f, 0.08f, half.Y * 0.4f), Hall.Ember);
            Hall.Flame(lights, new Vector3(-0.3f, 0.55f, 0f), 0.75f);
            Hall.Flame(lights, new Vector3(0.35f, 0.6f, 0.05f), 0.6f);
            lights.Add(AssetLight.Campfire(new Vector3(0f, 1.1f, half.Y + 0.4f)) with { Intensity = 120f, Radius = 20f });
            // On the mantel: candles, a pair of jugs and a clock-like box.
            float shelf = openTop + 0.77f;
            Hall.Candle(b, new Vector3(-half.X + 0.3f, shelf, 0.1f), 0.5f, 0.06f, lights);
            Hall.Candle(b, new Vector3(half.X - 0.3f, shelf, 0.1f), 0.5f, 0.06f, lights);
            b.Tube(new Vector3(-0.8f, shelf, 0.05f), new Vector3(-0.8f, shelf + 0.45f, 0.05f), 0.15f, 0.1f, 8, Hall.Copper);
            b.Tube(new Vector3(0.8f, shelf, 0.05f), new Vector3(0.8f, shelf + 0.45f, 0.05f), 0.15f, 0.1f, 8, Hall.Copper);
            b.Box(new Vector3(0f, shelf + 0.3f, 0.05f), new Vector3(0.3f, 0.3f, 0.15f), Hall.Wood(80, 52, 34));
            b.Tube(new Vector3(0f, shelf + 0.34f, 0.2f), new Vector3(0f, shelf + 0.34f, 0.22f), 0.18f, 0.18f, 12, Hall.Paper);
        }

        private static void Armchair(MeshBuilder b, float r, float h, int variant)
        {
            Vector3 cloth = variant == 0 ? Hall.Cloths[0] : Hall.Cloths[1];
            VertexMaterial upholstery = Hall.ClothOf(cloth);
            VertexMaterial wood = Hall.Wood(86, 56, 36);
            float w = r * 0.8f;
            foreach (float x in new[] { -w + 0.12f, w - 0.12f })
            {
                foreach (float z in new[] { -w + 0.12f, w - 0.12f }) Hall.Leg(b, new Vector3(x, 0f, z), 0.45f, 0.08f, wood);
            }
            b.Box(new Vector3(0f, 0.75f, 0f), new Vector3(w, 0.3f, w), upholstery);
            b.Box(new Vector3(0f, 1.1f, 0.08f), new Vector3(w - 0.15f, 0.08f, w - 0.2f), Hall.ClothOf(cloth * 1.15f));
            // The wings and the tall back.
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * (w - 0.1f), 1.45f, -0.05f), new Vector3(0.12f, 0.45f, w - 0.1f), upholstery);
            b.Box(new Vector3(0f, (h + 0.9f) * 0.5f, -w + 0.15f), new Vector3(w, (h - 0.9f) * 0.5f, 0.15f), upholstery);
            b.Box(new Vector3(0f, h - 0.05f, -w + 0.15f), new Vector3(w * 0.9f, 0.08f, 0.16f), Hall.ClothOf(cloth * 0.85f));
        }

        private static void RoundTable(MeshBuilder b, float r, float h, int variant, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(110, 74, 48);
            b.Tube(new Vector3(0f, h - 0.12f, 0f), new Vector3(0f, h, 0f), r, r, 16, wood);
            Hall.Turned(b, Vector3.Zero, wood, 8, (0f, r * 0.55f), (0.1f, r * 0.5f), (0.18f, 0.12f), (h * 0.5f, 0.15f), (h * 0.6f, 0.1f), (h - 0.12f, 0.14f));
            if (variant == 0)
            {
                // Dice, and cups of drink.
                for (int i = 0; i < 3; i++) GamblingDie(b, new Vector3(random.Range(-0.4f, 0.4f), h + 0.1f, random.Range(-0.4f, 0.4f)), 0.09f, random.Range(0f, 3f));
                Mug(b, new Vector3(r * 0.5f, h, -r * 0.3f), Hall.Pewter);
                Mug(b, new Vector3(-r * 0.55f, h, r * 0.2f), Hall.Brick(170, 120, 84));
                Coins(b, new Vector3(0f, h + 0.01f, r * 0.5f), 6, random);
            }
            else if (variant == 1)
            {
                // A game of cards half played.
                for (int i = 0; i < 9; i++) Card(b, new Vector3(random.Range(-0.6f, 0.6f), h + 0.006f + i * 0.004f, random.Range(-0.6f, 0.6f)), random.Range(0f, 3f), random.Chance(0.5f));
                Coins(b, new Vector3(0.3f, h + 0.01f, -0.4f), 5, random);
                Mug(b, new Vector3(-r * 0.6f, h, -r * 0.3f), Hall.Pewter);
            }
            if (variant <= 1 || variant == 2)
            {
                // A candle in a dish.
                Vector3 dish = new Vector3(variant == 2 ? 0f : -r * 0.2f, h, variant == 2 ? 0f : -r * 0.55f);
                b.Tube(dish, dish + Vector3.UnitY * 0.04f, 0.16f, 0.17f, 10, Hall.Pewter);
                Hall.Candle(b, dish + Vector3.UnitY * 0.04f, 0.3f, 0.06f, lights);
                lights.Add(AssetLight.Candles(dish + Vector3.UnitY * 0.8f) with { Intensity = 14f, Radius = 9f });
            }
            if (variant == 2)
            {
                b.Box(new Vector3(0.25f, h + 0.06f, 0.2f), new Vector3(0.18f, 0.06f, 0.24f), Hall.Leather(60, 36, 24));
                b.Tube(new Vector3(-0.25f, h, 0.15f), new Vector3(-0.25f, h + 0.25f, 0.15f), 0.07f, 0.06f, 6, Hall.Mirror(120, 70, 24));
            }
        }

        private static void Stage(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial boards = Hall.Wood(140, 100, 66);
            b.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(half.X, h * 0.5f, half.Y), boards);
            b.Box(new Vector3(0f, h + 0.01f, 0f), new Vector3(half.X - 0.4f, 0.01f, half.Y - 0.4f), Hall.Cloth(44, 58, 116));
            // A lute on its stand, a drum, a stool, and a music stand.
            Vector3 lute = new Vector3(-half.X * 0.4f, h, -0.2f);
            b.Beam(lute, lute + new Vector3(0f, 0.9f, -0.3f), 0.03f, Hall.DarkOak);
            Hall.At(b, lute + new Vector3(0f, 0.9f, -0.1f), 0f, () =>
            {
                b.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.25f), 0.42f, 0.36f, 12, Hall.Wood(176, 120, 64));
                b.Beam(new Vector3(0f, 0.3f, 0.26f), new Vector3(0f, 1.3f, 0.2f), 0.06f, Hall.DarkOak);
                b.Tube(new Vector3(0f, 0f, 0.26f), new Vector3(0f, 0f, 0.27f), 0.1f, 0.1f, 8, Hall.Flat(30, 22, 16));
            });
            Vector3 drum = new Vector3(half.X * 0.35f, h, -0.3f);
            b.Tube(drum, drum + Vector3.UnitY * 0.8f, 0.5f, 0.5f, 12, Hall.Wood(150, 96, 56));
            b.Tube(drum + Vector3.UnitY * 0.8f, drum + Vector3.UnitY * 0.82f, 0.5f, 0.5f, 12, Hall.Leather(214, 196, 160));
            Hall.At(b, new Vector3(-0.2f, h, 0.4f), 0f, () => Stool(b, 0.4f, 1.25f));
            Vector3 stand = new Vector3(0.9f, h, half.Y * 0.4f);
            b.Beam(stand, stand + Vector3.UnitY * 1.9f, 0.025f, Hall.Iron(76));
            b.Box(stand + new Vector3(0f, 2f, 0f), new Vector3(0.3f, 0.2f, 0.02f), Hall.Paper);
        }

        private static void Rug(MeshBuilder b, Vector2 half, int variant)
        {
            // Border, field, and a medallion, each a hair above the last.
            Vector3 border = variant == 1 ? Hall.Cloths[3] : Hall.Cloths[2];
            Vector3 field = variant == 1 ? new Vector3(30, 74, 80) : new Vector3(36, 56, 104);
            b.Box(new Vector3(0f, 0.065f, 0f), new Vector3(half.X, 0.015f, half.Y), Hall.ClothOf(border));
            b.Box(new Vector3(0f, 0.08f, 0f), new Vector3(half.X - 0.35f, 0.01f, half.Y - 0.35f), Hall.ClothOf(field));
            b.Box(new Vector3(0f, 0.09f, 0f), new Vector3(half.X - 0.5f, 0.005f, half.Y - 0.5f), Hall.ClothOf(field * 0.8f));
            b.Box(new Vector3(0f, 0.095f, 0f), new Vector3(half.X * 0.35f, 0.006f, half.Y * 0.35f), Hall.ClothOf(border * 0.9f));
            b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(half.X * 0.2f, 0.006f, half.Y * 0.2f), Hall.ClothOf(Hall.Cloths[4]));
            // Fringes at the ends.
            for (float x = -half.X + 0.1f; x < half.X; x += 0.2f)
            {
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x, 0.06f, s * (half.Y + 0.12f)), new Vector3(0.03f, 0.008f, 0.12f), Hall.ClothOf(Hall.Cloths[4]));
            }
        }

        private static void Planter(MeshBuilder b, float r, float h, WorldRandom random)
        {
            VertexMaterial tub = Hall.Wood(118, 84, 56);
            b.Tube(Vector3.Zero, new Vector3(0f, 0.9f, 0f), r * 0.75f, r * 0.9f, 10, tub, capped: false);
            b.Tube(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0.88f, 0f), r * 0.86f, r * 0.86f, 10, Hall.Flat(56, 42, 30));
            foreach (float y in new[] { 0.2f, 0.7f }) b.Tube(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.06f, 0f), r * 0.82f, r * 0.82f, 10, Hall.Iron(84), capped: false);
            // A bay tree, clipped round: a stem and a ball of leaves.
            b.Tube(new Vector3(0f, 0.85f, 0f), new Vector3(0f, h - 1.1f, 0f), 0.06f, 0.05f, 5, Hall.Wood(110, 84, 62));
            VertexMaterial leaf = new VertexMaterial(MaterialCatalog.FoliageWhite.Index, new Colour(52, 92, 40));
            VertexMaterial leaf2 = new VertexMaterial(MaterialCatalog.FoliageWhite.Index, new Colour(66, 110, 46));
            Vector3 ball = new Vector3(0f, h - 0.75f, 0f);
            for (int i = 0; i < 40; i++)
            {
                Vector3 d = new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f));
                if (d.LengthSquared > 1f || d.LengthSquared < 0.05f) continue;
                d = d.Normalized() * r * random.Range(0.5f, 0.85f);
                float s = random.Range(0.12f, 0.22f);
                b.Box(ball + d, new Vector3(s, s * 0.8f, s), random.Chance(0.5f) ? leaf : leaf2);
            }
        }

        // ---- The Archive.

        private static void Bookcase(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(80, 54, 36);
            Shelving(b, half, h, wood, 5, out float[] levels);
            // A carved cornice along its top and a plinth along its foot.
            b.Box(new Vector3(0f, h + 0.12f, 0.06f), new Vector3(half.X + 0.14f, 0.12f, half.Y + 0.08f), Hall.Wood(70, 46, 30));
            b.Box(new Vector3(0f, 0.08f, 0.04f), new Vector3(half.X + 0.06f, 0.08f, half.Y + 0.04f), Hall.Wood(70, 46, 30));
            for (int shelf = 0; shelf < levels.Length - 1; shelf++)
            {
                float y = levels[shelf];
                float room = levels[shelf + 1] - y - 0.12f;
                Vector3 start = new Vector3(-half.X + 0.15f, y, 0f);
                if (random.Chance(0.18f))
                {
                    // A shelf of rolled scrolls, or a stack lying flat.
                    for (float x = -half.X + 0.3f; x < half.X - 0.3f; x += 0.24f)
                    {
                        b.Tube(new Vector3(x, y + 0.11f, -half.Y * 0.7f), new Vector3(x, y + 0.11f, half.Y * 0.7f), 0.1f, 0.1f, 6, Hall.Paper);
                        if (x + 0.24f < half.X - 0.3f && random.Chance(0.5f)) b.Tube(new Vector3(x + 0.12f, y + 0.3f, -half.Y * 0.6f), new Vector3(x + 0.12f, y + 0.3f, half.Y * 0.6f), 0.1f, 0.1f, 6, Hall.Flat(196, 180, 146));
                    }
                    continue;
                }
                Hall.Books(b, start, Vector3.UnitX, Vector3.UnitZ, half.X * 2f - 0.3f, half.Y * 1.5f, MathF.Min(room, 0.95f), random);
            }
        }

        private static void ReadingDesk(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(96, 64, 42);
            Table(b, half, h, wood, Hall.Wood(76, 50, 34));
            // A sloped board with a great book open on it, quills in their ink, a candle, and books stacked about.
            Hall.At(b, new Vector3(0f, h, -0.1f), 0f, () =>
            {
                b.Beam(new Vector3(-0.9f, 0.05f, 0.4f), new Vector3(-0.9f, 0.5f, -0.4f), 0.04f, wood);
                b.Beam(new Vector3(0.9f, 0.05f, 0.4f), new Vector3(0.9f, 0.5f, -0.4f), 0.04f, wood);
            });
            Vector3 book = new Vector3(0f, h + 0.25f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                // Each page wound so that it faces up off the board.
                Vector3 inner0 = book + new Vector3(s * 0.02f, 0.18f, -0.42f), outer0 = book + new Vector3(s * 0.62f, 0.19f, -0.4f);
                Vector3 outer1 = book + new Vector3(s * 0.62f, -0.17f, 0.38f), inner1 = book + new Vector3(s * 0.02f, -0.18f, 0.4f);
                if (s > 0f) b.PlaneQuad(inner0, inner1, outer1, outer0, Hall.Paper, book, Vector3.UnitX, Vector3.UnitY, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
                else b.PlaneQuad(inner0, outer0, outer1, inner1, Hall.Paper, book, -Vector3.UnitX, Vector3.UnitY, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
            }
            b.Box(book - new Vector3(0f, 0.02f, 0f), new Vector3(0.66f, 0.02f, 0.44f), Hall.Leather(70, 30, 26));
            Vector3 ink = new Vector3(half.X - 0.4f, h, half.Y - 0.35f);
            b.Tube(ink, ink + Vector3.UnitY * 0.14f, 0.09f, 0.07f, 8, Hall.Flat(26, 26, 34));
            b.Beam(ink + Vector3.UnitY * 0.1f, ink + new Vector3(0.12f, 0.6f, -0.1f), 0.012f, Hall.Flat(220, 214, 200));
            b.Beam(ink + Vector3.UnitY * 0.1f, ink + new Vector3(-0.1f, 0.55f, -0.05f), 0.012f, Hall.Flat(200, 196, 186));
            for (int i = 0; i < 4; i++)
            {
                float thick = random.Range(0.07f, 0.12f);
                b.Box(new Vector3(-half.X + 0.45f, h + 0.06f + i * 0.13f, -0.1f), new Vector3(0.32f - i * 0.02f, thick * 0.5f + 0.01f, 0.24f), random.Pick(Hall.BookCovers));
            }
            Vector3 stick = new Vector3(half.X - 0.45f, h, -half.Y + 0.35f);
            Hall.Turned(b, stick, Hall.Pewter, 8, (0f, 0.16f), (0.05f, 0.14f), (0.08f, 0.05f), (0.4f, 0.04f), (0.45f, 0.1f));
            Hall.Candle(b, stick + Vector3.UnitY * 0.45f, 0.35f, 0.06f, lights);
            lights.Add(AssetLight.Candles(stick + Vector3.UnitY * 1.1f) with { Intensity = 20f, Radius = 11f });
        }

        private static void CandleStand(MeshBuilder b, float r, float h, List<AssetLight> lights)
        {
            VertexMaterial metal = Hall.Pewter;
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f;
                b.Beam(new Vector3(MathF.Cos(a) * r, 0.03f, MathF.Sin(a) * r), new Vector3(0f, 0.45f, 0f), 0.035f, metal);
            }
            Hall.Turned(b, new Vector3(0f, 0.4f, 0f), metal, 8, (0f, 0.08f), (0.2f, 0.12f), (0.35f, 0.05f), (h - 1.2f, 0.045f), (h - 1.1f, 0.1f), (h - 1.05f, 0.06f));
            float crown = h - 0.75f;
            b.Tube(new Vector3(0f, crown - 0.3f, 0f), new Vector3(0f, crown - 0.25f, 0f), 0.55f, 0.55f, 12, metal);
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = i == 0 ? new Vector3(0f, crown + 0.1f, 0f) : new Vector3(MathF.Cos(MathF.Tau * i / 4f) * 0.45f, crown - 0.22f, MathF.Sin(MathF.Tau * i / 4f) * 0.45f);
                if (i > 0) b.Beam(new Vector3(0f, crown - 0.3f, 0f), at - Vector3.UnitY * 0.02f, 0.025f, metal);
                b.Tube(at - Vector3.UnitY * 0.04f, at, 0.1f, 0.12f, 8, metal);
                Hall.Candle(b, at, i == 0 ? 0.5f : 0.4f, 0.06f, lights);
            }
            lights.Add(AssetLight.CandleStand(new Vector3(0f, crown + 0.6f, 0f)) with { Intensity = 24f, Radius = 13f });
        }

        private static void Bench(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial wood = Hall.Wood(110, 76, 50);
            b.Box(new Vector3(0f, h - 0.08f, 0f), new Vector3(half.X, 0.08f, half.Y), wood);
            foreach (float x in new[] { -half.X + 0.3f, half.X - 0.3f }) Hall.Leg(b, new Vector3(x, 0f, 0f), h - 0.16f, 0.12f, wood);
            b.Box(new Vector3(0f, 0.35f, 0f), new Vector3(half.X - 0.3f, 0.05f, 0.05f), wood);
        }

        private static void Globe(MeshBuilder b, float r, float h)
        {
            VertexMaterial wood = Hall.Wood(96, 64, 42);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.5f;
                b.Beam(new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r), new Vector3(MathF.Cos(a) * r * 0.5f, 1.3f, MathF.Sin(a) * r * 0.5f), 0.06f, wood);
            }
            b.Tube(new Vector3(0f, 1.3f, 0f), new Vector3(0f, 1.42f, 0f), r * 0.75f, r * 0.75f, 16, wood);
            // The globe: sea, and the lands on it raised a little, set in a meridian.
            Vector3 c = new Vector3(0f, h - r * 0.75f - 0.1f, 0f);
            float radius = r * 0.7f;
            Sphere(b, c, radius, 14, 9, Hall.Cloth(48, 84, 118));
            var rng = new WorldRandom(0x610B);
            VertexMaterial land = Hall.Cloth(160, 140, 96);
            for (int i = 0; i < 14; i++)
            {
                Vector3 d = new Vector3(rng.Range(-1f, 1f), rng.Range(-0.7f, 0.7f), rng.Range(-1f, 1f));
                if (d.LengthSquared < 0.1f) continue;
                d = d.Normalized();
                b.Box(c + d * radius, new Vector3(rng.Range(0.08f, 0.2f), rng.Range(0.06f, 0.14f), rng.Range(0.08f, 0.2f)), land);
            }
            for (int i = 0; i < 12; i++)
            {
                float a0 = -MathF.PI * 0.5f + MathF.PI * i / 12f, a1 = -MathF.PI * 0.5f + MathF.PI * (i + 1) / 12f;
                b.Beam(c + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f) * (radius + 0.06f), c + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f) * (radius + 0.06f), 0.03f, Hall.Copper);
            }
        }

        /// <summary>A sphere of <paramref name="material"/> as a grid of quads, for a globe, a glass or a gem.</summary>
        public static void Sphere(MeshBuilder b, Vector3 centre, float radius, int around, int up, VertexMaterial material)
        {
            b.Surface(around, up, (u, v) =>
            {
                float a = u * MathF.Tau, e = (v - 0.5f) * MathF.PI;
                return centre + new Vector3(MathF.Cos(a) * MathF.Cos(e), MathF.Sin(e), -MathF.Sin(a) * MathF.Cos(e)) * radius;
            }, material);
        }

        private static void Lectern(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial wood = Hall.Wood(92, 62, 40);
            b.Box(new Vector3(0f, 0.1f, 0f), new Vector3(half.X, 0.1f, half.Y), wood);
            Hall.Turned(b, new Vector3(0f, 0.2f, 0f), wood, 8, (0f, 0.2f), (0.3f, 0.12f), (h - 0.9f, 0.1f), (h - 0.6f, 0.16f));
            Hall.At(b, new Vector3(0f, h - 0.45f, 0f), 0f, () =>
            {
                b.Beam(new Vector3(0f, -0.1f, 0f), new Vector3(0f, 0.25f, -0.25f), 0.08f, wood);
                b.PlaneQuad(new Vector3(-0.7f, 0f, 0.4f), new Vector3(0.7f, 0f, 0.4f), new Vector3(0.7f, 0.42f, -0.35f), new Vector3(-0.7f, 0.42f, -0.35f), wood, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
                b.PlaneQuad(new Vector3(-0.66f, 0.04f, 0.36f), new Vector3(0.66f, 0.04f, 0.36f), new Vector3(0.66f, 0.45f, -0.33f), new Vector3(-0.66f, 0.45f, -0.33f), Hall.Paper, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
                b.Beam(new Vector3(0f, 0.05f, 0.36f), new Vector3(0f, 0.46f, -0.33f), 0.015f, Hall.Leather(60, 30, 24));
            });
        }
    }
}
