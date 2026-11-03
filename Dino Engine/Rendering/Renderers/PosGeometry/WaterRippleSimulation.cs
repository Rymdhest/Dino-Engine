using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>Something pushing on the water: a wading foot, a splash.</summary>
    public struct WaterContact
    {
        public Vector2 Center;
        public float Radius;   // Metres.
        public float Amount;   // Metres the surface is pushed down at the centre (per second for continuous contacts).
    }

    /// <summary>
    /// Ripples things leave in the water, simulated around the camera with the damped wave equation at a fixed 60 Hz
    /// (Water_Ripple_Step.frag). The grid follows the camera in whole texels so its state carries over exactly.
    /// </summary>
    internal sealed class WaterRippleSimulation
    {
        public const int Resolution = 512;
        public const float Size = 64f;
        private const float StepSeconds = 1f / 60f;
        private const int MaxStepsPerFrame = 4;
        private const int MaxContacts = 32;
        // Courant number c dt / dx stays below this; the leapfrog scheme is stable up to 1 / sqrt(2).
        private const float MaxCourant = 0.5f;

        private static readonly string[] ContactNames = Enumerable.Range(0, MaxContacts).Select(i => "contacts[" + i + "]").ToArray();

        private readonly ShaderProgram _stepShader = new ShaderProgram("Simple.vert", "Water/Water_Ripple_Step.frag");
        private readonly FrameBuffer[] _state = new FrameBuffer[2];
        private readonly List<WaterContact> _pendingImpulses = new();
        private readonly List<Vector4> _contacts = new();
        private int _index;
        private bool _valid;
        private Vector2i _originTexel;
        private float _accumulator;

        public WaterRippleSimulation()
        {
            _stepShader.bind();
            _stepShader.loadUniformInt("previousState", 0);
            _stepShader.unBind();

            for (int i = 0; i < _state.Length; i++)
            {
                FrameBufferSettings settings = new FrameBufferSettings(new Vector2i(Resolution));
                settings.drawBuffers.Add(new DrawBufferSettings(FramebufferAttachment.ColorAttachment0)
                {
                    formatInternal = PixelInternalFormat.Rgba16f,
                    pixelType = PixelType.Float,
                    wrapMode = TextureWrapMode.ClampToEdge,
                });
                _state[i] = new FrameBuffer(settings);
            }
            _state[0].unbind();
        }

        public int Texture => _state[_index].GetAttachment(0);

        /// <summary>xy world corner, zw size of the simulated area; zero before the first step.</summary>
        public Vector4 Region
        {
            get
            {
                if (!_valid) return Vector4.Zero;
                float texel = Size / Resolution;
                return new Vector4(_originTexel.X * texel, _originTexel.Y * texel, Size, Size);
            }
        }

        /// <param name="rates">Continuous contacts, Amount in metres per second.</param>
        /// <param name="impulses">One-off contacts, Amount in metres.</param>
        public void Step(Vector2 center, IReadOnlyList<WaterContact> rates, IReadOnlyList<WaterContact> impulses, float deltaTime,
            float waveSpeed, float decayTime, RenderEngine renderEngine)
        {
            _pendingImpulses.AddRange(impulses);

            _accumulator += Math.Clamp(deltaTime, 0f, 0.25f);
            int steps = (int)(_accumulator / StepSeconds);
            if (steps <= 0) return;
            if (steps > MaxStepsPerFrame)
            {
                // A hitch: drop the backlog rather than spending several frames catching up.
                steps = MaxStepsPerFrame;
                _accumulator = 0f;
            }
            else
            {
                _accumulator -= steps * StepSeconds;
            }

            float texel = Size / Resolution;
            Vector2i originTexel = new Vector2i(
                (int)MathF.Floor((center.X - Size * 0.5f) / texel),
                (int)MathF.Floor((center.Y - Size * 0.5f) / texel));
            Vector2i shift = _valid ? originTexel - _originTexel : Vector2i.Zero;
            bool reset = !_valid || Math.Abs(shift.X) >= Resolution || Math.Abs(shift.Y) >= Resolution;
            _originTexel = originTexel;
            Vector4 region = new Vector4(originTexel.X * texel, originTexel.Y * texel, Size, Size);

            float courant = Math.Clamp(MathF.Max(waveSpeed, 0f) * StepSeconds / texel, 0f, MaxCourant);
            float damping = MathF.Exp(-StepSeconds / MathF.Max(decayTime, 0.05f));

            _stepShader.bind();
            _stepShader.loadUniformVector4f("region", region);
            _stepShader.loadUniformFloat("courantSquared", courant * courant);
            _stepShader.loadUniformFloat("damping", damping);
            GL.ActiveTexture(TextureUnit.Texture0);

            for (int s = 0; s < steps; s++)
            {
                _contacts.Clear();
                if (s == 0)
                {
                    foreach (WaterContact impulse in _pendingImpulses) AddContact(impulse, impulse.Amount, region);
                    _pendingImpulses.Clear();
                }
                foreach (WaterContact rate in rates) AddContact(rate, rate.Amount * StepSeconds, region);

                FrameBuffer source = _state[_index];
                FrameBuffer target = _state[1 - _index];
                target.bind();
                GL.BindTexture(TextureTarget.Texture2D, source.GetAttachment(0));
                _stepShader.loadUniformVector2f("texelShift", s == 0 ? new Vector2(shift.X, shift.Y) : Vector2.Zero);
                _stepShader.loadUniformInt("resetState", s == 0 && reset ? 1 : 0);
                _stepShader.loadUniformInt("contactCount", _contacts.Count);
                for (int i = 0; i < _contacts.Count; i++) _stepShader.loadUniformVector4f(ContactNames[i], _contacts[i]);
                renderEngine.ScreenQuadRenderer.Render(clearColor: false);
                _index = 1 - _index;
            }
            _stepShader.unBind();
            _state[_index].unbind();
            _valid = true;
        }

        private void AddContact(WaterContact contact, float amount, Vector4 region)
        {
            if (_contacts.Count >= MaxContacts || !(contact.Radius > 0f) || amount == 0f) return;
            // Skip contacts that can't touch the simulated area.
            if (contact.Center.X + contact.Radius < region.X || contact.Center.X - contact.Radius > region.X + region.Z) return;
            if (contact.Center.Y + contact.Radius < region.Y || contact.Center.Y - contact.Radius > region.Y + region.W) return;
            _contacts.Add(new Vector4(contact.Center.X, contact.Center.Y, contact.Radius, amount));
        }

        public void CleanUp()
        {
            _stepShader.cleanUp();
            foreach (FrameBuffer buffer in _state) buffer.cleanUp();
        }
    }
}
