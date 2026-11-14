using Dino_Defenders.Gameplay;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using Dino_Engine.World.Chart;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The land round the hero in the top right of the screen: a round window on the map's own chart and fog (read CHART.md), turned as the land is seen, so what's up on it is
    /// what's ahead on the screen. On it are the same things as on the map and nothing more: the hero in the middle, the lit waygates, and what the land asks, which stays on the
    /// rim, pointing the way, when it's further off than the window shows.
    /// </summary>
    internal static class Minimap
    {
        public const float Size = 210f;
        /// <summary>Metres from the hero to the rim.</summary>
        private const float Reach = 105f;
        private static readonly Colour Rim = GuiColors.Rgba(150, 118, 66);
        private static readonly Colour GoalColour = GuiColors.Rgba(255, 190, 70);
        private static readonly Colour GateColour = GuiColors.Rgba(104, 206, 255);

        /// <summary>Draws it with its top right corner at <paramref name="topRight"/>, and says how far down the screen it reaches.</summary>
        public static float Draw(GuiContext context, GuiDrawList draw, GameSession session, Vector2 topRight)
        {
            MapPanel map = GameUi.Current.Map;
            Chart? chart = map.Chart;
            if (chart == null || map.ChartTexture == 0) return topRight.Y;
            map.TendFog(session.Explored);

            float radius = Size * 0.5f;
            Vector2 centre = new Vector2(topRight.X - radius, topRight.Y + radius);
            GuiRect window = new GuiRect(centre.X - radius, centre.Y - radius, Size, Size);
            Vector2 hero = chart.Frame.ToShare(session.PlayerPosition);
            float half = MathF.Min(Reach / chart.Frame.Span, 0.5f);
            Vector2 uv0 = hero - new Vector2(half), uv1 = hero + new Vector2(half);

            // A shadow under it, the chart and its fog cut round, and a rim of brass over the edge.
            draw.AddCircle(centre + new Vector2(0f, 3f), radius + 5f, GuiColors.Rgba(0, 0, 0, 110));
            draw.AddImage(map.ChartTexture, window, uv0, uv1, GuiColors.White, cornerRadius: radius);
            draw.AddImage(map.FogTexture, window, uv0, uv1, GuiColors.White, cornerRadius: radius);
            draw.AddCircle(centre, radius + 1.5f, GuiColors.Transparent, 4f, Ui.Ink);
            draw.AddCircle(centre, radius + 0.5f, GuiColors.Transparent, 2f, Rim);
            Vector2 north = centre - new Vector2(0f, radius + 1f);
            draw.AddCircle(north, 9f, Ui.Ink, 1.5f, Rim);
            GuiText.Draw(context, draw, "N", Ui.Face, 12f, new GuiRect(north.X - 9f, north.Y - 9f, 18f, 18f), Ui.Parchment, Alignment.Center, Alignment.Center);

            Vector2 Place(Vector2 world) => centre + (chart.Frame.ToShare(world) - hero) / (2f * half) * Size;

            foreach (SiteState state in session.Sites)
            {
                if (!WorldProgress.CanTravelTo(state) || session.IsGoal(state)) continue;
                Vector2 at = Place(state.Site.Position);
                if ((at - centre).Length > radius - 6f) continue;
                draw.AddCircle(at, 5.5f, GuiColors.Rgba(0, 0, 0, 170));
                draw.AddCircle(at, 3.5f, GuiColors.Transparent, 2f, GateColour);
            }

            // What the land asks: in the window where it is, and otherwise on the rim, the way to go.
            bool tyrants = !session.PlacesAsked;
            float pulse = 0.5f + 0.5f * MathF.Sin(Engine.Time * 2.2f);
            foreach (SiteState state in session.Sites)
            {
                if (!session.IsGoal(state)) continue;
                Vector2 at = Place(state.Site.Position);
                Vector2 off = at - centre;
                bool far = off.Length > radius - 8f;
                if (far) at = centre + off.Normalized() * (radius - 8f);
                bool guided = session.Guidance is Vector2 goal && (goal - state.Site.Position).LengthSquared < 1f;
                float size = far ? 4f : tyrants ? 6f : 5f;
                if (guided) draw.AddCircle(at, size + 3f + pulse * 4f, GuiColors.Transparent, 1.5f, GoalColour.MultiplyAlpha(1f - pulse * 0.8f));
                draw.AddCircle(at, size + 2f, GuiColors.Rgba(0, 0, 0, 200));
                draw.AddCircle(at, size, tyrants ? Ui.Blood : GoalColour, 1.5f, tyrants ? GoalColour : GuiColors.Rgba(60, 36, 10));
            }

            DrawHero(draw, centre, session.Sim.Yaw);
            return window.Bottom + 6f;
        }

        // A white arrowhead the way the hero faces: +x is to the left on the chart and ahead is up.
        private static void DrawHero(GuiDrawList draw, Vector2 at, float yaw)
        {
            const float Arm = 8f;
            Vector2 facing = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
            Vector2 side = new Vector2(-facing.Y, facing.X);
            draw.AddCircle(at, Arm + 2f, GuiColors.Rgba(0, 0, 0, 170));
            Vector2 tip = at + facing * Arm;
            Vector2 tail = at - facing * Arm * 0.6f;
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f * 2f - 1f;
                Vector2 corner = tail + facing * Arm * 0.3f * (1f - MathF.Abs(t)) + side * t * Arm * 0.65f;
                draw.AddLine(tip, corner, GuiColors.White, 2f, roundCaps: true);
            }
        }
    }
}
