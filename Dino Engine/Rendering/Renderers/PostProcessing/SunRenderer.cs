using Dino_Engine.ECS;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;
using Dino_Engine.Core;

namespace Dino_Engine.Rendering.Renderers.PostProcessing
{
    public class SunRenderer : Renderer
    {

        private ShaderProgram _sunShader = new ShaderProgram("Simple.vert", "Sun.frag");

        public SunRenderer()
        {
            _sunShader.bind();
            _sunShader.loadUniformInt("shadedColourTexture", 0);
            _sunShader.loadUniformInt("gPosition", 1);
            _sunShader.unBind();
        }
        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            FrameBuffer gBuffer = renderEngine.GBuffer;

            Vector3 cameraPos = eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Matrix4 inverseView = Matrix4.Invert(viewMatrix);



            _sunShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            _sunShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _sunShader.loadUniformVector2f("screenResolution", Engine.Resolution);
            _sunShader.loadUniformVector3f("sunColour", new Colour(1f, 1f, 0.95f, 20.0f).ToVector3());
            _sunShader.loadUniformVector3f("sunDirectionViewSpace",  (new Vector3(-2f, 2f, 0.9f)));

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetLastOutputTexture());
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, gBuffer.GetAttachment(2));
            renderer.RenderToNextFrameBuffer();
        }

        public override void CleanUp()
        {
            _sunShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            _sunShader.bind();
        }


    }
}
