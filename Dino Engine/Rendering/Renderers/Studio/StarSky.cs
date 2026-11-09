using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Studio
{
    /// <summary>
    /// A procedural night sky drawn into a texture of its own, for an interface to put behind something it wants thought about (<c>Shaders/Studio/StarSky.frag</c>).
    /// It's drawn when asked, on the render thread, and leaves every piece of state it touched as it found it, so it can be asked for while the interface is
    /// being built. Where it's looking from moves its layers by different amounts, so the stars drift past one another as the view moves.
    /// </summary>
    public sealed class StarSky : IDisposable
    {
        private readonly ShaderProgram _shader = new ShaderProgram("Simple.vert", "Studio/StarSky.frag");
        private FrameBuffer? _target;
        private Vector2i _size;

        /// <summary>Draws the sky at this size and returns its texture, whose rows run upwards as a framebuffer's do.</summary>
        /// <param name="offset">Where the view is looking, in whatever units it moves in: the layers drift by small shares of it.</param>
        /// <param name="zoom">How close the view is, which brings the sky a little nearer too.</param>
        public int Render(Vector2i size, Vector2 offset, float zoom, float time, float seed = 0.37f)
        {
            size = new Vector2i(Math.Max(size.X, 1), Math.Max(size.Y, 1));
            if (_target == null)
            {
                var settings = new FrameBufferSettings(size);
                settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0));
                _target = new FrameBuffer(settings);
                _size = size;
            }
            else if (size != _size)
            {
                _target.resize(size);
                _size = size;
            }

            GL.GetInteger(GetPName.DrawFramebufferBinding, out int framebuffer);
            var viewport = new int[4];
            GL.GetInteger(GetPName.Viewport, viewport);
            GL.GetInteger(GetPName.CurrentProgram, out int program);
            bool blend = GL.IsEnabled(EnableCap.Blend);
            bool depthTest = GL.IsEnabled(EnableCap.DepthTest);
            bool scissor = GL.IsEnabled(EnableCap.ScissorTest);
            bool cull = GL.IsEnabled(EnableCap.CullFace);
            GL.GetBoolean(GetPName.DepthWritemask, out bool depthMask);

            _target.bind();
            GL.Viewport(0, 0, size.X, size.Y);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.CullFace);
            GL.DepthMask(false);

            _shader.bind();
            _shader.loadUniformVector2f("resolution", new Vector2(size.X, size.Y));
            _shader.loadUniformVector2f("offset", offset);
            _shader.loadUniformFloat("zoom", zoom);
            _shader.loadUniformFloat("time", time);
            _shader.loadUniformFloat("seed", seed);
            Engine.RenderEngine.ScreenQuadRenderer.DrawQuad();
            _shader.unBind();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
            GL.UseProgram(program);
            if (blend) GL.Enable(EnableCap.Blend);
            if (depthTest) GL.Enable(EnableCap.DepthTest);
            if (scissor) GL.Enable(EnableCap.ScissorTest);
            if (cull) GL.Enable(EnableCap.CullFace);
            GL.DepthMask(depthMask);
            return _target.GetAttachment(0);
        }

        public void Dispose()
        {
            _shader.cleanUp();
            _target?.cleanUp();
        }
    }
}
