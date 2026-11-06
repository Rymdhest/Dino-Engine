using Dino_Engine.Animation;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>The ground of a world, as feet find it, and what's standing on it, as falling bodies do. Each user keeps their own: it holds what it last looked at.</summary>
    public sealed class WorldGround : IGround
    {
        private readonly List<(Footprint Shape, float Height)> _near = new();

        public readonly WorldData World;

        public WorldGround(WorldData world)
        {
            World = world;
        }

        public float HeightAt(float x, float z) => World.Height.SampleCubic(x, z);

        public bool LookAround(Vector2 centre, float reach)
        {
            _near.Clear();
            World.Placement.CollectSolids(centre, reach, _near);
            return _near.Count > 0;
        }

        public bool TryPushOut(Vector3 at, float radius, out Vector2 push)
        {
            push = default;
            bool touching = false;
            Vector2 where = at.Xz;
            float above = at.Y - World.Height.Sample(where);
            foreach ((Footprint shape, float height) in _near)
            {
                if (above > height || !shape.TryPushOut(where + push, radius, out Vector2 out_)) continue;
                push += out_;
                touching = true;
            }
            return touching;
        }
    }
}
