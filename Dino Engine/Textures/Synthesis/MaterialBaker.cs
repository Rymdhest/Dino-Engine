using Dino_Engine.Rendering;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Textures.Synthesis
{
    /// <summary>
    /// The three texture arrays every material is a layer of (textureUtil.glsl): albedo in sRGB, normal and material in eight bits a channel. A material is made
    /// <see cref="Size"/> across with <see cref="Levels"/> mips, and the arrays keep all but the first <see cref="Dropped"/> of them, which nothing is ever near
    /// enough to read (TextureGenerator.MaterialLevelsDropped).
    /// </summary>
    internal readonly record struct MaterialArrays(int Albedo, int Normal, int Material, int Size, int Levels, int Dropped);

    /// <summary>
    /// Turns a finished <see cref="Surface"/> into its layer of the material arrays. The normal comes from the height at exactly the material's relief, so it agrees
    /// with the parallax that marches the same height to the same depth; the ambient occlusion is baked from the same height at the same scale. Every mip is made
    /// in floats from the one below and only then packed: normals are averaged as vectors and how much they disagreed widens the roughness, so a bumpy material
    /// far off is a rough one rather than a glittering one, and colour is averaged in linear light before it's stored as sRGB.
    /// </summary>
    internal static class MaterialBaker
    {
        public static int LevelsFor(int size) => (int)Math.Floor(Math.Log2(size)) + 1;

        public static void Bake(Synthesizer gpu, Canvas canvas, Surface surface, MaterialArrays arrays, int layer)
        {
            MaterialSpec spec = canvas.Spec;
            int size = canvas.Size;

            Field height = surface.Height.Clamp(0f, 1f);
            bool relief = spec.ReliefMetres > 0f && !height.IsConstant;

            // How far a point looks for what shades it: about six times the relief, within reason, as a crack's walls only hide so much sky.
            float reachMetres = Math.Clamp(spec.ReliefMetres * 6f, 0.03f, 0.6f);
            Field occlusion = relief ? height.Occlusion(reachMetres / spec.TileMetres) * surface.Occlusion : surface.Occlusion;

            Node albedoNode = surface.Albedo.IsConstant && surface.Alpha.IsConstant
                ? Constant(canvas, new Vector4(surface.Albedo.Value, surface.Alpha.Value))
                : Materialise(canvas, WithAlpha(canvas, surface.Albedo, surface.Alpha), ImageFormat.Colour);
            Node propertiesNode = Materialise(canvas, WithAlpha(canvas, Compose(canvas, surface.Roughness, surface.Emission, surface.Metallic), surface.Subsurface), ImageFormat.Colour);
            Node occlusionNode = MaterialiseField(canvas, occlusion);
            Node normalNode = new PassNode(canvas, g => g.Normal, new[] { height.Operand }, shader => shader.loadUniformFloat("slope", relief ? spec.ReliefRatio : 0f), ImageFormat.Vector);

            var evaluation = new Evaluation(gpu, new[] { albedoNode, propertiesNode, occlusionNode, normalNode });
            Image albedo = evaluation.Get(albedoNode)[0];
            Image properties = evaluation.Get(propertiesNode)[0];
            Image occluded = evaluation.Get(occlusionNode)[0];
            Image normal = evaluation.Get(normalNode)[0];

            int levels = arrays.Levels;
            Image albedoChain = gpu.Pool.Take(ImageFormat.Colour, size, levels);
            Image normalChain = gpu.Pool.Take(ImageFormat.Vector, size, levels);
            Image propertiesChain = gpu.Pool.Take(ImageFormat.Colour, size, levels);
            Image occlusionChain = gpu.Pool.Take(ImageFormat.Scalar, size, levels);
            Copy(albedo, albedoChain);
            Copy(normal, normalChain);
            Copy(properties, propertiesChain);
            Copy(occluded, occlusionChain);
            evaluation.Finish();

            Image[] chains = { albedoChain, normalChain, propertiesChain, occlusionChain };
            for (int level = 1; level < levels; level++)
            {
                foreach (Image chain in chains) Synthesizer.ReadOnlyLevel(chain.Texture, level - 1);
                ShaderProgram downsample = gpu.Downsample;
                downsample.bind();
                BindChains(downsample, chains);
                int levelSize = Math.Max(size >> level, 1);
                gpu.Draw(downsample, levelSize, new[] { (albedoChain.Texture, level), (normalChain.Texture, level), (propertiesChain.Texture, level), (occlusionChain.Texture, level) });
            }
            foreach (Image chain in chains) Synthesizer.ReadAllLevels(chain.Texture, levels);

            GL.Enable(EnableCap.FramebufferSrgb);
            // The chain is made from the full size down, so every level kept is exactly what it would be if the ones above it were kept too.
            for (int level = arrays.Dropped; level < levels; level++)
            {
                ShaderProgram pack = gpu.Pack;
                pack.bind();
                BindChains(pack, chains);
                pack.loadUniformInt("level", level);
                gpu.DrawLayers(pack, Math.Max(size >> level, 1), level - arrays.Dropped, layer, arrays.Albedo, arrays.Normal, arrays.Material);
            }
            GL.Disable(EnableCap.FramebufferSrgb);

            foreach (Image chain in chains) gpu.Pool.Give(chain);
            Core.Engine.CheckGLError($"After baking {spec.Name}");
        }

        private static void BindChains(ShaderProgram shader, Image[] chains)
        {
            string[] names = { "albedoIn", "normalIn", "propertiesIn", "occlusionIn" };
            for (int i = 0; i < chains.Length; i++)
            {
                Synthesizer.BindTexture(i, chains[i].Texture);
                shader.loadUniformInt(names[i], i);
            }
        }

        private static void Copy(Image from, Image to)
        {
            GL.CopyImageSubData(from.Texture, ImageTarget.Texture2D, 0, 0, 0, 0, to.Texture, ImageTarget.Texture2D, 0, 0, 0, 0, from.Size, from.Size, 1);
        }

        private static Paint WithAlpha(Canvas canvas, Paint colour, Field alpha)
        {
            if (colour.IsConstant && alpha.IsConstant) return colour;
            Operand[] operands = { colour.Operand, alpha.Operand, Operand.None };
            return new Paint(new PassNode(canvas, g => g.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.WithAlpha);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, ImageFormat.Colour));
        }

        private static Paint Compose(Canvas canvas, Field r, Field g, Field b)
        {
            if (r.IsConstant && g.IsConstant && b.IsConstant) return new Vector3(r.Value, g.Value, b.Value);
            Operand[] operands = { r.Operand, g.Operand, b.Operand };
            return new Paint(new PassNode(canvas, gpu => gpu.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.Compose);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, ImageFormat.Colour));
        }

        private static Node Constant(Canvas canvas, Vector4 value)
        {
            Operand[] operands = { new Operand(value), Operand.None, Operand.None };
            return new PassNode(canvas, g => g.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.Add);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, ImageFormat.Colour);
        }

        // A paint as an image of its own, constant or not, with its alpha (from WithAlpha) or 1.
        private static Node Materialise(Canvas canvas, Paint paint, ImageFormat format)
        {
            if (paint.Node != null) return paint.Node;
            Operand[] operands = { paint.Operand, Operand.None, Operand.None };
            return new PassNode(canvas, g => g.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.Add);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, format);
        }

        private static Node MaterialiseField(Canvas canvas, Field field)
        {
            if (field.Node != null && field.Output == 0 && field.Channel == 0 && field.Node.Outputs[0] == ImageFormat.Scalar) return field.Node;
            Operand[] operands = { field.Operand, Operand.None, Operand.None };
            return new PassNode(canvas, g => g.Op, operands, shader =>
            {
                shader.loadUniformInt("op", (int)Op.Add);
                shader.loadUniformVector4f("parameters", Vector4.Zero);
            }, ImageFormat.Scalar);
        }
    }
}
