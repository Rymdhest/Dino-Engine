using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// What a chart is drawn with: an old sheet, a sepia ink and a box of watercolours. Colours here are as shown on screen, not albedos, as the chart is a picture and
    /// is never lit.
    /// </summary>
    internal static class ChartInk
    {
        public static readonly Vector3 Paper = new Vector3(0.905f, 0.84f, 0.69f);
        public static readonly Vector3 Ink = new Vector3(0.2f, 0.14f, 0.09f);
        public static readonly Vector3 InkSoft = new Vector3(0.34f, 0.26f, 0.18f);
        /// <summary>What a shadow does to what it falls on, as a wash.</summary>
        public static readonly Vector3 Shadow = new Vector3(0.74f, 0.7f, 0.67f);
        public static readonly Vector3 SeaShallow = new Vector3(0.66f, 0.76f, 0.7f);
        public static readonly Vector3 SeaDeep = new Vector3(0.45f, 0.6f, 0.62f);
        public static readonly Vector3 SeaInk = new Vector3(0.28f, 0.38f, 0.4f);
        public static readonly Vector3 Snow = new Vector3(0.96f, 0.95f, 0.92f);
        public static readonly Vector3 SnowShade = new Vector3(0.7f, 0.75f, 0.8f);

        /// <summary>Light comes from the top left of a chart, as it always has on maps: a unit vector pointing towards it, down being +y.</summary>
        public static readonly Vector2 Light = Vector2.Normalize(new Vector2(-0.62f, -0.78f));

        public static float Luma(Vector3 c) => c.X * 0.3f + c.Y * 0.59f + c.Z * 0.11f;

        /// <summary>
        /// A colour of the world (an albedo, under a sun of about 13, so dark) as a watercolour of it: brought up to about <paramref name="lightness"/>, keeping its hue, and
        /// softened towards the paper, as paint on an old sheet is.
        /// </summary>
        public static Vector3 Tone(Vector3 albedo, float lightness)
        {
            float luma = MathF.Max(Luma(albedo), 0.005f);
            Vector3 c = albedo * (lightness / luma);
            float most = MathF.Max(c.X, MathF.Max(c.Y, c.Z));
            if (most > 0.97f) c *= 0.97f / most;
            // Less saturated than the world, and warmed by the paper under it.
            Vector3 grey = new Vector3(Luma(c));
            c = Vector3.Lerp(grey, c, 0.78f);
            return Vector3.Lerp(c, c * Paper * 1.12f, 0.5f);
        }

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
