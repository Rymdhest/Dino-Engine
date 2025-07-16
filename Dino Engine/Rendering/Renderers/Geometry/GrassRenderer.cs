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

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    public struct GrassChunkRenderCommand : IRenderCommand
    {
        public Vector2 chunkPos;
        public float size;
        public float arrayID;
    }
    public class GrassRenderer : CommandDrivenRenderer<GrassChunkRenderCommand>
    {

        private ShaderProgram _grassShader = new ShaderProgram("Grass.vert", "Grass.frag");
        private ShaderProgram _grassSimulationShader = new ShaderProgram("Simple.vert", "Grass_Simulation.frag");
        private ShaderProgram _grassBlastShader = new ShaderProgram("Simple.vert", "Grass_Blast.frag");
        private ShaderProgram _grassDisplaceShader = new ShaderProgram("Simple.vert", "Grass_Displace.frag");


        private FrameBuffer _buffer1;
        private FrameBuffer _buffer2;
        private bool _toggle = false;

        private glModel grassBlade;
        private float bladeHeight;

        private int grassNoiseTexture;

        public static readonly int MAX_GRASS_CHUNKS = 1024;
        private float time = 0f;
        private int chunkUBO;

        public GrassRenderer()  
        {
            _grassShader.bind();
            _grassShader.loadUniformInt("heightmaps", 0);
            _grassShader.loadUniformInt("grassNoise", 1);
            _grassShader.unBind();

            generateBladeModel();

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


            var small = TextureGenerator.procTextGen.Voronoi(new Vector2(70f, 70f), jitter: 1.0f, phase: 1.0f, returnMode: ReturnMode.Height);
            var big = TextureGenerator.procTextGen.PerlinFBM(new Vector2(7f, 7f), octaves: 2);

            small.scaleHeight(0.6f);
            small.addHeight(0.4f);

            big.scaleHeight(0.9f);
            big.addHeight(0.1f);

            var final = TextureGenerator.MaterialLayersCombiner.combine(small, big, MaterialLayersManipulator.FilterMode.Everywhere, MaterialLayersManipulator.Operation.Scale);


            int texture = final.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            TextureGenerator.MaterialLayerHandler.cleanUp();

            return texture;


        }


        public void StepSimulation(ScreenQuadRenderer renderer)
        {
            displace(renderer);
            blast(renderer);

            GetNextFrameBuffer().bind();
            _grassSimulationShader.bind();
            _grassSimulationShader.loadUniformFloat("delta", Engine.Delta);
            _grassSimulationShader.loadUniformFloat("regenTime",13.8f);
            _grassSimulationShader.loadUniformFloat("time", time);
            _grassSimulationShader.loadUniformVector2f("grassFieldSize", new Vector2(100f, 100f));
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));
            renderer.Render();

            StepToggle();
        }
        private void generateBladeModel()
        {
            Material grassMaterial = new Material(new Colour(0,0,0), 1); //Throwaway

            if (grassBlade != null) grassBlade.cleanUp();
            float radius = 0.013f;
            bladeHeight = 0.5f;
            List<Vector2> bladeLayers = new List<Vector2>() {
                new Vector2(radius, 0),
                new Vector2(radius*0.8f, bladeHeight*0.33f),
                new Vector2(radius*0.6f, bladeHeight*0.66f),
                new Vector2(radius*0.4f, bladeHeight*1.0f)};
            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 2, grassMaterial, sealTop: 0.0f);


            //bladeMesh.makeFlat(true, false);
            grassBlade = glLoader.loadToVAO(bladeMesh);
        }
        internal override void Prepare(RenderEngine renderEngine)
        {

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.Disable(EnableCap.Blend);

            int bladesPerAxis = 75;
            int bladesPerChunk = (int)Math.Pow(bladesPerAxis, 2);
            generateBladeModel();
            //StepSimulation(renderEngine.ScreenQuadRenderer);
            renderEngine.GBuffer.bind();
            time += Engine.Delta;

            _grassShader.bind();


            Vector4[] chunkData = new Vector4[MAX_GRASS_CHUNKS];
            for (int i = 0; i < commands.Count; i++)
            {
                var command = commands[i];
                chunkData[i] = new Vector4(command.chunkPos.X, command.chunkPos.Y, command.size, command.arrayID);
            }
            GL.BindBuffer(BufferTarget.UniformBuffer, chunkUBO);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes * MAX_GRASS_CHUNKS, chunkData);

            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(2);

            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(3);
            GL.DisableVertexAttribArray(4);
            GL.DisableVertexAttribArray(5);


            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, Engine.RenderEngine._terrainRenderer.GetNormalHeightTextureArray());

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, grassNoiseTexture);


            _grassShader.loadUniformMatrix4f("viewMatrix", Matrix4.Transpose( Engine.RenderEngine.context.viewMatrix));
            _grassShader.loadUniformMatrix4f("projectionMatrix", Matrix4.Transpose(Engine.RenderEngine.context.projectionMatrix));
            _grassShader.loadUniformMatrix4f("invViewMatrix", Matrix4.Transpose(Engine.RenderEngine.context.invViewMatrix));

            _grassShader.loadUniformFloat("swayAmount", 1.30f);
            _grassShader.loadUniformFloat("time", time);
            _grassShader.loadUniformFloat("bladeHeight", bladeHeight);
            _grassShader.loadUniformFloat("bendyness", .10f);
            _grassShader.loadUniformFloat("heightError", 0.25f);
            _grassShader.loadUniformFloat("radiusError", 0.35f);
            _grassShader.loadUniformFloat("cutOffThreshold", 0.03f);
            _grassShader.loadUniformFloat("groundNormalStrength", 2.5f);
            _grassShader.loadUniformFloat("colourError", 0.2f);
            _grassShader.loadUniformFloat("fakeAmbientOcclusionStrength", 1.0f);
            _grassShader.loadUniformFloat("fakeColorAmbientOcclusionStrength", 0.9f);
            _grassShader.loadUniformVector4f("grassMaterial", new Vector4(0.9f, 0f, 0f, 0f));
            _grassShader.loadUniformVector3f("baseColor", new Colour(50, 75, 10).ToVector3());
            _grassShader.loadUniformInt("bladesPerChunk", bladesPerChunk);
            _grassShader.loadUniformInt("bladesPerAxis", bladesPerAxis);
            _grassShader.loadUniformFloat("textureMapOffset", 1f / TerrainRenderer.CHUNK_RESOLUTION);
            
            //GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero, bladesPerChunk*commands.Count);

        }
        internal override void Finish(RenderEngine renderEngine)
        {
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

        public override void PerformCommand(GrassChunkRenderCommand command, RenderEngine renderEngine)
        {
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
    }
}
