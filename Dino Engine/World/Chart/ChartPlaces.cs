using OpenTK.Mathematics;
using static Dino_Engine.World.Chart.ChartInk;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// What marks a place with a story of its own on the chart (PLACES.md), drawn as a chart-maker draws such things: a wreck's hull lying on the strand, its planking inked along it; a pit or a
    /// quarry's floor washed dark and hatched in from its inked edge, as a cutting is on an old sheet; a nest a ring of twigs with its clutch in it; and a weaver's web a few threads strung
    /// between the trunks with the spiral across them. A place's buildings, walls and cages are drawn as any building is, and its fences as any fence (WorldChart).
    /// </summary>
    internal sealed class PlaceGlyph : ChartGlyph
    {
        private static readonly Vector3 Wood = new Vector3(0.46f, 0.34f, 0.22f);
        private static readonly Vector3 Pit = new Vector3(0.62f, 0.56f, 0.46f);
        private static readonly Vector3 Twigs = new Vector3(0.44f, 0.34f, 0.22f);
        private static readonly Vector3 Eggs = new Vector3(0.9f, 0.86f, 0.74f);
        private static readonly Vector3 Silk = new Vector3(0.92f, 0.92f, 0.88f);

        private readonly PlaceMarkKind _kind;
        private readonly Vector2 _centre, _ax, _az;
        private readonly float _ink;

        /// <param name="ax">The mark's own x, in pixels, as long as its half extent along it.</param>
        /// <param name="az">Its own z the same.</param>
        public PlaceGlyph(PlaceMarkKind kind, Vector2 centre, Vector2 ax, Vector2 az, float ink)
        {
            _kind = kind;
            _centre = centre;
            _ax = ax;
            _az = az;
            _ink = ink;
            Vector2 reach = new Vector2(MathF.Abs(ax.X) + MathF.Abs(az.X), MathF.Abs(ax.Y) + MathF.Abs(az.Y)) + new Vector2(ink * 3f + 2f);
            Min = centre - reach;
            Max = centre + reach;
            // A pit or a nest lies on the ground, under anything standing in it.
            Order = kind is PlaceMarkKind.Pit or PlaceMarkKind.Nest ? Min.Y : centre.Y + MathF.Abs(az.Y);
        }

        private Vector2 At(float u, float v) => _centre + _ax * u + _az * v;

        public override void Draw(in Pen pen)
        {
            switch (_kind)
            {
                case PlaceMarkKind.Hull: DrawHull(pen); break;
                case PlaceMarkKind.Pit: DrawPit(pen); break;
                case PlaceMarkKind.Nest: DrawNest(pen); break;
                case PlaceMarkKind.Web: DrawWeb(pen); break;
                case PlaceMarkKind.Tower: pen.Ring(_centre, MathF.Max(_ax.Length, 1.5f), _ink, Ink, 0.9f); break;
            }
        }

        // A hull along its own z: pointed at its bow, square at its stern, planked along its length.
        private void DrawHull(in Pen pen)
        {
            var outline = new[]
            {
                At(0f, 1f), At(0.7f, 0.6f), At(1f, 0f), At(0.9f, -0.85f), At(-0.9f, -0.85f), At(-1f, 0f), At(-0.7f, 0.6f),
            };
            pen.Polygon(outline, Wood, 1f, _ink, Ink, 0.9f);
            for (float u = -0.5f; u <= 0.51f; u += 0.5f) pen.Stroke(At(u, -0.8f), At(u * 0.6f, 0.75f), _ink * 0.5f, InkSoft, 0.7f);
        }

        // Washed dark and hatched in from its inked edge, all round.
        private void DrawPit(in Pen pen)
        {
            var box = new[] { At(-1f, -1f), At(1f, -1f), At(1f, 1f), At(-1f, 1f) };
            pen.Polygon(box, Pit, 0.55f, _ink, Ink, 0.85f);
            float along = MathF.Max(_ax.Length, _az.Length);
            int ticks = Math.Clamp((int)(along / 2.5f), 4, 40);
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = box[side], b = box[(side + 1) % 4];
                Vector2 inward = (_centre - (a + b) * 0.5f);
                float reach = MathF.Min(inward.Length * 0.35f, 6f);
                inward = inward.LengthSquared > 1e-4f ? inward.Normalized() * reach : Vector2.Zero;
                for (int k = 1; k < ticks; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)ticks);
                    pen.Taper(p, p + inward * (k % 2 == 0 ? 1f : 0.6f), _ink * 0.7f, 0.1f, Ink, 0.75f);
                }
            }
        }

        // A ring of twigs, and its clutch in the middle.
        private void DrawNest(in Pen pen)
        {
            float radius = MathF.Max(_ax.Length, 2f);
            pen.Ring(_centre, radius, MathF.Max(radius * 0.3f, _ink * 1.4f), Twigs, 0.95f);
            pen.Ring(_centre, radius + radius * 0.15f, _ink * 0.6f, Ink, 0.8f);
            for (int k = 0; k < 6; k++)
            {
                float a = k * MathF.Tau / 6f;
                pen.Stroke(_centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * 0.85f, _centre + new Vector2(MathF.Cos(a + 0.6f), MathF.Sin(a + 0.6f)) * radius * 1.15f, _ink * 0.5f, Ink, 0.7f);
            }
            pen.Disc(_centre, MathF.Max(radius * 0.38f, 1f), Eggs, 1f);
        }

        // Threads strung along its own x, a few out from its middle, and the spiral across them.
        private void DrawWeb(in Pen pen)
        {
            Vector2 a = At(-1f, 0f), b = At(1f, 0f);
            pen.Stroke(a, b, _ink * 0.6f, Silk, 0.95f);
            Vector2 middle = (a + b) * 0.5f;
            float span = (b - a).Length * 0.5f;
            for (int k = 0; k < 6; k++)
            {
                float t = k / 6f * MathF.Tau;
                pen.Stroke(middle, middle + new Vector2(MathF.Cos(t), MathF.Sin(t) * 0.45f) * span * 0.6f, _ink * 0.4f, Silk, 0.9f);
            }
            pen.Ring(middle, span * 0.3f, _ink * 0.35f, Silk, 0.8f);
            pen.Ring(middle, span * 0.5f, _ink * 0.35f, Silk, 0.7f);
        }
    }
}
