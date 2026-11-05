using Dino_Defenders.Gameplay;
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
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// The talent tree, as a map to wander. Dragging moves it and the wheel zooms. Resting on a node shows what it does and lights the cheapest way
    /// there from what's already taken; a click takes the whole of that way, and a right click gives a node back if nothing depends on it. It's far too
    /// big to read, so typing in the box lights whatever mentions what's typed.
    /// </summary>
    internal sealed class TalentPanel : GuiElement
    {
        private const float Nearest = 2.2f;
        private const float Farthest = 0.2f;
        private const float WheelStep = 1.14f;
        private const float PickSlack = 10f;
        private const float SmallestDot = 2.4f;
        private const float LabelsFrom = 0.8f;
        private const float SectorRadius = 980f;
        private const float TipWidth = 340f;

        private const string ForgetText = "Forget all";
        private const double ForgetWindow = 4.0;    // Seconds to press again.
        private const double ForgetPause = 0.4;     // Sooner than this is the same press, bounced or doubled.

        private readonly TextBox _search;
        private readonly Button _forget;
        private double _forgetUntil;    // While this is ahead of the clock, a press means it.
        private readonly bool[] _taken = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _open = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _lit = new bool[TalentTree.Nodes.Length];
        private readonly bool[] _onPath = new bool[TalentTree.Nodes.Length];
        private readonly string[] _text = new string[TalentTree.Nodes.Length];
        private List<int> _path = new();
        private Vector2 _focus;
        private float _zoom = 0.5f;
        private int _hover = -1;
        private int _hoverPointsWhenPlanned = -1;

        private static GameSession Session => GameSession.Current;
        private static HeroState Hero => Session.Hero;

        public TalentPanel()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            BackgroundColor = GuiColors.Rgba(8, 7, 9);
            HitTest = HitTestMode.Block;
            CapturesMouse = true;
            Visible = false;

            for (int i = 0; i < _text.Length; i++) _text[i] = Searchable(TalentTree.Nodes[i]);

            var top = Add(new StackPanel(Orientation.Horizontal, 16f)
            {
                Width = UIValue.Fill,
                Height = 50f,
                Padding = new Thickness(18f, 10f, 18f, 0f),
                BackgroundColor = GuiColors.Rgba(14, 12, 12, 235),
            });
            top.Add(Ui.Heading("The Tree", 22f)).Width = 130f;
            var points = top.Add(Ui.Line("", 16f, Ui.Gold).Bind(() => Hero.UnspentPoints > 0 ? $"{Hero.UnspentPoints} to spend" : "Nothing to spend"));
            points.Width = 150f;
            top.Add(Ui.Line("", 16f).Bind(() => $"{TalentTree.TitleOf(Hero)}, level {Hero.Level}")).Width = 300f;
            _search = top.Add(new TextBox("", "Search the tree") { Width = 280f, Height = 30f });
            _search.TextChanged += _ => Search();
            _forget = top.Add(Ui.Action(ForgetText, ForgetAll));
            _forget.Width = 120f;
        }

        private static string Searchable(TalentNode node)
        {
            var words = new List<string> { node.Name, node.Kind.ToString() };
            foreach (Mod mod in node.Mods) words.Add(mod.Describe());
            if (node.Lore != null) words.Add(node.Lore);
            return string.Join(' ', words).ToLowerInvariant();
        }

        private void Search()
        {
            string wanted = _search.Text.Trim().ToLowerInvariant();
            for (int i = 0; i < _lit.Length; i++) _lit[i] = wanted.Length > 1 && _text[i].Contains(wanted);
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

        private Vector2 ToScreen(Vector2 tree) => Bounds.Center + (tree - _focus) * _zoom;
        private Vector2 ToTree(Vector2 screen) => (screen - Bounds.Center) / _zoom + _focus;

        private static float SizeOf(TalentKind kind) => kind switch
        {
            TalentKind.Keystone => 34f,
            TalentKind.Root => 26f,
            TalentKind.Gate => 24f,
            TalentKind.Notable or TalentKind.Skill => 21f,
            _ => 11f,
        };

        protected override void OnDrag(GuiDragEventArgs e)
        {
            _focus -= e.Delta / _zoom;
            float reach = 1750f;
            if (_focus.Length > reach) _focus = _focus.Normalized() * reach;
        }

        protected override bool WantsDrag => true;

        protected override void OnScroll(GuiScrollEventArgs e)
        {
            Vector2 under = ToTree(e.Position);
            _zoom = Math.Clamp(_zoom * MathF.Pow(WheelStep, e.Delta.Y), Farthest, Nearest);
            _focus += under - ToTree(e.Position);
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
                    float reach = MathF.Max(SizeOf(node.Kind) * _zoom, SmallestDot) + PickSlack;
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
            DrawSectorNames(draw);
            DrawLinks(draw);
            DrawNodes(draw);
            if (_hover >= 0) DrawTip(draw, TalentTree.Nodes[_hover]);
            Ui.Text(Context, draw, "Drag to move, wheel to zoom. Click to take the lit path, right click to give a node back. T to close.", 14f,
                new GuiRect(0f, Bounds.Height - 30f, Bounds.Width, 20f), Ui.Faded, Alignment.Center);
            draw.PopClip();
        }

        private void DrawSectorNames(GuiDrawList draw)
        {
            float alpha = Math.Clamp((0.62f - _zoom) / 0.2f, 0f, 1f);
            if (alpha <= 0f) return;

            for (int i = 0; i < TalentContent.Sectors.Length; i++)
            {
                Sector sector = TalentContent.Sectors[i];
                float angle = MathHelper.DegreesToRadians(i * 30f);
                Vector2 at = ToScreen(new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * SectorRadius);
                Colour colour = GuiColors.Rgba(sector.Colour.X, sector.Colour.Y, sector.Colour.Z, 0.34f * alpha);
                Ui.Text(Context!, draw, sector.Name, 30f, new GuiRect(at.X - 160f, at.Y - 18f, 320f, 36f), colour, Alignment.Center);
            }
        }

        private void DrawLinks(GuiDrawList draw)
        {
            GuiRect view = Bounds;
            foreach (TalentNode node in TalentTree.Nodes)
            {
                Vector2 from = ToScreen(node.Position);
                foreach (int link in node.Links)
                {
                    if (link < node.Index) continue;
                    TalentNode other = TalentTree.Nodes[link];
                    Vector2 to = ToScreen(other.Position);
                    if (!Crosses(view, from, to)) continue;

                    bool both = _taken[node.Index] && _taken[link];
                    bool planned = (_onPath[node.Index] || _taken[node.Index]) && (_onPath[link] || _taken[link]) && !both;
                    Colour colour = both ? Ui.Gold
                        : planned ? GuiColors.Rgba(240, 226, 170)
                        : _taken[node.Index] || _taken[link] ? GuiColors.Rgba(120, 104, 74)
                        : GuiColors.Rgba(70, 64, 58);
                    draw.AddLine(from, to, colour, both ? 2.6f : planned ? 2.2f : 1.2f);
                }

                // A gate's twin is far off. A short mark says which way, once it's lit.
                if (node.Twin > node.Index && (_taken[node.Index] || _taken[node.Twin]))
                {
                    draw.AddLine(from, ToScreen(TalentTree.Nodes[node.Twin].Position), GuiColors.Rgba(150, 200, 255, 90), 1f);
                }
            }
        }

        private static bool Crosses(GuiRect view, Vector2 from, Vector2 to)
        {
            float left = MathF.Min(from.X, to.X);
            float right = MathF.Max(from.X, to.X);
            float top = MathF.Min(from.Y, to.Y);
            float bottom = MathF.Max(from.Y, to.Y);
            return right >= view.X && left <= view.Right && bottom >= view.Y && top <= view.Bottom;
        }

        private void DrawNodes(GuiDrawList draw)
        {
            GuiRect view = Bounds;
            foreach (TalentNode node in TalentTree.Nodes)
            {
                Vector2 at = ToScreen(node.Position);
                float radius = MathF.Max(SizeOf(node.Kind) * _zoom, SmallestDot);
                if (at.X < view.X - radius || at.X > view.Right + radius || at.Y < view.Y - radius || at.Y > view.Bottom + radius) continue;

                Vector3 hue = node.Sector >= 0 ? TalentContent.Sectors[node.Sector].Colour : new Vector3(0.9f, 0.86f, 0.7f);
                int i = node.Index;
                float shade = _taken[i] ? 1f : _open[i] || _onPath[i] ? 0.62f : 0.26f;
                Colour fill = GuiColors.Rgba(hue.X * shade, hue.Y * shade, hue.Z * shade);
                Colour edge = _taken[i] ? Ui.Gold : _onPath[i] ? GuiColors.Rgba(240, 226, 170) : _open[i] ? GuiColors.Rgba(200, 186, 150) : GuiColors.Rgba(84, 78, 70);
                float thickness = node.Kind == TalentKind.Small ? 1f : 2f;

                if (_lit[i]) draw.AddCircle(at, radius + 7f, GuiColors.Rgba(255, 230, 120, 70), 2f, GuiColors.Rgba(255, 230, 120));

                switch (node.Kind)
                {
                    case TalentKind.Skill:
                        draw.AddRect(new GuiRect(at.X - radius, at.Y - radius, radius * 2f, radius * 2f), fill, 3f, thickness, edge);
                        break;
                    case TalentKind.Gate:
                        draw.AddCircle(at, radius, GuiColors.Rgba(10, 14, 24), thickness, GuiColors.Rgba(150, 200, 255));
                        draw.AddCircle(at, radius * 0.58f, fill, 1f, edge);
                        break;
                    case TalentKind.Keystone:
                        draw.AddCircle(at, radius + 3f, GuiColors.Rgba(0, 0, 0, 0), 1.5f, edge);
                        draw.AddCircle(at, radius, fill, 2.4f, edge);
                        break;
                    default:
                        draw.AddCircle(at, radius, fill, thickness, edge);
                        break;
                }

                bool named = node.Kind is TalentKind.Keystone or TalentKind.Skill or TalentKind.Notable or TalentKind.Root;
                if (named && (_zoom >= LabelsFrom || node.Kind == TalentKind.Keystone && _zoom >= 0.42f))
                {
                    Colour text = _taken[i] ? Ui.Parchment : Ui.Faded;
                    Ui.Text(Context!, draw, node.Name, 13f, new GuiRect(at.X - 110f, at.Y + radius + 2f, 220f, 16f), text, Alignment.Center);
                }
            }
        }

        private void DrawTip(GuiDrawList draw, TalentNode node)
        {
            var lines = new List<(string text, float size, Colour colour)>
            {
                (node.Name, 19f, node.Kind == TalentKind.Keystone ? Ui.Gold : Ui.Parchment),
                (node.Kind switch
                {
                    TalentKind.Keystone => "Keystone",
                    TalentKind.Skill => "Teaches a skill",
                    TalentKind.Notable => "Notable",
                    TalentKind.Gate => "Waygate",
                    TalentKind.Root => "Where every hero starts",
                    _ => node.Sector >= 0 ? TalentContent.Sectors[node.Sector].Name : "",
                }, 13f, Ui.Faded),
            };

            foreach (Mod mod in node.Mods)
            {
                foreach (string line in Ui.Wrap(Context!, mod.Describe(), 15f, TipWidth - 24f)) lines.Add((line, 15f, Ui.Parchment));
            }

            if (node.Lore != null)
            {
                foreach (string line in Ui.Wrap(Context!, node.Lore, 14f, TipWidth - 24f)) lines.Add((line, 14f, Ui.Faded));
            }

            if (_taken[node.Index])
            {
                if (node.Index != TalentTree.Root.Index) lines.Add((TalentTree.CanRefund(Hero, node.Index) ? "Right click to give it back" : "Other talents depend on this", 13f, Ui.Faded));
            }
            else if (_path.Count > 0)
            {
                bool enough = _path.Count <= Hero.UnspentPoints;
                string cost = _path.Count == 1 ? "1 point" : $"{_path.Count} points by this path";
                lines.Add((enough ? $"Click to take: {cost}" : $"Needs {cost}", 13f, enough ? Ui.Gold : Ui.Blood));
            }

            float height = 18f;
            foreach ((_, float size, _) in lines) height += size + 6f;

            Vector2 mouse = Context!.MousePosition;
            float x = mouse.X + TipWidth + 24f > Bounds.Right ? mouse.X - TipWidth - 16f : mouse.X + 16f;
            float y = MathF.Min(mouse.Y + 12f, Bounds.Bottom - height - 12f);
            draw.AddRect(new GuiRect(x, y, TipWidth, height), Ui.Ink, 3f, 1f, GuiColors.Rgba(120, 96, 58), 4f);

            float line_y = y + 9f;
            foreach ((string text, float size, Colour colour) in lines)
            {
                Ui.Text(Context!, draw, text, size, new GuiRect(x + 12f, line_y, TipWidth - 24f, size + 4f), colour);
                line_y += size + 6f;
            }
        }
    }

    /// <summary>What's on the bar, and everything that could be. A slot is picked, then a skill to go in it.</summary>
    internal sealed class SkillPanel : GuiElement
    {
        public static readonly string[] Keys = { "LMB", "RMB", "Q", "1", "2", "3", "4" };

        private readonly StackPanel _slots;
        private readonly StackPanel _known;
        private readonly StackPanel _details;
        private int _chosen;

        private static GameSession Session => GameSession.Current;

        public SkillPanel()
        {
            Anchor = Anchor.Center;
            Width = 980f;
            Height = 640f;
            Padding = new Thickness(20f);
            Ui.Dress(this);
            Visible = false;

            var columns = Add(new StackPanel(Orientation.Horizontal, 18f) { Width = UIValue.Fill, Height = UIValue.Fill });

            var left = columns.Add(new StackPanel(Orientation.Vertical, 6f) { Width = 230f, Height = UIValue.Fill });
            left.Add(Ui.Heading("Skills"));
            left.Add(Ui.Line("The bar", 15f, Ui.Gold));
            _slots = left.Add(new StackPanel(Orientation.Vertical, 6f) { Width = UIValue.Fill, Height = UIValue.Auto });

            var middle = columns.Add(new StackPanel(Orientation.Vertical, 6f) { Width = 300f, Height = UIValue.Fill });
            middle.Add(Ui.Line("What you know", 15f, Ui.Gold));
            var scroll = middle.Add(new ScrollPanel { Width = UIValue.Fill, Height = UIValue.Fill });
            _known = scroll.Content.Add(new StackPanel(Orientation.Vertical, 5f) { Width = UIValue.Fill, Height = UIValue.Auto });

            _details = columns.Add(new StackPanel(Orientation.Vertical, 3f) { Width = UIValue.Fill, Height = UIValue.Fill });
        }

        public void Open()
        {
            Rebuild();
            Describe(SkillIn(_chosen));
        }

        private static SkillDefinition? SkillIn(int slot)
        {
            string? id = Session.Hero.Bar[slot];
            return id != null ? SkillCatalog.Find(id) : null;
        }

        private void Rebuild()
        {
            _slots.ClearChildren();
            for (int i = 0; i < HeroState.SkillSlots; i++)
            {
                int slot = i;
                string name = SkillIn(slot)?.Name ?? "Empty";
                Button button = _slots.Add(Ui.Action($"{(slot == _chosen ? "> " : "")}{Keys[slot]}: {name}", () =>
                {
                    _chosen = slot;
                    Audio.Play(Interface.Open);
                    Rebuild();
                    Describe(SkillIn(slot));
                }));
                button.Height = 34f;
            }

            _known.ClearChildren();
            _known.Add(Ui.Action("Nothing", () => Assign(null)));
            foreach (SkillDefinition skill in Session.Sheet.Loadout.Skills)
            {
                if (skill.Hidden) continue;
                SkillDefinition known = skill;
                Button button = _known.Add(Ui.Action(skill.Name, () => Assign(known)));
                button.MouseEnter += () => Describe(known);
            }
        }

        private void Assign(SkillDefinition? skill)
        {
            Audio.Play(Interface.Equip);
            Session.Hero.Bar[_chosen] = skill?.Id;
            Session.Client.AssignSlot(_chosen, skill);
            Rebuild();
            Describe(skill);
        }

        private void Describe(SkillDefinition? skill)
        {
            _details.ClearChildren();
            if (skill == null)
            {
                _details.Add(Ui.Paragraph("Pick a slot, then a skill to go in it. New skills are learned on the tree, and from what you wear.", 14f, Ui.Faded));
                return;
            }

            _details.Add(Ui.Paragraph(skill.Name, 20f, Ui.Gold));
            _details.Add(Ui.Paragraph(skill.Text, 14f, Ui.Faded));
            if (Session.Sheet.Profile(skill) is not SkillProfile profile) return;

            _details.Add(new Spacer(height: 6f));
            foreach (string line in SkillText.Describe(profile)) _details.Add(Ui.Paragraph(line, 14f));
        }
    }

    /// <summary>What a skill comes to for someone, in words.</summary>
    internal static class SkillText
    {
        public static List<string> Describe(SkillProfile skill)
        {
            var lines = new List<string>();
            string kind = skill.Skill.IsAttack ? "Attacks" : "Casts";
            if (skill.DealsDamage)
            {
                lines.Add($"{skill.AverageHit:0.#} a hit, {skill.DamagePerSecond:0.#} a second");
                foreach (DamageFlow flow in skill.Flows)
                {
                    float scale = MathF.Max(1f + flow.Increased / 100f, 0f) * flow.More;
                    lines.Add($"  {flow.Min * scale:0.#} to {flow.Max * scale:0.#} {flow.Type}");
                }
                lines.Add($"Critical {skill.CritChance:0.#}% for {skill.CritMultiplier:0}%");
            }

            lines.Add($"{kind} in {skill.UseTime:0.##} seconds");
            if (skill.Cost > 0f) lines.Add($"Costs {skill.Cost:0.#} {(skill.CostsHealth ? "health" : "mana")}");
            if (skill.Cooldown > 0f) lines.Add($"Cooldown {skill.Cooldown:0.#} seconds");
            if (skill.Skill.Needs != Tags.None) lines.Add("Needs a bow in hand");
            if (skill.Radius > 0f) lines.Add($"Reaches {skill.Radius:0.#} metres around");
            if (skill.Projectiles > 1) lines.Add($"{skill.Projectiles} projectiles");
            if (skill.Pierce > 0) lines.Add($"Pierces {skill.Pierce}");
            if (skill.Chains > 0) lines.Add($"Chains {skill.Chains} times");
            if (skill.Duration > 0f) lines.Add($"Lasts {skill.Duration:0.#} seconds");

            for (int i = 0; i < AilmentRules.Count; i++)
            {
                if (skill.Ailments[i].Chance > 0f) lines.Add($"{skill.Ailments[i].Chance:0}% chance of {(Ailment)i}");
            }
            return lines;
        }
    }
}
