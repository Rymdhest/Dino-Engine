using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Tells global illumination which world it's in and which chunks of terrain are live (GlobalIlluminationRenderer, GI.md). Everything
    /// else it needs it reads from the world's own data, which is why what's off the screen or under a roof is as solid as what's in view.
    /// </summary>
    public class GlobalIlluminationSystem : SystemBase
    {
        public GlobalIlluminationSystem()
            : base(new BitMask(typeof(WorldStreamSingleton)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            var gi = Engine.RenderEngine._globalIllumination;
            if (gi == null) return;
            if (!world.TryGetSingleton<WorldStreamSingleton>(out Entity streamEntity))
            {
                gi.SetWorld(null, null, null);
                return;
            }
            WorldStreamSingleton stream = world.GetComponent<WorldStreamSingleton>(streamEntity);
            gi.SetWorld(stream.World, stream.ObjectModels, stream.ObjectLoose);
            if (!Engine.RenderEngine.GlobalIllumination.Enabled) return;

            if (world.TryGetSingleton<TerrainQuadTreeSingleton>(out Entity terrainEntity))
            {
                TerrainQuadTreeSingleton terrain = world.GetComponent<TerrainQuadTreeSingleton>(terrainEntity);
                Vector4 focus = Engine.RenderEngine.context.focusPoint;
                Vector3 at = focus.W > 0f ? focus.Xyz : Engine.RenderEngine.context.viewPos;
                gi.SetChunks(terrain.Chunks, world, terrain.ChunkSize, at);
            }
        }
    }
}
