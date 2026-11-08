using Dino_Engine.Particles;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_Engine.Rendering.Renderers.PosGeometry
{
    /// <summary>
    /// Grows the flipbooks flames and smoke are drawn from, on the GPU at load: nothing about a particle's picture is stored. Flames are a fire simulated in two dimensions
    /// (Particles/Bake/Fire_Step.comp) and filmed: two that go on for good, a broad one and a single tongue, whose ends are blended into their starts so they loop, and gouts of
    /// fire that rise and break up once. Puffs of smoke are volumes, each frame grown, lit from six ways through itself and filmed into six-way lightmaps (Particles/Bake/Puff_Bake.comp).
    /// The layout is in Shaders/Particles/ParticleFlipbooks.glsl, whose constants these have to match.
    /// </summary>
    public sealed class ParticleBaker
    {
        public const int FlameWidth = 96;
        public const int FlameHeight = 144;
        public const int FlameLoopFrames = 48;
        public const int FlameTongueFrames = 32;
        public const int FlameTongues = 3;
        public const int FlameLayers = FlameLoopFrames * 2 + FlameTongueFrames * FlameTongues;
        public const int PuffSize = 128;
        public const int PuffFrames = 32;
        public const int PuffVariants = 4;
        public const int PuffLayers = PuffFrames * PuffVariants;

        private const int PuffDepth = 64;
        private const int SimWidth = 128;
        private const int SimHeight = 192;
        private const float FrameSeconds = 1f / 30f;
        private const int StepsPerFrame = 3;
        private const int JacobiIterations = 30;
        private const int LoopWarmupSteps = 360;
        private const int LoopBlendFrames = 12;
        private const int TongueFuelSteps = 7;

        private const int StageSource = 0, StageCurl = 1, StageForces = 2, StageAdvect = 3, StageCorrect = 4, StageDivergence = 5, StageJacobi = 6, StageProject = 7, StageCapture = 8, StageBlend = 9;
        private const int PuffDensity = 0, PuffSweepX = 1, PuffSweepY = 2, PuffRender = 3;

        /// <summary>A fire to film: the bed it burns from and how it burns.</summary>
        private readonly record struct FireRecipe(float BedWidth, float BedTop, float Fuel, float Unevenness, float Jet, float Buoyancy, float Vorticity, float Burn, float Heat, float Cooling, float Fade, float Seed, bool Ball = false, float Pilot = 0.6f);

        // A broad fire off a bed of fuel, as a campfire or a patch of burning ground: several tongues licking up off clumps of fuel.
        private static readonly FireRecipe Hearth = new(BedWidth: 0.28f, BedTop: 16f, Fuel: 11f, Unevenness: 1f, Jet: 55f, Buoyancy: 480f, Vorticity: 16f, Burn: 3.2f, Heat: 0.5f, Cooling: 2.1f, Fade: 1.15f, Seed: 0.31f);
        // One tall tongue off a small source, as a candle or a torch.
        private static readonly FireRecipe Wick = new(BedWidth: 0.12f, BedTop: 12f, Fuel: 18f, Unevenness: 0.45f, Jet: 60f, Buoyancy: 520f, Vorticity: 6f, Burn: 2.6f, Heat: 0.5f, Cooling: 1.9f, Fade: 0.85f, Seed: 0.77f);
        // A gout of fire that leaps up off a puff of fuel, breaks up and is gone.
        private static readonly FireRecipe Tongue = new(BedWidth: 0.12f, BedTop: 40f, Fuel: 60f, Unevenness: 0.8f, Jet: 120f, Buoyancy: 320f, Vorticity: 14f, Burn: 4f, Heat: 0.45f, Cooling: 2.4f, Fade: 1.6f, Seed: 0.13f, Ball: true, Pilot: 0.9f);

        private readonly ShaderProgram _fire = new ShaderProgram("Particles/Bake/Fire_Step.comp");
        private readonly ShaderProgram _puff = new ShaderProgram("Particles/Bake/Puff_Bake.comp");

        public int FlameBook { get; }
        public int PuffBookA { get; }
        public int PuffBookB { get; }

        /// <summary>Milliseconds the flipbooks took to grow.</summary>
        public double BakeMilliseconds { get; }

        public ParticleBaker()
        {
            var clock = Stopwatch.StartNew();
            FlameBook = CreateArray(FlameWidth, FlameHeight, FlameLayers);
            PuffBookA = CreateArray(PuffSize, PuffSize, PuffLayers);
            PuffBookB = CreateArray(PuffSize, PuffSize, PuffLayers);

            BakeFlames();
            BakePuffs();

            foreach (int book in new[] { FlameBook, PuffBookA, PuffBookB })
            {
                GL.BindTexture(TextureTarget.Texture2DArray, book);
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            }
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            GL.Finish();
            BakeMilliseconds = clock.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Particles: flipbooks grown in {BakeMilliseconds:0} ms.");
        }

        private static int CreateArray(int width, int height, int layers)
        {
            int levels = 1 + (int)MathF.Floor(MathF.Log2(Math.Max(width, height)));
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, levels, SizedInternalFormat.Rgba16f, width, height, layers);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return texture;
        }

        private static int CreateGrid(SizedInternalFormat format)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, format, SimWidth, SimHeight);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        // ---------------------------------------------------------------- flames

        private int[] _velocity = Array.Empty<int>();
        private int[] _fields = Array.Empty<int>();
        private int[] _pressure = Array.Empty<int>();
        private int _scratch;
        private int _captured;
        private float _simTime;

        private void BakeFlames()
        {
            _velocity = new[] { CreateGrid(SizedInternalFormat.Rg32f), CreateGrid(SizedInternalFormat.Rg32f), CreateGrid(SizedInternalFormat.Rg32f) };
            _fields = new[] { CreateGrid(SizedInternalFormat.Rgba32f), CreateGrid(SizedInternalFormat.Rgba32f), CreateGrid(SizedInternalFormat.Rgba32f) };
            _pressure = new[] { CreateGrid(SizedInternalFormat.R32f), CreateGrid(SizedInternalFormat.R32f) };
            _scratch = CreateGrid(SizedInternalFormat.R32f);
            _captured = CreateArray(FlameWidth, FlameHeight, FlameLoopFrames + LoopBlendFrames);

            _fire.bind();
            _fire.loadUniformInt("velocityIn", 0);
            _fire.loadUniformInt("fieldsIn", 1);
            _fire.loadUniformInt("scalarIn", 2);
            _fire.loadUniformInt("velocityHat", 3);
            _fire.loadUniformInt("fieldsHat", 4);
            _fire.loadUniformInt("capturedFrames", 5);
            _fire.loadUniformVector2i("gridSize", new Vector2i(SimWidth, SimHeight));
            _fire.loadUniformVector2i("captureSize", new Vector2i(FlameWidth, FlameHeight));
            _fire.loadUniformFloat("dt", FrameSeconds / StepsPerFrame);
            _fire.loadUniformFloat("frameSeconds", FrameSeconds);

            // Every image unit holds something, whatever a stage writes.
            BindImage(0, _velocity[2], SizedInternalFormat.Rg32f);
            BindImage(1, _fields[2], SizedInternalFormat.Rgba32f);
            BindImage(2, _scratch, SizedInternalFormat.R32f);
            GL.BindImageTexture(3, FlameBook, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);

            Loop(Hearth, 0);
            Loop(Wick, FlameLoopFrames);
            for (int i = 0; i < FlameTongues; i++) Gout(Tongue with { Seed = Tongue.Seed + i * 0.29f, BedWidth = Tongue.BedWidth * (1f + 0.25f * i) }, FlameLoopFrames * 2 + i * FlameTongueFrames);

            GL.UseProgram(0);
            foreach (int texture in _velocity.Concat(_fields).Concat(_pressure).Append(_scratch).Append(_captured)) GL.DeleteTexture(texture);
        }

        // A fire that goes on: let it settle, film it a little longer than the loop, and blend the extra frames into the start so the end runs back into the beginning.
        private void Loop(FireRecipe recipe, int firstLayer)
        {
            Clear(recipe);
            for (int i = 0; i < LoopWarmupSteps; i++) Step(recipe, true);

            int filmed = FlameLoopFrames + LoopBlendFrames;
            GL.BindImageTexture(3, _captured, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            for (int frame = 0; frame < filmed; frame++)
            {
                for (int i = 0; i < StepsPerFrame; i++) Step(recipe, true);
                Capture(frame);
            }

            GL.BindImageTexture(3, FlameBook, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            Bind(5, TextureTarget.Texture2DArray, _captured);
            _fire.loadUniformInt("stage", StageBlend);
            for (int frame = 0; frame < FlameLoopFrames; frame++)
            {
                bool blended = frame < LoopBlendFrames;
                float share = blended ? Smooth((frame + 0.5f) / LoopBlendFrames) : 1f;
                _fire.loadUniformInt("blendFrom", blended ? FlameLoopFrames + frame : frame);
                _fire.loadUniformInt("blendTo", frame);
                _fire.loadUniformFloat("blendShare", share);
                _fire.loadUniformInt("captureLayer", firstLayer + frame);
                DispatchFrame();
            }
            GL.Flush();
        }

        // A gout of fire: a puff of fuel, lit, filmed as it rises and burns out.
        private void Gout(FireRecipe recipe, int firstLayer)
        {
            Clear(recipe);
            int step = 0;
            for (int frame = 0; frame < FlameTongueFrames; frame++)
            {
                for (int i = 0; i < StepsPerFrame; i++) Step(recipe, step++ < TongueFuelSteps);
                Capture(firstLayer + frame);
            }
            GL.Flush();
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private void Clear(FireRecipe recipe)
        {
            float[] zero = new float[SimWidth * SimHeight * 4];
            foreach (int texture in _velocity.Concat(_fields).Concat(_pressure).Append(_scratch))
            {
                GL.BindTexture(TextureTarget.Texture2D, texture);
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, SimWidth, SimHeight, PixelFormat.Rgba, PixelType.Float, zero);
            }
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _simTime = 0f;

            _fire.loadUniformFloat("buoyancy", recipe.Buoyancy);
            _fire.loadUniformFloat("vorticity", recipe.Vorticity);
            _fire.loadUniformFloat("burnRate", recipe.Burn);
            _fire.loadUniformFloat("heatRelease", recipe.Heat);
            _fire.loadUniformFloat("cooling", recipe.Cooling);
            _fire.loadUniformFloat("fade", recipe.Fade);
            _fire.loadUniformFloat("sourceNoise", recipe.Unevenness);
            _fire.loadUniformFloat("jet", recipe.Jet);
            _fire.loadUniformFloat("seed", recipe.Seed);
            _fire.loadUniformFloat("sourceRound", recipe.Ball ? 1f : 0f);
            _fire.loadUniformFloat("pilot", recipe.Pilot);
        }

        // One step of the fire. Fields live in slot 0 between stages, and slots 1 and 2 are where a stage writes.
        private void Step(FireRecipe recipe, bool fed)
        {
            float dt = FrameSeconds / StepsPerFrame;
            _simTime += dt;
            _fire.loadUniformFloat("simTime", _simTime);
            _fire.loadUniformVector4f("source", new Vector4(SimWidth * 0.5f, SimWidth * recipe.BedWidth, recipe.BedTop, fed ? recipe.Fuel : 0f));

            Stage(StageSource, velocityIn: _velocity[0], fieldsIn: _fields[0], velocityOut: _velocity[1], fieldsOut: _fields[1]);
            Swap(_velocity, 0, 1);
            Swap(_fields, 0, 1);

            Stage(StageCurl, velocityIn: _velocity[0], scalarOut: _scratch);
            Stage(StageForces, velocityIn: _velocity[0], fieldsIn: _fields[0], scalarIn: _scratch, velocityOut: _velocity[1]);
            Swap(_velocity, 0, 1);

            Stage(StageAdvect, velocityIn: _velocity[0], fieldsIn: _fields[0], velocityOut: _velocity[1], fieldsOut: _fields[1]);
            Stage(StageCorrect, velocityIn: _velocity[0], fieldsIn: _fields[0], velocityHat: _velocity[1], fieldsHat: _fields[1], velocityOut: _velocity[2], fieldsOut: _fields[2]);
            Swap(_velocity, 0, 2);
            Swap(_fields, 0, 2);

            Stage(StageDivergence, velocityIn: _velocity[0], scalarOut: _scratch);
            for (int i = 0; i < JacobiIterations; i++)
            {
                // The divergence goes in as fieldsHat: see the shader.
                Stage(StageJacobi, scalarIn: _pressure[0], fieldsHat: _scratch, scalarOut: _pressure[1]);
                Swap(_pressure, 0, 1);
            }
            Stage(StageProject, velocityIn: _velocity[0], scalarIn: _pressure[0], velocityOut: _velocity[1]);
            Swap(_velocity, 0, 1);
        }

        private void Capture(int layer)
        {
            Bind(0, TextureTarget.Texture2D, _velocity[0]);
            Bind(1, TextureTarget.Texture2D, _fields[0]);
            _fire.loadUniformInt("stage", StageCapture);
            _fire.loadUniformInt("captureLayer", layer);
            DispatchFrame();
        }

        private void DispatchFrame()
        {
            GL.DispatchCompute((FlameWidth + 7) / 8, (FlameHeight + 7) / 8, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        private void Stage(int stage, int velocityIn = 0, int fieldsIn = 0, int scalarIn = 0, int velocityHat = 0, int fieldsHat = 0, int velocityOut = 0, int fieldsOut = 0, int scalarOut = 0)
        {
            if (velocityIn != 0) Bind(0, TextureTarget.Texture2D, velocityIn);
            if (fieldsIn != 0) Bind(1, TextureTarget.Texture2D, fieldsIn);
            if (scalarIn != 0) Bind(2, TextureTarget.Texture2D, scalarIn);
            if (velocityHat != 0) Bind(3, TextureTarget.Texture2D, velocityHat);
            if (fieldsHat != 0) Bind(4, TextureTarget.Texture2D, fieldsHat);
            if (velocityOut != 0) BindImage(0, velocityOut, SizedInternalFormat.Rg32f);
            if (fieldsOut != 0) BindImage(1, fieldsOut, SizedInternalFormat.Rgba32f);
            if (scalarOut != 0) BindImage(2, scalarOut, SizedInternalFormat.R32f);
            _fire.loadUniformInt("stage", stage);
            GL.DispatchCompute((SimWidth + 7) / 8, (SimHeight + 7) / 8, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        private static void Swap(int[] slots, int a, int b) => (slots[a], slots[b]) = (slots[b], slots[a]);

        private static void Bind(int unit, TextureTarget target, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(target, texture);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private static void BindImage(int unit, int texture, SizedInternalFormat format)
        {
            GL.BindImageTexture(unit, texture, 0, false, 0, TextureAccess.WriteOnly, format);
        }

        // ---------------------------------------------------------------- smoke

        private void BakePuffs()
        {
            int density = CreateVolume(SizedInternalFormat.R16f);
            int sideX = CreateVolume(SizedInternalFormat.Rg16f);
            int sideY = CreateVolume(SizedInternalFormat.Rg16f);

            _puff.bind();
            _puff.loadUniformInt("density", 0);
            _puff.loadUniformInt("sideX", 1);
            _puff.loadUniformInt("sideY", 2);
            _puff.loadUniformVector3i("volumeSize", new Vector3i(PuffSize, PuffSize, PuffDepth));
            GL.BindImageTexture(0, density, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.R16f);
            GL.BindImageTexture(1, sideX, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rg16f);
            GL.BindImageTexture(2, sideY, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rg16f);
            GL.BindImageTexture(3, PuffBookA, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            GL.BindImageTexture(4, PuffBookB, 0, true, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            Bind(0, TextureTarget.Texture3D, density);
            Bind(1, TextureTarget.Texture3D, sideX);
            Bind(2, TextureTarget.Texture3D, sideY);

            for (int variant = 0; variant < PuffVariants; variant++)
            {
                _puff.loadUniformFloat("puffSeed", 0.21f + variant * 0.137f);
                for (int frame = 0; frame < PuffFrames; frame++)
                {
                    _puff.loadUniformFloat("puffTime", frame / (float)(PuffFrames - 1));
                    _puff.loadUniformInt("puffLayer", variant * PuffFrames + frame);
                    PuffStage(PuffDensity, PuffSize / 8, PuffSize / 8, PuffDepth);
                    PuffStage(PuffSweepX, PuffSize / 8, PuffDepth / 8, 1);
                    PuffStage(PuffSweepY, PuffSize / 8, PuffDepth / 8, 1);
                    PuffStage(PuffRender, PuffSize / 8, PuffSize / 8, 1);
                }
                GL.Flush();
            }

            GL.UseProgram(0);
            for (int unit = 0; unit < 5; unit++) GL.BindImageTexture(unit, 0, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba16f);
            GL.DeleteTexture(density);
            GL.DeleteTexture(sideX);
            GL.DeleteTexture(sideY);
        }

        private void PuffStage(int stage, int x, int y, int z)
        {
            _puff.loadUniformInt("stage", stage);
            GL.DispatchCompute(x, y, z);
            GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit | MemoryBarrierFlags.ShaderImageAccessBarrierBit);
        }

        private static int CreateVolume(SizedInternalFormat format)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, texture);
            GL.TexStorage3D(TextureTarget3d.Texture3D, 1, format, PuffSize, PuffSize, PuffDepth);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.BindTexture(TextureTarget.Texture3D, 0);
            return texture;
        }

        // ---------------------------------------------------------------- looking at them

        /// <summary>
        /// Writes every frame of the flipbooks to pictures in <paramref name="folder"/>, as they'd be seen: flames through the fire palette and tone mapped, over black; puffs lit by a
        /// sun up and to the right and a blue sky, over grey.
        /// </summary>
        public void WriteSheets(string folder)
        {
            Directory.CreateDirectory(folder);
            WriteFlames(Path.Combine(folder, "flames.png"));
            WritePuffs(Path.Combine(folder, "smoke.png"));
            WriteLightmaps(Path.Combine(folder, "smoke-lightmaps.png"));
        }

        // The six lightmaps of the first puff's frames on their own, a row each: lit from the right, the left, above, below, in front and behind, and its alpha last.
        private void WriteLightmaps(string path)
        {
            float[] a = ReadArray(PuffBookA, PuffSize, PuffSize, PuffLayers);
            float[] b = ReadArray(PuffBookB, PuffSize, PuffSize, PuffLayers);
            const int frames = 8;
            const int rows = 7;
            int width = frames * PuffSize;
            int height = rows * PuffSize;
            byte[] pixels = new byte[width * height * 4];
            for (int row = 0; row < rows; row++)
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    int layer = frame * (PuffFrames / frames);
                    for (int y = 0; y < PuffSize; y++)
                    {
                        for (int x = 0; x < PuffSize; x++)
                        {
                            int at = ((layer * PuffSize + y) * PuffSize + x) * 4;
                            float value = row switch
                            {
                                0 => a[at], 1 => b[at], 2 => a[at + 1], 3 => b[at + 1], 4 => a[at + 2], 5 => b[at + 2], _ => a[at + 3],
                            };
                            Put(pixels, width, frame * PuffSize + x, row * PuffSize + (PuffSize - 1 - y), new Vector3(MathF.Sqrt(Math.Clamp(value, 0f, 1f))));
                        }
                    }
                }
            }
            PngWriter.Write(path, pixels, width, height);
        }

        private void WriteFlames(string path)
        {
            float[] data = ReadArray(FlameBook, FlameWidth, FlameHeight, FlameLayers);
            const int columns = 16;
            int rows = (FlameLayers + columns - 1) / columns;
            int width = columns * FlameWidth;
            int height = rows * FlameHeight;
            byte[] pixels = new byte[width * height * 4];
            for (int layer = 0; layer < FlameLayers; layer++)
            {
                int ox = layer % columns * FlameWidth;
                int oy = layer / columns * FlameHeight;
                for (int y = 0; y < FlameHeight; y++)
                {
                    for (int x = 0; x < FlameWidth; x++)
                    {
                        int at = ((layer * FlameHeight + y) * FlameWidth + x) * 4;
                        float burning = data[at];
                        float heat = burning > 1e-4f ? data[at + 1] / burning : 0f;
                        Vector3 light = Palettes.Sample(ParticlePalette.Fire, heat) * burning;
                        Put(pixels, width, ox + x, oy + (FlameHeight - 1 - y), Shown(light));
                    }
                }
            }
            PngWriter.Write(path, pixels, width, height);
        }

        private void WritePuffs(string path)
        {
            float[] a = ReadArray(PuffBookA, PuffSize, PuffSize, PuffLayers);
            float[] b = ReadArray(PuffBookB, PuffSize, PuffSize, PuffLayers);
            const int columns = 16;
            int rows = (PuffLayers + columns - 1) / columns;
            int width = columns * PuffSize;
            int height = rows * PuffSize;
            byte[] pixels = new byte[width * height * 4];

            // A sun of about 13 low on the right and a little in front, which shows how the smoke shades, and a sky above and ground below.
            Vector3 sun = new Vector3(0.8f, 0.45f, 0.4f).Normalized();
            Vector3 sunLight = new Vector3(13f, 12f, 10.5f) / MathF.PI;
            Vector3 sky = new Vector3(0.55f, 0.7f, 1f) * 0.9f;
            Vector3 ground = new Vector3(0.35f, 0.3f, 0.24f) * 0.6f;
            Vector3 albedo = new Vector3(0.62f, 0.6f, 0.58f);
            Vector3 backdrop = new Vector3(0.9f, 0.9f, 0.9f);

            for (int layer = 0; layer < PuffLayers; layer++)
            {
                int ox = layer % columns * PuffSize;
                int oy = layer / columns * PuffSize;
                for (int y = 0; y < PuffSize; y++)
                {
                    for (int x = 0; x < PuffSize; x++)
                    {
                        // As Particle.frag shades it: the sun through the lightmaps of the ways it comes from, the sky through those in front and to the sides, the ground behind.
                        int at = ((layer * PuffSize + y) * PuffSize + x) * 4;
                        float sides = a[at] + b[at] + a[at + 1] + b[at + 1];
                        Vector3 lit = sunLight * (a[at] * sun.X * sun.X + a[at + 1] * sun.Y * sun.Y + a[at + 2] * sun.Z * sun.Z)
                                    + sky * (a[at + 2] * 0.55f + sides * 0.42f) + ground * (b[at + 2] * 0.3f + sides * 0.1f);
                        float alpha = a[at + 3];
                        Vector3 light = albedo * lit + backdrop * (1f - alpha);
                        Put(pixels, width, ox + x, oy + (PuffSize - 1 - y), Shown(light));
                    }
                }
            }
            PngWriter.Write(path, pixels, width, height);
        }

        private static float[] ReadArray(int texture, int width, int height, int layers)
        {
            float[] data = new float[width * height * layers * 4];
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.GetTexImage(TextureTarget.Texture2DArray, 0, PixelFormat.Rgba, PixelType.Float, data);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return data;
        }

        // As the game shows radiance: its exposure, then the ACES fit, then sRGB.
        private static Vector3 Shown(Vector3 light)
        {
            Vector3 x = light * 0.42f;
            Vector3 mapped = new Vector3(Aces(x.X), Aces(x.Y), Aces(x.Z));
            return new Vector3(Srgb(mapped.X), Srgb(mapped.Y), Srgb(mapped.Z));
        }

        private static float Aces(float x) => Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);

        private static float Srgb(float x) => x <= 0.0031308f ? 12.92f * x : 1.055f * MathF.Pow(x, 1f / 2.4f) - 0.055f;

        private static void Put(byte[] pixels, int width, int x, int y, Vector3 colour)
        {
            int at = (y * width + x) * 4;
            pixels[at] = (byte)Math.Clamp(colour.X * 255f + 0.5f, 0f, 255f);
            pixels[at + 1] = (byte)Math.Clamp(colour.Y * 255f + 0.5f, 0f, 255f);
            pixels[at + 2] = (byte)Math.Clamp(colour.Z * 255f + 0.5f, 0f, 255f);
            pixels[at + 3] = 255;
        }

        public void CleanUp()
        {
            _fire.cleanUp();
            _puff.cleanUp();
            GL.DeleteTexture(FlameBook);
            GL.DeleteTexture(PuffBookA);
            GL.DeleteTexture(PuffBookB);
        }
    }
}
