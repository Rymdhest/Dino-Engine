using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.Modelling.Procedural.Rock
{
    /// <summary>What a piece of rock is, from the smallest that's worth a mesh to one that's part of a mountain. Sizes are at a scale of one.</summary>
    public enum RockForm
    {
        Pebble,     // A hand's width, worn by water.
        Chip,       // The sharp stuff of a scree, half a metre.
        Boulder,    // As tall as a person: what's rolled down and stopped.
        Block,      // A joint block, square-shouldered, that's come away and not gone far.
        Slab,       // A flat sheet of it, tipped over and half buried.
        Outcrop,    // Bedrock breaking through, 5 m and more, as jointed as a cliff.
        Crag,       // A piece of the mountain, 15 m tall.
        Spire,      // A tower left standing when everything around it went, fluted by the rain.
        Hoodoo,     // A column of soft rock under a cap of hard, bulging where its beds are hard and necked where they're soft.
        Tor,        // Rounded blocks piled on each other where the rock round them weathered away.
        Columns,    // A stand of basalt columns, hexagons of every height.
        Fin,        // A thin wall of rock left between two joints, its top ragged.
        Arch,       // A fin worn through near its foot, the sky seen through it.
        Bluff,      // A stretch of cliff: beds stacked in ledges, soft ones worn back under the hard.
    }

    /// <summary>The material stones are made of. Headless, so that the forge can run with no GPU: <see cref="Textures.TextureGenerator"/> fills it in.</summary>
    public static class RockMaterials
    {
        public static int Stone;
    }

    /// <summary>
    /// Makes rock. A rock's shape is a signed distance built from the fractures of the kind of rock it is (<see cref="RockField"/>), which a grid turns into a surface far finer than can be drawn.
    /// Relief goes on along the surface's own gradient, as Paris et al. warp theirs, and then quadric error decimation spends a small budget of triangles on what shows: flat faces go almost for free,
    /// and edges and cracks keep what they need. Normals are split at sharp edges, and ambient occlusion comes straight from the field. Nothing is stored: a model id is a form and a seed.
    /// Safe on any thread. It knows nothing of the game and touches no GPU.
    /// </summary>
    public static class RockForge
    {
        private const float CreaseCosine = 0.72f;   // Faces further apart than this have a hard edge between them: about 44 degrees.
        private const float SmallestPiece = 0.03f;  // Of the whole, for a loose piece to be worth keeping.
        private const float SmallestColumn = 0.003f;    // A stand of columns is dozens of pieces that don't touch, every one of them worth keeping.

        /// <param name="Size">Metres across, about.</param>
        /// <param name="Grid">Cells across the longest side of what's meshed.</param>
        /// <param name="Budget">The most triangles it's allowed.</param>
        /// <param name="Relief">How far its surface wanders, as a share of its size.</param>
        private readonly record struct Shape(float Size, int Grid, int Budget, float Relief)
        {
            // Anything open has to be wider than this: a cell's diagonal, as a joint can lie at any angle to the grid. Narrower, and the two sides of a crack share the grid's vertices and
            // fold together, which nothing can then simplify.
            public float Narrowest => Size * 1.25f / Grid * 1.9f;
        }

        private static Shape ShapeOf(RockForm form) => form switch
        {
            RockForm.Pebble => new Shape(0.42f, 22, 46, 0.02f),
            RockForm.Chip => new Shape(0.8f, 24, 72, 0.012f),
            RockForm.Boulder => new Shape(2.6f, 56, 760, 0.03f),
            RockForm.Block => new Shape(3.2f, 56, 820, 0.022f),
            RockForm.Slab => new Shape(4f, 64, 640, 0.018f),
            RockForm.Outcrop => new Shape(9f, 72, 2000, 0.016f),
            RockForm.Crag => new Shape(22f, 88, 3400, 0.012f),
            RockForm.Spire => new Shape(22f, 88, 2600, 0.01f),
            RockForm.Hoodoo => new Shape(10f, 72, 1500, 0.01f),
            RockForm.Tor => new Shape(7f, 60, 1500, 0.018f),
            RockForm.Columns => new Shape(9f, 72, 2600, 0.004f),
            RockForm.Fin => new Shape(13f, 80, 1800, 0.01f),
            RockForm.Arch => new Shape(19f, 92, 3000, 0.009f),
            _ => new Shape(17f, 92, 3400, 0.01f),
        };

        /// <summary>The most triangles a form is allowed.</summary>
        public static int BudgetOf(RockForm form) => ShapeOf(form).Budget;

        public static FloraMesh Forge(RockForm form, RockKind kind, ulong seed)
        {
            var genes = new Genes(seed);
            Shape shape = ShapeOf(form);
            RockField field = Design(form, kind, genes, shape);
            field.Seed = (uint)seed;

            var points = new List<Vector3>();
            var triangles = new List<int>();
            March(field, shape, points, triangles);
            AddRelief(kind, shape, genes, points, triangles);
            SweepUp(points, triangles, form == RockForm.Columns ? SmallestColumn : SmallestPiece);
            Decimator.Reduce(points, triangles, shape.Budget);
            FloraMesh mesh = Dress(field, shape, points, triangles);
            // Moss stands on stone wherever Rock.glsl paints it (SHELLS.md), but for what's too small to hold a cushion.
            if (form is not (RockForm.Pebble or RockForm.Chip))
            {
                mesh.ShellStyle = Rendering.Shells.ShellStyles.RockMoss;
                // Ground cover tips a stone up to about a quarter turn of a right angle where it's put, which can turn a face a little under level to the sky.
                mesh.ShellFacing = -0.25f;
            }
            return mesh;
        }

        private static RockField Design(RockForm form, RockKind kind, Genes genes, in Shape shape)
        {
            float size = shape.Size;
            var field = new RockField { WarpSize = size * 0.35f, WarpReach = size * 0.012f };
            switch (form)
            {
                case RockForm.Pebble:
                    field.Bodies.Add(Worn(genes, size));
                    break;
                case RockForm.Chip:
                    field.Bodies.Add(Broken(genes, kind, size * new Vector3(0.5f, 0.32f, 0.42f), 7, 0.02f * size));
                    break;
                case RockForm.Boulder:
                    field.Bodies.Add(Broken(genes, kind, size * new Vector3(genes.Range("long", 0.42f, 0.56f), genes.Range("tall", 0.3f, 0.44f), genes.Range("wide", 0.36f, 0.5f)), 12, RoundingOf(kind) * size));
                    Crack(field, genes, shape, 1, 3);
                    break;
                case RockForm.Block:
                    field.Bodies.Add(Broken(genes, kind, size * new Vector3(genes.Range("long", 0.4f, 0.52f), genes.Range("tall", 0.34f, 0.46f), genes.Range("wide", 0.32f, 0.46f)), 6, RoundingOf(kind) * size * 0.45f));
                    Crack(field, genes, shape, 2, 4);
                    break;
                case RockForm.Slab:
                    field.Bodies.Add(Broken(genes, kind, size * new Vector3(genes.Range("long", 0.42f, 0.55f), genes.Range("thick", 0.09f, 0.16f), genes.Range("wide", 0.32f, 0.46f)), 7, RoundingOf(kind) * size * 0.3f));
                    Crack(field, genes, shape, 1, 2);
                    break;
                case RockForm.Outcrop:
                    Mass(field, genes, size, new Vector3(0.5f, 0.42f, 0.42f), genes.Count("masses", 2, 4));
                    Joint(field, kind, genes, shape);
                    break;
                case RockForm.Crag:
                    Mass(field, genes, size, new Vector3(0.34f, 0.62f, 0.34f), genes.Count("masses", 3, 5));
                    Joint(field, kind, genes, shape);
                    break;
                case RockForm.Spire:
                    Fluted(field, genes.Part("tower"), kind, shape);
                    // It's thin: joints that went as deep as a crag's would meet in the middle and leave it in pieces, so its beds only notch it.
                    Notches(field, genes, shape, 3, 6);
                    break;
                case RockForm.Hoodoo:
                    Hoodoo(field, genes, shape);
                    break;
                case RockForm.Tor:
                    Tor(field, genes, shape);
                    break;
                case RockForm.Columns:
                    Columns(field, genes, shape);
                    break;
                case RockForm.Fin:
                    Fin(field, genes, kind, shape, false);
                    break;
                case RockForm.Arch:
                    Fin(field, genes, kind, shape, true);
                    break;
                default:
                    Bluff(field, genes, kind, shape);
                    break;
            }
            return field;
        }

        // A tower, tapering as it climbs, its sides cut by many shallow flutes where rain has run down it for ages, and its top a dome or a flat cap by its rock.
        private static void Fluted(RockField field, Genes genes, RockKind kind, in Shape shape)
        {
            WorldRandom random = genes.Stream("flutes");
            float size = shape.Size;
            float tall = size * genes.Range("tall", 0.62f, 0.8f);
            float half = size * genes.Range("wide", 0.12f, 0.17f);
            var body = new RockBody { Rounding = MathF.Max(size * 0.012f, shape.Narrowest * 0.5f) };
            int flutes = genes.Count("flutes", 11, 15);
            for (int i = 0; i < flutes; i++)
            {
                float turn = (i + random.Range(-0.2f, 0.2f)) * MathF.Tau / flutes;
                float taper = random.Range(0.07f, 0.13f);
                Vector3 n = Vector3.Normalize(new Vector3(MathF.Cos(turn), taper, MathF.Sin(turn)));
                float stretch = 1f + 0.25f * MathF.Cos(turn * 2f + random.Float());
                body.Fractures.Add(new Fracture(n, half * stretch * (i % 2 == 0 ? 1f : random.Range(0.88f, 0.95f))));
            }
            // A dome of limestone or granite, a flat cap of sandstone or basalt, a ragged point of schist.
            float dome = kind is RockKind.Limestone or RockKind.Granite ? 0.42f : kind == RockKind.Schist ? 0.75f : 0.08f;
            for (int i = 0; i < 6; i++)
            {
                float turn = i * MathF.Tau / 6f + random.Float();
                Vector3 n = Vector3.Normalize(new Vector3(MathF.Cos(turn) * dome, 1f, MathF.Sin(turn) * dome));
                body.Fractures.Add(new Fracture(n, tall * n.Y * random.Range(0.96f, 1.02f)));
            }
            body.Fractures.Add(new Fracture(-Vector3.UnitY, size * 0.4f));
            field.Bodies.Add(body);
        }

        // Beds of soft rock stacked under a cap of hard: each bed a short column of its own width, bulging where it's hard and necked where it's soft, and the cap overhanging
        // the neck under it, flat and broken at its edges.
        private static void Hoodoo(RockField field, Genes genes, in Shape shape)
        {
            WorldRandom random = genes.Stream("beds");
            float size = shape.Size;
            float tall = size * genes.Range("tall", 0.6f, 0.78f);
            float radius = size * genes.Range("girth", 0.085f, 0.12f);
            int beds = genes.Count("beds", 5, 8);
            float foot = -size * 0.35f;
            float neck = tall - size * 0.1f;
            field.Blend = MathF.Max(radius * 0.32f, shape.Narrowest);
            float bedHeight = (neck - foot) / beds;
            for (int i = 0; i < beds; i++)
            {
                float t = (i + 0.5f) / beds;
                bool hard = random.Chance(0.5f);
                // Wide at its foot, where the talus props it, narrowest under the cap.
                float r = radius * (1f + 0.55f * (1f - t) * (1f - t)) * (hard ? random.Range(1.05f, 1.2f) : random.Range(0.78f, 0.9f)) * (1f - 0.18f * Smoothstep(0.7f, 1f, t));
                var bed = new RockBody { Rounding = r * 0.35f };
                for (int side = 0; side < 8; side++)
                {
                    float turn = (side + random.Range(-0.25f, 0.25f)) * MathF.Tau / 8f;
                    bed.Fractures.Add(new Fracture(new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)), r * random.Range(0.9f, 1.08f)));
                }
                float bottom = foot + i * bedHeight;
                bed.Fractures.Add(new Fracture(Vector3.UnitY, bottom + bedHeight * 1.15f));
                bed.Fractures.Add(new Fracture(-Vector3.UnitY, -(bottom - bedHeight * 0.15f)));
                field.Bodies.Add(bed);
            }
            // The cap: hard, wide, a little tilted, its edge broken into a few straight faces.
            var cap = new RockBody { Rounding = MathF.Max(size * 0.01f, shape.Narrowest * 0.5f) };
            float capRadius = radius * genes.Range("cap", 1.45f, 1.9f);
            int faces = genes.Count("cap faces", 6, 9);
            for (int side = 0; side < faces; side++)
            {
                float turn = (side + random.Range(-0.3f, 0.3f)) * MathF.Tau / faces;
                cap.Fractures.Add(new Fracture(Vector3.Normalize(new Vector3(MathF.Cos(turn), random.Range(-0.15f, 0.2f), MathF.Sin(turn))), capRadius * random.Range(0.85f, 1.05f)));
            }
            float thick = size * genes.Range("cap thick", 0.06f, 0.1f);
            cap.Fractures.Add(new Fracture(Tilted(Vector3.UnitY, 0.12f, random), neck + thick));
            cap.Fractures.Add(new Fracture(-Vector3.UnitY, -(neck - thick * 0.2f)));
            field.Bodies.Add(cap);
        }

        // Rounded blocks piled up: a big one at the foot and smaller ones on it, each sitting a little off the one under it, turned its own way.
        private static void Tor(RockField field, Genes genes, in Shape shape)
        {
            WorldRandom random = genes.Stream("pile");
            float size = shape.Size;
            int blocks = genes.Count("blocks", 3, 5);
            field.Blend = MathF.Max(size * 0.02f, shape.Narrowest);
            Vector3 at = new Vector3(0f, -size * 0.12f, 0f);
            float scale = 1f;
            for (int i = 0; i < blocks; i++)
            {
                Vector3 half = size * scale * new Vector3(random.Range(0.32f, 0.44f), random.Range(0.14f, 0.2f), random.Range(0.28f, 0.4f));
                var block = new RockBody { Rounding = MathF.Max(half.Y * random.Range(0.55f, 0.85f), shape.Narrowest) };
                float yaw = random.Float() * MathF.Tau;
                Vector3 x = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw));
                Vector3 z = new Vector3(-x.Z, 0f, x.X);
                Vector3 y = Tilted(Vector3.UnitY, 0.12f, random);
                AddFaces(block, x, Vector3.Dot(at, x), half.X, random);
                AddFaces(block, z, Vector3.Dot(at, z), half.Z, random);
                AddFaces(block, y, Vector3.Dot(at, y), half.Y, random);
                field.Bodies.Add(block);
                // The next sits on this one, off its middle, and smaller.
                float drift = random.Range(0.05f, 0.18f) * size * scale;
                float turn = random.Float() * MathF.Tau;
                at += new Vector3(MathF.Cos(turn) * drift, half.Y * random.Range(1.55f, 1.85f), MathF.Sin(turn) * drift);
                scale *= random.Range(0.62f, 0.82f);
            }
            // And one or two lying against the foot of the pile, as some always come down.
            int fallen = genes.Count("fallen", 0, 2);
            for (int i = 0; i < fallen; i++)
            {
                float turn = random.Float() * MathF.Tau;
                Vector3 centre = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)) * size * random.Range(0.36f, 0.48f) + new Vector3(0f, -size * 0.16f, 0f);
                Vector3 half = size * new Vector3(random.Range(0.12f, 0.18f), random.Range(0.08f, 0.12f), random.Range(0.1f, 0.16f));
                var block = new RockBody { Rounding = MathF.Max(half.Y * 0.7f, shape.Narrowest) };
                AddFaces(block, Vector3.UnitX, centre.X, half.X, random);
                AddFaces(block, Vector3.UnitZ, centre.Z, half.Z, random);
                AddFaces(block, Vector3.UnitY, centre.Y, half.Y, random);
                field.Bodies.Add(block);
            }
        }

        // Both faces of a block across one direction, either side of a middle, each a little off true: a plane n.p = d with n near +direction, and its opposite.
        private static void AddFaces(RockBody body, Vector3 direction, float middle, float half, WorldRandom random)
        {
            Vector3 front = Tilted(direction, 0.08f, random);
            Vector3 back = Tilted(-direction, 0.08f, random);
            body.Fractures.Add(new Fracture(front, middle * Vector3.Dot(direction, front) + half * random.Range(0.92f, 1.05f)));
            body.Fractures.Add(new Fracture(back, -middle * Vector3.Dot(direction, -back) + half * random.Range(0.92f, 1.05f)));
        }

        // Columns of every height in a stand, tallest at its middle, and the odd joint across them all where the lava cooled in stages.
        private static void Columns(RockField field, Genes genes, in Shape shape)
        {
            WorldRandom random = genes.Stream("columns");
            float size = shape.Size;
            field.Columns = new ColumnSet
            {
                Width = size * genes.Range("width", 0.11f, 0.15f),
                Gap = shape.Narrowest * 1.15f,
                Reach = size * genes.Range("reach", 0.38f, 0.46f),
                Low = size * genes.Range("low", -0.12f, 0.05f),
                High = size * genes.Range("high", 0.42f, 0.62f),
                Bottom = -size * 0.4f,
                Tilt = genes.Range("tilt", 0.04f, 0.14f),
                Seed = (uint)random.NextULong(),
            };
            int joints = genes.Count("cross joints", 1, 3);
            for (int i = 0; i < joints; i++)
            {
                field.Cracks.Add(new Crack(Tilted(Vector3.UnitY, 0.05f, random), random.Range(-0.05f, 0.4f) * size, shape.Narrowest * 0.6f, size * 0.08f));
            }
        }

        // A wall of rock left between two joints, thin and long, leaning as its beds do, its top ragged where blocks have come off it. Worn through near its foot, it's an arch.
        private static void Fin(RockField field, Genes genes, RockKind kind, in Shape shape, bool arch)
        {
            WorldRandom random = genes.Stream("fin");
            float size = shape.Size;
            Frame(kind, random, out Vector3 along, out Vector3 up, out Vector3 across);
            // A fin stands along its joints: its long way runs along z, and it leans as its beds do.
            Vector3 face = along;
            Vector3 length = across;
            float thick = size * (arch ? genes.Range("thick", 0.1f, 0.14f) : genes.Range("thick", 0.055f, 0.08f));
            float reach = size * (arch ? genes.Range("long", 0.42f, 0.48f) : genes.Range("long", 0.36f, 0.46f));
            float tall = size * (arch ? genes.Range("tall", 0.42f, 0.52f) : genes.Range("tall", 0.42f, 0.58f));
            var body = new RockBody { Rounding = MathF.Max(size * 0.012f, shape.Narrowest * 0.5f) };
            body.Fractures.Add(new Fracture(Tilted(face, 0.04f, random), thick));
            body.Fractures.Add(new Fracture(Tilted(-face, 0.04f, random), thick));
            body.Fractures.Add(new Fracture(Vector3.Normalize(length + up * random.Range(0.1f, 0.6f)), reach));
            body.Fractures.Add(new Fracture(Vector3.Normalize(-length + up * random.Range(0.1f, 0.6f)), reach));
            int tops = genes.Count("tops", 3, 5);
            for (int i = 0; i < tops; i++)
            {
                float t = (i + 0.5f) / tops * 2f - 1f;
                Vector3 n = Vector3.Normalize(up + length * random.Range(-0.7f, 0.7f));
                Vector3 onTop = length * (t * reach) + up * (tall * random.Range(0.82f, 1f));
                body.Fractures.Add(new Fracture(n, Vector3.Dot(onTop, n)));
            }
            body.Fractures.Add(new Fracture(-Vector3.UnitY, size * 0.4f));
            field.Bodies.Add(body);

            if (arch)
            {
                // The opening is wide and low, its top well under the fin's, so the span over it is thick enough to stand. It goes right through, so it's long across the fin.
                float wide = reach * genes.Range("span", 0.48f, 0.62f);
                float high = tall * genes.Range("opening", 0.38f, 0.5f);
                Vector3 centre = length * (reach * random.Range(-0.12f, 0.12f)) + up * (high * 0.35f);
                Vector3 radii = new Vector3(MathF.Abs(face.X) * thick * 3f + MathF.Abs(length.X) * wide, high, MathF.Abs(face.Z) * thick * 3f + MathF.Abs(length.Z) * wide);
                field.Hollows.Add((centre, Vector3.ComponentMax(radii, new Vector3(shape.Narrowest * 2f))));
            }

            field.Joints = new JointLattice
            {
                Along = length,
                Across = face,
                Up = up,
                Seed = (uint)random.NextULong(),
                // An arch is one great slab of rock, worn smooth round its opening: jointed as finely as a fin, it would read as built.
                Spacing = arch ? new Vector3(size * genes.Range("joints", 0.24f, 0.34f), size, size * genes.Range("beds", 0.14f, 0.2f))
                               : new Vector3(size * genes.Range("joints", 0.14f, 0.22f), size, size * genes.Range("beds", 0.09f, 0.15f)),
                Depth = thick * 0.6f,
                Gap = shape.Narrowest * 0.5f,
                Rounding = MathF.Max(size * 0.006f, shape.Narrowest * 0.4f),
                Retreat = thick * (arch ? 0.12f : 0.25f),
                BedRecess = thick * (arch ? 0.16f : 0.3f),
            };
        }

        /// <summary>
        /// The most a form stands above where it's set down, in metres at a scale of one, from the ranges it's designed in. What's set down on rock is held to keep under the line
        /// from the camera to anyone beyond it by this (RockStage), and the world's check measures it the same way.
        /// </summary>
        public static float TopOf(RockForm form) => form switch
        {
            RockForm.Pebble => 0.25f,
            RockForm.Chip => 0.3f,
            RockForm.Boulder => 1.3f,
            RockForm.Block => 1.6f,
            RockForm.Slab => 0.75f,
            RockForm.Outcrop => 4f,
            RockForm.Crag => 14f,
            RockForm.Spire => 17.8f,
            RockForm.Hoodoo => 8.4f,
            RockForm.Tor => 7.8f,
            RockForm.Columns => 6f,
            RockForm.Fin => 7.8f,
            RockForm.Arch => 10.2f,
            _ => 7.4f,
        };

        // A stretch of cliff with its back in the hill: beds stacked in ledges, the soft ones worn back under the hard so that every hard one overhangs a little, cut by joints
        // into blocks that stand out and back, its top worn round. Its face looks along +z.
        private static void Bluff(RockField field, Genes genes, RockKind kind, in Shape shape)
        {
            WorldRandom random = genes.Stream("bluff");
            float size = shape.Size;
            float wide = size * genes.Range("wide", 0.42f, 0.49f);
            float tall = size * genes.Range("tall", 0.28f, 0.4f);
            float deep = size * genes.Range("deep", 0.18f, 0.24f);
            var body = new RockBody { Rounding = MathF.Max(size * 0.02f, shape.Narrowest * 0.5f) };
            body.Fractures.Add(new Fracture(Vector3.Normalize(new Vector3(random.Range(-0.08f, 0.08f), random.Range(0.02f, 0.18f), 1f)), deep));
            body.Fractures.Add(new Fracture(-Vector3.UnitZ, deep * 1.4f));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                body.Fractures.Add(new Fracture(Vector3.Normalize(new Vector3(side, random.Range(0.05f, 0.3f), random.Range(0.1f, 0.5f))), wide));
            }
            int tops = genes.Count("tops", 2, 4);
            for (int i = 0; i < tops; i++)
            {
                Vector3 n = Vector3.Normalize(new Vector3(random.Range(-0.35f, 0.35f), 1f, random.Range(0.1f, 0.6f)));
                Vector3 onTop = new Vector3(random.Range(-0.6f, 0.6f) * wide, tall * random.Range(0.85f, 1f), 0f);
                body.Fractures.Add(new Fracture(n, Vector3.Dot(onTop, n)));
            }
            body.Fractures.Add(new Fracture(-Vector3.UnitY, size * 0.38f));
            field.Bodies.Add(body);

            Frame(kind, random, out _, out Vector3 up, out _);
            float joints = size * 0.2f;
            var lattice = new JointLattice
            {
                Along = Vector3.UnitX,
                Across = Vector3.UnitZ,
                Up = up,
                Seed = (uint)random.NextULong(),
                Depth = deep * 0.7f,
                Gap = shape.Narrowest * 0.5f,
            };
            switch (kind)
            {
                case RockKind.Granite:
                    lattice.Spacing = new Vector3(joints * 1.3f, joints * 2f, joints * 1.1f);
                    lattice.Rounding = joints * 0.22f;
                    lattice.Retreat = joints * 0.3f;
                    lattice.BedRecess = joints * 0.12f;
                    break;
                case RockKind.Schist:
                    // Its beds are the thinnest of any, but even the thinnest of them, at four tenths of a spacing, has to be wider than the grid's diagonal (Shape.Narrowest).
                    lattice.Spacing = new Vector3(joints * 0.9f, joints * 2f, joints * 0.55f);
                    lattice.Rounding = joints * 0.02f;
                    lattice.Gap *= 1.4f;
                    lattice.Retreat = joints * 0.32f;
                    lattice.BedRecess = joints * 0.22f;
                    break;
                case RockKind.Basalt:
                    lattice.Spacing = new Vector3(joints * 0.45f, joints * 0.45f, joints * 1.6f);
                    lattice.Rounding = joints * 0.03f;
                    lattice.Retreat = joints * 0.55f;
                    break;
                default:
                    // Beds in ledges, the soft ones worn well back: every hard bed overhangs the one under it.
                    lattice.Spacing = new Vector3(joints * genes.Range("joint spacing", 1.1f, 1.7f), joints * 2f, joints * genes.Range("bed spacing", 0.38f, 0.6f));
                    lattice.Rounding = joints * (kind == RockKind.Sandstone ? 0.09f : 0.05f);
                    lattice.Retreat = joints * 0.24f;
                    lattice.BedRecess = joints * genes.Range("recess", 0.4f, 0.6f);
                    break;
            }
            lattice.Rounding = MathF.Max(lattice.Rounding, shape.Narrowest * 0.4f);
            field.Joints = lattice;
        }

        private static float RoundingOf(RockKind kind) => kind switch
        {
            RockKind.Granite => 0.16f,
            RockKind.Sandstone => 0.1f,
            RockKind.Limestone => 0.06f,
            RockKind.Schist => 0.03f,
            _ => 0.025f,
        };

        // Nothing but rounded corners: planes all round at about the same distance, which is what water makes of anything in the end.
        private static RockBody Worn(Genes genes, float size)
        {
            var body = new RockBody { Rounding = size * 0.34f };
            Vector3 half = size * 0.5f * new Vector3(1f, genes.Range("flat", 0.45f, 0.72f), genes.Range("wide", 0.68f, 0.95f));
            WorldRandom random = genes.Stream("faces");
            for (int i = 0; i < 12; i++)
            {
                Vector3 n = Direction(random);
                body.Fractures.Add(new Fracture(n, Support(n, half) * random.Range(0.72f, 0.92f)));
            }
            return body;
        }

        // A block with its corners knocked off. Six faces lie along the joints of its kind, more or less, and the rest are what's broken from it since.
        private static RockBody Broken(Genes genes, RockKind kind, Vector3 half, int cuts, float rounding)
        {
            var body = new RockBody { Rounding = rounding };
            WorldRandom random = genes.Stream("fractures");
            Frame(kind, random, out Vector3 along, out Vector3 up, out Vector3 across);

            float ragged = kind == RockKind.Schist ? 0.1f : 0.18f;
            AddPair(body, along, half.X, ragged, random);
            AddPair(body, up, half.Y, ragged, random);
            AddPair(body, across, half.Z, ragged, random);

            // Columns have six sides.
            if (kind == RockKind.Basalt)
            {
                AddPair(body, Vector3.Normalize(along * 0.5f + across * 0.866f), half.X * 0.9f, ragged, random);
                AddPair(body, Vector3.Normalize(along * 0.5f - across * 0.866f), half.X * 0.9f, ragged, random);
            }

            for (int i = 0; i < cuts; i++)
            {
                Vector3 n = Direction(random);
                body.Fractures.Add(new Fracture(n, Support(n, half) * random.Range(0.56f, 0.86f)));
            }
            return body;
        }

        private static void AddPair(RockBody body, Vector3 normal, float reach, float ragged, WorldRandom random)
        {
            body.Fractures.Add(new Fracture(Tilted(normal, ragged, random), reach * random.Range(0.85f, 1.05f)));
            body.Fractures.Add(new Fracture(Tilted(-normal, ragged, random), reach * random.Range(0.85f, 1.05f)));
        }

        // The block of joints a kind of rock breaks into, as seen from itself. A placed stone turns about y, so that's all the world's strike has to agree with.
        private static void Frame(RockKind kind, WorldRandom random, out Vector3 along, out Vector3 up, out Vector3 across)
        {
            float lean = kind switch
            {
                RockKind.Schist => random.Range(0.5f, 0.85f),
                RockKind.Limestone => random.Range(0f, 0.1f),
                RockKind.Sandstone => random.Range(0f, 0.08f),
                _ => random.Range(0f, 0.16f),
            };
            up = Vector3.Normalize(new Vector3(MathF.Sin(lean), MathF.Cos(lean), 0f));
            along = Vector3.Normalize(new Vector3(MathF.Cos(lean), -MathF.Sin(lean), 0f));
            across = Vector3.UnitZ;
        }

        // Tall pieces standing together, each a rough prism, that the joints then cut up.
        private static void Mass(RockField field, Genes genes, float size, Vector3 proportion, int count)
        {
            WorldRandom random = genes.Stream("masses");
            field.Blend = size * 0.06f;
            for (int i = 0; i < count; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float out_ = i == 0 ? 0f : random.Range(0.14f, 0.3f) * size;
                Vector3 centre = new Vector3(MathF.Cos(angle) * out_, 0f, MathF.Sin(angle) * out_);
                float tall = proportion.Y * random.Range(0.55f, 1f) * (i == 0 ? 1f : random.Range(0.5f, 0.92f));
                Vector3 half = size * new Vector3(proportion.X * random.Range(0.65f, 1.05f), tall, proportion.Z * random.Range(0.65f, 1.05f));

                var body = new RockBody { Rounding = size * 0.03f };
                for (int side = 0; side < 7; side++)
                {
                    float turn = (side + random.Float() * 0.6f) * MathF.Tau / 7f;
                    float taper = random.Range(0.1f, 0.34f);
                    Vector3 n = Vector3.Normalize(new Vector3(MathF.Cos(turn), taper, MathF.Sin(turn)));
                    float reach = MathF.Abs(n.X) * half.X + MathF.Abs(n.Z) * half.Z;
                    body.Fractures.Add(new Fracture(n, Vector3.Dot(centre, n) + reach * random.Range(0.72f, 1f)));
                }
                body.Fractures.Add(new Fracture(Tilted(Vector3.UnitY, 0.22f, random), half.Y));
                body.Fractures.Add(new Fracture(-Vector3.UnitY, size * 0.45f));
                field.Bodies.Add(body);
            }
        }

        private static void Joint(RockField field, RockKind kind, Genes genes, in Shape shape)
        {
            WorldRandom random = genes.Stream("joints");
            Frame(kind, random, out Vector3 along, out Vector3 up, out Vector3 across);
            float size = shape.Size;
            float joints = size * (size > 12f ? 0.2f : 0.3f);
            var lattice = new JointLattice { Along = along, Across = across, Up = up, Seed = (uint)random.NextULong(), Depth = joints * 0.9f, Gap = shape.Narrowest * 0.5f };

            switch (kind)
            {
                case RockKind.Granite:
                    lattice.Spacing = new Vector3(joints * 1.3f, joints * 1.2f, joints * 1.1f);
                    lattice.Rounding = joints * 0.2f;
                    lattice.Retreat = joints * 0.32f;
                    break;
                case RockKind.Limestone:
                    lattice.Spacing = new Vector3(joints * 1.5f, joints * 1.2f, joints * 0.42f);
                    lattice.Rounding = joints * 0.05f;
                    lattice.Retreat = joints * 0.22f;
                    lattice.BedRecess = joints * 0.3f;
                    break;
                case RockKind.Sandstone:
                    lattice.Spacing = new Vector3(joints * 1.7f, joints * 1.3f, joints * 0.65f);
                    lattice.Rounding = joints * 0.09f;
                    lattice.Retreat = joints * 0.2f;
                    lattice.BedRecess = joints * 0.34f;
                    break;
                case RockKind.Schist:
                    lattice.Spacing = new Vector3(joints * 1.15f, joints * 0.95f, joints * 0.44f);
                    lattice.Rounding = joints * 0.015f;
                    // Its joints are the furthest of any from lying along the grid.
                    lattice.Gap *= 1.4f;
                    lattice.Retreat = joints * 0.3f;
                    lattice.BedRecess = joints * 0.2f;
                    break;
                default:
                    // Long columns: the beds are the tops of them, and far apart.
                    lattice.Spacing = new Vector3(joints * 0.55f, joints * 0.55f, joints * 2.4f);
                    lattice.Rounding = joints * 0.02f;
                    lattice.Retreat = joints * 0.75f;
                    break;
            }
            // A knife edge is as narrow as a crack, for the same reason.
            lattice.Rounding = MathF.Max(lattice.Rounding, shape.Narrowest * 0.4f);
            field.Joints = lattice;
        }

        // The partings of a thin tower's beds: shallow, near level, and all the way round.
        private static void Notches(RockField field, Genes genes, in Shape shape, int least, int most)
        {
            WorldRandom random = genes.Stream("notches");
            int count = genes.Count("notch count", least, most);
            float size = shape.Size;
            for (int i = 0; i < count; i++)
            {
                field.Cracks.Add(new Crack(Tilted(Vector3.UnitY, 0.06f, random), random.Range(-0.1f, 0.62f) * size, shape.Narrowest * random.Range(0.6f, 0.9f), size * random.Range(0.012f, 0.025f)));
            }
        }

        private static void Crack(RockField field, Genes genes, in Shape shape, int least, int most)
        {
            WorldRandom random = genes.Stream("cracks");
            int count = genes.Count("crack count", least, most);
            float size = shape.Size;
            for (int i = 0; i < count; i++)
            {
                Vector3 n = Direction(random);
                field.Cracks.Add(new Crack(n, random.Range(-0.22f, 0.22f) * size, shape.Narrowest * random.Range(0.55f, 0.9f), random.Range(0.1f, 0.26f) * size));
            }
        }

        private static void March(RockField field, in Shape shape, List<Vector3> points, List<int> triangles)
        {
            // The field is only known to be inside a generous box, so it's found first, coarsely, and the grid is fitted to that.
            float reach = shape.Size * 1.05f;
            Vector3 min = new Vector3(reach), max = new Vector3(-reach);
            const int Survey = 24;
            float step = reach * 2f / Survey;
            for (int z = 0; z <= Survey; z++)
            {
                for (int y = 0; y <= Survey; y++)
                {
                    for (int x = 0; x <= Survey; x++)
                    {
                        Vector3 p = new Vector3(x, y, z) * step - new Vector3(reach);
                        if (field.Distance(p) > step) continue;
                        min = Vector3.ComponentMin(min, p);
                        max = Vector3.ComponentMax(max, p);
                    }
                }
            }
            min -= new Vector3(step);
            max += new Vector3(step);

            Vector3 extent = max - min;
            float cell = MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z)) / shape.Grid;
            int nx = (int)MathF.Ceiling(extent.X / cell) + 2;
            int ny = (int)MathF.Ceiling(extent.Y / cell) + 2;
            int nz = (int)MathF.Ceiling(extent.Z / cell) + 2;

            // A block at a time, as nearly all of the grid is nowhere near the surface.
            const int Block = 6;
            float[] grid = new float[nx * ny * nz];
            float blockRadius = Block * cell * 0.87f;
            for (int bz = 0; bz < nz; bz += Block)
            {
                for (int by = 0; by < ny; by += Block)
                {
                    for (int bx = 0; bx < nx; bx += Block)
                    {
                        Vector3 middle = min + (new Vector3(bx, by, bz) + new Vector3(Block * 0.5f)) * cell;
                        bool clear = field.IsClear(middle, blockRadius, out float value);
                        for (int z = bz; z < Math.Min(bz + Block, nz); z++)
                        {
                            for (int y = by; y < Math.Min(by + Block, ny); y++)
                            {
                                for (int x = bx; x < Math.Min(bx + Block, nx); x++)
                                {
                                    grid[x + nx * (y + ny * z)] = clear ? value : field.Distance(min + new Vector3(x, y, z) * cell);
                                }
                            }
                        }
                    }
                }
            }
            SurfaceNets.Extract(grid, nx, ny, nz, min, cell, points, triangles);
        }

        // Moves the surface along its own gradient by what it's been through, which is the warp of Paris et al.: nothing about it needs a parameterisation, and it can't open a hole.
        private static void AddRelief(RockKind kind, in Shape shape, Genes genes, List<Vector3> points, List<int> triangles)
        {
            uint seed = (uint)genes.Stream("relief").NextULong();
            float feature = shape.Size * 0.3f;
            float reach = shape.Size * shape.Relief;
            float sharp = kind is RockKind.Schist or RockKind.Basalt ? 1f : kind == RockKind.Granite ? 0.4f : 0.7f;

            // The surface is fine enough at this stage that its own faces are the field's gradient.
            var outward = new Vector3[points.Count];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 face = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                outward[a] += face;
                outward[b] += face;
                outward[c] += face;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                Vector3 out_ = outward[i].LengthSquared > 1e-14f ? outward[i].Normalized() : Vector3.Zero;
                Vector3 at = p / feature;

                // Soft swells, and on them a ridged grain that picks out edges the way frost and water do.
                float swell = FloraNoise.Fbm(at, seed, 3) * 2f - 1f;
                float ridge = 1f - MathF.Abs(FloraNoise.Fbm(at * 2.3f + new Vector3(7.1f), seed + 11u, 3) * 2f - 1f) * 2f;
                points[i] = p + out_ * reach * (swell * 0.7f + ridge * 0.5f * sharp);
            }
        }

        // Joints can cut a corner clean off. Nothing holds up a flake like that, and every one of them is triangles a budget can't reach.
        private static void SweepUp(List<Vector3> points, List<int> triangles, float smallest)
        {
            int[] root = new int[points.Count];
            for (int i = 0; i < root.Length; i++) root[i] = i;
            int Find(int i)
            {
                while (root[i] != i)
                {
                    root[i] = root[root[i]];
                    i = root[i];
                }
                return i;
            }

            for (int i = 0; i < triangles.Count; i += 3)
            {
                root[Find(triangles[i + 1])] = Find(triangles[i]);
                root[Find(triangles[i + 2])] = Find(triangles[i]);
            }

            var faces = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int piece = Find(triangles[i]);
                faces[piece] = faces.GetValueOrDefault(piece) + 1;
            }

            int least = (int)(triangles.Count / 3 * smallest);
            var kept = new List<int>(triangles.Count);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                if (faces[Find(triangles[i])] < least) continue;
                kept.Add(triangles[i]);
                kept.Add(triangles[i + 1]);
                kept.Add(triangles[i + 2]);
            }
            triangles.Clear();
            triangles.AddRange(kept);
        }

        // Splits vertices where faces meet at a hard edge, and colours them by how much sky they can see, which is what a crack looks like from any distance.
        private static FloraMesh Dress(RockField field, in Shape shape, List<Vector3> points, List<int> triangles) =>
            Dress(field.Distance, shape.Size * 0.09f, points, triangles, null, null);

        /// <summary>
        /// Normals split at hard edges and a colour from how much sky each point sees, for any surface of a signed distance. A <paramref name="pinned"/> point takes its normal from the
        /// field, as it's shared with another mesh that can't know this one's faces, and <paramref name="tint"/> says how light the rock is where a point is.
        /// </summary>
        internal static FloraMesh Dress(Func<Vector3, float> distance, float reach, List<Vector3> points, List<int> triangles, bool[]? pinned, Func<Vector3, Vector3, float>? tint)
        {
            int faces = triangles.Count / 3;
            var faceNormals = new Vector3[faces];
            var around = new List<int>[points.Count];
            for (int f = 0; f < faces; f++)
            {
                int a = triangles[f * 3], b = triangles[f * 3 + 1], c = triangles[f * 3 + 2];
                Vector3 cross = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                faceNormals[f] = cross;
                (around[a] ??= new List<int>()).Add(f);
                (around[b] ??= new List<int>()).Add(f);
                (around[c] ??= new List<int>()).Add(f);
            }

            var mesh = new FloraMesh();
            var made = new Dictionary<(int point, Vector3 normal), int>();
            // Which point each vertex was made from, for the shells (ShellNormals). A vertex is a point and a normal, so there are never more than three to a face.
            int[] pointOf = System.Buffers.ArrayPool<int>.Shared.Rent(Math.Max(triangles.Count, 1));
            float[] sky = new float[points.Count];
            Array.Fill(sky, -1f);

            Span<int> corners = stackalloc int[3];
            for (int f = 0; f < faces; f++)
            {
                Vector3 own = faceNormals[f].LengthSquared > 1e-14f ? faceNormals[f].Normalized() : Vector3.UnitY;
                for (int i = 0; i < 3; i++)
                {
                    int point = triangles[f * 3 + i];
                    Vector3 sum = Vector3.Zero;
                    foreach (int other in around[point])
                    {
                        Vector3 theirs = faceNormals[other];
                        if (theirs.LengthSquared < 1e-14f) continue;
                        // Weighted by area, as the cross product already is: a sliver has no say in which way a corner faces.
                        if (Vector3.Dot(theirs.Normalized(), own) >= CreaseCosine) sum += theirs;
                    }
                    Vector3 normal = sum.LengthSquared > 1e-14f ? sum.Normalized() : own;
                    if (pinned != null && pinned[point]) normal = Gradient(distance, points[point], reach * 0.25f, normal);

                    if (sky[point] < 0f) sky[point] = SkyAt(distance, points[point], normal, reach) * (tint == null ? 1f : tint(points[point], normal));
                    Vector3 key = new Vector3(MathF.Round(normal.X * 64f), MathF.Round(normal.Y * 64f), MathF.Round(normal.Z * 64f));
                    if (!made.TryGetValue((point, key), out int index))
                    {
                        Vector3 tangent = Vector3.Cross(normal, MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX).Normalized();
                        index = mesh.Add(new FloraVertex
                        {
                            Position = points[point],
                            Normal = normal,
                            Tangent = tangent,
                            Colour = new Vector3(sky[point]),
                            UV = points[point].Xz,
                            Material = RockMaterials.Stone,
                            Joint = -1,
                        });
                        made[(point, key)] = index;
                        pointOf[index] = point;
                    }
                    corners[i] = index;
                }
                mesh.Triangle(corners[0], corners[1], corners[2]);
            }
            ShellNormals(distance, reach, points, pinned, faceNormals, around, mesh, pointOf);
            System.Buffers.ArrayPool<int>.Shared.Return(pointOf);
            return mesh;
        }

        // Which way moss is pushed out of each point (FloraVertex.ShellNormal): every face round it has its say, by area, as one point split into several by a hard
        // edge has to push all of them the same way or the crease opens. It's as long as it needs to be for the face it leans furthest from to keep the full depth, up to
        // twice that. A pinned point is shared with the next tile, which can't see these faces, so it goes by the field, as its normal does. Rented, as a tile of the
        // rock's skin may make nothing for the collector to copy.
        private static void ShellNormals(Func<Vector3, float> distance, float reach, List<Vector3> points, bool[]? pinned, Vector3[] faceNormals, List<int>[] around, FloraMesh mesh, int[] pointOf)
        {
            Vector3[] smooth = System.Buffers.ArrayPool<Vector3>.Shared.Rent(Math.Max(points.Count, 1));
            float[] least = System.Buffers.ArrayPool<float>.Shared.Rent(Math.Max(points.Count, 1));
            Array.Fill(least, 1f, 0, points.Count);
            for (int point = 0; point < points.Count; point++)
            {
                if (around[point] == null) continue;
                Vector3 sum = Vector3.Zero;
                foreach (int face in around[point]) sum += faceNormals[face];
                Vector3 normal = sum.LengthSquared > 1e-14f ? sum.Normalized() : Vector3.UnitY;
                if (pinned != null && pinned[point]) normal = Gradient(distance, points[point], reach * 0.25f, normal);
                smooth[point] = normal;
            }
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                int point = pointOf[v];
                least[point] = MathF.Min(least[point], Vector3.Dot(smooth[point], mesh.Vertices[v].Normal));
            }
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                int point = pointOf[v];
                FloraVertex vertex = mesh.Vertices[v];
                vertex.ShellNormal = smooth[point] / Math.Clamp(least[point], 0.5f, 1f);
                mesh.Vertices[v] = vertex;
            }
            System.Buffers.ArrayPool<Vector3>.Shared.Return(smooth);
            System.Buffers.ArrayPool<float>.Shared.Return(least);
        }

        // How open a point is: the field says how far the nearest rock is, so steps out along the normal that find it nearer than they've come have run into something.
        private static float SkyAt(Func<Vector3, float> distance, Vector3 p, Vector3 normal, float reach)
        {
            float closed = 0f;
            float weight = 0.5f;
            for (int i = 1; i <= 4; i++)
            {
                float out_ = reach * i * 0.25f;
                closed += weight * Saturate((out_ - distance(p + normal * out_)) / out_);
                weight *= 0.62f;
            }
            return Lerp(0.34f, 1f, Saturate(1f - closed * 1.05f));
        }

        // Which way a field rises at a point, or the guess if it's flat there.
        private static Vector3 Gradient(Func<Vector3, float> distance, Vector3 p, float step, Vector3 guess)
        {
            Vector3 g = new Vector3(
                distance(p + Vector3.UnitX * step) - distance(p - Vector3.UnitX * step),
                distance(p + Vector3.UnitY * step) - distance(p - Vector3.UnitY * step),
                distance(p + Vector3.UnitZ * step) - distance(p - Vector3.UnitZ * step));
            return g.LengthSquared > 1e-12f ? g.Normalized() : guess;
        }

        private static Vector3 Direction(WorldRandom random)
        {
            float z = random.Range(-1f, 1f);
            float turn = random.Float() * MathF.Tau;
            float r = MathF.Sqrt(MathF.Max(1f - z * z, 0f));
            return new Vector3(r * MathF.Cos(turn), z, r * MathF.Sin(turn));
        }

        private static Vector3 Tilted(Vector3 normal, float amount, WorldRandom random) => Vector3.Normalize(normal + Direction(random) * amount);

        private static float Support(Vector3 n, Vector3 half) => MathF.Abs(n.X) * half.X + MathF.Abs(n.Y) * half.Y + MathF.Abs(n.Z) * half.Z;
    }
}
