using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Rendering.Renderers.Lighting
{
    /// <summary>
    /// What GGX reflects of light from all round, by how rough a surface is and how squarely it's seen (Shaders/Lighting/lightingCalc.glsl's specularScaleBias): the split
    /// sum's scale and bias (Karis, "Real Shading in Unreal Engine 4", 2013), x what a surface's reflectance head-on is multiplied by and y what's added to it, so their sum is
    /// all a perfect mirror's microfacets send back once, and what's missing from 1 is what single scattering loses and <c>specularMultipleScattering</c> puts back
    /// (Kulla and Conty 2017, as Fdez-Agüera 2019 and Filament have it). Worked out exactly for the same lobe the light passes draw, GGX with the height-correlated Smith
    /// masking of Heitz 2014, as the analytic fit that stood in for it was made for UE4's cheaper masking and came out a fifth dark on half-rough metal and half again too
    /// bright on the roughest. Grown when the renderer starts, a few milliseconds, like everything else, and bound to one unit of its own that nothing else uses, which every
    /// shader reads by its layout binding, so no pass has anything to set.
    /// </summary>
    public static class SpecularLookup
    {
        /// <summary>The texture unit, which Shaders/Lighting/lightingCalc.glsl names in its layout. Nothing else may bind it.</summary>
        public const int Unit = 31;

        /// <summary>Texels each way: how squarely it's seen across (the cosine, 0 to 1), how rough down (perceptual roughness, 0 to 1), each value at a texel's middle.</summary>
        public const int Size = 32;

        /// <summary>Directions a texel's integral takes, on a Hammersley set, so it's the same every run.</summary>
        public const int Samples = 512;

        /// <summary>The roughest a lobe is drawn narrower than, as lightingCalc.glsl's MIN_ROUGHNESS.</summary>
        public const float MinRoughness = 0.045f;

        private static int _texture;

        /// <summary>Binds the table to its unit, growing it the first time.</summary>
        public static void Bind()
        {
            if (_texture == 0) _texture = Upload(Table());
            GL.ActiveTexture(TextureUnit.Texture0 + Unit);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        /// <summary>The whole table, row by row from smoothest to roughest, two floats a texel.</summary>
        public static float[] Table()
        {
            var table = new float[Size * Size * 2];
            for (int y = 0; y < Size; y++)
            {
                float roughness = (y + 0.5f) / Size;
                for (int x = 0; x < Size; x++)
                {
                    (double scale, double bias) = Integrate(roughness, (x + 0.5f) / Size, Samples);
                    table[(y * Size + x) * 2] = (float)scale;
                    table[(y * Size + x) * 2 + 1] = (float)bias;
                }
            }
            return table;
        }

        /// <summary>
        /// The split sum's scale and bias at one roughness and cosine to the view: the mean over the lobe, importance sampled by its normals, of what each direction it sends light
        /// back along gives (G V.H / (N.H N.V)), times one less Schlick's (1 - V.H)^5 for the scale and times it for the bias.
        /// </summary>
        public static (double Scale, double Bias) Integrate(float roughness, float cosView, int samples)
        {
            double lobe = System.Math.Max(roughness, MinRoughness);
            double alpha2 = lobe * lobe * lobe * lobe;
            double nv = System.Math.Clamp(cosView, 1e-4, 1.0);
            double vx = System.Math.Sqrt(1.0 - nv * nv);
            double lambdaV = System.Math.Sqrt(alpha2 + (1.0 - alpha2) * nv * nv);
            double scale = 0.0, bias = 0.0;
            for (int i = 0; i < samples; i++)
            {
                double u1 = (i + 0.5) / samples;
                double u2 = RadicalInverse((uint)i);
                double phi = 2.0 * System.Math.PI * u2;
                double cosTheta = System.Math.Sqrt((1.0 - u1) / (1.0 + (alpha2 - 1.0) * u1));
                double sinTheta = System.Math.Sqrt(1.0 - cosTheta * cosTheta);
                double hx = sinTheta * System.Math.Cos(phi);
                double hz = cosTheta;
                double vh = vx * hx + nv * hz;
                double nl = 2.0 * vh * hz - nv;
                if (nl <= 0.0 || vh <= 0.0) continue;
                double lambdaL = System.Math.Sqrt(alpha2 + (1.0 - alpha2) * nl * nl);
                double masking = 2.0 * nl * nv / (nv * lambdaL + nl * lambdaV);
                double weight = masking * vh / (hz * nv);
                double fresnel = System.Math.Pow(1.0 - vh, 5.0);
                scale += weight * (1.0 - fresnel);
                bias += weight * fresnel;
            }
            return (scale / samples, bias / samples);
        }

        private static double RadicalInverse(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
            return bits * 2.3283064365386963e-10;
        }

        private static int Upload(float[] table)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rg16f, Size, Size, 0, PixelFormat.Rg, PixelType.Float, table);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }
    }
}
