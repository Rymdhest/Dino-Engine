using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// Keeps a character on ground it can stand on. It takes the velocity it asks for and cuts it down to what the world allows:
    /// solid things push it out, so it slides around trees and along walls, and where the ground is too steep or the water too
    /// deep it stops or slides along the edge. It reads the same footprints and maps the world was built from, so what looks
    /// solid is exactly what is solid. What it decides depends on nothing but the world and what it is asked, which is why a
    /// client can work out where its hero goes and arrive at the same place as the server. Each user keeps their own: it holds
    /// a little scratch space and is not to be shared between threads.
    /// </summary>
    public sealed class CharacterMover
    {
        private const int PushPasses = 3;
        private const float SteepestRun = 0.5f;     // Slope at which climbing starts to slow a character down.
        private const float WadeSlowdown = 0.45f;   // Speed left in water up to the waist.

        private readonly List<Footprint> _solids = new();

        public readonly WorldData World;

        public CharacterMover(WorldData world)
        {
            World = world;
        }

        /// <summary>Where a character at <paramref name="from"/>, asking for <paramref name="wanted"/> metres a second, ends up.</summary>
        public Vector2 Move(Vector2 from, Vector2 wanted, float radius, float deltaTime)
        {
            if (wanted.LengthSquared < 1e-6f) return from;

            wanted *= SpeedFactor(from, wanted, World.WaterDepthAt(from));
            Vector2 next = Slide(from + wanted * deltaTime, radius);

            if (!World.Walkable.IsSet(next))
            {
                // Try each axis on its own, so that walking at an edge slides along it rather than stopping dead.
                Vector2 alongX = Slide(from + new Vector2(wanted.X, 0f) * deltaTime, radius);
                Vector2 alongZ = Slide(from + new Vector2(0f, wanted.Y) * deltaTime, radius);
                if (World.Walkable.IsSet(alongX)) next = alongX;
                else if (World.Walkable.IsSet(alongZ)) next = alongZ;
                else next = from;
            }
            return next;
        }

        private float SpeedFactor(Vector2 from, Vector2 wanted, float waterDepth)
        {
            float speed = wanted.Length;
            Vector2 direction = wanted / speed;
            float rise = World.Height.Sample(from + direction) - World.Height.Sample(from);

            float factor = 1f;
            if (rise > 0f) factor *= 1f - 0.55f * MathF.Min(rise / SteepestRun, 1f);
            if (waterDepth > 0.15f) factor *= 1f - (1f - WadeSlowdown) * MathF.Min(waterDepth / WorldData.WadingDepth, 1f);
            return factor;
        }

        private Vector2 Slide(Vector2 position, float radius)
        {
            _solids.Clear();
            World.Placement.CollectSolids(position, radius + 2f, _solids);

            for (int pass = 0; pass < PushPasses; pass++)
            {
                bool touching = false;
                for (int i = 0; i < _solids.Count; i++)
                {
                    if (!_solids[i].TryPushOut(position, radius, out Vector2 push)) continue;
                    position += push;
                    touching = true;
                }
                if (!touching) break;
            }
            return position;
        }
    }
}
