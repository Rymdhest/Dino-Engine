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
using Dino_Engine.ECS.ComponentsOLD;
using Dino_Engine.ECS.SystemsOLD;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class GrassRenderer : Renderer
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

        private float time = 0f;

        public GrassRenderer()
        {
            _grassShader.bind();
            _grassShader.loadUniformInt("heightMap", 0);
            _grassShader.loadUniformInt("grassMap", 1);
            _grassShader.loadUniformInt("terrainNormalMap", 2);
            _grassShader.loadUniformInt("bendMap", 3);
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
        }

        public void blast(ScreenQuadRenderer renderer)
        {
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
        }

        public void displace(ScreenQuadRenderer renderer)
        {
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
        private void generateBladeModel2()
        {
            Material grassMaterial = new Material(new Colour(50, 75, 10), 1);
            Material topMaterial = grassMaterial;
            Material botMaterial = grassMaterial;
            topMaterial.Colour.Intensity = 1.0f;
            botMaterial.Colour.Intensity = 0.3f;

            if (grassBlade != null) grassBlade.cleanUp();
            float radius = .07215f;
            bladeHeight =2.6f;
            List<Vector3> bladeLayers = new List<Vector3>() {
                new Vector3(radius, 0, radius*0.3f),
                new Vector3(radius*0.6f, bladeHeight*0.4f, radius*0.2f),
                new Vector3(radius*0.4f, bladeHeight*0.75f, radius*0.1f),
                new Vector3(radius*0.15f, bladeHeight, radius*0.05f)};
            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 4, grassMaterial);

            foreach (MeshVertex vertex in bladeMesh.meshVertices)
            {
                vertex.material.Colour = Colour.mix(botMaterial.Colour, topMaterial.Colour, vertex.position.Y / bladeHeight);
            }

            bladeMesh.makeFlat(true, false);
            grassBlade = glLoader.loadToVAO(bladeMesh);
        }
        private void generateBladeModel()
        {
            Material grassMaterial = new Material(new Colour(76, 96, 23), 1);
            Material topMaterial = grassMaterial;
            Material botMaterial = grassMaterial;
            topMaterial.Colour.Intensity = 1.0f;
            botMaterial.Colour.Intensity = 0.4f;

            if (grassBlade != null) grassBlade.cleanUp();
            float radius = .12f;
            bladeHeight = 4.6f;
            List<Vector2> bladeLayers = new List<Vector2>() {
                new Vector2(radius, 0),
                new Vector2(radius*0.6f, bladeHeight*0.4f),
                new Vector2(radius*0.4f, bladeHeight*0.75f),
                new Vector2(radius*0.15f, bladeHeight)};
            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 2, grassMaterial);

            foreach (MeshVertex vertex in bladeMesh.meshVertices)
            {
                vertex.material.Colour = Colour.mix(botMaterial.Colour, topMaterial.Colour, vertex.position.Y / bladeHeight);
            }

            bladeMesh.makeFlat(true, false);
            grassBlade = glLoader.loadToVAO(bladeMesh);
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {

        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            //StepSimulation(renderEngine.ScreenQuadRenderer);
            renderEngine.GBuffer.bind();
            generateBladeModel2();
            time += Engine.Delta;
            float spacing =0.2525f;

            _grassShader.bind();
            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            foreach (EntityOLD terrain in eCSEngine.getSystem<TerrainSystem>().MemberEntities)
            {

                Vector2 grassFieldSizeWorld = ((TerrainHitBox)(terrain.getComponent<CollisionComponent>().HitBox))._max.Xz;
                Vector2 resolution = terrain.getComponent<TerrainMapsComponent>().heightMap.Resolution;
                Vector2 bladesPerAxis = grassFieldSizeWorld / spacing;
                Vector2 quadsPerMeter = (resolution - new Vector2(1f)) / grassFieldSizeWorld;

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().heightMap.GetTexture());

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().grassMap.GetTexture());

                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().normalMap.GetTexture());

                GL.ActiveTexture(TextureUnit.Texture3);
                GL.BindTexture(TextureTarget.Texture2D, GetLastFrameBuffer().GetAttachment(0));

                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(terrain.getComponent<TransformationComponent>().Transformation);
                Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;
                _grassShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _grassShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
                _grassShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
                _grassShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));

                

                // super weird not sure why this works...
                _grassShader.loadUniformVector2f("offsetTest", new Vector2(1f)/ (quadsPerMeter*2f));
                _grassShader.loadUniformVector2f("offsetTest2", new Vector2(1f) / (quadsPerMeter));

                _grassShader.loadUniformFloat("swayAmount", 0.0f);
                _grassShader.loadUniformFloat("time", time);
                _grassShader.loadUniformFloat("bladeHeight", bladeHeight);
                _grassShader.loadUniformFloat("bendyness", 0.1f);
                _grassShader.loadUniformFloat("heightError", 0.45f);
                _grassShader.loadUniformFloat("cutOffThreshold", 0.1f);
                _grassShader.loadUniformFloat("groundNormalStrength", 4.0f);
                _grassShader.loadUniformFloat("colourError", 0.315f);
                _grassShader.loadUniformVector2f("bladesPerAxis", bladesPerAxis);
                _grassShader.loadUniformVector2f("grassFieldSizeWorld", grassFieldSizeWorld);
                _grassShader.loadUniformFloat("spacing", spacing);

                GL.DrawElementsInstanced(PrimitiveType.Triangles, grassBlade.getVertexCount(), DrawElementsType.UnsignedInt, IntPtr.Zero,(int)(bladesPerAxis.X*bladesPerAxis.Y));

            }

        }
        public override void CleanUp()
        {
            _grassShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
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
    }
}
