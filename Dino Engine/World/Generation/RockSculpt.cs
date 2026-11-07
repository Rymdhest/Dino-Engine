using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Turns the shape of the mountains into rock, a metre at a time, and only where nobody goes: everything here is measured out from the land a character can reach, so
    /// that land stays exactly as it was, and what looks like the edge of the world really is a wall. In order: a wall where the rock begins, with the scree that's fallen from it,
    /// gullies down every slope, hard beds standing out of soft ones, and then the whole of it broken along the world's joints.
    /// Every point is worked out alone, so this runs in parallel and in any order. Safe from many threads.
    /// </summary>
    public sealed class RockSculpt
    {
        /// <summary>The steepest a face can fall towards +z with someone beyond it. The camera looks along +z from a fixed direction at 2.32, so this keeps rock off the line to the hero and well under the camera.</summary>
        public const float SightSlope = 1.6f;

        private const float Margin = 2f;            // Metres left between the last reachable ground and where rock can begin.
        private const float SightMargin = 0.3f;
        private const byte StandableRock = 128;     // The most rock there can be under someone, as FinalizeStage has it.

        private readonly WorldData _world;
        private readonly Geology _rock;
        private readonly ScalarField _elevation;
        private readonly ScalarField _massif;
        private readonly ScalarField _steep;
        private readonly ScalarField _crests;
        private readonly ScalarField _reach;
        private readonly WorldNoise _noise;
        private readonly uint _seed;
        private readonly ErosionFilter.Style _style;
        private readonly Vector2 _along;
        private readonly Vector2 _across;

        public RockSculpt(WorldData world, WorldRandom random)
        {
            _world = world;
            _rock = world.Geology;
            _elevation = world.Elevation;
            // Nearly all or nothing at the coarse map's cells, which a metre at a time shows as a string of beads along every edge.
            _massif = world.Massif.Copy();
            _massif.Blur(2, 2);

            // Rock shows wherever the land is too steep to hold anything, mountain or not: a sea cliff is the same rock.
            ScalarField elevation = world.Elevation;
            _steep = ScalarField.ForWorld(world.Size, WorldData.CoarseCell);
            Parallel.For(0, _steep.Depth, z =>
            {
                for (int x = 0; x < _steep.Width; x++) _steep[x, z] = Smoothstep(0.5f, 0.75f, elevation.SlopeAt(x, z));
            });
            _steep.Blur(1, 2);

            _crests = world.Crests;
            _noise = new WorldNoise(random.Fork("rock"));
            _seed = (uint)random.Fork("gullies").NextULong();
            _along = new Vector2(MathF.Cos(_rock.Strike), MathF.Sin(_rock.Strike));
            _across = new Vector2(-_along.Y, _along.X);

            float[] reachable = world.Reachable.Data;
            ScalarField reach = ScalarField.DistanceTo(world.Reachable.Width, world.Reachable.Depth, WorldData.CoarseCell, i => reachable[i] > 0.5f);
            // The map's cells would show as steps along the foot of a wall. Blurring eats into the land by a little, which Margin allows for.
            reach.Blur(1, 2);
            _reach = reach;

            _style = new ErosionFilter.Style
            {
                Scale = 64f,
                Strength = 0.16f * _rock.Gullies,
                GullyWeight = 0.62f,
                Detail = 1.1f,
                RidgeRounding = 0.08f + 0.3f * _rock.Rounding,
                CreaseRounding = 0.02f,
                InputRounding = 0.12f,
                OctaveRounding = 2f,
                InputOnset = 1.25f,
                OctaveOnset = 1.25f,
                RidgeInputOnset = 2.8f,
                RidgeOctaveOnset = 1.5f,
                AssumedSlope = 0.7f,
                AssumedShare = 1f,
                CellScale = 0.7f,
                Normalization = 0.5f,
                Octaves = 4,
                Lacunarity = 2f,
                Gain = 0.5f,
            };
        }

        /// <summary>What a point of the height map becomes. <paramref name="height"/> is what it was, and what comes back if it's ground anybody uses.</summary>
        public float Apply(int nodeX, int nodeZ, float x, float z, float height)
        {
            float massif = _massif.Sample(x, z);
            // Bare rock runs down to the shore, but what's under the waves is the sea's: nothing here may move the coast.
            float steep = _steep.Sample(x, z) * Smoothstep(0.6f, 4f, height);
            if (massif <= 0.05f && steep <= 0.05f) return height;

            float reach = _reach.Sample(x, z) - Margin;
            if (reach <= 0f) return height;

            // The foot of a wall wanders in and out, however straight the map's edge happens to be, but never onto the land.
            float wander = _noise.Fbm01(x, z, 31f, 3);
            float inside = reach - wander * 5f;
            if (inside <= 0f) return height;

            float standing = Smoothstep(0.1f, 0.5f, massif);
            float rock = Smoothstep(0f, 4.5f, inside) * MathF.Max(standing, Smoothstep(0.1f, 0.5f, steep));
            _world.Rock.Raise(nodeX, nodeZ, rock);

            // Domain warping keeps the grid of the coarse map from showing through in straight runs.
            Vector2 warp = _noise.Warp(x, z, 19f, 3.2f, 2);
            float wx = x + warp.X;
            float wz = z + warp.Y;
            float h = Lerp(height, _elevation.SampleCubic(wx, wz), rock);

            h += Wall(x, z, inside, out float skirt) * standing;

            // Rock isn't the same all the way across: here it's broken up, there it's one great slab.
            float character = Smoothstep(0.3f, 0.7f, _noise.Fbm01(x - 450f, z + 260f, 58f, 3));
            h = Gullies(wx, wz, h, rock * (0.35f + 0.65f * character), out float crease);
            h = Bedding(x, z, wx, wz, h, rock * (0.45f + 0.55f * (1f - character)));

            float scree = MathF.Max(crease, skirt);
            if (scree > 0f) _world.Scree.Raise(nodeX, nodeZ, scree * standing);
            return h;
        }

        /// <summary>
        /// Breaks the sculpted rock along its joints, once every point of it is known. Nothing about rock is smooth at this scale: a slope is made of flat faces that meet at
        /// edges, and those faces lie along a few planes, over and over. So a slope is cut into cells, each is replaced by the plane through it, and that plane is pulled round
        /// towards the nearest of the world's joints. Then the small cells break up the large ones.
        /// </summary>
        public void Break(ScalarField height)
        {
            float[] source = new float[height.Data.Length];
            Array.Copy(height.Data, source, source.Length);
            Facets(source, height, _rock.BlockSize.X, 0x51ED2701u, 1.5f * _rock.Blockiness, 4.5f, 0.8f);
            Array.Copy(height.Data, source, source.Length);
            Facets(source, height, _rock.BlockSize.Y, 0x7F4A7C15u, 0.6f * _rock.Blockiness, 1.8f, 0.62f);

            // Whatever a face is, it isn't polished.
            byte[] rock = _world.Rock.Data;
            int width = height.Width;
            float cell = height.CellSize;
            Parallel.For(0, height.Depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (rock[i] == 0) continue;
                    height.Data[i] += (_noise.Ridged(x * cell + 17f, z * cell - 5f, 7f, 3) - 0.4f) * 0.8f * (rock[i] / 255f);
                }
            });
        }

        private void Facets(float[] source, ScalarField height, float size, uint salt, float lift, float reach, float share)
        {
            byte[] rock = _world.Rock.Data;
            int width = height.Width;
            int depth = height.Depth;
            float cell = height.CellSize;
            float crack = 0.9f + size * 0.05f;
            Parallel.For(0, depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (rock[i] == 0) continue;

                    float worldX = x * cell;
                    float worldZ = z * cell;
                    Nearest(worldX, worldZ, size, salt, out Vector2 first, out uint firstHash, out Vector2 second, out uint secondHash, out float edge);

                    float near = Face(source, width, depth, first, firstHash, size, lift, worldX, worldZ);
                    float far = Face(source, width, depth, second, secondHash, size, lift, worldX, worldZ);
                    // Where two faces meet the rock parts a little. Blending over a metre stops the coarse grid showing up as stairs along it.
                    float meet = 0.5f * (1f - Smoothstep(0f, 1.1f, edge));
                    float faced = Lerp(near, far, meet) - (1f - Smoothstep(0f, crack, edge)) * lift * 0.5f;
                    // A plane runs away from a slope that curves. The shape of the mountain was worked out with some care, and it stays.
                    faced = Math.Clamp(faced, source[i] - reach, source[i] + reach);
                    height.Data[i] = Lerp(source[i], faced, share * rock[i] / 255f);
                }
            });
        }

        // The height at a point of the plane a cell has broken along.
        private float Face(float[] source, int width, int depth, Vector2 centre, uint hash, float size, float lift, float x, float z)
        {
            float reach = MathF.Max(size * 0.32f, 1.5f);
            float middle = Read(source, width, depth, centre.X, centre.Y);
            float slopeX = (Read(source, width, depth, centre.X + reach, centre.Y) - Read(source, width, depth, centre.X - reach, centre.Y)) / (2f * reach);
            float slopeZ = (Read(source, width, depth, centre.X, centre.Y + reach) - Read(source, width, depth, centre.X, centre.Y - reach)) / (2f * reach);

            Vector3 normal = Vector3.Normalize(new Vector3(-slopeX, 1f, -slopeZ));
            float best = 0f;
            Vector3 joint = normal;
            foreach (Vector3 candidate in _rock.Joints)
            {
                float agreement = Vector3.Dot(normal, candidate);
                if (MathF.Abs(agreement) <= MathF.Abs(best)) continue;
                best = agreement;
                joint = candidate;
            }
            normal = Vector3.Normalize(Vector3.Lerp(normal, best < 0f ? -joint : joint, _rock.Planing));

            // A height map has to go up somewhere: the steepest a plane can lie is short of the joint it wants.
            float up = MathF.Max(normal.Y, 0.36f);
            float planeX = -normal.X / up;
            float planeZ = -normal.Z / up;
            float offset = ((Mix(hash) & 0xFF) / 255f - 0.5f) * 2f * lift;
            return middle + offset + planeX * (x - centre.X) + planeZ * (z - centre.Y);
        }

        private static float Read(float[] source, int width, int depth, float x, float z)
        {
            int nodeX = Math.Clamp((int)x, 0, width - 2);
            int nodeZ = Math.Clamp((int)z, 0, depth - 2);
            float tx = Saturate(x - nodeX);
            float tz = Saturate(z - nodeZ);
            int i = nodeZ * width + nodeX;
            float near = source[i] + (source[i + 1] - source[i]) * tx;
            float far = source[i + width] + (source[i + width + 1] - source[i + width]) * tx;
            return near + (far - near) * tz;
        }

        // Worley cells stretched along the strike, as blocks are by the joints that run that way. Edge is the metres to the border between the two nearest.
        private void Nearest(float x, float z, float size, uint salt, out Vector2 first, out uint firstHash, out Vector2 second, out uint secondHash, out float edge)
        {
            const float Stretch = 1.5f;
            float u = (x * _along.X + z * _along.Y) / (size * Stretch);
            float v = (x * _across.X + z * _across.Y) / size;
            int cellU = (int)MathF.Floor(u);
            int cellV = (int)MathF.Floor(v);

            float nearest = float.MaxValue;
            float next = float.MaxValue;
            first = second = default;
            firstHash = secondHash = 0;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    uint hash = Hash(cellU + i, cellV + j, salt);
                    float pointU = cellU + i + (hash & 0xFFFF) / 65535f;
                    float pointV = cellV + j + (hash >> 16) / 65535f;
                    float du = (pointU - u) * Stretch;
                    float dv = pointV - v;
                    float distance = du * du + dv * dv;
                    if (distance >= next) continue;

                    Vector2 point = _along * (pointU * size * Stretch) + _across * (pointV * size);
                    if (distance < nearest)
                    {
                        next = nearest;
                        second = first;
                        secondHash = firstHash;
                        nearest = distance;
                        first = point;
                        firstHash = hash;
                    }
                    else
                    {
                        next = distance;
                        second = point;
                        secondHash = hash;
                    }
                }
            }
            edge = (MathF.Sqrt(next) - MathF.Sqrt(nearest)) * 0.5f * size;
        }

        // A face that goes up all at once from a skirt of what's fallen off it, and lies back as it climbs. It's tall in places and in others hardly there, where a valley comes down to the land.
        private float Wall(float x, float z, float inside, out float skirted)
        {
            float tall = 5f + 26f * Smoothstep(0.32f, 0.72f, _noise.Fbm01(x + 700f, z - 300f, 96f, 3));
            float skirt = 2.5f + 5f * _noise.Fbm01(x - 200f, z + 500f, 44f, 2);
            float scree = 0.6f * MathF.Min(inside, skirt);
            float climbed = Saturate((inside - skirt) / (tall / 2.1f));
            skirted = 1f - Smoothstep(skirt, skirt + 1.5f, inside);
            return scree + tall * climbed * (2f - climbed);
        }

        private float Gullies(float x, float z, float h, float rock, out float crease)
        {
            const float Reach = WorldData.CoarseCell;
            float slopeX = (_elevation.Sample(x + Reach, z) - _elevation.Sample(x - Reach, z)) / (2f * Reach);
            float slopeZ = (_elevation.Sample(x, z + Reach) - _elevation.Sample(x, z - Reach)) / (2f * Reach);

            ErosionFilter.Result result = ErosionFilter.Apply(x, z, slopeX, slopeZ, _crests.Sample(x, z), _seed, _style);

            // Mostly cut down rather than built up: a gully takes rock away.
            crease = Saturate(-result.Ridges * 1.4f - 0.15f);
            return h + (result.Height - 0.45f * result.Reach) * rock;
        }

        // Hard beds stand and soft ones wear back. How far depends on the slope, so level ground isn't touched, and a bed that crosses a spur steps out of it.
        private float Bedding(float x, float z, float wx, float wz, float h, float rock)
        {
            const float Reach = 2f;
            float slopeX = (_elevation.Sample(wx + Reach, wz) - _elevation.Sample(wx - Reach, wz)) / (2f * Reach);
            float slopeZ = (_elevation.Sample(wx, wz + Reach) - _elevation.Sample(wx, wz - Reach)) / (2f * Reach);
            float slope = MathF.Sqrt(slopeX * slopeX + slopeZ * slopeZ);

            float layer = _rock.LayerAt(x, h, z);
            return h + _rock.ShiftAt(layer) * _rock.Bedding * rock * Smoothstep(0.1f, 0.55f, slope);
        }

        /// <summary>
        /// Cuts back whatever would stand between the camera and someone on the land. The camera only ever looks along +z, from a fixed height for its distance,
        /// so a hero can be hidden by nothing but a face falling towards +z steeper than the line of sight, and one pass down every column settles it. A face
        /// the camera can see is untouched, and so is one with no one behind it.
        /// </summary>
        public void KeepHeroInSight(ScalarField height)
        {
            byte[] rock = _world.Rock.Data;
            byte[] paved = _world.PavedRoad.Data;
            byte[] trail = _world.TrailRoad.Data;
            float cell = height.CellSize;
            Parallel.For(0, height.Width, x =>
            {
                float cap = float.MaxValue;
                for (int z = height.Depth - 1; z >= 0; z--)
                {
                    int i = z * height.Width + x;
                    // A road that's been cut through keeps its grade.
                    if (rock[i] > 0 && height.Data[i] > cap && paved[i] == 0 && trail[i] == 0) height.Data[i] = cap;

                    // Anything short of bare rock might be walked on, which goes a little further than the land sites were placed on.
                    if (rock[i] < StandableRock && height.Data[i] > WorldPlan.SeaLevel) cap = MathF.Min(cap, height.Data[i] + SightMargin);
                    if (cap != float.MaxValue) cap += SightSlope * cell;
                }
            });
        }

        /// <summary>
        /// Works out how far every point of rock stands out of what's around it, from the finished ground. It's the difference from a ring of neighbours, as a slope, at two sizes: a crack and
        /// a buttress. Bare ground on a steady slope comes to nothing, however steep, as the far side of the ring cancels the near.
        /// </summary>
        public void MeasureRelief(ScalarField height)
        {
            ByteField proud = _world.Proud;
            byte[] rock = _world.Rock.Data;
            int width = height.Width;
            int depth = height.Depth;
            float[] data = height.Data;
            Parallel.For(0, depth, z =>
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (rock[i] == 0) continue;
                    float near = Ring(data, width, depth, x, z, 2.5f);
                    float far = Ring(data, width, depth, x, z, 7f);
                    float amount = Math.Clamp((near * 0.6f + far * 0.4f) * 2.4f, -1f, 1f);
                    proud.Data[i] = (byte)Math.Clamp((int)((0.5f + 0.5f * amount) * 255f + 0.5f), 0, 255);
                }
            });
        }

        private static float Ring(float[] data, int width, int depth, int x, int z, float radius)
        {
            float sum = 0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * (MathF.Tau / 8f);
                sum += Read(data, width, depth, x + MathF.Cos(angle) * radius, z + MathF.Sin(angle) * radius);
            }
            return (data[z * width + x] - sum * 0.125f) / radius;
        }

        private static uint Hash(int x, int z, uint salt)
        {
            unchecked
            {
                uint h = salt ^ (uint)x * 0x85EBCA6Bu ^ (uint)z * 0xC2B2AE35u;
                return Mix(h);
            }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
