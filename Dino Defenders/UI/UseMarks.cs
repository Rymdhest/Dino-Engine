using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// Everything the hero can use, marked where it stands. One shape means "this can be used", whatever it is: a dark pill with a stem down to the thing, which reads over any
    /// ground, day or night. What's inside says what it is, and in reach it grows to name the key and what it does, on the very thing that key would use. Clicking one walks the hero
    /// there and uses it, on the same terms as a find's label: only the marks themselves stop the mouse, and an attack already held goes straight across them. Its colours keep clear
    /// of what loot and enemies mean. Anything that's an <see cref="InteractableComponent"/> gets one, with nothing to add.
    /// </summary>
    internal sealed class UseMarks : GuiElement
    {
        private const float PillHeight = 30f;
        private const float PillEnd = 13f;          // From the end of the pill to the end of its words.
        private const float IconRoom = 30f;
        private const float IconSize = 9f;
        private const float KeySize = 21f;
        private const float StemLength = 12f;
        private const float TextDrop = 1.5f;        // This face sits high in its line.
        private const float Gap = 3f;
        private const float EdgeMargin = 60f;
        private const float NameSize = 14f;
        private const float PromptSize = 16.5f;
        private const float NameReach = 48f;        // Metres from the hero. Further than this a mark is only its icon.
        private const float FadeFrom = 62f;
        private const float FadeTo = 84f;
        private const float StandsWithin = 8f;      // How far from where it's used a place's model can be.
        private const float BobHeight = 2.5f;
        private const float BobRate = 2.1f;
        private const float BreathRate = 2.6f;
        private const float RestedShare = 0.72f;    // A shrine already lit has been dealt with, so it stops calling.
        private const float ClickPadding = 4f;
        private const int Nothing = -1;

        private static readonly BitMask Places = new BitMask(typeof(InteractableComponent), typeof(PositionComponent));

        private struct Mark
        {
            public InteractKind Kind;
            public string? Prompt;      // Set on the one thing the key would use.
            public bool Named;
            public bool Rested;
            public int Order;
            public int Site;
            public int Spawn;
            public float Alpha;
            public Vector2 Anchor;
            public Vector2 At;
            public GuiRect Rect;
        }

        private readonly List<Mark> _marks = new();
        private readonly List<Vector2> _line = new();
        private readonly Dictionary<(string, float), float> _widths = new();
        private readonly Dictionary<int, Vector3> _stands = new();
        private GameSession? _session;
        private int _hovered = Nothing;
        private bool _heldElsewhere;

        public UseMarks()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.Block;
            Cursor = MouseCursor.Hand;
        }

        // A key is square until what it says is longer than that.
        private float KeyWidth() => MathF.Max(KeySize, WidthOf(Bindings.Label(GameAction.Interact), 15f) + 10f);

        // Whether a mark under the mouse should have it. A button held since before it got here belongs to the fight, or the attack would stop every time the mouse crossed one.
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

        public override bool HitTestSelf(Vector2 point) => Open && MarkAt(point) != Nothing;

        protected override void OnUpdate(float deltaTime)
        {
            _heldElsewhere = Engine.WindowHandler.IsMouseButtonDown(MouseButton.Left) && Context?.CapturedElement != this;
        }

        protected override void OnMouseDown(GuiMouseEventArgs e)
        {
            GameSession session = GameSession.Current;
            e.Handled = true;
            if (session == null || e.Button != MouseButton.Left) return;

            int order = MarkAt(e.Position);
            foreach (Mark mark in _marks)
            {
                if (mark.Order != order) continue;
                session.Fetch.BeginUse(mark.Site, mark.Spawn, mark.At);
                break;
            }
        }

        private int MarkAt(Vector2 point)
        {
            // In the order they were placed, which is the order they're on top of each other.
            foreach (Mark mark in _marks)
            {
                GuiRect rect = mark.Rect;
                if (new GuiRect(rect.X - ClickPadding, rect.Y - ClickPadding, rect.Width + ClickPadding * 2f, rect.Height + ClickPadding * 2f).Contains(point)) return mark.Order;
            }
            return Nothing;
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            GameSession session = GameSession.Current;
            if (session == null || Context == null) return;
            if (session.Health <= 0f)
            {
                _marks.Clear();
                return;
            }
            if (session != _session)
            {
                _session = session;
                _stands.Clear();
            }

            Gather(session, Bounds);
            Arrange();
            _hovered = IsHovered && Open ? MarkAt(Context.MousePosition) : Nothing;
            for (int i = _marks.Count - 1; i >= 0; i--) Draw(draw, _marks[i]);
        }

        private void Gather(GameSession session, GuiRect screen)
        {
            _marks.Clear();
            ECSWorld world = Engine.Instance!.world;
            foreach (Archetype archetype in world.QueryArchetypes(Places, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var place = entity.Get<InteractableComponent>();
                    Vector3 stands = StandsAt(session, place, entity.Get<PositionComponent>().value);
                    float distance = (stands.Xz - session.PlayerPosition).Length;
                    if (distance >= FadeTo) continue;

                    float scale = entity.Has<ScaleComponent>() ? entity.Get<ScaleComponent>().value.Y : 1f;
                    if (!Hud.Project(stands + Vector3.UnitY * (HeightOf(place.Kind) * scale), screen, out Vector2 at)) continue;
                    if (at.X < -EdgeMargin || at.Y < -EdgeMargin || at.X > screen.Width + EdgeMargin || at.Y > screen.Height + EdgeMargin) continue;

                    bool offered = entity.Entity.Id == session.OfferedThing.Id;
                    bool rested = place.Kind == InteractKind.Shrine && (uint)place.SiteIndex < (uint)session.Sites.Count && session.Sites[place.SiteIndex].Kindled;
                    bool named = offered || distance < NameReach;
                    float width = IconRoom + (offered ? KeyWidth() + 7f + WidthOf(session.InteractPrompt ?? "", PromptSize) + PillEnd : named ? WidthOf(NameOf(place.Kind), NameSize) + PillEnd : 0f);

                    _marks.Add(new Mark
                    {
                        Kind = place.Kind,
                        Prompt = offered ? session.InteractPrompt : null,
                        Named = named,
                        Rested = rested && !offered,
                        Order = place.SiteIndex * 4096 + place.SpawnIndex,
                        Site = place.SiteIndex,
                        Spawn = place.SpawnIndex,
                        At = entity.Get<PositionComponent>().value.Xz,
                        Alpha = 1f - Math.Clamp((distance - FadeFrom) / (FadeTo - FadeFrom), 0f, 1f),
                        Anchor = at,
                        Rect = new GuiRect(at.X - width * 0.5f, at.Y - StemLength - PillHeight, width, PillHeight),
                    });
                }
            }

            // What the key would use keeps its place. The rest keep an order of their own, so that they don't change places as the hero walks.
            _marks.Sort(static (a, b) => (a.Prompt != null) != (b.Prompt != null) ? (a.Prompt != null ? -1 : 1) : a.Order.CompareTo(b.Order));
        }

        // A shrine or a gate is used from a little way in front of it, and it's what stands there that the mark belongs on.
        private Vector3 StandsAt(GameSession session, in InteractableComponent place, Vector3 position)
        {
            string? model = place.Kind switch
            {
                InteractKind.Shrine => StructureCatalog.Altar.Id,
                InteractKind.Waygate => StructureCatalog.Waygate.Id,
                InteractKind.Portal => StructureCatalog.GreatWaygate.Id,
                InteractKind.MapTable => StructureCatalog.MapTable.Id,
                InteractKind.Stash => StructureCatalog.Strongbox.Id,
                _ => null,
            };
            if (model == null) return position;

            int key = place.SiteIndex * 4096 + place.SpawnIndex;
            if (_stands.TryGetValue(key, out Vector3 known)) return known;

            Vector3 stands = session.World.Objects.TryNearest(model, position.Xz, StandsWithin, out WorldObject found) ? found.Position : position;
            _stands[key] = stands;
            return stands;
        }

        // Each goes up past anything it would land on, as a merchant can stand at a chest's elbow when the camera's far out.
        private void Arrange()
        {
            for (int i = 1; i < _marks.Count; i++)
            {
                Mark mark = _marks[i];
                for (int other = 0; other < i; other++)
                {
                    if (LabelStack.Lift(ref mark.Rect, _marks[other].Rect, Gap)) other = -1;
                }
                _marks[i] = mark;
            }
        }

        private void Draw(GuiDrawList draw, in Mark mark)
        {
            bool offered = mark.Prompt != null;
            bool hovered = mark.Order == _hovered;
            Colour colour = ColourOf(mark.Kind);

            // Things that are waiting move a little, as it's movement the eye goes to. What the key would use holds still to be read.
            float bob = offered || hovered || mark.Rested ? 0f : MathF.Sin(Engine.Time * BobRate + mark.Order) * BobHeight;
            GuiRect rect = new GuiRect(mark.Rect.X, mark.Rect.Y + bob, mark.Rect.Width, mark.Rect.Height);

            draw.PushOpacity(hovered ? 1f : mark.Alpha * (mark.Rested ? RestedShare : 1f));

            draw.AddLine(new Vector2(mark.Anchor.X, rect.Bottom), mark.Anchor, GuiColors.Rgba(0, 0, 0, 170), 4f);
            draw.AddLine(new Vector2(mark.Anchor.X, rect.Bottom), mark.Anchor, colour, 2f);
            draw.AddCircle(mark.Anchor, 3f, colour, 1f, GuiColors.Rgba(0, 0, 0, 190));

            if (!mark.Rested || hovered)
            {
                float glow = offered || hovered ? 0.6f : 0.26f + 0.12f * MathF.Sin(Engine.Time * BreathRate + mark.Order);
                float spread = offered || hovered ? 9f : 6f;
                draw.AddRect(new GuiRect(rect.X - spread, rect.Y - spread, rect.Width + spread * 2f, rect.Height + spread * 2f), colour.MultiplyAlpha(glow), PillHeight * 0.5f + spread, 0f, default, 12f);
            }

            draw.AddRect(rect, Ui.Ink, PillHeight * 0.5f, offered || hovered ? 2f : 1.5f, colour);
            DrawIcon(draw, mark.Kind, new Vector2(rect.X + IconRoom * 0.5f + 1f, rect.Y + PillHeight * 0.5f), IconSize, colour);

            float x = rect.X + IconRoom;
            if (offered)
            {
                GuiRect key = new GuiRect(x, rect.Y + (PillHeight - KeySize) * 0.5f, KeyWidth(), KeySize);
                draw.AddRect(key, colour, 4f);
                GuiText.Draw(Context!, draw, Bindings.Label(GameAction.Interact), Ui.Face, 15f, new GuiRect(key.X, key.Y + TextDrop, key.Width, key.Height), GuiColors.Rgba(14, 10, 8), Alignment.Center, Alignment.Center);
                x += key.Width + 7f;
                Ui.Text(Context!, draw, mark.Prompt!, PromptSize, new GuiRect(x, rect.Y + TextDrop, rect.Right - x, PillHeight), Ui.Parchment);
            }
            else if (mark.Named) Ui.Text(Context!, draw, NameOf(mark.Kind), NameSize, new GuiRect(x, rect.Y + TextDrop, rect.Right - x, PillHeight), Ui.Parchment);

            draw.PopOpacity();
        }

        private float WidthOf(string text, float size)
        {
            if (!_widths.TryGetValue((text, size), out float width))
            {
                width = GuiText.MeasureWidth(Context!, text, Ui.Face, size);
                _widths[(text, size)] = width;
            }
            return width;
        }

        private static string NameOf(InteractKind kind) => kind switch
        {
            InteractKind.Shrine => "Shrine",
            InteractKind.Chest => "Chest",
            InteractKind.Waygate or InteractKind.Portal => "Waygate",
            InteractKind.Merchant => "Merchant",
            InteractKind.Stash => "Stash",
            InteractKind.MapTable => "Map Table",
            _ => "Elder",
        };

        // Metres above the thing's feet: over a head, over a lid, over the altar's crystal, and in the mouth of a gate, whose top is too far up to seem to belong to it.
        private static float HeightOf(InteractKind kind) => kind switch
        {
            InteractKind.Shrine => 4.4f,
            InteractKind.Chest => 2.3f,
            InteractKind.Waygate => 5f,
            InteractKind.Portal => 7.5f,
            InteractKind.MapTable => 3f,
            InteractKind.Stash => 3.6f,
            _ => 5.2f,
        };

        // The colours the places themselves give off, so that a mark and what it's on agree. People are all one, and told apart by what's in the mark.
        private static Colour ColourOf(InteractKind kind) => kind switch
        {
            InteractKind.Shrine => GuiColors.Rgba(112, 236, 176),
            InteractKind.Chest => GuiColors.Rgba(246, 190, 84),
            InteractKind.Waygate or InteractKind.Portal => GuiColors.Rgba(104, 206, 255),
            InteractKind.MapTable => GuiColors.Rgba(150, 214, 255),
            InteractKind.Stash => GuiColors.Rgba(214, 188, 146),
            _ => GuiColors.Rgba(240, 228, 204),
        };

        private void DrawIcon(GuiDrawList draw, InteractKind kind, Vector2 c, float s, Colour colour)
        {
            const float Stroke = 2f;
            switch (kind)
            {
                case InteractKind.Chest:
                    draw.AddRect(new GuiRect(c.X - s, c.Y - s * 0.7f, s * 2f, s * 1.5f), GuiColors.Transparent, s * 0.3f, Stroke, colour);
                    draw.AddLine(c + new Vector2(-s, -s * 0.05f), c + new Vector2(s, -s * 0.05f), colour, Stroke);
                    draw.AddCircle(c + new Vector2(0f, -s * 0.05f), s * 0.3f, colour);
                    break;
                case InteractKind.Stash:
                    draw.AddRect(new GuiRect(c.X - s, c.Y - s * 0.55f, s * 2f, s * 1.4f), GuiColors.Transparent, s * 0.2f, Stroke, colour);
                    draw.AddLine(c + new Vector2(-s, -s * 0.05f), c + new Vector2(s, -s * 0.05f), colour, Stroke);
                    draw.AddRect(new GuiRect(c.X - s * 0.28f, c.Y - s * 1.05f, s * 0.56f, s * 0.6f), GuiColors.Transparent, s * 0.28f, Stroke, colour);
                    draw.AddCircle(c + new Vector2(0f, s * 0.35f), s * 0.2f, colour);
                    break;
                case InteractKind.MapTable:
                    _line.Clear();
                    _line.Add(c + new Vector2(-s, -s * 0.7f));
                    _line.Add(c + new Vector2(-s * 0.33f, -s * 0.9f));
                    _line.Add(c + new Vector2(s * 0.33f, -s * 0.7f));
                    _line.Add(c + new Vector2(s, -s * 0.9f));
                    _line.Add(c + new Vector2(s, s * 0.7f));
                    _line.Add(c + new Vector2(s * 0.33f, s * 0.9f));
                    _line.Add(c + new Vector2(-s * 0.33f, s * 0.7f));
                    _line.Add(c + new Vector2(-s, s * 0.9f));
                    _line.Add(c + new Vector2(-s, -s * 0.7f));
                    draw.AddPolyline(_line, colour, Stroke);
                    draw.AddLine(c + new Vector2(-s * 0.33f, -s * 0.9f), c + new Vector2(-s * 0.33f, s * 0.7f), colour, 1.2f);
                    draw.AddLine(c + new Vector2(s * 0.33f, -s * 0.7f), c + new Vector2(s * 0.33f, s * 0.9f), colour, 1.2f);
                    break;
                case InteractKind.Waygate:
                case InteractKind.Portal:
                    _line.Clear();
                    for (int i = 0; i <= 12; i++)
                    {
                        float angle = MathF.PI * i / 12f;
                        _line.Add(c + new Vector2(MathF.Cos(angle) * s * 0.85f, s * 0.15f - MathF.Sin(angle) * s * 1.1f));
                    }
                    draw.AddPolyline(_line, colour, Stroke);
                    draw.AddLine(c + new Vector2(-s * 0.85f, s * 0.15f), c + new Vector2(-s * 0.85f, s * 0.9f), colour, Stroke);
                    draw.AddLine(c + new Vector2(s * 0.85f, s * 0.15f), c + new Vector2(s * 0.85f, s * 0.9f), colour, Stroke);
                    draw.AddLine(c + new Vector2(-s * 1.1f, s * 0.9f), c + new Vector2(s * 1.1f, s * 0.9f), colour, Stroke);
                    draw.AddCircle(c + new Vector2(0f, s * 0.1f), s * 0.34f, colour);
                    break;
                case InteractKind.Shrine:
                    Vector2 top = c + new Vector2(0f, -s * 1.1f), right = c + new Vector2(s * 0.7f, 0f), bottom = c + new Vector2(0f, s * 1.1f), left = c + new Vector2(-s * 0.7f, 0f);
                    draw.AddLine(top, right, colour, Stroke, roundCaps: true);
                    draw.AddLine(right, bottom, colour, Stroke, roundCaps: true);
                    draw.AddLine(bottom, left, colour, Stroke, roundCaps: true);
                    draw.AddLine(left, top, colour, Stroke, roundCaps: true);
                    draw.AddCircle(c, s * 0.26f, colour);
                    break;
                case InteractKind.Merchant:
                    draw.AddCircle(c + new Vector2(0f, s * 0.25f), s * 0.8f, GuiColors.Transparent, Stroke, colour);
                    draw.AddLine(c + new Vector2(-s * 0.32f, -s * 0.5f), c + new Vector2(-s * 0.6f, -s * 1.05f), colour, Stroke, roundCaps: true);
                    draw.AddLine(c + new Vector2(s * 0.32f, -s * 0.5f), c + new Vector2(s * 0.6f, -s * 1.05f), colour, Stroke, roundCaps: true);
                    draw.AddLine(c + new Vector2(-s * 0.6f, -s * 1.05f), c + new Vector2(s * 0.6f, -s * 1.05f), colour, Stroke, roundCaps: true);
                    draw.AddCircle(c + new Vector2(0f, s * 0.25f), s * 0.26f, colour);
                    break;
                default:
                    draw.AddRect(new GuiRect(c.X - s, c.Y - s * 0.85f, s * 2f, s * 1.35f), GuiColors.Transparent, s * 0.4f, Stroke, colour);
                    draw.AddLine(c + new Vector2(-s * 0.35f, s * 0.5f), c + new Vector2(-s * 0.55f, s * 1.1f), colour, Stroke, roundCaps: true);
                    draw.AddLine(c + new Vector2(-s * 0.55f, s * 1.1f), c + new Vector2(s * 0.2f, s * 0.5f), colour, Stroke, roundCaps: true);
                    for (int i = -1; i <= 1; i++) draw.AddCircle(c + new Vector2(i * s * 0.5f, -s * 0.17f), s * 0.14f, colour);
                    break;
            }
        }
    }
}
