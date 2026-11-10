using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural;
using Dino_Engine.Modelling.Procedural.Terrain;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using Dino_Engine.World;
using Dino_Engine.World.Content;
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
        /// <summary>Mountains too far off to have chunks, that can still shade what's here. Only shadows have these.</summary>
        public TerrainChunkRenderData[]? farRock;

        public TerrainRenderCommand(TerrainChunkRenderData[] chunks, TerrainChunkRenderData[]? farRock = null)
        {
            this.chunks = chunks;
            this.farRock = farRock;
        }
    }

    public class TerrainRenderer : GeometryCommandDrivenRenderer<TerrainRenderCommand>
    {
        private ShaderProgram _terrainShader = new ShaderProgram("Terrain.vert", "Terrain.frag");
        private ShaderProgram _terrainShadowShader = new ShaderProgram("Terrain_Shadow.vert", "Terrain_Shadow.frag");
        private ShaderProgram _grassCarveShader = new ShaderProgram("GrassCarve.vert", "GrassCarve.frag");
        private ShaderProgram _farShadowShader = new ShaderProgram("Terrain_Far_Shadow.vert", "Terrain_Shadow.frag");

        private glModel baseChunkModel;
        private int normalRoadHeightTextureArray;
        private int grassTextureArray;
        private int grassColourTextureArray;
        private int grassBloomTextureArray;
        // Where each texel lies on the roads (World/RoadFrames.cs): metres across and along and the road's way, in full floats, as along runs to thousands of
        // metres; and the road's half width and how much of a junction it is.
        private int roadFrameTextureArray;
        private int roadInfoTextureArray;
        private readonly int[] surfaceWeightArrays = new int[3];
        private readonly Vector4[] surfaceMaterials = new Vector4[3];

        // What the ground of each live chunk is made of, a layer to a chunk (TerrainChunkComponent.normalHeightTextureArrayID), for
        // global illumination's voxels, which colour the ground as this does.
        public int SurfaceWeightArray(int index) => surfaceWeightArrays[index];
        public int GrassTextureArray => grassTextureArray;
        public int GrassColourTextureArray => grassColourTextureArray;
        public int NormalRoadHeightTextureArray => normalRoadHeightTextureArray;
        public Vector4[] SurfaceMaterials => surfaceMaterials;
        private IDAllocator<ushort> normalHeightTextureArrayAllocator = new();
        private readonly int MAX_TERRAIN_CHUNKS = 1024*2;
        public static readonly int CHUNK_RESOLUTION = 16;
        /// <summary>
        /// Metres Terrain.vert lifts the drawn surface above the height map (Terrain_shadow.vert, used for shadows and height
        /// captures, does not). Code that matches the visible ground adds it to TerrainGenerator heights.
        /// </summary>
        public const float SurfaceOffset = 0.15f;
        private int carveFBO;

        private int instanceVBO;

        private const int GeologyBinding = 10;
        // How fine the far mountains are, which only ever make shadows, in metres.
        private const float FarCell = 8f;
        private const float FarTile = 64f;
        private const int FarHeightUnit = 7;

        private int farHeightMap;
        private Vector2 farHeightMapping;
        private readonly List<(TerrainChunkRenderData tile, float lowest, float highest)> farTiles = new();

        private const int GeologyHeader = 12;
        private const int GeologyDataBytes = GeologyHeader * sizeof(float) + Geology.Steps * 4;
        private int geologyBuffer;

        public TerrainRenderer() : base("Terrain")
        {
            _terrainShader.bind();
            _terrainShader.loadUniformInt("albedoMapTextureArray", 0);
            _terrainShader.loadUniformInt("normalMapTextureArray", 1);
            _terrainShader.loadUniformInt("materialMapTextureArray", 2);

            // The ground reads no cards, so the units their arrays would take carry the roads instead: a stage is only promised sixteen.
            _terrainShader.loadUniformInt("roadFrameTextureArray", 3);
            _terrainShader.loadUniformInt("roadInfoTextureArray", 4);

            _terrainShader.loadUniformInt("normalHeightTextureArray", 6);
            _terrainShader.loadUniformInt("grassTextureArray", 7);
            _terrainShader.loadUniformInt("grassColourTextureArray", 8);
            _terrainShader.loadUniformInt("surfaceWeightsA", 9);
            _terrainShader.loadUniformInt("surfaceWeightsB", 10);
            _terrainShader.loadUniformInt("surfaceWeightsC", 11);
            Simulation.WeatherRenderer.LinkSurfaceSamplers(_terrainShader);

            _terrainShader.unBind();


            _terrainShadowShader.bind();
            _terrainShadowShader.loadUniformInt("normalHeightTextureArray", 6);
            _terrainShadowShader.unBind();

            _farShadowShader.bind();
            _farShadowShader.loadUniformInt("farHeightMap", FarHeightUnit);
            _farShadowShader.unBind();


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


            // Full floats, as heights are absolute: at the top of a mountain a half float only comes in steps of a quarter of a metre.
            normalRoadHeightTextureArray = CreateChunkTextureArray(SizedInternalFormat.Rgba32f);
            grassTextureArray = CreateChunkTextureArray();
            grassColourTextureArray = CreateChunkTextureArray();
            grassBloomTextureArray = CreateChunkTextureArray();
            roadFrameTextureArray = CreateChunkTextureArray(SizedInternalFormat.Rgba32f);
            roadInfoTextureArray = CreateChunkTextureArray();
            for (int i = 0; i < surfaceWeightArrays.Length; i++) surfaceWeightArrays[i] = CreateChunkTextureArray();


            carveFBO = GL.GenFramebuffer();

            geologyBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, geologyBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, GeologyDataBytes, IntPtr.Zero, BufferUsageHint.StaticDraw);
            // With no world yet, no material is rock: item pictures are drawn before there's a world, and a 0 here is a real material.
            float[] nothing = new float[GeologyHeader];
            nothing[7] = nothing[11] = -10f;
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, nothing.Length * sizeof(float), nothing);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, GeologyBinding, geologyBuffer);
        }

        /// <summary>
        /// Keeps the shape of a world's mountains for the sun, which sees further than the terrain goes: a wall past the last chunk still throws its shadow in, and with no chunks there to cast it,
        /// it would appear all at once as they arrive. Each height is the lowest for some way around, so that this always lies under the real ground and can't shade it.
        /// </summary>
        public void SetFarRock(WorldData world)
        {
            ScalarField ground = world.Height;
            int nodes = (int)MathF.Ceiling(world.Size / FarCell) + 1;
            float[] heights = new float[nodes * nodes];
            Parallel.For(0, nodes, z =>
            {
                for (int x = 0; x < nodes; x++)
                {
                    float lowest = float.MaxValue;
                    for (float dz = -FarCell; dz <= FarCell; dz += FarCell * 0.5f)
                    {
                        for (float dx = -FarCell; dx <= FarCell; dx += FarCell * 0.5f)
                        {
                            lowest = MathF.Min(lowest, ground.Sample(x * FarCell + dx, z * FarCell + dz));
                        }
                    }
                    heights[z * nodes + x] = lowest - 0.5f;
                }
            });

            if (farHeightMap == 0) farHeightMap = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, farHeightMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, nodes, nodes, 0, PixelFormat.Red, PixelType.Float, heights);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            farHeightMapping = new Vector2(1f / (FarCell * nodes), 0.5f / nodes);

            // Only where rock stands: nothing on open land is tall enough to reach this far.
            farTiles.Clear();
            int tiles = (int)MathF.Ceiling(world.Size / FarTile);
            for (int tz = 0; tz < tiles; tz++)
            {
                for (int tx = 0; tx < tiles; tx++)
                {
                    bool rock = false;
                    float lowest = float.MaxValue, highest = float.MinValue;
                    for (float z = tz * FarTile; z <= (tz + 1) * FarTile; z += FarCell)
                    {
                        for (float x = tx * FarTile; x <= (tx + 1) * FarTile; x += FarCell)
                        {
                            rock |= world.Rock.Sample(x, z) > 0.5f;
                            float h = ground.Sample(x, z);
                            lowest = MathF.Min(lowest, h);
                            highest = MathF.Max(highest, h);
                        }
                    }
                    if (!rock) continue;
                    farTiles.Add((new TerrainChunkRenderData { chunkPos = new Vector3(tx * FarTile, 0f, tz * FarTile), size = new Vector3(FarTile, 1f, FarTile) }, lowest, highest));
                }
            }
        }

        /// <summary>The far mountains that fall within a shadow map.</summary>
        public void CollectFarRock(Util.Frustum frustum, List<TerrainChunkRenderData> into)
        {
            into.Clear();
            foreach ((TerrainChunkRenderData tile, float lowest, float highest) in farTiles)
            {
                Vector3 min = new Vector3(tile.chunkPos.X, lowest, tile.chunkPos.Z);
                Vector3 max = new Vector3(tile.chunkPos.X + FarTile, highest, tile.chunkPos.Z + FarTile);
                if (frustum.IntersectsAABB(new Physics.AABB(min, max)) != Util.IntersectionResult.Outside) into.Add(tile);
            }
        }

        /// <summary>Tells every shader that reads Rock.glsl what a world's rock is. <see cref="GeologyData"/> there has to match this.</summary>
        public void SetGeology(Geology geology)
        {
            float[] header = new float[GeologyHeader];
            header[0] = geology.Dip.X;
            header[1] = geology.Dip.Y;
            header[2] = Geology.Bottom;
            header[3] = Geology.Steps / (Geology.Top - Geology.Bottom);
            header[4] = geology.Phase.X;
            header[5] = geology.Phase.Y;
            header[6] = geology.Phase.Z;
            header[7] = TextureGenerator.cliff;

            // Its hue and not its brightness, as gravel has to go on looking like gravel.
            Vector3 hue = geology.Colour / MathF.Max((geology.Colour.X + geology.Colour.Y + geology.Colour.Z) / 3f, 0.01f);
            header[8] = hue.X;
            header[9] = hue.Y;
            header[10] = hue.Z;
            header[11] = TextureGenerator.gravel;

            GL.BindBuffer(BufferTarget.UniformBuffer, geologyBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, GeologyHeader * sizeof(float), header);
            GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(GeologyHeader * sizeof(float)), geology.Layers.Length, geology.Layers);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        private int CreateChunkTextureArray(SizedInternalFormat format = SizedInternalFormat.Rgba16f)
        {
            int array = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, array);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, 1, format, CHUNK_RESOLUTION, CHUNK_RESOLUTION, MAX_TERRAIN_CHUNKS);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return array;
        }

        /// <summary>Tells the terrain which materials a world's surface weights stand for, in the order of its palette.</summary>
        public void SetSurfacePalette(IReadOnlyList<SurfaceMaterial> palette)
        {
            float[] ids = new float[TerrainSurface.MaxMaterials];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = TextureOf(i < palette.Count ? palette[i] : SurfaceMaterial.Rock);
            }
            for (int i = 0; i < surfaceMaterials.Length; i++)
            {
                surfaceMaterials[i] = new Vector4(ids[i * 4], ids[i * 4 + 1], ids[i * 4 + 2], ids[i * 4 + 3]);
            }
        }

        private static int TextureOf(SurfaceMaterial material) => material switch
        {
            SurfaceMaterial.Soil => TextureGenerator.soil,
            SurfaceMaterial.Grass => TextureGenerator.grass,
            SurfaceMaterial.GrassySoil => TextureGenerator.grassy_soil,
            SurfaceMaterial.Rock => TextureGenerator.cliff,
            SurfaceMaterial.Sand => TextureGenerator.sand,
            SurfaceMaterial.Dunes => TextureGenerator.sandDunes,
            SurfaceMaterial.CrackedEarth => TextureGenerator.crackedDesert,
            SurfaceMaterial.Ash => TextureGenerator.crackedLava,
            SurfaceMaterial.Snow => TextureGenerator.snow,
            SurfaceMaterial.Ice => TextureGenerator.ice,
            SurfaceMaterial.Mud => TextureGenerator.mud,
            SurfaceMaterial.Moss => TextureGenerator.moss,
            SurfaceMaterial.Gravel => TextureGenerator.gravel,
            SurfaceMaterial.Cobble => TextureGenerator.cobble,
            SurfaceMaterial.Dirt or SurfaceMaterial.Road => TextureGenerator.roadDirt,
            SurfaceMaterial.Setts => TextureGenerator.setts,
            SurfaceMaterial.Farmland => TextureGenerator.farmland,
            SurfaceMaterial.Flagstones => TextureGenerator.flagstones,
            _ => TextureGenerator.soil,
        };

        public int GetNormalRoadHeightTextureArray()
        {
            return normalRoadHeightTextureArray;
        }
        public int GetGrassTextureArray()
        {
            return grassTextureArray;
        }
        public int GetGrassColourTextureArray()
        {
            return grassColourTextureArray;
        }
        public int GetGrassBloomTextureArray()
        {
            return grassBloomTextureArray;
        }

        public void freeChunk(int chunk)
        {
            normalHeightTextureArrayAllocator.Release((ushort)chunk);
        }

        public int insertDataAndCarveChunk(FloatGrid heightGrid, Vector3Grid normalGrid, TerrainChunkSurface surface, Vector2 chunkPos, Vector2 chunkSize, SpatialGrid spatialGrid)
        {
            int id = (int)normalHeightTextureArrayAllocator.Allocate();
            var resolution = heightGrid.Resolution;
            int dimensions = 4;

            // 1. Upload the chunk's CPU data into its slice of the texture arrays
            var pixelsNormalRoadHeight = new float[dimensions * resolution.X * resolution.Y];

            for (int y = 0; y < resolution.Y; y++)
            {
                for (int x = 0; x < resolution.X; x++)
                {
                    int i = y * resolution.X + x;
                    pixelsNormalRoadHeight[i * dimensions + 0] = normalGrid.Values[x, y].X;
                    pixelsNormalRoadHeight[i * dimensions + 1] = normalGrid.Values[x, y].Y;
                    pixelsNormalRoadHeight[i * dimensions + 2] = normalGrid.Values[x, y].Z;
                    pixelsNormalRoadHeight[i * dimensions + 3] = heightGrid.Values[x, y];
                }
            }

            UploadSliceData(normalRoadHeightTextureArray, pixelsNormalRoadHeight, resolution, id);
            UploadSliceData(grassTextureArray, surface.Grass, resolution, id);
            UploadSliceData(grassColourTextureArray, surface.GrassColour, resolution, id);
            UploadSliceData(grassBloomTextureArray, surface.GrassBloom, resolution, id);
            UploadSliceData(roadFrameTextureArray, surface.RoadFrame, resolution, id);
            UploadSliceData(roadInfoTextureArray, surface.RoadInfo, resolution, id);
            for (int i = 0; i < surfaceWeightArrays.Length; i++)
            {
                UploadSliceData(surfaceWeightArrays[i], surface.Weights[i], resolution, id);
            }

            // 2. GPU Carving Pass: Render overlapping models top-down into the grass texture slice
            CarveGrassOnGPU(chunkPos, chunkSize, id, spatialGrid, resolution);

            return id;
        }

        private void UploadSliceData(int textureArray, float[] pixels, Vector2i resolution, int id)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);
            GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, id, resolution.X, resolution.Y, 1, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
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

            CarveGrass(arrayID, chunkPos, chunkSize, resolution, nearbyEntities);
        }

        /// <summary>
        /// Carves the grass under <paramref name="entities"/> into an already created chunk. Chunks only
        /// carve what exists when they are created, so this covers entities that appear afterwards.
        /// </summary>
        public void CarveGrass(int arrayID, Vector2 chunkPos, Vector2 chunkSize, Vector2i resolution, List<Entity> entities)
        {
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
            _grassCarveShader.loadUniformFloat("firstCard", Engine.RenderEngine.textureGenerator.loadedMaterialTextures);

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

            foreach (var entity in entities)
            {
                // The grid only hears of what is gone when it next updates, and terrain streaming applies deferred commands in
                // between. So what it hands out can have been destroyed a moment ago, or its id be something else's by now.
                if (!world.HasComponent<ModelComponent>(entity)) continue;

                // Assuming your entities have a Model component and a Transform/Matrix component
                var model = world.GetComponent<ModelComponent>(entity);
                var matrix = world.GetComponent<LocalToWorldMatrixComponent>(entity).value;
                GL.BindVertexArray(model.model.getVAOID());
                GL.EnableVertexAttribArray(0);
                GL.EnableVertexAttribArray(5);
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
            _farShadowShader.cleanUp();
            GL.DeleteBuffer(geologyBuffer);
            if (farHeightMap != 0) GL.DeleteTexture(farHeightMap);
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
            GL.BindTexture(TextureTarget.Texture2DArray, roadFrameTextureArray);
            GL.ActiveTexture(TextureUnit.Texture4);
            GL.BindTexture(TextureTarget.Texture2DArray, roadInfoTextureArray);
            _terrainShader.loadUniformFloat("roadMaterial", TextureGenerator.roadDirt);
            _terrainShader.loadUniformFloat("settsMaterial", TextureGenerator.setts);

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);
            GL.ActiveTexture(TextureUnit.Texture7);
            GL.BindTexture(TextureTarget.Texture2DArray, grassTextureArray);
            GL.ActiveTexture(TextureUnit.Texture8);
            GL.BindTexture(TextureTarget.Texture2DArray, grassColourTextureArray);
            GL.ActiveTexture(TextureUnit.Texture9);
            GL.BindTexture(TextureTarget.Texture2DArray, surfaceWeightArrays[0]);
            GL.ActiveTexture(TextureUnit.Texture10);
            GL.BindTexture(TextureTarget.Texture2DArray, surfaceWeightArrays[1]);
            GL.ActiveTexture(TextureUnit.Texture11);
            GL.BindTexture(TextureTarget.Texture2DArray, surfaceWeightArrays[2]);
            GL.ActiveTexture(TextureUnit.Texture0);

            _terrainShader.loadUniformVector3f("viewPos", renderEngine.context.viewPos);
            _terrainShader.loadUniformVector4fArray("surfaceMaterials", surfaceMaterials);

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

            _terrainShadowShader.loadUniformFloat("textureMapOffset", (1.0f / (CHUNK_RESOLUTION)));

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
            // Each material's own relief, from the material table (Surface/MaterialTable.glsl).
            _terrainShader.loadUniformFloat("reliefScale", renderEngine.ReliefScale);

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

        /// <summary>
        /// Renders the terrain surface of <paramref name="chunks"/> into the currently bound depth target with
        /// both faces and no depth bias, for top-down height captures (see VolumetricFogRenderer).
        /// </summary>
        public void RenderHeightCapture(TerrainChunkRenderData[] chunks, Matrix4 projectionViewMatrix)
        {
            if (chunks.Length == 0) return;

            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);

            _terrainShadowShader.bind();
            _terrainShadowShader.loadUniformFloat("textureMapOffset", (1.0f / (CHUNK_RESOLUTION)));
            _terrainShadowShader.loadUniformMatrix4f("projectionViewMatrix", projectionViewMatrix);

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, normalRoadHeightTextureArray);

            GL.BindVertexArray(baseChunkModel.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(3);
            GL.EnableVertexAttribArray(4);
            GL.EnableVertexAttribArray(5);

            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVBO);
            GL.BufferData(BufferTarget.ArrayBuffer,
                chunks.Length * Marshal.SizeOf<TerrainChunkRenderData>(),
                chunks,
                BufferUsageHint.DynamicDraw);

            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                baseChunkModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                chunks.Length
            );

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
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

            if (command.farRock == null || command.farRock.Length == 0) return;

            _farShadowShader.bind();
            _farShadowShader.loadUniformMatrix4f("projectionViewMatrix", projectionViewMatrix);
            _farShadowShader.loadUniformVector2f("farHeightMapping", farHeightMapping);
            GL.ActiveTexture(TextureUnit.Texture0 + FarHeightUnit);
            GL.BindTexture(TextureTarget.Texture2D, farHeightMap);

            GL.BufferData(BufferTarget.ArrayBuffer,
                command.farRock.Length * Marshal.SizeOf<TerrainChunkRenderData>(),
                command.farRock,
                BufferUsageHint.DynamicDraw);
            GL.DrawElementsInstanced(
                PrimitiveType.Triangles,
                baseChunkModel.getVertexCount(),
                DrawElementsType.UnsignedInt,
                IntPtr.Zero,
                command.farRock.Length
            );

            GL.ActiveTexture(TextureUnit.Texture0);
            _terrainShadowShader.bind();
        }
    }
}
