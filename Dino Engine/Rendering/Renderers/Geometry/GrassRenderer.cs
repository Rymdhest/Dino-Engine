using Dino_Engine.ECS;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;
using Dino_Engine.Physics;
using Dino_Engine.Util.Data_Structures.Grids;
using static Dino_Engine.Textures.ProceduralTextureRenderer;
using Dino_Engine.Textures;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Modelling.Procedural;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
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

            _grassShader.unBind();

            _grassShadowShader.bind();
            _grassShadowShader.loadUniformInt("heightmaps", 0);
            _grassShadowShader.loadUniformInt("grassNoise", 1);
            _grassShadowShader.unBind();

            generateBladeModelLOD0();
            generateBladeModelLOD1();
            generateBladeModelShadow();

            FrameBufferSettings frameBufferSettings = new FrameBufferSettings(new Vector2i(600,600));
            frameBufferSettings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0));
            _buffer1 = new FrameBuffer(frameBufferSettings);
            _buffer2 = new FrameBuffer(frameBufferSettings);
            _toggle = true;
            _grassSimulationShader.bind();
            _grassSimulationShader.loadUniformInt("lastTexture", 0);
            _grassSimulationShader.unBind();

            _grassDisplaceShader.bind();
            _grassDisplaceShader.loadUniformInt("lastTexture", 0);
            _grassDisplaceShader.unBind();

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


        public void StepSimulation(ScreenQuadRenderer renderer)
        {
            displace(renderer);
            blast(renderer);

            GetNextFrameBuffer().bind();
            _grassSimulationShader.bind();
            _grassSimulationShader.loadUniformFloat("regenTime",13.8f);
            _grassSimulationShader.loadUniformVector2f("grassFieldSize", new Vector2(100f, 100f));
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));
            renderer.Render();

            StepToggle();
        }
        private void generateBladeModelLOD0()
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
            grassBladeLOD0 = glLoader.loadToVAO(bladeMesh);
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
        
        private void generateBladeModelLOD1()
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

            grassBladeLOD1 = glLoader.loadToVAO(positions, normals, indices);
        }

        public override void Update()
        {
            bladesPerAxis = 50;

            bladeHeight =0.5f;
            radiusBase = 0.015f;
            radiusTop = radiusBase * 0.4f;
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
            /*
            GetLastFrameBuffer().bind();
            _grassBlastShader.bind();
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            List<EntityOLD> blasts = Engine.Instance.ECSEngine.getSystem<GrassBlastSystem>().MemberEntities;
            List<EntityOLD> terrains = Engine.Instance.ECSEngine.getSystem<TerrainSystem>().MemberEntities;

            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                GrassBlastComponent blastComponent = blasts[i].getComponent<GrassBlastComponent>();
                Vector2 blastCenterWorldSpace = blasts[i].getComponent<TransformationComponent>().Transformation.position.Xz;
                _grassBlastShader.loadUniformFloat("power", blastComponent.power);
                _grassBlastShader.loadUniformFloat("exponent", blastComponent.exponent);
                foreach (EntityOLD terrain in terrains)
                {
                    Vector2 terrainPositionWorldSpace = terrain.getComponent<TransformationComponent>().Transformation.position.Xz;
                    Vector2 terrainSizeWorld = ((TerrainHitBox)terrain.getComponent<CollisionComponent>().HitBox)._max.Xz;
                    Vector2 blastCenterTerrainSpace = (blastCenterWorldSpace- terrainPositionWorldSpace);
                    _grassBlastShader.loadUniformVector2f("center", blastCenterTerrainSpace);
                    _grassBlastShader.loadUniformFloat("radius", blastComponent.radius);
                    _grassBlastShader.loadUniformVector2f("grassPatchSize", terrainSizeWorld);
                    renderer.Render(clearColor: false, blend: true);
                }
                blasts[i].CleanUp();
            }
            */
        }

        public void displace(ScreenQuadRenderer renderer)
        {
            /*
            _grassDisplaceShader.bind();

            List<EntityOLD> displacements = Engine.Instance.ECSEngine.getSystem<GrassInteractSystem>().MemberEntities;
            List<EntityOLD> terrains = Engine.Instance.ECSEngine.getSystem<TerrainSystem>().MemberEntities;


            for (int i = displacements.Count - 1; i >= 0; i--)
            {

                Vector3 blastCenterWorldSpace = displacements[i].getComponent<TransformationComponent>().Transformation.position;
                float radius = ((SphereHitbox)displacements[i].getComponent<CollisionComponent>().HitBox).Radius*1.0f;
                _grassDisplaceShader.loadUniformFloat("exponent", 1f);
                foreach (EntityOLD terrain in terrains)
                {

                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));
                    Vector3 terrainPositionWorldSpace = terrain.getComponent<TransformationComponent>().Transformation.position;
                    Vector2 terrainSizeWorld = ((TerrainHitBox)terrain.getComponent<CollisionComponent>().HitBox)._max.Xz;
                    Vector3 blastCenterTerrainSpace = (blastCenterWorldSpace - terrainPositionWorldSpace);

                    FloatGrid heightMap = terrain.getComponent<TerrainMapsComponent>().heightMap;
                    FloatGrid grassHeightMap = terrain.getComponent<TerrainMapsComponent>().grassMap;

                    Vector2 toResolutionSpace =  heightMap.Resolution/ terrainSizeWorld;
                    float terrainHeight = heightMap.BilinearInterpolate(blastCenterTerrainSpace.Xz* toResolutionSpace);

                    Vector2 toResolutionSpaceGrass = grassHeightMap.Resolution / terrainSizeWorld;
                    float grassHeight = bladeHeight * grassHeightMap.BilinearInterpolate(blastCenterTerrainSpace.Xz * toResolutionSpaceGrass);

                    float power =MyMath.clamp01( (grassHeight - (blastCenterTerrainSpace.Y - radius - terrainHeight)) / radius);
                    _grassDisplaceShader.loadUniformFloat("power", power*3);

                    _grassDisplaceShader.loadUniformVector2f("center", blastCenterTerrainSpace.Xz);
                    _grassDisplaceShader.loadUniformFloat("radius", radius*1.0f);
                    _grassDisplaceShader.loadUniformVector2f("grassPatchSize", terrainSizeWorld);
                    GetNextFrameBuffer().bind();
                    renderer.Render(clearColor: false, blend: false);
                    StepToggle();
                }
            }
            */
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
            _grassShader.loadUniformFloat("groundNormalStrength", 2.5f);
            _grassShader.loadUniformFloat("groundNormalStrengthFlat", 1.1f);
            _grassShader.loadUniformFloat("colourError", 0.1f);
            _grassShader.loadUniformFloat("fakeAmbientOcclusionStrength", 0.2f);
            _grassShader.loadUniformFloat("fakeColorAmbientOcclusionStrength", 0.3f);
            _grassShader.loadUniformVector4f("grassMaterial", new Vector4(0.8f, 0f, 0.0f, 1f));
            _grassShader.loadUniformVector3f("baseColorAlive", new Colour(180, 180, 115).ToVector3());
            _grassShader.loadUniformVector3f("baseColorDead", new Colour(555, 454, 600).ToVector3());
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

            _grassShadowShader.bind();
            ShaderProgram shader = _grassShadowShader;
            for (int i = 0; i <2; i++)
            {
                shader.loadUniformFloat("swayAmount", 0.3f);
                shader.loadUniformFloat("bladeHeight", bladeHeight);
                shader.loadUniformFloat("bendyness", 0.06f);
                shader.loadUniformFloat("heightError", 0.35f);
                shader.loadUniformFloat("radiusError", 0.35f);
                shader.loadUniformFloat("cutOffThreshold", 0.1f);
                shader.loadUniformFloat("cutOffRange", 0.7f);
                shader.loadUniformFloat("steepnessCutoffStrength", .5f);
                shader.loadUniformFloat("textureMapOffset", 1f / TerrainRenderer.CHUNK_RESOLUTION);

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
            GL.EnableVertexAttribArray(4);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(5);

            GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk * command.chunks.Length);

        }

        internal override void PerformShadowCommand(GrassRenderCommand command, Shadow shadow, RenderEngine renderEngine)
        {
            GL.PolygonOffset(shadow.polygonOffset, shadow.polygonOffset * 10.1f);
            GL.PolygonOffset(1, 1);
            shadow.shadowFrameBuffer.bind();
            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            _grassShadowShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShadowShader.loadUniformInt("bladesPerAxis", bladesPerAxis);
            _grassShadowShader.loadUniformMatrix4f("shadowViewProjectionMatrix", shadow.lightViewMatrix*shadow.shadowProjectionMatrix);

            //  KINDA BAD TO INVERT HE VIEW MATRIX JUST TO GET THE POSITION....
            _grassShadowShader.loadUniformVector3f("lightPosWorld", shadow.lightViewMatrix.Inverted().ExtractTranslation());
            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < command.chunks.Length; i++)
            {
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
