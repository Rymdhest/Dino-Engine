using Dino_Engine.Rendering.Renderers.AntiAliasing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    public enum MarkKind
    {
        Circle,
        Sector,
        Line,
    }

    /// <summary>A shape on the ground, and how far it has got.</summary>
    public struct GroundMarkCommand : IRenderCommand
    {
        public MarkKind Kind;
        public Vector2 Origin;
        public Vector2 Facing;

        /// <summary>How far it goes: a radius, or how long a line is.</summary>
        public float Length;

        /// <summary>A sector's arc in radians, or how far a line goes either side of its middle.</summary>
        public float Width;

        /// <summary>How far the fill has come, from where it starts at 0 to the edge at 1.</summary>
        public float Fill;

        /// <summary>How much it shows, up to 1, and past that for the moment it lands.</summary>
        public float Strength;

        public bool Hostile;
    }

    /// <summary>
    /// Shapes on the ground, for everything that's about to happen there and everything that just has. Whatever's going to hit someone is what they most need to see, so this has two rules: a shape is exactly
    /// where it means, and a crowd of them is still one thing to look at.
    ///
    /// So they aren't drawn on the scene. Each goes into a map of the ground around the hero, looked at from straight above, as how far every point is from its edge. Taking the least of those is the same as taking the
    /// union of the shapes: everything that overlaps comes out as one shape with one outline, where a stack of see-through shapes would go muddy. Then one pass over the screen follows each pixel's ray to the ground and shows
    /// what's on the map there, which puts it on hills and under trees without anything having to know about them. All of it is skipped when there's nothing on the ground.
    /// </summary>
    public class GroundMarkRenderer : CommandDrivenRenderer<GroundMarkCommand>
    {
        private const int Resolution = 1024;
        private const float Size = 176f;            // Metres across. It has to cover all the camera can see of the ground.
        private const float Margin = 1.2f;          // Metres around a shape's edge that the map has to know about.
        private const float Clear = 1000f;

        private static readonly Vector3 HostileColour = new Vector3(1.9f, 0.32f, 0.06f);
        private static readonly Vector3 FriendlyColour = new Vector3(0.55f, 0.95f, 1.5f);

        private readonly ShaderProgram _shapeShader = new ShaderProgram("Marks/Mark_Shape.vert", "Marks/Mark_Shape.frag");
        private readonly ShaderProgram _compositeShader = new ShaderProgram("Simple.vert", "Marks/Mark_Composite.frag");
        private readonly FrameBuffer _map;
        private readonly int _corners = GL.GenVertexArray();    // The shader knows its own four corners, but drawing needs something bound.
        private readonly float[] _clearDistance = { Clear, Clear, 0f, 0f };
        private readonly float[] _clearFill = { 0f, 0f, 0f, 0f };

        private bool _any;
        private Vector2 _corner;

        /// <summary>Where the ground around the hero is, which is what the map is kept around, and what's used wherever the shape of the ground isn't known.</summary>
        public Vector3 Focus;

        public GroundMarkRenderer() : base("Ground marks")
        {
            var settings = new FrameBufferSettings(new Vector2i(Resolution));
            settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0)
            {
                formatInternal = PixelInternalFormat.Rg16f,
                formatExternal = PixelFormat.Rg,
                pixelType = PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment1)
            {
                formatInternal = PixelInternalFormat.Rgba16f,
                pixelType = PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
            });
            _map = new FrameBuffer(settings);
            _map.unbind();

            _compositeShader.bind();
            _compositeShader.loadUniformInt("distances", 0);
            _compositeShader.loadUniformInt("fills", 1);
            _compositeShader.loadUniformInt("terrainDepth", 2);
            _compositeShader.unBind();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
            _any = commands.Count > 0;
            if (!_any) return;

            // On whole texels, or the edges would crawl as the hero walks.
            float texel = Size / Resolution;
            Vector2 centre = Focus.Xz;
            _corner = new Vector2(MathF.Floor((centre.X - Size * 0.5f) / texel), MathF.Floor((centre.Y - Size * 0.5f) / texel)) * texel;

            _map.bind();
            GL.ClearBuffer(ClearBuffer.Color, 0, _clearDistance);
            GL.ClearBuffer(ClearBuffer.Color, 1, _clearFill);

            GL.DepthMask(false);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.StencilTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            GL.BlendEquation(0, BlendEquationMode.Min);
            GL.BlendEquation(1, BlendEquationMode.Max);

            _shapeShader.bind();
            _shapeShader.loadUniformVector4f("mapRegion", new Vector4(_corner.X, _corner.Y, 1f / Size, 1f / Size));
            GL.BindVertexArray(_corners);
        }

        public override void PerformCommand(GroundMarkCommand command, RenderEngine renderEngine)
        {
            float length = command.Length;
            Vector2 centre = command.Origin;
            float reach = length + Margin;
            if (command.Kind == MarkKind.Line)
            {
                centre += command.Facing * (length * 0.5f);
                reach = length * 0.5f + command.Width + Margin;
            }

            _shapeShader.loadUniformVector4f("box", new Vector4(centre.X, centre.Y, reach, reach));
            _shapeShader.loadUniformInt("kind", (int)command.Kind);
            _shapeShader.loadUniformVector2f("origin", command.Origin);
            _shapeShader.loadUniformVector2f("facing", command.Facing);
            _shapeShader.loadUniformFloat("len", MathF.Max(length, 0.01f));
            _shapeShader.loadUniformFloat("wid", command.Width);
            _shapeShader.loadUniformFloat("fill", command.Fill);
            _shapeShader.loadUniformFloat("strength", command.Strength);
            _shapeShader.loadUniformFloat("hostile", command.Hostile ? 1f : 0f);
            GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
        }

        internal override void Finish(RenderEngine renderEngine)
        {
            if (!_any) return;

            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One);

            // It lies on the ground, under whoever's standing there.
            renderEngine.Msaa.SceneBuffer.bind();
            GL.Enable(EnableCap.StencilTest);
            GL.StencilMask(0);
            GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
            GL.StencilFunc(StencilFunction.Equal, 0, MsaaRenderer.CharacterStencilBit);

            _compositeShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _map.GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _map.GetAttachment(1));

            WaterRenderer water = renderEngine._waterRenderer;
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, water.TerrainHeightTexture);
            GL.ActiveTexture(TextureUnit.Texture0);

            _compositeShader.loadUniformVector4f("mapRegion", new Vector4(_corner.X, _corner.Y, 1f / Size, 1f / Size));
            _compositeShader.loadUniformVector4f("terrainRegion", water.TerrainRegion);
            _compositeShader.loadUniformVector2f("terrainHeightRange", water.TerrainHeightRange);
            _compositeShader.loadUniformFloat("groundFallback", Focus.Y);
            _compositeShader.loadUniformFloat("seaLevel", water.SeaLevel);
            _compositeShader.loadUniformVector3f("hostileColour", HostileColour);
            _compositeShader.loadUniformVector3f("friendlyColour", FriendlyColour);
            renderEngine.ScreenQuadRenderer.DrawQuad();

            GL.StencilMask(0xFF);
            GL.Disable(EnableCap.StencilTest);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.DepthMask(true);
        }

        public override void CleanUp()
        {
            _shapeShader.cleanUp();
            _compositeShader.cleanUp();
            _map.cleanUp();
            GL.DeleteVertexArray(_corners);
        }
    }
}
