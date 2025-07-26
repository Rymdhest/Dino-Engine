using Dino_Engine.Rendering.Renderers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using Dino_Engine.Modelling;
using Dino_Engine.Core;
using System;
using Dino_Engine.Modelling.Model;
using Dino_Engine.ECS;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers
{
    public class ScreenQuadRenderer : RenderPassRenderer
    {
        glModel _quadModel;
        public ScreenQuadRenderer() : base("Full Screen Quad")
        {
            trackPerformance = false;
            float[] positions = { -1, 1, -1, -1, 1, -1, 1, 1 };
            int[] indices = { 0, 1, 2, 3, 0, 2 };
            _quadModel = glLoader.loadToVAO(positions, indices, 2);
        }
        public void Render(bool depthTest = false, bool blend = false, bool clearColor = true)
        {


            GL.ClearColor(0f, 0f, 0f, 1f);
            if (clearColor)GL.Clear(ClearBufferMask.ColorBufferBit);
            

            if (depthTest) GL.Enable(EnableCap.DepthTest); 
            else GL.Disable(EnableCap.DepthTest);

            GL.DepthMask(false);

            if (blend) GL.Enable(EnableCap.Blend);
            else GL.Disable(EnableCap.Blend);

            RenderPass(null);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
        }

        public override void Update()
        {

        }
        public override void CleanUp()
        {
            _quadModel.cleanUp();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.BindVertexArray(_quadModel.getVAOID());
            GL.EnableVertexAttribArray(0);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.BindVertexArray(0);
        }

        internal override void Render(RenderEngine renderEngine)
        {
            GL.DrawElements(PrimitiveType.Triangles, _quadModel.getVertexCount(), DrawElementsType.UnsignedInt, 0);
        }
    }
}
