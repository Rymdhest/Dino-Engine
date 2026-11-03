using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>
    /// How light travels through a body of water. Both coefficients are per metre and per colour channel (r, g, b):
    /// light that crosses x metres of water keeps exp(-(Absorption + Scattering) * x) of its strength.
    /// <para>Absorption tints whatever lies under the water. Pure water absorbs red far more than blue, which is why
    /// clear water over sand turns turquoise and then blue with depth.</para>
    /// <para>Scattering is light bounced back out of the water by particles. It gives deep water its own colour:
    /// Scattering / (Absorption + Scattering) is that colour's albedo, so more scattering means brighter, milkier,
    /// murkier water.</para>
    /// </summary>
    public struct WaterOptics
    {
        public Vector3 Absorption;
        public Vector3 Scattering;

        public WaterOptics(Vector3 absorption, Vector3 scattering)
        {
            Absorption = absorption;
            Scattering = scattering;
        }

        /// <summary>Clear coastal sea: turquoise over sand, blue where it is deep (close to pure water, Pope and Fry 1997).</summary>
        public static WaterOptics Ocean => new WaterOptics(new Vector3(0.36f, 0.068f, 0.04f), new Vector3(0.006f, 0.013f, 0.022f));

        /// <summary>A clear lake: dissolved plant matter absorbs blue, so it reads green.</summary>
        public static WaterOptics Lake => new WaterOptics(new Vector3(0.45f, 0.12f, 0.2f), new Vector3(0.01f, 0.016f, 0.012f));

        /// <summary>A still, murky green pond: the bottom disappears within a metre.</summary>
        public static WaterOptics Pond => new WaterOptics(new Vector3(0.8f, 0.35f, 0.55f), new Vector3(0.08f, 0.12f, 0.06f));

        /// <summary>A muddy puddle: brown, nearly opaque once it is more than a few centimetres deep.</summary>
        public static WaterOptics Puddle => new WaterOptics(new Vector3(1.5f, 1.8f, 2.4f), new Vector3(0.6f, 0.45f, 0.3f));

        /// <summary>The colour deep water takes on (single-scattering albedo per channel, 0..1).</summary>
        public Vector3 DeepAlbedo => new Vector3(
            Scattering.X / MathF.Max(Absorption.X + Scattering.X, 1e-5f),
            Scattering.Y / MathF.Max(Absorption.Y + Scattering.Y, 1e-5f),
            Scattering.Z / MathF.Max(Absorption.Z + Scattering.Z, 1e-5f));
    }
}
