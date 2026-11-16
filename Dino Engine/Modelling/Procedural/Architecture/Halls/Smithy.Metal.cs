using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Halls.HallProps;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// What stands in the Forge, each piece built in its own frame to the numbers of its <see cref="HallPiece"/>, its middle on the floor at the origin and its front towards +z, as
    /// <see cref="GamingHouse"/>'s are. This is the hot side and the middle of the room: the bellows, the anvil, the master's bench, the smelting furnace, its stock, the casting
    /// bed, the quench trough, the grindstone, and by the door what's been mended and what never was fetched. The soft side is Smithy.Soft.cs. Every piece is in the middle of
    /// being used, and says by what's on it what was being done and by whom: metal goes from white to red to grey along a row of moulds, a blade from a blank to a hilt along a
    /// rack. What burns, steams and smokes is said as an effect where it happens (<see cref="AssetLight.Emitting"/>, Particles/Content/Smithing.cs), and whatever glows has a light.
    /// The camera looks down from the south: what's on top of a thing is what's seen of it.
    /// </summary>
    internal static partial class Smithy
    {
        private const float FloorTop = Hall.FloorTop;

        public static void Piece(MeshBuilder b, in HallPiece piece, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            Vector2 half = piece.Half;
            float r = half.X;
            float h = piece.Height;
            switch (piece.Kind)
            {
                case PieceKind.Hearth: break;       // The forge itself is part of the building, with its hood and its chimney (HallShells.ForgeHearth).
                case PieceKind.Bellows: Bellows(b, half, h); break;
                case PieceKind.Anvil: Anvil(b, r, h, random); break;
                case PieceKind.Workbench: MasterBench(b, half, h, lights, random); break;
                case PieceKind.Smelter: Smelter(b, r, h, lights, random); break;
                case PieceKind.StockBins: StockBins(b, half, h, random); break;
                case PieceKind.CastingBed: CastingBed(b, half, lights, random); break;
                case PieceKind.Trough: Trough(b, half, h, lights); break;
                case PieceKind.Grindstone: Grindstone(b, half, h); break;
                case PieceKind.Uncollected: Uncollected(b, half, h, lights, random); break;
                case PieceKind.ArmourStand: Harness(b, r, h, piece.Variant == 1); break;
                case PieceKind.Crates: Delivery(b, half, random); break;
                case PieceKind.Ballista: Ballista(b, half, lights, random); break;
                case PieceKind.Cauldron: Cauldron(b, r, lights); break;
                case PieceKind.Vat: Vat(b, r, h, piece.Variant, lights); break;
                case PieceKind.HideFrame: HideFrame(b, half, h); break;
                case PieceKind.FleshingBeam: FleshingBeam(b, half, h, random); break;
                case PieceKind.LeatherTable: LeatherTable(b, half, h, lights, random); break;
                case PieceKind.SaddleStand: SaddleStand(b, r, h); break;
                case PieceKind.Loom: Loom(b, half, h, random); break;
                case PieceKind.TailorTable: TailorTable(b, half, h, lights, random); break;
                case PieceKind.DressForm: DressForm(b, h); break;
                case PieceKind.ClothRack: ClothRack(b, half, random); break;
                case PieceKind.SpinningWheel: SpinningWheel(b, h); break;
                default: HallFurniture.Build(b, piece, kit, lights, random); break;
            }
        }

        // ---- What it's made of.

        private static VertexMaterial Steel => Hall.Metal(178, 182, 190);
        private static VertexMaterial Bright => Hall.Metal(206, 210, 216);
        private static VertexMaterial DarkSteel => Hall.Metal(112, 114, 120);
        private static VertexMaterial Firebrick => Hall.Brick(160, 128, 110);
        private static VertexMaterial Charcoal => Hall.Flat(30, 28, 28);
        private static VertexMaterial Sand => new VertexMaterial(MaterialCatalog.Sand.Index, new Colour(200, 184, 150));
        private static VertexMaterial Mail => new VertexMaterial(MaterialCatalog.Mail.Index, new Colour(150, 152, 158));
        private static VertexMaterial Handle => Hall.Timber(164, 124, 80);
        private static VertexMaterial Tan => Hall.Leather(152, 104, 62);
        private static VertexMaterial DarkHide => Hall.Leather(86, 54, 34);
        private static VertexMaterial Rawhide => Hall.Leather(188, 164, 124);

        /// <summary>Metal by how hot it is, 1 just poured and 0 only just too hot to hold: white-yellow, orange, a dull red. Its colour stays deep, as a glow's has to.</summary>
        private static VertexMaterial Hot(float heat)
        {
            heat = Math.Clamp(heat, 0f, 1f);
            return Hall.Glow(1f, 0.14f + 0.44f * heat, 0.02f + 0.16f * heat * heat, 0.55f + 0.75f * heat);
        }

        // ---- Small things a smith's hands are never far from.

        // A hammer: its head at one end of its handle, across it.
        private static void Hammer(MeshBuilder b, Vector3 head, Vector3 end, float size = 1f)
        {
            Vector3 along = (end - head).Normalized();
            Vector3 across = Vector3.Cross(along, MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
            b.Tube(head, end, 0.035f * size, 0.03f * size, 6, Handle);
            Hall.Board(b, head - across * 0.2f * size, head + across * 0.2f * size, along, 0.075f * size, 0.075f * size, Hall.Iron(72));
        }

        // Tongs: two reins from a rivet, opening to jaws at one end.
        private static void Tongs(MeshBuilder b, Vector3 jaws, Vector3 grips)
        {
            Vector3 along = (grips - jaws).Normalized();
            Vector3 across = Vector3.Cross(along, MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
            Vector3 rivet = Vector3.Lerp(jaws, grips, 0.22f);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Beam(jaws + across * s * 0.07f, rivet, 0.022f, Hall.Iron(70));
                b.Beam(rivet, grips - across * s * 0.09f, 0.02f, Hall.Iron(70));
            }
        }

        // A tag of paper on a thread, with a line of ink on it: whose a thing is.
        private static void Tag(MeshBuilder b, Vector3 from, Vector3 out_)
        {
            Vector3 across = Vector3.Cross(Vector3.UnitY, out_).Normalized();
            Vector3 at = from - Vector3.UnitY * 0.2f + out_ * 0.03f;
            b.Beam(from, at + Vector3.UnitY * 0.08f, 0.008f, Twine);
            Hall.Board(b, at + Vector3.UnitY * 0.08f, at - Vector3.UnitY * 0.1f, across, 0.07f, 0.006f, Hall.Paper);
            Hall.Board(b, at - across * 0.04f + out_ * 0.008f, at + across * 0.04f + out_ * 0.008f, Vector3.UnitY, 0.012f, 0.003f, Hall.Ink);
        }

        // Something round and soft that isn't a ball: as wide, tall and deep as its radii.
        private static void Blob(MeshBuilder b, Vector3 centre, Vector3 radii, VertexMaterial material, int around = 10, int up = 6)
        {
            b.Surface(around, up, (u, v) =>
            {
                float a = u * MathF.Tau, e = (v - 0.5f) * MathF.PI;
                return centre + new Vector3(MathF.Cos(a) * MathF.Cos(e) * radii.X, MathF.Sin(e) * radii.Y, -MathF.Sin(a) * MathF.Cos(e) * radii.Z);
            }, material);
        }

        // Something spilt or trodden flat on the floor: a blot with an edge of its own, a hair over what it lies on. `at` is where it lies and how high.
        private static void Blot(MeshBuilder b, Vector3 at, float radius, VertexMaterial material, int seed, float long_ = 1f, float turn = 0f)
        {
            var random = new WorldRandom(0xB107).Fork(seed);
            const int Points = 11;
            var rim = new Vector3[Points];
            float cos = MathF.Cos(turn), sin = MathF.Sin(turn);
            for (int i = 0; i < Points; i++)
            {
                float a = MathF.Tau * i / Points;
                float reach = radius * random.Range(0.62f, 1.08f);
                float x = MathF.Cos(a) * reach * long_, z = MathF.Sin(a) * reach;
                rim[i] = at + new Vector3(x * cos + z * sin, 0f, -x * sin + z * cos);
            }
            for (int i = 0; i < Points; i++) Hall.Tri(b, at, rim[i], rim[(i + 1) % Points], Vector3.UnitY, material);
        }

        // A helmet standing on something: a round skull with a brim or a nasal or a comb, by its kind.
        private static void Helmet(MeshBuilder b, Vector3 foot, int kind, VertexMaterial metal, float yaw = 0f)
        {
            Hall.At(b, foot, yaw, () =>
            {
                b.Tube(Vector3.Zero, new Vector3(0f, 0.3f, 0f), 0.3f, 0.3f, 10, metal, capped: false);
                b.Tube(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.52f, 0f), 0.3f, 0.12f, 10, metal);
                switch (kind % 3)
                {
                    case 0:     // A kettle hat: a brim all round.
                        b.Tube(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.08f, 0f), 0.46f, 0.3f, 12, metal, capped: false);
                        b.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.04f, 0f), 0.46f, 0.46f, 12, metal, capped: false);
                        break;
                    case 1:     // A nasal down the face, and a slot of dark above it.
                        b.Box(new Vector3(0f, 0.12f, 0.3f), new Vector3(0.04f, 0.16f, 0.02f), metal);
                        b.Box(new Vector3(0f, 0.22f, 0.292f), new Vector3(0.2f, 0.03f, 0.012f), Hall.Flat(20, 20, 22));
                        break;
                    default:    // A comb over the crown.
                        Hall.Board(b, new Vector3(0f, 0.3f, 0.26f), new Vector3(0f, 0.56f, 0.02f), Vector3.UnitX, 0.02f, 0.05f, metal);
                        Hall.Board(b, new Vector3(0f, 0.56f, 0.02f), new Vector3(0f, 0.34f, -0.28f), Vector3.UnitX, 0.02f, 0.05f, metal);
                        break;
                }
            });
        }

        // ---- The fire's side.

        /// <summary>
        /// The great bellows: two boards the shape of a pear with leather pleated between them, lying on a trestle with its iron nose into the side of the hearth (-x), and the
        /// pole that works it rocking on a post over it, a chain down to the top board and a rope down to a hand. Whoever pumps it has a stool, a broom, and their dinner half eaten.
        /// </summary>
        private static void Bellows(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial board = Hall.Timber(118, 84, 54);
            VertexMaterial hide = Hall.Leather(110, 70, 42);
            VertexMaterial fold = Hall.Leather(84, 52, 32);
            // The trestle it lies on.
            foreach (float x in new[] { -0.7f, 0.9f })
            {
                foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(x, 0f, s * 1.05f), new Vector3(x, 1.3f, s * 0.55f), Vector3.UnitX, 0.08f, 0.07f, FrameDark);
                Hall.Board(b, new Vector3(x, 1.3f, -0.8f), new Vector3(x, 1.3f, 0.8f), Vector3.UnitY, 0.09f, 0.08f, FrameDark);
            }
            Hall.Board(b, new Vector3(-0.7f, 0.5f, 0f), new Vector3(0.9f, 0.5f, 0f), Vector3.UnitY, 0.06f, 0.05f, FrameDark);

            // The boards: a nose at -x widening to the back. The lower lies level and the upper is lifted at the back, as it is when it's full.
            (float x, float wide)[] outline = { (-1.25f, 0.14f), (-0.7f, 0.66f), (0.3f, 0.98f), (1.05f, 0.9f), (1.35f, 0.5f) };
            float Lower(float x) => 1.4f;
            float Upper(float x) => 1.56f + (x + 1.25f) * 0.38f;
            for (int i = 0; i + 1 < outline.Length; i++)
            {
                (float x0, float w0) = outline[i];
                (float x1, float w1) = outline[i + 1];
                Hall.Face(b, new Vector3(x0, Upper(x0), -w0), new Vector3(x0, Upper(x0), w0), new Vector3(x1, Upper(x1), w1), new Vector3(x1, Upper(x1), -w1), Vector3.UnitY, board);
                Hall.Face(b, new Vector3(x0, Lower(x0) - 0.07f, -w0), new Vector3(x0, Lower(x0) - 0.07f, w0), new Vector3(x1, Lower(x1) - 0.07f, w1), new Vector3(x1, Lower(x1) - 0.07f, -w1), -Vector3.UnitY, board);
                foreach (float s in new[] { -1f, 1f })
                {
                    // The leather between, drawn in at its middle where it pleats.
                    float m0 = (Lower(x0) + Upper(x0)) * 0.5f, m1 = (Lower(x1) + Upper(x1)) * 0.5f;
                    Vector3 out_ = Vector3.UnitZ * s;
                    Hall.Face(b, new Vector3(x0, Lower(x0), s * w0), new Vector3(x1, Lower(x1), s * w1), new Vector3(x1, m1, s * (w1 - 0.1f)), new Vector3(x0, m0, s * MathF.Max(w0 - 0.1f, 0.05f)), out_, hide);
                    Hall.Face(b, new Vector3(x0, m0, s * MathF.Max(w0 - 0.1f, 0.05f)), new Vector3(x1, m1, s * (w1 - 0.1f)), new Vector3(x1, Upper(x1), s * w1), new Vector3(x0, Upper(x0), s * w0), out_, fold);
                }
            }
            float back = outline[^1].x, backWide = outline[^1].wide;
            Hall.Face(b, new Vector3(back, Lower(back), -backWide), new Vector3(back, Lower(back), backWide), new Vector3(back, Upper(back), backWide), new Vector3(back, Upper(back), -backWide), Vector3.UnitX, hide);
            // Battens across the top board, the valve in it, and the nose.
            foreach (float x in new[] { -0.4f, 0.5f }) Hall.Board(b, new Vector3(x, Upper(x) + 0.03f, -0.7f), new Vector3(x, Upper(x) + 0.03f, 0.7f), Vector3.UnitY, 0.06f, 0.03f, FrameDark);
            b.Box(new Vector3(0.05f, Upper(0.05f) + 0.03f, 0f), new Vector3(0.16f, 0.025f, 0.16f), hide);
            b.Tube(new Vector3(-1.2f, 1.44f, 0f), new Vector3(-2.05f, 1.3f, 0f), 0.15f, 0.07f, 8, Hall.Iron(70));

            // The pole, on its post, with the chain to the board and the rope to pull.
            Vector3 pivot = new Vector3(0.9f, 3.5f, -1.1f);
            Hall.Board(b, new Vector3(0.9f, 0f, -1.1f), pivot, Vector3.UnitX, 0.11f, 0.11f, FrameDark);
            Hall.Board(b, new Vector3(0.5f, 2.6f, -1.1f), new Vector3(0.9f, 3.2f, -1.1f), Vector3.UnitZ, 0.05f, 0.05f, FrameDark);
            Vector3 over = new Vector3(1f, 3.34f, -0.25f), handEnd = new Vector3(0.7f, 3.86f, 1.6f);
            b.Tube(new Vector3(1.05f, 3.2f, -1.5f), handEnd, 0.07f, 0.05f, 6, Handle);
            b.Beam(over, new Vector3(1.05f, Upper(1.05f) + 0.03f, 0f), 0.016f, Hall.Iron(74));
            b.Beam(handEnd, handEnd - Vector3.UnitY * 1.5f, 0.016f, Twine);
            b.Tube(handEnd - Vector3.UnitY * 1.5f - Vector3.UnitX * 0.16f, handEnd - Vector3.UnitY * 1.5f + Vector3.UnitX * 0.16f, 0.035f, 0.035f, 6, Handle);

            // The stool of whoever pumps it, their broom against the trestle, and a heel of bread and a mug on the floor by the stool.
            Vector3 stool = new Vector3(-0.2f, 0f, half.Y + 0.55f);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.4f;
                b.Beam(stool + new Vector3(MathF.Cos(a) * 0.3f, 0f, MathF.Sin(a) * 0.3f), stool + new Vector3(MathF.Cos(a) * 0.14f, 0.82f, MathF.Sin(a) * 0.14f), 0.04f, Oak);
            }
            b.Tube(stool + Vector3.UnitY * 0.8f, stool + Vector3.UnitY * 0.88f, 0.3f, 0.3f, 10, Oak);
            Mug(b, stool + new Vector3(0.55f, 0f, 0.1f), Hall.Pewter, 0.6f);
            b.Box(stool + new Vector3(0.52f, 0.05f, -0.28f), new Vector3(0.13f, 0.05f, 0.09f), Hall.Flat(156, 106, 58));
            Vector3 broom = new Vector3(1.2f, 0f, half.Y + 0.25f);
            b.Tube(broom + new Vector3(0f, 0.5f, 0f), new Vector3(1f, 2.7f, 0.75f), 0.03f, 0.03f, 5, Handle);
            b.Tube(broom, broom + new Vector3(0f, 0.55f, 0f), 0.2f, 0.06f, 7, Hall.Flat(150, 118, 62));
        }

        /// <summary>
        /// The anvil, chained down to a stump of oak: its horn, its face worn bright, its heel with the hardy hole. A bar that was being drawn out is lying across it, still
        /// red, with the hammer put down beside it and the tongs leaning where they were dropped: whoever was at it has only just stepped away. Scale lies round its foot.
        /// </summary>
        private static void Anvil(MeshBuilder b, float r, float h, WorldRandom random)
        {
            VertexMaterial bark = Hall.Bark(112, 94, 78);
            float stump = h - 0.78f;
            b.Tube(Vector3.Zero, new Vector3(0f, stump, 0f), 0.66f, 0.58f, 12, bark);
            b.Tube(new Vector3(0f, stump - 0.01f, 0f), new Vector3(0f, stump + 0.012f, 0f), 0.58f, 0.58f, 12, Hall.Timber(176, 138, 96));
            foreach (float y in new[] { 0.3f, stump - 0.25f }) b.Tube(new Vector3(0f, y - 0.05f, 0f), new Vector3(0f, y + 0.05f, 0f), 0.64f, 0.63f, 12, Hall.Iron(74), capped: false);

            VertexMaterial steel = Hall.Metal(96, 96, 102);
            b.Box(new Vector3(0f, stump + 0.12f, 0f), new Vector3(0.44f, 0.12f, 0.32f), steel);
            b.Box(new Vector3(0f, stump + 0.36f, 0f), new Vector3(0.27f, 0.14f, 0.2f), steel);
            b.Box(new Vector3(0f, stump + 0.63f, 0f), new Vector3(0.64f, 0.14f, 0.27f), steel);
            b.Box(new Vector3(-0.02f, stump + 0.775f, 0f), new Vector3(0.6f, 0.006f, 0.24f), Bright);
            b.Tube(new Vector3(0.62f, stump + 0.66f, 0f), new Vector3(1.22f, stump + 0.72f, 0f), 0.21f, 0.02f, 9, steel);
            b.Box(new Vector3(-0.74f, stump + 0.67f, 0f), new Vector3(0.12f, 0.09f, 0.22f), steel);
            b.Box(new Vector3(-0.72f, stump + 0.765f, 0.05f), new Vector3(0.05f, 0.004f, 0.05f), Hall.Flat(18, 18, 20));
            // Chains over its feet, stapled to the stump.
            foreach (float s in new[] { -1f, 1f })
            {
                b.Beam(new Vector3(s * 0.4f, stump + 0.26f, 0.34f), new Vector3(s * 0.5f, stump - 0.3f, 0.52f), 0.03f, Hall.Iron(66));
                b.Beam(new Vector3(s * 0.4f, stump + 0.26f, -0.34f), new Vector3(s * 0.5f, stump - 0.3f, -0.52f), 0.03f, Hall.Iron(66));
            }

            // The work: a bar half drawn out to a point, hottest where it was last struck.
            float face = stump + 0.782f;
            Hall.Board(b, new Vector3(-0.5f, face + 0.045f, 0.04f), new Vector3(0f, face + 0.045f, 0.02f), Vector3.UnitZ, 0.05f, 0.045f, Hall.Iron(60));
            Hall.Board(b, new Vector3(0f, face + 0.045f, 0.02f), new Vector3(0.36f, face + 0.04f, 0f), Vector3.UnitZ, 0.045f, 0.04f, Hot(0.25f));
            Hall.Board(b, new Vector3(0.36f, face + 0.04f, 0f), new Vector3(0.82f, face + 0.03f, -0.03f), Vector3.UnitZ, 0.034f, 0.03f, Hot(0.6f));
            Hammer(b, new Vector3(-0.3f, face + 0.08f, -0.14f), new Vector3(-0.2f, face + 0.08f, 0.62f));
            Tongs(b, new Vector3(-0.52f, face + 0.05f, 0.06f), new Vector3(-0.95f, 0.04f, 0.5f));
            // A hardy standing in its hole, and a bucket of water with the dipper in it.
            Hall.Board(b, new Vector3(-0.72f, face - 0.02f, 0.05f), new Vector3(-0.72f, face + 0.2f, 0.05f), Vector3.UnitX, 0.05f, 0.012f, DarkSteel);
            Vector3 pail = new Vector3(0.62f, 0f, 0.66f);
            b.Tube(pail, pail + Vector3.UnitY * 0.56f, 0.24f, 0.29f, 9, Hall.Wood(130, 96, 62), capped: false);
            b.Tube(pail + Vector3.UnitY * 0.46f, pail + Vector3.UnitY * 0.48f, 0.27f, 0.27f, 9, Hall.Mirror(46, 58, 60));
            b.Tube(pail + Vector3.UnitY * 0.16f, pail + Vector3.UnitY * 0.22f, 0.26f, 0.265f, 9, Hall.Iron(84), capped: false);
            b.Beam(pail + new Vector3(0.05f, 0.44f, 0f), pail + new Vector3(0.3f, 0.92f, 0.12f), 0.02f, Handle);

            // Scale, struck off the iron and trodden about: black flakes round the stump, thickest under the face.
            for (int i = 0; i < 46; i++)
            {
                float a = random.Range(0f, MathF.Tau), reach = 0.7f + MathF.Pow(random.Float(), 1.6f) * 1.5f;
                Vector3 at = new Vector3(MathF.Cos(a) * reach, 0.012f, MathF.Sin(a) * reach * 0.8f + 0.2f);
                b.Box(at, new Vector3(random.Range(0.025f, 0.07f), 0.006f, random.Range(0.025f, 0.06f)), Hall.Flat(40, 36, 34));
            }
        }

        /// <summary>
        /// The quench trough: a trough cut from one stone, black water in it, and a blade just put in at the end towards the anvil, with the steam still coming off it and the
        /// tongs across the rim. A bucket and a dipper stand by it, and what's slopped over has run down the flags.
        /// </summary>
        private static void Trough(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            VertexMaterial stone = Hall.Stone(152, 146, 136);
            b.Box(new Vector3(0f, 0.13f, 0f), new Vector3(half.X, 0.13f, half.Y), stone);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(0f, h * 0.5f, s * (half.Y - 0.13f)), new Vector3(half.X, h * 0.5f, 0.13f), stone);
                b.Box(new Vector3(s * (half.X - 0.13f), h * 0.5f, 0f), new Vector3(0.13f, h * 0.5f, half.Y - 0.26f), stone);
            }
            float water = h - 0.16f;
            b.Box(new Vector3(0f, water, 0f), new Vector3(half.X - 0.26f, 0.008f, half.Y - 0.26f), Hall.Mirror(40, 50, 52));

            // The blade, in to its shoulders, its tang red yet, and the tongs that put it there.
            Vector3 hot = new Vector3(half.X - 0.75f, water, 0.02f);
            Hall.Board(b, hot - new Vector3(0.5f, 0.3f, 0f), hot + new Vector3(0.1f, 0.06f, 0f), Vector3.UnitZ, 0.06f, 0.012f, DarkSteel);
            Hall.Board(b, hot + new Vector3(0.1f, 0.06f, 0f), hot + new Vector3(0.5f, 0.3f, 0f), Vector3.UnitZ, 0.03f, 0.014f, Hot(0.3f));
            Tongs(b, hot + new Vector3(0.36f, 0.24f, 0.02f), new Vector3(half.X + 0.5f, h + 0.06f, half.Y - 0.3f));
            lights.Add(AssetLight.Emitting(hot + Vector3.UnitY * 0.05f, "Smithing.Quench"));

            // What's been quenched already today, standing in the other end to cool right through.
            for (int i = 0; i < 3; i++)
            {
                Vector3 foot = new Vector3(-half.X + 0.5f + i * 0.2f, water - 0.4f, -0.1f + i * 0.08f);
                Hall.Board(b, foot, foot + new Vector3(-0.22f, 1.2f, -0.05f - i * 0.03f), Vector3.UnitZ, 0.05f, 0.011f, DarkSteel);
            }

            Vector3 pail = new Vector3(-half.X - 0.45f, 0f, half.Y - 0.2f);
            b.Tube(pail, pail + Vector3.UnitY * 0.7f, 0.3f, 0.36f, 9, Hall.Wood(124, 90, 60), capped: false);
            b.Tube(pail + Vector3.UnitY * 0.56f, pail + Vector3.UnitY * 0.58f, 0.33f, 0.33f, 9, Hall.Mirror(46, 58, 60));
            foreach (float y in new[] { 0.16f, 0.5f }) b.Tube(pail + Vector3.UnitY * (y - 0.03f), pail + Vector3.UnitY * (y + 0.03f), 0.335f + y * 0.05f, 0.34f + y * 0.05f, 9, Hall.Iron(84), capped: false);
        }

        /// <summary>
        /// The master's bench, which is the crafting bench: oak a hand thick on legs like gateposts, drawers under it towards whoever comes to it, a vice at its end. On it all
        /// three of the Forge's trades meet: a sword waiting for its grip beside the drawing it was made to, a bracer half riveted, a cloak with the needle still in it; the
        /// stones a crafter works with, in their box, alight; a tyrant's tooth that someone wants a hilt made of; the book of what's owed, and a mug gone cold.
        /// </summary>
        private static void MasterBench(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial dark = Walnut;
            float top = h;
            for (int i = 0; i < 3; i++)
            {
                float z = -half.Y + (i + 0.5f) * (half.Y * 2f / 3f);
                int shade = 150 + (i - 1) * 9;
                Hall.Board(b, new Vector3(-half.X, top - 0.1f, z), new Vector3(half.X, top - 0.1f, z), Vector3.UnitZ, half.Y / 3f - 0.006f, 0.1f, Hall.Timber(shade, shade - 44, shade - 86));
            }
            Hall.Board(b, new Vector3(-half.X, top - 0.1f, half.Y + 0.012f), new Vector3(half.X, top - 0.1f, half.Y + 0.012f), Vector3.UnitY, 0.102f, 0.012f, Hall.Timber(184, 142, 94));
            foreach (float x in new[] { -half.X + 0.3f, half.X - 0.3f })
            {
                foreach (float z in new[] { -half.Y + 0.24f, half.Y - 0.24f }) b.Box(new Vector3(x, (top - 0.2f) * 0.5f, z), new Vector3(0.18f, (top - 0.2f) * 0.5f, 0.18f), dark);
                Hall.Board(b, new Vector3(x, 0.4f, -half.Y + 0.24f), new Vector3(x, 0.4f, half.Y - 0.24f), Vector3.UnitY, 0.09f, 0.07f, dark);
            }
            Hall.Board(b, new Vector3(-half.X + 0.3f, 0.52f, 0f), new Vector3(half.X - 0.3f, 0.52f, 0f), Vector3.UnitZ, half.Y - 0.3f, 0.04f, Hall.Timber(120, 86, 56));
            // The drawers, three, each with an iron pull.
            b.Box(new Vector3(0f, top - 0.52f, half.Y - 0.14f), new Vector3(half.X - 0.48f, 0.32f, 0.05f), dark);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 1.58f;
                b.Box(new Vector3(x, top - 0.52f, half.Y - 0.08f), new Vector3(0.7f, 0.24f, 0.03f), Hall.Timber(138, 98, 62));
                b.Box(new Vector3(x, top - 0.5f, half.Y - 0.035f), new Vector3(0.15f, 0.03f, 0.03f), Iron);
            }
            // A leg vice at the left end, with a blank still in it.
            float vice = -half.X + 0.3f;
            b.Box(new Vector3(vice, top * 0.52f, half.Y + 0.1f), new Vector3(0.13f, top * 0.52f + 0.1f, 0.07f), Iron);
            b.Tube(new Vector3(vice, top - 0.3f, half.Y - 0.2f), new Vector3(vice, top - 0.3f, half.Y + 0.42f), 0.06f, 0.06f, 6, Hall.Iron(70));
            b.Beam(new Vector3(vice - 0.34f, top - 0.3f, half.Y + 0.38f), new Vector3(vice + 0.34f, top - 0.3f, half.Y + 0.38f), 0.03f, Hall.Iron(70));
            Hall.Board(b, new Vector3(vice, top - 0.06f, half.Y + 0.04f), new Vector3(vice, top + 0.7f, half.Y + 0.04f), Vector3.UnitX, 0.06f, 0.014f, DarkSteel);

            // Under it: ingots, a box of offcuts, a coil of wire.
            for (int i = 0; i < 5; i++) b.Box(new Vector3(-1.9f + (i % 3) * 0.36f + (i / 3) * 0.18f, 0.61f + (i / 3) * 0.1f, -0.2f), new Vector3(0.15f, 0.05f, 0.3f), DarkSteel);
            Crate(b, new Vector3(0.3f, 0.56f, 0f), new Vector3(0.42f, 0.2f, 0.34f), 0.1f, random);
            for (int ring = 0; ring < 3; ring++) b.Tube(new Vector3(1.7f, 0.57f + ring * 0.05f, 0.05f), new Vector3(1.7f, 0.61f + ring * 0.05f, 0.05f), 0.3f - ring * 0.02f, 0.3f - ring * 0.02f, 10, Hall.Copper, capped: false);

            // The stones, in their box at the end, each with its own light.
            Vector3 box = new Vector3(-half.X + 0.62f, top, -0.3f);
            b.Box(box + Vector3.UnitY * 0.09f, new Vector3(0.4f, 0.09f, 0.28f), Hall.Timber(100, 68, 44));
            b.Box(box + new Vector3(0f, 0.36f, -0.3f), new Vector3(0.4f, 0.2f, 0.02f), Hall.Timber(100, 68, 44));
            b.Box(box + Vector3.UnitY * 0.17f, new Vector3(0.36f, 0.012f, 0.24f), Dyed(Blue));
            Vector3[] glows = { new(0.3f, 0.82f, 1f), new(0.82f, 0.38f, 1f), new(1f, 0.6f, 0.18f), new(0.4f, 1f, 0.56f) };
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = glows[i % glows.Length];
                b.Box(box + new Vector3(-0.24f + (i % 3) * 0.24f, 0.24f, -0.11f + (i / 3) * 0.22f), new Vector3(0.075f, 0.065f, 0.075f), Hall.Gem(c.X, c.Y, c.Z, 1.4f));
            }
            lights.Add(AssetLight.Glow(box + Vector3.UnitY * 0.6f, new Vector3(0.6f, 0.75f, 1f), 10f, 6f));

            // The sword: ground and polished, its guard on, its tang bare, with the strip of leather for its grip and its pommel waiting.
            Hall.Board(b, new Vector3(-1.5f, top + 0.03f, -0.1f), new Vector3(0.3f, top + 0.03f, 0.04f), Vector3.UnitZ, 0.085f, 0.014f, Bright);
            Hall.Board(b, new Vector3(0.3f, top + 0.04f, -0.2f), new Vector3(0.34f, top + 0.04f, 0.28f), Vector3.UnitX, 0.04f, 0.04f, Steel);
            Hall.Board(b, new Vector3(0.32f, top + 0.03f, 0.04f), new Vector3(0.78f, top + 0.03f, 0.08f), Vector3.UnitZ, 0.03f, 0.012f, DarkSteel);
            b.Tube(new Vector3(0.95f, top, 0.34f), new Vector3(0.95f, top + 0.09f, 0.34f), 0.075f, 0.06f, 8, Steel);
            for (int turn = 0; turn < 3; turn++) b.Tube(new Vector3(0.62f, top + 0.012f + turn * 0.02f, 0.5f), new Vector3(0.62f, top + 0.03f + turn * 0.02f, 0.5f), 0.14f - turn * 0.03f, 0.14f - turn * 0.03f, 9, DarkHide, capped: false);

            // The drawing it was made to, weighted at its corners, and the dividers lying on it.
            Hall.At(b, new Vector3(-0.2f, top, -0.58f), 0.06f, () =>
            {
                b.Box(new Vector3(0f, 0.006f, 0f), new Vector3(0.78f, 0.006f, 0.34f), Hall.Paper);
                b.Box(new Vector3(-0.05f, 0.014f, 0f), new Vector3(0.6f, 0.002f, 0.02f), Hall.Ink);
                b.Box(new Vector3(0.42f, 0.014f, 0f), new Vector3(0.015f, 0.002f, 0.14f), Hall.Ink);
                b.Box(new Vector3(0.56f, 0.014f, 0f), new Vector3(0.1f, 0.002f, 0.012f), Hall.Ink);
                foreach (float x in new[] { -0.5f, -0.2f, 0.1f }) b.Box(new Vector3(x, 0.014f, 0.22f), new Vector3(0.1f, 0.002f, 0.01f), Hall.Ink);
                foreach ((float x, float z) in new[] { (-0.7f, -0.26f), (0.7f, 0.26f) }) b.Box(new Vector3(x, 0.04f, z), new Vector3(0.07f, 0.035f, 0.07f), Hall.Rough(126));
                b.Beam(new Vector3(-0.42f, 0.03f, -0.2f), new Vector3(-0.1f, 0.03f, -0.02f), 0.012f, Steel);
                b.Beam(new Vector3(-0.42f, 0.03f, -0.2f), new Vector3(-0.08f, 0.03f, -0.24f), 0.012f, Steel);
            });

            // Leather: a bracer shaped over a block, half its rivets in, the punch and the rest of them beside it.
            Vector3 bracer = new Vector3(1.5f, top, -0.45f);
            b.Tube(bracer + new Vector3(-0.3f, 0.12f, 0f), bracer + new Vector3(0.3f, 0.12f, 0f), 0.13f, 0.11f, 8, Oak);
            b.Tube(bracer + new Vector3(-0.24f, 0.12f, 0f), bracer + new Vector3(0.24f, 0.12f, 0f), 0.16f, 0.14f, 8, Tan, capped: false);
            for (int i = 0; i < 4; i++) b.Box(bracer + new Vector3(-0.18f + i * 0.12f, 0.285f, 0f), new Vector3(0.022f, 0.012f, 0.022f), i < 2 ? Bright : Hall.Flat(30, 26, 24));
            b.Beam(bracer + new Vector3(0.05f, 0.02f, 0.3f), bracer + new Vector3(0.32f, 0.02f, 0.38f), 0.022f, DarkSteel);
            b.Tube(bracer + new Vector3(-0.3f, 0f, 0.34f), bracer + new Vector3(-0.3f, 0.06f, 0.34f), 0.1f, 0.1f, 8, Hall.Pewter);

            // Cloth: a cloak folded with its hem turned up, the needle and its thread in it, and the spool.
            Vector3 cloak = new Vector3(2.15f, top, 0.3f);
            b.Box(cloak + Vector3.UnitY * 0.05f, new Vector3(0.44f, 0.05f, 0.32f), Dyed(Teal));
            b.Box(cloak + new Vector3(0.06f, 0.115f, 0.05f), new Vector3(0.36f, 0.02f, 0.26f), Dyed(Teal * 0.84f));
            b.Box(cloak + new Vector3(0f, 0.105f, 0.3f), new Vector3(0.44f, 0.012f, 0.03f), Dyed(Saffron));
            b.Beam(cloak + new Vector3(-0.2f, 0.14f, 0.1f), cloak + new Vector3(0.02f, 0.2f, 0.02f), 0.008f, Bright);
            b.Tube(cloak + new Vector3(-0.56f, 0f, -0.1f), cloak + new Vector3(-0.56f, 0.14f, -0.1f), 0.06f, 0.06f, 7, Dyed(Saffron));

            // The tooth: as long as a forearm, propped on a block, with the steel that's to be its ferrule tried on its root.
            Vector3 tooth = new Vector3(2.35f, top + 0.12f, -0.55f);
            b.Box(tooth - new Vector3(0.1f, 0.07f, 0f), new Vector3(0.12f, 0.05f, 0.1f), Oak);
            b.Tube(tooth + new Vector3(-0.34f, 0f, 0f), tooth + new Vector3(0f, 0.04f, 0.01f), 0.1f, 0.085f, 7, Hall.Bone(214, 198, 160), capped: false);
            b.Tube(tooth + new Vector3(0f, 0.04f, 0.01f), tooth + new Vector3(0.3f, 0.02f, 0.05f), 0.085f, 0.01f, 7, Hall.Bone(226, 214, 184));
            b.Tube(tooth + new Vector3(-0.36f, -0.005f, 0f), tooth + new Vector3(-0.26f, 0.006f, 0f), 0.11f, 0.105f, 8, Steel);

            // A stake anvil in its hole, a hammer and files to hand, and the lamp it's all done by.
            b.Box(new Vector3(-1.15f, top + 0.14f, -0.62f), new Vector3(0.04f, 0.14f, 0.04f), DarkSteel);
            b.Box(new Vector3(-1.15f, top + 0.3f, -0.62f), new Vector3(0.2f, 0.04f, 0.07f), Steel);
            Hammer(b, new Vector3(-0.7f, top + 0.06f, 0.5f), new Vector3(-1.3f, top + 0.04f, 0.38f), 0.8f);
            for (int i = 0; i < 3; i++) Hall.Board(b, new Vector3(-0.1f + i * 0.12f, top + 0.015f, 0.44f), new Vector3(0f + i * 0.13f, top + 0.015f, 0.82f), Vector3.UnitX, 0.03f, 0.012f, i == 1 ? DarkSteel : Steel);
            Vector3 lamp = new Vector3(half.X - 0.36f, top, -half.Y + 0.3f);
            b.Tube(lamp, lamp + Vector3.UnitY * 0.05f, 0.2f, 0.16f, 8, Iron);
            b.Beam(lamp + Vector3.UnitY * 0.05f, lamp + Vector3.UnitY * 1.5f, 0.024f, Iron);
            b.Beam(lamp + Vector3.UnitY * 1.5f, lamp + new Vector3(-0.46f, 1.62f, 0.12f), 0.022f, Iron);
            Lantern(b, lamp + new Vector3(-0.46f, 1f, 0.12f), lights, 32f, 12f);

            // The book of what's owed, open towards the door with a pen in it, and someone's mug.
            Hall.At(b, new Vector3(1.05f, top, 0.5f), -0.1f, () =>
            {
                b.Box(new Vector3(0f, 0.025f, 0f), new Vector3(0.44f, 0.025f, 0.3f), Hall.Leather(66, 38, 24));
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * 0.21f, 0.058f, 0f), new Vector3(0.2f, 0.008f, 0.27f), Hall.Paper);
                for (int line = 0; line < 5; line++) b.Box(new Vector3(-0.21f, 0.068f, -0.2f + line * 0.09f), new Vector3(0.15f, 0.002f, 0.012f), Hall.Ink);
                for (int line = 0; line < 2; line++) b.Box(new Vector3(0.21f, 0.068f, -0.2f + line * 0.09f), new Vector3(0.15f, 0.002f, 0.012f), Hall.Ink);
                b.Beam(new Vector3(0.1f, 0.075f, 0.05f), new Vector3(0.4f, 0.11f, -0.2f), 0.012f, Hall.Flat(222, 216, 202));
            });
            Mug(b, new Vector3(0.1f, top, 0.62f), Hall.Pewter, 1.1f);
        }

        /// <summary>
        /// The smelting furnace: a shaft of stone bound in iron, as tall as whoever feeds it, its throat open and burning, its arch at the foot where the bloom is drawn and the
        /// slag is run off along a bed of sand, still glowing where it's freshest. A pipe brings it its blast; baskets of ore and charcoal stand at its foot with the bar that
        /// rakes it, and steps stand against it for whoever charges it from the top.
        /// </summary>
        private static void Smelter(MeshBuilder b, float r, float h, List<AssetLight> lights, WorldRandom random)
        {
            b.Tube(Vector3.Zero, new Vector3(0f, 0.5f, 0f), r, r - 0.04f, 14, Dressed);
            b.Tube(new Vector3(0f, 0.5f, 0f), new Vector3(0f, h - 0.3f, 0f), r - 0.1f, 1f, 14, Rubble, capped: false);
            VertexMaterial sooted = Hall.Stone(104, 96, 88);
            b.Tube(new Vector3(0f, h - 0.3f, 0f), new Vector3(0f, h, 0f), 1.06f, 1.1f, 14, sooted, capped: false);
            // The lip of its throat, the dark inside it and the fire at the bottom of that.
            for (int i = 0; i < 14; i++)
            {
                float a0 = MathF.Tau * i / 14f, a1 = MathF.Tau * (i + 1) / 14f;
                Vector3 o0 = new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)), o1 = new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1));
                Hall.Face(b, o0 * 0.7f + Vector3.UnitY * h, o0 * 1.1f + Vector3.UnitY * h, o1 * 1.1f + Vector3.UnitY * h, o1 * 0.7f + Vector3.UnitY * h, Vector3.UnitY, sooted);
                Hall.Face(b, o0 * 0.7f + Vector3.UnitY * (h - 0.7f), o1 * 0.7f + Vector3.UnitY * (h - 0.7f), o1 * 0.7f + Vector3.UnitY * h, o0 * 0.7f + Vector3.UnitY * h, -(o0 + o1), Soot);
            }
            b.Tube(new Vector3(0f, h - 0.74f, 0f), new Vector3(0f, h - 0.7f, 0f), 0.7f, 0.7f, 12, Hot(0.6f));
            foreach (float y in new[] { 1.5f, 2.6f, 3.7f })
            {
                float radius = MathHelper.Lerp(r - 0.1f, 1f, (y - 0.5f) / (h - 0.8f)) + 0.02f;
                b.Tube(new Vector3(0f, y - 0.06f, 0f), new Vector3(0f, y + 0.06f, 0f), radius + 0.005f, radius - 0.005f, 14, Hall.Iron(66), capped: false);
            }

            // The arch at its foot, turned towards the middle of the room, and the slag run out of it.
            const float Turn = 0.55f;
            Hall.At(b, Vector3.Zero, Turn, lights, () =>
            {
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * 0.5f, 0.75f, r - 0.02f), new Vector3(0.15f, 0.55f, 0.2f), sooted);
                b.Box(new Vector3(0f, 1.42f, r - 0.02f), new Vector3(0.68f, 0.14f, 0.22f), sooted);
                b.Box(new Vector3(0f, 0.75f, r - 0.16f), new Vector3(0.36f, 0.55f, 0.04f), Hot(0.55f));
                b.Box(new Vector3(0f, 0.3f, r - 0.08f), new Vector3(0.36f, 0.1f, 0.1f), Charcoal);
                // The runner: a bed of sand with a gutter down it, the slag in it black at its end and red at the arch.
                b.Box(new Vector3(0f, 0.05f, r + 1.05f), new Vector3(0.5f, 0.05f, 1.15f), Sand, closeBottom: true);
                b.Box(new Vector3(0f, 0.105f, r + 0.45f), new Vector3(0.12f, 0.012f, 0.45f), Hot(0.45f));
                b.Box(new Vector3(0.02f, 0.105f, r + 1.2f), new Vector3(0.13f, 0.012f, 0.32f), Hot(0.12f));
                Blot(b, new Vector3(0.03f, 0.112f, r + 1.85f), 0.36f, Hall.Flat(44, 40, 40), 21, 1.2f);
                lights.Add(AssetLight.Glow(new Vector3(0f, 1.8f, r + 1.8f), new Vector3(1f, 0.42f, 0.12f), 14f, 7f) with { Flicker = 0.25f, FlickerSpeed = 7f });
                // The rabble that draws the bloom, leaning in the arch's corner.
                b.Beam(new Vector3(0.9f, 0f, r + 0.5f), new Vector3(0.66f, 2.9f, r - 0.2f), 0.025f, Hall.Iron(70));
                b.Box(new Vector3(0.9f, 0.05f, r + 0.52f), new Vector3(0.1f, 0.05f, 0.03f), Hall.Iron(70));
            });

            // Its blast: a pipe into its side from a bag bellows on a frame, and steps against it for charging.
            Hall.At(b, Vector3.Zero, 2.25f, () =>
            {
                b.Tube(new Vector3(0f, 1f, r - 0.2f), new Vector3(0f, 1.1f, r + 0.7f), 0.08f, 0.12f, 7, Hall.Iron(70));
                b.Tube(new Vector3(0f, 1.1f, r + 0.7f), new Vector3(0f, 1.24f, r + 1.5f), 0.14f, 0.4f, 8, Hall.Leather(104, 66, 40), capped: false);
                b.Tube(new Vector3(0f, 1.24f, r + 1.5f), new Vector3(0f, 1.3f, r + 1.62f), 0.4f, 0.36f, 8, Hall.Timber(118, 84, 54));
                foreach (float s in new[] { -1f, 1f }) b.Beam(new Vector3(s * 0.3f, 0f, r + 1.2f), new Vector3(s * 0.12f, 1.05f, r + 1.2f), 0.04f, FrameDark);
            });
            Hall.At(b, Vector3.Zero, -1.05f, () =>
            {
                for (int step = 0; step < 3; step++)
                {
                    b.Box(new Vector3(0f, 0.3f + step * 0.6f, r + 1.1f - step * 0.38f), new Vector3(0.5f, 0.05f, 0.2f), Weathered(random));
                    foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(s * 0.46f, (0.3f + step * 0.6f) * 0.5f, r + 1.1f - step * 0.38f), new Vector3(0.04f, (0.3f + step * 0.6f) * 0.5f, 0.18f), FrameDark);
                }
            });

            // Baskets at its foot: ore one side of the arch, charcoal the other.
            foreach ((float turn, bool ore) in new[] { (Turn + 0.95f, true), (Turn - 0.85f, false) })
            {
                Vector3 at = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn)) * (r + 0.55f);
                b.Tube(at, at + Vector3.UnitY * 0.6f, 0.36f, 0.44f, 10, Hall.Timber(150, 116, 72), capped: false);
                b.Tube(at + Vector3.UnitY * 0.46f, at + Vector3.UnitY * 0.5f, 0.4f, 0.4f, 10, ore ? Hall.Rough(96) : Charcoal);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 lump = at + new Vector3(random.Range(-0.26f, 0.26f), 0.52f + random.Range(0f, 0.12f), random.Range(-0.26f, 0.26f));
                    float size = random.Range(0.07f, 0.13f);
                    b.Box(lump, new Vector3(size, size * 0.8f, size), ore ? new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(150, 96, 70)) : Charcoal);
                }
            }

            lights.Add(AssetLight.Emitting(new Vector3(0f, h - 0.5f, 0f), "Smithing.Furnace", 1f, new Vector3(MathF.Sin(Turn), 0f, MathF.Cos(Turn))));
            lights.Add(new AssetLight
            {
                // Out towards the room from the throat, not over it: its smoke goes straight up, and a light in the smoke makes a lamp of every puff.
                Position = new Vector3(MathF.Sin(Turn) * 1.5f, h + 1.5f, MathF.Cos(Turn) * 1.5f), Colour = new Vector3(1f, 0.46f, 0.14f), Intensity = 80f, Radius = 17f, Fill = 0.25f,
                Flicker = 0.34f, FlickerSpeed = 8f, Shadow = true, LightsFog = true,
            });
        }

        /// <summary>
        /// What the fires eat, in three bins against the wall: charcoal heaped up it with the shovel left in, ore as it came off the cart, and iron in the bar, with the
        /// horseshoes too worn to nail on again thrown in beside it for the next melt.
        /// </summary>
        private static void StockBins(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            float bay = half.X * 2f / 3f;
            for (int i = 0; i <= 3; i++)
            {
                float x = -half.X + bay * i;
                Hall.Board(b, new Vector3(x, 0f, 0f), new Vector3(x, h, 0f), Vector3.UnitZ, half.Y, 0.05f, Weathered(random));
            }
            for (int plank = 0; plank < 3; plank++)
            {
                float y = 0.18f + plank * 0.32f;
                Hall.Board(b, new Vector3(-half.X, y, half.Y - 0.04f), new Vector3(half.X, y, half.Y - 0.04f), Vector3.UnitY, 0.15f, 0.035f, Weathered(random));
            }
            Hall.Board(b, new Vector3(-half.X, h * 0.5f, -half.Y + 0.04f), new Vector3(half.X, h * 0.5f, -half.Y + 0.04f), Vector3.UnitY, h * 0.5f, 0.035f, FrameDark);

            VertexMaterial ore = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(150, 98, 72));
            for (int binIndex = 0; binIndex < 2; binIndex++)
            {
                float middle = -half.X + bay * (binIndex + 0.5f);
                for (int i = 0; i < 44; i++)
                {
                    float x = random.Range(-bay * 0.42f, bay * 0.42f), z = random.Range(-half.Y + 0.15f, half.Y - 0.14f);
                    // Heaped against the back, and running down to the board at the front.
                    float heap = 0.55f + (half.Y - z) / (half.Y * 2f) * (h - 0.5f) * (binIndex == 0 ? 1f : 0.72f);
                    float size = binIndex == 0 ? random.Range(0.09f, 0.17f) : random.Range(0.11f, 0.2f);
                    b.Box(new Vector3(middle + x, heap * random.Range(0.75f, 1f), z), new Vector3(size, size * 0.75f, size), binIndex == 0 ? Charcoal : random.Chance(0.3f) ? Hall.Rough(104) : ore);
                }
                b.Box(new Vector3(middle, 0.28f, 0f), new Vector3(bay * 0.46f, 0.28f, half.Y - 0.1f), binIndex == 0 ? Charcoal : Hall.Rough(86));
            }
            float charcoal = -half.X + bay * 0.5f;
            b.Tube(new Vector3(charcoal + 0.2f, 0.9f, 0.2f), new Vector3(charcoal + 0.75f, 2.5f, 0.75f), 0.035f, 0.035f, 6, Handle);
            Hall.Board(b, new Vector3(charcoal + 0.06f, 0.62f, 0.1f), new Vector3(charcoal + 0.24f, 1f, 0.24f), Vector3.UnitX, 0.17f, 0.012f, Hall.Iron(72));

            // The bar iron, on end, and the old shoes.
            float iron = -half.X + bay * 2.5f;
            for (int i = 0; i < 9; i++)
            {
                Vector3 foot = new Vector3(iron - 0.4f + (i % 5) * 0.2f, 0f, -half.Y + 0.2f + (i / 5) * 0.2f);
                Hall.Board(b, foot, foot + new Vector3(random.Range(-0.06f, 0.06f), random.Range(1.7f, 2.5f), -0.08f), Vector3.UnitX, 0.045f, 0.03f, i % 3 == 0 ? Hall.Iron(96) : DarkSteel);
            }
            for (int shoe = 0; shoe < 5; shoe++)
            {
                Vector3 middle = new Vector3(iron + random.Range(-0.3f, 0.36f), 0.1f + shoe * 0.05f, 0.25f + random.Range(-0.15f, 0.2f));
                float turn = random.Range(0f, MathF.Tau);
                for (int k = 0; k < 6; k++)
                {
                    float a0 = turn + MathF.PI * (0.12f + k * 0.126f) * 2f * 0.78f, a1 = turn + MathF.PI * (0.12f + (k + 1) * 0.126f) * 2f * 0.78f;
                    b.Beam(middle + new Vector3(MathF.Cos(a0) * 0.16f, 0f, MathF.Sin(a0) * 0.18f), middle + new Vector3(MathF.Cos(a1) * 0.16f, 0f, MathF.Sin(a1) * 0.18f), 0.024f, Hall.Iron(92));
                }
            }
            // Charcoal fallen out of the bin and trodden towards the fire.
            for (int i = 0; i < 14; i++)
            {
                float size = random.Range(0.05f, 0.1f);
                b.Box(new Vector3(charcoal + random.Range(-0.5f, 1.6f), size * 0.5f, half.Y + random.Range(0.1f, 0.9f)), new Vector3(size, size * 0.6f, size), Charcoal);
            }
            Blot(b, new Vector3(charcoal + 0.5f, 0.011f, half.Y + 0.6f), 0.9f, Hall.Flat(52, 48, 46), 31, 1.5f);
        }

        /// <summary>
        /// The casting bed, along the wall under its window: a frame of sand with the moulds sunk in it. A row of ingots was poured from the far end to the near one, and the
        /// metal says how long ago each was: the last is yellow yet, the first is only iron. A blade is cooling in its own long mould beside them, the crucible stands empty on
        /// the sand with its tongs, and what came out of the moulds yesterday is stacked at the bed's foot.
        /// </summary>
        private static void CastingBed(MeshBuilder b, Vector2 half, List<AssetLight> lights, WorldRandom random)
        {
            const float Bed = 0.46f;
            foreach (float s in new[] { -1f, 1f })
            {
                Hall.Board(b, new Vector3(s * (half.X - 0.05f), Bed * 0.5f, -half.Y), new Vector3(s * (half.X - 0.05f), Bed * 0.5f, half.Y), Vector3.UnitY, Bed * 0.5f, 0.05f, Weathered(random));
                Hall.Board(b, new Vector3(-half.X, Bed * 0.5f, s * (half.Y - 0.05f)), new Vector3(half.X, Bed * 0.5f, s * (half.Y - 0.05f)), Vector3.UnitY, Bed * 0.5f, 0.05f, Weathered(random));
            }
            b.Box(new Vector3(0f, Bed - 0.06f, 0f), new Vector3(half.X - 0.1f, 0.02f, half.Y - 0.1f), Sand);

            // Four ingots in iron moulds, poured in turn: the hottest is at -z, the end the crucible was carried from.
            float[] heats = { 0.95f, 0.6f, 0.28f, -1f };
            for (int i = 0; i < heats.Length; i++)
            {
                Vector3 at = new Vector3(0.45f, Bed - 0.04f, -1.25f + i * 0.72f);
                b.Box(at + Vector3.UnitY * 0.05f, new Vector3(0.5f, 0.05f, 0.24f), Hall.Iron(60));
                b.Box(at + Vector3.UnitY * 0.105f, new Vector3(0.42f, 0.012f, 0.16f), heats[i] < 0f ? DarkSteel : Hot(heats[i]));
            }
            // The blade, in a mould of two stones, one lifted off and laid beside.
            Vector3 mould = new Vector3(-0.55f, Bed - 0.04f, -0.2f);
            b.Box(mould + Vector3.UnitY * 0.06f, new Vector3(0.24f, 0.06f, 1.2f), Hall.Rough(132));
            b.Box(mould + new Vector3(0f, 0.125f, -0.1f), new Vector3(0.05f, 0.01f, 0.95f), Hot(0.2f));
            b.Box(mould + new Vector3(0f, 0.125f, 0.95f), new Vector3(0.12f, 0.01f, 0.03f), Hot(0.2f));
            Hall.At(b, mould + new Vector3(-0.4f, 0.03f, 0.4f), 0.12f, () => b.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.24f, 0.06f, 1.1f), Hall.Rough(124)));

            // The crucible, and the tongs that carry it, laid across the frame.
            Vector3 pot = new Vector3(0.5f, Bed - 0.04f, -half.Y + 0.42f);
            b.Tube(pot, pot + Vector3.UnitY * 0.5f, 0.2f, 0.3f, 9, Firebrick, capped: false);
            b.Tube(pot + Vector3.UnitY * 0.06f, pot + Vector3.UnitY * 0.1f, 0.2f, 0.2f, 9, Hot(0.7f));
            b.Tube(pot + Vector3.UnitY * 0.44f, pot + Vector3.UnitY * 0.5f, 0.31f, 0.3f, 9, Firebrick, capped: false);
            Tongs(b, pot + new Vector3(-0.3f, 0.3f, 0.1f), new Vector3(-half.X + 0.2f, Bed + 0.1f, -half.Y + 0.9f));
            // Yesterday's, stacked.
            for (int i = 0; i < 6; i++)
            {
                int layer = i < 3 ? 0 : i < 5 ? 1 : 2;
                float along = layer == 0 ? i - 1 : layer == 1 ? i - 3.5f : 0f;
                b.Box(new Vector3(-0.3f + along * 0.34f, Bed + 0.05f + layer * 0.11f, half.Y - 0.42f), new Vector3(0.15f, 0.05f, 0.3f), layer == 2 ? Steel : DarkSteel);
            }
            // A rammer left standing in the sand.
            b.Tube(new Vector3(-0.2f, Bed - 0.04f, 1f), new Vector3(-0.12f, Bed + 1.1f, 1.05f), 0.035f, 0.035f, 6, Handle);
            b.Tube(new Vector3(-0.2f, Bed - 0.04f, 1f), new Vector3(-0.19f, Bed + 0.16f, 1.01f), 0.1f, 0.09f, 7, Oak);

            lights.Add(AssetLight.Emitting(new Vector3(0.3f, Bed + 0.1f, -0.7f), "Smithing.Mould"));
            lights.Add(AssetLight.Glow(new Vector3(0.9f, Bed + 1.7f, -0.6f), new Vector3(1f, 0.48f, 0.14f), 16f, 7f) with { Flicker = 0.1f, FlickerSpeed = 3f });
        }

        /// <summary>
        /// The grindstone, its face to the room: a wheel of stone in a trough of water on a frame, with a treadle, a can that drips on it, and a seat. Against the wall behind
        /// it a rack holds one blade at each stage of its making, left to right as the work goes: the forged blank black from the fire, ground grey, polished, and hilted.
        /// It stands along x, with its front, the side that's sat at, towards +z.
        /// </summary>
        private static void Grindstone(MeshBuilder b, Vector2 half, float h)
        {
            VertexMaterial frame = FrameDark;
            float axle = h - 1.12f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * 0.42f;
                Hall.Board(b, new Vector3(x, 0f, -0.55f), new Vector3(x, axle + 0.1f, -0.06f), Vector3.UnitX, 0.07f, 0.06f, frame);
                Hall.Board(b, new Vector3(x, 0f, 0.55f), new Vector3(x, axle + 0.1f, 0.06f), Vector3.UnitX, 0.07f, 0.06f, frame);
                Hall.Board(b, new Vector3(x, 0.35f, -0.42f), new Vector3(x, 0.35f, 0.42f), Vector3.UnitY, 0.06f, 0.05f, frame);
            }
            b.Tube(new Vector3(-0.2f, axle, 0f), new Vector3(0.2f, axle, 0f), 1f, 1f, 20, Hall.Rough(172));
            b.Tube(new Vector3(0.2f, axle, 0f), new Vector3(-0.2f, axle, 0f), 1f, 1f, 20, Hall.Rough(172));
            b.Tube(new Vector3(-0.62f, axle, 0f), new Vector3(0.78f, axle, 0f), 0.06f, 0.06f, 6, Hall.Iron(72));
            b.Beam(new Vector3(0.78f, axle, 0f), new Vector3(0.78f, axle - 0.42f, 0.2f), 0.035f, Hall.Iron(72));
            b.Beam(new Vector3(0.78f, axle - 0.42f, 0.2f), new Vector3(0.74f, 0.2f, 0.74f), 0.03f, frame);
            Hall.Board(b, new Vector3(0.74f, 0.1f, 0.2f), new Vector3(0.74f, 0.2f, 1.05f), Vector3.UnitX, 0.13f, 0.03f, frame);
            // The trough it turns in, wet.
            b.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.2f, 0.7f), Hall.Wood(104, 76, 52));
            b.Box(new Vector3(0f, 0.545f, 0f), new Vector3(0.3f, 0.008f, 0.62f), Hall.Mirror(52, 60, 60));
            // The can over it, on an arm, and the seat.
            b.Beam(new Vector3(-0.42f, axle + 0.1f, -0.06f), new Vector3(-0.42f, h + 0.5f, -0.06f), 0.03f, Hall.Iron(72));
            b.Beam(new Vector3(-0.42f, h + 0.5f, -0.06f), new Vector3(0f, h + 0.56f, 0f), 0.025f, Hall.Iron(72));
            b.Tube(new Vector3(0f, h + 0.2f, 0f), new Vector3(0f, h + 0.52f, 0f), 0.1f, 0.15f, 8, Hall.Copper);
            b.Beam(new Vector3(0f, h + 0.2f, 0f), new Vector3(0f, h - 0.1f, 0f), 0.008f, Hall.Mirror(120, 130, 132));
            Hall.Board(b, new Vector3(-0.45f, 1.1f, half.Y + 0.25f), new Vector3(0.45f, 1.1f, half.Y + 0.25f), Vector3.UnitZ, 0.2f, 0.04f, Oak);
            foreach (float s in new[] { -1f, 1f }) b.Beam(new Vector3(s * 0.36f, 0f, half.Y + 0.36f), new Vector3(s * 0.3f, 1.08f, half.Y + 0.25f), 0.045f, frame);
            // A blade laid on the rest, where it was being held to the stone.
            Hall.Board(b, new Vector3(-0.1f, axle + 0.62f, 0.72f), new Vector3(1.25f, axle + 0.74f, 0.82f), Vector3.UnitZ, 0.06f, 0.012f, Steel);

            // The rack: a blade at each stage.
            float wall = -half.Y + 0.14f;
            b.Box(new Vector3(-0.15f, 0.2f, wall), new Vector3(1.3f, 0.2f, 0.14f), frame);
            Hall.Board(b, new Vector3(-1.45f, 2.3f, wall - 0.06f), new Vector3(1.15f, 2.3f, wall - 0.06f), Vector3.UnitY, 0.07f, 0.04f, frame);
            foreach (float x in new[] { -1.4f, 1.1f }) Hall.Board(b, new Vector3(x, 0f, wall - 0.06f), new Vector3(x, 2.4f, wall - 0.06f), Vector3.UnitX, 0.06f, 0.05f, frame);
            for (int stage = 0; stage < 4; stage++)
            {
                float x = 0.75f - stage * 0.62f;
                VertexMaterial metal = stage == 0 ? Hall.Flat(44, 40, 40) : stage == 1 ? DarkSteel : Bright;
                Vector3 point = new Vector3(x, 0.4f, wall), shoulder = new Vector3(x, 2.5f, wall - 0.05f);
                Hall.Board(b, point, shoulder, Vector3.UnitX, stage == 0 ? 0.085f : 0.07f, stage == 0 ? 0.028f : 0.014f, metal);
                Hall.Board(b, shoulder, shoulder + new Vector3(0f, 0.46f, -0.01f), Vector3.UnitX, 0.028f, 0.014f, stage == 0 ? Hall.Flat(44, 40, 40) : DarkSteel);
                if (stage < 3) continue;
                Hall.Board(b, shoulder - Vector3.UnitX * 0.22f, shoulder + Vector3.UnitX * 0.22f, Vector3.UnitY, 0.035f, 0.035f, Steel);
                b.Tube(shoulder + Vector3.UnitY * 0.03f, shoulder + new Vector3(0f, 0.44f, -0.01f), 0.04f, 0.04f, 6, DarkHide);
                b.Tube(shoulder + new Vector3(0f, 0.44f, -0.01f), shoulder + new Vector3(0f, 0.55f, -0.01f), 0.065f, 0.04f, 6, Steel);
            }
        }

        /// <summary>
        /// The shelf by the door of what was mended and never fetched. Everything on it has its owner's tag: helmets along the top, a shield, gauntlets, a sword in its
        /// scabbard, boots. Whoever keeps the Forge keeps candles burning in the middle of it, with a sprig of blossom in a jar, and chalks a stroke on the board over it for
        /// each. It stands against a wall with its front towards +z.
        /// </summary>
        private static void Uncollected(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Timber(110, 78, 52);
            float[] shelves = { 0.9f, 2.05f, 3.2f };
            float top = h - 0.5f;
            int boards = 9;
            for (int i = 0; i < boards; i++)
            {
                float x = -half.X + half.X * 2f * (i + 0.5f) / boards;
                Hall.Board(b, new Vector3(x, 0f, -half.Y + 0.04f), new Vector3(x, top, -half.Y + 0.04f), Vector3.UnitX, half.X / boards - 0.008f, 0.04f, Weathered(random));
            }
            foreach (float x in new[] { -half.X + 0.07f, half.X - 0.07f }) b.Box(new Vector3(x, top * 0.5f, 0f), new Vector3(0.07f, top * 0.5f, half.Y), wood);
            foreach (float y in shelves) Hall.Board(b, new Vector3(-half.X, y, 0.02f), new Vector3(half.X, y, 0.02f), Vector3.UnitZ, half.Y, 0.05f, wood);
            Hall.Board(b, new Vector3(-half.X - 0.08f, top, 0.04f), new Vector3(half.X + 0.08f, top, 0.04f), Vector3.UnitZ, half.Y + 0.08f, 0.07f, Walnut);

            // Along the top: helmets, each its own make, each tagged.
            for (int i = 0; i < 5; i++)
            {
                Vector3 foot = new Vector3(-half.X + 0.5f + i * 0.78f, shelves[2] + 0.05f, 0.02f);
                Helmet(b, foot, i, i == 2 ? DarkSteel : i == 4 ? Hall.Metal(150, 150, 156) : Steel, random.Range(-0.3f, 0.3f));
                Tag(b, foot + new Vector3(0.1f, 0.02f, half.Y - 0.06f), Vector3.UnitZ);
            }

            // The middle shelf: the candles, with a jar of blossom, between a pair of gauntlets and a cloak folded with its clasp on top.
            float middle = shelves[1] + 0.05f;
            b.Tube(new Vector3(0f, middle, 0.02f), new Vector3(0f, middle + 0.03f, 0.02f), 0.55f, 0.5f, 12, Hall.Wax);
            float[] tall = { 0.34f, 0.5f, 0.26f, 0.42f, 0.2f };
            for (int i = 0; i < tall.Length; i++) Hall.Candle(b, new Vector3(-0.36f + i * 0.18f, middle + 0.03f, 0.04f + (i % 2) * 0.12f - 0.06f), tall[i], 0.055f, lights);
            lights.Add(AssetLight.Candles(new Vector3(0f, middle + 0.7f, 0.9f)) with { Intensity = 18f, Radius = 9f, Shadow = false });
            Vector3 jar = new Vector3(0.62f, middle, 0.08f);
            b.Tube(jar, jar + Vector3.UnitY * 0.3f, 0.1f, 0.08f, 8, Stoneware);
            for (int i = 0; i < 6; i++)
            {
                Vector3 tip = jar + new Vector3(random.Range(-0.22f, 0.22f), random.Range(0.5f, 0.82f), random.Range(-0.1f, 0.16f));
                b.Beam(jar + Vector3.UnitY * 0.28f, tip, 0.01f, Hall.Timber(96, 70, 50));
                b.Box(tip, new Vector3(0.05f, 0.04f, 0.05f), Dyed(i % 2 == 0 ? Blossom : new Vector3(226, 170, 180)));
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 hand = new Vector3(-1.4f + s * 0.2f, middle, 0.02f);
                b.Tube(hand, hand + new Vector3(0f, 0.34f, 0f), 0.12f, 0.15f, 7, Steel);
                b.Box(hand + new Vector3(0f, 0.42f, 0.04f), new Vector3(0.13f, 0.09f, 0.1f), Steel);
            }
            Tag(b, new Vector3(-1.4f, middle + 0.02f, half.Y - 0.04f), Vector3.UnitZ);
            Vector3 cloak = new Vector3(1.45f, middle, 0f);
            b.Box(cloak + Vector3.UnitY * 0.09f, new Vector3(0.42f, 0.09f, 0.3f), Dyed(Green));
            b.Box(cloak + Vector3.UnitY * 0.2f, new Vector3(0.38f, 0.03f, 0.26f), Dyed(Green * 0.85f));
            b.Tube(cloak + new Vector3(0f, 0.23f, 0.05f), cloak + new Vector3(0f, 0.26f, 0.05f), 0.08f, 0.08f, 8, Hall.Pewter);
            Tag(b, cloak + new Vector3(0.2f, 0.1f, half.Y - 0.04f), Vector3.UnitZ);

            // The lowest: a sword in its scabbard, laid along it, and two pairs of boots.
            float low = shelves[0] + 0.05f;
            Hall.Board(b, new Vector3(-1.75f, low + 0.06f, 0.05f), new Vector3(-0.1f, low + 0.06f, 0f), Vector3.UnitZ, 0.08f, 0.04f, DarkHide);
            Hall.Board(b, new Vector3(-0.1f, low + 0.06f, -0.2f), new Vector3(-0.06f, low + 0.06f, 0.22f), Vector3.UnitX, 0.03f, 0.03f, Steel);
            b.Tube(new Vector3(-0.08f, low + 0.06f, 0f), new Vector3(0.34f, low + 0.06f, -0.02f), 0.04f, 0.04f, 6, Tan);
            Tag(b, new Vector3(-0.9f, low + 0.1f, half.Y - 0.1f), Vector3.UnitZ);
            foreach (float x in new[] { 0.75f, 1.05f, 1.5f, 1.78f }) Boot(b, new Vector3(x, low, 0f), 0f, x > 1.3f ? DarkHide : Tan);
            Tag(b, new Vector3(0.9f, low + 0.5f, half.Y - 0.1f), Vector3.UnitZ);
            Tag(b, new Vector3(1.64f, low + 0.5f, half.Y - 0.1f), Vector3.UnitZ);

            // A shield nobody came for, leaning on the foot of it, and under the shelf a chest of the rest.
            Vector3 boss = new Vector3(-0.9f, 0.48f, half.Y + 0.14f);
            Vector3 face = new Vector3(0f, 0.34f, 1f).Normalized();
            b.Tube(boss - face * 0.04f, boss + face * 0.04f, 0.46f, 0.46f, 14, PaintedOf(Blue));
            b.Tube(boss + face * 0.04f, boss + face * 0.05f, 0.3f, 0.3f, 12, PaintedOf(Cream));
            b.Tube(boss + face * 0.05f, boss + face * 0.14f, 0.12f, 0.07f, 9, Steel);
            Crate(b, new Vector3(0.9f, 0f, 0.05f), new Vector3(0.6f, 0.36f, 0.34f), 0f, random);

            // The board over it: a stroke for each, in fives.
            Vector3 slate = new Vector3(0f, top + 0.42f, -half.Y + 0.06f);
            b.Box(slate, new Vector3(1.1f, 0.3f, 0.04f), Walnut);
            b.Box(slate + Vector3.UnitZ * 0.045f, new Vector3(1f, 0.22f, 0.008f), Slate);
            for (int i = 0; i < 13; i++)
            {
                float x = -0.86f + i * 0.13f + (i / 5) * 0.08f;
                b.Box(slate + new Vector3(x, 0f, 0.056f), new Vector3(0.012f, 0.13f, 0.004f), Chalk);
            }
            foreach (float x in new[] { -0.6f, 0.13f }) Hall.Board(b, slate + new Vector3(x - 0.34f, -0.1f, 0.058f), slate + new Vector3(x + 0.3f, 0.1f, 0.058f), Vector3.UnitY, 0.012f, 0.004f, Chalk);
        }

        // A boot standing: a foot, a heel and a shaft, its front towards +z.
        private static void Boot(MeshBuilder b, Vector3 foot, float yaw, VertexMaterial leather)
        {
            Hall.At(b, foot, yaw, () =>
            {
                b.Box(new Vector3(0f, 0.09f, 0.08f), new Vector3(0.1f, 0.09f, 0.22f), leather);
                b.Box(new Vector3(0f, 0.02f, 0.06f), new Vector3(0.11f, 0.02f, 0.24f), Hall.Leather(60, 40, 28));
                b.Tube(new Vector3(0f, 0.16f, -0.06f), new Vector3(0f, 0.78f, -0.08f), 0.11f, 0.13f, 8, leather, capped: false);
                b.Tube(new Vector3(0f, 0.72f, -0.08f), new Vector3(0f, 0.78f, -0.08f), 0.135f, 0.135f, 8, Hall.Leather(60, 40, 28), capped: false);
            });
        }

        /// <summary>
        /// Harness on a stand. One is finished and waiting for whoever it was made for: mail under a breastplate burnished bright, the helmet on top, a sash in the Haven's
        /// teal. The other is what came back from a land: the same plate with three furrows clawed across it from shoulder to hip, its helmet stove in and set down at its
        /// foot, and its shield against it with a mouthful gone from the rim.
        /// </summary>
        private static void Harness(MeshBuilder b, float r, float h, bool mauled)
        {
            VertexMaterial wood = Hall.Timber(120, 86, 58);
            VertexMaterial plate = mauled ? Hall.Metal(150, 152, 158) : Bright;
            b.Tube(Vector3.Zero, new Vector3(0f, 0.18f, 0f), r * 0.8f, r * 0.74f, 10, wood);
            b.Tube(new Vector3(0f, 0.18f, 0f), new Vector3(0f, h - 1.2f, 0f), 0.08f, 0.07f, 6, wood);
            b.Beam(new Vector3(-0.85f, h - 1.55f, 0f), new Vector3(0.85f, h - 1.55f, 0f), 0.06f, wood);
            b.Tube(new Vector3(0f, h - 3.5f, 0f), new Vector3(0f, h - 2.2f, 0f), 0.6f, 0.66f, 12, Mail, capped: false);
            b.Tube(new Vector3(0f, h - 2.4f, 0f), new Vector3(0f, h - 1.5f, 0f), 0.68f, 0.56f, 12, plate);
            b.Box(new Vector3(0f, h - 1.95f, 0.6f), new Vector3(0.05f, 0.42f, 0.04f), plate);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Tube(new Vector3(s * 0.62f, h - 1.6f, 0f), new Vector3(s * 0.97f, h - 1.75f, 0f), 0.31f, 0.26f, 8, plate);
                b.Tube(new Vector3(s * 0.86f, h - 1.8f, 0f), new Vector3(s * 0.9f, h - 2.9f, 0.05f), 0.2f, 0.17f, 8, Mail, capped: false);
            }
            b.Tube(new Vector3(0f, h - 1.45f, 0f), new Vector3(0f, h - 1.2f, 0f), 0.16f, 0.14f, 6, wood);
            if (!mauled)
            {
                Helmet(b, new Vector3(0f, h - 1.25f, 0f), 1, Bright);
                // The sash, knotted at the hip with its ends down.
                b.Tube(new Vector3(0f, h - 2.55f, 0f), new Vector3(0f, h - 2.35f, 0f), 0.7f, 0.69f, 12, Dyed(Teal), capped: false);
                b.Box(new Vector3(0.5f, h - 2.9f, 0.48f), new Vector3(0.09f, 0.4f, 0.03f), Dyed(Teal));
                b.Box(new Vector3(0.36f, h - 2.8f, 0.52f), new Vector3(0.08f, 0.3f, 0.03f), Dyed(Teal * 0.85f));
                Tag(b, new Vector3(-0.4f, h - 2.4f, 0.66f), Vector3.UnitZ);
                return;
            }

            // Three furrows, shoulder to hip: the dark of the rent and the bright of its torn lip.
            for (int claw = 0; claw < 3; claw++)
            {
                Vector3 from = new Vector3(-0.42f + claw * 0.2f, h - 1.62f, 0.56f + claw * 0.03f), to = new Vector3(-0.02f + claw * 0.2f, h - 2.36f, 0.66f - claw * 0.02f);
                Vector3 across = Vector3.Cross(to - from, Vector3.UnitZ).Normalized();
                Hall.Board(b, from, to, across, 0.035f, 0.02f, Hall.Flat(22, 20, 20));
                Hall.Board(b, from + across * 0.04f, to + across * 0.04f, across, 0.012f, 0.026f, Bright);
            }
            // The helmet at its foot, its crown stove in, and the shield with the bite out of it.
            Hall.At(b, new Vector3(-0.55f, 0.02f, r - 0.1f), 0.7f, () =>
            {
                b.Tube(Vector3.Zero, new Vector3(0f, 0.3f, 0f), 0.3f, 0.3f, 10, plate, capped: false);
                b.Tube(new Vector3(0f, 0.3f, 0f), new Vector3(0.1f, 0.36f, 0.04f), 0.3f, 0.2f, 10, plate, capped: false);
                b.Tube(new Vector3(0.1f, 0.36f, 0.04f), new Vector3(0.12f, 0.3f, 0.05f), 0.2f, 0.06f, 10, Hall.Metal(120, 122, 128));
            });
            Vector3 boss = new Vector3(0.5f, 0.62f, r - 0.08f);
            Vector3 face = new Vector3(0.2f, 0.38f, 1f).Normalized();
            Vector3 right = Vector3.Cross(Vector3.UnitY, face).Normalized(), up = Vector3.Cross(face, right).Normalized();
            const int Rim = 20;
            // The bite: a mouthful out of the rim at the top right, splintered where the teeth went through.
            float Reach(int corner) => corner is >= 2 and <= 4 ? 0.3f + 0.05f * (corner % 2) : 0.6f;
            for (int i = 0; i < Rim; i++)
            {
                float a0 = MathF.Tau * i / Rim, a1 = MathF.Tau * (i + 1) / Rim;
                Vector3 p0 = boss + (right * MathF.Cos(a0) + up * MathF.Sin(a0)) * Reach(i), p1 = boss + (right * MathF.Cos(a1) + up * MathF.Sin(a1)) * Reach(i + 1);
                VertexMaterial paint = PaintedOf(i % 10 < 5 ? Teal : Cream);
                Hall.Tri(b, boss + face * 0.03f, p0 + face * 0.03f, p1 + face * 0.03f, face, paint);
                Hall.Tri(b, boss - face * 0.03f, p0 - face * 0.03f, p1 - face * 0.03f, -face, Hall.Timber(140, 104, 68));
                if (Reach(i) == 0.6f && Reach(i + 1) == 0.6f) Hall.Board(b, p0, p1, face, 0.045f, 0.035f, Hall.Iron(88));
            }
            b.Tube(boss + face * 0.03f, boss + face * 0.14f, 0.15f, 0.08f, 9, Steel);
        }

        // What came on the cart this morning and hasn't been put away: crates on one another, the top one opened, straw and all, and a sack.
        private static void Delivery(MeshBuilder b, Vector2 half, WorldRandom random)
        {
            Crate(b, new Vector3(-half.X * 0.42f, 0f, 0f), new Vector3(half.X * 0.52f, 0.42f, half.Y * 0.86f), 0.03f, random);
            Crate(b, new Vector3(half.X * 0.5f, 0f, -0.05f), new Vector3(half.X * 0.45f, 0.36f, half.Y * 0.78f), -0.05f, random);
            Vector3 open = new Vector3(-half.X * 0.36f, 0.84f, 0.02f);
            Crate(b, open, new Vector3(half.X * 0.44f, 0.3f, half.Y * 0.66f), 0.14f, random);
            b.Box(open + Vector3.UnitY * 0.61f, new Vector3(half.X * 0.4f, 0.03f, half.Y * 0.6f), Hay);
            Stalks(b, open + Vector3.UnitY * 0.6f, 0.4f, 12, random);
            for (int i = 0; i < 4; i++) b.Box(open + new Vector3(-0.26f + i * 0.17f, 0.67f, random.Range(-0.2f, 0.2f)), new Vector3(0.07f, 0.035f, 0.2f), DarkSteel);
            // Its lid, against it, and the bar that took it off.
            Hall.Board(b, new Vector3(half.X * 0.2f, 0.72f, half.Y - 0.02f), new Vector3(half.X * 0.9f, 1.3f, half.Y - 0.3f), Vector3.UnitZ, 0.36f, 0.02f, Weathered(random));
            b.Beam(new Vector3(half.X * 0.9f, 0.74f, 0.1f), new Vector3(half.X * 0.3f, 0.76f, -0.2f), 0.02f, Hall.Iron(72));
            Sack(b, new Vector3(half.X + 0.3f, 0f, -0.1f), 0.36f, 0.95f, random);
        }

        /// <summary>
        /// The Forge's great work, on trestles in the middle of the floor, where everyone has to walk round it: an engine to throw a bolt the length of a spear at whatever comes
        /// over the water, for the Haven's wall. All three trades are in it: its stock and frame are the joiner's, its bands, its washers and its bolts' heads the smith's, its
        /// skeins of rope and the fletching of its bolts the others'. It's half built. One arm is in its skein and the other lies across the trestle waiting, its string is
        /// coiled on the stock, a bolt is laid in the groove to try the fit, and the plan it's built to is pinned to the trestle. Its head is towards +z, and its arms along x.
        /// </summary>
        private static void Ballista(MeshBuilder b, Vector2 half, List<AssetLight> lights, WorldRandom random)
        {
            // Drawn at the size of a thing a person could carry and built half as big again, as everything here is for people 4.7 m tall: the footprint it's given is the
            // size it stands, and what follows is in its own measure.
            const float Size = 1.45f;
            Vector3 stretch = b.Stretch;
            b.Stretch = stretch * Size;
            half /= Size;
            VertexMaterial timber = Hall.Timber(150, 110, 70);
            VertexMaterial rope = Hall.Cloth(166, 142, 98);
            const float Bed = 1.05f;
            // Two trestles.
            foreach (float z in new[] { -0.75f, 0.45f })
            {
                Hall.Board(b, new Vector3(-0.95f, Bed - 0.07f, z), new Vector3(0.95f, Bed - 0.07f, z), Vector3.UnitY, 0.07f, 0.09f, FrameDark);
                foreach (float x in new[] { -0.8f, 0.8f })
                {
                    foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(x * 1.1f, 0f, z + s * 0.36f), new Vector3(x, Bed - 0.14f, z + s * 0.05f), Vector3.UnitX, 0.05f, 0.045f, FrameDark);
                }
            }
            // The stock: a beam with a groove down it, banded in iron, and the bolt that's laid in it.
            float back = -half.Y + 0.05f, head = half.Y - 0.35f;
            Hall.Board(b, new Vector3(0f, Bed + 0.11f, back), new Vector3(0f, Bed + 0.11f, head + 0.1f), Vector3.UnitX, 0.15f, 0.11f, timber);
            Hall.Board(b, new Vector3(0f, Bed + 0.225f, back + 0.5f), new Vector3(0f, Bed + 0.225f, head), Vector3.UnitX, 0.05f, 0.006f, Walnut);
            foreach (float z in new[] { back + 0.3f, -0.1f, head - 0.25f }) b.Box(new Vector3(0f, Bed + 0.11f, z), new Vector3(0.16f, 0.118f, 0.045f), Hall.Iron(74));
            Bolt(b, new Vector3(0f, Bed + 0.27f, back + 0.55f), new Vector3(0f, Bed + 0.27f, head + 0.5f));

            // The windlass at its tail: a drum, its two handspikes, and the hook that draws the string.
            b.Tube(new Vector3(-0.34f, Bed + 0.16f, back + 0.12f), new Vector3(0.34f, Bed + 0.16f, back + 0.12f), 0.11f, 0.11f, 8, Walnut);
            b.Tube(new Vector3(0.34f, Bed + 0.16f, back + 0.12f), new Vector3(-0.34f, Bed + 0.16f, back + 0.12f), 0.11f, 0.11f, 8, Walnut);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 0.24f, Bed + 0.16f, back + 0.12f), new Vector3(0.03f, 0.17f, 0.17f), timber);
                b.Tube(new Vector3(s * 0.4f, Bed + 0.16f - s * 0.3f, back - 0.14f), new Vector3(s * 0.4f, Bed + 0.16f + s * 0.34f, back + 0.42f), 0.03f, 0.028f, 6, Handle);
            }
            b.Tube(new Vector3(-0.12f, Bed + 0.16f, back + 0.12f), new Vector3(0.12f, Bed + 0.16f, back + 0.12f), 0.13f, 0.13f, 8, rope, capped: false);
            b.Box(new Vector3(0f, Bed + 0.25f, back + 0.42f), new Vector3(0.07f, 0.03f, 0.08f), Hall.Iron(74));

            // Its head: a frame of two plates and four posts, a skein of rope standing in each side of it under an iron washer.
            float frame = head + 0.02f;
            foreach (float y in new[] { Bed - 0.22f, Bed + 0.62f }) Hall.Board(b, new Vector3(-0.92f, y, frame), new Vector3(0.92f, y, frame), Vector3.UnitZ, 0.2f, 0.05f, timber);
            foreach (float x in new[] { -0.86f, -0.3f, 0.3f, 0.86f }) b.Box(new Vector3(x, Bed + 0.2f, frame), new Vector3(0.05f, 0.37f, 0.16f), timber);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 skein = new Vector3(s * 0.58f, Bed + 0.2f, frame);
                b.Tube(skein - Vector3.UnitY * 0.4f, skein + Vector3.UnitY * 0.44f, 0.13f, 0.13f, 8, rope, capped: false);
                b.Tube(skein + Vector3.UnitY * 0.47f, skein + Vector3.UnitY * 0.52f, 0.18f, 0.18f, 9, Hall.Iron(80));
                b.Beam(skein + new Vector3(-0.2f, 0.55f, 0f), skein + new Vector3(0.2f, 0.55f, 0f), 0.025f, Hall.Iron(66));
                b.Box(new Vector3(s * 0.58f, Bed + 0.67f, frame + 0.19f), new Vector3(0.3f, 0.03f, 0.012f), Hall.Iron(74));
            }
            // One arm in, drawn a little back as it will stand, its tip bound for the string. The other lies across the trestle, shaped and not yet fitted.
            Vector3 root = new Vector3(0.58f, Bed + 0.2f, frame), tip = new Vector3(half.X - 0.08f, Bed + 0.2f, frame - 0.42f);
            Hall.Board(b, root, Vector3.Lerp(root, tip, 0.55f), Vector3.UnitY, 0.075f, 0.055f, timber);
            Hall.Board(b, Vector3.Lerp(root, tip, 0.55f), tip, Vector3.UnitY, 0.055f, 0.04f, timber);
            b.Tube(tip - Vector3.UnitX * 0.14f, tip - Vector3.UnitX * 0.02f, 0.065f, 0.065f, 7, rope, capped: false);
            Vector3 lying = new Vector3(-1.5f, Bed + 0.06f, -0.55f);
            Hall.Board(b, lying, lying + new Vector3(0.75f, 0f, 0.5f), Vector3.UnitY, 0.075f, 0.055f, Pale);
            Hall.Board(b, lying + new Vector3(0.75f, 0f, 0.5f), lying + new Vector3(1.35f, 0f, 0.95f), Vector3.UnitY, 0.055f, 0.04f, Pale);
            // Its string, coiled on the stock till there are two arms to string, and a mallet and an auger put down on it.
            for (int turn = 0; turn < 3; turn++) b.Tube(new Vector3(0.02f, Bed + 0.235f + turn * 0.04f, -0.42f), new Vector3(0.02f, Bed + 0.27f + turn * 0.04f, -0.42f), 0.2f - turn * 0.03f, 0.2f - turn * 0.03f, 9, rope, capped: false);
            b.Tube(new Vector3(-0.5f, Bed + 0.1f, 0.42f), new Vector3(-0.24f, Bed + 0.1f, 0.5f), 0.1f, 0.1f, 7, Oak);
            b.Tube(new Vector3(-0.37f, Bed + 0.1f, 0.46f), new Vector3(-0.5f, Bed + 0.06f, 0.05f), 0.03f, 0.03f, 6, Handle);
            b.Beam(new Vector3(0.5f, Bed + 0.04f, -0.9f), new Vector3(0.84f, Bed + 0.04f, -0.6f), 0.02f, DarkSteel);
            b.Tube(new Vector3(0.36f, Bed + 0.04f, -0.74f), new Vector3(0.64f, Bed + 0.04f, -1.06f), 0.03f, 0.03f, 6, Handle);

            // The bolts that are done, leaned in a row against the back trestle, heads up, and the plan, pinned to the front one where everyone can see it.
            for (int i = 0; i < 4; i++)
            {
                Vector3 butt = new Vector3(-0.9f + i * 0.2f, 0f, back - 0.2f + i * 0.03f);
                Bolt(b, butt, new Vector3(butt.X + 0.06f, 2f - (i % 2) * 0.1f, -0.72f));
            }
            Vector3 plan = new Vector3(0.62f, Bed - 0.5f, 0.45f + 0.1f);
            b.Box(plan, new Vector3(0.34f, 0.26f, 0.008f), Hall.Paper);
            b.Box(plan + new Vector3(0f, 0.02f, 0.01f), new Vector3(0.02f, 0.2f, 0.003f), Hall.Ink);
            Hall.Board(b, plan + new Vector3(-0.26f, 0.06f, 0.01f), plan + new Vector3(0f, 0.16f, 0.01f), Vector3.UnitY, 0.012f, 0.003f, Hall.Ink);
            Hall.Board(b, plan + new Vector3(0.26f, 0.06f, 0.01f), plan + new Vector3(0f, 0.16f, 0.01f), Vector3.UnitY, 0.012f, 0.003f, Hall.Ink);
            // Shavings under it, as curls of pale wood.
            for (int i = 0; i < 26; i++)
            {
                Vector3 at = new Vector3(random.Range(-half.X + 0.2f, half.X - 0.2f), 0.03f, random.Range(-half.Y + 0.1f, half.Y + 0.4f));
                b.Tube(at, at + new Vector3(random.Range(-0.1f, 0.1f), 0f, random.Range(-0.1f, 0.1f)), 0.03f, 0.03f, 5, random.Chance(0.5f) ? Pale : Hall.Timber(206, 174, 126), capped: false);
            }
            b.Stretch = stretch;
            // A lantern stood on the end of the back trestle, to work on it by. Said at the size it's built, as a light's place isn't stretched with the shapes.
            Lantern(b, new Vector3(0.8f, Bed, -0.75f) * Size + Vector3.UnitY * 0.36f, lights, 24f, 10f, shadow: false);
        }

        // A bolt for the engine: an ash shaft as thick as a wrist, a head of iron with two barbs, and three vanes of leather at its butt.
        private static void Bolt(MeshBuilder b, Vector3 butt, Vector3 point)
        {
            Vector3 along = (point - butt).Normalized();
            Vector3 across = Vector3.Cross(along, MathF.Abs(along.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY).Normalized();
            Vector3 neck = point - along * 0.34f;
            b.Tube(butt, neck, 0.045f, 0.04f, 6, Handle);
            b.Tube(neck, point, 0.075f, 0.006f, 5, DarkSteel);
            foreach (float s in new[] { -1f, 1f }) Hall.Tri(b, neck, neck + along * 0.22f, neck - along * 0.14f + across * s * 0.13f, Vector3.Cross(along, across), DarkSteel);
            foreach (float s in new[] { -1f, 1f }) Hall.Tri(b, neck, neck + along * 0.22f, neck - along * 0.14f + across * s * 0.13f, -Vector3.Cross(along, across), DarkSteel);
            for (int vane = 0; vane < 3; vane++)
            {
                float a = MathF.Tau * vane / 3f;
                Vector3 out_ = across * MathF.Cos(a) + Vector3.Cross(along, across) * MathF.Sin(a);
                Hall.Board(b, butt + along * 0.08f + out_ * 0.08f, butt + along * 0.42f + out_ * 0.07f, out_, 0.045f, 0.005f, Tan);
            }
        }
    }
}
