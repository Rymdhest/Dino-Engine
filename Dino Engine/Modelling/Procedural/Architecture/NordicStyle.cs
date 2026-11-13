using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    // The north's way of building (BuildingStyle.Nordic), after the old farms of Sweden: walls of upright boards with a batten over every joint, painted the red of the
    // north's copper mines, and white at every corner, every window, every door and along every verge, so that from up here a farm is red walls drawn in white under
    // roofs of red-brown tiles or of turf. Windows of six panes in casings with a crown over them; a porch with a little gable over the door (a farstukvist); a brick stack
    // with a band of plaster under its cap; a stone plinth under it all. The village's holy place is a red chapel under tarred shingles with its bells in a tower of their
    // own beside it (a klockstapel); its hall carries a bell on its ridge (a vällingklocka); its barn has white crosses on its doors; and its sheds are a privy with a
    // heart in its door beside the woodshed, or a storehouse of red logs up on stones, out of the mice's reach (a härbre). Front towards +z, every building of the
    // catalogue to its size. The red is warm, earth and iron, and kept apart from the Reich's oxblood, which is deeper and leans to blue: nothing white ever goes with that.
    public static partial class BuildingGenerator
    {
        private enum NordicRoofing { Tiles, Turf, Shingles }

        /// <summary>What a northern building is made of, chosen once for it.</summary>
        private sealed class Nordic
        {
            public VertexMaterial Red;          // The walls.
            public VertexMaterial White;        // Corner boards, casings, sashes, verges and fascias.
            public VertexMaterial Roof;
            public VertexMaterial Ridge;
            public VertexMaterial Plinth;
            public VertexMaterial Door;
            public VertexMaterial Window;
            public VertexMaterial Brick;
            public VertexMaterial Plaster;      // The band round a chimney under its cap.
            public VertexMaterial Cap;
            public VertexMaterial Deck;
            public VertexMaterial Dark;         // What's seen through an opening.
            public VertexMaterial Tar;          // Tarred shingles: a spire, a bell tower.
            public VertexMaterial Bronze;
            public VertexMaterial Logs;         // Red-painted logs, for what's built the old way.
            public NordicRoofing Roofing;
            public float Pitch;
            public float Eave;                  // How far the roof reaches past the long walls.
            public float Verge;                 // And past the gables.
            public float Thickness;             // How thick the roof is at its edges.
            public float Lumps;                 // How uneven turf lies.
            public bool Horns;                  // Verge boards crossed past the ridge, as an old storehouse's are.
            public bool Sunburst;               // White boards fanned in a porch's gable.
            public List<AssetLight>? Lights;
        }

        private const float NordicBoards = 0.7f;        // What the painted boards' own texture comes to, as a building's colours are meant as albedos.
        private const float NordicBark = 0.72f;         // And the bare wood's, for red-painted logs.
        private const float NordicDoorTop = 5.1f;       // A door's height over its sill: a little over a person's.
        private const float NordicTrimClear = 0.09f;    // Where a trim board's texture starts across one of the material's boards, so that it misses the battens.

        private static VertexMaterial Painted(Vector3 albedo) => new VertexMaterial(TextureGenerator.paintedBoards, new Colour(albedo / NordicBoards));

        private static Mesh BuildNordic(StructureDefinition definition, int variant, WorldRandom random, List<Vector3>? chimneys, List<AssetLight>? lights)
        {
            Nordic n = NordicKit(definition, variant, random);
            n.Lights = lights;
            var b = new MeshBuilder();
            Vector2 half = definition.HalfExtents;
            if (definition == StructureCatalog.Barn) NordicBarn(b, half, n, random);
            else if (definition == StructureCatalog.Temple) NordicChapel(b, half, n, random);
            else if (definition == StructureCatalog.Shed && variant % 2 == 1) NordicStorehouse(b, half, n, random);
            else if (definition == StructureCatalog.Shed) NordicOuthouse(b, half, n, random);
            else NordicDwelling(b, definition, half, n, random, chimneys);
            return b.Build();
        }

        private static Nordic NordicKit(StructureDefinition definition, int variant, WorldRandom random)
        {
            bool barn = definition == StructureCatalog.Barn;
            bool shed = definition == StructureCatalog.Shed;
            bool temple = definition == StructureCatalog.Temple;
            bool storehouse = shed && variant % 2 == 1;

            // Every farm mixed its own paint, and the sun has had longer at some: from a deep fresh red to one gone chalky and pale, never the Reich's oxblood, which
            // has blue in it where this has the yellow of iron earth.
            Vector3 red = Vector3.Lerp(new Vector3(0.29f, 0.043f, 0.02f), new Vector3(0.38f, 0.07f, 0.034f), random.Float()) * random.Range(0.95f, 1.05f);
            Vector3 white = new Vector3(0.66f, 0.65f, 0.61f) * random.Range(0.97f, 1.02f);

            float roll = random.Float();
            NordicRoofing roofing = temple ? NordicRoofing.Shingles
                : storehouse ? (roll < 0.5f ? NordicRoofing.Turf : NordicRoofing.Shingles)
                : barn ? (roll < 0.45f ? NordicRoofing.Tiles : roll < 0.75f ? NordicRoofing.Turf : NordicRoofing.Shingles)
                : shed ? (roll < 0.45f ? NordicRoofing.Turf : roll < 0.85f ? NordicRoofing.Tiles : NordicRoofing.Shingles)
                : definition == StructureCatalog.Hall || definition == StructureCatalog.Inn ? (roll < 0.85f ? NordicRoofing.Tiles : NordicRoofing.Turf)
                : roll < 0.72f ? NordicRoofing.Tiles : NordicRoofing.Turf;

            // Doors are painted a colour of their own: the green and the blue the north paints its doors, a brown-red, a grey.
            Vector3[] doors = { new Vector3(0.075f, 0.15f, 0.09f), new Vector3(0.085f, 0.125f, 0.2f), new Vector3(0.17f, 0.065f, 0.035f), new Vector3(0.28f, 0.28f, 0.26f) };
            Vector3 door = doors[random.Int(doors.Length)] * random.Range(0.92f, 1.08f);

            var n = new Nordic
            {
                Red = Painted(red),
                White = Painted(white),
                Plinth = new VertexMaterial(TextureGenerator.stoneWall, Shade(new Vector3(0.66f, 0.67f, 0.72f), random, 0.06f)),
                Door = Painted(door),
                // As every style's: glass with a faint light behind it, so that a window reads as a room and not a hole.
                Window = new VertexMaterial(TextureGenerator.flatGlow, new Colour(1f, 0.6f, 0.26f, 0.5f)),
                Brick = new VertexMaterial(TextureGenerator.brick, Shade(new Vector3(0.95f, 0.88f, 0.86f), random, 0.06f)),
                Plaster = new VertexMaterial(TextureGenerator.plaster, new Colour(0.97f, 0.97f, 0.95f)),
                Cap = new VertexMaterial(TextureGenerator.slate, new Colour(0.62f, 0.62f, 0.64f)),
                Deck = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.82f, 0.8f, 0.76f), random, 0.06f)),
                Dark = new VertexMaterial(TextureGenerator.flat, new Colour(16, 13, 11)),
                Tar = new VertexMaterial(TextureGenerator.shingle, Shade(new Vector3(0.36f, 0.32f, 0.3f), random, 0.06f)),
                Bronze = new VertexMaterial(TextureGenerator.iron, new Colour(0.5f, 0.38f, 0.24f)),
                Logs = new VertexMaterial(TextureGenerator.barkBare, new Colour(red / NordicBark)),
                Roofing = roofing,
                Sunburst = random.Chance(0.5f),
            };

            switch (roofing)
            {
                case NordicRoofing.Tiles:
                    // Red clay pantiles, deeper and browner than the south's, some roofs older and darker than others.
                    n.Roof = new VertexMaterial(TextureGenerator.roofTiles, Shade(new Vector3(0.6f, 0.44f, 0.43f), random, 0.1f));
                    n.Ridge = new VertexMaterial(TextureGenerator.roofTiles, new Colour(0.5f, 0.36f, 0.35f));
                    n.Pitch = random.Range(0.78f, 0.9f);
                    n.Thickness = 0.28f;
                    break;
                case NordicRoofing.Turf:
                    // Sod on birch bark, lower pitched so that it stays where it's laid, and lumpy, with its ridge held down by a log.
                    n.Roof = new VertexMaterial(TextureGenerator.grass, Shade(new Vector3(0.66f, 0.74f, 0.5f), random, 0.1f));
                    n.Ridge = new VertexMaterial(TextureGenerator.barkBare, new Colour(84, 70, 58));
                    n.Pitch = random.Range(0.62f, 0.72f);
                    n.Thickness = 0.5f;
                    n.Lumps = 0.18f;
                    n.Horns = (shed || barn) && random.Chance(0.6f);
                    break;
                default:
                    // Riven shingles, tarred near black: steep, as shingles want.
                    n.Roof = n.Tar;
                    n.Ridge = new VertexMaterial(TextureGenerator.wood, new Colour(0.32f, 0.29f, 0.27f));
                    n.Pitch = random.Range(0.95f, 1.15f);
                    n.Thickness = 0.22f;
                    n.Horns = storehouse;
                    break;
            }
            n.Eave = random.Range(0.75f, 0.92f);
            n.Verge = random.Range(0.52f, 0.66f);
            return n;
        }

        // ================================================================ Trim and boards

        /// <summary>
        /// A board of the trim, or anything else squared and painted: a box about <paramref name="centre"/>, half <paramref name="length"/> along its grain, half
        /// <paramref name="width"/> across its face and half <paramref name="thick"/> out of it. Each face is textured with the grain along the board and across one of the
        /// material's boards, between its battens, so a corner board shows none: kept to 0.3 m wide. <paramref name="back"/> false leaves out the face against the wall.
        /// </summary>
        private static void Trim(MeshBuilder b, Vector3 centre, Vector3 length, Vector3 width, Vector3 thick, VertexMaterial material, bool back = false)
        {
            // The faces below are wound for a right-handed set; turning the width round makes one, and keeps the back the side away from where thick points.
            if (Vector3.Dot(Vector3.Cross(length, width), thick) < 0f) width = -width;
            Vector3 A = length, B = width, C = thick;
            float a = A.Length, w = B.Length, t = C.Length;
            if (a < 1e-4f || w < 1e-4f || t < 1e-4f) return;
            Vector3 ad = A / a, bd = B / w, cd = C / t;
            // A shift along the grain, so that two boards side by side don't show the same patch of it.
            float shift = (centre.X * 1.7f + centre.Y * 0.6f + centre.Z * 2.3f) % 2.4f;

            void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 uAxis, float uHalf, Vector3 vAxis, float vHalf)
            {
                Vector3 origin = centre - uAxis * (uHalf + NordicTrimClear) - vAxis * shift;
                Vector4 bounds = new Vector4(NordicTrimClear, NordicTrimClear + uHalf * 2f, shift - vHalf, shift + vHalf);
                b.PlaneQuad(p0, p1, p2, p3, material, origin, uAxis, vAxis, bounds);
            }

            Vector3 c = centre;
            Face(c - A - B + C, c + A - B + C, c + A + B + C, c - A + B + C, bd, w, ad, a);
            if (back) Face(c + A - B - C, c - A - B - C, c - A + B - C, c + A + B - C, bd, w, ad, a);
            Face(c + A - B - C, c + A + B - C, c + A + B + C, c + A - B + C, bd, w, cd, t);
            Face(c - A + B - C, c - A - B - C, c - A - B + C, c - A + B + C, bd, w, cd, t);
            Face(c + B - C - A, c + B + C - A, c + B + C + A, c + B - C + A, cd, t, ad, a);
            Face(c - B + C - A, c - B - C - A, c - B - C + A, c - B + C + A, cd, t, ad, a);
        }

        // A box turned any way, its texture laid as a plain box's: a ridge board on edge, a tower's sloping cladding.
        private static void TurnedBox(MeshBuilder b, Vector3 centre, Vector3 A, Vector3 B, float halfC, VertexMaterial material)
        {
            Vector3 C = Vector3.Cross(A, B).Normalized() * halfC;
            Vector3 c = centre;
            b.Quad(c - A - B + C, c + A - B + C, c + A + B + C, c - A + B + C, material);
            b.Quad(c + A - B - C, c - A - B - C, c - A + B - C, c + A + B - C, material);
            b.Quad(c + A - B - C, c + A + B - C, c + A + B + C, c + A - B + C, material);
            b.Quad(c - A + B - C, c - A - B - C, c - A - B + C, c - A + B + C, material);
            b.Quad(c + B - C - A, c + B + C - A, c + B + C + A, c + B - C + A, material);
            b.Quad(c - B + C - A, c - B - C - A, c - B - C + A, c - B + C + A, material);
        }

        // White boards up every corner, the one on the long wall lapping the end of the one on the gable, so that the corner is one white line: what "white corners" means.
        private static void CornerBoards(MeshBuilder b, Vector2 half, float bottom, float top, VertexMaterial white)
        {
            const float Half = 0.15f, Thick = 0.045f;
            float middle = (bottom + top) * 0.5f, tall = (top - bottom) * 0.5f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 front = new Vector3(sx * (half.X - Half + Thick * 2f), middle, sz * (half.Y + Thick));
                    Trim(b, front, Vector3.UnitY * tall, Vector3.UnitX * (Half + Thick), Vector3.UnitZ * sz * Thick, white);
                    Vector3 end = new Vector3(sx * (half.X + Thick * 0.9f), middle, sz * (half.Y - Half));
                    Trim(b, end, Vector3.UnitY * tall, Vector3.UnitZ * Half, Vector3.UnitX * sx * Thick * 0.9f, white);
                }
            }
        }

        // ================================================================ Windows and doors

        /// <summary>
        /// A window of two casements, each <paramref name="rows"/> panes high, set in the wall that runs along <paramref name="along"/> (its outside is that crossed with up):
        /// white sashes and bars standing just proud of the glass, in a casing with a sill under it and a head and a crown over it, as the north frames a window. Only a
        /// window on the ground floor throws its pool of light on the ground (<paramref name="shines"/>), as one above it would only light the same ground again.
        /// </summary>
        private static void NordicWindow(MeshBuilder b, Nordic n, Vector3 centre, Vector3 along, float halfWidth, float halfHeight, int rows, int across = 2, bool shines = true)
        {
            Vector3 up = Vector3.UnitY;
            Vector3 outward = Vector3.Cross(along, up);
            Vector3 glass = centre + outward * 0.02f;
            b.Quad(glass - along * halfWidth - up * halfHeight, glass + along * halfWidth - up * halfHeight, glass + along * halfWidth + up * halfHeight, glass - along * halfWidth + up * halfHeight, n.Window);
            if (shines) n.Lights?.Add(AssetLight.Window(centre + outward * 0.5f, outward));

            // The sashes: a frame round the glass, a bar between the casements and bars between the panes.
            const float Frame = 0.07f, Bar = 0.032f;
            Vector3 sash = centre + outward * 0.055f;
            Vector3 depth = outward * 0.035f;
            Trim(b, sash + up * (halfHeight - Frame), along * halfWidth, up * Frame, depth, n.White);
            Trim(b, sash - up * (halfHeight - Frame), along * halfWidth, up * Frame, depth, n.White);
            Trim(b, sash + along * (halfWidth - Frame), up * (halfHeight - Frame * 2f), along * Frame, depth, n.White);
            Trim(b, sash - along * (halfWidth - Frame), up * (halfHeight - Frame * 2f), along * Frame, depth, n.White);
            for (int i = 1; i < across; i++)
            {
                float x = -halfWidth + 2f * halfWidth * i / across;
                Trim(b, sash + along * x, up * (halfHeight - Frame * 2f), along * (Bar * 1.6f), depth, n.White);
            }
            for (int j = 1; j < rows; j++)
            {
                float y = -halfHeight + 2f * halfHeight * j / rows;
                Trim(b, sash + up * y + outward * 0.005f, along * (halfWidth - Frame * 2f), up * Bar, depth, n.White);
            }

            // The casing: boards either side, a head over it with its crown standing out above, and a sill that throws the rain off.
            const float Side = 0.12f;
            Vector3 casing = centre + outward * 0.045f;
            Trim(b, casing + along * (halfWidth + Side), up * (halfHeight + 0.02f), along * Side, outward * 0.04f, n.White);
            Trim(b, casing - along * (halfWidth + Side), up * (halfHeight + 0.02f), along * Side, outward * 0.04f, n.White);
            Trim(b, casing + up * (halfHeight + 0.16f), along * (halfWidth + Side * 2f), up * 0.15f, outward * 0.05f, n.White);
            Trim(b, centre + up * (halfHeight + 0.34f) + outward * 0.11f, along * (halfWidth + Side * 2f + 0.1f), outward * 0.11f, up * 0.035f, n.White, back: true);
            Trim(b, centre - up * (halfHeight + 0.07f) + outward * 0.09f, along * (halfWidth + Side * 2f), outward * 0.1f, up * 0.04f, n.White, back: true);
        }

        /// <summary>
        /// A door facing +z at <paramref name="z"/>: boards in its paint with raised panels, one leaf or a pair, a fanlight over a pair, between white casings under a head
        /// with a crown. Returns how high its casing's crown stands.
        /// </summary>
        private static float NordicDoor(MeshBuilder b, Nordic n, float x, float z, float halfWidth, float bottom, float top, bool pair, bool fanlight)
        {
            b.Quad(new Vector3(x - halfWidth, bottom, z + 0.03f), new Vector3(x + halfWidth, bottom, z + 0.03f), new Vector3(x + halfWidth, top, z + 0.03f), new Vector3(x - halfWidth, top, z + 0.03f), n.Door);
            int leaves = pair ? 2 : 1;
            float leaf = halfWidth * 2f / leaves;
            VertexMaterial panel = Shaded(n.Door, 1.12f);
            float tall = top - bottom;
            for (int i = 0; i < leaves; i++)
            {
                float middle = x - halfWidth + leaf * (i + 0.5f);
                float panelHalf = MathF.Max(leaf * 0.5f - 0.24f, 0.12f);
                (float from, float to)[] panels = { (0.08f, 0.36f), (0.44f, 0.92f) };
                foreach ((float from, float to) in panels)
                {
                    float low = bottom + tall * from, high = bottom + tall * to;
                    b.Box(new Vector3(middle, (low + high) * 0.5f, z + 0.06f), new Vector3(panelHalf, (high - low) * 0.5f, 0.03f), panel);
                }
            }

            float head = top;
            if (fanlight)
            {
                const float Light = 0.75f;
                b.Quad(new Vector3(x - halfWidth, top, z + 0.03f), new Vector3(x + halfWidth, top, z + 0.03f), new Vector3(x + halfWidth, top + Light, z + 0.03f), new Vector3(x - halfWidth, top + Light, z + 0.03f), n.Window);
                Trim(b, new Vector3(x, top + 0.04f, z + 0.07f), Vector3.UnitX * halfWidth, Vector3.UnitY * 0.06f, Vector3.UnitZ * 0.04f, n.White);
                for (int i = 1; i < 3; i++)
                {
                    float bx = x - halfWidth + halfWidth * 2f * i / 3f;
                    Trim(b, new Vector3(bx, top + Light * 0.5f, z + 0.07f), Vector3.UnitY * (Light * 0.5f), Vector3.UnitX * 0.035f, Vector3.UnitZ * 0.03f, n.White);
                }
                head = top + Light;
            }

            const float Side = 0.14f;
            float jamb = (head - bottom) * 0.5f;
            Trim(b, new Vector3(x - halfWidth - Side, bottom + jamb, z + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * Side, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(x + halfWidth + Side, bottom + jamb, z + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * Side, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(x, head + 0.15f, z + 0.06f), Vector3.UnitX * (halfWidth + Side * 2f + 0.06f), Vector3.UnitY * 0.15f, Vector3.UnitZ * 0.06f, n.White);
            Trim(b, new Vector3(x, head + 0.34f, z + 0.13f), Vector3.UnitX * (halfWidth + Side * 2f + 0.16f), Vector3.UnitZ * 0.13f, Vector3.UnitY * 0.04f, n.White, back: true);
            return head + 0.38f;
        }

        // A door of boards facing +z on ledges and a brace, all white over red, as the north hangs a barn's and a shed's: a white frame round each leaf and a cross on it.
        private static void CrossedDoor(MeshBuilder b, Nordic n, float x, float z, float halfWidth, float bottom, float top, int leaves, bool crossed)
        {
            b.Quad(new Vector3(x - halfWidth, bottom, z + 0.03f), new Vector3(x + halfWidth, bottom, z + 0.03f), new Vector3(x + halfWidth, top, z + 0.03f), new Vector3(x - halfWidth, top, z + 0.03f), n.Red);
            float leaf = halfWidth * 2f / leaves;
            const float Rail = 0.13f, Thick = 0.04f;
            for (int i = 0; i < leaves; i++)
            {
                float left = x - halfWidth + leaf * i, right = left + leaf, middle = (left + right) * 0.5f;
                float front = z + 0.03f + Thick;
                Trim(b, new Vector3(middle, top - Rail, front), Vector3.UnitX * (leaf * 0.5f), Vector3.UnitY * Rail, Vector3.UnitZ * Thick, n.White);
                Trim(b, new Vector3(middle, bottom + Rail, front), Vector3.UnitX * (leaf * 0.5f), Vector3.UnitY * Rail, Vector3.UnitZ * Thick, n.White);
                Trim(b, new Vector3(left + Rail, (top + bottom) * 0.5f, front), Vector3.UnitY * ((top - bottom) * 0.5f - Rail * 2f), Vector3.UnitX * Rail, Vector3.UnitZ * Thick, n.White);
                Trim(b, new Vector3(right - Rail, (top + bottom) * 0.5f, front), Vector3.UnitY * ((top - bottom) * 0.5f - Rail * 2f), Vector3.UnitX * Rail, Vector3.UnitZ * Thick, n.White);
                // A brace rises from the hinge, which is at the outer edge of each leaf of a pair, so a pair's braces make a chevron.
                bool hingeRight = leaves > 1 && i == leaves - 1;
                Vector3 low = new Vector3(hingeRight ? right - Rail * 2f : left + Rail * 2f, bottom + Rail * 2f, front + 0.01f);
                Vector3 high = new Vector3(hingeRight ? left + Rail * 2f : right - Rail * 2f, top - Rail * 2f, front + 0.01f);
                Vector3 run = high - low;
                Vector3 across = Vector3.Cross(Vector3.UnitZ, run).Normalized();
                Trim(b, (low + high) * 0.5f, run * 0.5f, across * (Rail * 0.85f), Vector3.UnitZ * Thick, n.White);
                if (!crossed) continue;
                low = new Vector3(hingeRight ? left + Rail * 2f : right - Rail * 2f, bottom + Rail * 2f, front + 0.02f);
                high = new Vector3(hingeRight ? right - Rail * 2f : left + Rail * 2f, top - Rail * 2f, front + 0.02f);
                run = high - low;
                across = Vector3.Cross(Vector3.UnitZ, run).Normalized();
                Trim(b, (low + high) * 0.5f, run * 0.5f, across * (Rail * 0.85f), Vector3.UnitZ * Thick, n.White);
            }
            // The frame of the doorway.
            float jamb = (top - bottom) * 0.5f;
            Trim(b, new Vector3(x - halfWidth - 0.13f, bottom + jamb, z + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * 0.13f, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(x + halfWidth + 0.13f, bottom + jamb, z + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * 0.13f, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(x, top + 0.14f, z + 0.05f), Vector3.UnitX * (halfWidth + 0.26f), Vector3.UnitY * 0.14f, Vector3.UnitZ * 0.05f, n.White);
        }

        // Windows evenly along a wall, centred on its middle, kept from its corners: `count` of them, or as many as fit.
        private static List<float> EvenSlots(float halfLength, float spacing, float margin)
        {
            int count = Math.Max(1, (int)((halfLength * 2f - margin * 2f) / spacing));
            var slots = new List<float>(count);
            float usable = halfLength * 2f - margin * 2f;
            for (int i = 0; i < count; i++) slots.Add(-usable * 0.5f + usable * (i + 0.5f) / count);
            return slots;
        }

        // Windows either side of a door, as a farmhouse's front is laid out about its door: from just clear of it outwards, as many as fit before the corner.
        private static List<float> FrontSlots(float halfLength, float doorX, float clear, float windowHalf, float spacing)
        {
            var slots = new List<float>();
            foreach (float side in new[] { -1f, 1f })
            {
                for (float at = clear + windowHalf; MathF.Abs(doorX + side * at) + windowHalf + 0.75f <= halfLength; at += spacing) slots.Add(doorX + side * at);
            }
            slots.Sort();
            return slots;
        }

        // ================================================================ Roofs

        // The surface of a roof whose ridge runs along x: thick enough at its edges to read as laid, and, as turf, wandering a little, though never at its edges.
        private static Vector3 NordicRoofPoint(float u, float v, float ridgeHalf, float reach, float eave, float ridge, float lumps, float seed, float side)
        {
            float x = (u * 2f - 1f) * ridgeHalf;
            float z = reach * (1f - v) * side;
            float y = eave + (ridge - eave) * v;
            if (lumps > 0f)
            {
                float edge = MathF.Min(MathF.Min(u, 1f - u) * 6f, 1f) * MathF.Min(v * 5f, 1f) * MathF.Min((1f - v) * 5f, 1f);
                y += lumps * (0.5f + 0.5f * MathF.Sin(x * 1.1f + seed) * MathF.Cos(z * 1.6f + seed * 1.7f) + 0.35f * MathF.Sin(x * 0.4f + z * 0.9f + seed)) * edge;
            }
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// A gable roof over walls of <paramref name="half"/> whose tops are at <paramref name="top"/>, its ridge along x: the roofing, its edges' thickness, white verge
        /// boards up both gables and white fascias along the eaves, a ridge to suit what it's roofed with, and the gables themselves in the walls' red, with a white board
        /// across each at the eaves. Returns the height of the top of the ridge. The roof's underside meets the walls' tops exactly, as a northern roof sits on its walls.
        /// </summary>
        private static float NordicRoof(MeshBuilder b, Nordic n, Vector2 half, float top, WorldRandom random, bool gables = true)
        {
            float k = n.Pitch, th = n.Thickness;
            float reach = half.Y + n.Eave;
            float ridgeHalf = half.X + n.Verge;
            float eaveTop = top + th - n.Eave * k;
            float ridgeTop = top + th + half.Y * k;
            float seedA = random.Float() * 20f, seedB = random.Float() * 20f;
            bool turf = n.Roofing == NordicRoofing.Turf;
            int su = turf ? 14 : 4, sv = turf ? 7 : 2;
            float lumps = turf ? n.Lumps : 0f;
            b.Surface(su, sv, (u, v) => NordicRoofPoint(u, v, ridgeHalf, reach, eaveTop, ridgeTop, lumps, seedA, 1f), n.Roof);
            b.Surface(su, sv, (u, v) => NordicRoofPoint(1f - u, v, ridgeHalf, reach, eaveTop, ridgeTop, lumps, seedB, -1f), n.Roof);

            // Its thickness along the eaves, and up both verges.
            float low = eaveTop - th, peakLow = ridgeTop - th;
            b.Quad(new Vector3(-ridgeHalf, low, reach), new Vector3(ridgeHalf, low, reach), new Vector3(ridgeHalf, eaveTop, reach), new Vector3(-ridgeHalf, eaveTop, reach), n.Roof);
            b.Quad(new Vector3(ridgeHalf, low, -reach), new Vector3(-ridgeHalf, low, -reach), new Vector3(-ridgeHalf, eaveTop, -reach), new Vector3(ridgeHalf, eaveTop, -reach), n.Roof);
            float x = ridgeHalf;
            b.Quad(new Vector3(x, low, reach), new Vector3(x, peakLow, 0f), new Vector3(x, ridgeTop, 0f), new Vector3(x, eaveTop, reach), n.Roof);
            b.Quad(new Vector3(x, peakLow, 0f), new Vector3(x, low, -reach), new Vector3(x, eaveTop, -reach), new Vector3(x, ridgeTop, 0f), n.Roof);
            b.Quad(new Vector3(-x, peakLow, 0f), new Vector3(-x, low, reach), new Vector3(-x, eaveTop, reach), new Vector3(-x, ridgeTop, 0f), n.Roof);
            b.Quad(new Vector3(-x, low, -reach), new Vector3(-x, peakLow, 0f), new Vector3(-x, ridgeTop, 0f), new Vector3(-x, eaveTop, -reach), n.Roof);

            // Turf is held at its foot by a log along the eaves, which the fascia's board covers on a roof of tiles or shingles.
            if (turf)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(-ridgeHalf - 0.05f, eaveTop - th * 0.45f, side * (reach + 0.05f)), new Vector3(ridgeHalf + 0.05f, eaveTop - th * 0.45f, side * (reach + 0.05f)), 0.2f, 0.19f, 7, n.Ridge);
                }
            }
            else
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Trim(b, new Vector3(0f, eaveTop - 0.11f, side * (reach + 0.035f)), Vector3.UnitX * (ridgeHalf + 0.03f), Vector3.UnitY * 0.15f, Vector3.UnitZ * side * 0.035f, n.White);
                }
            }

            // The verge boards, up from each eave to the ridge, standing a little above the roofing so that from up here every gable is drawn in white; crossed past the
            // ridge into horns on an old storehouse.
            float run = MathF.Sqrt(reach * reach + (ridgeTop - eaveTop) * (ridgeTop - eaveTop));
            float past = n.Horns ? 0.95f : 0.12f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 foot = new Vector3(sx * (ridgeHalf + 0.04f), eaveTop, sz * (reach + 0.04f));
                    Vector3 peak = new Vector3(sx * (ridgeHalf + 0.04f), ridgeTop, 0f);
                    Vector3 along = (peak - foot) / run;
                    Vector3 outward = new Vector3(0f, reach, sz * (ridgeTop - eaveTop)) / run;    // Out of the roof, square to the slope.
                    Vector3 from = foot - along * 0.05f, to = peak + along * past;
                    Trim(b, (from + to) * 0.5f - outward * 0.07f, (to - from) * 0.5f, outward * 0.15f, Vector3.UnitX * sx * 0.04f, n.White, back: n.Horns);
                }
            }

            // The ridge: capped with tiles, held by a log on turf, or a board on edge over shingles.
            switch (n.Roofing)
            {
                case NordicRoofing.Tiles:
                    b.Tube(new Vector3(-ridgeHalf - 0.02f, ridgeTop - 0.06f, 0f), new Vector3(ridgeHalf + 0.02f, ridgeTop - 0.06f, 0f), 0.24f, 0.24f, 8, n.Ridge);
                    break;
                case NordicRoofing.Turf:
                    b.Tube(new Vector3(-ridgeHalf - 0.15f, ridgeTop + 0.02f, 0f), new Vector3(ridgeHalf + 0.15f, ridgeTop + 0.02f, 0f), 0.21f, 0.2f, 7, n.Ridge);
                    break;
                default:
                    TurnedBox(b, new Vector3(0f, ridgeTop - 0.02f, 0f), Vector3.UnitX * (ridgeHalf + 0.05f), new Vector3(0f, 0.12f, 0.12f), 0.12f, n.Ridge);
                    break;
            }

            if (!gables) return ridgeTop;
            // The gables, in the walls' red, and a white board across each at the eaves, which carries the eaves' line round the house.
            float apex = top + half.Y * k;
            b.Triangle(new Vector3(half.X, top, half.Y), new Vector3(half.X, top, -half.Y), new Vector3(half.X, apex, 0f), n.Red);
            b.Triangle(new Vector3(-half.X, top, -half.Y), new Vector3(-half.X, top, half.Y), new Vector3(-half.X, apex, 0f), n.Red);
            foreach (float sx in new[] { -1f, 1f })
            {
                Trim(b, new Vector3(sx * (half.X + 0.04f), top - 0.1f, 0f), Vector3.UnitZ * (half.Y - 0.3f), Vector3.UnitY * 0.13f, Vector3.UnitX * sx * 0.04f, n.White);
            }
            return ridgeTop;
        }

        // A stack of brick through the ridge, as tall again over it as a person's arm, with a band of plaster under a slab of a cap, and where its smoke comes out.
        private static void NordicChimney(MeshBuilder b, Nordic n, Vector2 at, float from, float top, List<Vector3>? chimneys)
        {
            const float HalfX = 0.62f, HalfZ = 0.5f;
            b.Box(new Vector3(at.X, (from + top) * 0.5f, at.Y), new Vector3(HalfX, (top - from) * 0.5f, HalfZ), n.Brick);
            b.Box(new Vector3(at.X, top - 0.5f, at.Y), new Vector3(HalfX + 0.06f, 0.3f, HalfZ + 0.06f), n.Plaster);
            b.Box(new Vector3(at.X, top + 0.08f, at.Y), new Vector3(HalfX + 0.16f, 0.08f, HalfZ + 0.16f), n.Cap);
            chimneys?.Add(new Vector3(at.X, top + 0.7f, at.Y));
        }

        // ================================================================ Porches

        /// <summary>
        /// A porch over a door in the wall at <paramref name="wallZ"/> (a farstukvist): a deck up from the yard by two steps, white posts at its front corners with rails
        /// back to the wall, and a little gable of the house's roofing with its gable to the yard and its verges white, its ridge never higher than
        /// <paramref name="mostRidge"/>, the house's eaves' lowest edge, so that the two roofs never cut into each other. Or, as a balcony, a flat roof with a railing round
        /// it at <paramref name="balconyFloor"/>. False, and nothing built, if there's no room for its roof between the door's casing and the eaves.
        /// </summary>
        private static bool NordicPorch(MeshBuilder b, Nordic n, float x, float wallZ, float halfWidth, float depth, float floor, float casingTop, float mostRidge, float balconyFloor = 0f)
        {
            bool balcony = balconyFloor > 0f;
            const float Over = 0.32f;       // How far its roof reaches past its posts.
            float halfSpan = halfWidth + Over;
            float eave = casingTop + 0.22f;
            float pitch = MathF.Min(0.8f, (mostRidge - eave) / halfSpan);
            if (!balcony && pitch < 0.32f) return false;
            float ridge = eave + halfSpan * pitch;
            float postTop = balcony ? balconyFloor - 0.3f : eave - 0.02f;
            float z0 = wallZ, front = wallZ + depth, z1 = front + Over + 0.06f;

            // The deck, and the steps up to it.
            b.Box(new Vector3(x, floor - 1.2f, wallZ + depth * 0.5f), new Vector3(halfWidth, 1.2f, depth * 0.5f), n.Deck);
            for (int step = 1; step <= 2; step++)
            {
                float height = floor * (1f - step / 3f);
                b.Box(new Vector3(x, height - 0.8f, front + 0.42f * step - 0.21f + 0.02f), new Vector3(halfWidth * 0.72f, 0.8f, 0.21f), n.Plinth);
            }

            // Posts at its front corners, beams back to the wall and across the front, and a rail either side.
            float postX = halfWidth - 0.15f, postZ = front - 0.15f;
            foreach (float s in new[] { -1f, 1f })
            {
                Trim(b, new Vector3(x + s * postX, (floor + postTop) * 0.5f, postZ), Vector3.UnitY * ((postTop - floor) * 0.5f), Vector3.UnitX * 0.12f, Vector3.UnitZ * 0.12f, n.White, back: true);
                Trim(b, new Vector3(x + s * postX, postTop - 0.13f, (wallZ + postZ) * 0.5f), Vector3.UnitZ * ((postZ - wallZ) * 0.5f), Vector3.UnitY * 0.13f, Vector3.UnitX * 0.07f, n.White, back: true);
                float railFrom = wallZ + 0.08f, railTo = postZ - 0.12f, railMiddle = (railFrom + railTo) * 0.5f, railHalf = (railTo - railFrom) * 0.5f;
                Trim(b, new Vector3(x + s * postX, floor + 1.7f, railMiddle), Vector3.UnitZ * railHalf, Vector3.UnitY * 0.07f, Vector3.UnitX * 0.06f, n.White, back: true);
                Trim(b, new Vector3(x + s * postX, floor + 0.32f, railMiddle), Vector3.UnitZ * railHalf, Vector3.UnitY * 0.06f, Vector3.UnitX * 0.05f, n.White, back: true);
                for (int i = 1; i <= 4; i++)
                {
                    float z = railFrom + (railTo - railFrom) * i / 5f;
                    Trim(b, new Vector3(x + s * postX, floor + 1.01f, z), Vector3.UnitY * 0.63f, Vector3.UnitZ * 0.045f, Vector3.UnitX * 0.045f, n.White, back: true);
                }
            }
            Trim(b, new Vector3(x, postTop - 0.15f, postZ), Vector3.UnitX * (postX + 0.12f), Vector3.UnitY * 0.15f, Vector3.UnitZ * 0.09f, n.White, back: true);

            if (balcony)
            {
                // A deck on its posts, with a white fascia round it and a railing with turned-looking balusters, onto which a door opens from upstairs.
                b.Box(new Vector3(x, balconyFloor - 0.15f, wallZ + (depth + 0.1f) * 0.5f), new Vector3(halfWidth + 0.12f, 0.15f, (depth + 0.1f) * 0.5f), n.Deck);
                Trim(b, new Vector3(x, balconyFloor - 0.15f, front + 0.12f), Vector3.UnitX * (halfWidth + 0.14f), Vector3.UnitY * 0.17f, Vector3.UnitZ * 0.04f, n.White);
                foreach (float s in new[] { -1f, 1f }) Trim(b, new Vector3(x + s * (halfWidth + 0.14f), balconyFloor - 0.15f, wallZ + (depth + 0.1f) * 0.5f), Vector3.UnitZ * ((depth + 0.1f) * 0.5f), Vector3.UnitY * 0.17f, Vector3.UnitX * s * 0.04f, n.White);
                float railY = balconyFloor + 1.75f;
                Trim(b, new Vector3(x, railY, front - 0.02f), Vector3.UnitX * halfWidth, Vector3.UnitZ * 0.08f, Vector3.UnitY * 0.05f, n.White, back: true);
                Trim(b, new Vector3(x, balconyFloor + 0.25f, front - 0.02f), Vector3.UnitX * halfWidth, Vector3.UnitY * 0.06f, Vector3.UnitZ * 0.05f, n.White, back: true);
                int count = Math.Max(3, (int)(halfWidth * 2f / 0.42f));
                for (int i = 0; i <= count; i++)
                {
                    float bx = x - halfWidth + halfWidth * 2f * i / count;
                    Trim(b, new Vector3(bx, balconyFloor + 0.98f, front - 0.02f), Vector3.UnitY * 0.72f, Vector3.UnitX * 0.045f, Vector3.UnitZ * 0.045f, n.White, back: true);
                }
                foreach (float s in new[] { -1f, 1f })
                {
                    Trim(b, new Vector3(x + s * halfWidth, railY, wallZ + depth * 0.5f), Vector3.UnitZ * (depth * 0.5f), Vector3.UnitX * 0.08f, Vector3.UnitY * 0.05f, n.White, back: true);
                    for (int i = 1; i <= 4; i++)
                    {
                        float z = wallZ + depth * i / 5f;
                        Trim(b, new Vector3(x + s * halfWidth, balconyFloor + 0.98f, z), Vector3.UnitY * 0.72f, Vector3.UnitZ * 0.045f, Vector3.UnitX * 0.045f, n.White, back: true);
                    }
                }
                return true;
            }

            // The roof: two slopes running out from the wall, its thickness up the front verges and along the eaves, a ridge, and white verge boards.
            float th = MathF.Min(n.Thickness, 0.26f);
            float rise = ridge - eave;
            b.Surface(3, 2, (u, v) => new Vector3(x + halfSpan * (1f - v), eave + rise * v, z1 + (z0 - z1) * u), n.Roof);
            b.Surface(3, 2, (u, v) => new Vector3(x - halfSpan * (1f - v), eave + rise * v, z0 + (z1 - z0) * u), n.Roof);
            b.Quad(new Vector3(x - halfSpan, eave - th, z1), new Vector3(x, ridge - th, z1), new Vector3(x, ridge, z1), new Vector3(x - halfSpan, eave, z1), n.Roof);
            b.Quad(new Vector3(x, ridge - th, z1), new Vector3(x + halfSpan, eave - th, z1), new Vector3(x + halfSpan, eave, z1), new Vector3(x, ridge, z1), n.Roof);
            b.Quad(new Vector3(x + halfSpan, eave - th, z1), new Vector3(x + halfSpan, eave - th, z0), new Vector3(x + halfSpan, eave, z0), new Vector3(x + halfSpan, eave, z1), n.Roof);
            b.Quad(new Vector3(x - halfSpan, eave - th, z0), new Vector3(x - halfSpan, eave - th, z1), new Vector3(x - halfSpan, eave, z1), new Vector3(x - halfSpan, eave, z0), n.Roof);
            if (n.Roofing == NordicRoofing.Turf) b.Tube(new Vector3(x, ridge + 0.01f, z0), new Vector3(x, ridge + 0.01f, z1 + 0.1f), 0.15f, 0.14f, 6, n.Ridge);
            else b.Tube(new Vector3(x, ridge - 0.05f, z0), new Vector3(x, ridge - 0.05f, z1 + 0.03f), 0.17f, 0.17f, 7, n.Ridge);

            float slope = MathF.Sqrt(halfSpan * halfSpan + rise * rise);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 foot = new Vector3(x + s * halfSpan, eave, z1 + 0.035f), peak = new Vector3(x, ridge, z1 + 0.035f);
                Vector3 along = (peak - foot) / slope;
                Vector3 outward = new Vector3(s * rise, halfSpan, 0f) / slope;
                Vector3 from = foot - along * 0.04f, to = peak + along * 0.08f;
                Trim(b, (from + to) * 0.5f - outward * 0.06f, (to - from) * 0.5f, outward * 0.13f, Vector3.UnitZ * 0.035f, n.White);
            }

            // Its gable, in the walls' red over the beam across the front, sometimes with white boards fanned in it, as a sun rising.
            float under = eave - th;
            float gableZ = front - 0.04f;
            float footY = under + Over * pitch, apexY = ridge - th - 0.02f;
            b.Triangle(new Vector3(x - halfWidth, footY, gableZ), new Vector3(x + halfWidth, footY, gableZ), new Vector3(x, apexY, gableZ), n.Red);
            if (n.Sunburst && apexY - footY > 0.9f)
            {
                Vector3 hub = new Vector3(x, footY + 0.05f, gableZ + 0.03f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathF.PI * (0.18f + 0.64f * i / 4f);
                    Vector3 direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
                    // As far as the gable's sloping edge, a little short of it.
                    float slopeHeight = apexY - footY;
                    float reachRay = slopeHeight / (direction.Y + MathF.Abs(direction.X) * slopeHeight / halfWidth) * 0.86f;
                    Vector3 tip = hub + direction * reachRay;
                    Vector3 sideways = new Vector3(-direction.Y, direction.X, 0f);
                    Trim(b, (hub + tip) * 0.5f, (tip - hub) * 0.5f, sideways * 0.06f, Vector3.UnitZ * 0.025f, n.White);
                }
            }
            return true;
        }

        // A little roof of boards over a door, on two white brackets, where a porch won't fit.
        private static void DoorHood(MeshBuilder b, Nordic n, float x, float wallZ, float halfWidth, float casingTop)
        {
            float high = casingTop + 0.75f, low = casingTop + 0.25f, reach = 1.25f;
            float x0 = x - halfWidth - 0.45f, x1 = x + halfWidth + 0.45f;
            b.Quad(new Vector3(x0, low, wallZ + reach), new Vector3(x1, low, wallZ + reach), new Vector3(x1, high, wallZ), new Vector3(x0, high, wallZ), n.Roof);
            b.Quad(new Vector3(x0, low - 0.16f, wallZ + reach), new Vector3(x1, low - 0.16f, wallZ + reach), new Vector3(x1, low, wallZ + reach), new Vector3(x0, low, wallZ + reach), n.Roof);
            foreach (float s in new[] { -1f, 1f })
            {
                float bx = x + s * (halfWidth + 0.3f);
                Trim(b, new Vector3(bx, high - 0.85f, wallZ + 0.06f), Vector3.UnitY * 0.65f, Vector3.UnitZ * 0.06f, Vector3.UnitX * 0.06f, n.White, back: true);
                Vector3 from = new Vector3(bx, high - 1.4f, wallZ + 0.1f), to = new Vector3(bx, low - 0.12f, wallZ + reach - 0.15f);
                Vector3 run = to - from;
                Trim(b, (from + to) * 0.5f, run * 0.5f, Vector3.Cross(Vector3.UnitX, run).Normalized() * 0.06f, Vector3.UnitX * 0.05f, n.White, back: true);
            }
        }

        // ================================================================ Dwellings

        // How each house of the catalogue is built in the north: its walls' height from plinth to eaves, its storeys (one and a half have small windows under the eaves
        // and windows in the gables), its door, its porch, its chimneys and whether a bell stands on its ridge.
        private sealed class NordicPlan
        {
            public float Plinth = 1f;
            public float Wall = 7.8f;
            public int Storeys = 1;
            public bool Knee;
            public bool Porch;
            public bool Balcony;
            public bool PairDoor;
            public bool Fanlight;
            public bool SecondDoor;
            public int Chimneys = 1;
            public bool Turret;
        }

        private static NordicPlan PlanFor(StructureDefinition definition, WorldRandom random)
        {
            if (definition == StructureCatalog.Hut) return new NordicPlan { Plinth = 0.8f, Wall = 7.2f, Porch = false };
            if (definition == StructureCatalog.Cottage) return new NordicPlan { Wall = 7.8f, Porch = random.Chance(0.6f) };
            if (definition == StructureCatalog.Longhouse) return new NordicPlan { Wall = 8.8f, Knee = true, Porch = random.Chance(0.75f), PairDoor = true, Fanlight = random.Chance(0.6f), SecondDoor = random.Chance(0.5f), Chimneys = 2 };
            // An inn's porch carries a balcony at its upper floor, which a fanlight over the door would reach into.
            if (definition == StructureCatalog.Inn) return new NordicPlan { Wall = 11.8f, Storeys = 2, Porch = true, Balcony = true, PairDoor = true, Chimneys = 2 };
            if (definition == StructureCatalog.Hall) return new NordicPlan { Wall = 10.6f, Storeys = 2, Porch = true, PairDoor = true, Fanlight = true, Chimneys = 2, Turret = true };
            // A farmhouse of a storey and a half, or of two.
            bool two = random.Chance(0.4f);
            return new NordicPlan { Wall = two ? 11.2f : 9f, Storeys = two ? 2 : 1, Knee = !two, Porch = random.Chance(0.8f), PairDoor = true, Fanlight = random.Chance(0.5f), Chimneys = random.Chance(0.35f) ? 2 : 1 };
        }

        private static void NordicDwelling(MeshBuilder b, StructureDefinition definition, Vector2 half, Nordic n, WorldRandom random, List<Vector3>? chimneys)
        {
            NordicPlan plan = PlanFor(definition, random);
            float p = plan.Plinth, top = p + plan.Wall;
            bool hut = definition == StructureCatalog.Hut;

            // A plinth of field stones, reaching well below the ground, so that a slope shows no daylight under the house.
            b.Box(new Vector3(0f, p - 2.4f, 0f), new Vector3(half.X + 0.2f, 2.4f, half.Y + 0.2f), n.Plinth);
            b.Box(new Vector3(0f, (p + top) * 0.5f, 0f), new Vector3(half.X, (top - p) * 0.5f, half.Y), n.Red);
            CornerBoards(b, half, p - 0.04f, top, n.White);

            // The door, in the middle of the front as a farmhouse's front is laid out about it, or to one side of a cottage too small for that, and its porch, which keeps
            // under the eaves' lowest edge.
            float doorX = hut ? (random.Chance(0.5f) ? 1f : -1f) * half.X * 0.36f : 0f;
            float doorHalf = plan.PairDoor ? 1.45f : 1.12f;
            float doorTop = p + NordicDoorTop;
            float casingTop = NordicDoor(b, n, doorX, half.Y, doorHalf, p, doorTop, plan.PairDoor, plan.Fanlight);
            float porchHalf = doorHalf + 1.15f;
            float storey = plan.Wall / plan.Storeys;
            float eaveLow = top - n.Eave * n.Pitch - 0.15f;
            bool porch = plan.Porch && NordicPorch(b, n, doorX, half.Y, porchHalf, 2.8f, p - 0.02f, casingTop, eaveLow, plan.Balcony ? p + storey : 0f);
            if (!porch)
            {
                DoorHood(b, n, doorX, half.Y, doorHalf, casingTop);
                b.Box(new Vector3(doorX, p * 0.5f - 0.6f, half.Y + 0.75f), new Vector3(doorHalf + 0.5f, 0.6f, 0.55f), n.Plinth);
            }
            if (plan.Balcony) NordicDoor(b, n, doorX, half.Y, 1.1f, p + storey, p + storey + 4.5f, false, false);

            // A plain second door at one end of the front, where the kitchen is.
            float secondX = 0f;
            if (plan.SecondDoor)
            {
                secondX = (random.Chance(0.5f) ? 1f : -1f) * (half.X - 2.2f);
                float second = NordicDoor(b, n, secondX, half.Y, 1.05f, p, doorTop, false, false);
                DoorHood(b, n, secondX, half.Y, 1.05f, second);
                b.Box(new Vector3(secondX, p * 0.5f - 0.6f, half.Y + 0.7f), new Vector3(1.55f, 0.6f, 0.5f), n.Plinth);
            }

            // Windows of six panes on the ground floor; the same over them upstairs, or, where the upper storey is half in the roof, small ones of two panes under the
            // eaves and a row of the upper floor's in the gable ends.
            const float WindowHalf = 0.9f, WindowHigh = 1.38f, Spacing = 3.9f;
            float clear = porch ? porchHalf + 0.55f : doorHalf + 0.75f;
            List<float> front = hut ? new List<float> { -MathF.Sign(doorX) * half.X * 0.47f } : FrontSlots(half.X, doorX, clear, WindowHalf + 0.36f, Spacing);
            if (plan.SecondDoor) front.RemoveAll(at => MathF.Abs(at - secondX) < 1.05f + 0.9f + WindowHalf + 0.4f);
            List<float> back = EvenSlots(half.X, Spacing + 0.3f, 1.35f);
            List<float> ends = EvenSlots(half.Y, Spacing + 0.4f, 1.35f);
            float groundY = p + MathF.Min(2.95f, storey * 0.52f);
            var walls = new (Vector3 Origin, Vector3 Along, List<float> Slots)[]
            {
                (new Vector3(0f, 0f, half.Y), Vector3.UnitX, front),
                (new Vector3(0f, 0f, -half.Y), -Vector3.UnitX, back),
                (new Vector3(half.X, 0f, 0f), -Vector3.UnitZ, ends),
                (new Vector3(-half.X, 0f, 0f), Vector3.UnitZ, ends),
            };
            for (int w = 0; w < walls.Length; w++)
            {
                (Vector3 origin, Vector3 along, List<float> slots) = walls[w];
                // The back and the ends leave out one now and then, as rooms behind them need; the front never does.
                bool spare = w > 0 && slots.Count > 1;
                foreach (float at in slots)
                {
                    if (spare && random.Chance(0.18f)) continue;
                    NordicWindow(b, n, origin + along * at + Vector3.UnitY * groundY, along, WindowHalf, WindowHigh, 3);
                    if (plan.Storeys == 2) NordicWindow(b, n, origin + along * at + Vector3.UnitY * (groundY + storey), along, WindowHalf, WindowHigh, 3, shines: false);
                    else if (plan.Knee && w < 2) NordicWindow(b, n, origin + along * at + Vector3.UnitY * (top - 0.95f), along, 0.78f, 0.45f, 1, shines: false);
                    else if (plan.Knee) NordicWindow(b, n, origin + along * at + Vector3.UnitY * (top - 1.66f), along, WindowHalf, 1f, 3, shines: false);
                }
                if (w == 0 && plan.Storeys == 2 && !plan.Balcony) NordicWindow(b, n, origin + along * doorX + Vector3.UnitY * (groundY + storey), along, WindowHalf, WindowHigh, 3, shines: false);
            }

            float ridgeTop = NordicRoof(b, n, half, top, random);

            // A small window under each gable's peak, where there's height for one.
            float apex = top + half.Y * n.Pitch;
            if (apex - top > 2.6f)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    Vector3 along = sx > 0f ? -Vector3.UnitZ : Vector3.UnitZ;
                    NordicWindow(b, n, new Vector3(sx * half.X, top + (apex - top) * 0.36f, 0f), along, 0.62f, 0.72f, 2, shines: false);
                }
            }

            // Chimneys up through the ridge.
            for (int i = 0; i < plan.Chimneys; i++)
            {
                float cx = plan.Chimneys == 1 ? random.Range(-0.25f, 0.25f) * half.X : (i == 0 ? 1f : -1f) * half.X * random.Range(0.42f, 0.58f);
                NordicChimney(b, n, new Vector2(cx, 0f), top - 1.5f, ridgeTop + (hut ? 1.2f : 1.7f), chimneys);
            }
            if (plan.Turret) NordicBellTurret(b, n, 0f, ridgeTop, n.Pitch, 1.05f);
        }

        // A bell on the ridge, in an open turret of its own under a spire with a vane: what calls a village to its meals and its meetings.
        private static void NordicBellTurret(MeshBuilder b, Nordic n, float x, float ridgeTop, float pitch, float half)
        {
            float foot = ridgeTop - half * pitch - 0.45f, body = ridgeTop + 0.9f, belfry = body + 2f;
            b.Box(new Vector3(x, (foot + body) * 0.5f, 0f), new Vector3(half, (body - foot) * 0.5f, half), n.Red);
            CornerBoardsAt(b, new Vector3(x, 0f, 0f), new Vector2(half, half), ridgeTop - 0.4f, body, n.White);
            b.Box(new Vector3(x, body + 0.06f, 0f), new Vector3(half + 0.08f, 0.06f, half + 0.08f), n.White);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Trim(b, new Vector3(x + sx * (half - 0.1f), (body + belfry) * 0.5f, sz * (half - 0.1f)), Vector3.UnitY * ((belfry - body) * 0.5f), Vector3.UnitX * 0.1f, Vector3.UnitZ * 0.1f, n.White, back: true);
                }
            }
            // The bell, flaring to its lip, on a beam across the belfry.
            b.Tube(new Vector3(x, belfry - 1.55f, 0f), new Vector3(x, belfry - 0.55f, 0f), half * 0.5f, half * 0.22f, 10, n.Bronze);
            b.Tube(new Vector3(x, belfry - 1.62f, 0f), new Vector3(x, belfry - 1.5f, 0f), half * 0.54f, half * 0.54f, 10, n.Bronze);
            Trim(b, new Vector3(x, belfry - 0.4f, 0f), Vector3.UnitX * half, Vector3.UnitY * 0.1f, Vector3.UnitZ * 0.08f, n.White, back: true);

            // A spire of tarred shingles, a ball, and a vane.
            float eave = half + 0.32f, point = belfry + eave * 2.4f;
            b.Box(new Vector3(x, belfry + 0.05f, 0f), new Vector3(eave, 0.07f, eave), n.White);
            for (int side = 0; side < 4; side++)
            {
                b.Place(new Vector3(x, 0f, 0f), side * MathF.PI * 0.5f);
                b.Triangle(new Vector3(-eave, belfry + 0.1f, eave), new Vector3(eave, belfry + 0.1f, eave), new Vector3(0f, point, 0f), n.Tar);
            }
            b.ResetPlacement();
            b.Tube(new Vector3(x, point - 0.2f, 0f), new Vector3(x, point + 1.6f, 0f), 0.05f, 0.03f, 5, n.Bronze);
            b.Tube(new Vector3(x, point + 0.45f, 0f), new Vector3(x, point + 0.75f, 0f), 0.17f, 0.17f, 7, n.Bronze);
            b.Box(new Vector3(x + 0.38f, point + 1.25f, 0f), new Vector3(0.36f, 0.2f, 0.015f), n.Bronze);
        }

        // ================================================================ Barn

        // A barn of red boards, white at its corners: wide wagon doors in the middle of its front, white crosses on them and white frames round them, a door for people
        // beside them, small stable windows, and a hatch for the hay high in each gable under a beam for the hoist.
        private static void NordicBarn(MeshBuilder b, Vector2 half, Nordic n, WorldRandom random)
        {
            const float P = 0.7f;
            float top = P + random.Range(8.6f, 9.4f);
            b.Box(new Vector3(0f, P - 2.4f, 0f), new Vector3(half.X + 0.18f, 2.4f, half.Y + 0.18f), n.Plinth);
            b.Box(new Vector3(0f, (P + top) * 0.5f, 0f), new Vector3(half.X, (top - P) * 0.5f, half.Y), n.Red);
            CornerBoards(b, half, P - 0.04f, top, n.White);

            float doorX = random.Range(-0.12f, 0.12f) * half.X;
            const float DoorHalf = 2.9f;
            float doorTop = P + 6.4f;
            CrossedDoor(b, n, doorX, half.Y, DoorHalf, P, doorTop, 2, random.Chance(0.5f));
            // A ramp of earth and stone up to the doors.
            b.Box(new Vector3(doorX, P * 0.5f - 0.6f, half.Y + 1.1f), new Vector3(DoorHalf + 0.6f, 0.6f, 0.9f), n.Plinth);
            float side = doorX > 0f ? -1f : 1f;
            float personX = doorX + side * (DoorHalf + 2.6f);
            if (MathF.Abs(personX) + 1.6f < half.X) CrossedDoor(b, n, personX, half.Y, 1.0f, P, P + 5f, 1, false);

            // Stable windows: square, of four panes, low along the front and the back.
            float windowY = P + 3.3f;
            foreach (float at in EvenSlots(half.X, 4.2f, 1.6f))
            {
                if (MathF.Abs(at - doorX) < DoorHalf + 1.5f || MathF.Abs(at - personX) < 2.4f) continue;
                NordicWindow(b, n, new Vector3(at, windowY, half.Y), Vector3.UnitX, 0.62f, 0.55f, 2);
            }
            foreach (float at in EvenSlots(half.X, 4.2f, 1.6f))
            {
                if (random.Chance(0.3f)) continue;
                NordicWindow(b, n, new Vector3(at, windowY, -half.Y), -Vector3.UnitX, 0.62f, 0.55f, 2);
            }

            float ridgeTop = NordicRoof(b, n, half, top, random);

            // The hay's hatch in each gable, and the hoist's beam out under the verges' meeting.
            float apex = top + half.Y * n.Pitch;
            foreach (float sx in new[] { -1f, 1f })
            {
                float hatchBottom = top + 0.5f, hatchTop = MathF.Min(top + 3.4f, apex - 1.6f);
                if (hatchTop - hatchBottom < 1.4f) continue;
                b.Place(new Vector3(sx * half.X, 0f, 0f), sx > 0f ? MathF.PI * 0.5f : -MathF.PI * 0.5f);
                CrossedDoor(b, n, 0f, 0f, 1.1f, hatchBottom, hatchTop, 2, false);
                b.ResetPlacement();
                b.Beam(new Vector3(sx * (half.X - 0.5f), apex - 0.55f, 0f), new Vector3(sx * (half.X + n.Verge + 0.9f), apex - 0.55f, 0f), 0.13f, n.Deck);
            }

            // Now and then a bell on the ridge, which a farm rang for its meals.
            if (random.Chance(0.3f)) NordicBellTurret(b, n, random.Range(-0.3f, 0.3f) * half.X, ridgeTop, n.Pitch, 0.75f);
        }

        // ================================================================ Sheds

        // A privy beside a woodshed under one roof, red with white corners: the woodshed's door crossed in white, and the privy's with a heart cut in it, as every one in
        // the north has.
        private static void NordicOuthouse(MeshBuilder b, Vector2 half, Nordic n, WorldRandom random)
        {
            const float P = 0.45f;
            float top = P + 5.2f;
            Vector2 walls = new Vector2(half.X - 0.35f, half.Y - 0.35f);
            b.Box(new Vector3(0f, P - 2.4f, 0f), new Vector3(walls.X + 0.15f, 2.4f, walls.Y + 0.15f), n.Plinth);
            b.Box(new Vector3(0f, (P + top) * 0.5f, 0f), new Vector3(walls.X, (top - P) * 0.5f, walls.Y), n.Red);
            CornerBoards(b, walls, P - 0.03f, top, n.White);

            float woodX = -walls.X * 0.42f, privyX = walls.X * 0.48f;
            if (random.Chance(0.5f)) (woodX, privyX) = (-woodX * 1.14f, -privyX * 0.875f);
            CrossedDoor(b, n, woodX, walls.Y, 1.05f, P, P + 4.2f, 1, false);
            float privyHalf = 0.78f, privyTop = P + 4.2f;
            b.Quad(new Vector3(privyX - privyHalf, P, walls.Y + 0.03f), new Vector3(privyX + privyHalf, P, walls.Y + 0.03f), new Vector3(privyX + privyHalf, privyTop, walls.Y + 0.03f), new Vector3(privyX - privyHalf, privyTop, walls.Y + 0.03f), n.Door);
            Trim(b, new Vector3(privyX, P + 0.9f, walls.Y + 0.07f), Vector3.UnitX * (privyHalf - 0.06f), Vector3.UnitY * 0.1f, Vector3.UnitZ * 0.04f, Shaded(n.Door, 0.85f));
            Trim(b, new Vector3(privyX, privyTop - 0.5f, walls.Y + 0.07f), Vector3.UnitX * (privyHalf - 0.06f), Vector3.UnitY * 0.1f, Vector3.UnitZ * 0.04f, Shaded(n.Door, 0.85f));
            Heart(b, new Vector3(privyX, privyTop - 1.12f, walls.Y + 0.06f), 0.3f, n.Dark);
            float jamb = (privyTop - P) * 0.5f;
            Trim(b, new Vector3(privyX - privyHalf - 0.12f, P + jamb, walls.Y + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * 0.12f, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(privyX + privyHalf + 0.12f, P + jamb, walls.Y + 0.05f), Vector3.UnitY * jamb, Vector3.UnitX * 0.12f, Vector3.UnitZ * 0.05f, n.White);
            Trim(b, new Vector3(privyX, privyTop + 0.13f, walls.Y + 0.05f), Vector3.UnitX * (privyHalf + 0.24f), Vector3.UnitY * 0.13f, Vector3.UnitZ * 0.05f, n.White);
            // A little window high in the privy's end, for the light.
            float endX = privyX > 0f ? walls.X : -walls.X;
            NordicWindow(b, n, new Vector3(endX, top - 1.3f, walls.Y * 0.3f), privyX > 0f ? -Vector3.UnitZ : Vector3.UnitZ, 0.4f, 0.36f, 1, 1, shines: false);

            NordicRoof(b, n, walls, top, random);
        }

        // A heart, as a privy's door has cut in it to let in the light: a flat dark shape facing +z, `size` across.
        private static void Heart(MeshBuilder b, Vector3 centre, float size, VertexMaterial material)
        {
            const int Points = 18;
            float scale = size / 32f;
            var rim = new Vector3[Points];
            for (int i = 0; i < Points; i++)
            {
                // The heart curve, run clockwise in t, so that reversed it winds counter-clockwise seen from the front.
                float t = -i * MathF.Tau / Points;
                float hx = 16f * MathF.Pow(MathF.Sin(t), 3f);
                float hy = 13f * MathF.Cos(t) - 5f * MathF.Cos(2f * t) - 2f * MathF.Cos(3f * t) - MathF.Cos(4f * t);
                rim[i] = centre + new Vector3(hx * scale, hy * scale, 0f);
            }
            // Counter-clockwise from the front is right then up: check the turn of the whole outline and wind each triangle to match.
            float area = 0f;
            for (int i = 0; i < Points; i++) area += rim[i].X * rim[(i + 1) % Points].Y - rim[(i + 1) % Points].X * rim[i].Y;
            Vector3 hub = centre + new Vector3(0f, 1.5f * scale, 0f);
            for (int i = 0; i < Points; i++)
            {
                Vector3 a = rim[i], c = rim[(i + 1) % Points];
                if (area > 0f) b.Triangle(hub, a, c, material);
                else b.Triangle(hub, c, a, material);
            }
        }

        // A storehouse of red logs up on four stones, a flat stone on each that no mouse gets past, its door reached by a step that doesn't touch it (a härbre).
        private static void NordicStorehouse(MeshBuilder b, Vector2 half, Nordic n, WorldRandom random)
        {
            const float Raise = 1.35f;
            float r = random.Range(0.22f, 0.25f);
            float course = r * 1.74f, over = r * 1.6f;
            Vector2 walls = new Vector2(half.X - over - 0.2f, half.Y - over - 0.2f);
            int courses = 10;
            float floor = Raise + 0.12f;
            float top = floor + r * 2f + (courses - 1) * course;

            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 at = new Vector3(sx * (walls.X - 0.1f), 0f, sz * (walls.Y - 0.1f));
                    b.Box(at + new Vector3(0f, (Raise - 0.2f) * 0.5f - 0.9f, 0f), new Vector3(0.42f, (Raise - 0.2f) * 0.5f + 0.9f, 0.42f), n.Plinth);
                    b.Box(at + new Vector3(0f, Raise - 0.1f, 0f), new Vector3(0.62f, 0.1f, 0.62f), n.Plinth);
                }
            }
            // A floor of planks over the stones, under the logs.
            b.Box(new Vector3(0f, floor - 0.06f, 0f), new Vector3(walls.X + 0.1f, 0.08f, walls.Y + 0.1f), n.Deck);

            var sides = new (Vector3 Origin, Vector3 Along, float Half, float Offset)[]
            {
                (new Vector3(0f, 0f, walls.Y), Vector3.UnitX, walls.X, 0f),
                (new Vector3(0f, 0f, -walls.Y), -Vector3.UnitX, walls.X, 0f),
                (new Vector3(walls.X, 0f, 0f), -Vector3.UnitZ, walls.Y, course * 0.5f),
                (new Vector3(-walls.X, 0f, 0f), Vector3.UnitZ, walls.Y, course * 0.5f),
            };
            float doorHalf = 0.95f, doorTop = floor + 3.3f;
            float Seam(float y) => floor + r + (MathF.Round((y - floor - r) / course - 0.5f) + 0.5f) * course;
            doorTop = Seam(doorTop);
            var openings = new[] { new List<Vector4> { new Vector4(-doorHalf, doorHalf, floor - 1f, doorTop) }, new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };
            for (int k = 0; k < courses; k++)
            {
                for (int w = 0; w < 4; w++)
                {
                    var side = sides[w];
                    float y = floor + r + side.Offset + k * course;
                    float reach = side.Half + over;
                    var material = new VertexMaterial(n.Logs.materialTextureIndex, new Colour(n.Logs.colour.ToVector3() * random.Range(0.9f, 1.06f)));
                    LogRun(b, side.Origin, side.Along, -reach, reach, y, r, openings[w], material);
                }
            }
            PlankDoor(b, 0f, walls.Y - 0.04f, doorHalf, floor, doorTop, Shaded(n.Deck, 0.55f), Shaded(n.Deck, 0.38f), false);
            // The frame: posts as deep as the logs, covering their cut ends either side of the door, and a head over it, all white.
            float jamb = (doorTop - floor) * 0.5f;
            Trim(b, new Vector3(-doorHalf - 0.1f, floor + jamb, walls.Y), Vector3.UnitY * jamb, Vector3.UnitX * 0.11f, Vector3.UnitZ * (r + 0.04f), n.White, back: true);
            Trim(b, new Vector3(doorHalf + 0.1f, floor + jamb, walls.Y), Vector3.UnitY * jamb, Vector3.UnitX * 0.11f, Vector3.UnitZ * (r + 0.04f), n.White, back: true);
            Trim(b, new Vector3(0f, doorTop + 0.1f, walls.Y), Vector3.UnitX * (doorHalf + 0.21f), Vector3.UnitY * 0.12f, Vector3.UnitZ * (r + 0.04f), n.White, back: true);
            // The step, a stone a stride from the door, not touching it.
            b.Box(new Vector3(0f, 0.25f, walls.Y + 1.15f), new Vector3(doorHalf + 0.3f, 0.45f, 0.45f), n.Plinth);

            // The gables: boards behind the logs that end under the roof, and the roof.
            n.Red = Shaded(n.Red, 0.92f);
            NordicRoof(b, n, new Vector2(walls.X + 0.15f, walls.Y + 0.15f), top + 0.05f, random);
        }

        // ================================================================ Chapel

        // The village's holy place: a red chapel of boards under a steep roof of tarred shingles, its door in the gable towards the village under a white pediment, tall
        // windows down its sides, and its bells in a tower of their own beside it, shingled and tarred, with an open belfry and a spire (a klockstapel).
        private static void NordicChapel(MeshBuilder b, Vector2 half, Nordic n, WorldRandom random)
        {
            // Built with its ridge along x and its door in the +x gable, then turned so that its door faces the front, with room beside it for its bells.
            const float NaveX = -3.1f, NaveZ = 0.4f;
            b.Place(new Vector3(NaveX, 0f, NaveZ), -MathF.PI * 0.5f);
            Vector2 nave = new Vector2(6.9f, 4f);
            const float P = 0.9f;
            float top = P + 8.4f;
            n.Pitch = random.Range(1.15f, 1.3f);
            b.Box(new Vector3(0f, P - 2.4f, 0f), new Vector3(nave.X + 0.2f, 2.4f, nave.Y + 0.2f), n.Plinth);
            b.Box(new Vector3(0f, (P + top) * 0.5f, 0f), new Vector3(nave.X, (top - P) * 0.5f, nave.Y), n.Red);
            CornerBoards(b, nave, P - 0.04f, top, n.White);
            foreach (float sz in new[] { -1f, 1f })
            {
                Vector3 along = sz > 0f ? Vector3.UnitX : -Vector3.UnitX;
                foreach (float at in EvenSlots(nave.X, 3.6f, 1.4f))
                {
                    NordicWindow(b, n, new Vector3(at, P + 4.1f, sz * nave.Y), along, 0.72f, 2.05f, 4);
                }
            }
            float ridgeTop = NordicRoof(b, n, nave, top, random);

            // The door in the gable, with a pediment over it, and a light high above it.
            b.Place(new Vector3(NaveX, 0f, NaveZ + nave.X), 0f);
            float casing = NordicDoor(b, n, 0f, 0f, 1.5f, P, P + 5.6f, true, false);
            float pedimentBase = casing + 0.05f;
            b.Triangle(new Vector3(-2.2f, pedimentBase, 0.16f), new Vector3(2.2f, pedimentBase, 0.16f), new Vector3(0f, pedimentBase + 1.1f, 0.16f), n.White);
            b.Box(new Vector3(0f, P * 0.5f - 0.6f, 1.0f), new Vector3(2.4f, 0.6f, 0.8f), n.Plinth);
            float apex = top + nave.Y * n.Pitch;
            NordicWindow(b, n, new Vector3(0f, top + (apex - top) * 0.38f, 0f), Vector3.UnitX, 0.62f, 0.62f, 2, shines: false);
            b.ResetPlacement();
            // A spike and a ball at the ridge's end over the door.
            Vector3 finial = new Vector3(NaveX, ridgeTop, NaveZ + nave.X + n.Verge);
            b.Tube(finial, finial + Vector3.UnitY * 2.2f, 0.07f, 0.03f, 5, n.Bronze);
            b.Tube(finial + Vector3.UnitY * 0.7f, finial + Vector3.UnitY * 1.05f, 0.2f, 0.2f, 7, n.Bronze);

            NordicBellTower(b, n, new Vector3(half.X - 2.9f, 0f, -half.Y + 3.1f), random);
        }

        // A bell tower standing on its own: a tapering body of tarred shingles, a red belfry open on every side with a white frame to each opening, a skirt of shingles
        // flaring out under its spire, a spire, and a ball and a spike.
        private static void NordicBellTower(MeshBuilder b, Nordic n, Vector3 foot, WorldRandom random)
        {
            float foot0 = 1.95f, waist = 1.5f, bodyTop = random.Range(8.6f, 9.6f), belfryTop = bodyTop + 2.7f;
            const float Below = 1.5f;
            // The body: four sloping sides, each a quad from the foot's width to the waist's.
            for (int side = 0; side < 4; side++)
            {
                b.Place(foot, side * MathF.PI * 0.5f);
                b.Quad(new Vector3(-foot0, -Below, foot0), new Vector3(foot0, -Below, foot0), new Vector3(waist, bodyTop, waist), new Vector3(-waist, bodyTop, waist), n.Tar);
            }
            b.ResetPlacement();
            b.Box(foot + new Vector3(0f, bodyTop + 0.08f, 0f), new Vector3(waist + 0.2f, 0.1f, waist + 0.2f), n.White);

            // The belfry, red, open on each side onto the dark, each opening framed in white.
            b.Box(foot + new Vector3(0f, (bodyTop + belfryTop) * 0.5f, 0f), new Vector3(waist, (belfryTop - bodyTop) * 0.5f, waist), n.Red);
            CornerBoardsAt(b, foot, new Vector2(waist, waist), bodyTop + 0.15f, belfryTop, n.White);
            for (int side = 0; side < 4; side++)
            {
                b.Place(foot, side * MathF.PI * 0.5f);
                float low = bodyTop + 0.55f, high = belfryTop - 0.45f, open = waist * 0.5f;
                b.Quad(new Vector3(-open, low, waist + 0.02f), new Vector3(open, low, waist + 0.02f), new Vector3(open, high, waist + 0.02f), new Vector3(-open, high, waist + 0.02f), n.Dark);
                Trim(b, new Vector3(-open - 0.1f, (low + high) * 0.5f, waist + 0.05f), Vector3.UnitY * ((high - low) * 0.5f + 0.1f), Vector3.UnitX * 0.1f, Vector3.UnitZ * 0.04f, n.White);
                Trim(b, new Vector3(open + 0.1f, (low + high) * 0.5f, waist + 0.05f), Vector3.UnitY * ((high - low) * 0.5f + 0.1f), Vector3.UnitX * 0.1f, Vector3.UnitZ * 0.04f, n.White);
                Trim(b, new Vector3(0f, high + 0.1f, waist + 0.05f), Vector3.UnitX * (open + 0.2f), Vector3.UnitY * 0.1f, Vector3.UnitZ * 0.04f, n.White);
                Trim(b, new Vector3(0f, low - 0.1f, waist + 0.05f), Vector3.UnitX * (open + 0.2f), Vector3.UnitY * 0.1f, Vector3.UnitZ * 0.04f, n.White);
            }
            b.ResetPlacement();

            // The skirt and the spire.
            float skirt = waist + 0.75f, neck = waist * 0.55f, skirtTop = belfryTop + 1.15f, spireTop = skirtTop + random.Range(5.4f, 6.4f);
            for (int side = 0; side < 4; side++)
            {
                b.Place(foot, side * MathF.PI * 0.5f);
                b.Quad(new Vector3(-skirt, belfryTop, skirt), new Vector3(skirt, belfryTop, skirt), new Vector3(neck, skirtTop, neck), new Vector3(-neck, skirtTop, neck), n.Tar);
                b.Quad(new Vector3(-skirt, belfryTop - 0.18f, skirt), new Vector3(skirt, belfryTop - 0.18f, skirt), new Vector3(skirt, belfryTop, skirt), new Vector3(-skirt, belfryTop, skirt), n.Tar);
                b.Triangle(new Vector3(-neck, skirtTop, neck), new Vector3(neck, skirtTop, neck), new Vector3(0f, spireTop, 0f), n.Tar);
            }
            b.ResetPlacement();
            Vector3 point = foot + Vector3.UnitY * spireTop;
            b.Tube(point - Vector3.UnitY * 0.3f, point + Vector3.UnitY * 1.9f, 0.06f, 0.03f, 5, n.Bronze);
            b.Tube(point + Vector3.UnitY * 0.5f, point + Vector3.UnitY * 0.85f, 0.2f, 0.2f, 7, n.Bronze);
        }

        // CornerBoards about a point other than the origin.
        private static void CornerBoardsAt(MeshBuilder b, Vector3 at, Vector2 half, float bottom, float top, VertexMaterial white)
        {
            Vector3 savedOrigin = b.Origin;
            float savedYaw = b.Yaw;
            b.Place(at, 0f);
            CornerBoards(b, half, bottom, top, white);
            b.Place(savedOrigin, savedYaw);
        }
    }
}
