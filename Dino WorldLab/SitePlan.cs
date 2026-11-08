using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>A surveyor's plan of a small part of a world, for checking layouts: ground, roads, and every footprint to scale.</summary>
    internal sealed class SitePlan
    {
        private readonly WorldData _world;
        private readonly Vector2 _centre;
        private readonly float _metresPerPixel;
        private readonly int _resolution;
        private readonly byte[] _pixels;

        public byte[] Pixels => _pixels;

        public SitePlan(WorldData world, Vector2 centre, float span, int resolution)
        {
            _world = world;
            _centre = centre;
            _resolution = resolution;
            _metresPerPixel = span / resolution;
            _pixels = new byte[resolution * resolution * 4];
        }

        public void Draw()
        {
            DrawGround();

            _world.Placement.ForEach((footprint, layer, solid) =>
            {
                if (layer == PlacementLayer.Road) return;
                if (layer == PlacementLayer.None)
                {
                    Outline(footprint, new Vector3(0.05f, 0.05f, 0.05f));
                    return;
                }
                Vector3 colour = layer switch
                {
                    PlacementLayer.Tree => new Vector3(0.1f, 0.42f, 0.14f),
                    PlacementLayer.Shrub => new Vector3(0.45f, 0.65f, 0.2f),
                    PlacementLayer.Rock => new Vector3(0.6f, 0.6f, 0.62f),
                    PlacementLayer.Deadwood => new Vector3(0.45f, 0.3f, 0.2f),
                    PlacementLayer.Structure => new Vector3(0.85f, 0.2f, 0.12f),
                    PlacementLayer.Prop => new Vector3(0.95f, 0.65f, 0.1f),
                    PlacementLayer.Fence => new Vector3(1f, 1f, 1f),
                    PlacementLayer.Field => new Vector3(0.8f, 0.72f, 0.3f),
                    _ => new Vector3(0.6f, 0.2f, 0.7f),
                };
                Outline(footprint, colour);
            });

            foreach (WorldSite site in _world.Sites)
            {
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    Vector3 colour = spawn.Role switch
                    {
                        SpawnRole.Villager => new Vector3(0.2f, 0.7f, 1f),
                        SpawnRole.Merchant or SpawnRole.Elder or SpawnRole.BetaVendor => new Vector3(1f, 1f, 0.3f),
                        SpawnRole.Chest => new Vector3(1f, 0.85f, 0f),
                        SpawnRole.Boss => new Vector3(1f, 0f, 1f),
                        _ => new Vector3(1f, 0.1f, 0.1f),
                    };
                    Dot(spawn.Position, 1.2f, colour);
                }
            }
        }

        private void DrawGround()
        {
            Parallel.For(0, _resolution, row =>
            {
                for (int column = 0; column < _resolution; column++)
                {
                    Vector2 p = ToWorld(column, row);
                    float h = _world.Height.Sample(p);
                    Vector3 colour;
                    if (_world.WaterDepthAt(p) > 0f)
                    {
                        colour = new Vector3(0.2f, 0.42f, 0.55f);
                    }
                    else
                    {
                        colour = Vector3.Zero;
                        for (int b = 0; b < _world.BiomeWeights.Length; b++)
                        {
                            colour += _world.Plan.Biomes[b].MapColour * _world.BiomeWeights[b].Sample(p);
                        }
                        colour = Vector3.Lerp(colour, new Vector3(0.12f, 0.28f, 0.14f), _world.Canopy.Sample(p) * 0.6f);
                        colour = Vector3.Lerp(colour, new Vector3(0.55f, 0.42f, 0.28f), _world.Farmland.Sample(p));
                        colour = Vector3.Lerp(colour, new Vector3(0.62f, 0.54f, 0.42f), _world.Yard.Sample(p));
                        colour = Vector3.Lerp(colour, new Vector3(0.68f, 0.66f, 0.6f), _world.Plaza.Sample(p));
                        colour = Vector3.Lerp(colour, new Vector3(0.78f, 0.68f, 0.5f), _world.TrailRoad.Sample(p));
                        colour = Vector3.Lerp(colour, new Vector3(0.82f, 0.82f, 0.8f), _world.PavedRoad.Sample(p));
                        float slope = _world.Height.SlopeAt(p);
                        colour *= 1f - MathF.Min(slope, 0.8f) * 0.5f;
                    }
                    Put(column, row, colour);
                }
            });
        }

        private Vector2 ToWorld(int column, int row)
        {
            float half = _resolution * 0.5f;
            return _centre + new Vector2(column - half, half - row) * _metresPerPixel;
        }

        private (float x, float y) ToPixel(Vector2 p)
        {
            Vector2 offset = (p - _centre) / _metresPerPixel;
            float half = _resolution * 0.5f;
            return (offset.X + half, half - offset.Y);
        }

        private void Put(int x, int y, Vector3 colour)
        {
            if (x < 0 || y < 0 || x >= _resolution || y >= _resolution) return;
            int i = (y * _resolution + x) * 4;
            _pixels[i] = (byte)Math.Clamp(colour.X * 255f, 0f, 255f);
            _pixels[i + 1] = (byte)Math.Clamp(colour.Y * 255f, 0f, 255f);
            _pixels[i + 2] = (byte)Math.Clamp(colour.Z * 255f, 0f, 255f);
            _pixels[i + 3] = 255;
        }

        private void Dot(Vector2 p, float radiusMetres, Vector3 colour)
        {
            (float cx, float cy) = ToPixel(p);
            float radius = radiusMetres / _metresPerPixel;
            for (int y = (int)(cy - radius); y <= (int)(cy + radius); y++)
            {
                for (int x = (int)(cx - radius); x <= (int)(cx + radius); x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius) Put(x, y, colour);
                }
            }
        }

        private void Line(Vector2 a, Vector2 b, Vector3 colour)
        {
            (float ax, float ay) = ToPixel(a);
            (float bx, float by) = ToPixel(b);
            int steps = (int)MathF.Max(MathF.Abs(bx - ax), MathF.Abs(by - ay)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Put((int)(ax + (bx - ax) * t), (int)(ay + (by - ay) * t), colour);
            }
        }

        private void Outline(in Footprint footprint, Vector3 colour)
        {
            if (footprint.IsCircle)
            {
                const int Sides = 20;
                Vector2 previous = footprint.Centre + new Vector2(footprint.HalfExtents.X, 0f);
                for (int i = 1; i <= Sides; i++)
                {
                    float angle = i * MathF.Tau / Sides;
                    Vector2 next = footprint.Centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * footprint.HalfExtents.X;
                    Line(previous, next, colour);
                    previous = next;
                }
                return;
            }

            Vector2 half = footprint.HalfExtents;
            Vector2 a = footprint.ToWorld(new Vector2(-half.X, -half.Y));
            Vector2 b = footprint.ToWorld(new Vector2(half.X, -half.Y));
            Vector2 c = footprint.ToWorld(new Vector2(half.X, half.Y));
            Vector2 d = footprint.ToWorld(new Vector2(-half.X, half.Y));
            Line(a, b, colour);
            Line(b, c, colour);
            Line(c, d, colour);
            Line(d, a, colour);
        }
    }
}
