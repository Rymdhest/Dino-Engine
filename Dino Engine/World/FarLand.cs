using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Textures;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The land beyond where the terrain's chunks end, for a view that looks further than the game's camera ever does: the picture behind the main menu, whose hills and mountain are hundreds of
    /// metres off. It's the world's own height map as plain meshes in tiles, its rock and its water and the ground under its woods, each coloured as it looks from far off, as nothing
    /// there is near enough to show more. The chunks cover a square round the camera, and the tiles everything outside it, meeting it at the world's own heights. The woods themselves are
    /// trees (<see cref="Forest"/>): they were a canopy laid over the ground once, a smooth green sheet that began where the real trees stopped, and a person saw where that was. Headless.
    /// </summary>
    public static class FarLand
    {
        public const float Tile = 128f;
        private const float NearCell = 4f;          // Metres between points out to here,
        private const float NearReach = 460f;       // from the camera,
        private const float FarCell = 8f;           // and beyond.
        private const float Sink = 0.15f;           // Under the ground, so that where the chunks meet it they're on top.
        private const float Skirt = 4f;             // Metres every tile's edge hangs down, so that tiles with points at different spacings show no gap.
        private const float ForestSpacing = 8.5f;   // Metres between the places a far wood is thickened from: one of the world's own is 13 m from the next, and seen from far off a wood has no gaps.
        private const float ForestRoom = 3.5f;      // And no nearer a tree that's there already than this, for trees of the size the world grows them.
        private const float ForestFrom = 0.5f;      // How thick the world's woods have to be somewhere for it to be thickened.
        private const float BareRock = 0.2f;        // How much rock there is somewhere, of the world's map of it, before no tree stands there.

        // Trees are what say how big a hill is. This land is 1300 m across and its mountain 130 m high, which with full-grown trees to its top is a wooded knoll, and looked it. So the
        // forest is in forced perspective, as a stage set or a matte painting is: from where the world's own trees stop, its trees are smaller the further off they stand, down to
        // well under half their size at the mountain's foot, and stand closer together by as much. A ridge 600 m off then reads as one a good deal further and higher, and the bare
        // mountain over it as a mountain. Nobody ever walks there to find out.
        private const float SmallerFrom = 360f;     // Metres from the camera at which they begin to be smaller,
        private const float SmallestAt = 820f;      // and at which they're as small as they get,
        private const float Smallest = 0.42f;       // which is this much of their size.

        // Albedos under a sun of about 13, as everything's are.
        private static readonly Vector3 Crowns = new Vector3(0.05f, 0.092f, 0.032f);
        private static readonly Vector3 Water = new Vector3(0.04f, 0.06f, 0.07f);
        // The air between: the further off, the more what's seen takes on the colour of the sky, as the fog hugs the ground and leaves a mountain's flanks clear.
        private static readonly Vector3 Air = new Vector3(0.3f, 0.37f, 0.46f);
        private const float AirDepth = 1500f;       // Metres over which nearly two thirds of what's seen is the air.
        private const float MostAir = 0.55f;

        public readonly record struct Piece(Vector2 Origin, FloraMesh Mesh);

        /// <summary>A tree of the far forest: one of the world's own models, and where it stands.</summary>
        public readonly record struct FarTree(string Model, Vector3 Position, Quaternion Rotation, Vector3 Scale);

        /// <summary>
        /// The trees past where the world's own are streamed, for a view that sees that far: every tree the world has out there, where the world put it, and more between them wherever its
        /// woods are thick, each after one that stands near, so that stands of a kind stay stands. A wood's trees are a dozen metres apart, which is a wood from above and from among
        /// them, and a scatter of trees on a hillside from half a kilometre off, where the eye expects a roof of crowns. Only what the view can hold is kept.
        /// </summary>
        /// <remarks>
        /// None stands on bare rock, whatever the world has growing on its ledges: a mountain's upper slopes are stone, and a tree on its summit is the measure of it.
        /// </remarks>
        /// <param name="streamed">Whether the world's objects are brought in at a place (<c>TerrainChunkSystem</c>), and so aren't this forest's.</param>
        /// <param name="look">The way the view looks, on the ground, and <paramref name="halfAngle"/> how far to either side of that it can see, in radians.</param>
        public static List<FarTree> Forest(WorldData world, Func<Vector2, bool> streamed, Vector2 camera, Vector2 look, float halfAngle)
        {
            WorldObjects objects = world.Objects;
            bool[] tree = objects.AssetIds.Select(IsTree).ToArray();
            float within = MathF.Cos(halfAngle);
            bool Wanted(Vector2 at)
            {
                Vector2 off = at - camera;
                float distance = off.Length;
                return distance > 1f && Vector2.Dot(off / distance, look) > within && !streamed(at) && world.Rock.Sample(at) < BareRock;
            }
            float Size(Vector2 at) => Lerp(1f, Smallest, Smoothstep(SmallerFrom, SmallestAt, (at - camera).Length));

            var forest = new List<FarTree>();
            var own = new Dictionary<Vector2i, List<WorldObject>>();
            foreach (WorldObject item in objects.All())
            {
                if (!tree[item.Asset]) continue;
                Vector2i sector = objects.SectorOf(item.Position.Xz);
                if (!own.TryGetValue(sector, out List<WorldObject>? stand)) own[sector] = stand = new List<WorldObject>();
                stand.Add(item);
                if (Wanted(item.Position.Xz)) forest.Add(new FarTree(objects.AssetIds[item.Asset], item.Position, item.Rotation, item.Scale * Size(item.Position.Xz)));
            }
            if (own.Count == 0) return forest;

            // Every draw is made whether it's used or not, so that what's kept of one view is the same trees as another's. A cell has as many places as trees of the size they are
            // there would need to roof it: one where they're full grown, and half a dozen at the mountain's foot.
            WorldRandom random = new WorldRandom(world.Plan.Seed).Fork("far forest");
            int cells = (int)(world.Size / ForestSpacing);
            int most = (int)MathF.Ceiling(1f / (Smallest * Smallest));
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float smaller = Size((new Vector2(x, z) + new Vector2(0.5f)) * ForestSpacing);
                    int places = Math.Clamp((int)MathF.Round(1f / (smaller * smaller)), 1, most);
                    for (int place = 0; place < most; place++)
                    {
                        Vector2 at = new Vector2(x + random.Float(), z + random.Float()) * ForestSpacing;
                        float chance = random.Float();
                        float size = random.Float();
                        float turn = random.Float();
                        if (place >= places || !Wanted(at)) continue;

                        float canopy = world.Canopy.Sample(at);
                        if (canopy < ForestFrom || chance > canopy) continue;
                        if (world.WaterDepthAt(at) > 0f || world.ShoreHeightAt(at) < 0.55f || world.Height.SlopeAt(at) > 0.8f) continue;

                        // The nearest of the world's own says what grows here, and whether there's room.
                        Vector2i sector = objects.SectorOf(at);
                        WorldObject? like = null;
                        float nearest = float.MaxValue;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (!own.TryGetValue(new Vector2i(sector.X + dx, sector.Y + dz), out List<WorldObject>? stand)) continue;
                                foreach (WorldObject item in stand)
                                {
                                    float distance = (item.Position.Xz - at).LengthSquared;
                                    if (distance >= nearest) continue;
                                    nearest = distance;
                                    like = item;
                                }
                            }
                        }
                        float room = ForestRoom * smaller;
                        if (like is not WorldObject kind || nearest < room * room) continue;

                        float scale = kind.Scale.X * Lerp(0.8f, 1.1f, size) * Size(at);
                        var position = new Vector3(at.X, world.Height.Sample(at) - 0.15f * scale, at.Y);
                        forest.Add(new FarTree(objects.AssetIds[kind.Asset], position, Quaternion.FromAxisAngle(Vector3.UnitY, turn * MathF.Tau), new Vector3(scale)));
                    }
                }
            }
            return forest;
        }

        /// <summary>Whether a model is a tree's, which is all the far forest is made of.</summary>
        public static bool IsTree(string model) =>
            FloraCatalog.TryGet(Content.NatureCatalog.SpeciesOf(model, out _), out PlantSpecies species) && species.Asset.Kind == Content.WorldAssetKind.Tree;

        /// <param name="holeMin">The corner of the square the chunks cover, nearest the world's origin.</param>
        /// <param name="holeMax">Its far corner.</param>
        public static List<Piece> Build(WorldData world, Vector2 holeMin, Vector2 holeMax, Vector2 camera)
        {
            var noise = new WorldNoise(new WorldRandom(world.Plan.Seed).Fork("far land"));
            Vector3 grass = world.Plan.Biomes.Count > 0 ? world.Plan.Biomes[0].GrassColour : new Vector3(0.15f, 0.27f, 0.06f);
            Vector3 rock = world.Geology.Colour;
            int material = MaterialCatalog.Flat.Index;

            int tiles = (int)MathF.Ceiling(world.Size / Tile);
            var pieces = new Piece[tiles * tiles];
            Parallel.For(0, tiles * tiles, index =>
            {
                var origin = new Vector2(index % tiles, index / tiles) * Tile;
                var centre = origin + new Vector2(Tile * 0.5f);
                float cell = (centre - camera).Length < NearReach ? NearCell : FarCell;
                FloraMesh? mesh = BuildTile(world, noise, origin, cell, holeMin, holeMax, camera, grass, rock, material);
                if (mesh != null) pieces[index] = new Piece(origin, mesh);
            });
            return pieces.Where(piece => piece.Mesh != null).ToList();
        }

        private static FloraMesh? BuildTile(WorldData world, WorldNoise noise, Vector2 origin, float cell, Vector2 holeMin, Vector2 holeMax, Vector2 camera, Vector3 grass, Vector3 rock, int material)
        {
            int n = (int)(Tile / cell);
            bool InHole(int x, int z)
            {
                Vector2 middle = origin + new Vector2(x + 0.5f, z + 0.5f) * cell;
                return middle.X > holeMin.X && middle.X < holeMax.X && middle.Y > holeMin.Y && middle.Y < holeMax.Y;
            }

            bool any = false;
            for (int z = 0; z < n && !any; z++)
                for (int x = 0; x < n && !any; x++)
                    any = !InHole(x, z) && world.InBounds(origin + new Vector2(x + 0.5f, z + 0.5f) * cell);
            if (!any) return null;

            var mesh = new FloraMesh();
            int[] made = new int[(n + 1) * (n + 1)];
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    Vector2 at = origin + new Vector2(x, z) * cell;
                    made[z * (n + 1) + x] = mesh.Add(Point(world, noise, at, cell, holeMin, holeMax, camera, grass, rock, material, 0f));
                }
            }
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (InHole(x, z) || !world.InBounds(origin + new Vector2(x + 0.5f, z + 0.5f) * cell)) continue;
                    int a = made[z * (n + 1) + x], b = made[z * (n + 1) + x + 1], c = made[(z + 1) * (n + 1) + x + 1], d = made[(z + 1) * (n + 1) + x];
                    mesh.Quad(a, d, c, b);
                }
            }

            // Along the tile's edges, a skirt hanging down, but for where the chunks are, as anything there would be carved out of their grass.
            void Edge(Vector2 from, Vector2 step, int count)
            {
                Vector2 inward = new Vector2(step.Y, step.X) * (from.X == origin.X && from.Y == origin.Y ? 0.5f : -0.5f);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = from + step * i, q = from + step * (i + 1);
                    Vector2 middle = (p + q) * 0.5f + inward;
                    if (middle.X > holeMin.X && middle.X < holeMax.X && middle.Y > holeMin.Y && middle.Y < holeMax.Y) continue;
                    int top0 = mesh.Add(Point(world, noise, p, cell, holeMin, holeMax, camera, grass, rock, material, 0f));
                    int top1 = mesh.Add(Point(world, noise, q, cell, holeMin, holeMax, camera, grass, rock, material, 0f));
                    int low0 = mesh.Add(Point(world, noise, p, cell, holeMin, holeMax, camera, grass, rock, material, Skirt));
                    int low1 = mesh.Add(Point(world, noise, q, cell, holeMin, holeMax, camera, grass, rock, material, Skirt));
                    mesh.Quad(top0, low0, low1, top1);
                    mesh.Quad(top0, top1, low1, low0);
                }
            }
            Edge(origin, new Vector2(cell, 0f), n);
            Edge(origin + new Vector2(0f, Tile), new Vector2(cell, 0f), n);
            Edge(origin, new Vector2(0f, cell), n);
            Edge(origin + new Vector2(Tile, 0f), new Vector2(0f, cell), n);
            return mesh;
        }

        private static FloraVertex Point(WorldData world, WorldNoise noise, Vector2 at, float cell, Vector2 holeMin, Vector2 holeMax, Vector2 camera, Vector3 grass, Vector3 rock, int material, float down)
        {
            float Surface(Vector2 p) => SurfaceAt(world, noise, p, holeMin, holeMax, out _, out _, out _);

            float height = SurfaceAt(world, noise, at, holeMin, holeMax, out float woods, out float bare, out float wet);
            float dx = Surface(at + new Vector2(cell, 0f)) - Surface(at - new Vector2(cell, 0f));
            float dz = Surface(at + new Vector2(0f, cell)) - Surface(at - new Vector2(0f, cell));
            Vector3 normal = Vector3.Normalize(new Vector3(-dx, 2f * cell, -dz));

            // Crowns vary, lit and dark, and the ground between them is in their shade.
            float crown = noise.Fbm01(at.X, at.Y, 11f, 2);
            Vector3 trees = Crowns * Lerp(0.7f, 1.3f, crown);
            Vector3 colour = Vector3.Lerp(grass * Lerp(0.85f, 1.1f, noise.Fbm01(at.X + 90f, at.Y, 40f, 2)), trees, woods);
            // Rock is darker on its faces than its ledges, and what's level enough holds scrub.
            Vector3 stone = rock * Lerp(0.8f, 1.1f, noise.Fbm01(at.X, at.Y - 70f, 30f, 2)) * Lerp(0.65f, 1.1f, Smoothstep(0.45f, 0.9f, normal.Y));
            stone = Vector3.Lerp(stone, Crowns * 1.3f, Smoothstep(0.82f, 0.95f, normal.Y) * Smoothstep(0.3f, 0.7f, noise.Fbm01(at.X - 30f, at.Y, 24f, 3)) * 0.8f);
            colour = Vector3.Lerp(colour, stone, bare);
            colour = Vector3.Lerp(colour, Water, wet);
            colour = Vector3.Lerp(colour, Air, MostAir * (1f - MathF.Exp(-(at - camera).Length / AirDepth)));

            Vector3 tangent = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitZ));
            return new FloraVertex
            {
                Position = new Vector3(at.X, height - down, at.Y),
                Normal = normal,
                Tangent = tangent,
                Colour = colour,
                UV = at,
                Material = material,
                Joint = -1,
            };
        }

        // The ground, or the water lying on it. Under a wood it's the wood's floor, in the crowns' shade and their colour: the trees stand on it (Forest).
        private static float SurfaceAt(WorldData world, WorldNoise noise, Vector2 at, Vector2 holeMin, Vector2 holeMax, out float woods, out float bare, out float wet)
        {
            Vector2 p = Vector2.Clamp(at, Vector2.Zero, new Vector2(world.Size));
            float ground = world.Height.Sample(p) - Sink;
            float depth = world.WaterDepthAt(p);
            wet = Smoothstep(0.05f, 0.4f, depth);
            bare = Smoothstep(0.35f, 0.65f, world.Rock.Sample(p));
            woods = Saturate(world.Canopy.Sample(p) * 1.4f) * (1f - bare) * (1f - wet);
            return MathF.Max(ground + depth, ground);
        }

    }
}
