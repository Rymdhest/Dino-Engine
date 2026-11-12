using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Textures;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The land beyond where the terrain's chunks end, for a view that looks further than the game's camera ever does: the picture behind the main menu, whose hills and mountain are hundreds of
    /// metres off. It's the world's own height map as plain meshes in tiles, its woods a canopy laid over the ground with crowns in it, its rock and its water, each coloured as it looks from
    /// far off, as nothing there is near enough to show more. The chunks cover a square round the camera, and the tiles everything outside it, meeting it at the world's own heights. Headless.
    /// </summary>
    public static class FarLand
    {
        public const float Tile = 128f;
        private const float NearCell = 4f;          // Metres between points out to here,
        private const float NearReach = 460f;       // from the camera,
        private const float FarCell = 8f;           // and beyond.
        private const float Sink = 0.15f;           // Under the ground, so that where the chunks meet it they're on top.
        private const float CanopyTop = 14f;        // Metres a wood's crowns stand over the ground.
        private const float CanopyRamp = 30f;       // Metres out from the chunks over which the canopy rises to that, as the real trees stand on there.
        private const float Skirt = 4f;             // Metres every tile's edge hangs down, so that tiles with points at different spacings show no gap.

        // Albedos under a sun of about 13, as everything's are.
        private static readonly Vector3 Crowns = new Vector3(0.05f, 0.092f, 0.032f);
        private static readonly Vector3 Water = new Vector3(0.04f, 0.06f, 0.07f);
        // The air between: the further off, the more what's seen takes on the colour of the sky, as the fog hugs the ground and leaves a mountain's flanks clear.
        private static readonly Vector3 Air = new Vector3(0.3f, 0.37f, 0.46f);
        private const float AirDepth = 1500f;       // Metres over which nearly two thirds of what's seen is the air.
        private const float MostAir = 0.55f;

        public readonly record struct Piece(Vector2 Origin, FloraMesh Mesh);

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

        private static float SurfaceAt(WorldData world, WorldNoise noise, Vector2 at, Vector2 holeMin, Vector2 holeMax, out float woods, out float bare, out float wet)
        {
            Vector2 p = Vector2.Clamp(at, Vector2.Zero, new Vector2(world.Size));
            float ground = world.Height.Sample(p) - Sink;
            float depth = world.WaterDepthAt(p);
            wet = Smoothstep(0.05f, 0.4f, depth);
            bare = Smoothstep(0.35f, 0.65f, world.Rock.Sample(p));

            // How far outside the chunks' square, for the canopy to rise from where the real trees stand.
            Vector2 outside = Vector2.ComponentMax(holeMin - at, at - holeMax);
            float beyond = MathF.Max(outside.X, outside.Y);
            float rise = Smoothstep(0f, CanopyRamp, beyond);
            woods = Saturate(world.Canopy.Sample(p) * 1.4f) * (1f - bare) * (1f - wet);
            float crowns = woods * CanopyTop * Lerp(0.7f, 1.15f, noise.Fbm01(at.X, at.Y, 11f, 2)) * rise;
            return MathF.Max(ground + depth, ground) + crowns;
        }

    }
}
