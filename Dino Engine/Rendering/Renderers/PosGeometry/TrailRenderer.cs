using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>Where a blade has been in the last moment, newest first. Whoever gives one of these has to keep it as it is until the frame's been drawn.</summary>
    public struct TrailRenderCommand : IRenderCommand
    {
        public Vector3[] Hilts;
        public Vector3[] Tips;
        public float[] Ages;    // 0 is new, and 1 is gone.
        public int Count;
        public Vector3 Colour;
        public float Strength;
    }

    /// <summary>The ribbons blows leave in the air, over the lit scene. They read depth and don't write it, as beams do.</summary>
    public class TrailRenderer : CommandDrivenRenderer<TrailRenderCommand>
    {
        public const int MostPoints = 32;
        private const int FloatsPerPoint = 5;

        private readonly ShaderProgram _shader = new ShaderProgram("Trail.vert", "Trail.frag");
        private readonly int _vertexArray = GL.GenVertexArray();
        private readonly int _buffer = GL.GenBuffer();
        private readonly float[] _points = new float[MostPoints * 2 * FloatsPerPoint];

        public TrailRenderer() : base("Trails")
        {
            GL.BindVertexArray(_vertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _buffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _points.Length * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, FloatsPerPoint * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, FloatsPerPoint * sizeof(float), 3 * sizeof(float));
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            GL.DepthMask(false);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);
            GL.BlendEquation(BlendEquationMode.FuncAdd);

            renderEngine.Msaa.SceneBuffer.bind();
            _shader.bind();
            GL.BindVertexArray(_vertexArray);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _buffer);
        }

        public override void PerformCommand(TrailRenderCommand command, RenderEngine renderEngine)
        {
            int count = Math.Min(command.Count, MostPoints);
            if (count < 2) return;

            int at = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 hilt = command.Hilts[i];
                Vector3 tip = command.Tips[i];
                float age = command.Ages[i];
                _points[at++] = hilt.X;
                _points[at++] = hilt.Y;
                _points[at++] = hilt.Z;
                _points[at++] = age;
                _points[at++] = 0f;
                _points[at++] = tip.X;
                _points[at++] = tip.Y;
                _points[at++] = tip.Z;
                _points[at++] = age;
                _points[at++] = 1f;
            }

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, at * sizeof(float), _points);
            _shader.loadUniformVector3f("colour", command.Colour);
            _shader.loadUniformFloat("strength", command.Strength);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, count * 2);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.CullFace);
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            GL.DeleteVertexArray(_vertexArray);
            GL.DeleteBuffer(_buffer);
        }
    }
}
