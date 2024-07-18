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

        private ShaderProgram _sunRayShader = new ShaderProgram("Simple.vert", "SunRay.frag");
        private ShaderProgram _sunFilterShader = new ShaderProgram("Simple.vert", "SunFilter.frag");
        private int downscale = 2;

        public SunRenderer()
        {
            _sunRayShader.bind();
            _sunRayShader.loadUniformInt("sunTexture", 0);
            _sunRayShader.unBind();
        }
        internal override void Render(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            _sunFilterShader.bind();
            ScreenQuadRenderer renderer = renderEngine.ScreenQuadRenderer;
            FrameBuffer gBuffer = renderEngine.GBuffer;

            Vector3 cameraPos = eCSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;
            Matrix4 viewMatrix = MyMath.createViewMatrix(eCSEngine.Camera.getComponent<TransformationComponent>().Transformation);
            Matrix4 projectionMatrix = eCSEngine.Camera.getComponent<ProjectionComponent>().ProjectionMatrix;
            Matrix4 inverseView = Matrix4.Invert(viewMatrix);

            _sunFilterShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            _sunFilterShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _sunFilterShader.loadUniformVector2f("screenResolution", Engine.Resolution);
            _sunFilterShader.loadUniformVector3f("sunColour", new Colour(1f, 0.7f, 0.65f, 25.0f).ToVector3());
            _sunFilterShader.loadUniformVector3f("sunDirection",  (new Vector3(-1f, 2f, 3.9f)).Normalized());
            _sunFilterShader.loadUniformFloat("exponent", 2.0f);

            renderer.GetNextFrameBuffer().bind();
            renderer.Render(depthTest:true, depthMask:false, clearColor:true);


            _sunRayShader.bind();
            _sunRayShader.loadUniformFloat("Density", 0.2f);
            _sunRayShader.loadUniformFloat("Weight", 0.1f);
            _sunRayShader.loadUniformFloat("Exposure", 0.2f);
            _sunRayShader.loadUniformFloat("Decay", 0.9f);
            _sunRayShader.loadUniformFloat("Decay", 1.0f);
            _sunRayShader.loadUniformFloat("illuminationDecay", 1.2f);
            _sunRayShader.loadUniformInt("samples", 40);

            _sunRayShader.loadUniformMatrix4f("projectionMatrix", projectionMatrix);
            _sunRayShader.loadUniformMatrix4f("viewMatrix", viewMatrix);
            _sunRayShader.loadUniformVector2f("screenResolution", Engine.Resolution);
            _sunRayShader.loadUniformVector3f("sunDirection", (new Vector3(-1f, 2f, 3.9f)).Normalized());


            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderer.GetNextFrameBuffer().GetAttachment(0));
            renderer.GetLastFrameBuffer().bind();
            renderer.Render(depthTest: false, depthMask: false, clearColor: false, blend:true);
            //renderer.StepToggle();
        }

        public override void CleanUp()
        {
            _sunRayShader.cleanUp();
            _sunFilterShader.cleanUp();
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {
        }

        internal override void Finish(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Less);
        }

        internal override void Prepare(ECSEngine eCSEngine, RenderEngine renderEngine)
        {
            GL.DepthFunc(DepthFunction.Lequal);
        }


    }
}
