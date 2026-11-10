using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// What throws a shadow from a local light: the models and characters around it. Gathered once per light and handed out
    /// per view of it, so a point light's six faces share the search. Terrain and grass stay out: a light this close to the
    /// ground can't be hidden by either, and they would cost more than everything else put together.
    /// </summary>
    internal sealed class LocalShadowCasters
    {
        // Ground cover under this height throws nothing anyone would see from up here.
        private const float SmallestCaster = 1f;
        private const float EnclosingSize = 6f;     // Metres across, at most, that something a light is inside of can be and still be what holds its flame.

        private readonly BitMask _characterMask = new BitMask(typeof(CharacterModelComponent), typeof(SkeletonComponent), typeof(LocalToWorldMatrixComponent));
        private readonly List<Entity> _near = new();
        private readonly List<(glModel model, Matrix4 matrix, AABB bounds)> _models = new();
        private readonly List<(glModel model, CharacterInstanceData instance, AABB bounds)> _characters = new();

        public void Gather(ECSWorld world, Vector3 position, float radius)
        {
            _models.Clear();
            _characters.Clear();

            SpatialGrid grid = world.GetComponent<RenderSpatialGridSingleton>(world.GetSingleton<RenderSpatialGridSingleton>()).Grid;
            grid.QuerySphere(position, radius, _near);
            foreach (Entity entity in _near)
            {
                if (!world.HasComponent<ModelComponent>(entity) || !world.HasComponent<ModelRenderTag>(entity) || !world.HasComponent<LocalToWorldMatrixComponent>(entity)) continue;

                glModel model = world.GetComponent<ModelComponent>(entity).model;
                if (model == null || model.Hidden) continue;

                Matrix4 matrix = world.GetComponent<LocalToWorldMatrixComponent>(entity).value;
                AABB bounds = AABB.Transform(new AABB(model.box.Min, model.box.Max), matrix);
                if (bounds.Max.Y - bounds.Min.Y < SmallestCaster) continue;

                // What a light hangs from or burns inside would shut it in: a lantern's glass goes all the way round its flame. A building the light is in is another
                // matter: its walls are what keep the light in its room.
                if (Contains(bounds, position) && MathF.Max(bounds.Max.X - bounds.Min.X, bounds.Max.Z - bounds.Min.Z) < EnclosingSize) continue;
                _models.Add((model, matrix, bounds));
            }

            foreach (Archetype archetype in world.QueryArchetypes(_characterMask, BitMask.Empty))
            {
                var modelArray = archetype.GetComponentArray<CharacterModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();

                for (int i = 0; i < archetype.EntityCount; i++)
                {
                    Matrix4 matrix = matrixArray[i].value;
                    glModel model = modelArray[i].model;
                    if (model == null || (matrix.ExtractTranslation() - position).LengthSquared > radius * radius) continue;

                    var instance = new CharacterInstanceData { ModelMatrix = matrix, SkinningMatrices = skeletonArray[i].SkinningMatrices };
                    _characters.Add((model, instance, AABB.Transform(new AABB(model.box.Min, model.box.Max), matrix)));
                }
            }
        }

        private static bool Contains(AABB bounds, Vector3 point)
        {
            return point.X >= bounds.Min.X && point.X <= bounds.Max.X
                && point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y
                && point.Z >= bounds.Min.Z && point.Z <= bounds.Max.Z;
        }

        /// <summary>Queues what <paramref name="frustum"/> can see for drawing into <paramref name="shadow"/>.</summary>
        public void Submit(Frustum frustum, Shadow shadow)
        {
            var models = new Dictionary<glModel, List<Matrix4>>();
            foreach (var (model, matrix, bounds) in _models)
            {
                if (frustum.IntersectsAABB(bounds) == IntersectionResult.Outside) continue;
                if (!models.TryGetValue(model, out var matrices)) models[model] = matrices = new List<Matrix4>();
                matrices.Add(matrix);
            }
            foreach (var (model, matrices) in models)
            {
                Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(new ModelRenderCommand { model = model, matrices = matrices }, shadow);
            }

            var characters = new Dictionary<glModel, List<CharacterInstanceData>>();
            foreach (var (model, instance, bounds) in _characters)
            {
                if (frustum.IntersectsAABB(bounds) == IntersectionResult.Outside) continue;
                if (!characters.TryGetValue(model, out var instances)) characters[model] = instances = new List<CharacterInstanceData>();
                instances.Add(instance);
            }
            foreach (var (model, instances) in characters)
            {
                Engine.RenderEngine._characterRenderer.SubmitShadowCommand(new CharacterRenderCommand(model, instances), shadow);
            }
        }
    }
}
