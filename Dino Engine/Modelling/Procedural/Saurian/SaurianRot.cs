using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// What the Reich's failures look like: animals that hatched dying, or were raised before the blood took, kept on their feet by the Ossuary's iron and bile. Their hide has gone grey and green and
    /// split open where it's rotted through, and there's a sick light in their eyes.
    /// </summary>
    public static class SaurianRot
    {
        private static readonly Vector3 Glow = new Vector3(0.55f, 0.85f, 0.12f);

        /// <summary>A living colour as it looks once it's started to go: drained, greyed and gone green, with bruising in patches.</summary>
        public static Vector3 Sicken(Vector3 colour, Vector3 p, uint seed, float size)
        {
            float grey = (colour.X + colour.Y + colour.Z) / 3f;
            Vector3 drained = Vector3.Lerp(colour, new Vector3(grey), 0.65f);
            Vector3 rotten = drained * new Vector3(0.85f, 1.02f, 0.8f) + new Vector3(0.012f, 0.02f, 0.008f);
            float bruise = FloraNoise.Value(p / (size * 0.04f), seed + 71u);
            return Vector3.Lerp(rotten, new Vector3(0.09f, 0.05f, 0.1f), MathF.Max(bruise - 0.62f, 0f) * 1.6f);
        }

        public sealed class Dress : SaurianDress
        {
            private readonly SaurianPaint _paint;

            public Dress(SaurianBody body, SaurianPaint paint) : base(body, paint) => _paint = paint;

            public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                Vector3 living = base.Colour(p, normal, part, out material, out glow);
                return Sicken(living, p, _paint.Coat.Seed, Body.Length);
            }
        }

        /// <summary>Where it's rotted through. Nothing yet.</summary>
        public static void Wound(SaurianField field, SaurianBody body)
        {
        }

        public static void Eyes(SaurianMesh mesh, SaurianBody body) => SaurianParts.Eyes(mesh, body, Glow, 2.4f, GearMaterials.Glow);

        /// <summary>Bone showing where it's rotted through. Nothing yet.</summary>
        public static void Bare(SaurianMesh mesh, SaurianBody body, SaurianDress dress)
        {
        }
    }
}
