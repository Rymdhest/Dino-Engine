using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Looks at the shape of the land as a whole, with no window: how much of it can be walked, how the rest stands up, and where to go and see it.
    ///   relief [--seed N] [--count N] [--archetype id]                     What the land is made of, world by world.
    ///   view   --at x,z [--seed N] [--archetype id] [--span m] [--angle degrees] [--yaw degrees] [--out f]   A drawing of a place, from the game's own direction.
    /// </summary>
    internal static class ReliefCheck
    {
        public static int Survey(int firstSeed, int count, string? archetype)
        {
            Console.WriteLine($"{"world",-26} {"rock",-10} {"size",5} {"top",5} {"walk%",6} {"wall%",6} {"rim%",5} {"p50",5} {"p90",5} {"p99",5}  spots");
            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                string id = archetype ?? ArchetypeCatalog.All[i % ArchetypeCatalog.All.Count].Id;
                WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, id));

                int land = 0, walk = 0, wall = 0, rim = 0;
                var steep = new List<float>();
                float top = float.NegativeInfinity;
                const int Step = 2;
                for (float z = 0f; z < world.Size; z += Step)
                {
                    for (float x = 0f; x < world.Size; x += Step)
                    {
                        Vector2 at = new Vector2(x, z);
                        float h = world.Height.Sample(at);
                        top = MathF.Max(top, h);
                        if (h <= WorldPlan.SeaLevel) continue;
                        land++;
                        if (world.Walkable.IsSet(at))
                        {
                            walk++;
                            continue;
                        }
                        if (world.Boundary.Sample(at) >= 0.6f) rim++;
                        else wall++;
                        steep.Add(world.Height.SlopeAt(at));
                    }
                }

                steep.Sort();
                float Percentile(float p) => steep.Count == 0 ? 0f : steep[(int)((steep.Count - 1) * p)];
                string spots = string.Join(" ", FindSpots(world, 3).Select(s => $"{s.X:0},{s.Y:0}"));
                if (TryFindCliff(world, out Vector2 cliff)) spots += $"  cliff {cliff.X:0},{cliff.Y:0}";
                float share = 100f / MathF.Max(land, 1);
                Console.WriteLine($"{$"{seed} {id}",-26} {world.Geology.Kind,-10} {world.Size,5:0} {top,5:0} {walk * share,6:0.0} {wall * share,6:0.0} {rim * share,5:0.0} {Percentile(0.5f),5:0.00} {Percentile(0.9f),5:0.00} {Percentile(0.99f),5:0.00}  {spots}");
            }
            return 0;
        }

        // Somewhere to stand with a sea cliff just ahead, for rock that's no part of a mountain.
        private static bool TryFindCliff(WorldData world, out Vector2 spot)
        {
            spot = default;
            for (float z = 16f; z < world.Size - 16f; z += 16f)
            {
                for (float x = 16f; x < world.Size - 16f; x += 16f)
                {
                    Vector2 ahead = new Vector2(x, z + 20f);
                    if (!world.Walkable.IsSet(new Vector2(x, z)) || world.Rock.Sample(ahead) < 0.6f || world.Massif.Sample(ahead) > 0.05f || world.Inland.Sample(ahead) > 24f) continue;
                    spot = new Vector2(x, z);
                    return true;
                }
            }
            return false;
        }

        // Places a character can stand with the most rock rising in front of the camera, which looks along +z, far enough apart to be different places.
        private static List<Vector2> FindSpots(WorldData world, int count)
        {
            var found = new List<(Vector2 at, float rise)>();
            const float Grid = 16f;
            for (float z = Grid; z < world.Size - Grid; z += Grid)
            {
                for (float x = Grid; x < world.Size - Grid; x += Grid)
                {
                    Vector2 at = new Vector2(x, z);
                    if (!world.Walkable.IsSet(at)) continue;
                    float here = world.Height.Sample(at);
                    float rise = 0f;
                    for (float ahead = 12f; ahead <= 30f; ahead += 6f)
                    {
                        Vector2 out_ = at + new Vector2(0f, ahead);
                        if (world.InBounds(out_) && !world.Walkable.IsSet(out_)) rise = MathF.Max(rise, world.Height.Sample(out_) - here);
                    }
                    if (rise > 2f) found.Add((at, rise));
                }
            }

            found.Sort((a, b) => b.rise.CompareTo(a.rise));
            var spots = new List<Vector2>();
            foreach ((Vector2 at, _) in found)
            {
                if (spots.Any(s => (s - at).Length < 200f)) continue;
                spots.Add(at);
                if (spots.Count == count) break;
            }
            return spots;
        }

        /// <summary>Says why a point can or can't be walked on.</summary>
        public static int Probe(int seed, string? archetype, Vector2 at)
        {
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            for (int dz = -8; dz <= 8; dz += 2)
            {
                var line = new System.Text.StringBuilder();
                for (int dx = -20; dx <= 20; dx += 2) line.Append(world.Walkable.IsSet(at + new Vector2(dx, dz)) ? '.' : '#');
                Console.WriteLine(line);
            }
            Console.WriteLine($"height {world.Height.Sample(at):0.00} slope {world.Height.SlopeAt(at):0.00} water {world.WaterDepthAt(at):0.00} boundary {world.Boundary.Sample(at):0.00} rock {world.Rock.Sample(at):0.00} reachable {world.Reachable.Sample(at):0.00} ponds {world.Ponds.Count}");

            // What's near, as a fault at a spot is usually something else's doing.
            foreach (WorldSite site in world.Sites.Where(site => (site.Position - at).Length < 200f)) Console.WriteLine($"  {site.Kind} {site.Name} at {site.Position}, radius {site.Radius:0}, {(site.Position - at).Length:0} m away");
            foreach (WorldRoad road in world.Roads)
            {
                int nearest = Enumerable.Range(0, road.Points.Count).MinBy(i => (road.Points[i].Xz - at).LengthSquared);
                float distance = (road.Points[nearest].Xz - at).Length;
                if (distance < 40f) Console.WriteLine($"  {road.Class} road of {road.Length:0} m passes {distance:0.0} m away, at point {nearest} of {road.Points.Count}, planned {road.Points[nearest].Y:0.00} where the ground is {world.Height.Sample(road.Points[nearest].Xz):0.00}");
            }
            return 0;
        }

        /// <summary>A drawing of the ground around a point, looking towards +z from above as the game does, and shaded by a low sun.</summary>
        public static int View(int seed, string? archetype, Vector2 at, float span, float angle, float yaw, int width, int height, string path, bool plain)
        {
            WorldData world = new WorldGenerator().Generate(new WorldRequest(seed, archetype));
            byte[] pixels = Draw(world, at, span, angle, yaw, width, height, plain);
            PngWriter.Write(path, pixels, width, height);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        private static byte[] Draw(WorldData world, Vector2 at, float span, float angle, float yaw, int width, int height, bool plain)
        {
            ScalarField ground = world.Height;
            byte[] pixels = new byte[width * height * 4];

            float pitch = angle * MathF.PI / 180f;
            float turn = yaw * MathF.PI / 180f;
            Vector3 forward = new Vector3(MathF.Sin(turn) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Cos(turn) * MathF.Cos(pitch));
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            Vector3 up = Vector3.Cross(right, forward);
            Vector3 centre = new Vector3(at.X, ground.Sample(at), at.Y);
            float metresPerPixel = span / width;
            Vector3 sun = Vector3.Normalize(new Vector3(-0.5f, 0.72f, -0.42f));
            float reach = span * 3f + 400f;

            Parallel.For(0, height, row =>
            {
                for (int column = 0; column < width; column++)
                {
                    Vector3 origin = centre + right * ((column - width * 0.5f) * metresPerPixel) + up * ((height * 0.5f - row) * metresPerPixel) - forward * reach * 0.5f;
                    float t = March(ground, origin, forward, reach, 0.5f);
                    if (t >= 0f && !world.InBounds((origin + forward * t).Xz)) t = -1f;

                    Vector3 colour = new Vector3(0.62f, 0.74f, 0.88f);
                    if (t >= 0f)
                    {
                        Vector3 hit = origin + forward * t;
                        Vector2 flat = hit.Xz;
                        float dx = (ground.SampleCubic(flat.X + 0.5f, flat.Y) - ground.SampleCubic(flat.X - 0.5f, flat.Y));
                        float dz = (ground.SampleCubic(flat.X, flat.Y + 0.5f) - ground.SampleCubic(flat.X, flat.Y - 0.5f));
                        Vector3 normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));

                        float lit = MathF.Max(Vector3.Dot(normal, sun), 0f);
                        if (lit > 0f && March(ground, hit + normal * 0.6f, sun, 260f, 1f) >= 0f) lit = 0f;

                        float slope = MathF.Sqrt(dx * dx + dz * dz);
                        Vector3 grass = new Vector3(0.36f, 0.45f, 0.24f);
                        Vector4 tone = world.Geology.ToneAt(world.Geology.LayerAt(flat.X, hit.Y, flat.Y));
                        Vector3 stone = new Vector3(tone.X, tone.Y, tone.Z) * 1.7f;
                        colour = Vector3.Lerp(grass, stone, WorldMath.Smoothstep(0.55f, 0.95f, slope));
                        colour = Vector3.Lerp(colour, new Vector3(0.55f, 0.52f, 0.48f) * tone.W, world.Scree.Sample(flat) * 0.5f * (1f - WorldMath.Smoothstep(0.9f, 1.6f, slope)));
                        if (plain) colour = new Vector3(0.62f);
                        if (hit.Y < WorldPlan.SeaLevel) colour = new Vector3(0.2f, 0.36f, 0.45f);
                        else if (world.Walkable.IsSet(flat)) colour *= new Vector3(1f, 1.08f, 0.95f);
                        colour *= 0.3f + 0.95f * lit + 0.15f * normal.Y;
                    }

                    int p = (row * width + column) * 4;
                    pixels[p] = (byte)(Math.Clamp(colour.X, 0f, 1f) * 255f);
                    pixels[p + 1] = (byte)(Math.Clamp(colour.Y, 0f, 1f) * 255f);
                    pixels[p + 2] = (byte)(Math.Clamp(colour.Z, 0f, 1f) * 255f);
                    pixels[p + 3] = 255;
                }
            });
            return pixels;
        }

        // How far along a ray the ground is, or -1 if it isn't.
        private static float March(ScalarField ground, Vector3 origin, Vector3 direction, float reach, float step)
        {
            float last = 0f;
            for (float t = 0f; t < reach; t += step)
            {
                Vector3 p = origin + direction * t;
                if (p.Y >= ground.Sample(p.X, p.Z))
                {
                    last = t;
                    continue;
                }

                // Halved a few times, or the steps show as bands on a steep face.
                float near = last, far = t;
                for (int i = 0; i < 6; i++)
                {
                    float middle = (near + far) * 0.5f;
                    Vector3 m = origin + direction * middle;
                    if (m.Y >= ground.Sample(m.X, m.Z)) near = middle;
                    else far = middle;
                }
                return near;
            }
            return -1f;
        }
    }
}
