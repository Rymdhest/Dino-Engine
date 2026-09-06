using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct TerrainChunkRenderData
    {
        public Vector3 chunkPos;
        public Vector3 size;
        public float arrayID;
    }
    public struct TerrainRenderCommand : IRenderCommand
    {
        public TerrainChunkRenderData[] chunks;
        public float parallaxDepth;

        public TerrainRenderCommand(TerrainChunkRenderData[] chunks, float parallaxDepth)
        {
            this.chunks = chunks;
            this.parallaxDepth = parallaxDepth;
        }
    }

    public class TerrainRenderer : GeometryCommandDrivenRenderer<TerrainRenderCommand>
    {
        private ShaderProgram _terrainShader = new ShaderProgram("Terrain.vert", "Terrain.frag");
        private ShaderProgram _terrainShadowShader = new ShaderProgram("Terrain_Shadow.vert", "Terrain_Shadow.frag");
        private ShaderProgram _grassCarveShader = new ShaderProgram("GrassCarve.vert", "GrassCarve.frag");

        private glModel baseChunkModel;
        private int normalRoadHeightTextureArray;
        private int grassTextureArray;
        private IDAllocator<ushort> normalHeightTextureArrayAllocator = new();
        private readonly int MAX_TERRAIN_CHUNKS = 1024*2;
        public static readonly int CHUNK_RESOLUTION = 16;
        private int carveFBO;

        private int instanceVBO;

        public TerrainRenderer() : base("Terrain")
        {
            _terrainShader.bind();
            _terrainShader.loadUniformInt("albedoMapTextureArray", 0);
            _terrainShader.loadUniformInt("normalMapTextureArray", 1);
            _terrainShader.loadUniformInt("materialMapTextureArray", 2);

            _terrainShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _terrainShader.loadUniformInt("normalMapModelTextureArray", 4);
            _terrainShader.loadUniformInt("materialMapModelTextureArray", 5);

            _terrainShader.loadUniformInt("normalHeightTextureArray", 6);
            _terrainShader.loadUniformInt("grassTextureArray", 7);

            _terrainShader.unBind();


            _terrainShadowShader.bind();
            _terrainShadowShader.loadUniformInt("normalHeightTextureArray", 6);
            _terrainShadowShader.unBind();


            var mesh = MeshGenerator.generatePlane(size : new Vector2(1f, 1f), resolution : new Vector2i(CHUNK_RESOLUTION-1), material : new VertexMaterial(TextureGenerator.flat), centerX : false, centerY : false);
            //var mesh = MeshGenerator.generateBox(Material.ROCK);
            vIndex[] vindices = mesh.getAllIndicesArray();
            int[] indices = new int[vindices.Length];
            for (int i = 0; i < vindices.Length; i++)
            {
                indices[i] = vindices[i].index;
            }
            
            baseChunkModel = glLoader.loadToVAO(mesh.getAllPositionsArray(), indices, 3);

            instanceVBO = GL.GenBuffer();
            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);

            int stride = Marshal.SizeOf<TerrainChunkRenderData>();

            // chunkPos at location 3
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(3, 1);

            // chunkSize at location 4
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, false, stride, Vector3.SizeInBytes);
            GL.VertexAttribDivisor(4, 1);

            // heightMapID at location 5
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 2 * Vector3.SizeInBytes);
            GL.VertexAttribDivisor(5, 1);

            GL.BindVertexArray(0);


            normalRoadHeightTextureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba16f, CHUNK_RESOLUTION, CHUNK_RESOLUTION, MAX_TERRAIN_CHUNKS);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            grassTextureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, grassTextureArray);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, SizedInternalFormat.Rgba16f, CHUNK_RESOLUTION, CHUNK_RESOLUTION, MAX_TERRAIN_CHUNKS);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);


            carveFBO = GL.GenFramebuffer();
        }

        public int GetNormalRoadHeightTextureArray()
        {
            return normalRoadHeightTextureArray;
        }
        public int GetGrassTextureArray()
        {
            return grassTextureArray;
        }

        public void freeChunk(int chunk)
        {
            normalHeightTextureArrayAllocator.Release((ushort)chunk);
        }

        public int insertDataAndCarveChunk(FloatGrid heightGrid, Vector3Grid normalGrid, FloatGrid grassGrid, Vector2 chunkPos, Vector2 chunkSize, SpatialGrid spatialGrid)
        {
            int id = (int)normalHeightTextureArrayAllocator.Allocate();
            var resolution = heightGrid.Resolution;
            int dimensions = 4;

            // 1. Upload initial CPU grass and normal/height data into texture arrays (your existing logic)
            var pixelsNormalRoadHeight = new float[dimensions * resolution.X * resolution.Y];
            var pixelsGrass = new float[dimensions * resolution.X * resolution.Y];

            for (int y = 0; y < resolution.Y; y++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    int i = y * resolution.X + x;
                    pixelsNormalRoadHeight[i * dimensions + 0] = normalGrid.Values[x, y].X;
                    pixelsNormalRoadHeight[i * dimensions + 1] = normalGrid.Values[x, y].Y;
                    pixelsNormalRoadHeight[i * dimensions + 2] = normalGrid.Values[x, y].Z;
                    pixelsNormalRoadHeight[i * dimensions + 3] = heightGrid.Values[x, y];

                    pixelsGrass[i * dimensions + 0] = grassGrid.Values[x, y];
                    pixelsGrass[i * dimensions + 1] = 0f;
                    pixelsGrass[i * dimensions + 2] = 0f;
                    pixelsGrass[i * dimensions + 3] = 0f;
                }
            }

            // Upload to normalRoadHeightTextureArray slice [id]
            UploadSliceData(normalRoadHeightTextureArray, pixelsNormalRoadHeight, resolution, id);
            // Upload to grassTextureArray slice [id]
            UploadSliceData(grassTextureArray, pixelsGrass, resolution, id);

            // 2. GPU Carving Pass: Render overlapping models top-down into the grass texture slice
            CarveGrassOnGPU(chunkPos, chunkSize, id, spatialGrid, resolution);

            return id;
        }

        private void UploadSliceData(int textureArray, float[] pixels, Vector2i resolution, int id)
        {
            int tempTex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tempTex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, resolution.X, resolution.Y, 0, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);
            GL.CopyImageSubData(tempTex, ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, id, resolution.X, resolution.Y, 1);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            GL.DeleteTexture(tempTex);
        }

        private void CarveGrassOnGPU(Vector2 chunkPos, Vector2 chunkSize, int arrayID, SpatialGrid spatialGrid, Vector2i resolution)
        {
            // Query spatial grid for entities in this chunk column
            AABB chunkColumnBounds = new AABB(
                new Vector3(chunkPos.X, -10000f, chunkPos.Y),
                new Vector3(chunkPos.X + chunkSize.X, 10000f, chunkPos.Y + chunkSize.Y)
            );

            List<Entity> nearbyEntities = new List<Entity>();
            spatialGrid.QueryAABB(chunkColumnBounds, nearbyEntities);

            if (nearbyEntities.Count == 0) return; // Nothing to carve
            System.Diagnostics.Debug.WriteLine($"Chunk at {chunkPos} found {nearbyEntities.Count} entities to carve.");

            // Bind FBO and attach the specific grass texture array slice
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, carveFBO);
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, grassTextureArray, 0, arrayID);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            var status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

            GL.Viewport(0, 0, resolution.X, resolution.Y);
            GL.ColorMask(true, false, false, false);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.SrcColor); // Or overwrite directly

            _grassCarveShader.bind();
            _grassCarveShader.loadUniformInt("normalHeightTextureArray", 0);
            _grassCarveShader.loadUniformFloat("chunkArrayID", arrayID);
            _grassCarveShader.loadUniformVector2f("chunkWorldPos", chunkPos);
            _grassCarveShader.loadUniformVector2f("chunkSize", chunkSize);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);

            // Orthographic projection looking straight down (-Y axis) over the chunk
            Matrix4 orthoProj = Matrix4.CreateOrthographicOffCenter(
                chunkPos.X,
                chunkPos.X + chunkSize.X,
                chunkPos.Y,              // <-- Swap: put top coordinate here
                chunkPos.Y + chunkSize.Y, // <-- Swap: put bottom coordinate here
                -1000f, 1000f
            );
            _grassCarveShader.loadUniformMatrix4f("orthoProjectionView", orthoProj);

            // Draw each overlapping model's mesh
            ECSWorld world = Engine.Instance.world;

            foreach (var entity in nearbyEntities)
            {
                // Assuming your entities have a Model component and a Transform/Matrix component
                var model = world.GetComponent<ModelComponent>(entity);
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(1);
                GL.EnableVertexAttribArray(2);
                var matrix = world.GetComponent<LocalToWorldMatrixComponent>(entity).value;
                GL.BindVertexArray(model.model.getVAOID());
                _grassCarveShader.loadUniformMatrix4f("modelMatrix", matrix);
                GL.DrawElements(PrimitiveType.Triangles, model.model.getVertexCount(), DrawElementsType.UnsignedInt, 0);
            }
                
            GL.Disable(EnableCap.Blend);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.ColorMask(true, true, true, true);
        }


        public override void CleanUp()
        {
            _terrainShader.cleanUp();
            _terrainShadowShader.cleanUp();
        }

        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);
            _terrainShader.bind();

            _terrainShader.loadUniformFloat("textureTileSize", 4.0f);

            _terrainShader.loadUniformBool("DEBUG_VIEW", false);
            _terrainShader.loadUniformFloat("textureMapOffset", (1.0f / (CHUNK_RESOLUTION)));
            _terrainShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoTextureArray);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalTextureArray);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialTextureArray);

            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaAlbedoModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaNormalModelTextureArray);
            GL.ActiveTexture(TextureUnit.Texture5);
            GL.BindTexture(TextureTarget.Texture2DArray, renderEngine.textureGenerator.megaMaterialModelTextureArray);

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);
            GL.ActiveTexture(TextureUnit.Texture7);
            GL.BindTexture(TextureTarget.Texture2DArray, grassTextureArray);

            _terrainShader.loadUniformVector3f("viewPos", renderEngine.context.viewPos);

            _terrainShader.loadUniformFloat("groundID", TextureGenerator.soil);
            _terrainShader.loadUniformFloat("grassID", TextureGenerator.grass);
            _terrainShader.loadUniformFloat("rockID", TextureGenerator.rock);
            _terrainShader.loadUniformFloat("roadID", TextureGenerator.cobble); 
            _terrainShader.loadUniformFloat("beachID", TextureGenerator.sandDunes);
            _terrainShader.loadUniformFloat("transitionID", TextureGenerator.crackedDesert);
            //_terrainShader.loadUniformFloat("groundID", Engine.RenderEngine.textureGenerator.flat);
            //_terrainShader.loadUniformFloat("rockID", Engine.RenderEngine.textureGenerator.flat);

            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(5);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            Matrix4 projectionViewMatrix = renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
            //_terrainShader.loadUniformMatrix4f("invViewMatrix", renderEngine.context.invViewMatrix);
            _terrainShader.loadUniformMatrix4f("projectionViewMatrix", projectionViewMatrix);
        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.BindVertexArray(0);
        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.CullFace(CullFaceMode.Front);

            _terrainShadowShader.bind();

            _terrainShader.loadUniformFloat("textureMapOffset", (1.0f / (CHUNK_RESOLUTION)));

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);

            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {
            GL.Disable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
        }

        internal override void PerformGeometryCommand(TerrainRenderCommand command, RenderEngine renderEngine)
        {
            _terrainShader.loadUniformFloat("parallaxDepth", command.parallaxDepth);
            _terrainShader.loadUniformFloat("parallaxDepth", 3.0f);
            _terrainShader.loadUniformFloat("parallaxLayers", 24);

            int numberOfChunks = command.chunks.Length;

            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer,
                numberOfChunks * Marshal.SizeOf<TerrainChunkRenderData>(),
                command.chunks,
                BufferUsageHint.DynamicDraw);


            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                baseChunkModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                numberOfChunks
            );
        }

        internal override void PerformShadowCommand(TerrainRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            int numberOfChunks = command.chunks.Length;

            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);
            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }
            GL.PolygonOffset(shadow.polygonOffsetTerrain, shadow.polygonOffsetTerrain * 10.1f);
            //GL.PolygonOffset(-10f, 1f);

            Matrix4 projectionViewMatrix = shadow.lightViewMatrix * shadow.shadowProjectionMatrix;
            _terrainShadowShader.loadUniformMatrix4f("projectionViewMatrix", projectionViewMatrix);

            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer,
                numberOfChunks * Marshal.SizeOf<TerrainChunkRenderData>(),
                command.chunks,
                BufferUsageHint.DynamicDraw);


            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                baseChunkModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                numberOfChunks
            );
        }
    }
}
