using Dino_Engine.Core;
using Dino_Engine.ECS;
using Dino_Engine.ECS.Components;
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
using static Dino_Engine.Textures.ProceduralTextureRenderer;

namespace Dino_Engine.Rendering.Renderers.Geometry
{

    public struct BlastData
    {
        public Vector2 center;
        public float radius;
        public float power;
        public float exponent;
    }

    public struct GrassChunkRenderData
    {
        public Vector2 chunkPos;
        public float size;
        public float arrayID;
    }

    public struct GrassRenderCommand : IRenderCommand
    {
        public GrassChunkRenderData[] chunks;
        public int LOD;

        public GrassRenderCommand(GrassChunkRenderData[] chunks, int lod)
        {
            this.chunks = chunks;
            this.LOD = lod;
        }
    }

    public class GrassRenderer : GeometryCommandDrivenRenderer<GrassRenderCommand>
    {

        private ShaderProgram _grassShader = new ShaderProgram("Grass.vert", "Grass.frag");
        private ShaderProgram _grassShadowShader = new ShaderProgram("Grass_Shadow.vert", "Grass_Shadow.frag");
        private ShaderProgram _grassSimulationShader = new ShaderProgram("Simple.vert", "Grass_Simulation.frag");
        private ShaderProgram _grassBlastShader = new ShaderProgram("Simple.vert", "Grass_Blast.frag");
        private ShaderProgram _grassDisplaceShader = new ShaderProgram("Simple.vert", "Grass_Displace.frag");

        private Vector2 simulationWorldSize = new Vector2(100f, 100f);
        private Vector2 simulationWorldPosition = new Vector2(float.MinValue, float.MinValue);

        private FrameBuffer _buffer1;
        private FrameBuffer _buffer2;
        private bool _toggle = false;

        private glModel grassBladeLOD0;
        private glModel grassBladeLOD1;
        private glModel grassBladeShadow;

        private int grassNoiseTexture;

        public static readonly int MAX_GRASS_CHUNKS = 1024;
        private int bladesPerAxis = 70;
        private float radiusBase = 0.02f;
        private float radiusTop = 0.01f;
        private float bladeHeight = 1.0f;

        public List<BlastData> blasts = new List<BlastData>();

        private int chunkUBO;

        public GrassRenderer() : base("Grass")
        {
            _grassShader.bind();


            _grassShader.loadUniformInt("albedoMapTextureArray", 0);
            _grassShader.loadUniformInt("normalMapTextureArray", 1);
            _grassShader.loadUniformInt("materialMapTextureArray", 2);

            _grassShader.loadUniformInt("albedoMapModelTextureArray", 3);
            _grassShader.loadUniformInt("normalMapModelTextureArray", 4);
            _grassShader.loadUniformInt("materialMapModelTextureArray", 5);

            _grassShader.loadUniformInt("heightmaps", 6);
            _grassShader.loadUniformInt("grassNoise", 7);
            _grassShader.loadUniformInt("bendMap", 8);

            _grassShader.unBind();

            _grassShadowShader.bind();
            _grassShadowShader.loadUniformInt("heightmaps", 0);
            _grassShadowShader.loadUniformInt("grassNoise", 1);
            _grassShadowShader.loadUniformInt("bendMap", 8);
            _grassShadowShader.unBind();

            grassBladeLOD0 = generateBladeModelLOD0();
            grassBladeLOD1 = generateBladeModelLOD1();
            generateBladeModelShadow();

            FrameBufferSettings frameBufferSettings = new FrameBufferSettings(new Vector2i(1024));
            DrawBufferSettings drawBufferSettings = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            drawBufferSettings.wrapMode = TextureWrapMode.ClampToBorder;
            drawBufferSettings.pixelType = PixelType.Float;
            drawBufferSettings.formatInternal = PixelInternalFormat.Rgba16f;
            drawBufferSettings.magFilterType = TextureMagFilter.Nearest;
            drawBufferSettings.minFilterType = TextureMinFilter.Nearest;
            frameBufferSettings.drawBuffers.Add(drawBufferSettings);
            _buffer1 = new FrameBuffer(frameBufferSettings);
            _buffer2 = new FrameBuffer(frameBufferSettings);
            _toggle = true;
            _grassSimulationShader.bind();
            _grassSimulationShader.loadUniformInt("lastTexture", 0);
            _grassSimulationShader.unBind();

            _grassDisplaceShader.bind();
            _grassDisplaceShader.loadUniformInt("lastTexture", 0);
            _grassDisplaceShader.unBind();

            _grassBlastShader.bind();
            _grassBlastShader.loadUniformInt("lastTexture", 0);
            _grassBlastShader.unBind();

            chunkUBO = GL.GenBuffer();
            int uboSize = MAX_GRASS_CHUNKS * (4 * 4); // vec4 = 16 bytes, no padding
            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferData(BufferTarget.UniformBuffer, uboSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 5, chunkUBO);


            grassNoiseTexture = generateGrassNoiseTexture();
        }




        private int generateGrassNoiseTexture()
        {


            var small = TextureGenerator.procTextGen.Voronoi(new Vector2(170f, 170f), jitter: 1.0f, phase: 1.0f, returnMode: ReturnMode.Height);
            var big = TextureGenerator.procTextGen.PerlinFBM(new Vector2(17f, 17f), octaves: 4);

            small.scaleHeight(0.6f);
            small.addHeight(0.4f);

            //big.scaleHeight(1.0f);
            //big.addHeight(0.0f);

            var final = TextureGenerator.MaterialLayersCombiner.combine(small, big, MaterialLayersManipulator.FilterMode.Everywhere, MaterialLayersManipulator.Operation.Scale);


            int texture = final.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment2);
            TextureGenerator.MaterialLayerHandler.cleanUp();

            return texture;


        }


        public void StepSimulation(ScreenQuadRenderer renderer, float time, float delta, Vector2 newCenter)
        {
            // 1. Determine the grid cell of the camera
            float textureResolution = 512f;

            // Calculate exactly how big one pixel is in world coordinates
            Vector2 texelSize = new Vector2(
                simulationWorldSize.X / textureResolution,
                simulationWorldSize.Y / textureResolution
            );

            // Find the target bottom corner position relative to the player/camera center
            Vector2 targetBottomCorner = newCenter - (simulationWorldSize * 0.5f);

            // Snap that bottom corner to the nearest precise pixel boundary
            Vector2 newAnchor = new Vector2(
                (float)Math.Floor(targetBottomCorner.X / texelSize.X) * texelSize.X,
                (float)Math.Floor(targetBottomCorner.Y / texelSize.Y) * texelSize.Y
            );

            Vector2 deltaPosition = Vector2.Zero;

            // Only shift if we crossed into a new pixel boundary
            if (simulationWorldPosition.X != float.MinValue && newAnchor != simulationWorldPosition)
            {
                // (New - Old) gives the precise distance the anchor stepped
                deltaPosition = newAnchor - simulationWorldPosition;
            }

            simulationWorldPosition = newAnchor;

            // 3. Render setup
            GetNextFrameBuffer().bind();
            _grassSimulationShader.bind();

            float windStrength = 2.0f; // 0.0 = dead calm, 1.0 = raging storm
            Vector2 windDirection = new Vector2(1.0f, 1.0f).Normalized();

            _grassSimulationShader.loadUniformFloat("globalWindStrength", windStrength);
            _grassSimulationShader.loadUniformVector2f("windDirection", windDirection);
            _grassSimulationShader.loadUniformVector2f("deltaPosition", deltaPosition);

            _grassSimulationShader.loadUniformFloat("regenTime", 13.8f);
            _grassSimulationShader.loadUniformFloat("time", time);
            _grassSimulationShader.loadUniformFloat("delta", delta);
            _grassSimulationShader.loadUniformVector2f("simulationWorldSize", simulationWorldSize);
            _grassSimulationShader.loadUniformVector2f("simulationWorldPosition", simulationWorldPosition);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));

            renderer.Render();

            blast(renderer);
            StepToggle();
        }
        private glModel generateBladeModelLOD0()
        {
            VertexMaterial grassMaterial = new VertexMaterial(TextureGenerator.brick); //Throwaway

            if (grassBladeLOD0 != null) grassBladeLOD0.cleanUp();
            List<Vector2> bladeLayers = new List<Vector2>() {
                new Vector2(radiusBase, 0),
                new Vector2(MyMath.lerp(radiusBase, radiusTop, 0.33f), bladeHeight*0.33f),
                new Vector2(MyMath.lerp(radiusBase, radiusTop, 0.66f), bladeHeight*0.66f),
                new Vector2(radiusTop, bladeHeight*0.95f),
                new Vector2(0.0001f, bladeHeight)};
            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 4, grassMaterial);
            bladeMesh.scale(new Vector3(1f, 1f, 0.3f));

            bladeMesh.makeFlat(true, false);
            return glLoader.loadToVAO(bladeMesh);
        }
        
        private void generateBladeModelShadow()
        {
            if (grassBladeShadow != null) grassBladeShadow.cleanUp();


            float[] positions = {
                -radiusBase, 0, 0,
                radiusBase, 0, 0,
                -radiusTop, bladeHeight, 0,
                radiusTop, bladeHeight, 0
            };

            float roundness = 0.5f;
            Vector3 NL = Vector3.Normalize(new Vector3(-roundness, 0, 1));
            Vector3 NR = Vector3.Normalize(new Vector3(roundness, 0, 1));

            float[] normals = {
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
            };

            int[] indices = {
                0, 1, 2,
                1, 3, 2
            };

            grassBladeShadow = glLoader.loadToVAO(positions, normals, indices);
        }


        private glModel generateBladeModelLOD1()
        {
            if (grassBladeLOD1 != null) grassBladeLOD1.cleanUp();


            float[] positions = {
                -radiusBase, 0, 0,
                0, 0, 0,
                radiusBase, 0, 0,
                -radiusTop, bladeHeight, 0,
                radiusTop, bladeHeight, 0
            };

            float roundness = 0.5f;
            Vector3 NL = Vector3.Normalize(new Vector3(-roundness, 0, 1));
            Vector3 NR = Vector3.Normalize(new Vector3(roundness, 0, 1));
            Vector3 N = Vector3.Normalize(new Vector3(0, 0, 1));

            float[] normals = {
                NL.X, NL.Y, NL.Z,
                N.X, N.Y, N.Z,
                NR.X, NR.Y, NR.Z,
                NL.X, NL.Y, NL.Z,
                NR.X, NR.Y, NR.Z,
            };

            int[] indices = {
                0, 1, 3,
                1, 4, 3,
                1, 2, 4
            };

            return glLoader.loadToVAO(positions, normals, indices);
        }

        public override void Update()
        {
            bladesPerAxis = 50;

            bladeHeight =2.0f;
            radiusBase = 0.005f;
            radiusTop = radiusBase * 0.6f;

            var world = Engine.Instance.world;
            Vector3 cameraPos = world.GetComponent<LocalToWorldMatrixComponent>(world.Camera).value.ExtractTranslation();
            StepSimulation(Engine.RenderEngine.ScreenQuadRenderer, Engine.Time, Engine.Delta, cameraPos.Xz);
        }

        public override void CleanUp()
        {
            GL.DeleteTexture(grassNoiseTexture);
            _grassShader.cleanUp();
            GL.DeleteBuffer(chunkUBO);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public FrameBuffer GetNextFrameBuffer()
        {
            if (_toggle) return _buffer1;
            else return _buffer2;
        }
        public FrameBuffer GetLastFrameBuffer()
        {
            if (_toggle) return _buffer2;
            else return _buffer1;
        }
        public void StepToggle()
        {
            if (_toggle == true) _toggle = false;
            else _toggle = true;
        }


        public void blast(ScreenQuadRenderer renderer)
        {
            
            GetNextFrameBuffer().bind();
            _grassBlastShader.bind();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));
            foreach (BlastData blast in blasts)
            {
                _grassBlastShader.loadUniformFloat("power", blast.power);
                _grassBlastShader.loadUniformFloat("exponent", blast.exponent);
                _grassBlastShader.loadUniformVector2f("blastCenterWorld", blast.center);
                _grassBlastShader.loadUniformFloat("radius", blast.radius);
                _grassBlastShader.loadUniformVector2f("simulationWorldSize", simulationWorldSize);
                _grassBlastShader.loadUniformVector2f("simulationWorldPosition", simulationWorldPosition);
                renderer.Render(clearColor: false, blend: false);
                
            }
            blasts.Clear();
        }


        internal override void PrepareGeometry(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);


            generateBladeModelLOD0();
            generateBladeModelLOD1();
            generateBladeModelShadow();

            //StepSimulation(renderEngine.ScreenQuadRenderer);
            renderEngine.GBuffer.bind();

            _grassShader.bind();

            GL.ActiveTexture(TextureUnit.Texture6);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetNormalHeightTextureArray());

            GL.ActiveTexture(TextureUnit.Texture7);
            GL.BindTexture(TextureTarget.Texture2D, grassNoiseTexture);

            GL.ActiveTexture(TextureUnit.Texture8);
            GL.BindTexture(TextureTarget.Texture2D, GetNextFrameBuffer().GetAttachment(0));

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

            _grassShader.loadUniformInt("numberOfMaterials", renderEngine.textureGenerator.loadedMaterialTextures);

            _grassShader.loadUniformInt("textureIndex", TextureGenerator.grass);
            _grassShader.loadUniformFloat("groundNormalStrength", 0.0f);
            _grassShader.loadUniformFloat("groundNormalStrengthFlat", 0.0f);
            _grassShader.loadUniformFloat("colourError", 0.1f);
            _grassShader.loadUniformFloat("fakeAmbientOcclusionStrength", 0.1f);
            _grassShader.loadUniformFloat("fakeColorAmbientOcclusionStrength", 0.1f);
            _grassShader.loadUniformVector4f("grassMaterial", new Vector4(0.95f, 0f, 0.0f, 0.0f));
            _grassShader.loadUniformVector3f("baseColorAlive", new Colour(180, 180, 115).ToVector3());
            //_grassShader.loadUniformVector3f("baseColorAlive", new Colour(20, 50, 15).ToVector3());
            _grassShader.loadUniformVector3f("baseColorDead", new Colour(255, 154,130).ToVector3());
            //_grassShader.loadUniformVector3f("baseColor", new Colour(30, 11, 8).ToVector3());

        }

        internal override void FinishGeometry(RenderEngine renderEngine)
        {

        }

        internal override void PrepareShadow(RenderEngine renderEngine)
        {
            GL.Enable(EnableCap.DepthTest);

            GL.Enable(EnableCap.PolygonOffsetFill);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);



            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetNormalHeightTextureArray());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, grassNoiseTexture);

            GL.ActiveTexture(TextureUnit.Texture8);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));

            _grassShadowShader.bind();
            ShaderProgram shader = _grassShadowShader;
            for (int i = 0; i <2; i++)
            {
                shader.loadUniformFloat("bladeHeight", bladeHeight);
                shader.loadUniformFloat("bendyness", 0.05f);
                shader.loadUniformFloat("heightError", 0.35f);
                shader.loadUniformFloat("radiusError", 0.35f);
                shader.loadUniformFloat("cutOffThreshold", 0.1f);
                shader.loadUniformFloat("cutOffRange", 0.7f);
                shader.loadUniformFloat("steepnessCutoffStrength", .5f);
                shader.loadUniformFloat("textureMapOffset", 1f / TerrainRenderer.CHUNK_RESOLUTION);
                shader.loadUniformVector2f("simulationWorldSize", simulationWorldSize);
                shader.loadUniformVector2f("simulationWorldPosition", simulationWorldPosition);

                shader = _grassShader;
                shader.bind();
            }

            _grassShadowShader.bind();
        }

        internal override void FinishShadow(RenderEngine renderEngine)
        {

            GL.Disable(EnableCap.PolygonOffsetFill);
        }

        internal override void PerformGeometryCommand(GrassRenderCommand command, RenderEngine renderEngine)
        {
            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            _grassShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShader.loadUniformInt("bladesPerAxis", bladesPerAxis);

            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < command.chunks.Length; i++)
            {
                var chunkCommand = command.chunks[i];
                chunkData[i] = new Vector4(chunkCommand.chunkPos.X, chunkCommand.chunkPos.Y, chunkCommand.size, chunkCommand.arrayID);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes * MAX_GRASS_CHUNKS, chunkData);

            glModel grassBlade = grassBladeLOD0;
            GL.Enable(EnableCap.CullFace);
            if (command.LOD == 1)
            {
                _grassShader.loadUniformBool("isBillboard", true);
                grassBlade = grassBladeLOD1;
            } else
            {
                _grassShader.loadUniformBool("isBillboard", false);
            }

            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(2);
            //GL.EnableVertexAttribArray(4);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(5);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk * command.chunks.Length);

        }


        internal override void PerformShadowCommand(GrassRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            
            GL.PolygonOffset(shadow.polygonOffsetModel, shadow.polygonOffsetModel * 10.1f);
            //GL.PolygonOffset(1, 1);

            if (shadow.isCubeMap && shadow.cubemapFaceIndex >= 0)
            {
                shadow.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + shadow.cubemapFaceIndex);

            }
            else
            {
                shadow.shadowFrameBuffer.bind();
            }

            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            _grassShadowShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShadowShader.loadUniformInt("bladesPerAxis", bladesPerAxis);
            _grassShadowShader.loadUniformMatrix4f("shadowViewProjectionMatrix", shadow.lightViewMatrix*shadow.shadowProjectionMatrix);

            //  KINDA BAD TO INVERT HE VIEW MATRIX JUST TO GET THE POSITION....
            _grassShadowShader.loadUniformVector3f("lightPosWorld", shadow.lightViewMatrix.Inverted().ExtractTranslation());
            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < command.chunks.Length; i++)
            {
                if (i >= MAX_GRASS_CHUNKS - 1) {
                    Console.WriteLine("Warning: More chunks in command than MAX_GRASS_CHUNKS. Some chunks will not be rendered.");
                    continue;
                }
                var chunkCommand = command.chunks[i];
                chunkData[i] = new Vector4(chunkCommand.chunkPos.X, chunkCommand.chunkPos.Y, chunkCommand.size, chunkCommand.arrayID);
            }

            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes * MAX_GRASS_CHUNKS, chunkData);

            glModel grassBlade = grassBladeShadow;

            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk * command.chunks.Length);
            
        }
    }
}
