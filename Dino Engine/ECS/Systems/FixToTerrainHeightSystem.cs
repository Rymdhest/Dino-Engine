using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;

namespace Dino_Engine.ECS.Systems
{
    public class FixToTerrainHeightSystem : SystemBase
    {
        private WorldData? _world;

        public FixToTerrainHeightSystem()
            : base(new BitMask(typeof(PositionComponent), typeof(FixToTerrainHeightComponent)))
        {
        }

        internal override void UpdateInternal(ECSWorld world, float deltaTime)
        {
            if (!world.TryGetSingleton<WorldDataSingleton>(out Entity data)) return;
            _world = world.GetComponent<WorldDataSingleton>(data).World;
            base.UpdateInternal(world, deltaTime);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var pos = entity.Get<PositionComponent>();
            pos.value.Y = _world!.Height.SampleCubic(pos.value.X, pos.value.Z);
            entity.Set(pos);
        }
    }
}
