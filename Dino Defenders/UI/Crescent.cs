using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Text;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using static Dino_Engine.GUI.Rendering.SdfPicture;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The half moon to the right of the bar, and the only panel that's always there: the talent tree as its hub, and round it, on an arc, calling the
    /// waygates, the pack and the map. Each is a disc with its key on it, and the hub says how many talents there are to spend. Only the discs stop the
    /// mouse, and only when nothing else has it, as everywhere else the mouse is what's aimed with.
    /// </summary>
    internal sealed class Crescent : GuiElement
    {
        private const float HubRadius = 32f;
        private const float Radius = 20f;
        private const float Reach = 62f;            // From the hub's middle to each disc's.
        private const float Clear = 16f;            // Between the bar and the nearest disc.
        private const float KeyHeight = 15f;
        private const float KeySize = 10.5f;

        private enum Button { None = -1, Talents, Waygates, Pack, Map }

        // Round the hub, from the bar's side over the top to the far side, as degrees up from the right.
        private static readonly (Button Button, float Angle)[] Satellites = { (Button.Waygates, 150f), (Button.Pack, 90f), (Button.Map, 30f) };

        private static readonly Colour Disc = GuiColors.Rgba(15, 12, 10, 228);
        private static readonly Colour Rim = Ui.Gold.MultiplyAlpha(0.55f);
        private static readonly Colour Thread = Ui.Gold.MultiplyAlpha(0.2f);
        private static readonly Colour Gate = GuiColors.Rgba(104, 206, 255);

        private readonly ActionBar _bar;
        private Button _hovered = Button.None;
        private bool _heldElsewhere;
        private float _time;

        public Crescent(ActionBar bar)
        {
            _bar = bar;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
        }

        private Vector2 Hub
        {
            get
            {
                GuiRect bar = _bar.Bounds;
                float x = bar.Right + Clear + Reach * MathF.Cos(MathHelper.DegreesToRadians(30f)) + Radius;
                return new Vector2(x, bar.Bottom - HubRadius);
            }
        }

        private static Vector2 Around(Vector2 hub, float degrees)
        {
            float a = MathHelper.DegreesToRadians(degrees);
            return hub + new Vector2(MathF.Cos(a), -MathF.Sin(a)) * Reach;
        }

        // A button held since before it got here belongs to the fight, or an attack would stop every time the mouse crossed a disc.
        private bool Open
        {
            get
            {
                if (GameSession.Current is not GameSession session || session.Paused || _heldElsewhere) return false;
                if (Engine.WindowHandler.IsMouseButtonDown(MouseButton.Right)) return false;
                return !Bindings.Down(GameAction.ForceAttack);
            }
        }

        private Button At(Vector2 point)
        {
            Vector2 hub = Hub;
            if ((point - hub).Length <= HubRadius) return Button.Talents;
            foreach ((Button button, float angle) in Satellites)
            {
                if ((point - Around(hub, angle)).Length <= Radius + 2f) return button;
            }
            return Button.None;
        }

        public override bool HitTestSelf(Vector2 point) => Open && At(point) != Button.None;

        protected override void OnUpdate(float deltaTime)
        {
            _time += deltaTime;
            _heldElsewhere = Engine.WindowHandler.IsMouseButtonDown(MouseButton.Left) && Context?.CapturedElement != this;
            _hovered = Context != null && Open && Context.HoveredElement == this ? At(Context.MousePosition) : Button.None;
        }

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            e.Handled = true;
            if (e.Button != MouseButton.Left || GameSession.Current is not GameSession session) return;
            GameUi ui = GameUi.Current;
            switch (At(e.Position))
            {
                case Button.Talents: ui.Toggle(ui.Talents); break;
                case Button.Pack: ui.Toggle(ui.Hero); break;
                case Button.Map: ui.Toggle(ui.Map); break;
                case Button.Waygates: session.BeginCalling(); break;
            }
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            if (Context == null || GameSession.Current is not GameSession session) return;
            Vector2 hub = Hub;
            int points = session.Hero.UnspentPoints;

            // The thread the discs hang on, which is what makes them one thing.
            DrawArc(draw, hub, Reach, 158f, 22f, Thread, 1.2f);

            foreach ((Button button, float angle) in Satellites)
            {
                Vector2 at = Around(hub, angle);
                bool usable = button != Button.Waygates || session.CanCall || session.Calling != null;
                DrawDisc(draw, at, Radius, button, usable, _hovered == button, false);
                if (button == Button.Waygates && session.Calling is float done) DrawArc(draw, at, Radius - 3f, 90f, 90f - 360f * done, Gate, 2.4f);
                DrawKey(draw, at, Radius, Bindings.Label(ActionOf(button)));
            }

            if (points > 0)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_time * 2.6f);
                float glow = HubRadius + 6f;
                draw.AddRect(new GuiRect(hub.X - glow, hub.Y - glow, glow * 2f, glow * 2f), Ui.Gold.MultiplyAlpha(0.16f + 0.14f * pulse), glow, 0f, default, 10f);
            }
            DrawDisc(draw, hub, HubRadius, Button.Talents, true, _hovered == Button.Talents, points > 0);
            DrawKey(draw, hub, HubRadius, Bindings.Label(GameAction.Talents));
            if (points > 0) DrawCount(draw, hub, points);
        }

        private static GameAction ActionOf(Button button) => button switch
        {
            Button.Waygates => GameAction.Roads,
            Button.Pack => GameAction.Hero,
            Button.Map => GameAction.Map,
            _ => GameAction.Talents,
        };

        private void DrawDisc(GuiDrawList draw, Vector2 at, float r, Button button, bool usable, bool hovered, bool owed)
        {
            float shadow = r + 3f;
            draw.AddRect(new GuiRect(at.X - shadow, at.Y - shadow + 2f, shadow * 2f, shadow * 2f), GuiColors.Rgba(0, 0, 0, 120), shadow, 0f, default, 6f);
            draw.AddCircle(at, r, Disc, hovered || owed ? 1.6f : 1.2f, hovered || owed ? Ui.Gold : Rim);
            if (button == Button.Talents) draw.AddCircle(at, r - 4.5f, default, 1f, Ui.Gold.MultiplyAlpha(owed ? 0.45f : 0.22f));

            Colour ink = !usable ? Ui.Faded.MultiplyAlpha(0.45f) : hovered ? Ui.Bright : owed ? Ui.TermInk : Ui.Parchment;
            int texture = Icons.Texture;
            float icon = r * (button == Button.Talents ? 1.0f : 1.02f);
            (Vector2 from, Vector2 to) = Icons.Of((int)button);
            draw.AddField(texture, new GuiRect(at.X - icon, at.Y - icon - r * 0.12f, icon * 2f, icon * 2f), from, to, ink);
        }

        // The key, on a small plate over the disc's lower rim.
        private void DrawKey(GuiDrawList draw, Vector2 at, float r, string key)
        {
            float width = MathF.Max(KeyHeight + 2f, GuiText.MeasureWidth(Context!, key, Ui.Face, KeySize) + 9f);
            var plate = new GuiRect(at.X - width * 0.5f, at.Y + r - KeyHeight * 0.3f, width, KeyHeight);
            draw.AddRect(plate, GuiColors.Rgba(15, 12, 10, 245), KeyHeight * 0.5f, 1f, Ui.Gold.MultiplyAlpha(0.5f));
            Ui.Text(Context!, draw, key, KeySize, new GuiRect(plate.X, plate.Y + Capitals(Context!, KeySize), plate.Width, plate.Height), Ui.Gold, Alignment.Center);
        }

        // How many talents are waiting, on a gold seal at the hub's upper right, between the two discs nearest it.
        private void DrawCount(GuiDrawList draw, Vector2 hub, int points)
        {
            float a = MathHelper.DegreesToRadians(56f);
            Vector2 at = hub + new Vector2(MathF.Cos(a), -MathF.Sin(a)) * (HubRadius - 1f);
            string count = points > 99 ? "99" : points.ToString();
            float r = count.Length > 1 ? 11f : 10f;
            draw.AddCircle(at, r + 1.5f, GuiColors.Rgba(15, 12, 10, 235));
            draw.AddCircle(at, r, Ui.Gold);
            Ui.Text(Context!, draw, count, 11.5f, new GuiRect(at.X - r, at.Y - r + Capitals(Context!, 11.5f), r * 2f, r * 2f), GuiColors.Rgba(12, 9, 6), Alignment.Center);
        }

        private static float _capitalHeight = -1f;      // An H's height as a share of the em, in the interface's face.

        /// <summary>
        /// How far down to move a line of text so that its capitals and figures are centred where it's drawn: a line is centred with its ascent and
        /// descent, and capitals have neither, so they'd sit high. Worked out from the face's own H.
        /// </summary>
        public static float Capitals(GuiContext context, float size)
        {
            FontFace face = Ui.Face;
            if (_capitalHeight < 0f)
            {
                float top = 0f;
                foreach (var contour in face.GetGlyphOutline(face.GetGlyphIndex('H')))
                    foreach (var point in contour) top = MathF.Max(top, point.Position.Y);
                _capitalHeight = top > 0f ? top / face.UnitsPerEm : 0.68f;
            }
            Font font = context.GetFont(face, size);
            float capital = _capitalHeight * font.PixelSize;
            return (font.LineHeight * 0.5f - font.Ascent + capital * 0.5f) / context.PixelScale;
        }

        // A stroke along a circle, from one angle to another (degrees up from the right), as short straight pieces.
        private static void DrawArc(GuiDrawList draw, Vector2 centre, float r, float from, float to, Colour colour, float thickness)
        {
            int pieces = Math.Max(6, (int)(MathF.Abs(to - from) / 4f));
            Vector2 last = default;
            for (int i = 0; i <= pieces; i++)
            {
                float a = MathHelper.DegreesToRadians(from + (to - from) * i / pieces);
                Vector2 p = centre + new Vector2(MathF.Cos(a), -MathF.Sin(a)) * r;
                if (i > 0) draw.AddLine(last, p, colour, thickness, roundCaps: true);
                last = p;
            }
        }

        /// <summary>The four pictures, as signed distance fields in one small texture, made the first time they're drawn.</summary>
        private static class Icons
        {
            private const int Cell = 96;
            private const int Count = 4;
            private const float Spread = 6f;
            private static int _texture;

            public static int Texture
            {
                get
                {
                    if (_texture != 0) return _texture;
                    var atlas = new byte[Cell * Count * Cell];
                    for (int i = 0; i < Count; i++) Picture((Button)i).Write(atlas, Cell * Count, i * Cell, 0, Cell, Spread, 0.62f);
                    _texture = GL.GenTexture();
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, _texture);
                    GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, Cell * Count, Cell, 0, PixelFormat.Red, PixelType.UnsignedByte, atlas);
                    GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
                    GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    return _texture;
                }
            }

            public static (Vector2 from, Vector2 to) Of(int i) => (new Vector2(i / (float)Count, 0f), new Vector2((i + 1) / (float)Count, 1f));

            public static SdfPicture Picture(Button button)
            {
                var g = new SdfPicture();
                switch (button)
                {
                    case Button.Talents:
                        // The tree as it's drawn in its own panel: a root, a way up, and the notables it forks into, under a star.
                        g.Add(Line(0f, 0.7f, 0f, -0.05f, 0.1f)).Add(Line(0f, -0.05f, -0.6f, -0.42f, 0.1f)).Add(Line(0f, -0.05f, 0.6f, -0.42f, 0.1f)).Add(Line(0f, -0.05f, 0f, -0.46f, 0.1f));
                        g.Add(Circle(0f, 0.7f, 0.17f)).Add(Circle(0f, -0.05f, 0.15f)).Add(Circle(-0.6f, -0.42f, 0.15f)).Add(Circle(0.6f, -0.42f, 0.15f));
                        g.Add(Ring(0f, -0.66f, 0.2f, 0.1f)).Add(Circle(0f, -0.66f, 0.08f));
                        break;
                    case Button.Waygates:
                        // A gate's arch, standing on its sill, with its way lit in it.
                        g.Add(Box(0f, 0.22f, 0.62f, 0.58f)).Add(Circle(0f, -0.32f, 0.62f));
                        g.Cut(Box(0f, 0.3f, 0.4f, 0.6f)).Cut(Circle(0f, -0.3f, 0.4f));
                        g.Add(Box(0f, 0.86f, 0.84f, 0.09f, 0.03f));
                        g.Add(Star(0f, 0.08f, 4, 0.28f, 0.08f));
                        break;
                    case Button.Pack:
                        // A satchel: its strap, its body, a flap and a buckle.
                        g.Add(Arc(0f, -0.38f, 0.36f, -80f, 80f, 0.12f));
                        g.Add(Box(0f, 0.22f, 0.76f, 0.62f, 0.2f));
                        g.Cut(Line(-0.76f, -0.08f, 0.76f, -0.08f, 0.08f));
                        g.Cut(Box(0f, 0.08f, 0.16f, 0.18f, 0.05f)).Add(Box(0f, 0.08f, 0.09f, 0.11f, 0.03f));
                        break;
                    default:
                        // A map, folded in three, with the way across it.
                        g.Add(Polygon(-0.88f, -0.58f, -0.3f, -0.8f, 0.3f, -0.58f, 0.88f, -0.8f, 0.88f, 0.6f, 0.3f, 0.82f, -0.3f, 0.6f, -0.88f, 0.82f));
                        g.Cut(Line(-0.3f, -0.8f, -0.3f, 0.6f, 0.07f)).Cut(Line(0.3f, -0.58f, 0.3f, 0.82f, 0.07f));
                        g.Cut(Path(0.07f, -0.62f, 0.45f, -0.4f, 0.05f, 0f, 0.2f, 0.4f, -0.2f, 0.58f, -0.45f));
                        break;
                }
                return g;
            }
        }
    }
}
