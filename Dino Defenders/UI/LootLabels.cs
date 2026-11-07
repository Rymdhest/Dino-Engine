using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// What's on the ground, as a box of its name in its rarity's colour, for clicking on. Labels push each other up rather than overlap, with the best finds placed first, so
    /// a boss's fountain stays readable and what's worth having is what stays where it fell. Clicking walks the hero to it. Only the labels themselves stop the mouse: everywhere
    /// else it goes through to the game. Holding shift goes through them as well, to fight over the top of what's on the ground.
    /// </summary>
    internal sealed class LootLabels : GuiElement
    {
        private const float LabelHeight = 1.9f;     // World units above the ground, so that a label stands off what it names.
        private const float PaddingX = 8f;
        private const float PaddingY = 3f;
        private const float Gap = 2f;
        private const float EdgeMargin = 40f;
        private const float ShiftedFar = 6f;        // Pixels a label can move before it needs a line back to its find.
        private const float CalloutLife = 4.2f;
        private const float CalloutTop = 66f;
        private const float CalloutStep = 44f;

        private static readonly float[] Sizes = { 13f, 14f, 15.5f, 19f };
        private static readonly BitMask Pickups = new BitMask(typeof(PickupComponent), typeof(PositionComponent));

        private struct Label
        {
            public int Find;
            public Item Item;
            public Rarity Rarity;
            public GuiRect Rect;
            public Vector2 Anchor;
            public float Fresh;     // 1 when it's just landed, falling away to nothing.
            public Vector2 At;
        }

        private readonly List<Label> _labels = new();
        private readonly Dictionary<int, float> _widths = new();
        private GameSession? _session;
        private int _hovered;
        private bool _heldElsewhere;

        public LootLabels()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
        }

        // Whether a label under the mouse should have it. A button held since before it got here belongs to the fight, or the attack would stop every time the mouse crossed a find.
        private bool Open
        {
            get
            {
                GameSession session = GameSession.Current;
                if (session == null || session.Paused || session.Health <= 0f || _heldElsewhere) return false;
                if (Engine.WindowHandler.IsMouseButtonDown(MouseButton.Right)) return false;
                return !Bindings.Down(GameAction.ForceAttack);
            }
        }

        public override bool HitTestSelf(Vector2 point) => Open && LabelAt(point) != 0;

        protected override void OnUpdate(float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;

            bool down = Engine.WindowHandler.IsMouseButtonDown(MouseButton.Left);
            _heldElsewhere = down && Context?.CapturedElement != this;

            for (int i = session.Callouts.Count - 1; i >= 0; i--)
            {
                LootCallout callout = session.Callouts[i];
                callout.Age += deltaTime;
                if (callout.Age >= CalloutLife) session.Callouts.RemoveAt(i);
                else session.Callouts[i] = callout;
            }
        }

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
            e.Handled = true;
            if (session == null || e.Button != MouseButton.Left) return;

            foreach (Label label in _labels)
            {
                if (label.Find != LabelAt(e.Position)) continue;
                session.Fetch.Begin(label.Find, label.At);
                break;
            }
        }

        private int LabelAt(Vector2 point)
        {
            // In the order they were placed, which is best first.
            foreach (Label label in _labels)
            {
                if (label.Rect.Contains(point)) return label.Find;
            }
            return 0;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            GameSession session = GameSession.Current;
            if (session == null || Context == null) return;
            if (session != _session)
            {
                _session = session;
                _widths.Clear();
            }

            GuiRect screen = Bounds;
            Gather(session, screen);
            Arrange();

            _hovered = IsHovered && Open ? LabelAt(Context.MousePosition) : 0;
            for (int i = _labels.Count - 1; i >= 0; i--) Draw(draw, _labels[i]);
            DrawCallouts(draw, session, screen);

            foreach (Label label in _labels)
            {
                if (label.Find == _hovered) Ui.DrawTooltip(session.Hero, label.Item, label.Rect, null, compare: true);
            }
        }

        private void Gather(GameSession session, GuiRect screen)
        {
            _labels.Clear();
            ECSWorld world = Engine.Instance!.world;
            foreach (Archetype archetype in world.QueryArchetypes(Pickups, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    if (pickup.Find == 0 || !pickup.Landed || session.Taken.ContainsKey(pickup.Find)) continue;
                    if (!session.Ground.TryGetValue(pickup.Find, out Item? item)) continue;
                    if (!Hud.Project(pickup.Rest + Vector3.UnitY * LabelHeight, screen, out Vector2 at)) continue;
                    if (at.X < -EdgeMargin || at.Y < -EdgeMargin || at.X > screen.Width + EdgeMargin || at.Y > screen.Height + EdgeMargin) continue;

                    float size = Sizes[(int)pickup.Rarity];
                    if (!_widths.TryGetValue(pickup.Find, out float width))
                    {
                        width = GuiText.MeasureWidth(Context!, item.Name, Ui.Face, size);
                        _widths[pickup.Find] = width;
                    }

                    float boxWidth = width + PaddingX * 2f;
                    float boxHeight = size + 5f + PaddingY * 2f;
                    _labels.Add(new Label
                    {
                        Find = pickup.Find,
                        Item = item,
                        Rarity = pickup.Rarity,
                        Rect = new GuiRect(at.X - boxWidth * 0.5f, at.Y - boxHeight, boxWidth, boxHeight),
                        Anchor = at,
                        Fresh = MathF.Exp(-pickup.LandedFor * 2.6f),
                        At = pickup.Rest.Xz,
                    });
                }
            }

            // By find as well, so that labels of a rarity keep their order from one frame to the next, and don't change places as the hero walks.
            _labels.Sort(static (a, b) => a.Rarity != b.Rarity ? b.Rarity.CompareTo(a.Rarity) : a.Find.CompareTo(b.Find));
        }

        // Each goes up past anything it would land on. There are never many on screen, and the ones placed first are the ones worth keeping where they are.
        private void Arrange()
        {
            for (int i = 1; i < _labels.Count; i++)
            {
                Label label = _labels[i];
                for (int other = 0; other < i; other++)
                {
                    if (LabelStack.Lift(ref label.Rect, _labels[other].Rect, Gap)) other = -1;
                }
                _labels[i] = label;
            }
        }

        private void Draw(GuiDrawList draw, in Label label)
        {
            GuiRect rect = label.Rect;
            Colour colour = Ui.RarityColour(label.Rarity);
            bool hovered = label.Find == _hovered;
            int rarity = (int)label.Rarity;

            float shifted = label.Anchor.Y - rect.Bottom;
            if (shifted > ShiftedFar) draw.AddLine(new Vector2(label.Anchor.X, rect.Bottom), label.Anchor, colour.MultiplyAlpha(0.45f), 1f);

            // A find that's just come down throws light on its own label, and the very best never quite stop.
            float pulse = label.Rarity == Rarity.Unique ? 0.32f + 0.16f * MathF.Sin(Engine.Time * 3.4f) : 0f;
            float glow = MathF.Max(label.Fresh, pulse);
            if (glow > 0.01f && rarity >= (int)Rarity.Magic)
            {
                float spread = 5f + 7f * label.Fresh;
                draw.AddRect(new GuiRect(rect.X - spread, rect.Y - spread, rect.Width + spread * 2f, rect.Height + spread * 2f), colour.MultiplyAlpha(glow * 0.75f), 6f, 0f, default, 12f);
            }

            Colour fill = hovered ? colour.MultiplyAlpha(0.92f) : Ui.RarityFill(label.Rarity);
            float border = label.Rarity == Rarity.Unique ? 1.6f : 1f;
            draw.AddRect(rect, fill, 2f, border, colour.MultiplyAlpha(hovered ? 1f : label.Rarity == Rarity.Normal ? 0.35f : 0.85f));

            Colour text = hovered ? GuiColors.Rgba(14, 10, 8) : colour;
            Ui.Text(Context!, draw, label.Item.Name, Sizes[rarity], rect, text, Alignment.Center);
        }

        // What was found, across the top of the screen, for the finds that are the reason for playing. It's the same moment as the sound breaking and the beam going up.
        private void DrawCallouts(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.Callouts.Count == 0) return;

            // Between a boss's bar and where banners start, which one of these often arrives with: the fall of a warlord is a banner too.
            float y = CalloutTop;
            foreach (LootCallout callout in session.Callouts)
            {
                Colour colour = Ui.RarityColour(callout.Rarity);
                float alpha = Math.Clamp(MathF.Min(callout.Age / 0.12f, (CalloutLife - callout.Age) / 0.7f), 0f, 1f);
                bool unique = callout.Rarity == Rarity.Unique;

                // A flush of colour from the edges of the screen, gone in a moment.
                float flush = MathF.Exp(-callout.Age * 2.4f) * (unique ? 0.42f : 0.2f);
                float depth = screen.Height * 0.24f;
                draw.AddRectGradient(new GuiRect(0f, 0f, screen.Width, depth), colour.MultiplyAlpha(flush), colour.MultiplyAlpha(0f));
                draw.AddRectGradient(new GuiRect(0f, screen.Height - depth, screen.Width, depth), colour.MultiplyAlpha(0f), colour.MultiplyAlpha(flush));

                // It lands big and settles.
                float punch = 1f + 0.45f * MathF.Exp(-callout.Age * 9f);
                float size = (unique ? 27f : 23f) * punch;
                string text = $"{callout.Rarity}: {callout.Text}";
                draw.PushOpacity(alpha);
                float back = MathF.Min(GuiText.MeasureWidth(Context!, text, Ui.Face, size) + 90f, screen.Width);
                draw.AddRect(new GuiRect((screen.Width - back) * 0.5f, y, back, CalloutStep - 6f), GuiColors.Rgba(10, 8, 6, 105), 10f, 0f, default, 18f);
                Ui.Text(Context!, draw, text, size, new GuiRect(0f, y, screen.Width, CalloutStep - 6f), colour, Alignment.Center);
                draw.PopOpacity();
                y += CalloutStep;
            }
        }
    }
}
