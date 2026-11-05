using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The land as a map, turned the way the land is seen: the hero knows its shape, but a place only appears on it once they
    /// have found it. Shrines that have been kindled can be travelled to.
    /// </summary>
    internal sealed class MapPanel : GuiElement
    {
        public const int Resolution = 1024;
        private const float PickRadius = 16f;

        private int _texture;
        private readonly Action _close;

        public MapPanel(Action close)
        {
            _close = close;
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(8, 6, 5, 215);
            HitTest = HitTestMode.Block;
            Visible = false;
            Clicked += OnPick;
        }

        public void SetImage(byte[] pixels)
        {
            if (_texture != 0) GL.DeleteTexture(_texture);
            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, Resolution, Resolution, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private GuiRect MapRect()
        {
            float side = MathF.Min(Bounds.Width, Bounds.Height) * 0.84f;
            return new GuiRect((Bounds.Width - side) * 0.5f, (Bounds.Height - side) * 0.5f + 12f, side, side);
        }

        // +x runs to the left of the screen in play, so the map runs that way too, and what's ahead on the land is up on the map.
        private static Vector2 ToMap(GuiRect map, Vector2 position, float worldSize)
        {
            return new Vector2(map.Right - position.X / worldSize * map.Width, map.Bottom - position.Y / worldSize * map.Height);
        }

        private void OnPick(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
            GuiRect map = MapRect();
            for (int i = 0; i < session.Sites.Count; i++)
            {
                SiteState state = session.Sites[i];
                if (!CanTravelTo(state)) continue;
                Vector2 at = ToMap(map, state.Site.Position, session.World.Size);
                if ((at - e.Position).Length > PickRadius) continue;

                session.Client.TravelToSite(i);
                _close();
                return;
            }
        }

        private static bool CanTravelTo(SiteState state)
        {
            return state.Kindled || state.Site.Kind == SiteKind.Landing;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            GameSession session = GameSession.Current;
            if (session == null || _texture == 0 || Context == null) return;

            GuiRect map = MapRect();
            float size = session.World.Size;
            draw.AddRect(new GuiRect(map.X - 3f, map.Y - 3f, map.Width + 6f, map.Height + 6f), Ui.Ink, 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddImage(_texture, map, new Vector2(1f, 0f), new Vector2(0f, 1f), GuiColors.White);

            Ui.Text(Context, draw, session.World.Plan.Name, 26f, new GuiRect(0f, map.Y - 40f, Bounds.Width, 32f), Ui.Parchment, Alignment.Center);

            foreach (SiteState state in session.Sites)
            {
                if (state.Wild || !state.Discovered) continue;
                Vector2 at = ToMap(map, state.Site.Position, size);
                Vector3 c = state.Site.Definition.MapColour;
                Colour colour = state.Cleared ? Ui.Faded : GuiColors.Rgba(c.X, c.Y, c.Z);
                float radius = CanTravelTo(state) ? 6f : 4.5f;
                draw.AddCircle(at, radius, colour, 1.5f, GuiColors.Rgba(0, 0, 0, 200));
                Ui.Text(Context, draw, state.Site.Name, 13.5f, new GuiRect(at.X - 110f, at.Y + 6f, 220f, 18f), Ui.Parchment, Alignment.Center);
            }

            if (session.Guidance is Vector2 goal)
            {
                Vector2 at = ToMap(map, goal, size);
                draw.AddCircle(at, 9f, GuiColors.Transparent, 2f, Ui.Gold);
            }

            Vector2 hero = ToMap(map, session.PlayerPosition, size);
            draw.AddCircle(hero, 5f, Ui.Parchment, 2f, GuiColors.Rgba(150, 30, 24));

            string help = "M to close.  Click a kindled shrine or the landing to travel there.";
            Ui.Text(Context, draw, help, 14f, new GuiRect(0f, map.Bottom + 6f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
        }
    }

    /// <summary>One place for an item: a bag cell, an equipment slot, or something on a merchant's table.</summary>
    internal sealed class ItemCell : GuiElement
    {
        private readonly Func<Item?> _item;
        private readonly ItemSlot? _slot;
        private readonly Action<Item?> _hover;

        public ItemCell(Func<Item?> item, Action<Item> pressed, Action<Item?> hover, ItemSlot? slot = null)
        {
            _item = item;
            _hover = hover;
            _slot = slot;
            Width = 52f;
            Height = 52f;
            BackgroundColor = Ui.InkLight;
            BorderColor = GuiColors.Rgba(84, 68, 46);
            BorderWidth = 1f;
            CornerRadius = 2f;
            Clicked += e =>
            {
                Item? held = _item();
                if (held != null) pressed(held);
            };
            MouseEnter += () => _hover(_item());
            MouseLeave += () => _hover(null);
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            base.OnDraw(draw);
            Item? item = _item();
            if (item == null)
            {
                if (_slot is ItemSlot empty) Ui.DrawSlotIcon(draw, empty, Bounds, Ui.Faded.MultiplyAlpha(0.4f));
                return;
            }

            Colour colour = Ui.GradeColour(Loot.GradeOf(item));
            if (IsHovered) draw.AddRect(Bounds, GuiColors.Rgba(255, 240, 200, 26), 2f);
            draw.AddRect(Bounds, GuiColors.Transparent, 2f, 1.5f, colour.MultiplyAlpha(0.8f));
            Ui.DrawSlotIcon(draw, item.Slot, Bounds, colour);
        }
    }

    /// <summary>
    /// The hero and what they carry: what they come to, gear to wear and, at a merchant, a table to buy from. Whatever the mouse
    /// is over is described on the right, set against what is worn in its place.
    /// </summary>
    internal sealed class HeroPanel : GuiElement
    {
        private const int Columns = 6;
        private const int StockSize = 12;
        private const int FlaskPrice = 12;  // Shown here; the server is what charges it.

        private readonly StackPanel _details;
        private readonly GuiElement _market;
        private readonly Label _title;
        private SiteState? _merchant;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public HeroPanel()
        {
            Anchor = Anchor.Center;
            Width = 1060f;
            Height = 660f;
            Padding = new Thickness(20f);
            Ui.Dress(this);
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, 20f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var left = columns.Add(new StackPanel(Orientation.Vertical, 3f) { Width = 250f, Height = UIValue.Fill });
            _title = left.Add(Ui.Heading("The Hero"));
            left.Add(Ui.Line("", 15f, Ui.Parchment).Bind(() => TalentTree.TitleOf(Hero)));
            left.Add(Ui.Line().Bind(() => $"Level {Hero.Level}, {Hero.Gold} gold"));
            left.Add(Ui.Line("", 14f, Ui.Gold).Bind(() => Hero.UnspentPoints > 0 ? $"{Hero.UnspentPoints} talents to spend  [T]" : " "));
            left.Add(new Spacer(height: 6f));
            var sheet = left.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            AddStats(sheet.Content.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = UIValue.Auto }));

            var middle = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = 420f, Height = UIValue.Fill });
            middle.Add(Ui.Line("Worn", 15f, Ui.Gold));
            middle.Add(BuildEquipment());
            middle.Add(Ui.Line("Carried", 15f, Ui.Gold));
            var bag = middle.Add(new GridPanel(Columns, 8f, 8f));
            for (int i = 0; i < HeroState.InventorySize; i++)
            {
                int index = i;
                bag.Add(new ItemCell(() => index < Hero.Inventory.Count ? Hero.Inventory[index] : null, UseCarried, Describe));
            }

            var right = columns.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Fill });
            _details = right.Add(new StackPanel(Orientation.Vertical, 2f) { Width = UIValue.Fill, Height = 270f });

            _market = right.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
            _market.Add(Ui.Line("For sale", 15f, Ui.Gold));
            var table = _market.Add(new GridPanel(4, 6f, 6f));
            for (int i = 0; i < StockSize; i++)
            {
                int index = i;
                table.Add(new ItemCell(() => StockAt(index), Buy, Describe));
            }
            _market.Add(Ui.Action($"Fill flasks ({FlaskPrice} gold each)", BuyFlasks));
            _market.Add(Ui.Paragraph("Click your own things to sell them.", 13f, Ui.Faded));

            Describe(null);
        }

        private Item? StockAt(int index)
        {
            return _merchant != null && index < _merchant.Stock.Count ? _merchant.Stock[index] : null;
        }

        private static void AddStats(GuiElement parent)
        {
            void Group(string name) => parent.Add(Ui.Line(name, 14f, Ui.Gold));
            void Line(Func<ActorSheet, string> text) => parent.Add(Ui.Line("", 14f, Ui.Faded).Bind(() => text(Session.Sheet)));

            Group("Your main skill");
            Line(s => Session.Client.Kit.Slots[0] is SkillProfile main ? main.Skill.Name : "Nothing");
            Line(s => Session.Client.Kit.Slots[0] is SkillProfile main ? $"{main.AverageHit:0.#} a hit, {main.DamagePerSecond:0.#} a second" : " ");
            Line(s => Session.Client.Kit.Slots[0] is SkillProfile main ? $"Critical {main.CritChance:0.#}% for {main.CritMultiplier:0}%" : " ");
            Line(s => Session.Client.Kit.Slots[0] is SkillProfile main ? $"Used in {main.UseTime:0.##} seconds" : " ");

            Group("Pools");
            Line(s => $"Health {s.MaxHealth:0}, {s.HealthRegen:0.#} a second");
            Line(s => $"Mana {s.MaxMana:0}, {s.ManaRegen:0.#} a second");
            Line(s => s.MaxBarrier > 0f ? $"Barrier {s.MaxBarrier:0}" : "No barrier");

            Group("Defence");
            Line(s => $"Armour {s.Armour:0}");
            Line(s => $"Evasion {s.Evasion:0}");
            Line(s => s.BlockAttacks > 0f || s.BlockSpells > 0f ? $"Block {s.BlockAttacks:0}% of attacks, {s.BlockSpells:0}% of spells" : "No block");
            Line(s => $"Fire {s.Resistance[1]:0}%  Cold {s.Resistance[2]:0}%  Lightning {s.Resistance[3]:0}%");
            Line(s => $"Magic {s.Resistance[4]:0}%  Poison {s.Resistance[5]:0}%");
            Line(s => s.TakenFromMana > 0f ? $"{s.TakenFromMana:0}% taken from mana first" : " ");

            Group("Going about");
            Line(s => $"Movement {s.MoveSpeed:0.#} m/s");
            Line(s => $"Dodge every {s.DodgeCooldown:0.#} seconds");
            Line(s => s.ItemRarity != 0f ? $"Item rarity {s.ItemRarity:0}%" : " ");
            Line(s => s.GoldFind != 0f ? $"Gold found {s.GoldFind:0}%" : " ");
        }

        private GuiElement BuildEquipment()
        {
            var row = new GridPanel(Enum.GetValues<ItemSlot>().Length, 8f, 8f);
            foreach (ItemSlot slot in Enum.GetValues<ItemSlot>())
            {
                ItemSlot worn = slot;
                row.Add(new ItemCell(() => Hero.Equipped.GetValueOrDefault(worn), item =>
                {
                    Session.Client.Unequip(item.Slot);
                    Audio.Play(Interface.Equip);
                    Describe(null);
                }, Describe, worn));
            }
            return row;
        }

        /// <summary>Shows the panel, with a merchant's table if it was opened at one.</summary>
        public void Open(SiteState? merchant)
        {
            _merchant = merchant;
            _market.Visible = merchant != null;
            _title.Text = merchant != null ? merchant.Site.Name : "The Hero";
            Describe(null);
        }

        // All of these ask, and the server answers: what the hero has changes when it says so, a moment later.
        private void UseCarried(Item item)
        {
            int index = Hero.Inventory.IndexOf(item);
            if (index < 0) return;
            Audio.Play(_merchant != null ? Interface.Trade : Interface.Equip);
            if (_merchant != null) Session.Client.Sell(index);
            else Session.Client.Equip(index);
            Describe(null);
        }

        private void Buy(Item item)
        {
            if (_merchant == null || Hero.Gold < item.BuyValue) return;
            Audio.Play(Interface.Trade);
            Session.Client.Buy(Session.Sites.IndexOf(_merchant), _merchant.Stock.IndexOf(item));
            Describe(null);
        }

        private static void BuyFlasks()
        {
            Audio.Play(Interface.Trade);
            Session.Client.BuyFlasks();
        }

        private void Describe(Item? item)
        {
            _details.ClearChildren();
            if (item == null)
            {
                _details.Add(Ui.Paragraph(_merchant != null ? "Rest your eyes on anything to see what it is worth." : "Click what you carry to wear it, and what you wear to take it off.", 14f, Ui.Faded));
                return;
            }

            Colour colour = Ui.GradeColour(Loot.GradeOf(item));
            _details.Add(Ui.Paragraph(item.Name, 18f, colour));
            string slot = item.Slot == ItemSlot.Weapon ? item.Kind.ToString() : item.Slot.ToString();
            _details.Add(Ui.Line($"{Ui.GradeName(item)} {slot}, level {item.Level}", 13f, Ui.Faded));
            AddBase(item, 14f, Ui.Parchment);
            foreach (string line in item.Describe()) _details.Add(Ui.Paragraph(line, 14f, colour));
            if (item.Unique != null && UniqueCatalog.Find(item.Unique) is UniqueDefinition unique) _details.Add(Ui.Paragraph(unique.Lore, 13f, Ui.Faded));

            bool carried = Hero.Inventory.Contains(item);
            if (_merchant != null) _details.Add(Ui.Line(carried ? $"Sells for {item.SellValue} gold" : $"Costs {item.BuyValue} gold", 14f, Ui.Gold));

            if (carried && Hero.Equipped.TryGetValue(item.Slot, out Item? worn))
            {
                _details.Add(new Spacer(height: 6f));
                _details.Add(Ui.Line($"Now worn: {worn.Name}", 13f, Ui.Faded));
                AddBase(worn, 13f, Ui.Faded);
                foreach (string line in worn.Describe()) _details.Add(Ui.Paragraph(line, 13f, Ui.Faded));
            }
        }

        private void AddBase(Item item, float size, Colour colour)
        {
            foreach (string line in item.DescribeBase()) _details.Add(Ui.Line(line, size, colour));
        }
    }

    /// <summary>The story of the land so far, and, from an elder, a word of thanks when it has been earned.</summary>
    internal sealed class ChroniclePanel : GuiElement
    {
        private readonly StackPanel _body;

        public ChroniclePanel()
        {
            Anchor = Anchor.Center;
            Width = 640f;
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
            _body.Add(Ui.Line($"{session.World.Plan.Archetype.Name}, under {chronicle.Faction}. Level {session.World.Plan.Level}.", 14f, Ui.Faded));
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
            _body.Add(Ui.Line("J to close", 13f, Ui.Faded));
        }

        private void AddElder(GameSession session, SiteState elder)
        {
            _body.Add(new Spacer(height: 8f));
            _body.Add(Ui.Line($"The elder of {elder.Site.Name}", 16f, Ui.Gold));

            if (!session.IsSafe(elder))
            {
                string threat = elder.ThreatIndex >= 0 ? session.Sites[elder.ThreatIndex].Site.Name : "the keep";
                _body.Add(Ui.Paragraph($"\"They come down from {threat} when the mood takes them. Break them there, and you'll have our thanks and more than thanks.\"", 15f));
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

    /// <summary>Where the waygate can lead: other lands, each its own place, and harder once this one is free.</summary>
    internal sealed class WaygatePanel : GuiElement
    {
        private const int Choices = 3;

        private readonly StackPanel _cards;
        private readonly Label _note;
        private readonly Action<WorldRequest> _travel;

        public WaygatePanel(Action<WorldRequest> travel)
        {
            _travel = travel;
            Anchor = Anchor.Center;
            Width = 960f;
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 12f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("The Waygate", 26f));
            _note = body.Add(Ui.Paragraph("", 15f, Ui.Faded));
            _cards = body.Add(new StackPanel(Orientation.Horizontal, 14f) { Width = UIValue.Fill, Height = UIValue.Auto });
        }

        public void Open()
        {
            GameSession session = GameSession.Current;
            bool free = session.Chronicle.WorldCleared;
            int level = session.World.Plan.Level + (free ? 1 : 0);
            _note.Text = free
                ? "With this land free, the old roads open further. Beyond them the lands are harder, and richer."
                : $"While {session.Chronicle.Warlord} holds this land, the gate reaches only as far as lands like it. You may go, and it will not wait for you.";

            _cards.ClearChildren();
            var random = new WorldRandom(session.World.Plan.Seed).Fork("roads onward").Fork(level);
            for (int i = 0; i < Choices; i++)
            {
                var request = new WorldRequest(random.Int(int.MaxValue), null, level);
                _cards.Add(Card(request));
            }
        }

        private GuiElement Card(WorldRequest request)
        {
            WorldPlan plan = WorldPlan.Create(request);
            var card = new StackPanel(Orientation.Vertical, 6f)
            {
                Width = UIValue.Relative(1f / Choices, -10f),
                Height = UIValue.Auto,
                Padding = new Thickness(14f),
                BackgroundColor = Ui.InkLight,
                BorderColor = GuiColors.Rgba(84, 68, 46),
                BorderWidth = 1f,
                CornerRadius = 2f,
            };

            card.Add(Ui.Paragraph(plan.Name, 20f, Ui.Parchment));
            card.Add(Ui.Line(plan.Archetype.Name, 14f, Ui.Gold));
            card.Add(Ui.Line($"Level {plan.Level}", 14f, Ui.Faded));
            card.Add(Ui.Paragraph(string.Join(", ", plan.Biomes.Select(b => b.Name)), 14f));
            card.Add(Ui.Line(plan.Mood.Name, 14f, Ui.Faded));
            card.Add(Ui.Action("Step through", () => _travel(request)));
            return card;
        }
    }

    /// <summary>The game held still: go on, or leave the road for now.</summary>
    internal sealed class PauseMenu : GuiElement
    {
        private readonly Label _confirm;
        private bool _confirming;

        public PauseMenu(Action resume, Action friends, Action quit, Action newJourney)
        {
            Anchor = Anchor.Center;
            Width = 340f;
            Height = UIValue.Auto;
            Padding = new Thickness(22f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 10f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("Rest a While", 24f));
            body.Add(Ui.Action("Go on", resume));
            body.Add(Ui.Action("The hero (C)", () => { resume(); GameUi.Current.Toggle(GameUi.Current.Hero); }));
            body.Add(Ui.Action("The tree (T)", () => { resume(); GameUi.Current.Toggle(GameUi.Current.Talents); }));
            body.Add(Ui.Action("Skills (K)", () => { resume(); GameUi.Current.Toggle(GameUi.Current.Skills); }));
            body.Add(Ui.Action("Travel with friends", friends));
            SoundSettings.AddControls(body);
            body.Add(Ui.Action("Leave the road", quit));
            body.Add(Ui.Action("Begin a new journey", () =>
            {
                if (_confirming)
                {
                    newJourney();
                    return;
                }
                _confirming = true;
                _confirm.Visible = true;
            }));
            _confirm = body.Add(Ui.Paragraph("This forgets your hero for good. Press it again if you mean it.", 14f, Ui.Blood));
            _confirm.Visible = false;

            body.Add(Ui.Line("WASD move, mouse aims, skills on LMB, RMB, Q, 1 to 4", 13f, Ui.Faded));
            body.Add(Ui.Line("Space dodge, R flask, E use, F guiding wind once a land is free", 13f, Ui.Faded));
            body.Add(Ui.Line("M map, C hero, T tree, K skills, J chronicle, wheel zoom", 13f, Ui.Faded));
        }

        public void Reset()
        {
            _confirming = false;
            _confirm.Visible = false;
        }
    }

    /// <summary>
    /// Playing together. A game is open to friends from here, with a code to send them, and the code one has been sent goes in here.
    /// </summary>
    internal sealed class FriendsPanel : GuiElement
    {
        private readonly ArpgGame _game;
        private readonly TextBox _code;
        private readonly Label _problem;

        public FriendsPanel(ArpgGame game)
        {
            _game = game;
            Anchor = Anchor.Center;
            Width = 620f;
            Height = UIValue.Auto;
            Padding = new Thickness(24f);
            Ui.Dress(this);
            Visible = false;

            var body = Add(new StackPanel(Orientation.Vertical, 10f) { Width = UIValue.Fill, Height = UIValue.Auto });
            body.Add(Ui.Heading("Travel with Friends", 24f));

            var hosting = body.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
            var open = hosting.Add(Ui.Action("Open this world to friends", () => _game.OpenToFriends()));
            var invite = hosting.Add(Ui.Line("", 16f, Ui.Gold).Bind(() => _game.InviteText ?? ""));
            var copy = hosting.Add(Ui.Action("Copy the invite", () => Context!.Clipboard = _game.InviteText ?? ""));
            var status = hosting.Add(Ui.Paragraph("", 14f, Ui.Faded).Bind(() => _game.InviteStatus));

            body.Add(new Spacer(height: 6f));
            var joining = body.Add(new StackPanel(Orientation.Vertical, 8f) { Width = UIValue.Fill, Height = UIValue.Auto });
            joining.Add(Ui.Line("Join a friend", 15f, Ui.Gold));
            var name = joining.Add(new TextBox(_game.HeroName, "Your name, for them to see") { Width = UIValue.Fill, Height = 32f, MaxLength = 24 });
            name.TextChanged += text => _game.HeroName = text;
            _code = joining.Add(new TextBox("", "Paste the invite here") { Width = UIValue.Fill, Height = 32f });
            _code.Submitted += _ => Join();
            joining.Add(Ui.Action("Join", Join));
            _problem = joining.Add(Ui.Paragraph("", 14f, Ui.Blood));

            var leave = body.Add(Ui.Action("Go back to your own world", () => _game.ReturnHome(null)));

            // Decided here rather than by each of them, as a hidden element isn't updated and could never show itself again.
            Updated += _ =>
            {
                open.Visible = !_game.Hosting && !_game.InFriendsGame;
                invite.Visible = _game.Hosting;
                copy.Visible = _game.Hosting;
                status.Visible = _game.Hosting;
                joining.Visible = !_game.Hosting;
                leave.Visible = _game.InFriendsGame;
            };

            body.Add(new Spacer(height: 6f));
            body.Add(Ui.Line("", 15f).Bind(() => $"In this game: {string.Join(", ", (_game.Client?.Party ?? new()).Select(p => p.name))}"));
            body.Add(Ui.Line($"Up to {Protocol.MaxPlayers} heroes can share a world. Your hero and what they find come home with you.", 13f, Ui.Faded));
        }

        private void Join()
        {
            _problem.Text = _game.Join(_code.Text) ? "" : "That doesn't look like an invite.";
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

        public readonly Hud Hud = new();
        public readonly LootLabels Labels = new();
        public readonly MapPanel Map;
        public readonly HeroPanel Hero = new();
        public readonly TalentPanel Talents = new();
        public readonly SkillPanel Skills = new();
        public readonly ChroniclePanel Chronicle = new();
        public readonly WaygatePanel Waygate;
        public readonly PauseMenu Pause;
        public readonly FriendsPanel Friends;
        public readonly LoadingScreen Loading = new();

        private readonly GuiElement[] _panels;

        public GameUi(GuiContext gui, ArpgGame game, Action<WorldRequest> travel, Action quit, Action newJourney)
        {
            Current = this;
            Map = new MapPanel(CloseAll);
            Waygate = new WaygatePanel(travel);
            Pause = new PauseMenu(CloseAll, () => Toggle(Friends!), quit, newJourney);
            Friends = new FriendsPanel(game);
            _panels = new GuiElement[] { Map, Hero, Talents, Skills, Chronicle, Waygate, Pause, Friends };

            gui.Root.Add(Hud);
            gui.Root.Add(Labels);
            foreach (GuiElement panel in _panels) gui.Root.Add(panel);
            gui.Root.Add(Loading);
        }

        public bool AnyOpen => _panels.Any(p => p.Visible);

        public void CloseAll()
        {
            if (AnyOpen) Audio.Play(Interface.Close);
            foreach (GuiElement panel in _panels) panel.Visible = false;
            Pause.Reset();
        }

        public void Toggle(GuiElement panel)
        {
            bool open = panel.Visible;
            CloseAll();
            if (open) return;

            Audio.Play(Interface.Open);
            panel.Visible = true;
            if (panel == Hero) Hero.Open(null);
            if (panel == Skills) Skills.Open();
            if (panel == Chronicle) Chronicle.Open(null);
        }

        /// <summary>Opens what goes with something the hero used in the world.</summary>
        public void Open(InteractKind kind, SiteState site)
        {
            CloseAll();
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
                    Waygate.Visible = true;
                    Waygate.Open();
                    break;
            }
        }
    }
}
