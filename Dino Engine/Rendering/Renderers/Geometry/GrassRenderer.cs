using Dino_Engine.ECS;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.ECS.Systems;
using Dino_Engine.ECS.Components;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.Geometry
{
    internal class GrassRenderer : Renderer
    {

        private ShaderProgram _grassShader = new ShaderProgram("Grass.vert", "Grass.frag");

        private glModel grassBlade;
        private float bladeHeight;

        private float time = 0f;

        public GrassRenderer()
        {
            _grassShader.bind();
            _grassShader.loadUniformInt("heightMap", 0);
            _grassShader.loadUniformInt("grassMap", 1);
            _grassShader.loadUniformInt("terrainNormalMap", 2);
            _grassShader.loadUniformInt("windMap", 3);
            _grassShader.unBind();
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            Material topMaterial = Material.LEAF;
            Material botMaterial = Material.LEAF;
            botMaterial.Colour.Intensity = 0.8f;

            if (grassBlade != null) grassBlade.cleanUp();
            float radius = .16f;
            bladeHeight = 3f;
            List<Vector2> bladeLayers = new List<Vector2>() {
                new Vector2(radius, 0),
                new Vector2(radius*0.5f, bladeHeight*0.8f),
                new Vector2(radius*0.3f, bladeHeight)};
            Mesh bladeMesh = MeshGenerator.generateCylinder(bladeLayers, 3, Material.LEAF, true);

            foreach(Vertex vertex in bladeMesh.vertices)
            {
                vertex.material.Colour = Colour.mix(botMaterial.Colour, topMaterial.Colour, vertex.position.Y/ bladeHeight);
            }

            bladeMesh.makeFlat(true, false);
            grassBlade = glLoader.loadToVAO(bladeMesh);
        }

        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            time += Engine.Delta;
            float spacing = .8f;

            _grassShader.bind();
            GL.BindVertexArray(grassBlade.getVAOID());
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            foreach (Entity terrain in eCSEngine.getSystem<TerrainSystem>().MemberEntities)
            {
                Vector2 grassFieldSizeWorld = terrain.getComponent<TerrainMapsComponent>().heightMap.Resolution;
                Vector2 bladesPerAxis = (grassFieldSizeWorld / spacing)- new Vector2(1f/ spacing);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().heightMap.GetTexture());

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().grassMap.GetTexture());

                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.Texture2D, terrain.getComponent<TerrainMapsComponent>().normalMap.GetTexture());

                Matrix4 transformationMatrix = MyMath.createTransformationMatrix(terrain.getComponent<TransformationComponent>().Transformation);
                Matrix4 modelViewMatrix = transformationMatrix * viewMatrix;
                _grassShader.loadUniformMatrix4f("modelMatrix", transformationMatrix);
                _grassShader.loadUniformMatrix4f("modelViewMatrix", modelViewMatrix);
                _grassShader.loadUniformMatrix4f("modelViewProjectionMatrix", modelViewMatrix * projectionMatrix);
                _grassShader.loadUniformMatrix4f("normalModelViewMatrix", Matrix4.Transpose(Matrix4.Invert(modelViewMatrix)));




                _grassShader.loadUniformFloat("swayAmount", 0.1f);
                _grassShader.loadUniformFloat("time", time);
                _grassShader.loadUniformFloat("bladeHeight", bladeHeight);
                _grassShader.loadUniformFloat("bendyness", 0.125f);
                _grassShader.loadUniformFloat("heightError", 0.3f);
                _grassShader.loadUniformFloat("cutOffThreshold", 0.2f);
                _grassShader.loadUniformFloat("groundNormalStrength", 2.0f);
                _grassShader.loadUniformFloat("colourError", 0.06f);
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
    }
}
