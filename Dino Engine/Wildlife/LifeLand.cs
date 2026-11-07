using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Wildlife
{
    /// <summary>What sort of place something can live in. A creature names the ones it does, and a place is exactly one of them.</summary>
    [Flags]
    public enum Habitat
    {
        None = 0,
        Meadow = 1 << 0,    // Open ground with grass on it.
        Barren = 1 << 1,    // Open ground with little or none.
        Edge = 1 << 2,      // Where a wood gives way to the open, which is where most birds are.
        Wood = 1 << 3,      // Under the trees.
        Field = 1 << 4,     // What's been ploughed or sown.
        Yard = 1 << 5,      // Around where people live.
        Beach = 1 << 6,     // Sand or shingle by the sea.
        Bank = 1 << 7,      // The edge of any other water.
        Sea = 1 << 8,
        Pond = 1 << 9,
        Crag = 1 << 10,     // Bare rock.
    }

    /// <summary>
    /// Everything wildlife asks of a world, all of it a lookup in a map that's already there. Nothing here draws from the world's own random streams, so whatever lives on a land has no say in how it grew.
    /// </summary>
    public sealed class LifeLand
    {
        private const float ShoreHeight = 1.7f;     // Metres above the water that still count as its edge.
        private const float DeepEnough = 0.3f;      // Metres of water that's water, and not a wet place.
        private const float TipHeight = 0.68f;      // How far up a blade its tip gets, as a share of how long it is.

        public readonly WorldData World;
        private readonly TerrainSurface _surface;

        public LifeLand(WorldData world)
        {
            World = world;
            _surface = new TerrainSurface(world);
        }

        /// <summary>The ground as it's drawn. That's a little above where people stand, which is nothing to someone 4.7 m tall and the whole of a crab.</summary>
        public float Ground(Vector2 at) => World.Height.SampleCubic(at.X, at.Y) + TerrainRenderer.SurfaceOffset;

        /// <summary>The same, near enough, for what asks often and is high up or small: a falling leaf.</summary>
        public float RoughGround(Vector2 at) => World.Height.Sample(at.X, at.Y) + TerrainRenderer.SurfaceOffset;

        /// <summary>Where the highest water over a point stands.</summary>
        public float WaterLevel(Vector2 at) => World.WaterLevelAt(at);

        /// <summary>Whether a body could stand here: on the map, on dry ground, and clear of everything solid.</summary>
        public bool CanStand(Vector2 at)
        {
            return World.InBounds(at) && World.Passable.IsSet(at) && World.WaterDepthAt(at) <= 0.02f;
        }

        /// <summary>Whether something small could stand here, which goes wherever the ground isn't too steep and the water isn't over it.</summary>
        public bool CanScuttle(Vector2 at)
        {
            return World.InBounds(at) && World.Walkable.IsSet(at) && World.WaterDepthAt(at) <= 0.02f;
        }

        public Habitat HabitatAt(Vector2 at)
        {
            if (!World.InBounds(at)) return Habitat.None;

            float level = World.WaterLevelAt(at);
            float height = World.Height.Sample(at);
            bool sea = level <= WorldPlan.SeaLevel + 0.01f;

            if (level - height > DeepEnough) return sea ? Habitat.Sea : Habitat.Pond;
            if (height - level < ShoreHeight)
            {
                if (!sea) return Habitat.Bank;
                SurfaceMaterial shore = World.SurfaceAt(at);
                return shore is SurfaceMaterial.Sand or SurfaceMaterial.Dunes or SurfaceMaterial.Gravel ? Habitat.Beach : Habitat.Bank;
            }

            if (World.Rock.Sample(at) > 0.5f || World.Scree.Sample(at) > 0.5f) return Habitat.Crag;
            if (World.Plaza.Sample(at) + World.Yard.Sample(at) > 0.4f) return Habitat.Yard;
            if (World.Farmland.Sample(at) > 0.5f) return Habitat.Field;

            float canopy = World.Canopy.Sample(at);
            if (canopy > 0.55f) return Habitat.Wood;
            if (canopy > 0.14f) return Habitat.Edge;
            return GrassAt(at).Cover > 0.35f ? Habitat.Meadow : Habitat.Barren;
        }

        /// <summary>What the grass is like at a point, as the ground's painted with it.</summary>
        public GrassCell GrassAt(Vector2 at)
        {
            Span<float> weights = stackalloc float[TerrainSurface.MaxMaterials];
            float height = World.Height.Sample(at);
            float slope = World.Height.SlopeAt(at);
            _surface.Evaluate(at.X, at.Y, height, 1f / MathF.Sqrt(1f + slope * slope), weights, out GrassCell grass);
            return grass;
        }

        /// <summary>How long the grass is at a point, in metres.</summary>
        public float GrassHeight(Vector2 at) => GrassAt(at).Height * WorldScene.LongestGrass;

        /// <summary>How high the grass comes up at a point, in metres. Blades lean and droop, so their tips are well short of how long they are, and where it's thin it comes up to nothing much.</summary>
        public float GrassTop(Vector2 at)
        {
            GrassCell grass = GrassAt(at);
            return grass.Cover < 0.3f ? 0f : grass.Height * WorldScene.LongestGrass * TipHeight;
        }
    }
}
