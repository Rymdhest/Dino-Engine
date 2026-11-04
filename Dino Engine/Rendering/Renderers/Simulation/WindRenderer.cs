using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Rendering.Renderers.Simulation
{
    public struct WindRenderCommand : IRenderCommand
    {
        public WindSingleton wind;
        public WindEmitter[] emitters;
        public Vector2 center;      // World xz the wind map and vegetation regions are centred on.
        public float deltaTime;
    }

    /// <summary>Something pressing plants down (a foot, a body, an impact), on the xz plane.</summary>
    public struct VegetationContact
    {
        public Vector2 center;
        public float radius;
        public float power;     // Radians plants are pressed down at the centre.
        public float exponent;  // Falloff towards the edge: 1 = linear, < 1 = flatter in the middle.
    }

    /// <summary>
    /// The GPU side of the engine's wind, run at the start of every frame before anything is drawn:
    /// 1. Uploads the frame's wind (WindData uniform block, binding <see cref="UniformBinding"/>) for Shaders/Wind.glsl.
    /// 2. Bakes the wind field into the wind map around the camera (Wind_Field.frag).
    /// 3. Steps the vegetation response, a damped spring per texel, plus contacts (Wind_Vegetation.frag).
    /// Consumers bind <see cref="WindMapTexture"/> / <see cref="VegetationBendTexture"/> and include Wind.glsl.
    /// </summary>
    public class WindRenderer : CommandDrivenRenderer<WindRenderCommand>
    {
        public const int UniformBinding = 6;
        public const int WindMapResolution = 512;
        public const float WindMapSize = 256f;
        public const int VegetationResolution = 1024;
        public const float VegetationSize = 160f;

        private const int ContactsPerPass = 32;
        // WindData in Wind.glsl: 9 vec4s, the wave trains, then 3 vec4s per emitter.
        private const int WaveSlot = 9;
        private const int HeaderVec4s = WaveSlot + WindField.WaveTrains;
        private const int UniformFloats = 4 * (HeaderVec4s + 3 * WindField.MaxGpuEmitters);

        private static readonly string[] ContactNames = UniformNames("contacts", ContactsPerPass);
        private static readonly string[] ContactExponentNames = UniformNames("contactExponents", ContactsPerPass);

        private readonly ShaderProgram _fieldShader = new ShaderProgram("Simple.vert", "Wind_Field.frag");
        private readonly ShaderProgram _vegetationShader = new ShaderProgram("Simple.vert", "Wind_Vegetation.frag");

        private readonly FrameBuffer _windMap;
        private readonly FrameBuffer[] _vegetation = new FrameBuffer[2];
        private int _vegetationIndex;
        private bool _vegetationValid;
        private Vector2i _vegetationOriginTexel;

        private readonly int _uniformBuffer;
        private readonly float[] _uniformData = new float[UniformFloats];
        private readonly List<VegetationContact> _contacts = new();
        private readonly List<(float distance, int index)> _emitterOrder = new();

        public WindRenderer() : base("Wind")
        {
            _vegetationShader.bind();
            _vegetationShader.loadUniformInt("previousBend", 0);
            _vegetationShader.loadUniformInt("previousContact", 1);
            _vegetationShader.loadUniformInt("windMap", 2);
            _vegetationShader.unBind();

            FrameBufferSettings windSettings = new FrameBufferSettings(new Vector2i(WindMapResolution));
            windSettings.drawBuffers.Add(StateDrawBuffer(FramebufferAttachment.ColorAttachment0));
            _windMap = new FrameBuffer(windSettings);

            for (int i = 0; i < _vegetation.Length; i++)
            {
                FrameBufferSettings vegetationSettings = new FrameBufferSettings(new Vector2i(VegetationResolution));
                vegetationSettings.drawBuffers.Add(StateDrawBuffer(FramebufferAttachment.ColorAttachment0));
                vegetationSettings.drawBuffers.Add(StateDrawBuffer(FramebufferAttachment.ColorAttachment1));
                _vegetation[i] = new FrameBuffer(vegetationSettings);
            }
            _windMap.unbind();

            // Zeroed data means "no wind yet": Wind.glsl treats an empty region as outside every map.
            _uniformBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferData(BufferTarget.UniformBuffer, UniformFloats * sizeof(float), _uniformData, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _uniformBuffer);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
        }

        /// <summary>Wind map: (velocity.x, velocity.y, velocity.z, gust intensity), see Wind.glsl.</summary>
        public int WindMapTexture => _windMap.GetAttachment(0);

        /// <summary>Vegetation state: xy bend (radians, leaning towards +x / +z), zw angular velocity. Read through vegetationBend() in Wind.glsl.</summary>
        public int VegetationBendTexture => _vegetation[_vegetationIndex].GetAttachment(0);

        /// <summary>Presses plants down this frame. Call every frame while the contact lasts.</summary>
        public void AddVegetationContact(VegetationContact contact) => _contacts.Add(contact);

        private static DrawBufferSettings StateDrawBuffer(FramebufferAttachment attachment)
        {
            return new DrawBufferSettings(attachment)
            {
                formatInternal = PixelInternalFormat.Rgba16f,
                pixelType = PixelType.Float,
                wrapMode = TextureWrapMode.ClampToEdge,
            };
        }

        private static string[] UniformNames(string array, int length)
        {
            string[] names = new string[length];
            for (int i = 0; i < length; i++) names[i] = array + "[" + i + "]";
            return names;
        }

        public override void Update()
        {
            // Runs before the systems add this frame's contacts, and also when the previous frame never rendered.
            _contacts.Clear();
        }

        internal override void Prepare(RenderEngine renderEngine)
        {
        }

        internal override void Finish(RenderEngine renderEngine)
        {
        }

        internal override void Render(RenderEngine renderEngine)
        {
            if (commands.Count == 0) return;

            // Only the newest command is current; older ones pile up when frames skip the render (e.g. the F8 debug view).
            PerformCommand(commands[commands.Count - 1], renderEngine);
            commands.Clear();
        }

        public override void PerformCommand(WindRenderCommand command, RenderEngine renderEngine)
        {
            float windTexel = WindMapSize / WindMapResolution;
            Vector2 windOrigin = new Vector2(
                MathF.Floor((command.center.X - WindMapSize * 0.5f) / windTexel) * windTexel,
                MathF.Floor((command.center.Y - WindMapSize * 0.5f) / windTexel) * windTexel);

            // The vegetation region moves in whole texels so its state can be carried over exactly.
            float vegetationTexel = VegetationSize / VegetationResolution;
            Vector2i originTexel = new Vector2i(
                (int)MathF.Floor((command.center.X - VegetationSize * 0.5f) / vegetationTexel),
                (int)MathF.Floor((command.center.Y - VegetationSize * 0.5f) / vegetationTexel));
            Vector2i shift = _vegetationValid ? originTexel - _vegetationOriginTexel : Vector2i.Zero;
            bool reset = !_vegetationValid;
            _vegetationOriginTexel = originTexel;
            Vector2 vegetationOrigin = new Vector2(originTexel.X * vegetationTexel, originTexel.Y * vegetationTexel);

            UploadWindData(command, windOrigin, vegetationOrigin);

            _windMap.bind();
            _fieldShader.bind();
            renderEngine.ScreenQuadRenderer.Render(clearColor: false);

            StepVegetation(command.wind, Math.Clamp(command.deltaTime, 0f, 0.1f), shift, reset, renderEngine);
            _vegetationValid = true;
        }

        private void UploadWindData(WindRenderCommand command, Vector2 windOrigin, Vector2 vegetationOrigin)
        {
            WindSingleton wind = command.wind;
            Array.Clear(_uniformData);

            // The GPU takes the emitters nearest the regions' centre. CPU sampling (Wind.Sample) uses all of them.
            WindEmitter[] emitters = command.emitters ?? Array.Empty<WindEmitter>();
            _emitterOrder.Clear();
            for (int i = 0; i < emitters.Length; i++)
            {
                if (!(emitters[i].Radius > 0f)) continue;
                float distance = MathF.Max((emitters[i].Position.Xz - command.center).Length - emitters[i].Radius, 0f);
                _emitterOrder.Add((distance, i));
            }
            _emitterOrder.Sort((a, b) => a.distance.CompareTo(b.distance));
            int emitterCount = Math.Min(_emitterOrder.Count, WindField.MaxGpuEmitters);

            Vector2 along = WindSingleton.Towards(wind.Heading);
            Write(0, wind.Velocity.X, wind.Velocity.Y, wind.LiveSpeed, emitterCount);
            Write(1, along.X, along.Y, wind.Prevailing.X, wind.Prevailing.Y);
            Write(2, wind.Gustiness, wind.Turbulence, WindField.InverseScale(wind.GustScale), WindField.InverseScale(wind.TurbulenceScale));
            Write(3, wind.GustOffset.X, wind.GustOffset.Y, wind.GustPhase, 0f);
            Write(4, wind.TurbulenceOffset.X, wind.TurbulenceOffset.Y, wind.TurbulencePhase, 0f);
            Write(5, windOrigin.X, windOrigin.Y, WindMapSize, WindMapSize);
            Write(6, vegetationOrigin.X, vegetationOrigin.Y, VegetationSize, VegetationSize);
            Write(7, wind.VegetationMaxBend, wind.VegetationReferenceSpeed, wind.VegetationFlutter, wind.FlutterCycles);
            Write(8, WindField.WaveLeanAt(wind.WaveLean, wind.LiveSpeed), WindField.WaveStrength(wind.LiveSpeed), wind.WaveWander, 0f);
            for (int i = 0; i < WindField.WaveTrains; i++)
            {
                Vector4 train = WindField.WaveTrain(wind, i);
                Write(WaveSlot + i, train.X, train.Y, train.Z, train.W);
            }
            for (int i = 0; i < emitterCount; i++)
            {
                WindEmitter emitter = emitters[_emitterOrder[i].index];
                int slot = HeaderVec4s + i * 3;
                Write(slot, emitter.Position.X, emitter.Position.Y, emitter.Position.Z, emitter.Radius);
                Write(slot + 1, emitter.Direction.X, emitter.Direction.Y, emitter.Direction.Z, emitter.Strength);
                Write(slot + 2, (float)emitter.Type, emitter.Age, emitter.Duration, 0f);
            }

            GL.BindBuffer(BufferTarget.UniformBuffer, _uniformBuffer);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, UniformFloats * sizeof(float), _uniformData);
            GL.BindBuffer(BufferTarget.UniformBuffer, 0);
            // Re-assert the binding in case anything else bound this point.
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, UniformBinding, _uniformBuffer);
        }

        private void Write(int vec4Index, float x, float y, float z, float w)
        {
            int offset = vec4Index * 4;
            _uniformData[offset] = x;
            _uniformData[offset + 1] = y;
            _uniformData[offset + 2] = z;
            _uniformData[offset + 3] = w;
        }

        /// <summary>
        /// One pass integrates the springs and applies up to <see cref="ContactsPerPass"/> contacts; any further
        /// contacts are applied by extra passes that only press plants down.
        /// </summary>
        private void StepVegetation(WindSingleton wind, float deltaTime, Vector2i shift, bool reset, RenderEngine renderEngine)
        {
            _vegetationShader.bind();
            _vegetationShader.loadUniformFloat("deltaTime", deltaTime);
            _vegetationShader.loadUniformFloat("naturalFrequency", MathF.Max(wind.VegetationFrequency, 0f));
            _vegetationShader.loadUniformFloat("dampingRatio", MathF.Max(wind.VegetationDamping, 0f));
            _vegetationShader.loadUniformFloat("flattenRecoveryTime", wind.FlattenRecoveryTime);

            int passes = Math.Max(1, (_contacts.Count + ContactsPerPass - 1) / ContactsPerPass);
            for (int pass = 0; pass < passes; pass++)
            {
                FrameBuffer source = _vegetation[_vegetationIndex];
                FrameBuffer target = _vegetation[1 - _vegetationIndex];
                target.bind();

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, source.GetAttachment(0));
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, source.GetAttachment(1));
                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.Texture2D, WindMapTexture);

                bool first = pass == 0;
                _vegetationShader.loadUniformInt("integrate", first ? 1 : 0);
                _vegetationShader.loadUniformInt("resetState", first && reset ? 1 : 0);
                _vegetationShader.loadUniformVector2f("texelShift", first ? new Vector2(shift.X, shift.Y) : Vector2.Zero);

                int start = pass * ContactsPerPass;
                int count = Math.Clamp(_contacts.Count - start, 0, ContactsPerPass);
                _vegetationShader.loadUniformInt("contactCount", count);
                for (int i = 0; i < count; i++)
                {
                    VegetationContact contact = _contacts[start + i];
                    _vegetationShader.loadUniformVector4f(ContactNames[i], new Vector4(contact.center.X, contact.center.Y, contact.radius, contact.power));
                    _vegetationShader.loadUniformFloat(ContactExponentNames[i], contact.exponent);
                }

                renderEngine.ScreenQuadRenderer.Render(clearColor: false);
                _vegetationIndex = 1 - _vegetationIndex;
            }
            _vegetationShader.unBind();
        }

        public override void CleanUp()
        {
            _fieldShader.cleanUp();
            _vegetationShader.cleanUp();
            _windMap.cleanUp();
            foreach (FrameBuffer buffer in _vegetation) buffer.cleanUp();
            GL.DeleteBuffer(_uniformBuffer);
        }
    }
}
