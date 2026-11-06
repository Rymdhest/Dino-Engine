using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using static Dino_Engine.Modelling.Model.glModel;

namespace Dino_Engine.ECS.Systems
{
    public class DirectionalCascadeShadowSystem : SystemBase
    {
        private int minCountForInstanced = 0;

        // How far along the view the centre of a cascade lies, in cascade widths.
        private const float LookAhead = 0.25f;

        // Persistent structures to avoid per-frame allocations
        private readonly Dictionary<glModel, List<Matrix4>> _models = new();
        private readonly Dictionary<glModel, List<CharacterInstanceData>> _characters = new(); // Added for characters
        private readonly List<ImposterInstanceData> _imposters = new();

        private readonly List<Entity> _visibleChunks = new();
        private readonly List<TerrainChunkRenderData> _terrainCommands = new();
        private readonly List<GrassChunkRenderData> _grassCommands = new();

        public DirectionalCascadeShadowSystem()
            : base(new BitMask(typeof(DirectionalLightTag), typeof(DirectionNormalizedComponent), typeof(DirectionalCascadingShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var direction = entity.Get<DirectionNormalizedComponent>().value;
            var shadowCascade = entity.Get<DirectionalCascadingShadowComponent>();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();

            // The camera looks down at the ground from well away, so the shadows belong around what it looks at, not around itself.
            Vector3 forward = (new Vector4(0f, 0f, -1f, 0f) * Matrix4.Invert(world.GetComponent<ViewMatrixComponent>(world.Camera).value)).Xyz.Normalized();

            // 1. Prepare View Matrices for Cascades
            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                var cascade = shadowCascade.cascades[i];
                int resolution = cascade.shadowFrameBuffer.getResolution().X;

                Vector3 center = cameraPos + forward * (cascade.projectionSize * LookAhead);
                shadowCascade.cascades[i].lightViewMatrix = CreateLightViewMatrix(direction, center, cascade.projectionSize, resolution);
                shadowCascade.cascades[i].shadowFrameBuffer.ClearDepth();
            }
            entity.Set(shadowCascade);

            // 2. Clear state
            foreach (var list in _models.Values) list.Clear();
            foreach (var list in _characters.Values) list.Clear(); // Clear character lists
            _imposters.Clear();

            // 3. Classify Archetypes (Regular Models & Imposters)
            var modelQuery = world.QueryArchetypes(new BitMask(
                typeof(ModelRenderTag),
                typeof(ModelComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(RotationComponent)),
                BitMask.Empty);

            foreach (var archetype in modelQuery)
            {
                var modelArray = archetype.GetComponentArray<ModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var rotArray = archetype.GetComponentArray<RotationComponent>();

                int count = archetype.EntityCount;

                for (int i = 0; i < count; i++)
                {
                    Matrix4 mat = matrixArray[i].value;
                    Vector3 pos = mat.ExtractTranslation();
                    float distSq = (pos - cameraPos).LengthSquared;

                    glModel model = modelArray[i].model;
                    bool useImposter = false;

                    if (model.Imposter != null)
                    {
                        if (distSq > model.Imposter.DistanceSquared) useImposter = true;
                    }

                    if (useImposter)
                    {
                        Vector3 entityScale = mat.ExtractScale();
                        ImposterData imposter = model.Imposter;
                        Quaternion rot = rotArray[i].quaternion;

                        Vector3 scaledCenter = imposter.LocalCenter * entityScale;
                        Vector3 rotatedOffset = Vector3.Transform(scaledCenter, rot);
                        Vector3 quadWorldCenter = pos + rotatedOffset;

                        _imposters.Add(new ImposterInstanceData
                        {
                            modelID = (float)model.Imposter.TextureIndex,
                            Position = quadWorldCenter,
                            Scale = entityScale,
                            Rotation = rot,
                            BaseLength = imposter.BaseLength
                        });
                    }
                    else
                    {
                        if (!_models.TryGetValue(model, out var list))
                        {
                            list = new List<Matrix4>();
                            _models[model] = list;
                        }
                        list.Add(mat);
                    }
                }
            }

            // 3.5. Classify Character Archetypes
            // Note: Replace 'CharacterRenderTag' and 'AnimatorComponent' with your actual component types if they differ.
            var characterQuery = world.QueryArchetypes(new BitMask(
                typeof(CharacterModelComponent),
                typeof(LocalToWorldMatrixComponent),
                typeof(SkeletonComponent)),
                BitMask.Empty);

            foreach (var archetype in characterQuery)
            {
                var modelArray = archetype.GetComponentArray<CharacterModelComponent>();
                var matrixArray = archetype.GetComponentArray<LocalToWorldMatrixComponent>();
                var skeletonArray = archetype.GetComponentArray<SkeletonComponent>();

                int count = archetype.EntityCount;
                for (int i = 0; i < count; i++)
                {
                    glModel model = modelArray[i].model;
                    Matrix4 mat = matrixArray[i].value;
                    var skeleton = skeletonArray[i];

                    var skinningMatrices = skeleton.SkinningMatrices;
                    int boneCount = skinningMatrices != null ? skinningMatrices.Length : (int)BoneType.Count;
                    Matrix4[] skinningMatricesCopy = new Matrix4[boneCount];

                    if (skinningMatrices != null && skinningMatrices.Length > 0)
                    {
                        Array.Copy(skinningMatrices, skinningMatricesCopy, boneCount);
                    }
                    else
                    {
                        for (int b = 0; b < boneCount; b++)
                        {
                            skinningMatricesCopy[b] = Matrix4.Identity;
                        }
                    }

                    if (!_characters.TryGetValue(model, out var list))
                    {
                        list = new List<CharacterInstanceData>();
                        _characters[model] = list;
                    }

                    list.Add(new CharacterInstanceData
                    {
                        ModelMatrix = mat,
                        SkinningMatrices = skinningMatricesCopy
                    });
                }
            }

                // 4. Pre-build Commands ONCE per frame
                var prebuiltModelCommands = new List<ModelRenderCommand>(_models.Count);
            foreach (var kvp in _models)
            {
                if (kvp.Value.Count > 0)
                {
                    prebuiltModelCommands.Add(new ModelRenderCommand(kvp.Key, kvp.Value));
                }
            }

            var prebuiltCharacterCommands = new List<CharacterRenderCommand>(_characters.Count);
            foreach (var kvp in _characters)
            {
                if (kvp.Value.Count > 0)
                {
                    prebuiltCharacterCommands.Add(new CharacterRenderCommand(kvp.Key, kvp.Value));
                }
            }

            ImposterRenderCommand? imposterCommand = null;
            if (_imposters.Count > 0)
            {
                imposterCommand = new ImposterRenderCommand { instances = _imposters.ToArray() };
            }

            // 5. Submit Models, Characters & Imposters to each Cascade
            for (int j = 0; j < shadowCascade.cascades.Length; j++)
            {
                Shadow cascade = shadowCascade.cascades[j];

                // Standard Models
                foreach (var cmd in prebuiltModelCommands)
                {
                    if (cmd.matrices.Count > minCountForInstanced)
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(cmd, cascade);
                    else
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(cmd, cascade);
                }

                // Characters
                foreach (var cmd in prebuiltCharacterCommands)
                {
                    Engine.RenderEngine._characterRenderer.SubmitShadowCommand(cmd, cascade);
                }

                // Imposters
                if (imposterCommand.HasValue)
                {
                    Engine.RenderEngine._imposterRenderer.SubmitShadowCommand(imposterCommand.Value, cascade);
                }
            }

            // 6. Terrain & Grass
            var quadtreeComp = world.GetComponent<TerrainQuadTreeSingleton>(world.GetSingleton<TerrainQuadTreeSingleton>());

            // The rain falls on the same things the sun does, and now and then needs to know what they are. Characters come and go too quickly to matter.
            var focusPoint = Engine.RenderEngine.context.focusPoint;
            Vector3 rainFocus = focusPoint.W > 0f ? focusPoint.Xyz : cameraPos;
            if (Engine.RenderEngine._weatherRenderer.WantsRainShadow(rainFocus, deltaTime, out Shadow rainShadow))
            {
                foreach (var cmd in prebuiltModelCommands) Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(cmd, rainShadow);
                if (imposterCommand.HasValue) Engine.RenderEngine._imposterRenderer.SubmitShadowCommand(imposterCommand.Value, rainShadow);

                // And while anything is wet, the same things from straight above, for what drips off them.
                if (Engine.RenderEngine._weatherRenderer.WantsDripDepth(rainFocus, out Shadow dripDepth))
                {
                    foreach (var cmd in prebuiltModelCommands) Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(cmd, dripDepth);
                    if (imposterCommand.HasValue) Engine.RenderEngine._imposterRenderer.SubmitShadowCommand(imposterCommand.Value, dripDepth);
                }

                _visibleChunks.Clear();
                _terrainCommands.Clear();
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComp, new Util.Frustum(rainShadow.lightViewMatrix * rainShadow.shadowProjectionMatrix), _visibleChunks);
                foreach (Entity chunkEntity in _visibleChunks)
                {
                    _terrainCommands.Add(new TerrainChunkRenderData
                    {
                        chunkPos = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value.ExtractTranslation(),
                        size = world.GetComponent<ScaleComponent>(chunkEntity).value,
                        arrayID = world.GetComponent<TerrainChunkComponent>(chunkEntity).normalHeightTextureArrayID,
                    });
                }
                Engine.RenderEngine._weatherRenderer.SubmitRainShadowTerrain(_terrainCommands.ToArray());
            }

            for (int i = 0; i < shadowCascade.cascades.Length; i++)
            {
                Shadow shadow = shadowCascade.cascades[i];

                _visibleChunks.Clear();
                _terrainCommands.Clear();
                _grassCommands.Clear();

                var viewProj = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComp, new Util.Frustum(viewProj), _visibleChunks);

                foreach (Entity chunkEntity in _visibleChunks)
                {
                    var ltw = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value;
                    var size = world.GetComponent<ScaleComponent>(chunkEntity).value;
                    var chunkComp = world.GetComponent<TerrainChunkComponent>(chunkEntity);

                    _terrainCommands.Add(new TerrainChunkRenderData { chunkPos = ltw.ExtractTranslation(), size = size, arrayID = chunkComp.normalHeightTextureArrayID });

                    float dist = Vector2.Distance(cameraPos.Xz, ltw.ExtractTranslation().Xz + size.Xz * 0.5f);
                    if (dist < 500 && i < 5)
                    {
                        _grassCommands.Add(new GrassChunkRenderData { chunkPos = ltw.ExtractTranslation().Xz, size = size.X, arrayID = chunkComp.normalHeightTextureArrayID });
                    }
                }

                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(_grassCommands.ToArray()), shadow);
                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(_terrainCommands.ToArray(), 0.0f), shadow);
            }
        }

        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center, float size, int resolution)
        {
            direction = Vector3.Normalize(direction);
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

            Vector3 forward = direction;
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            Vector3 lightUp = Vector3.Cross(forward, right);

            float texelSize = size / resolution;

            float x = Vector3.Dot(center, right);
            float y = Vector3.Dot(center, lightUp);
            float z = Vector3.Dot(center, forward);

            x = MathF.Floor(x / texelSize) * texelSize;
            y = MathF.Floor(y / texelSize) * texelSize;

            Vector3 snappedCenter = right * x + lightUp * y + forward * z;

            Vector3 lightPos = snappedCenter - direction * size / 2f;
            return Matrix4.LookAt(lightPos, snappedCenter, up);
        }
    }
}