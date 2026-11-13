using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// Where the sun goes into skin and hide (SUBSURFACE.md): a depth map from the sun's side of flesh alone, drawn with no polygon offset, round the hero. The pixel being lit is
    /// where the light comes out, so the depth between the two is how far it went through flesh, exactly. The sun's own map can't say: its offset, which keeps its shadows off the
    /// surfaces casting them, pushes each surface back by its slope, centimetres on a flank, which is as far as red light gets through flesh. Only characters are drawn, and only
    /// their flesh survives (Shadow_Flesh.frag), so armour, a harness or a tree in the way is the sun's map's to say, which decides whether the light reached the flesh at all
    /// (Lighting/SunShadow.glsl's sunFleshTransmittance).
    /// </summary>
    public static class FleshDepth
    {
        /// <summary>Texels on a side.</summary>
        public const int Resolution = 2048;

        /// <summary>Metres the map spans, round the hero: more than the game's camera sees of the ground, at three centimetres a texel.</summary>
        public const float Size = 64f;

        /// <summary>Texture unit the ambient pass reads it from.</summary>
        public const int Unit = 12;

        private static Shadow? _map;
        private static int _sampler;
        private static bool _drawn;

        /// <summary>The map to draw into this frame, made the first time it's asked for.</summary>
        public static Shadow Map
        {
            get
            {
                if (_map == null)
                {
                    Matrix4 projection = Matrix4.CreateOrthographic(Size, Size, -Size, Size);
                    _map = new Shadow(new Vector2i(Resolution), projection, Size, 0f, 0f) { fleshOnly = true };
                }
                return _map.Value;
            }
        }

        /// <summary>Whether the map holds this frame's flesh, which only the sun's cascades' system says.</summary>
        public static bool Drawn => _drawn && _map != null;

        /// <summary>
        /// Points the map along the sun, centred on <paramref name="centre"/> and snapped to its texels as the sun's cascades are, so that it holds still as the hero walks, and clears it
        /// for this frame's flesh.
        /// </summary>
        public static Shadow Aim(Vector3 direction, Vector3 centre)
        {
            Shadow map = Map;
            direction = Vector3.Normalize(direction);
            Vector3 up = MathF.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(up, direction));
            Vector3 lightUp = Vector3.Cross(direction, right);
            float texel = Size / Resolution;
            float x = MathF.Floor(Vector3.Dot(centre, right) / texel) * texel;
            float y = MathF.Floor(Vector3.Dot(centre, lightUp) / texel) * texel;
            Vector3 snapped = right * x + lightUp * y + direction * Vector3.Dot(centre, direction);
            map.lightViewMatrix = Matrix4.LookAt(snapped - direction * Size * 0.5f, snapped, up);
            _map = map;
            map.shadowFrameBuffer.ClearDepth();
            _drawn = true;
            return map;
        }

        /// <summary>Nothing was drawn into it this frame: the sun's down, or flesh lets nothing through.</summary>
        public static void Skip() => _drawn = false;

        /// <summary>From the view space the light passes work in to the map's, as Lighting/SunShadow.glsl reads it.</summary>
        public static Matrix4 ViewSpaceMatrix(RenderEngine renderEngine)
        {
            Shadow map = Map;
            return renderEngine.context.invViewMatrix * map.lightViewMatrix * map.shadowProjectionMatrix;
        }

        /// <summary>Binds the map to <see cref="Unit"/> as plain depth, each texel's own, with nothing past its edge.</summary>
        public static void Bind()
        {
            if (_sampler == 0)
            {
                _sampler = GL.GenSampler();
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureBorderColor, new[] { 1f, 1f, 1f, 1f });
                GL.SamplerParameter(_sampler, SamplerParameterName.TextureCompareMode, (int)All.None);
            }
            GL.ActiveTexture(TextureUnit.Texture0 + Unit);
            GL.BindTexture(TextureTarget.Texture2D, Map.shadowFrameBuffer.getDepthAttachment());
            GL.BindSampler(Unit, _sampler);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        public static void Release() => GL.BindSampler(Unit, 0);

        public static void CleanUp()
        {
            _map?.shadowFrameBuffer.cleanUp();
            _map = null;
            if (_sampler != 0) GL.DeleteSampler(_sampler);
            _sampler = 0;
        }
    }
}
