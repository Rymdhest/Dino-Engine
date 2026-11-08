using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Tooltips;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The map table in the Haven, laid out as what it lets a player do, left to right: choose a level, choose one of its three lands, and choose what orders to give for it. Each land is a card
    /// with a map of it, grown on a worker thread the first time it's shown, and the few things that tell lands apart. What the orders put on the land is the same whichever is chosen, so it's
    /// said once, beside the slots they go in. Nothing is explained in words that the layout already says. The lands are worked out from the hero alone, the same as the server does. Read LANDS.md.
    /// </summary>
    internal sealed class MapTablePanel : GuiElement
    {
        private const float PanelWidth = 1300f;
        private const float PanelHeight = 560f;
        private const float Pad = 20f;
        private const float Gap = 16f;
        private const float LevelsWidth = 140f;
        private const float SideWidth = 320f;
        private const float CardGap = 12f;
        private const float CardPad = 10f;
        private const float RowHeight = 30f;
        private const float SlotSize = 52f;
        private const float SlotGap = 8f;
        private const int SlotsAcross = 5;
        private const int PreviewResolution = 256;
        private const int MostPreviewsKept = 12;

        private static readonly Colour Line = GuiColors.Rgba(84, 68, 46);
        private static readonly Colour Chosen = GuiColors.Rgba(112, 74, 28);
        private static readonly Colour ArenaInk = GuiColors.Rgba(196, 92, 70);
        private static readonly Colour Danger = GuiColors.Rgba(214, 120, 104);

        private static float MiddleWidth => PanelWidth - Pad * 2f - LevelsWidth - SideWidth - Gap * 2f;
        private static float CardWidth => (MiddleWidth - CardGap * (Lands.Choices - 1)) / Lands.Choices;

        private readonly ScrollPanel _levels;
        private readonly Label _levelTitle;
        private readonly Button _reroll;
        private readonly StackPanel _cards;
        private readonly Label _ordersTitle;
        private readonly SlotRow _slots;
        private readonly PackRow _pack;
        private readonly ScrollPanel _effects;
        private readonly StackPanel _openRow;
        private readonly Label _openText;

        private readonly List<int> _slotted = new();
        private readonly Dictionary<string, Preview> _previews = new();
        private int _level = 1;
        private string _shownFor = "";
        private int _levelsFor = -1;
        private IReadOnlyList<LandOffer> _offers = Array.Empty<LandOffer>();
        private (OmenDefinition omen, float value, GuiRect rect)? _lookingAtOmen;
        private (Item item, GuiRect rect)? _lookingAtOrders;
        private object? _ordersShownBy;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        /// <summary>A land's map, as it's growing and once it's grown: where heroes come in and where its tyrant is, from 0 to 1 across it.</summary>
        private sealed class Preview
        {
            public Task<(byte[] Pixels, Vector2 Landing, Vector2 Tyrant)>? Growing;
            public int Texture;
            public Vector2 Landing;
            public Vector2 Tyrant;
        }

        public MapTablePanel()
        {
            Anchor = Anchor.Center;
            Width = PanelWidth;
            Height = PanelHeight;
            Padding = new Thickness(Pad);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var top = body.Add(new StackPanel(Orientation.Horizontal, 12f) { Width = UIValue.Fill, Height = 30f });
            top.Stack!.CrossAlignment = Alignment.Center;
            Label title = top.Add(Ui.Heading("The Map Table", 22f));
            title.Width = UIValue.Fill;
            _openRow = top.Add(new StackPanel(Orientation.Horizontal, 10f) { Width = 520f, Height = 30f, Visible = false });
            _openRow.Stack!.CrossAlignment = Alignment.Center;
            _openText = _openRow.Add(Ui.Line("", 14f, Ui.Good));
            _openText.Width = UIValue.Fill;
            Button close = _openRow.Add(Ui.Action("Close the way", CloseWay));
            close.Width = 140f;

            var table = body.Add(new StackPanel(Orientation.Horizontal, Gap) { Width = UIValue.Fill, Height = UIValue.Fill });

            // Left: the levels.
            var levelFrame = table.Add(new GuiElement { Width = LevelsWidth, Height = UIValue.Fill, BackgroundColor = Ui.InkLight, BorderColor = Line, BorderWidth = 1f, CornerRadius = 2f });
            _levels = levelFrame.Add(Kit.Scroll());

            // Middle: the level's three lands.
            var middle = table.Add(new StackPanel(Orientation.Vertical, 10f) { Width = MiddleWidth, Height = UIValue.Fill });
            var middleTop = middle.Add(new StackPanel(Orientation.Horizontal, 10f) { Width = MiddleWidth, Height = 32f });
            middleTop.Stack!.CrossAlignment = Alignment.Center;
            _levelTitle = middleTop.Add(Ui.Line("", 19f, Ui.Parchment));
            _levelTitle.Width = UIValue.Fill;
            _reroll = middleTop.Add(Ui.Action("New lands", Reroll));
            _reroll.Width = 230f;
            _reroll.Label.Bind(() => GameSession.Current == null ? "" : $"New lands  ·  {Lands.RerollCost(_level):N0} gold");
            _cards = middle.Add(new StackPanel(Orientation.Horizontal, CardGap) { Width = MiddleWidth, Height = UIValue.Fill });

            // Right: the orders, and what they come to.
            var side = table.Add(new StackPanel(Orientation.Vertical, 8f) { Width = SideWidth, Height = UIValue.Fill });
            _ordersTitle = side.Add(Ui.Line("", 16f, Ui.Gold));
            _slots = side.Add(new SlotRow(this));
            side.Add(Ui.Line("In your pack", 12.5f, Ui.Faded));
            _pack = side.Add(new PackRow(this));
            side.Add(new Rule());
            side.Add(Ui.Line("What the land will hold", 16f, Ui.Gold));
            var effectsFrame = side.Add(new GuiElement { Width = SideWidth, Height = UIValue.Fill });
            _effects = effectsFrame.Add(Kit.Scroll());
        }

        /// <summary>Shows the table as it stands for this hero, at the deepest level they can open, with nothing slotted.</summary>
        public void Open()
        {
            _slotted.Clear();
            _level = Hero.LandLevel;
            _shownFor = "";
            _levelsFor = -1;
            Rebuild();
            _levels.ScrollToTop();
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (GameSession.Current == null || !Visible) return;

            // Orders that have gone from where they were aren't slotted any more, as when the server has had the last word, and nor are more than there's room for.
            _slotted.RemoveAll(cell => Hero.Inventory[cell] is not { IsOrders: true });
            while (_slotted.Count > Lands.OrderSlots(Hero)) _slotted.RemoveAt(_slotted.Count - 1);
            _level = Math.Clamp(_level, 1, Hero.LandLevel);
            if (Signature() != _shownFor) Rebuild();

            var portal = Session.Client.Portal;
            _openRow.Visible = portal != null;
            if (portal is var (name, request, _)) _openText.Text = $"The way is open to {name}, level {request.Level}";
            _reroll.Enabled = Hero.Gold >= Lands.RerollCost(_level);
            TendPreviews();
        }

        // What the panel shows depends on these and nothing else: which level, which lands, which orders there are and which are slotted.
        private string Signature()
        {
            var orders = string.Join(",", OrdersCells().Select(cell => $"{cell}:{Hero.Inventory[cell]!.LookSeed()}"));
            return $"{Hero.Offers}/{Hero.LandLevel}/{_level}/{Hero.RerollsAt(_level)}/{string.Join(",", _slotted)}/{orders}";
        }

        private IEnumerable<int> OrdersCells()
        {
            for (int cell = 0; cell < HeroState.InventorySize; cell++)
            {
                if (Hero.Inventory[cell] is { IsOrders: true }) yield return cell;
            }
        }

        /// <summary>The land's terms as the slotted orders make them, before any land's own, which have nothing in them but how many places are asked.</summary>
        private LandTerms Slotted()
        {
            var terms = new LandTerms();
            foreach (int cell in _slotted) terms.Give(Hero.Inventory[cell]!);
            terms.Sanitize();
            return terms;
        }

        private void Rebuild()
        {
            _shownFor = Signature();
            int deepest = Hero.LandLevel;
            if (_levelsFor != deepest) FillLevels(deepest);

            bool arena = Lands.IsBossLevel(_level);
            _levelTitle.Text = arena ? $"Level {_level}  ·  arena" : $"Level {_level}";
            _levelTitle.Color = arena ? ArenaInk : Ui.Parchment;

            LandTerms orders = Slotted();
            _offers = Lands.Offer(Hero, _level);
            _cards.ClearChildren();
            for (int i = 0; i < _offers.Count; i++) _cards.Add(new LandCard(this, _offers[i], i, orders));

            int slots = Lands.OrderSlots(Hero);
            _ordersTitle.Text = $"Orders  {_slotted.Count}/{slots}";
            _slots.Refresh(slots);
            _pack.Refresh();
            FillEffects(orders);
        }

        // Every level that can be opened, deepest first, as that's where a climb goes on from.
        private void FillLevels(int deepest)
        {
            _levelsFor = deepest;
            _levels.Content.ClearChildren();
            var list = _levels.Content.Add(new StackPanel(Orientation.Vertical, 0f) { Width = UIValue.Fill, Height = UIValue.Auto });
            for (int level = deepest; level >= 1; level--) list.Add(new LevelRow(this, level, level == deepest));
        }

        // What the slotted orders put on the land, all of it in one list, rewards and then dangers, and what any unique orders do.
        private void FillEffects(LandTerms terms)
        {
            _effects.Content.ClearChildren();
            var list = _effects.Content.Add(new StackPanel(Orientation.Vertical, 6f) { Width = SideWidth - 12f, Height = UIValue.Auto });
            bool any = false;
            foreach ((OmenDefinition omen, float value) in terms.Chosen().Where(c => c.Omen.IsPrefix))
            {
                list.Add(new EffectRow(this, omen, value, omen.Describe(value), Ui.Good));
                any = true;
            }
            foreach ((OmenDefinition omen, float value) in terms.Chosen().Where(c => !c.Omen.IsPrefix))
            {
                list.Add(new EffectRow(this, omen, value, omen.Describe(value), Danger));
                any = true;
            }
            foreach (UniqueOrders unique in terms.Uniques())
            {
                list.Add(new EffectRow(this, null, 0f, unique.Gives, Ui.RarityColour(Rarity.Unique)));
                list.Add(new EffectRow(this, null, 0f, unique.Costs, Danger));
                any = true;
            }
            if (!any) list.Add(Ui.Line("Nothing but the land", 13.5f, Ui.Faded));
        }

        private void OpenWay(int index)
        {
            Audio.Play(Interface.Trade);
            Session.Client.OpenWay(_level, index, _slotted.ToList());
            _slotted.Clear();
            GameUi.Current.CloseAll();
        }

        private void Reroll()
        {
            if (Hero.Gold < Lands.RerollCost(_level)) return;
            Audio.Play(Interface.Trade);
            Session.Client.Reroll(_level);
        }

        private static void CloseWay()
        {
            Audio.Play(Interface.Close);
            Session.Client.CloseWay();
        }

        public void ChooseLevel(int level)
        {
            if (level == _level) return;
            Audio.Play(Interface.Click);
            _level = level;
        }

        public bool IsChosenLevel(int level) => _level == level;

        /// <summary>Puts the orders in a cell of the pack into the first empty slot, if there is one.</summary>
        public void Slot(int cell)
        {
            if (_slotted.Contains(cell) || _slotted.Count >= Lands.OrderSlots(Hero)) return;
            Audio.Play(Interface.Click);
            _slotted.Add(cell);
        }

        /// <summary>Takes whatever's in a slot back out of it.</summary>
        public void Unslot(int slot)
        {
            if (slot < 0 || slot >= _slotted.Count) return;
            Audio.Play(Interface.Click);
            _slotted.RemoveAt(slot);
        }

        public IReadOnlyList<int> SlottedCells => _slotted;

        public IEnumerable<int> UnslottedCells => OrdersCells().Where(cell => !_slotted.Contains(cell));

        public void LookAt(OmenDefinition? omen, float value, GuiRect rect) => _lookingAtOmen = omen != null ? (omen, value, rect) : null;

        /// <summary>Shows orders' card beside where they're pointed at, or stops, for whichever piece of the panel is showing it.</summary>
        public void LookAt(Item? item, GuiRect rect, object by)
        {
            if (item != null)
            {
                _lookingAtOrders = (item, rect);
                _ordersShownBy = by;
            }
            else if (_ordersShownBy == by)
            {
                _lookingAtOrders = null;
                _ordersShownBy = null;
            }
        }

        protected override void OnDrawOverlay(GuiDrawList draw)
        {
            base.OnDrawOverlay(draw);
            if (_lookingAtOmen is var (omen, value, rect)) GameUi.Current.Tips.Show(() => Cards.Omen(omen, value, GameSession.Current?.Sheet), rect);
            if (_lookingAtOrders is var (item, from)) Ui.DrawTooltip(Hero, item, from, null, compare: false);
        }

        // ---- Previews: each land grown on a worker thread as it will be with what's slotted, drawn as its map, and kept a while in case it's looked at again.

        private static string KeyOf(in WorldRequest request) => $"{request.Seed}/{request.ArchetypeId}/{request.Level}/{request.Larger:0.000}";

        /// <summary>The request a land's map is grown from: the land as asked, as large as the slotted orders make it. Who stands on it doesn't change the ground.</summary>
        private WorldRequest PreviewRequest(LandOffer offer)
        {
            WorldRequest shaped = Slotted().Shape(offer.Request);
            return new WorldRequest(shaped.Seed, shaped.ArchetypeId, shaped.Level, larger: shaped.Larger);
        }

        private Preview? PreviewOf(LandOffer offer)
        {
            WorldRequest request = PreviewRequest(offer);
            string key = KeyOf(request);
            if (_previews.TryGetValue(key, out Preview? preview)) return preview;
            preview = new Preview { Growing = Task.Run(() => Grow(request)) };
            _previews[key] = preview;
            return preview;
        }

        private static (byte[] Pixels, Vector2 Landing, Vector2 Tyrant) Grow(WorldRequest request)
        {
            WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(request);
            byte[] pixels = WorldMapImage.Render(world, PreviewResolution, showSites: false);
            WorldSite? tyrant = world.Sites.FirstOrDefault(site => site.Kind is SiteKind.Keep or SiteKind.Arena);
            Vector2 landing = world.Sites[0].Position / world.Size;
            return (pixels, landing, tyrant != null ? tyrant.Position / world.Size : landing);
        }

        // Grown maps go to the GPU here, on the thread that owns it, and the oldest that aren't shown go, so that clicking through levels doesn't fill the card's memory.
        private void TendPreviews()
        {
            foreach (Preview preview in _previews.Values)
            {
                if (preview.Texture != 0 || preview.Growing is not { IsCompleted: true } growing) continue;
                if (growing.IsFaulted)
                {
                    Console.WriteLine($"A land's map would not grow: {growing.Exception?.GetBaseException().Message}");
                    preview.Growing = null;
                    continue;
                }
                (byte[] pixels, Vector2 landing, Vector2 tyrant) = growing.Result;
                preview.Texture = Upload(pixels);
                preview.Landing = landing;
                preview.Tyrant = tyrant;
                preview.Growing = null;
            }

            if (_previews.Count <= MostPreviewsKept) return;
            var shown = new HashSet<string>(_offers.Select(offer => KeyOf(PreviewRequest(offer))));
            foreach (string key in _previews.Keys.Where(key => !shown.Contains(key)).Take(_previews.Count - MostPreviewsKept).ToList())
            {
                Preview old = _previews[key];
                if (old.Growing != null) continue;
                if (old.Texture != 0) GL.DeleteTexture(old.Texture);
                _previews.Remove(key);
            }
        }

        private static int Upload(byte[] pixels)
        {
            int texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, PreviewResolution, PreviewResolution, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }

        // ---- Pieces.

        /// <summary>A land: its map, its name, what kind of land and weather, its tyrant, what it asks, and the way in.</summary>
        private sealed class LandCard : StackPanel
        {
            public LandCard(MapTablePanel panel, LandOffer offer, int index, LandTerms orders) : base(Orientation.Vertical, 4f)
            {
                float inner = CardWidth - CardPad * 2f;
                Width = CardWidth;
                Height = UIValue.Fill;
                Padding = new Thickness(CardPad);
                BackgroundColor = Ui.InkLight;
                BorderColor = offer.Arena ? ArenaInk.MultiplyAlpha(0.7f) : Line;
                BorderWidth = 1f;
                CornerRadius = 2f;

                WorldPlan plan = offer.Plan;
                Herald herald = offer.Herald;
                Add(new MapPreview(panel, offer, inner));
                Add(new Spacer(height: 4f));
                Add(Sized(Ui.Paragraph(plan.Name, 17f, Ui.Parchment), inner));
                Add(Sized(Ui.Line($"{plan.Archetype.Name}  ·  {Lands.WeatherOf(plan)}", 12.5f, offer.Arena ? ArenaInk : Ui.Faded), inner));
                Add(Sized(Ui.Line($"{herald.Beast}, {Chronicle.KindName(herald.BossKind)}", 13.5f, Ui.Gold), inner));
                string asks = offer.Arena ? "Bring down its tyrant" : $"Free {offer.Terms.Places} places, then its tyrant";
                if (orders.Has(OrdersCatalog.TwinPens)) asks = offer.Arena ? "Bring down both tyrants" : $"Free {offer.Terms.Places} places, then both tyrants";
                Add(Sized(Ui.Line(asks, 13f, Ui.Faded), inner));
                Add(new Spacer(height: UIValue.Fill));

                Button open = Add(Ui.Action("Open", () => panel.OpenWay(index)));
                open.Width = inner;
                int level = offer.Request.Level;
                open.Label.Bind(() => GameSession.Current?.Client.Portal is var (name, request, _) && name == plan.Name && request.Level == level ? "The way is open"
                    : panel.SlottedCells.Count == 1 ? "Open with 1 order" : panel.SlottedCells.Count > 1 ? $"Open with {panel.SlottedCells.Count} orders" : "Open");
            }

            private static T Sized<T>(T element, float width) where T : GuiElement
            {
                element.Width = width;
                return element;
            }
        }

        /// <summary>A land's map, once it's grown, with where heroes come in and where its tyrant waits.</summary>
        private sealed class MapPreview : GuiElement
        {
            private readonly MapTablePanel _panel;
            private readonly LandOffer _offer;

            public MapPreview(MapTablePanel panel, LandOffer offer, float side)
            {
                _panel = panel;
                _offer = offer;
                Width = side;
                Height = side;
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null) return;
                GuiRect map = Bounds;
                draw.AddRect(map, GuiColors.Rgba(12, 10, 8, 255), 2f, 1f, Line);
                Preview? preview = _panel.PreviewOf(_offer);
                if (preview == null || preview.Texture == 0)
                {
                    Ui.Text(Context, draw, "…", 22f, map, Ui.Faded, Alignment.Center);
                    return;
                }

                // +x runs to the left of the screen in play, so the map runs that way too, as the travel map does.
                draw.AddImage(preview.Texture, map, new Vector2(1f, 0f), new Vector2(0f, 1f), GuiColors.White, cornerRadius: 2f);
                Vector2 At(Vector2 share) => new Vector2(map.Right - share.X * map.Width, map.Bottom - share.Y * map.Height);
                draw.AddCircle(At(preview.Landing), 4.5f, GuiColors.Rgba(240, 236, 224), 1.5f, GuiColors.Rgba(20, 16, 12));
                draw.AddCircle(At(preview.Tyrant), 5.5f, ArenaInk, 1.5f, GuiColors.Rgba(20, 16, 12));
            }
        }

        /// <summary>One level in the list down the side: its number, and whether it's the next or an arena.</summary>
        private sealed class LevelRow : GuiElement
        {
            private readonly MapTablePanel _panel;
            private readonly int _level;
            private readonly bool _next;

            public LevelRow(MapTablePanel panel, int level, bool next)
            {
                _panel = panel;
                _level = level;
                _next = next;
                Width = UIValue.Fill;
                Height = RowHeight;
                HitTest = HitTestMode.Block;
                MouseDown += e =>
                {
                    if (e.Button != MouseButton.Left) return;
                    e.Handled = true;
                    _panel.ChooseLevel(_level);
                };
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null) return;
                bool chosen = _panel.IsChosenLevel(_level);
                if (chosen) draw.AddRect(Bounds, Chosen.MultiplyAlpha(0.6f), 0f);
                else if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 18), 0f);

                bool arena = Lands.IsBossLevel(_level);
                var text = new GuiRect(Bounds.X + 12f, Bounds.Y, Bounds.Width - 24f, Bounds.Height);
                Ui.Text(Context, draw, $"{_level}", 15f, text, chosen ? Ui.Bright : arena ? ArenaInk : Ui.Parchment);
                string? note = _next ? "next" : arena ? "arena" : null;
                if (note != null) Ui.Text(Context, draw, note, 12f, text, _next ? Ui.Good : ArenaInk, Alignment.End);
            }
        }

        /// <summary>The slots orders go in, as many as the table has room for: empty until something is put in one, and clicking a full one takes it back out.</summary>
        private sealed class SlotRow : GuiElement
        {
            private readonly MapTablePanel _panel;
            private int _slots = 1;

            public SlotRow(MapTablePanel panel)
            {
                _panel = panel;
                Width = SideWidth;
                Height = SlotSize;
                HitTest = HitTestMode.Block;
                MouseDown += e =>
                {
                    if (e.Button != MouseButton.Left) return;
                    int slot = SlotAt(e.Position);
                    if (slot < 0) return;
                    e.Handled = true;
                    _panel.Unslot(slot);
                };
            }

            public void Refresh(int slots)
            {
                _slots = slots;
                int rows = (slots + SlotsAcross - 1) / SlotsAcross;
                Height = rows * SlotSize + (rows - 1) * SlotGap;
            }

            private GuiRect RectOf(int slot) => new GuiRect(Bounds.X + slot % SlotsAcross * (SlotSize + SlotGap), Bounds.Y + slot / SlotsAcross * (SlotSize + SlotGap), SlotSize, SlotSize);

            private int SlotAt(Vector2 point)
            {
                for (int slot = 0; slot < _slots; slot++)
                {
                    if (RectOf(slot).Contains(point)) return slot;
                }
                return -1;
            }

            protected override void OnUpdate(float deltaTime)
            {
                if (Context == null || GameSession.Current == null) return;
                int slot = IsHovered ? SlotAt(Context.MousePosition) : -1;
                IReadOnlyList<int> cells = _panel.SlottedCells;
                _panel.LookAt(slot >= 0 && slot < cells.Count ? GameSession.Current.Hero.Inventory[cells[slot]] : null, slot >= 0 ? RectOf(slot) : Bounds, this);
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null || GameSession.Current == null) return;
                IReadOnlyList<int> cells = _panel.SlottedCells;
                for (int slot = 0; slot < _slots; slot++)
                {
                    GuiRect rect = RectOf(slot);
                    if (slot < cells.Count && GameSession.Current.Hero.Inventory[cells[slot]] is Item item)
                    {
                        Colour colour = Ui.RarityColour(item.Rarity);
                        draw.AddRect(rect, Ui.InkLight, 2f);
                        Ui.DrawItem(draw, item, rect, colour, GuiColors.White);
                        draw.AddRect(rect, GuiColors.Transparent, 2f, 1.5f, colour.MultiplyAlpha(0.9f));
                    }
                    else
                    {
                        draw.AddRect(rect, GuiColors.Rgba(10, 8, 6, 160), 2f, 1f, Line.MultiplyAlpha(0.8f));
                        Ui.Text(Context, draw, "+", 20f, rect, Line, Alignment.Center);
                    }
                }
            }
        }

        /// <summary>The orders in the pack that aren't in a slot: clicking one puts it in the first empty one.</summary>
        private sealed class PackRow : GuiElement
        {
            private const float Chip = 40f;
            private const float ChipGap = 6f;
            private readonly MapTablePanel _panel;
            private List<int> _cells = new();

            public PackRow(MapTablePanel panel)
            {
                _panel = panel;
                Width = SideWidth;
                Height = Chip;
                HitTest = HitTestMode.Block;
                MouseDown += e =>
                {
                    if (e.Button != MouseButton.Left) return;
                    int at = ChipAt(e.Position);
                    if (at < 0) return;
                    e.Handled = true;
                    _panel.Slot(_cells[at]);
                };
            }

            private int Across => Math.Max((int)((SideWidth + ChipGap) / (Chip + ChipGap)), 1);

            public void Refresh()
            {
                _cells = _panel.UnslottedCells.ToList();
                int rows = Math.Max((_cells.Count + Across - 1) / Across, 1);
                Height = rows * Chip + (rows - 1) * ChipGap;
            }

            private GuiRect RectOf(int i) => new GuiRect(Bounds.X + i % Across * (Chip + ChipGap), Bounds.Y + i / Across * (Chip + ChipGap), Chip, Chip);

            private int ChipAt(Vector2 point)
            {
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (RectOf(i).Contains(point)) return i;
                }
                return -1;
            }

            protected override void OnUpdate(float deltaTime)
            {
                if (Context == null || GameSession.Current == null) return;
                int at = IsHovered ? ChipAt(Context.MousePosition) : -1;
                _panel.LookAt(at >= 0 ? GameSession.Current.Hero.Inventory[_cells[at]] : null, at >= 0 ? RectOf(at) : Bounds, this);
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                if (Context == null || GameSession.Current == null) return;
                if (_cells.Count == 0)
                {
                    Ui.Text(Context, draw, "None", 13f, Bounds, Ui.Faded);
                    return;
                }
                bool full = _panel.SlottedCells.Count >= Lands.OrderSlots(GameSession.Current.Hero);
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (GameSession.Current.Hero.Inventory[_cells[i]] is not Item item) continue;
                    GuiRect rect = RectOf(i);
                    Colour colour = Ui.RarityColour(item.Rarity);
                    bool hovered = IsHovered && rect.Contains(Context.MousePosition);
                    draw.AddRect(rect, Ui.InkLight, 2f);
                    Ui.DrawItem(draw, item, rect, colour, full ? GuiColors.Rgba(150, 150, 150) : GuiColors.White);
                    draw.AddRect(rect, hovered && !full ? GuiColors.Rgba(255, 240, 200, 30) : GuiColors.Transparent, 2f, 1f, colour.MultiplyAlpha(full ? 0.35f : 0.75f));
                }
            }
        }

        /// <summary>One thing the land will hold, in the colour of a reward or a danger. An omen opens its card when it's looked at.</summary>
        private sealed class EffectRow : StackPanel
        {
            private readonly bool _omen;

            public EffectRow(MapTablePanel panel, OmenDefinition? omen, float value, string text, Colour colour) : base(Orientation.Vertical, 0f)
            {
                _omen = omen != null;
                Width = SideWidth - 12f;
                Height = UIValue.Auto;
                HitTest = HitTestMode.Block;
                Label line = Add(Ui.Paragraph(text, 13f, colour));
                line.Width = SideWidth - 12f;
                if (omen == null) return;
                MouseEnter += () => panel.LookAt(omen, value, Bounds);
                MouseLeave += () => panel.LookAt(null, 0f, Bounds);
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                if (_omen && IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 16), 2f);
                base.OnDraw(draw);
            }
        }

        /// <summary>A thin line between parts of the side.</summary>
        private sealed class Rule : GuiElement
        {
            public Rule()
            {
                Width = SideWidth;
                Height = 9f;
            }

            protected override void OnDraw(GuiDrawList draw)
            {
                base.OnDraw(draw);
                draw.AddLine(new Vector2(Bounds.X, Bounds.Y + 4f), new Vector2(Bounds.Right, Bounds.Y + 4f), Line, 1f);
            }
        }
    }

    /// <summary>Asked before leaving a land that isn't free, as going home now loses it, though it costs nothing but the try.</summary>
    internal sealed class LeavePrompt : GuiElement
    {
        private readonly Label _says;

        public LeavePrompt(Action leave, Action stay)
        {
            Anchor = Anchor.Center;
            Width = 460f;
            Height = UIValue.Auto;
            Padding = new Thickness(22f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("Leave this land?", 22f));
            _says = body.Add(Ui.Paragraph("", 15f));
            var buttons = body.Add(new StackPanel(Orientation.Horizontal, 10f) { Width = UIValue.Fill, Height = 34f });
            Button go = buttons.Add(Ui.Action("Leave it", leave));
            go.Width = UIValue.Relative(0.5f, -5f);
            Button wait = buttons.Add(Ui.Action("Stay", stay));
            wait.Width = UIValue.Relative(0.5f, -5f);
        }

        public void Ask()
        {
            GameSession session = GameSession.Current;
            _says.Text = $"{session.World.Plan.Name} isn't free. Go home now and it's lost, along with the orders given for it, though the map table will show it again to try once more.";
            Visible = true;
        }
    }
}
