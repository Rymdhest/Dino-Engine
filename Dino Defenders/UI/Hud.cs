using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.GUI.Widgets;
using Dino_Engine.Util;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// What is always on screen, kept to the edges so the land stays the picture: how far the hero has come, what the chronicle
    /// asks for next, and the words and numbers that come off the world itself. Health and Mana are on the <see cref="ActionBar"/>.
    /// </summary>
    internal sealed class Hud : GuiElement
    {
        private const float Margin = 26f;
        private const float BannerFade = 0.7f;
        private const float LogLife = 8f;
        private const float BossRange = 70f;
        private const float OverHead = 5.6f;        // Metres above the hero's feet, where everyone else's health is.
        private const float OverWidth = 96f;
        private const float OverHealth = 9f;
        private const float OverMana = 5f;

        /// <summary>How much of the top of the Health bar is Barrier's, wherever it's shown. Anything written on the bar goes under it, as it's too pale to read over.</summary>
        public const float BarrierHeight = 3f;

        private const float CornerWidth = 340f;
        private const float OmenGap = 16f;

        private static readonly BitMask EnemyMask = new BitMask(typeof(EnemyComponent), typeof(HealthComponent), typeof(PositionComponent));
        private static readonly BitMask DummyMask = new BitMask(typeof(DummyComponent), typeof(PositionComponent));
        private static readonly BitMask HeroMask = new BitMask(typeof(HeroComponent), typeof(HealthComponent), typeof(PositionComponent));

        public Hud()
        {
            Width = UIValue.Fill;
            Height = UIValue.Fill;
            HitTest = HitTestMode.PassThrough;
        }

        protected override void OnUpdate(float deltaTime)
        {
            GameSession session = GameSession.Current;
            if (session == null) return;

            var texts = session.FloatingTexts;
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                FloatingText text = texts[i];
                text.Age += deltaTime;
                text.Position.Y += deltaTime * 1.4f;
                if (text.Age >= text.Life) texts.RemoveAt(i);
                else texts[i] = text;
            }

            // Banners wait their turn; only the first one ages.
            if (session.Banners.Count > 0)
            {
                Banner banner = session.Banners[0];
                banner.Age += deltaTime;
                if (banner.Age >= banner.Life) session.Banners.RemoveAt(0);
                else session.Banners[0] = banner;
            }

            for (int i = session.Log.Count - 1; i >= 0; i--)
            {
                LogLine line = session.Log[i];
                line.Age += deltaTime;
                if (line.Age >= LogLife) session.Log.RemoveAt(i);
                else session.Log[i] = line;
            }
        }

        protected override void OnDraw(GuiDrawList draw)
        {
            GameSession session = GameSession.Current;
            if (session == null || Context == null) return;

            GuiRect screen = Bounds;
            if (session.Health <= 0f) draw.AddRect(screen, GuiColors.Rgba(20, 0, 0, 130));

            DrawEnemies(draw, session, screen);
            DrawDummies(draw, session, screen);
            DrawCompanions(draw, session, screen);
            DrawOverHero(draw, session, screen);
            DrawFloatingTexts(draw, session, screen);
            DrawGuidance(draw, session, screen);
            DrawCalling(draw, session, screen);
            DrawStanding(draw, session, screen);
            DrawChronicle(draw, session, screen);
            DrawLog(draw, session, screen);
            DrawBanner(draw, session, screen);
            DrawConnection(draw, session);
        }

        // Nothing here stops the mouse, as it's also what's aimed with: a card comes up for what it's resting on, and never while it's being used for anything.
        private bool Resting(GuiRect rect)
        {
            if (Context == null || GameUi.Current.AnyOpen) return false;
            if (Context.IsMouseDown(MouseButton.Left) || Context.IsMouseDown(MouseButton.Right)) return false;
            return rect.Contains(Context.MousePosition);
        }

        // World to screen, in the same convention the renderer uses (row vectors).
        public static bool Project(Vector3 world, GuiRect screen, out Vector2 point)
        {
            var context = Engine.RenderEngine.context;
            Vector4 clip = new Vector4(world, 1f) * context.viewMatrix * context.projectionMatrix;
            point = default;
            if (clip.W <= 0.01f) return false;
            point = new Vector2((clip.X / clip.W * 0.5f + 0.5f) * screen.Width, (0.5f - clip.Y / clip.W * 0.5f) * screen.Height);
            return true;
        }

        private void DrawEnemies(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            ECSWorld world = Engine.Instance!.world;
            foreach (Archetype archetype in world.QueryArchetypes(EnemyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var enemy = entity.Get<EnemyComponent>();
                    if (enemy.State == EnemyState.Dead) continue;

                    var health = entity.Get<HealthComponent>();
                    Vector3 position = entity.Get<PositionComponent>().value;
                    float share = Math.Clamp(health.Current / MathF.Max(health.Max, 1f), 0f, 1f);

                    if (enemy.Friendly)
                    {
                        if (!Project(position + Vector3.UnitY * 5f, screen, out Vector2 over)) continue;
                        GuiRect bar = new GuiRect(over.X - 22f, over.Y, 44f, 4f);
                        draw.AddRect(bar, GuiColors.Rgba(0, 0, 0, 170));
                        draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * share, bar.Height), Ui.Good);

                        // A Minion that pays for its book shows what it has left to pay with, under its Health.
                        if (entity.Has<ManaComponent>())
                        {
                            var mana = entity.Get<ManaComponent>();
                            GuiRect well = new GuiRect(bar.X, bar.Bottom + 1f, bar.Width, 3f);
                            draw.AddRect(well, GuiColors.Rgba(0, 0, 0, 170));
                            draw.AddRect(new GuiRect(well.X, well.Y, well.Width * Math.Clamp(mana.Current / MathF.Max(mana.Max, 1f), 0f, 1f), well.Height), Ui.Mana);
                        }
                        continue;
                    }

                    // A dummy's bar would only ever be full, and what's dealt to it is said over its group.
                    if (entity.Has<DummyComponent>()) continue;

                    bool sought = session.Sought(enemy);
                    float height = 5.4f * entity.Get<ScaleComponent>().value.Y;
                    if (enemy.Rarity == Rarity.Unique)
                    {
                        bool engaged = enemy.State != EnemyState.Idle || share < 1f;
                        if (engaged && (position.Xz - session.PlayerPosition).Length < BossRange) DrawBossBar(draw, enemy, share, screen);
                        if (sought && Project(position + Vector3.UnitY * height, screen, out Vector2 over)) DrawSought(draw, over, entity.Entity.Id);
                        continue;
                    }

                    bool marked = enemy.Rarity != Rarity.Normal;
                    if (share >= 1f && !marked && !sought) continue;

                    if (!Project(position + Vector3.UnitY * height, screen, out Vector2 at)) continue;
                    if (sought) DrawSought(draw, new Vector2(at.X, at.Y - (marked ? 22f : 4f)), entity.Entity.Id);

                    float width = marked ? 76f : 52f;
                    GuiRect back = new GuiRect(at.X - width * 0.5f, at.Y, width, 5f);
                    draw.AddRect(back, GuiColors.Rgba(0, 0, 0, 170));
                    draw.AddRect(new GuiRect(back.X, back.Y, back.Width * share, back.Height), Ui.Blood);
                    if (marked) Ui.Text(Context!, draw, enemy.Name, 13f, new GuiRect(back.X - 60f, back.Y - 18f, width + 120f, 16f), Ui.RarityColour(enemy.Rarity), Alignment.Center);
                }
            }
        }

        // Over whoever the land asks to be beaten: a gold point down at them, in the gold of the places on the map, as a line round them says how strong they are and that's another thing. It bobs a little,
        // each out of step with the rest, so that a crowd of them reads as marks and not as a pattern.
        private static void DrawSought(GuiDrawList draw, Vector2 over, uint order)
        {
            const float Size = 9f;
            Vector2 tip = over + new Vector2(0f, -6f + MathF.Sin(Engine.Time * SoughtBob + order * 0.7f) * 2.5f);
            Vector2 top = tip - new Vector2(0f, Size * 1.6f);
            draw.AddLine(tip, top + new Vector2(-Size, 0f), SoughtRim, 5f, roundCaps: true);
            draw.AddLine(tip, top + new Vector2(Size, 0f), SoughtRim, 5f, roundCaps: true);
            draw.AddLine(top + new Vector2(-Size, 0f), top + new Vector2(Size, 0f), SoughtRim, 5f, roundCaps: true);

            // Filled by lines fanned from the tip, as the draw list has no triangles.
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f * 2f - 1f;
                draw.AddLine(tip, top + new Vector2(t * Size, 0f), SoughtGold, 2.2f, roundCaps: true);
            }
        }

        private const float SoughtBob = 3.2f;
        private static readonly Colour SoughtGold = GuiColors.Rgba(255, 190, 70);
        private static readonly Colour SoughtRim = GuiColors.Rgba(30, 18, 6, 230);

        // Everyone else in the world: their name and health over their head, and along the side of the screen wherever they are.
        private void DrawCompanions(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.Client.Party.Count <= 1) return;

            ECSWorld world = Engine.Instance!.world;
            float row = 0f;
            foreach (Archetype archetype in world.QueryArchetypes(HeroMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var hero = entity.Get<HeroComponent>();
                    if (hero.Player == session.Client.PlayerId) continue;

                    var health = entity.Get<HealthComponent>();
                    float share = Math.Clamp(health.Current / MathF.Max(health.Max, 1f), 0f, 1f);

                    GuiRect frame = new GuiRect(Margin, Margin + row, 190f, 34f);
                    Shade(draw, frame);
                    Ui.Text(Context!, draw, $"{hero.Name}, {hero.Title} {hero.Level}", 14f, new GuiRect(frame.X, frame.Y, frame.Width, 18f), Ui.Parchment);
                    draw.AddRect(new GuiRect(frame.X, frame.Y + 21f, frame.Width, 6f), GuiColors.Rgba(0, 0, 0, 170));
                    draw.AddRect(new GuiRect(frame.X, frame.Y + 21f, frame.Width * share, 6f), Ui.Life);
                    row += 46f;

                    Vector3 position = entity.Get<PositionComponent>().value;
                    if (!Project(position + Vector3.UnitY * 5.6f, screen, out Vector2 at)) continue;
                    Ui.Text(Context!, draw, hero.Name, 14f, new GuiRect(at.X - 100f, at.Y - 22f, 200f, 18f), Ui.Parchment, Alignment.Center);
                    GuiRect bar = new GuiRect(at.X - 28f, at.Y - 3f, 56f, 4f);
                    draw.AddRect(bar, GuiColors.Rgba(0, 0, 0, 170));
                    draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * share, bar.Height), Ui.Life);
                }
            }
        }

        // The pools again where the eye already is. It goes by what's drawn and not by the ground, as the hero isn't always standing on it.
        private void DrawOverHero(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (!InterfaceSettings.Load().PoolsOverHero || session.Health <= 0f) return;

            ECSWorld world = Engine.Instance!.world;
            if (!session.Player.IsValid() || !world.TryGetEntityLocation(session.Player, out _, out _)) return;

            Vector3 position = world.GetEntityView(session.Player).Get<PositionComponent>().value;
            if (!Project(position + Vector3.UnitY * OverHead, screen, out Vector2 at)) return;

            // They stand on that point, so that they stay off the head however far out the camera is.
            GuiRect health = new GuiRect(at.X - OverWidth * 0.5f, at.Y - OverMana - 1f - OverHealth, OverWidth, OverHealth);
            DrawPools(draw, session, health, new GuiRect(health.X, health.Bottom + 1f, OverWidth, OverMana));
        }

        /// <summary>Health with Barrier along the top of it, and Mana, wherever they're being shown. The numbers are up to whatever's showing them, as not everything has room.</summary>
        public static void DrawPools(GuiDrawList draw, GameSession session, GuiRect health, GuiRect mana)
        {
            ActorSheet sheet = session.Sheet;

            float life = Math.Clamp(session.Health / sheet.MaxHealth, 0f, 1f);
            draw.AddRect(health, GuiColors.Rgba(0, 0, 0, 170), 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(health.X + 1f, health.Y + 1f, (health.Width - 2f) * life, health.Height - 2f), Ui.Life, 1f);

            // What's owed shows as what's about to go.
            if (session.Debt > 0f)
            {
                float owed = Math.Clamp(session.Debt / sheet.MaxHealth, 0f, life);
                draw.AddRect(new GuiRect(health.X + 1f + (health.Width - 2f) * (life - owed), health.Y + 1f, (health.Width - 2f) * owed, health.Height - 2f), Ui.Owed, 1f);
            }

            if (session.Barrier > 0f)
            {
                float shield = Math.Clamp(session.Barrier / MathF.Max(sheet.MaxBarrier, session.Barrier), 0f, 1f);
                draw.AddRect(new GuiRect(health.X + 1f, health.Y + 1f, (health.Width - 2f) * shield, BarrierHeight), Ui.Barrier, 1f);
            }

            float pool = Math.Clamp(session.Sim.Mana / MathF.Max(sheet.MaxMana, 1f), 0f, 1f);
            draw.AddRect(mana, GuiColors.Rgba(0, 0, 0, 170), 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(mana.X + 1f, mana.Y + 1f, (mana.Width - 2f) * pool, mana.Height - 2f), Ui.Mana, 1f);
        }

        private void DrawConnection(GuiDrawList draw, GameSession session)
        {
            GameClient client = session.Client;
            if (client.Party.Count <= 1) return;

            string text = $"{client.RoundTrip:0} ms";
            if (client.Predictor.Corrections > 0) text += $"   {client.Predictor.Corrections} corrected";
            Ui.Text(Context!, draw, text, 12.5f, new GuiRect(Bounds.Width - 200f - Margin, Bounds.Height - Margin - 16f, 200f, 16f), Ui.Faded, Alignment.End);
        }

        private void DrawBossBar(GuiDrawList draw, in EnemyComponent enemy, float share, GuiRect screen)
        {
            float width = MathF.Min(screen.Width * 0.46f, 620f);
            GuiRect bar = new GuiRect((screen.Width - width) * 0.5f, Margin + 26f, width, 10f);
            Ui.Text(Context!, draw, enemy.Name, 19f, new GuiRect(bar.X, Margin, bar.Width, 24f), Ui.Parchment, Alignment.Center);
            draw.AddRect(bar, GuiColors.Rgba(0, 0, 0, 180), 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(bar.X + 1f, bar.Y + 1f, (bar.Width - 2f) * share, bar.Height - 2f), Ui.Blood, 1f);
        }

        private void DrawFloatingTexts(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            foreach (FloatingText text in session.FloatingTexts)
            {
                if (!Project(text.Position, screen, out Vector2 at)) continue;

                float left = text.Life - text.Age;
                float alpha = Math.Clamp(left / 0.3f, 0f, 1f);
                Colour colour = GuiColors.Rgba(text.Colour.X, text.Colour.Y, text.Colour.Z, alpha);
                at += SlotOffset(text.Slot, text.Size);
                Ui.Text(Context!, draw, text.Text, text.Size, new GuiRect(at.X - 200f, at.Y - 14f, 400f, 28f), colour, Alignment.Center);
            }
        }

        // Where the nth text at one spot sits: the first on the spot, then rows above it, each fanned to either side of the one below,
        // so that two hits landing together read as two numbers.
        private static Vector2 SlotOffset(int slot, float size)
        {
            if (slot == 0) return Vector2.Zero;
            int row = (slot + 1) / 2;
            float side = (slot & 1) == 1 ? -1f : 1f;
            return new Vector2(side * size * (row % 2 == 1 ? 1.4f : 0.7f), -row * size * 1.05f);
        }

        // A mark that circles the hero, pointing the way to the next goal. Where it points comes from the world, projected, so it's
        // right whatever the camera does.
        private void DrawGuidance(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.Guidance is not Vector2 target || session.Health <= 0f) return;

            Vector2 toward = target - session.PlayerPosition;
            float distance = toward.Length;
            if (distance < 12f) return;

            float height = session.World.Height.Sample(session.PlayerPosition);
            Vector3 hero = new Vector3(session.PlayerPosition.X, height, session.PlayerPosition.Y);
            Vector3 ahead = hero + new Vector3(toward.X, 0f, toward.Y) / distance * 12f;
            if (!Project(hero, screen, out Vector2 from) || !Project(ahead, screen, out Vector2 to)) return;

            Vector2 direction = to - from;
            if (direction.LengthSquared < 1f) return;
            direction.Normalize();

            Vector2 normal = new Vector2(-direction.Y, direction.X);
            Vector2 tip = screen.Center + direction * MathF.Min(screen.Width, screen.Height) * 0.34f;
            Vector2 back = tip - direction * 13f;
            draw.AddLine(tip, back + normal * 9f, Ui.Gold, 2.4f);
            draw.AddLine(tip, back - normal * 9f, Ui.Gold, 2.4f);

            string label = distance >= 1000f ? $"{distance / 1000f:0.0} km" : $"{distance:0} m";
            Ui.Text(Context!, draw, label, 13f, new GuiRect(back.X - 60f, back.Y - direction.Y * -14f - 9f, 120f, 18f), Ui.Parchment, Alignment.Center);
        }

        // A call to the old roads fills a bar over the hero's bar, in the colour of a lit gate.
        private void DrawCalling(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.Calling is not float done) return;
            const float Width = 280f, Height = 12f;
            GuiRect bar = new GuiRect((screen.Width - Width) * 0.5f, screen.Height - 210f, Width, Height);
            draw.AddRect(new GuiRect(bar.X - 2f, bar.Y - 2f, bar.Width + 4f, bar.Height + 4f), Ui.Ink, 3f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(bar.X, bar.Y, bar.Width * done, bar.Height), GuiColors.Rgba(104, 206, 255), 2f);
            Ui.Text(Context!, draw, "Calling the waygates", 14f, new GuiRect(bar.X, bar.Y - 22f, bar.Width, 18f), Ui.Parchment, Alignment.Center);
        }

        private void DrawStanding(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            HeroState hero = session.Hero;
            float width = 300f;
            GuiRect experience = new GuiRect(Margin, screen.Height - Margin - 4f, width, 4f);
            GuiRect above = new GuiRect(Margin, experience.Y - 26f, width, 22f);

            DrawBuffs(draw, session, new GuiRect(above.X, above.Y - 21f, width, 18f));
            Shade(draw, above);

            draw.AddRect(experience, GuiColors.Rgba(0, 0, 0, 170));
            float gained = hero.Level >= HeroState.MaxLevel ? 1f : Math.Clamp((float)hero.Experience / hero.ExperienceToNext, 0f, 1f);
            draw.AddRect(new GuiRect(experience.X, experience.Y, experience.Width * gained, experience.Height), Ui.Experience);

            Ui.Text(Context!, draw, $"Level {hero.Level}", 16f, above, Ui.Gold);
            Ui.Text(Context!, draw, $"{hero.Gold} gold", 14f, above, Ui.Parchment, Alignment.End);
            if (hero.UnspentPoints > 0)
            {
                Ui.Text(Context!, draw, $"{hero.UnspentPoints} to spend  [{Bindings.Label(GameAction.Talents)}]", 13f, above, Ui.Gold, Alignment.Center);
            }
        }

        private void DrawBuffs(GuiDrawList draw, GameSession session, GuiRect line)
        {
            float x = line.X;
            foreach (ActiveBuff buff in session.Client.Buffs)
            {
                string name = buff.Stacks > 1 ? $"{buff.Buff.Name} x{buff.Stacks}" : buff.Buff.Name;
                float width = GuiText.MeasureWidth(Context!, name, Ui.Face, 13f);
                GuiRect place = new GuiRect(x, line.Y, width, line.Height);
                Ui.Text(Context!, draw, name, 13f, place, Ui.Good);
                if (Resting(place))
                {
                    ActiveBuff shown = buff;
                    GameUi.Current.Tips.Show(() => Cards.Buff(shown.Buff, session.Sheet, shown.Stacks, shown.Left), place, above: true);
                }
                x += width + 14f;
            }
        }

        // The corner where what the land asks is kept: its name, what's to be done, and under that its level and its omens, each of which a rested eye can read in full. In the Haven, the climb.
        private void DrawChronicle(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            float width = CornerWidth;
            float x = screen.Width - width - Margin;
            float y = Margin;
            float wrap = width - 14f;

            if (session.IsHaven)
            {
                var portal = session.Client.Portal;
                string next = portal is var (name, request, _) ? $"The way is open to {name}, level {request.Level}" : "Choose your next land at the map table";
                List<string> nextLines = Ui.Wrap(Context!, next, 14f, wrap);
                Shade(draw, new GuiRect(x, y, width, 46f + nextLines.Count * 18f));
                Ui.Text(Context!, draw, session.World.Plan.Name, 17f, new GuiRect(x, y, width, 22f), Ui.Gold, Alignment.End);
                y += 26f;
                Ui.Text(Context!, draw, $"Levels up to {session.Hero.LandLevel} are open", 14f, new GuiRect(x, y, width, 18f), Ui.Parchment, Alignment.End);
                y += 20f;
                foreach (string line in nextLines)
                {
                    Ui.Text(Context!, draw, line, 14f, new GuiRect(x, y, width, 18f), portal != null ? Ui.Good : Ui.Faded, Alignment.End);
                    y += 18f;
                }
                return;
            }

            // What's to be done, and then what the land is.
            var objectives = new List<(List<string> lines, Objective objective)>();
            float height = 26f;
            foreach (Objective objective in session.Chronicle.Objectives)
            {
                List<string> lines = Ui.Wrap(Context!, objective.Summary, 14f, wrap);
                objectives.Add((lines, objective));
                height += lines.Count * 18f + 4f;
            }

            string? standing = session.Client.Out ? "You've fallen. You'll go home with the others." : session.Liberated ? "The land is free. Go home through any waygate." : null;
            List<string> standingLines = standing != null ? Ui.Wrap(Context!, standing, 14f, wrap) : new List<string>();
            height += standingLines.Count * 18f + (standingLines.Count > 0 ? 4f : 0f);

            List<List<(OmenDefinition omen, float value, float width)>> omenRows = OmenRows(session, wrap);
            List<UniqueOrders> uniques = session.Terms.Uniques().ToList();
            height += 10f + 20f + omenRows.Count * 18f + uniques.Count * 18f;
            Shade(draw, new GuiRect(x, y, width, height));

            Ui.Text(Context!, draw, session.World.Plan.Name, 17f, new GuiRect(x, y, width, 22f), Ui.Gold, Alignment.End);
            y += 26f;

            foreach ((List<string> lines, Objective objective) in objectives)
            {
                Colour colour = objective.Done ? Ui.Faded : objective.IsMain ? Ui.Parchment : GuiColors.Rgba(206, 194, 170);
                float top = y;
                foreach (string line in lines)
                {
                    Ui.Text(Context!, draw, line, 14f, new GuiRect(x, y, wrap, 18f), colour, Alignment.End);
                    y += 18f;
                }
                draw.AddCircle(new Vector2(x + width - 4f, top + 9f), 3f, objective.Done ? Ui.Good : Ui.Gold);
                y += 4f;
            }

            foreach (string line in standingLines)
            {
                Ui.Text(Context!, draw, line, 14f, new GuiRect(x, y, width, 18f), session.Client.Out ? Ui.Blood : Ui.Good, Alignment.End);
                y += 18f;
            }
            if (standingLines.Count > 0) y += 4f;

            // Under what's to be done: how deep the land is, and what it holds.
            y += 10f;
            draw.AddLine(new Vector2(x + width * 0.35f, y - 5f), new Vector2(x + width, y - 5f), Ui.Gold.MultiplyAlpha(0.35f), 1f);
            int omens = session.Terms.Count;
            Ui.Text(Context!, draw, omens > 0 ? $"Level {session.World.Plan.Level}, with {(omens == 1 ? "one omen" : $"{omens} omens")}" : $"Level {session.World.Plan.Level}", 15f, new GuiRect(x, y, width, 20f), Ui.Parchment, Alignment.End);
            y += 20f;

            foreach (List<(OmenDefinition omen, float value, float width)> row in omenRows)
            {
                float rowWidth = row.Sum(entry => entry.width) + OmenGap * (row.Count - 1);
                float at = x + width - rowWidth;
                foreach ((OmenDefinition omen, float value, float nameWidth) in row)
                {
                    GuiRect place = new GuiRect(at, y, nameWidth, 18f);
                    bool resting = Resting(place);
                    Ui.Text(Context!, draw, omen.Name, 13.5f, place, resting ? Ui.Bright : OmenColour(omen));
                    if (resting)
                    {
                        draw.AddLine(new Vector2(place.X, place.Bottom - 1f), new Vector2(place.Right, place.Bottom - 1f), Ui.Gold.MultiplyAlpha(0.7f), 1f);
                        OmenDefinition shown = omen;
                        float amount = value;
                        GameUi.Current.Tips.Show(() => Cards.Omen(shown, amount, GameSession.Current?.Sheet), place);
                    }
                    at += nameWidth + OmenGap;
                }
                y += 18f;
            }
            foreach (UniqueOrders unique in uniques)
            {
                Ui.Text(Context!, draw, unique.Name, 13.5f, new GuiRect(x, y, width, 18f), Ui.RarityColour(Rarity.Unique), Alignment.End);
                y += 18f;
            }
        }

        // What a land gives reads as the colour of good news, what it does to enemies warm, and what it does to a hero red, so that the worst of a land stands out among the rest of it.
        private static Colour OmenColour(OmenDefinition omen)
        {
            if (omen.IsPrefix) return Ui.Good;
            if (omen.Heroes(1f).Length > 0) return GuiColors.Rgba(214, 120, 104);
            return GuiColors.Rgba(222, 200, 160);
        }

        // The land's omens by name, in as few rows as fit the corner.
        private List<List<(OmenDefinition omen, float value, float width)>> OmenRows(GameSession session, float wrap)
        {
            var rows = new List<List<(OmenDefinition omen, float value, float width)>>();
            var row = new List<(OmenDefinition omen, float value, float width)>();
            float used = 0f;
            foreach ((OmenDefinition omen, float value) in session.Terms.Chosen())
            {
                float nameWidth = GuiText.MeasureWidth(Context!, omen.Name, Ui.Face, 13.5f);
                if (row.Count > 0 && used + OmenGap + nameWidth > wrap)
                {
                    rows.Add(row);
                    row = new List<(OmenDefinition omen, float value, float width)>();
                    used = 0f;
                }
                used += (row.Count > 0 ? OmenGap : 0f) + nameWidth;
                row.Add((omen, value, nameWidth));
            }
            if (row.Count > 0) rows.Add(row);
            return rows;
        }

        // Over each group of training dummies, what's been dealt to it a second, since they were last left alone, and in all.
        private void DrawDummies(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (!session.IsHaven) return;
            double now = Engine.Time;
            Span<Vector3> sum = stackalloc Vector3[DummyMeter.Groups];
            Span<int> count = stackalloc int[DummyMeter.Groups];
            ECSWorld world = Engine.Instance!.world;
            foreach (Archetype archetype in world.QueryArchetypes(DummyMask, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    int group = Math.Clamp(entity.Get<DummyComponent>().Group, 0, DummyMeter.Groups - 1);
                    sum[group] += entity.Get<PositionComponent>().value;
                    count[group]++;
                }
            }

            for (int group = 0; group < DummyMeter.Groups; group++)
            {
                if (count[group] == 0 || !session.Meter.Showing(group, now)) continue;
                Vector3 middle = sum[group] / count[group];
                if (!Project(middle + Vector3.UnitY * 7.5f, screen, out Vector2 at)) continue;

                DummyMeter.Reading reading = session.Meter[group];
                float fade = Math.Clamp((float)(Rulebook.DummyRest * 2f - (now - reading.Last)) / 1.5f, 0f, 1f);
                draw.PushOpacity(fade);
                Ui.Text(Context!, draw, $"{Short(reading.PerSecond)} a second", 22f, new GuiRect(at.X - 160f, at.Y - 26f, 320f, 28f), Ui.Gold, Alignment.Center);
                Ui.Text(Context!, draw, $"{Short(reading.Dealt)} in {reading.Seconds:0.0} s, the biggest blow {Short(reading.Biggest)}", 13.5f, new GuiRect(at.X - 200f, at.Y + 2f, 400f, 18f), Ui.Parchment, Alignment.Center);
                draw.PopOpacity();
            }
        }

        private static string Short(float amount) => amount >= 1e6f ? $"{amount / 1e6f:0.##}m" : amount >= 1e4f ? $"{amount / 1e3f:0.#}k" : $"{amount:0}";

        // A soft dark patch behind text, so it holds up over snow, sand and sunlit walls as well as it does over grass.
        private static void Shade(GuiDrawList draw, GuiRect rect)
        {
            GuiRect patch = new GuiRect(rect.X - 14f, rect.Y - 8f, rect.Width + 28f, rect.Height + 14f);
            draw.AddRect(patch, GuiColors.Rgba(10, 8, 6, 120), 8f, 0f, default, 12f);
        }

        private void DrawLog(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            float y = screen.Height - Margin - 54f;
            for (int i = session.Log.Count - 1; i >= 0; i--)
            {
                LogLine line = session.Log[i];
                float alpha = Math.Clamp((LogLife - line.Age) / 1.5f, 0f, 1f);
                Ui.Text(Context!, draw, line.Text, 14f, new GuiRect(Margin, y - 18f, 460f, 18f), Ui.Parchment.MultiplyAlpha(alpha));
                y -= 19f;
            }
        }

        private void DrawBanner(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.Banners.Count == 0) return;
            Banner banner = session.Banners[0];

            float alpha = MathF.Min(banner.Age / BannerFade, (banner.Life - banner.Age) / BannerFade);
            alpha = Math.Clamp(alpha, 0f, 1f);
            float y = screen.Height * 0.17f;
            float half = MathF.Min(screen.Width * 0.3f, 380f);

            draw.PushOpacity(alpha);
            Ui.Text(Context!, draw, banner.Title, 38f, new GuiRect(0f, y, screen.Width, 46f), Ui.Parchment, Alignment.Center);
            y += 52f;
            draw.AddLine(new Vector2(screen.Width * 0.5f - half, y), new Vector2(screen.Width * 0.5f + half, y), Ui.Gold.MultiplyAlpha(0.7f), 1f);
            y += 8f;
            foreach (string line in Ui.Wrap(Context!, banner.Subtitle, 17f, half * 2f))
            {
                Ui.Text(Context!, draw, line, 17f, new GuiRect(0f, y, screen.Width, 24f), Ui.Parchment, Alignment.Center);
                y += 24f;
            }
            draw.PopOpacity();
        }
    }
}
