using Dino_Engine.Core;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// The sun's contact shadows (Shadows/ContactShadows.comp, SHADOWS.md): traced in screen space through the occlusion's depth, after Bend Studio's
    /// screen space shadows, for what's too small or too close to what it shades for the shadow map. The ambient pass takes the sun's direct light by
    /// <see cref="ShadowTexture"/> wherever it isn't already shaded (Ambient_Light.frag).
    /// </summary>
    internal class ContactShadowRenderer : RenderPassRenderer
    {
        private const int WaveSize = 64;
        // The shader's SAMPLE_COUNT: no sample is further along the ray.
        private const int SampleCount = 32;
        // What a clear leaves: lit.
        private static readonly float[] AllLit = { 1f };

        private readonly ShaderProgram _shader = new ShaderProgram("Shadows/ContactShadows.comp");
        private readonly ShaderProgram _blurShader = new ShaderProgram("Shadows/ContactShadows_Blur.comp");
        // What the rays found, and that smoothed over the pixels round each on one surface, which is what's read.
        private int _raw;
        private int _shadow;
        private Vector2i _size;
        private readonly DispatchList _dispatches = new DispatchList();

        /// <summary>This frame's contact shadows, 1 lit and 0 shaded, once the pass has run: all lit when there's no sun or they're off.</summary>
        public int ShadowTexture => _shadow;

        internal ContactShadowRenderer() : base("Contact Shadows")
        {
            // Timed by its stages instead (GPU timers don't nest).
            trackPerformance = false;
            _shader.bind();
            _shader.loadUniformInt("linearDepth", 0);
            _blurShader.bind();
            _blurShader.loadUniformInt("source", 0);
            _blurShader.loadUniformInt("linearDepth", 1);
            _blurShader.unBind();
            CreateTextures(Engine.Resolution);
        }

        private void CreateTextures(Vector2i size)
        {
            if (_raw != 0) GL.DeleteTexture(_raw);
            if (_shadow != 0) GL.DeleteTexture(_shadow);
            _size = new Vector2i(Math.Max(size.X, 1), Math.Max(size.Y, 1));
            _raw = CreateTexture();
            _shadow = CreateTexture();
        }

        private int CreateTexture()
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R8, _size.X, _size.Y);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        // Drawn in stages by RenderEngine.ScreenTerms, beside the occlusion and the sun's soft shadow, with one barrier between stages.
        internal override void Render(RenderEngine renderEngine)
        {
        }

        // Whether this frame's rays were traced, so that there's something to smooth.
        private bool _traced;

        /// <summary>
        /// The first stage: what's to be traced into is cleared, or what's read is, all lit, if nothing is to be traced: there's no sun, they're off,
        /// or the light's projection reaches no pixel. A clear comes in order before anything that reads it or draws into it.
        /// </summary>
        public void Begin(RenderEngine renderEngine)
        {
            ContactShadowSettings settings = renderEngine.ContactShadows;
            _traced = false;
            if (settings.Enabled && renderEngine.Lighting.Sun && renderEngine._directionalLightRenderer.PeekSun(out DirectionallightRenderCommand sun))
            {
                // Where the sun projects to: the way to it, as a point at infinity.
                Vector4 towardsSun = new Vector4(-sun.direction.Normalized(), 0f);
                Vector4 projected = towardsSun * renderEngine.context.viewMatrix * renderEngine.context.projectionMatrix;
                BuildDispatchList(projected, _size, _dispatches);
                _traced = _dispatches.Count > 0;
            }
            // Lit everywhere, unless something shades it: the rays only reach as far as the light's projection allows.
            GL.ClearTexImage(_traced ? _raw : _shadow, 0, PixelFormat.Red, PixelType.Float, AllLit);
        }

        /// <summary>The second stage: the rays, through the occlusion's depth, once a barrier has come after it.</summary>
        public void Trace(RenderEngine renderEngine)
        {
            if (!_traced) return;
            ContactShadowSettings settings = renderEngine.ContactShadows;
            Engine.PerformanceMonitor.startGPUTask("Contact Shadows");
            _shader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.OcclusionDepthTexture);
            GL.BindImageTexture(0, _raw, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R8);
            _shader.loadUniformVector4f("lightCoordinate", _dispatches.LightCoordinate);
            _shader.loadUniformFloat("surfaceThickness", Math.Max(settings.Thickness, 1e-5f));
            _shader.loadUniformFloat("bilinearThreshold", Math.Max(settings.EdgeThreshold, 0f));
            _shader.loadUniformFloat("shadowContrast", Math.Max(settings.Contrast, 1f));
            _shader.loadUniformFloat("minHeight", Math.Max(settings.MinHeight, 0f));
            float fadeFrom = Math.Clamp(settings.FadeFrom, 0f, SampleCount);
            _shader.loadUniformFloat("fadeFrom", fadeFrom);
            _shader.loadUniformFloat("fadeTo", Math.Clamp(settings.FadeTo, fadeFrom + 1f, SampleCount + 1f));
            for (int i = 0; i < _dispatches.Count; i++)
            {
                Dispatch dispatch = _dispatches[i];
                _shader.loadUniformVector2i("waveOffset", dispatch.WaveOffset);
                // Overlapping waves near the light's projection write the same pixels with the same answer: nothing has to wait between dispatches.
                GL.DispatchCompute(dispatch.WaveCount.X, dispatch.WaveCount.Y, dispatch.WaveCount.Z);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            Engine.PerformanceMonitor.finishGPUTask("Contact Shadows");
        }

        /// <summary>The third stage: the rays' shadows smoothed over the pixels round each on one surface, into <see cref="ShadowTexture"/>.</summary>
        public void Smooth(RenderEngine renderEngine)
        {
            if (!_traced) return;
            Engine.PerformanceMonitor.startGPUTask("Contact Shadows Smoothed");
            _blurShader.bind();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _raw);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, renderEngine.OcclusionDepthTexture);
            GL.BindImageTexture(0, _shadow, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R8);
            GL.DispatchCompute((_size.X + 7) / 8, (_size.Y + 7) / 8, 1);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            Engine.PerformanceMonitor.finishGPUTask("Contact Shadows Smoothed");
        }

        private struct Dispatch
        {
            public Vector3i WaveCount;
            public Vector2i WaveOffset;
        }

        private sealed class DispatchList
        {
            private readonly Dispatch[] _items = new Dispatch[8];
            public Vector4 LightCoordinate;
            public int Count;
            public Dispatch this[int index] { get => _items[index]; set => _items[index] = value; }
            public ref Dispatch At(int index) => ref _items[index];
        }

        /// <summary>
        /// The dispatches that cover the screen with rays towards a light's projection, as Bend Studio's BuildDispatchList does (bend_sss_cpu.h,
        /// Apache License 2.0, Sony Interactive Entertainment 2023; see Shaders/Shadows/ContactShadows.comp), in OpenGL's terms: pixels counted up
        /// from the bottom, as the depth texture's are, and a projection whose depth runs -1 to 1. <paramref name="lightProjection"/> is the light in
        /// clip space before the divide: (direction, 0) for the sun.
        /// </summary>
        private static void BuildDispatchList(Vector4 lightProjection, Vector2i viewport, DispatchList result)
        {
            result.Count = 0;

            // Floating point division in the shader has a limit when the light is very far off the screen, so the xy divide uses a w kept off zero.
            float xyLightW = lightProjection.W;
            float limit = 0.000002f * WaveSize;
            if (xyLightW >= 0f && xyLightW < limit) xyLightW = limit;
            else if (xyLightW < 0f && xyLightW > -limit) xyLightW = -limit;

            result.LightCoordinate = new Vector4(
                ((lightProjection.X / xyLightW) * 0.5f + 0.5f) * viewport.X,
                ((lightProjection.Y / xyLightW) * 0.5f + 0.5f) * viewport.Y,
                lightProjection.W == 0f ? 0f : (lightProjection.Z / lightProjection.W) * 0.5f + 0.5f,
                lightProjection.W > 0f ? 1f : -1f);

            int lightX = (int)(result.LightCoordinate.X + 0.5f);
            int lightY = (int)(result.LightCoordinate.Y + 0.5f);

            // The bounds, inclusive, relative to the light: on the stack, as this runs every frame.
            Span<int> biased = stackalloc int[4];
            biased[0] = 0 - lightX;
            biased[1] = -(viewport.Y - lightY);
            biased[2] = viewport.X - lightX;
            biased[3] = -(0 - lightY);

            // Four quadrants round the light, each a rectangle with a corner on it, broken in two along its longer side if it isn't square.
            for (int q = 0; q < 4; q++)
            {
                bool vertical = q == 0 || q == 3;

                int b0 = Math.Max(0, (q & 1) != 0 ? biased[0] : -biased[2]) / WaveSize;
                int b1 = Math.Max(0, (q & 2) != 0 ? biased[1] : -biased[3]) / WaveSize;
                int b2 = Math.Max(0, ((q & 1) != 0 ? biased[2] : -biased[0]) + WaveSize * (vertical ? 1 : 2) - 1) / WaveSize;
                int b3 = Math.Max(0, ((q & 2) != 0 ? biased[3] : -biased[1]) + WaveSize * (vertical ? 2 : 1) - 1) / WaveSize;

                if (b2 - b0 <= 0 || b3 - b1 <= 0) continue;

                int biasX = q == 2 || q == 3 ? 1 : 0;
                int biasY = q == 1 || q == 3 ? 1 : 0;

                int index = result.Count++;
                ref Dispatch dispatch = ref result.At(index);
                dispatch.WaveCount = new Vector3i(WaveSize, b2 - b0, b3 - b1);
                dispatch.WaveOffset = new Vector2i(((q & 1) != 0 ? b0 : -b2) + biasX, ((q & 2) != 0 ? -b3 : b1) + biasY);

                // The far corner of this quadrant from the light, as the diagonal ray meets the edge of the bounds there.
                int axisDelta = +biased[0] - biased[1];
                if (q == 1) axisDelta = +biased[2] + biased[1];
                if (q == 2) axisDelta = -biased[0] - biased[3];
                if (q == 3) axisDelta = -biased[2] + biased[3];
                axisDelta = (axisDelta + WaveSize - 1) / WaveSize;

                if (axisDelta > 0)
                {
                    int index2 = result.Count++;
                    ref Dispatch dispatch2 = ref result.At(index2);
                    dispatch2 = dispatch;

                    if (q == 0)
                    {
                        // Split on y; the split becomes one larger on x.
                        dispatch2.WaveCount.Z = Math.Min(dispatch.WaveCount.Z, axisDelta);
                        dispatch.WaveCount.Z -= dispatch2.WaveCount.Z;
                        dispatch2.WaveOffset.Y = dispatch.WaveOffset.Y + dispatch.WaveCount.Z;
                        dispatch2.WaveOffset.X--;
                        dispatch2.WaveCount.Y++;
                    }
                    if (q == 1)
                    {
                        // Split on x; the split becomes one larger on y.
                        dispatch2.WaveCount.Y = Math.Min(dispatch.WaveCount.Y, axisDelta);
                        dispatch.WaveCount.Y -= dispatch2.WaveCount.Y;
                        dispatch2.WaveOffset.X = dispatch.WaveOffset.X + dispatch.WaveCount.Y;
                        dispatch2.WaveCount.Z++;
                    }
                    if (q == 2)
                    {
                        // Split on x; the split becomes one larger on y.
                        dispatch2.WaveCount.Y = Math.Min(dispatch.WaveCount.Y, axisDelta);
                        dispatch.WaveCount.Y -= dispatch2.WaveCount.Y;
                        dispatch.WaveOffset.X += dispatch2.WaveCount.Y;
                        dispatch2.WaveCount.Z++;
                        dispatch2.WaveOffset.Y--;
                    }
                    if (q == 3)
                    {
                        // Split on y; the split becomes one larger on x.
                        dispatch2.WaveCount.Z = Math.Min(dispatch.WaveCount.Z, axisDelta);
                        dispatch.WaveCount.Z -= dispatch2.WaveCount.Z;
                        dispatch.WaveOffset.Y += dispatch2.WaveCount.Z;
                        dispatch2.WaveCount.Y++;
                    }

                    // Either that's too small to be worth a dispatch is dropped.
                    if (dispatch2.WaveCount.Y <= 0 || dispatch2.WaveCount.Z <= 0)
                    {
                        dispatch2 = result[--result.Count];
                    }
                    if (dispatch.WaveCount.Y <= 0 || dispatch.WaveCount.Z <= 0)
                    {
                        dispatch = result[--result.Count];
                    }
                }
            }

            // The shader wants the offsets in pixels.
            for (int i = 0; i < result.Count; i++)
            {
                ref Dispatch dispatch = ref result.At(i);
                dispatch.WaveOffset *= WaveSize;
            }
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            CreateTextures(eventArgs.Size);
        }

        public override void CleanUp()
        {
            _shader.cleanUp();
            _blurShader.cleanUp();
            if (_raw != 0) GL.DeleteTexture(_raw);
            if (_shadow != 0) GL.DeleteTexture(_shadow);
        }
    }
}
