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
        Spire,      // A tower left standing when everything around it went.
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
            _ => new Shape(22f, 80, 2400, 0.012f),
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
            SweepUp(points, triangles);
            Decimator.Reduce(points, triangles, shape.Budget);
            return Dress(field, shape, points, triangles);
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
                default:
                    Mass(field, genes.Part("tower"), size, new Vector3(0.21f, 0.8f, 0.21f), genes.Count("masses", 1, 3));
                    Joint(field, kind, genes, shape);
                    // It's thin: joints that went as deep as a crag's would meet in the middle, and it'd fall down.
                    field.Joints!.Depth *= 0.42f;
                    field.Joints.Retreat *= 0.5f;
                    field.Joints.BedRecess *= 0.6f;
                    break;
            }
            return field;
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
        private static void SweepUp(List<Vector3> points, List<int> triangles)
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

            int least = (int)(triangles.Count / 3 * SmallestPiece);
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
        private static FloraMesh Dress(RockField field, in Shape shape, List<Vector3> points, List<int> triangles)
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
            float[] sky = new float[points.Count];
            Array.Fill(sky, -1f);
            float reach = shape.Size * 0.09f;

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

                    if (sky[point] < 0f) sky[point] = SkyAt(field, points[point], normal, reach);
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
                    }
                    corners[i] = index;
                }
                mesh.Triangle(corners[0], corners[1], corners[2]);
            }
            return mesh;
        }

        // How open a point is: the field says how far the nearest rock is, so steps out along the normal that find it nearer than they've come have run into something.
        private static float SkyAt(RockField field, Vector3 p, Vector3 normal, float reach)
        {
            float closed = 0f;
            float weight = 0.5f;
            for (int i = 1; i <= 4; i++)
            {
                float out_ = reach * i * 0.25f;
                closed += weight * Saturate((out_ - field.Distance(p + normal * out_)) / out_);
                weight *= 0.62f;
            }
            return Lerp(0.34f, 1f, Saturate(1f - closed * 1.05f));
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
