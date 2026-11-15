using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// The beach, as whatever's laid on the land has to know it (SHORE.md): how much of a point the sea's swash runs over, with the waves a land has on an
    /// ordinary day and erring towards the biggest of them, so that nothing grows where they'd wash it and the sand there is bare but for what the sea leaves.
    /// The swash itself is the shaders' (Water/ShoreData.glsl), worked out from the waves of the moment; this is only how far up it comes. Headless.
    /// </summary>
    public static class WorldShore
    {
        /// <summary>Metres above the sea the waves on an open coast climb to on a steep beach, about twice their height (WaterWaves.MaxRunUpHeight), with room for a lobe.</summary>
        public const float SwashTop = 1.5f;

        /// <summary>Metres past the sea's edge they run on a gentle one: Hunt's sqrt(H L0) for the biggest of them, with room for a lobe.</summary>
        public const float SwashReach = 9f;

        /// <summary>0..1, how much of the point the swash runs over: 1 on the wet beach, 0 above it, and 0 anywhere but by the sea.</summary>
        public static float Swashed(WorldData world, Vector2 position)
        {
            // The picture behind the main menu is composed and settled, and its meadow runs down to the water as it was laid out (MENU.md).
            if (world.Plan.Archetype.Vista) return 0f;
            if (world.Inland == null || world.Height == null || !world.InBounds(position)) return 0f;
            float sea = 1f - Saturate(world.PondShore?.Sample(position) ?? 0f);
            if (sea <= 0f) return 0f;
            float above = world.Height.Sample(position) - WorldPlan.SeaLevel;
            if (above >= SwashTop) return 0f;
            float inland = world.Inland.Sample(position);
            return sea * (1f - Smoothstep(SwashReach * 0.7f, SwashReach * 1.4f, inland)) * (1f - Smoothstep(SwashTop * 0.6f, SwashTop, above));
        }
    }
}
