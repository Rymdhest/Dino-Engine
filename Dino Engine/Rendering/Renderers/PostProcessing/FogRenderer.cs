using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.ECS;
using Dino_Engine.Util;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FogRenderer : Renderer
    {
        private ShaderProgram fogShader = new ShaderProgram("Simple.vert", "Fog.frag");
        private float time = 0;
        public FogRenderer()
        {
            fogShader.bind();
            fogShader.loadUniformInt("shadedColourTexture", 0);
            fogShader.loadUniformInt("gPosition", 1);
            fogShader.unBind();
        }

        public void Render(ECSEngine eCSEngine ,ScreenQuadRenderer renderer, FrameBuffer gBuffer)
        {
            Vector3 cameraPos = eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 inverseView = Matrix4.Invert(viewMatrix);

            fogShader.bind();

            fogShader.loadUniformFloat("fogDensity", 0.0017f);
            fogShader.loadUniformFloat("heightFallOff", 1000.04075f);
            fogShader.loadUniformFloat("noiseFactor", 30.09f);
            fogShader.loadUniformVector3f("fogColor", SkyRenderer.SkyColour.ToVector3());

            fogShader.loadUniformVector3f("cameraPosWorldSpace", cameraPos);
            fogShader.loadUniformMatrix4f("inverseViewMatrix", inverseView);
            fogShader.loadUniformFloat("time", time);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            renderer.RenderToNextFrameBuffer();

            fogShader.unBind();
        }


        public override void CleanUp()
        {
            fogShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
            time += Engine.Delta;
        }
    }
}
