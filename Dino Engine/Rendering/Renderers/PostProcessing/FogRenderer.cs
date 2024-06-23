using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.ECS;
using Dino_Engine.Util;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    internal class FogRenderer : Renderer
    {
        private ShaderProgram fogShader = new ShaderProgram("Simple_Vertex", "Fog_Fragment");
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

            fogShader.loadUniformFloat("fogDensity", 0.02f);
            fogShader.loadUniformFloat("heightFallOff", 0.005f);
            fogShader.loadUniformVector3f("fogColor", new Vector3(1f));

            fogShader.loadUniformVector3f("cameraPosWorldSpace", cameraPos);
            fogShader.loadUniformMatrix4f("inverseViewMatrix", inverseView);

            //renderer.RenderToNextFrameBuffer();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.getRenderAttachment(2));
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
        }
    }
}
