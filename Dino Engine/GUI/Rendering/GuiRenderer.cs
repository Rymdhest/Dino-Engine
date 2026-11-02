using Dino_Engine.Rendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.GUI.Rendering
{
    /// <summary>
    /// Uploads a <see cref="GuiDrawList"/> into one streaming instance buffer and draws it to the currently bound
    /// framebuffer: one instanced triangle strip per draw command, premultiplied alpha blending, scissor clipping.
    /// GL state touched by the GUI pass is saved and restored so the rest of the pipeline is unaffected.
    /// </summary>
    public class GuiRenderer : IDisposable
    {
        private readonly ShaderProgram _shader;
        private readonly int _vao;
        private readonly int _instanceBuffer;
        private readonly int _whiteTexture;
        private int _instanceBufferBytes;
        private readonly int[] _viewport = new int[4];

        public int LastDrawCalls { get; private set; }
        public int LastQuadCount { get; private set; }

        public GuiRenderer()
        {
            _shader = new ShaderProgram("GUI.vert", "GUI.frag");
            _shader.bind();
            _shader.loadUniformInt("uTexture", 0);
            _shader.unBind();

            _vao = GL.GenVertexArray();
            _instanceBuffer = GL.GenBuffer();

            GL.BindVertexArray(_vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceBuffer);
            int stride = GuiInstance.SizeInBytes;
            InstanceAttribute(0, 2, VertexAttribPointerType.Float, false, stride, 0);         // centre
            InstanceAttribute(1, 2, VertexAttribPointerType.Float, false, stride, 8);         // half size
            InstanceAttribute(2, 2, VertexAttribPointerType.Float, false, stride, 16);        // axis
            InstanceAttribute(3, 4, VertexAttribPointerType.Float, false, stride, 24);        // uv rect
            InstanceAttribute(4, 4, VertexAttribPointerType.UnsignedByte, true, stride, 40);  // colour
            InstanceAttribute(5, 4, VertexAttribPointerType.UnsignedByte, true, stride, 44);  // border colour
            InstanceAttribute(6, 4, VertexAttribPointerType.Float, false, stride, 48);        // params
            InstanceAttribute(7, 1, VertexAttribPointerType.Float, false, stride, 64);        // expand
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            _whiteTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _whiteTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new byte[] { 255, 255, 255, 255 });
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private static void InstanceAttribute(int index, int size, VertexAttribPointerType type, bool normalized, int stride, int offset)
        {
            GL.EnableVertexAttribArray(index);
            GL.VertexAttribPointer(index, size, type, normalized, stride, offset);
            GL.VertexAttribDivisor(index, 1);
        }

        /// <param name="guiSize">Size of the screen in GUI units.</param>
        /// <param name="framebufferSize">Size of the target framebuffer in physical pixels.</param>
        public void Render(GuiDrawList drawList, Vector2 guiSize, Vector2i framebufferSize)
        {
            drawList.End();
            LastDrawCalls = 0;
            LastQuadCount = drawList.InstanceCount;
            if (drawList.InstanceCount == 0 || framebufferSize.X <= 0 || framebufferSize.Y <= 0) return;

            // ---- save state
            bool blend = GL.IsEnabled(EnableCap.Blend);
            bool depthTest = GL.IsEnabled(EnableCap.DepthTest);
            bool cullFace = GL.IsEnabled(EnableCap.CullFace);
            bool scissor = GL.IsEnabled(EnableCap.ScissorTest);
            GL.GetInteger(GetPName.BlendSrcRgb, out int blendSrcRgb);
            GL.GetInteger(GetPName.BlendDstRgb, out int blendDstRgb);
            GL.GetInteger(GetPName.BlendSrcAlpha, out int blendSrcAlpha);
            GL.GetInteger(GetPName.BlendDstAlpha, out int blendDstAlpha);
            GL.GetInteger(GetPName.BlendEquationRgb, out int blendEquationRgb);
            GL.GetInteger(GetPName.BlendEquationAlpha, out int blendEquationAlpha);
            GL.GetInteger(GetPName.ActiveTexture, out int activeTexture);
            GL.GetInteger(GetPName.Viewport, _viewport);

            // ---- GUI state
            GL.Viewport(0, 0, framebufferSize.X, framebufferSize.Y);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.ScissorTest);

            _shader.bind();
            _shader.loadUniformVector2f("uScreenSize", guiSize);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindVertexArray(_vao);

            // Orphan and refill the instance buffer.
            GL.BindBuffer(BufferTarget.ArrayBuffer, _instanceBuffer);
            int bytes = drawList.InstanceCount * GuiInstance.SizeInBytes;
            if (bytes > _instanceBufferBytes) _instanceBufferBytes = Math.Max(bytes, _instanceBufferBytes * 2);
            GL.BufferData(BufferTarget.ArrayBuffer, _instanceBufferBytes, IntPtr.Zero, BufferUsageHint.StreamDraw);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, drawList.Instances);

            float scaleX = framebufferSize.X / guiSize.X;
            float scaleY = framebufferSize.Y / guiSize.Y;
            int boundTexture = -1;
            var commands = drawList.Commands;
            for (int c = 0; c < commands.Count; c++)
            {
                GuiDrawCommand command = commands[c];
                if (command.InstanceCount == 0) continue;
                int x0 = Math.Clamp((int)MathF.Floor(command.Clip.Left * scaleX), 0, framebufferSize.X);
                int x1 = Math.Clamp((int)MathF.Ceiling(command.Clip.Right * scaleX), 0, framebufferSize.X);
                int y0 = Math.Clamp((int)MathF.Floor(command.Clip.Top * scaleY), 0, framebufferSize.Y);
                int y1 = Math.Clamp((int)MathF.Ceiling(command.Clip.Bottom * scaleY), 0, framebufferSize.Y);
                if (x1 <= x0 || y1 <= y0) continue;
                GL.Scissor(x0, framebufferSize.Y - y1, x1 - x0, y1 - y0);

                int texture = command.Texture != 0 ? command.Texture : _whiteTexture;
                if (texture != boundTexture)
                {
                    GL.BindTexture(TextureTarget.Texture2D, texture);
                    boundTexture = texture;
                }
                GL.DrawArraysInstancedBaseInstance(PrimitiveType.TriangleStrip, 0, 4, command.InstanceCount, command.FirstInstance);
                LastDrawCalls++;
            }

            // ---- restore state
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _shader.unBind();
            GL.ActiveTexture((TextureUnit)activeTexture);
            if (!scissor) GL.Disable(EnableCap.ScissorTest);
            if (!blend) GL.Disable(EnableCap.Blend);
            if (depthTest) GL.Enable(EnableCap.DepthTest);
            if (cullFace) GL.Enable(EnableCap.CullFace);
            GL.BlendEquationSeparate((BlendEquationMode)blendEquationRgb, (BlendEquationMode)blendEquationAlpha);
            GL.BlendFuncSeparate((BlendingFactorSrc)blendSrcRgb, (BlendingFactorDest)blendDstRgb, (BlendingFactorSrc)blendSrcAlpha, (BlendingFactorDest)blendDstAlpha);
            GL.Viewport(_viewport[0], _viewport[1], _viewport[2], _viewport[3]);
        }

        public void Dispose()
        {
            _shader.cleanUp();
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_instanceBuffer);
            GL.DeleteTexture(_whiteTexture);
        }
    }
}
