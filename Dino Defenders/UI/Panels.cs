using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Chart;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The land as a map, drawn as an old chart of what's really in it (read CHART.md), turned the way the land is seen: what the hero has walked is in colour, and the rest of the
    /// land they could walk an uncoloured sketch, so it's plain where they've been. On it are what the land still asks of them, from the start, and the waygates that are lit, and
    /// nothing else: the places it names that are still held, and once those are free, where its tyrant is. Opened at a waygate, it's also the way to travel: every gate found in
    /// the land can be clicked, and so can the way home to the Haven, which asks first if the land isn't free.
    /// </summary>
    internal sealed class MapPanel : GuiElement
    {
        public const int Resolution = 2048;
        private const float PickRadius = 16f;
        private const float GateIcon = 7f;
        private const float GoalIcon = 7f;
        private const float TyrantIcon = 10f;
        private const float HeroIcon = 11f;
        private const float PulseRate = 2.2f;
        private const float FogEvery = 0.25f;   // Seconds between redrawing the fog while the map's open and the hero walks.
        private static readonly Colour GoalColour = GuiColors.Rgba(255, 190, 70);
        private static readonly Colour HeroColour = GuiColors.Rgba(255, 255, 255);
        private static readonly Colour Halo = GuiColors.Rgba(0, 0, 0, 210);
        private static readonly Colour MapInk = GuiColors.Rgba(52, 36, 22);

        private int _texture;
        private int _fogTexture;
        private Chart? _chart;
        private byte[]? _fog;
        private ChartDiscovery? _fogOf;
        private float _fogWait;
        private readonly Action _close;
        private readonly Button _home;
        private readonly LeavePrompt _prompt;

        /// <summary>Whether it was opened at a waygate, from which the land's roads can be taken.</summary>
        public bool Travelling { get; private set; }

        /// <summary>The land's chart and its two textures, which the minimap draws a piece of (<see cref="Minimap"/>).</summary>
        public Chart? Chart => _chart;
        public int ChartTexture => _texture;
        public int FogTexture => _fogTexture;

        public MapPanel(Action close)
        {
            _close = close;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(8, 6, 5, 215);
            HitTest = HitTestMode.Block;
            Visible = false;
            Clicked += OnPick;

            // Up in a corner, as the bar along the bottom of the screen is drawn over everything.
            _home = Add(Ui.Action("Go home to the Haven", GoHome));
            _home.Anchor = Anchor.TopLeft;
            _home.X = 30f;
            _home.Y = 30f;
            _home.Width = 300f;
            _home.Height = 36f;
            _home.Visible = false;

            _prompt = Add(new LeavePrompt(Leave, () => _prompt!.Visible = false));
        }

        /// <summary>Opens it to look at, from anywhere.</summary>
        public void Look()
        {
            Travelling = false;
            _home.Visible = false;
            _prompt.Visible = false;
        }

        /// <summary>Opens it at a waygate, to travel from.</summary>
        public void Travel()
        {
            Travelling = true;
            _home.Visible = true;
            _prompt.Visible = false;
            _home.Label.Bind(() => GameSession.Current is { Liberated: true } ? "Go home to the Haven" : "Leave the land, and go home");
        }

        private void GoHome()
        {
            if (GameSession.Current is not { } session) return;
            if (session.Liberated)
            {
                Leave();
                return;
            }
            Audio.Play(Interface.Open);
            _prompt.Ask();
        }

        private void Leave()
        {
            _prompt.Visible = false;
            GameSession.Current?.Client.ReturnHome();
            _close();
        }

        /// <summary>The chart of the land just entered: its colours go up at once, and its fog is drawn when the map is first looked at.</summary>
        public void SetChart(Chart chart)
        {
            _chart = chart;
            if (_texture != 0) GL.DeleteTexture(_texture);
            if (_fogTexture != 0) GL.DeleteTexture(_fogTexture);
            _texture = Upload(chart.Colour, chart.Resolution);
            _fog = (byte[])chart.Sketch.Clone();
            _fogTexture = Upload(_fog, chart.Resolution);
            _fogOf = null;
        }

        private static int Upload(byte[] pixels, int size)
        {
            int texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, size, size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            // Mipmapped, as the chart is drawn smaller than it is and its pen lines would shimmer.
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        /// <summary>Redraws the fog over whatever's been seen since it was last drawn, and sends only those rows up. Asked every frame by whatever shows it, the map or the minimap.</summary>
        public void TendFog(ChartDiscovery seen)
        {
            if (_chart == null || _fog == null) return;
            bool fresh = _fogOf != seen;
            _fogWait -= Engine.Delta;
            if (!fresh && _fogWait > 0f) return;
            _fogWait = FogEvery;
            if (fresh)
            {
                _fogOf = seen;
                seen.Touch();
            }
            if (!seen.TakeChanged(out Vector2 min, out Vector2 max)) return;

            int size = _chart.Resolution;
            Vector2 a = _chart.Frame.ToPixel(min), b = _chart.Frame.ToPixel(max);
            int x0 = Math.Max((int)MathF.Floor(MathF.Min(a.X, b.X)), 0), x1 = Math.Min((int)MathF.Ceiling(MathF.Max(a.X, b.X)), size - 1);
            int y0 = Math.Max((int)MathF.Floor(MathF.Min(a.Y, b.Y)), 0), y1 = Math.Min((int)MathF.Ceiling(MathF.Max(a.Y, b.Y)), size - 1);
            if (x0 > x1 || y0 > y1) return;
            ChartFog.Compose(_chart, seen, _fog, x0, y0, x1, y1);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _fogTexture);
            var pinned = System.Runtime.InteropServices.GCHandle.Alloc(_fog, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                IntPtr rows = pinned.AddrOfPinnedObject() + y0 * size * 4;
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y0, size, y1 - y0 + 1, PixelFormat.Rgba, PixelType.UnsignedByte, rows);
            }
            finally
            {
                pinned.Free();
            }
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        // Clear of the bar along the bottom, with room above for its name and what can be done with it.
        private GuiRect MapRect()
        {
            float side = MathF.Min(Bounds.Width * 0.8f, Bounds.Height - 190f);
            return new GuiRect((Bounds.Width - side) * 0.5f, 70f, side, side);
        }

        // The chart is drawn turned as the land is seen in play: +x to the left and what's ahead up (ChartFrame).
        private Vector2 ToMap(GuiRect map, Vector2 position)
        {
            Vector2 share = _chart != null ? _chart.Frame.ToShare(position) : new Vector2(0.5f);
            return new Vector2(map.X + share.X * map.Width, map.Y + share.Y * map.Height);
        }

        private void OnPick(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
            if (!Travelling || _prompt.Visible || session.IsHaven) return;
            GuiRect map = MapRect();
            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                if (!CanTravelTo(state)) continue;
                Vector2 at = ToMap(map, state.Site.Position);
                if ((at - e.Position).Length > PickRadius) continue;

                session.Client.TravelToSite(i);
                _close();
                return;
            }
        }

        private static bool CanTravelTo(SiteState state) => WorldProgress.CanTravelTo(state);

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            GameSession session = GameSession.Current;
            if (session == null || _texture == 0 || _chart == null || Context == null) return;
            TendFog(session.Explored);

            GuiRect map = MapRect();
            draw.AddRect(new GuiRect(map.X - 2f, map.Y + 6f, map.Width + 12f, map.Height + 4f), GuiColors.Rgba(0, 0, 0, 120), 4f, softness: 14f);
            draw.AddImage(_texture, map, Vector2.Zero, Vector2.One, GuiColors.White);
            draw.AddImage(_fogTexture, map, Vector2.Zero, Vector2.One, GuiColors.White);
            DrawNames(draw, map, session);

            Ui.Text(Context, draw, session.World.Plan.Name, 26f, new GuiRect(0f, map.Y - 62f, Bounds.Width, 32f), Ui.Parchment, Alignment.Center);

            Vector2 mouse = Context.MousePosition;
            bool placesAsked = session.PlacesAsked;
            // Besides what the land asks, only the gates that are lit, as they're how the land is travelled: the rest of what's been found was too much to read. A gate's name is there when it's pointed at.
            foreach (SiteState state in session.Sites)
            {
                if (!CanTravelTo(state) || session.IsGoal(state)) continue;
                Vector2 at = ToMap(map, state.Site.Position);
                bool hovered = (at - mouse).Length <= PickRadius;
                DrawGate(draw, at, hovered && Travelling ? GuiColors.Rgba(190, 236, 255) : GuiColors.Rgba(104, 206, 255));
                if (hovered) Ui.Text(Context, draw, state.Site.Name, 13f, new GuiRect(at.X - 110f, at.Y + 9f, 220f, 18f), Ui.Bright, Alignment.Center);
            }

            // What the land asks goes over everything else, found or not, with the nearest of it ringed, as that's where the mark on the screen points.
            float pulse = 0.5f + 0.5f * MathF.Sin(Engine.Time * PulseRate);
            foreach (SiteState state in session.Sites)
            {
                if (!session.IsGoal(state)) continue;
                Vector2 at = ToMap(map, state.Site.Position);
                bool tyrant = !placesAsked;
                float radius = tyrant ? TyrantIcon : GoalIcon;
                if (session.Guidance is Vector2 goal && (goal - state.Site.Position).LengthSquared < 1f)
                {
                    draw.AddCircle(at, radius + 5f + pulse * 6f, GuiColors.Transparent, 2f, GoalColour.MultiplyAlpha(1f - pulse * 0.8f));
                }
                if (tyrant) DrawTyrant(draw, at);
                else DrawGoal(draw, at);

                bool hovered = Travelling && CanTravelTo(state) && (at - mouse).Length <= PickRadius;
                // On a plate of its own, as the map's snow and heath are as bright as the gold.
                float wide = GuiText.MeasureWidth(Context, state.Site.Name, Ui.Face, 14f) + 10f;
                draw.AddRect(new GuiRect(at.X - wide * 0.5f, at.Y + radius + 4f, wide, 17f), GuiColors.Rgba(8, 6, 5, 190), 3f);
                Ui.Text(Context, draw, state.Site.Name, 14f, new GuiRect(at.X - 120f, at.Y + radius + 3f, 240f, 18f), hovered ? Ui.Bright : GoalColour, Alignment.Center);
            }

            DrawHero(draw, ToMap(map, session.PlayerPosition), session.Sim.Yaw);
            DrawLegend(draw, map, !placesAsked);

            string help = Travelling
                ? "Click a lit waygate to go there."
                : session.IsHaven ? $"{Bindings.Label(GameAction.Map)} to close." : $"{Bindings.Label(GameAction.Map)} to close.  A waygate lights as you come near it.  {Bindings.Label(GameAction.Roads)} calls the lit ones, from anywhere.";
            Ui.Text(Context, draw, help, 14f, new GuiRect(0f, map.Y - 28f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
        }

        /// <summary>The kinds of place whose name is written on the map in ink once the hero has been there.</summary>
        private static bool Named(SiteKind kind) => kind is SiteKind.Village or SiteKind.Hamlet or SiteKind.FishingHamlet or SiteKind.LumberCamp or SiteKind.Keep or SiteKind.Ruins
            or SiteKind.Graveyard or SiteKind.Watchtower or SiteKind.Haven or SiteKind.Shipwreck or SiteKind.Quarry or SiteKind.SnowCastle or SiteKind.Tradepost
            or SiteKind.Excavation or SiteKind.Laboratory or SiteKind.Farm or SiteKind.Nest or SiteKind.Prison;

        // In the cartographer's own hand, as part of the drawing and not a mark on it: each region's name spaced across where it has most room, and each place the hero has
        // been to under it, faint enough that what the land asks and the gates read over them.
        private void DrawNames(GuiDrawList draw, GuiRect map, GameSession session)
        {
            float scale = map.Width / 1180f;
            foreach (ChartLabel label in _chart!.Labels)
            {
                float size = Math.Clamp(11f + label.Room * 70f, 13f, 21f) * scale;
                string spaced = string.Join("   ", label.Text.ToUpperInvariant().Split(' ').Select(word => string.Join(" ", word.ToCharArray())));
                Vector2 at = new Vector2(map.X + label.Share.X * map.Width, map.Y + label.Share.Y * map.Height);
                GuiRect line = new GuiRect(at.X - 300f, at.Y - size, 600f, size * 2f);
                GuiText.Draw(Context!, draw, spaced, Ui.Face, size, new GuiRect(line.X + 1f, line.Y + 1f, line.Width, line.Height), GuiColors.Rgba(240, 228, 200, 110), Alignment.Center, Alignment.Center);
                GuiText.Draw(Context!, draw, spaced, Ui.Face, size, line, MapInk.MultiplyAlpha(0.72f), Alignment.Center, Alignment.Center);
            }
            foreach (SiteState state in session.Sites)
            {
                WorldSite site = state.Site;
                if (!Named(site.Kind) || session.IsGoal(state) || CanTravelTo(state)) continue;
                if (session.Explored.Sample(site.Position) < 0.5f) continue;
                Vector2 at = ToMap(map, site.Position);
                float size = (site.Kind is SiteKind.Village or SiteKind.Keep or SiteKind.Haven ? 16f : 13.5f) * scale;
                GuiRect line = new GuiRect(at.X - 160f, at.Y + site.Radius * 0.35f * map.Width / _chart.Frame.Span, 320f, size * 1.5f);
                GuiText.Draw(Context!, draw, site.Name, Ui.Face, size, new GuiRect(line.X + 1f, line.Y + 1f, line.Width, line.Height), GuiColors.Rgba(240, 228, 200, 150), Alignment.Center, Alignment.Center);
                GuiText.Draw(Context!, draw, site.Name, Ui.Face, size, line, MapInk.MultiplyAlpha(0.9f), Alignment.Center, Alignment.Center);
            }
        }

        // A place still to free: a bright gold disc with a dark rim, which nothing else on the map is.
        private static void DrawGoal(GuiDrawList draw, Vector2 at)
        {
            draw.AddCircle(at, GoalIcon + 2.5f, Halo);
            draw.AddCircle(at, GoalIcon, GoalColour, 2f, GuiColors.Rgba(60, 36, 10));
            draw.AddCircle(at, GoalIcon * 0.35f, GuiColors.Rgba(60, 36, 10));
        }

        // Where a tyrant is: bigger, red within gold, and crossed through, so that the end of a land reads as that.
        private static void DrawTyrant(GuiDrawList draw, Vector2 at)
        {
            draw.AddCircle(at, TyrantIcon + 3f, Halo);
            draw.AddCircle(at, TyrantIcon, Ui.Blood, 2.5f, GoalColour);
            float arm = TyrantIcon * 0.5f;
            draw.AddLine(at + new Vector2(-arm, -arm), at + new Vector2(arm, arm), Ui.Bright, 2.5f, roundCaps: true);
            draw.AddLine(at + new Vector2(arm, -arm), at + new Vector2(-arm, arm), Ui.Bright, 2.5f, roundCaps: true);
        }

        // The hero: a white arrowhead on a dark disc, pointing the way they face. +x is to the left on the map and ahead is up, as in ToMap.
        private static void DrawHero(GuiDrawList draw, Vector2 at, float yaw)
        {
            Vector2 facing = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
            Vector2 side = new Vector2(-facing.Y, facing.X);
            draw.AddCircle(at, HeroIcon + 3f, Halo, 2f, HeroColour.MultiplyAlpha(0.85f));
            Vector2 tip = at + facing * HeroIcon * 0.95f;
            Vector2 tail = at - facing * HeroIcon * 0.55f;
            // Filled by lines fanned from the tip to a notched base, as the draw list has no triangles.
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f * 2f - 1f;
                Vector2 corner = tail + facing * HeroIcon * 0.3f * (1f - MathF.Abs(t)) + side * t * HeroIcon * 0.7f;
                draw.AddLine(tip, corner, HeroColour, 2.2f, roundCaps: true);
            }
        }

        // What each mark is, in a corner of the map.
        private void DrawLegend(GuiDrawList draw, GuiRect map, bool tyrants)
        {
            const float Row = 24f;
            // Beside the map where there's room, so it covers none of it.
            float left = map.X >= 190f ? map.X - 176f : map.X + 10f;
            GuiRect box = new GuiRect(left, map.Y + 10f, 160f, Row * 3f + 10f);
            draw.AddRect(box, GuiColors.Rgba(8, 6, 5, 200), 4f, 1f, GuiColors.Rgba(120, 96, 58));
            float x = box.X + 22f, y = box.Y + 5f + Row * 0.5f;

            DrawHero(draw, new Vector2(x, y), 0f);
            Ui.Text(Context!, draw, "You", 14f, new GuiRect(x + 22f, y - 9f, 120f, 18f), HeroColour);
            y += Row;
            if (tyrants) DrawTyrant(draw, new Vector2(x, y));
            else DrawGoal(draw, new Vector2(x, y));
            Ui.Text(Context!, draw, tyrants ? "Tyrant" : "Place to free", 14f, new GuiRect(x + 22f, y - 9f, 120f, 18f), GoalColour);
            y += Row;
            DrawGate(draw, new Vector2(x, y), GuiColors.Rgba(104, 206, 255));
            Ui.Text(Context!, draw, "Waygate", 14f, new GuiRect(x + 22f, y - 9f, 120f, 18f), Ui.Parchment);
        }

        // A little arch, as the mark on a gate is, so that a gate reads as one wherever it is.
        private static void DrawGate(GuiDrawList draw, Vector2 at, Colour colour)
        {
            draw.AddCircle(at, GateIcon + 2.5f, GuiColors.Rgba(0, 0, 0, 170));
            var line = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float angle = MathF.PI * i / 10f;
                line.Add(at + new Vector2(MathF.Cos(angle) * GateIcon * 0.8f, GateIcon * 0.35f - MathF.Sin(angle) * GateIcon));
            }
            draw.AddPolyline(line, colour, 2f);
            draw.AddLine(at + new Vector2(-GateIcon * 0.8f, GateIcon * 0.35f), at + new Vector2(-GateIcon * 0.8f, GateIcon * 0.8f), colour, 2f);
            draw.AddLine(at + new Vector2(GateIcon * 0.8f, GateIcon * 0.35f), at + new Vector2(GateIcon * 0.8f, GateIcon * 0.8f), colour, 2f);
        }
    }

    /// <summary>The story of the land so far, and, from an elder, a word of thanks when it has been earned.</summary>
    internal sealed class ChroniclePanel : GuiElement
    {
        private readonly StackPanel _body;

        public ChroniclePanel()
        {
            Ui.DockLeft(this, 640f, fullHeight: false);
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
            Ui.Dress(this);
            Visible = false;
            _body = Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
        }

        public void Open(SiteState? elder)
        {
            GameSession session = GameSession.Current;
            Chronicle chronicle = session.Chronicle;

            _body.ClearChildren();
            _body.Add(Ui.Heading(session.World.Plan.Name, 26f));
            if (session.IsHaven)
            {
                _body.Add(Ui.Paragraph(chronicle.Prologue, 16f));
                _body.Add(Ui.Line(session.Hero.BestLand > 0 ? $"You've freed {session.Hero.WorldsCleared} lands in all, as deep as level {session.Hero.BestLand}. The map table opens any level up to {session.Hero.LandLevel}." : "The map table opens your first land.", 14f, Ui.Gold));
                _body.Add(new Spacer(height: 4f));
                _body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{Bindings.Label(GameAction.Chronicle)} to close"));
                return;
            }
            _body.Add(Ui.Line($"{session.World.Plan.Archetype.Name}, under {chronicle.Faction}. Level {session.World.Plan.Level}.", 14f, Ui.Faded));
            foreach ((OmenDefinition omen, float value) in session.Terms.Chosen()) _body.Add(Ui.Paragraph($"{omen.Name}. {omen.Describe(value)}", 13f, Ui.Faded));
            foreach (UniqueOrders unique in session.Terms.Uniques()) _body.Add(Ui.Paragraph($"{unique.Name}. {unique.Gives}. {unique.Costs}.", 13f, Ui.RarityColour(Rarity.Unique)));
            _body.Add(Ui.Paragraph(chronicle.Prologue, 16f));
            _body.Add(new Spacer(height: 6f));

            foreach (Objective objective in chronicle.Objectives)
            {
                Colour colour = objective.Done ? Ui.Faded : Ui.Parchment;
                _body.Add(Ui.Paragraph(objective.Summary, 15f, colour));
                _body.Add(Ui.Line($"{objective.RewardExperience} experience, {objective.RewardGold} gold", 13f, Ui.Gold.MultiplyAlpha(objective.Done ? 0.4f : 0.9f)));
            }

            if (elder != null) AddElder(session, elder);
            _body.Add(new Spacer(height: 4f));
            _body.Add(Ui.Line("", 13f, Ui.Faded).Bind(() => $"{Bindings.Label(GameAction.Chronicle)} to close"));
        }

        private void AddElder(GameSession session, SiteState elder)
        {
            _body.Add(new Spacer(height: 8f));
            _body.Add(Ui.Line($"The elder of {elder.Site.Name}", 16f, Ui.Gold));

            if (!session.IsSafe(elder))
            {
                _body.Add(Ui.Paragraph($"\"While {session.Chronicle.Beast} holds this land we keep our heads down and our doors barred. Free us, and you'll have our thanks and more than thanks.\"", 15f));
                return;
            }

            if (elder.Rewarded)
            {
                _body.Add(Ui.Paragraph("\"The children play on the road again. That was you.\"", 15f));
                return;
            }

            int gift = 45 * session.World.Plan.Level;
            session.Client.ThankElder(session.Sites.IndexOf(elder));
            _body.Add(Ui.Paragraph($"\"We didn't think anyone would come. Take this, it's what we have.\"", 15f));
            _body.Add(Ui.Line($"{gift} gold", 14f, Ui.Gold));
        }
    }

    /// <summary>Shown while a new land takes shape.</summary>
    internal sealed class LoadingScreen : GuiElement
    {
        public string Title = "";
        public string Detail = "";
        public string Stage = "";
        public float Progress;

        public LoadingScreen()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(9, 7, 6);
            HitTest = HitTestMode.Block;
            Visible = false;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            if (Context == null) return;

            float y = Bounds.Height * 0.42f;
            Ui.Text(Context, draw, Title, 42f, new GuiRect(0f, y, Bounds.Width, 52f), Ui.Parchment, Alignment.Center);
            y += 56f;
            Ui.Text(Context, draw, Detail, 17f, new GuiRect(0f, y, Bounds.Width, 24f), Ui.Faded, Alignment.Center);
            y += 44f;

            float width = MathF.Min(Bounds.Width * 0.36f, 460f);
            GuiRect bar = new GuiRect((Bounds.Width - width) * 0.5f, y, width, 3f);
            draw.AddRect(bar, GuiColors.Rgba(60, 50, 40));
            draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * Math.Clamp(Progress, 0f, 1f), bar.Height), Ui.Gold);
            Ui.Text(Context, draw, Stage, 14f, new GuiRect(0f, y + 12f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
        }
    }

    /// <summary>All of the game's interface, and which of it is showing.</summary>
    internal sealed class GameUi
    {
        public static GameUi Current = null!;

        public readonly Wardrobe Wardrobe;
        public readonly UseMarks Marks = new();
        public readonly ArchivePlaques Plaques = new();
        public readonly Hud Hud = new();
        public readonly LootLabels Labels = new();
        public readonly MapPanel Map;
        public readonly HeroPanel Hero = new();
        public readonly TalentPanel Talents = new();
        public readonly BookPanel Book;
        public readonly GlossaryPage Glossary = new();
        public readonly SettingsPage Settings = new();
        public readonly ChroniclePanel Chronicle = new();
        public readonly MapTablePanel MapTable = new();
        public readonly HallPanel HallTable;
        public readonly PauseMenu Pause;
        public readonly MultiplayerPage Multiplayer;
        public readonly LoadingScreen Loading = new();
        public readonly TipLayer Tips = new();
        public readonly GroundCatcher Ground = new();
        public readonly HandLayer Hand = new();
        public readonly ActionBar Bar;
        public readonly Crescent Crescent;
        public readonly MenuScreen Menu;

        private readonly GuiElement[] _panels;

        public GameUi(GuiContext gui, ArpgGame game, Action quit, Action heroSelect)
        {
            Current = this;
            Wardrobe = game.Wardrobe;
            Book = new BookPanel(Hero);
            Map = new MapPanel(CloseAll);
            HallTable = new HallPanel(CloseAll);
            Pause = new PauseMenu(CloseAll, () => Into(Settings), () => Into(Glossary), () => Into(Multiplayer!), heroSelect, quit);
            Multiplayer = new MultiplayerPage(game);
            Bar = new ActionBar(Hero);
            Crescent = new Crescent(Bar);
            Menu = new MenuScreen(game);
            _panels = new GuiElement[] { Map, Hero, Hero.Side, Talents, Book, Glossary, Chronicle, MapTable, HallTable, Pause, Multiplayer, Settings };

            gui.Root.Add(Marks);
            gui.Root.Add(Plaques);
            gui.Root.Add(Hud);
            gui.Root.Add(Labels);
            gui.Root.Add(Ground);
            foreach (GuiElement panel in _panels) gui.Root.Add(panel);
            gui.Root.Add(Bar);
            gui.Root.Add(Crescent);
            gui.Root.Add(Menu);
            Lift(gui);
            gui.Root.Add(Tips);
            gui.Root.Add(Hand);
            gui.Root.Add(Loading);
        }

        // Pages cover everything, the bar and the main menu among them.
        private void Lift(GuiContext gui)
        {
            foreach (GuiElement page in new GuiElement[] { Pause, Glossary, Multiplayer, Settings }) gui.Root.Add(page);
        }

        /// <summary>Whether it's the main menu that's showing, and nobody is being played.</summary>
        public bool AtMenu { get; private set; }

        /// <summary>Whether one of the screens that cover everything is open.</summary>
        public bool OnPage => Pause.Visible || Glossary.Visible || Multiplayer.Visible || Settings.Visible;

        private GuiElement? _cameFrom;

        /// <summary>Goes to a page from wherever this is, which is what Back goes to.</summary>
        public void Into(GuiElement page)
        {
            _cameFrom = Pause.Visible ? Pause : AtMenu ? Menu : null;
            CloseAll();
            Menu.Visible = false;
            Audio.Play(Interface.Open);
            page.Visible = true;
            if (page == Settings) Settings.Open();
            if (page == Multiplayer) Multiplayer.Open();
        }

        /// <summary>Back from a page, to whatever it was opened from.</summary>
        public void Back()
        {
            GuiElement? to = _cameFrom;
            _cameFrom = null;
            CloseAll();
            if (to == null) return;
            to.Visible = true;
        }

        /// <summary>Whether there's a game going on, as everything that's part of one goes when the main menu comes.</summary>
        public void Playing(bool playing)
        {
            AtMenu = !playing;
            CloseAll();
            Menu.Visible = !playing;
            Clear();
        }

        /// <summary>What's on the world goes while a page is over it, as a page is all there should be to read. It's asked every frame, as anything can open one.</summary>
        public void Clear()
        {
            bool showing = !AtMenu && !OnPage;
            Hud.Visible = showing;
            Bar.Visible = showing;
            Crescent.Visible = showing;
            Marks.Visible = showing;
            Plaques.Visible = showing;
            Labels.Visible = showing;
            Ground.Visible = showing;
        }

        public bool AnyOpen => _panels.Any(p => p.Visible);

        /// <summary>How far a hero can go from where they used something before what it opened closes, as the world goes on while it's open.</summary>
        private const float UseLeash = 6f;

        private Vector2? _usedAt;

        /// <summary>Closes whatever was opened by using something in the world once the hero has walked away from it.</summary>
        public void WalkedAway(Vector2 hero)
        {
            if (_usedAt is not Vector2 at) return;
            if (!AnyOpen)
            {
                _usedAt = null;
                return;
            }
            if ((hero - at).LengthSquared > UseLeash * UseLeash) CloseAll();
        }

        public void CloseAll()
        {
            if (AnyOpen) Audio.Play(Interface.Close);
            foreach (GuiElement panel in _panels) panel.Visible = false;
            _usedAt = null;
            Tips.Clear();
            Hero.Forget();
        }

        public void Toggle(GuiElement panel)
        {
            bool open = panel.Visible;
            CloseAll();
            if (open) return;

            Audio.Play(Interface.Open);
            panel.Visible = true;
            if (panel == Book) Hero.Visible = true;
            if (panel == Hero || panel == Book) Hero.Open(null);
            if (panel == Book) Book.OpenAny();
            _cameFrom = null;
            if (panel == Chronicle) Chronicle.Open(null);
            if (panel == Map) Map.Look();
        }

        /// <summary>Opens a book, in place of whatever else is open, or shuts it if it's the one that's open.</summary>
        public void Read(ItemPlace place)
        {
            // What's already open stays open: a right click on it again is no reason to lose the page being read.
            if (Book.IsOpen(Book.At(place))) return;
            CloseAll();

            Audio.Play(Interface.Open);
            Hero.Visible = true;
            Hero.Open(null);
            Book.Visible = true;
            Book.Show(place);
        }

        /// <summary>Opens what goes with something the hero used in the world.</summary>
        public void Open(InteractKind kind, SiteState site)
        {
            CloseAll();
            _usedAt = GameSession.Current?.PlayerPosition;
            Audio.Play(Interface.Open);
            switch (kind)
            {
                case InteractKind.Merchant:
                    Hero.Visible = true;
                    Hero.Open(site);
                    break;
                case InteractKind.Elder:
                    Chronicle.Visible = true;
                    Chronicle.Open(site);
                    break;
                case InteractKind.Waygate:
                    Map.Visible = true;
                    Map.Travel();
                    break;
                case InteractKind.MapTable:
                    MapTable.Visible = true;
                    MapTable.Open();
                    break;
                case InteractKind.Stash:
                    Hero.Visible = true;
                    Hero.OpenStash();
                    break;
                case InteractKind.BetaVendor:
                    Hero.Visible = true;
                    Hero.OpenVendor();
                    break;
                case InteractKind.Workbench:
                    Hero.Visible = true;
                    Hero.OpenBench();
                    break;
                case InteractKind.GamingTable:
                    HallTable.Visible = true;
                    HallTable.Open(kind);
                    break;
            }
        }
    }
}
