using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    public class PointLightShadowSystem : SystemBase
    {
        private int minCountForInstanced = 10;

        public PointLightShadowSystem()
            : base(new BitMask(
                typeof(PointLightTag),
                typeof(LocalToWorldMatrixComponent),
                typeof(PointLightShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            var shadowComp = entity.Get<PointLightShadowComponent>();
            var lightPos = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
            var cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            Shadow shadow = shadowComp.shadow;

            // Gather shadow casting models once per point light
            var shadowCastingModels = world.QueryEntities(new BitMask(
                typeof(ModelComponent), typeof(ModelRenderTag), typeof(LocalToWorldMatrixComponent)), BitMask.Empty);

            Dictionary<glModel, List<Matrix4>> commands = new();
            for (int i = 0; i < shadowCastingModels.Count; i++)
            {
                var ltw = world.GetComponent<LocalToWorldMatrixComponent>(shadowCastingModels[i]).value;
                var model = world.GetComponent<ModelComponent>(shadowCastingModels[i]).model;
                if (!commands.ContainsKey(model)) commands[model] = new List<Matrix4>();
                commands[model].Add(ltw);
            }
            Vector3[] directions = {
            Vector3.UnitX, -Vector3.UnitX, // +X, -X
            Vector3.UnitY, -Vector3.UnitY, // +Y, -Y
            Vector3.UnitZ, -Vector3.UnitZ  // +Z, -Z
        };

            Vector3[] ups = {
            -Vector3.UnitY, -Vector3.UnitY, // +X, -X use -Y as Up
            Vector3.UnitZ, -Vector3.UnitZ,  // +Y uses +Z, -Y uses -Z as Up
            -Vector3.UnitY, -Vector3.UnitY  // +Z, -Z use -Y as Up
         };

            // Render 6 passes into the Cubemap
            for (int i = 0; i < 6; i++)
            {
                Shadow passShadow = shadow;
                // 1. Bind specific face of Cubemap to Framebuffer
                GL.DepthMask(true);
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + i);
                GL.Clear(ClearBufferMask.DepthBufferBit);


                passShadow.cubemapFaceIndex = i;
                passShadow.lightViewMatrix = Matrix4.LookAt(lightPos, lightPos + directions[i], ups[i]);

                // 3. Submit geometry
                foreach (var command in commands)
                {
                    var renderCmd = new ModelRenderCommand { model = command.Key, matrices = command.Value.ToArray() };
                    if (renderCmd.matrices.Length > minCountForInstanced)
                        Engine.RenderEngine._instancedModelRenderer.SubmitShadowCommand(renderCmd, passShadow);
                    else
                        Engine.RenderEngine._modelRenderer.SubmitShadowCommand(renderCmd, passShadow);
                }


                var visibleChunks = new List<Entity>();

                var grassChunks = new List<GrassChunkRenderData>();

                var quadtreeComponent = world.GetComponent<TerrainQuadTreeComponent>(world.GetSingleton<TerrainQuadTreeComponent>());
                var viewProjectionMatrix = passShadow.lightViewMatrix * passShadow.shadowProjectionMatrix;
                TerrainChunkSystem.CollectVisibleChunks(quadtreeComponent.QuadTree, new Util.Frustum(viewProjectionMatrix), visibleChunks);
                var terrainChunksRenderData = new List<TerrainChunkRenderData>();
                foreach (Entity chunkEntity in visibleChunks)
                {
                    Vector3 chunkPosition = world.GetComponent<LocalToWorldMatrixComponent>(chunkEntity).value.ExtractTranslation();
                    Vector3 chunkSize = world.GetComponent<ScaleComponent>(chunkEntity).value;
                    float arrayID = world.GetComponent<TerrainChunkComponent>(chunkEntity).normalHeightTextureArrayID;
                    TerrainChunkRenderData chunkCommand = new TerrainChunkRenderData();
                    chunkCommand.chunkPos = chunkPosition;
                    chunkCommand.size = chunkSize;
                    chunkCommand.arrayID = arrayID;
                    terrainChunksRenderData.Add(chunkCommand);

                    GrassChunkRenderData grassCommand = new GrassChunkRenderData();
                    grassCommand.chunkPos = chunkPosition.Xz;
                    grassCommand.size = chunkSize.X;
                    grassCommand.arrayID = arrayID;

                    float distance = Vector2.Distance(cameraPos.Xz, chunkPosition.Xz + chunkSize.Xz * 0.5f);

                    if (distance < 30)
                    {
                        grassChunks.Add(grassCommand);
                    }
                }

                Engine.RenderEngine._grassRenderer.SubmitShadowCommand(new GrassRenderCommand(grassChunks.ToArray(), 0), passShadow);


                Engine.RenderEngine._terrainRenderer.SubmitShadowCommand(new TerrainRenderCommand(terrainChunksRenderData.ToArray(), 0.0f), passShadow);
            }


        }
    }
}