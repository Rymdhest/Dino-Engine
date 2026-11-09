using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Rendering.Renderers.Studio;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The talent tree, as a chart of the stars to wander. Dragging moves it and the wheel zooms. Resting on a node shows what it does and lights the
    /// cheapest way there from what's already taken; a click takes the whole of that way, and a right click gives a node back if nothing depends on it.
    /// It's far too big to read at once, so typing in the box lights whatever mentions what's typed, wherever it is. Behind it is a night sky
    /// (<see cref="StarSky"/>) whose layers drift past one another as the tree is dragged. Everything on the tree is drawn before any of its words, so the
    /// pictures (<see cref="TalentAtlas"/>) and the shapes go to the GPU as one batch.
    /// </summary>
    internal sealed class TalentPanel : GuiElement
    {
        private const float Nearest = 2.4f;
        private const float Farthest = 0.075f;
        private const float WheelStep = 1.14f;
        private const float StartZoom = 0.5f;
        private const float PickSlack = 6f;
        private const float SmallestDot = 1.4f;
        private const float IconsFrom = 4f;             // Pixels of half a picture below which a node is only its frame.
        private const float NotableLabelsFrom = 0.72f;
        private const float KeystoneLabelsFrom = 0.3f;
        private const float RegionNamesUntil = 0.22f;

        private const string ForgetText = "Forget all";
        private const double ForgetWindow = 4.0;    // Seconds to press again.
        private const double ForgetPause = 0.4;     // Sooner than this is the same press, bounced or doubled.

        private static readonly Vector3 Night = new Vector3(0.035f, 0.04f, 0.075f);
        private static readonly Vector3 RootHue = new Vector3(1f, 0.84f, 0.52f);
        private static readonly Vector3 GateHue = new Vector3(0.52f, 0.76f, 1f);
        private static readonly Colour Clear = GuiColors.Rgba(0, 0, 0, 0);
        private static readonly Colour Taken = GuiColors.Rgba(240, 196, 104);
        private static readonly Colour Planned = GuiColors.Rgba(252, 236, 182);
        private static readonly Colour Open = GuiColors.Rgba(196, 188, 164);
        private static readonly Colour Locked = GuiColors.Rgba(66, 72, 98);
        private static readonly Colour RoadRing = GuiColors.Rgba(160, 136, 88);
        private static readonly Colour Hovered = GuiColors.Rgba(255, 250, 230);
        private static readonly Colour Found = GuiColors.Rgba(255, 226, 120);

        private static readonly float Edge = TalentTree.Nodes.Max(n => n.Position.Length);
        private static readonly Vector3 Unlit = new Vector3(0.33f, 0.36f, 0.48f);

        // Each node's colour, from its region, which is what a link between two regions blends between.
        private static readonly Vector3[] Hues = TalentTree.Nodes.Select(n => n.Kind == TalentKind.Root || n.Region < 0 ? RootHue : TalentContent.Regions[n.Region].Colour).ToArray();

        private readonly TextBox _search;
        private readonly Button _forget;
        private readonly Label _found;
        private double _forgetUntil;    // While this is ahead of the clock, a press means it.
        private readonly bool[] _taken = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _open = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _lit = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _onPath = new bool[TalentTree.Nodes.Length];
        private readonly string[] _text = new string[TalentTree.Nodes.Length];
        private List<int> _path = new();
        private Vector2 _focus;
        private float _zoom = StartZoom;
        private int _hover = -1;
        private int _hoverPointsWhenPlanned = -1;
        private bool _placed;
        private StarSky? _sky;
        private float _skyClock;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public TalentPanel()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(3, 4, 8);
            HitTest = HitTestMode.Block;
            CapturesMouse = true;
            Visible = false;

            for (int i = 0; i < _text.Length; i++) _text[i] = Searchable(TalentTree.Nodes[i]);

            var top = Add(new StackPanel(Orientation.Horizontal, 16f)
            {
                Width = UIValue.Fill,
                Height = 50f,
                Padding = new Thickness(18f, 10f, 18f, 0f),
                BackgroundColor = GuiColors.Rgba(6, 7, 14, 200),
            });
            top.Add(Ui.Heading("The Tree", 22f)).Width = 130f;
            var points = top.Add(Ui.Line("", 16f, Ui.Gold).Bind(() => Hero.UnspentPoints > 0 ? $"{Hero.UnspentPoints} to spend" : "Nothing to spend"));
            points.Width = 150f;
            top.Add(Ui.Line("", 16f).Bind(() => $"{TalentTree.TitleOf(Hero)}, level {Hero.Level}")).Width = 300f;
            _search = top.Add(new TextBox("", "Search the tree") { Width = 280f, Height = 30f });
            _search.TextChanged += _ => Search();
            _found = top.Add(Ui.Line("", 14f, Ui.Faded));
            _found.Width = 100f;
            _forget = top.Add(Ui.Action(ForgetText, ForgetAll));
            _forget.Width = 120f;
        }

        private static string Searchable(TalentNode node)
        {
            var words = new List<string> { node.Name, node.Kind.ToString(), Cards.TitleOf(node) };
            foreach (Mod mod in node.Mods) words.Add(mod.Describe());
            if (node.Lore != null) words.Add(node.Lore);
            return string.Join(' ', words).ToLowerInvariant();
        }

        private void Search()
        {
            string wanted = _search.Text.Trim().ToLowerInvariant();
            int count = 0;
            for (int i = 0; i < _lit.Length; i++)
            {
                _lit[i] = wanted.Length > 1 && _text[i].Contains(wanted);
                if (_lit[i]) count++;
            }
            _found.Text = wanted.Length <= 1 ? "" : count == 1 ? "1 found" : $"{count} found";
        }

        // A whole build goes with this, and there's no way back, so one stray click mustn't be enough, and nor must a double click.
        private void ForgetAll()
        {
            double now = Dino_Engine.Core.Engine.Time;
            if (now >= _forgetUntil)
            {
                _forgetUntil = now + ForgetWindow;
                _forget.Text = "Press again";
                return;
            }
            if (now < _forgetUntil - ForgetWindow + ForgetPause) return;

            StopForgetting();
            Audio.Play(Interface.Close);
            Session.Client.ForgetTalents();
            _path.Clear();
        }

        private void StopForgetting()
        {
            _forgetUntil = 0.0;
            _forget.Text = ForgetText;
        }

        /// <summary>Looks at the tree from this far off, and at this place on it if one's given, for a scripted run's pictures.</summary>
        public void Look(float zoom, Vector2? at)
        {
            _zoom = Math.Clamp(zoom, Farthest, Nearest);
            if (at is not Vector2 focus) return;
            _focus = focus;
            _placed = true;
        }

        private Vector2 ToScreen(Vector2 tree) => Bounds.Center + (tree - _focus) * _zoom;
        private Vector2 ToTree(Vector2 screen) => (screen - Bounds.Center) / _zoom + _focus;

        private static float SizeOf(TalentNode node) => node.Kind switch
        {
            TalentKind.Keystone => 36f,
            TalentKind.Root => 30f,
            TalentKind.Gate => 26f,
            TalentKind.Notable => 22f,
            _ => node.Highway ? 9.5f : node.Travel ? 8.5f : 12.5f,
        };

        private float RadiusOf(TalentNode node) => MathF.Max(SizeOf(node) * _zoom, SmallestDot);

        protected override void OnDrag(GuiDragEventArgs e)
        {
            _focus -= e.Delta / _zoom;
            if (_focus.Length > Edge) _focus = _focus.Normalized() * Edge;
        }

        protected override bool WantsDrag => true;

        protected override void OnScroll(GuiScrollEventArgs e)
        {
            Vector2 under = ToTree(e.Position);
            _zoom = Math.Clamp(_zoom * MathF.Pow(WheelStep, e.Delta.Y), Farthest, Nearest);
            _focus += under - ToTree(e.Position);
            if (_focus.Length > Edge) _focus = _focus.Normalized() * Edge;
            e.Handled = true;
        }

        protected override void OnClick(GuiMouseEventArgs e)
        {
            if (_hover < 0) return;

            if (e.Button == MouseButton.Right)
            {
                if (!TalentTree.CanRefund(Hero, _hover)) return;
                Audio.Play(Interface.Close);
                Session.Client.Refund(_hover);
                return;
            }

            if (_path.Count == 0 || _path.Count > Hero.UnspentPoints) return;
            Audio.Play(Interface.Equip);

            // Each of these is a request, and they arrive in order, so the server sees a path being walked one step at a time.
            foreach (int node in _path) Session.Client.Allocate(node);
            _path = new List<int>();
        }

        protected override void OnUpdate(float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null || Context == null) return;
            _skyClock += deltaTime;

            // The first time it's opened it looks at the middle of what's been taken, and at the root if that's nothing yet.
            if (!_placed)
            {
                _placed = true;
                List<int> allocated = TalentTree.Allocated(Hero);
                if (allocated.Count > 1) _focus = allocated.Aggregate(Vector2.Zero, (sum, i) => sum + TalentTree.Nodes[i].Position) / allocated.Count;
            }

            // Not on the mouse leaving: the button's text changing costs it the hover for a frame, which took back every first press.
            if (_forgetUntil > 0.0 && Dino_Engine.Core.Engine.Time >= _forgetUntil) StopForgetting();

            Array.Clear(_taken);
            foreach (int index in TalentTree.Allocated(Hero)) _taken[index] = true;

            for (int i = 0; i < _open.Length; i++)
            {
                TalentNode node = TalentTree.Nodes[i];
                bool open = !_taken[i] && node.Twin >= 0 && _taken[node.Twin];
                foreach (int link in node.Links) open |= !_taken[i] && _taken[link];
                _open[i] = open;
            }

            int hover = -1;
            if (IsHovered && Context.HoveredElement == this)
            {
                float best = float.PositiveInfinity;
                Vector2 mouse = Context.MousePosition;
                foreach (TalentNode node in TalentTree.Nodes)
                {
                    float reach = RadiusOf(node) + PickSlack;
                    float distance = (ToScreen(node.Position) - mouse).Length;
                    if (distance > reach || distance >= best) continue;
                    best = distance;
                    hover = node.Index;
                }
            }

            if (hover != _hover || Hero.UnspentPoints != _hoverPointsWhenPlanned)
            {
                _hover = hover;
                _hoverPointsWhenPlanned = Hero.UnspentPoints;
                _path = hover >= 0 ? TalentTree.PathTo(Hero, hover) : new List<int>();
                Array.Clear(_onPath);
                foreach (int node in _path) _onPath[node] = true;
            }
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (GameSession.Current == null || Context == null) return;

            draw.PushClip(Bounds);
            DrawSky(draw);
            DrawLinks(draw);
            DrawNodes(draw);
            DrawRegionNames(draw);
            DrawLabels(draw);
            if (_hover >= 0) DrawTip(TalentTree.Nodes[_hover]);
            DrawHints(draw);
            draw.PopClip();
        }

        // How it works, in the corner the bar leaves free.
        private void DrawHints(GuiDrawList draw)
        {
            string[] lines =
            {
                "Drag to move, wheel to zoom",
                "Click to take the lit path, right click to give a node back",
                "The gold roads reach far for few points",
                $"{Bindings.Label(GameAction.Talents)} to close",
            };
            float y = Bounds.Bottom - 22f - 18f * lines.Length;
            foreach (string line in lines)
            {
                Ui.Text(Context!, draw, line, 13f, new GuiRect(Bounds.X + 18f, y, 520f, 18f), Fade(Ui.Faded, 0.85f));
                y += 18f;
            }
        }

        // The night behind the tree, drawn at the screen's own resolution so that every star is a crisp point.
        private void DrawSky(GuiDrawList draw)
        {
            _sky ??= new StarSky();
            float scale = Context!.PixelScale;
            var size = new Vector2i((int)MathF.Round(Bounds.Width * scale), (int)MathF.Round(Bounds.Height * scale));
            int texture = _sky.Render(size, _focus, _zoom, _skyClock);
            draw.AddImage(texture, Bounds, new Vector2(0f, 1f), new Vector2(1f, 0f), GuiColors.Rgba(255, 255, 255), GuiTextureMode.Opaque);
        }

        private static Colour Rgb(Vector3 colour, float alpha = 1f) => GuiColors.Rgba(Math.Clamp(colour.X, 0f, 1f), Math.Clamp(colour.Y, 0f, 1f), Math.Clamp(colour.Z, 0f, 1f), alpha);

        private static Colour Fade(Colour colour, float alpha)
        {
            colour.Alpha *= alpha;
            return colour;
        }

        private void DrawLinks(GuiDrawList draw)
        {
            GuiRect view = Bounds;
            TalentNode[] nodes = TalentTree.Nodes;
            float thin = Math.Clamp(2.2f * _zoom, 0.9f, 3.2f);
            foreach (TalentNode node in nodes)
            {
                Vector2 from = ToScreen(node.Position);
                foreach (int link in node.Links)
                {
                    if (link < node.Index) continue;
                    TalentNode other = nodes[link];
                    Vector2 to = ToScreen(other.Position);
                    if (!Crosses(view, from, to)) continue;

                    bool both = _taken[node.Index] && _taken[link];
                    bool planned = !both && (_onPath[node.Index] || _taken[node.Index]) && (_onPath[link] || _taken[link]);
                    bool either = _taken[node.Index] || _taken[link];
                    bool road = node.Highway && other.Highway;
                    Vector3 hue = (Hues[node.Index] + Hues[link]) * 0.5f;
                    Colour colour = both ? Taken
                        : planned ? Planned
                        : road ? GuiColors.Rgba(196, 166, 108, 170)
                        : either ? GuiColors.Rgba(150, 128, 86, 210)
                        : Rgb(Vector3.Lerp(Unlit, hue, 0.28f), 0.62f);
                    float width = both ? thin * 1.7f : planned ? thin * 1.5f : road ? thin * 1.3f : thin;

                    bool arc = node.OrbitRadius > 0f && MathF.Abs(node.OrbitRadius - other.OrbitRadius) < 0.5f && (node.Orbit - other.Orbit).LengthSquared < 0.25f;
                    if (both || road) Stroke(draw, node, from, to, arc, Fade(both ? Taken : colour, both ? 0.16f : 0.14f), width * 4.5f);
                    Stroke(draw, node, from, to, arc, colour, width);
                }

                // A lit waygate's twin is across the tree: a faint line says which way.
                if (node.Twin > node.Index && (_taken[node.Index] || _taken[node.Twin]))
                {
                    draw.AddLine(from, ToScreen(nodes[node.Twin].Position), Rgb(GateHue, 0.22f), 1f);
                }
            }
        }

        // A link, straight, or along the circle both its ends sit on.
        private void Stroke(GuiDrawList draw, TalentNode node, Vector2 from, Vector2 to, bool arc, Colour colour, float width)
        {
            if (!arc)
            {
                draw.AddLine(from, to, colour, width, roundCaps: true);
                return;
            }

            // Square ends, as round ones overlap where the pieces meet and bead a line that isn't solid.
            Vector2 centre = ToScreen(node.Orbit);
            float radius = node.OrbitRadius * _zoom;
            float start = MathF.Atan2(from.Y - centre.Y, from.X - centre.X);
            float end = MathF.Atan2(to.Y - centre.Y, to.X - centre.X);
            float sweep = MathF.IEEERemainder(end - start, MathF.Tau);
            int steps = Math.Clamp((int)(MathF.Abs(sweep) * radius / 6f), 1, 48);
            Vector2 last = from;
            for (int i = 1; i <= steps; i++)
            {
                float angle = start + sweep * i / steps;
                Vector2 next = i == steps ? to : centre + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                draw.AddLine(last, next, colour, width);
                last = next;
            }
        }

        private static bool Crosses(GuiRect view, Vector2 from, Vector2 to)
        {
            float left = MathF.Min(from.X, to.X);
            float right = MathF.Max(from.X, to.X);
            float top = MathF.Min(from.Y, to.Y);
            float bottom = MathF.Max(from.Y, to.Y);
            return right >= view.X - 8f && left <= view.Right + 8f && bottom >= view.Y - 8f && top <= view.Bottom + 8f;
        }

        private void DrawNodes(GuiDrawList draw)
        {
            GuiRect view = Bounds;
            int atlas = TalentAtlas.Texture;
            float line = Math.Clamp(1.6f * _zoom, 1f, 2.6f);
            foreach (TalentNode node in TalentTree.Nodes)
            {
                Vector2 at = ToScreen(node.Position);
                float radius = RadiusOf(node);
                float reach = radius * 2.4f + 8f;
                if (at.X < view.X - reach || at.X > view.Right + reach || at.Y < view.Y - reach || at.Y > view.Bottom + reach) continue;

                int i = node.Index;
                Vector3 hue = Hues[i];
                bool taken = _taken[i];
                bool near = _onPath[i] || _open[i];
                Colour ring = i == _hover ? Hovered : taken ? Taken : _onPath[i] ? Planned : _open[i] ? Open : node.Highway ? RoadRing : Locked;
                Vector3 fill = taken ? Vector3.Lerp(Night, hue, 0.36f) : near ? Vector3.Lerp(Night, hue, 0.16f) : Night;
                Vector3 ink = taken ? Vector3.Lerp(hue, Vector3.One, 0.55f) : near ? Vector3.Lerp(hue, Vector3.One, 0.3f) * 0.9f : Vector3.Lerp(new Vector3(0.36f, 0.4f, 0.52f), hue, 0.18f);

                if (_lit[i]) draw.AddCircle(at, radius + 7f, Fade(Found, 0.22f), 2f, Found);

                switch (node.Kind)
                {
                    case TalentKind.Keystone:
                        DrawKeystone(draw, node, at, radius, hue, fill, ink, ring, taken, atlas);
                        break;

                    case TalentKind.Notable:
                        if (taken) Glow(draw, at, radius * 1.9f, Rgb(hue, 0.16f));
                        draw.AddCircle(at, radius + MathF.Max(3.5f * _zoom, 1.5f), Clear, MathF.Max(line * 0.6f, 0.8f), Fade(ring, 0.6f));
                        draw.AddCircle(at, radius, Rgb(fill), line * 1.4f, ring);
                        Picture(draw, atlas, node.Icon.Main, at, radius * 0.86f, Rgb(ink));
                        Badge(draw, atlas, node.Icon.Badge, at, radius, fill, ring, ink);
                        break;

                    case TalentKind.Gate:
                        Glow(draw, at, radius * 1.8f, Rgb(GateHue, taken ? 0.2f : 0.08f));
                        draw.AddCircle(at, radius + MathF.Max(4f * _zoom, 1.5f), Clear, line, Rgb(GateHue, taken || near ? 0.9f : 0.45f));
                        draw.AddCircle(at, radius, Rgb(Vector3.Lerp(Night, GateHue, taken ? 0.34f : 0.12f)), line * 1.3f, taken ? Taken : i == _hover ? Hovered : Rgb(GateHue, 0.85f));
                        Picture(draw, atlas, TalentGlyph.Waygate, at, radius * 0.88f, Rgb(taken ? Vector3.Lerp(GateHue, Vector3.One, 0.5f) : GateHue * 0.85f));
                        break;

                    case TalentKind.Root:
                        Glow(draw, at, radius * 2.2f, Rgb(RootHue, 0.12f));
                        draw.AddCircle(at, radius, Rgb(Vector3.Lerp(Night, RootHue, 0.3f)), line * 1.5f, Taken);
                        Picture(draw, atlas, TalentGlyph.Root, at, radius * 0.9f, Rgb(Vector3.Lerp(RootHue, Vector3.One, 0.3f)));
                        break;

                    default:
                        draw.AddCircle(at, radius, Rgb(fill), MathF.Max(line * 0.85f, 0.8f), ring);
                        Picture(draw, atlas, node.Icon.Main, at, radius * (node.Travel ? 0.8f : 0.84f), Rgb(ink));
                        Badge(draw, atlas, node.Icon.Badge, at, radius, fill, ring, ink);
                        break;
                }
            }
        }

        // A keystone is meant to be seen from across the tree: a crown of rays, a second frame, and its emblem with a halo that lights once it's taken.
        private void DrawKeystone(GuiDrawList draw, TalentNode node, Vector2 at, float radius, Vector3 hue, Vector3 fill, Vector3 ink, Colour ring, bool taken, int atlas)
        {
            float line = Math.Clamp(1.6f * _zoom, 1f, 2.6f);
            Glow(draw, at, radius * 2.3f, Rgb(hue, taken ? 0.2f : 0.07f));
            float outer = radius + MathF.Max(4.5f * _zoom, 1.5f);
            const int rays = 16;
            for (int r = 0; r < rays; r++)
            {
                float angle = MathF.Tau * r / rays;
                var way = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
                float length = radius * (r % 2 == 0 ? 0.44f : 0.24f);
                draw.AddLine(at + way * (outer + line), at + way * (outer + line + length), Fade(ring, taken ? 0.9f : 0.5f), line, roundCaps: true);
            }
            draw.AddCircle(at, outer, Clear, line * 0.8f, Fade(ring, 0.75f));
            draw.AddCircle(at, radius, Rgb(fill), line * 1.6f, ring);

            if (atlas == 0) return;
            float half = radius * 0.94f;
            if (half < IconsFrom) return;
            (Vector2 from, Vector2 to) = TalentAtlas.Of(node.Icon.Main);
            var rect = new GuiRect(at.X - half, at.Y - half, half * 2f, half * 2f);
            draw.AddField(atlas, rect, from, to, Rgb(hue, taken ? 0.8f : 0.35f), 0.3f);
            draw.AddField(atlas, rect, from, to, Rgb(ink));
        }

        private static void Glow(GuiDrawList draw, Vector2 at, float radius, Colour colour)
        {
            draw.AddRect(new GuiRect(at.X - radius, at.Y - radius, radius * 2f, radius * 2f), colour, radius, 0f, default, radius * 0.9f);
        }

        private static void Picture(GuiDrawList draw, int atlas, TalentGlyph glyph, Vector2 at, float half, Colour ink)
        {
            if (atlas == 0 || glyph == TalentGlyph.None || half < IconsFrom) return;
            (Vector2 from, Vector2 to) = TalentAtlas.Of(glyph);
            draw.AddField(atlas, new GuiRect(at.X - half, at.Y - half, half * 2f, half * 2f), from, to, ink);
        }

        // What a node's picture is narrowed to, in a small disc on its rim.
        private static void Badge(GuiDrawList draw, int atlas, TalentGlyph glyph, Vector2 at, float radius, Vector3 fill, Colour ring, Vector3 ink)
        {
            float small = radius * 0.44f;
            if (glyph == TalentGlyph.None || small < IconsFrom + 1f) return;
            Vector2 where = at + new Vector2(radius * 0.72f, radius * 0.72f);
            draw.AddCircle(where, small, Rgb(Vector3.Lerp(fill, Vector3.Zero, 0.35f)), 1f, Fade(ring, 0.9f));
            Picture(draw, atlas, glyph, where, small * 0.86f, Rgb(ink));
        }

        // Each region's name around the rim, for when the whole tree is in view and the nodes are only points.
        private void DrawRegionNames(GuiDrawList draw)
        {
            float alpha = Math.Clamp((RegionNamesUntil - _zoom) / 0.07f, 0f, 1f);
            if (alpha <= 0f) return;

            foreach (Region region in TalentContent.Regions)
            {
                Vector2 way = TalentTree.At(1f, region.Centre);
                Vector2 at = ToScreen(way * (Edge + 120f));
                var centre = at + new Vector2(way.X * 130f, way.Y * 14f);
                Colour colour = Rgb(Vector3.Lerp(region.Colour, Vector3.One, 0.25f), 0.8f * alpha);
                Ui.Text(Context!, draw, region.Name, 19f, new GuiRect(centre.X - 140f, centre.Y - 14f, 280f, 28f), colour, Alignment.Center);
            }
        }

        private void DrawLabels(GuiDrawList draw)
        {
            GuiRect view = Bounds;
            foreach (TalentNode node in TalentTree.Nodes)
            {
                bool far = node.Kind is TalentKind.Keystone or TalentKind.Gate;
                bool named = far || node.Kind is TalentKind.Notable or TalentKind.Root;
                if (!named || _zoom < (far ? KeystoneLabelsFrom : NotableLabelsFrom)) continue;
                Vector2 at = ToScreen(node.Position);
                if (at.X < view.X - 120f || at.X > view.Right + 120f || at.Y < view.Y - 40f || at.Y > view.Bottom + 40f) continue;

                float radius = RadiusOf(node);
                float below = node.Kind == TalentKind.Keystone ? radius * 1.75f + 6f : radius + 4f;
                Colour text = _taken[node.Index] ? Ui.Parchment : node.Kind == TalentKind.Keystone ? GuiColors.Rgba(220, 206, 176) : Ui.Faded;
                Ui.Text(Context!, draw, node.Name, node.Kind == TalentKind.Keystone ? 15f : 13f, new GuiRect(at.X - 120f, at.Y + below, 240f, 18f), text, Alignment.Center);
            }
        }

        private void DrawTip(TalentNode node)
        {
            float radius = RadiusOf(node);
            Vector2 at = ToScreen(node.Position);
            bool taken = _taken[node.Index];
            int cost = _path.Count;
            GameUi.Current.Tips.Show(() =>
            {
                Card card = Cards.Talent(node, Session.Sheet);
                if (taken)
                {
                    if (node.Index != TalentTree.Root.Index) card.Text(TalentTree.CanRefund(Hero, node.Index) ? "Right click to refund" : "Other talents depend on this", LineLook.Detail);
                }
                else if (cost > 0)
                {
                    string points = cost == 1 ? "1 point" : $"{cost} points";
                    card.Text(cost <= Hero.UnspentPoints ? $"Click to take: {points}" : $"Needs {points}", cost <= Hero.UnspentPoints ? LineLook.Price : LineLook.Bad);
                }
                return card;
            }, new GuiRect(at.X - radius, at.Y - radius, radius * 2f, radius * 2f));
        }
    }
}
