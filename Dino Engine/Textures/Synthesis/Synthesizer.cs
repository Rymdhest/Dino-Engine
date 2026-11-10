using Dino_Engine.Core;
using Dino_Engine.Rendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>
    /// What every canvas draws with: the synthesis shaders (Shaders/Texture/), compiled once, the pool of images, and a framebuffer to draw into. Every pass is one
    /// triangle over the target, made from vertex numbers alone, so there's nothing to bind but an empty vertex array.
    /// </summary>
    internal sealed class Synthesizer : IDisposable
    {
        public readonly ShaderProgram Noise = Make("Synth_Noise.frag");
        public readonly ShaderProgram Cells = Make("Synth_Cells.frag");
        public readonly ShaderProgram Bricks = Make("Synth_Bricks.frag");
        public readonly ShaderProgram Scatter = Make("Synth_Scatter.frag");
        public readonly ShaderProgram Weave = Make("Synth_Weave.frag");
        public readonly ShaderProgram Mail = Make("Synth_Mail.frag");
        public readonly ShaderProgram Facets = Make("Synth_Facets.frag");
        public readonly ShaderProgram Grain = Make("Synth_Grain.frag");
        public readonly ShaderProgram Op = Make("Synth_Op.frag");
        public readonly ShaderProgram Ramp = Make("Synth_Ramp.frag");
        public readonly ShaderProgram Warp = Make("Synth_Warp.frag");
        public readonly ShaderProgram Blur = Make("Synth_Blur.frag");
        public readonly ShaderProgram SlopeBlur = Make("Synth_SlopeBlur.frag");
        public readonly ShaderProgram Morph = Make("Synth_Morph.frag");
        public readonly ShaderProgram Curve = Make("Synth_Curve.frag");
        public readonly ShaderProgram Coordinates = Make("Synth_Coordinates.frag");
        public readonly ShaderProgram Normal = Make("Synth_Normal.frag");
        public readonly ShaderProgram Occlusion = Make("Synth_Occlusion.frag");
        public readonly ShaderProgram Downsample = Make("Synth_Downsample.frag");
        public readonly ShaderProgram Pack = Make("Synth_Pack.frag");
        public readonly ShaderProgram Reduce = new ShaderProgram("Texture/Synth_Reduce.comp");

        public readonly ImagePool Pool = new();

        private readonly int _framebuffer = GL.GenFramebuffer();
        private readonly int _emptyVertices = GL.GenVertexArray();
        private readonly int _sums = GL.GenBuffer();
        private readonly int _histogram = GL.GenBuffer();
        private readonly int _curve = GL.GenTexture();

        public const int HistogramBins = 4096;

        private static ShaderProgram Make(string fragment) => new ShaderProgram("Texture/Synth.vert", "Texture/" + fragment);

        public Synthesizer()
        {
            Edit(_curve);
            GL.TexStorage2D(TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, HistogramBins, 1);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            Edit(0);
        }

        /// <summary>
        /// The texture unit a texture is bound to while it's made or changed, which no pass reads. Binding a texture to change it binds it to whatever unit is active,
        /// which may hold a pass's input: Equalize once read its own curve back as the field it was spreading, and every patch in every material came out empty.
        /// </summary>
        public const int ScratchUnit = 15;

        /// <summary>Binds <paramref name="texture"/> to <see cref="ScratchUnit"/>, to make or change it without disturbing what a pass has bound.</summary>
        public static void Edit(int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + ScratchUnit);
            GL.BindTexture(TextureTarget.Texture2D, texture);
        }

        /// <summary>Points the framebuffer at <paramref name="targets"/> (each an image's level) and draws <paramref name="shader"/> over them, with whatever's bound.</summary>
        public void Draw(ShaderProgram shader, int size, IReadOnlyList<(int texture, int level)> targets)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            var buffers = new DrawBuffersEnum[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, targets[i].texture, targets[i].level);
                buffers[i] = DrawBuffersEnum.ColorAttachment0 + i;
            }
            for (int i = targets.Count; i < 4; i++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, 0, 0);
            }
            GL.DrawBuffers(buffers.Length, buffers);
            DrawBound(shader, size);
        }

        /// <summary>Draws into one level of a layer of each of three texture arrays, as the finished material is written.</summary>
        public void DrawLayers(ShaderProgram shader, int size, int level, int layer, params int[] arrays)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
            var buffers = new DrawBuffersEnum[arrays.Length];
            for (int i = 0; i < arrays.Length; i++)
            {
                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, arrays[i], level, layer);
                buffers[i] = DrawBuffersEnum.ColorAttachment0 + i;
            }
            for (int i = arrays.Length; i < 4; i++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + i, TextureTarget.Texture2D, 0, 0);
            }
            GL.DrawBuffers(buffers.Length, buffers);
            DrawBound(shader, size);
        }

        private void DrawBound(ShaderProgram shader, int size)
        {
            shader.bind();
            if (shader.HasUniform("targetSize")) shader.loadUniformVector2f("targetSize", new Vector2(size));
            GL.Viewport(0, 0, size, size);
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.ScissorTest);
            GL.DepthMask(false);
            GL.BindVertexArray(_emptyVertices);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            GL.BindVertexArray(0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        public static void BindTexture(int unit, int texture)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, texture);
        }

        /// <summary>The least, most, mean and deviation of a channel of an image, read back.</summary>
        public FieldStatistics Measure(Image image, int channel)
        {
            int groupsX = (image.Size + 15) / 16;
            int groupsY = (image.Size + 15) / 16;
            int groups = groupsX * groupsY;
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _sums);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, groups * 16, IntPtr.Zero, BufferUsageHint.DynamicRead);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _sums);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _histogram);

            Reduce.bind();
            BindTexture(0, image.Texture);
            Reduce.loadUniformInt("inputA", 0);
            Reduce.loadUniformInt("channelA", channel);
            Reduce.loadUniformInt("mode", 0);
            GL.DispatchCompute(groupsX, groupsY, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.BufferUpdateBarrierBit);

            float[] sums = new float[groups * 4];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _sums);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, sums.Length * sizeof(float), sums);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            double least = double.MaxValue, most = double.MinValue, sum = 0, sumSquared = 0;
            for (int g = 0; g < groups; g++)
            {
                least = Math.Min(least, sums[g * 4]);
                most = Math.Max(most, sums[g * 4 + 1]);
                sum += sums[g * 4 + 2];
                sumSquared += sums[g * 4 + 3];
            }
            double count = (double)image.Size * image.Size;
            double mean = sum / count;
            double variance = Math.Max(sumSquared / count - mean * mean, 0.0);
            return new FieldStatistics((float)least, (float)most, (float)mean, (float)Math.Sqrt(variance));
        }

        /// <summary>How many texels of a channel fall in each of <see cref="HistogramBins"/> bins evenly over <paramref name="low"/> to <paramref name="high"/>.</summary>
        public uint[] Histogram(Image image, int channel, float low, float high)
        {
            uint[] counts = new uint[HistogramBins];
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _histogram);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, counts.Length * sizeof(uint), counts, BufferUsageHint.DynamicRead);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, _histogram);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, _sums);

            Reduce.bind();
            BindTexture(0, image.Texture);
            Reduce.loadUniformInt("inputA", 0);
            Reduce.loadUniformInt("channelA", channel);
            Reduce.loadUniformInt("mode", 1);
            Reduce.loadUniformVector2f("range", new Vector2(low, high));
            Reduce.loadUniformInt("bins", HistogramBins);
            GL.DispatchCompute((image.Size + 15) / 16, (image.Size + 15) / 16, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit | MemoryBarrierFlags.BufferUpdateBarrierBit);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _histogram);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, counts.Length * sizeof(uint), counts);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            return counts;
        }

        /// <summary>Level 0 of an image read back, one float per channel it holds: for checks, never for making a material.</summary>
        public float[] Read(Image image)
        {
            bool scalar = image.Format == ImageFormat.Scalar;
            float[] texels = new float[image.Size * image.Size * (scalar ? 1 : 4)];
            Edit(image.Texture);
            GL.GetTexImage(TextureTarget.Texture2D, 0, scalar ? PixelFormat.Red : PixelFormat.Rgba, PixelType.Float, texels);
            return texels;
        }

        /// <summary>Loads a row of <see cref="HistogramBins"/> values for Synth_Curve.frag and binds it to <paramref name="unit"/>.</summary>
        public void BindCurve(int unit, float[] values)
        {
            Edit(_curve);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, HistogramBins, 1, PixelFormat.Red, PixelType.Float, values);
            BindTexture(unit, _curve);
        }

        /// <summary>Lets a pass read only <paramref name="level"/> of an image, so that it can write the next one up without a feedback loop.</summary>
        public static void ReadOnlyLevel(int texture, int level)
        {
            Edit(texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, level);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, level);
        }

        public static void ReadAllLevels(int texture, int levels)
        {
            Edit(texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, levels - 1);
        }

        public void Dispose()
        {
            foreach (ShaderProgram shader in new[] { Noise, Cells, Bricks, Scatter, Weave, Mail, Facets, Grain, Op, Ramp, Warp, Blur, SlopeBlur, Morph, Curve, Coordinates, Normal, Occlusion, Downsample, Pack, Reduce })
            {
                shader.cleanUp();
            }
            Pool.Dispose();
            GL.DeleteFramebuffer(_framebuffer);
            GL.DeleteVertexArray(_emptyVertices);
            GL.DeleteBuffer(_sums);
            GL.DeleteBuffer(_histogram);
            GL.DeleteTexture(_curve);
        }
    }

    public readonly record struct FieldStatistics(float Least, float Most, float Mean, float Deviation);
}
