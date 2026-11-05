using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.GUI;
using Dino_Engine.GUI.Rendering;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// What is always on screen, kept to the edges so the land stays the picture: health and flasks, the abilities, what the
    /// chronicle asks for next, and the words and numbers that come off the world itself.
    /// </summary>
    internal sealed class Hud : GuiElement
    {
        private const float Margin = 26f;
        private const float BannerFade = 0.7f;
        private const float LogLife = 8f;
        private const float BossRange = 70f;

        private static readonly BitMask EnemyMask = new BitMask(typeof(EnemyComponent), typeof(HealthComponent), typeof(PositionComponent));
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
            DrawCompanions(draw, session, screen);
            DrawFloatingTexts(draw, session, screen);
            DrawGuidance(draw, session, screen);
            DrawVitals(draw, session, screen);
            DrawAbilities(draw, session, screen);
            DrawChronicle(draw, session, screen);
            DrawLog(draw, session, screen);
            DrawPrompt(draw, session, screen);
            DrawBanner(draw, session, screen);
            DrawConnection(draw, session);
        }

        // World to screen, in the same convention the renderer uses (row vectors).
        private static bool Project(Vector3 world, GuiRect screen, out Vector2 point)
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
                        continue;
                    }

                    if (enemy.Rank == EnemyRank.Boss)
                    {
                        bool engaged = enemy.State != EnemyState.Idle || share < 1f;
                        if (engaged && (position.Xz - session.PlayerPosition).Length < BossRange) DrawBossBar(draw, enemy, share, screen);
                        continue;
                    }

                    bool marked = enemy.Rank == EnemyRank.Elite;
                    if (share >= 1f && !marked) continue;

                    float height = 5.4f * entity.Get<ScaleComponent>().value.Y;
                    if (!Project(position + Vector3.UnitY * height, screen, out Vector2 at)) continue;

                    float width = marked ? 76f : 52f;
                    GuiRect back = new GuiRect(at.X - width * 0.5f, at.Y, width, 5f);
                    draw.AddRect(back, GuiColors.Rgba(0, 0, 0, 170));
                    draw.AddRect(new GuiRect(back.X, back.Y, back.Width * share, back.Height), Ui.Blood);
                    if (marked) Ui.Text(Context!, draw, enemy.Name, 13f, new GuiRect(back.X - 60f, back.Y - 18f, width + 120f, 16f), Ui.Gold, Alignment.Center);
                }
            }
        }

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
                Ui.Text(Context!, draw, text.Text, text.Size, new GuiRect(at.X - 200f, at.Y - 14f, 400f, 28f), colour, Alignment.Center);
            }
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

        private void DrawVitals(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            HeroState hero = session.Hero;
            float width = 300f;
            ActorSheet sheet = session.Sheet;
            GuiRect bar = new GuiRect(Margin, screen.Height - Margin - 50f, width, 16f);

            float life = Math.Clamp(session.Health / sheet.MaxHealth, 0f, 1f);
            draw.AddRect(bar, GuiColors.Rgba(0, 0, 0, 170), 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(bar.X + 1f, bar.Y + 1f, (bar.Width - 2f) * life, bar.Height - 2f), Ui.Life, 1f);

            // What's owed shows as what's about to go.
            if (session.Debt > 0f)
            {
                float owed = Math.Clamp(session.Debt / sheet.MaxHealth, 0f, life);
                draw.AddRect(new GuiRect(bar.X + 1f + (bar.Width - 2f) * (life - owed), bar.Y + 1f, (bar.Width - 2f) * owed, bar.Height - 2f), GuiColors.Rgba(70, 12, 12), 1f);
            }

            if (session.Barrier > 0f)
            {
                float shield = Math.Clamp(session.Barrier / MathF.Max(sheet.MaxBarrier, session.Barrier), 0f, 1f);
                draw.AddRect(new GuiRect(bar.X + 1f, bar.Y + 1f, (bar.Width - 2f) * shield, 4f), GuiColors.Rgba(180, 220, 240), 1f);
            }

            string reading = session.Barrier > 0f ? $"{MathF.Max(session.Health, 0f):0} / {sheet.MaxHealth:0}  +{session.Barrier:0}" : $"{MathF.Max(session.Health, 0f):0} / {sheet.MaxHealth:0}";
            Ui.Text(Context!, draw, reading, 12.5f, bar, Ui.Parchment, Alignment.Center);

            GuiRect mana = new GuiRect(bar.X, bar.Bottom + 3f, width, 12f);
            float pool = Math.Clamp(session.Sim.Mana / MathF.Max(sheet.MaxMana, 1f), 0f, 1f);
            draw.AddRect(mana, GuiColors.Rgba(0, 0, 0, 170), 2f, 1f, GuiColors.Rgba(120, 96, 58));
            draw.AddRect(new GuiRect(mana.X + 1f, mana.Y + 1f, (mana.Width - 2f) * pool, mana.Height - 2f), GuiColors.Rgba(44, 84, 170), 1f);
            Ui.Text(Context!, draw, $"{session.Sim.Mana:0} / {sheet.MaxMana:0}", 11f, mana, Ui.Parchment, Alignment.Center);

            DrawBuffs(draw, session, new GuiRect(bar.X, bar.Y - 46f, width, 18f));

            GuiRect experience = new GuiRect(bar.X, mana.Bottom + 4f, width, 4f);
            draw.AddRect(experience, GuiColors.Rgba(0, 0, 0, 170));
            float gained = hero.Level >= HeroState.MaxLevel ? 1f : Math.Clamp((float)hero.Experience / hero.ExperienceToNext, 0f, 1f);
            draw.AddRect(new GuiRect(experience.X, experience.Y, experience.Width * gained, experience.Height), Ui.Experience);

            GuiRect above = new GuiRect(bar.X, bar.Y - 25f, width, 22f);
            Shade(draw, above);
            Ui.Text(Context!, draw, $"Level {hero.Level}", 16f, above, Ui.Gold);
            Ui.Text(Context!, draw, $"{hero.Gold} gold", 14f, above, Ui.Parchment, Alignment.End);
            if (hero.UnspentPoints > 0)
            {
                Ui.Text(Context!, draw, $"{hero.UnspentPoints} to spend  [T]", 13f, new GuiRect(bar.Right + 12f, bar.Y, 160f, 16f), Ui.Gold);
            }
        }

        private void DrawBuffs(GuiDrawList draw, GameSession session, GuiRect line)
        {
            if (session.Client.Buffs.Count == 0) return;
            var names = new List<string>();
            foreach (ActiveBuff buff in session.Client.Buffs) names.Add(buff.Stacks > 1 ? $"{buff.Buff.Name} x{buff.Stacks}" : buff.Buff.Name);
            Ui.Text(Context!, draw, string.Join("   ", names), 13f, line, Ui.Good);
        }

        private readonly record struct Ability(string Key, string Name, float Share, bool Ready);

        private void DrawAbilities(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            var abilities = new List<Ability>(HeroState.SkillSlots + 3);
            HeroSimState sim = session.Sim;
            for (int slot = 0; slot < HeroState.SkillSlots; slot++)
            {
                SkillProfile? skill = session.Client.Kit.Slots[slot];
                string? held = session.Hero.Bar[slot];
                if (skill == null && slot > 2 && held == null) continue;

                // Something on the bar that can't be used right now still shows, dimmed: a bow skill with a sword in hand.
                string name = skill?.Skill.Name ?? (held != null ? Skills.SkillCatalog.Find(held)?.Name ?? "" : "");
                float wait = skill != null && skill.Cooldown > 0f ? sim.Cooldowns[slot] / skill.Cooldown : 0f;
                bool affordable = skill != null && (skill.CostsHealth ? session.Health > skill.Cost : sim.Mana >= skill.Cost);
                abilities.Add(new(SkillPanel.Keys[slot], name, wait, affordable));
            }
            abilities.Add(new("Space", "Dodge", sim.DodgeCooldown / MathF.Max(session.Sheet.DodgeCooldown, 0.1f), true));
            abilities.Add(new("R", $"Flask {session.Hero.Flasks}", 0f, session.Hero.Flasks > 0));
            abilities.Add(new("F", "Wind", session.GuideCooldown / 14f, true));

            const float size = 62f;
            const float gap = 8f;
            float total = abilities.Count * size + (abilities.Count - 1) * gap;
            float x = (screen.Width - total) * 0.5f;
            float y = screen.Height - Margin - size;

            foreach (Ability ability in abilities)
            {
                GuiRect slot = new GuiRect(x, y, size, size);
                draw.AddRect(slot, Ui.Ink, 3f, 1f, GuiColors.Rgba(120, 96, 58));

                float share = Math.Clamp(ability.Share, 0f, 1f);
                if (share > 0f) draw.AddRect(new GuiRect(x, y + size * (1f - share), size, size * share), GuiColors.Rgba(150, 120, 70, 90), 3f);

                Colour colour = !ability.Ready ? Ui.Faded.MultiplyAlpha(0.6f) : share > 0f ? Ui.Faded : Ui.Parchment;
                float nameSize = ability.Name.Length > 8 ? 10.5f : 12.5f;
                Ui.Text(Context!, draw, ability.Name, nameSize, new GuiRect(x, y + 8f, size, 22f), colour, Alignment.Center);
                Ui.Text(Context!, draw, ability.Key, 11.5f, new GuiRect(x, y + size - 22f, size, 18f), Ui.Gold, Alignment.Center);
                x += size + gap;
            }
        }

        private void DrawChronicle(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            float width = 330f;
            float x = screen.Width - width - Margin;
            float y = Margin;

            float height = 26f;
            foreach (Objective objective in session.Chronicle.Objectives)
            {
                height += Ui.Wrap(Context!, objective.Summary, 14f, width - 14f).Count * 18f + 4f;
            }
            Shade(draw, new GuiRect(x, y, width, height));

            Ui.Text(Context!, draw, session.World.Plan.Name, 17f, new GuiRect(x, y, width, 22f), Ui.Gold, Alignment.End);
            y += 26f;

            for (int i = session.Chronicle.Objectives.Count - 1; i >= 0; i--)
            {
                Objective objective = session.Chronicle.Objectives[i];
                Colour colour = objective.Done ? Ui.Faded : objective.IsMain ? Ui.Parchment : GuiColors.Rgba(206, 194, 170);
                foreach (string line in Ui.Wrap(Context!, objective.Summary, 14f, width - 14f))
                {
                    Ui.Text(Context!, draw, line, 14f, new GuiRect(x, y, width - 14f, 18f), colour, Alignment.End);
                    y += 18f;
                }
                draw.AddCircle(new Vector2(x + width - 4f, y - 9f), 3f, objective.Done ? Ui.Good : Ui.Gold);
                y += 4f;
            }
        }

        // A soft dark patch behind text, so it holds up over snow, sand and sunlit walls as well as it does over grass.
        private static void Shade(GuiDrawList draw, GuiRect rect)
        {
            GuiRect patch = new GuiRect(rect.X - 14f, rect.Y - 8f, rect.Width + 28f, rect.Height + 14f);
            draw.AddRect(patch, GuiColors.Rgba(10, 8, 6, 120), 8f, 0f, default, 12f);
        }

        private void DrawLog(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            float y = screen.Height - Margin - 92f;
            for (int i = session.Log.Count - 1; i >= 0; i--)
            {
                LogLine line = session.Log[i];
                float alpha = Math.Clamp((LogLife - line.Age) / 1.5f, 0f, 1f);
                Ui.Text(Context!, draw, line.Text, 14f, new GuiRect(Margin, y - 18f, 460f, 18f), Ui.Parchment.MultiplyAlpha(alpha));
                y -= 19f;
            }
        }

        private void DrawPrompt(GuiDrawList draw, GameSession session, GuiRect screen)
        {
            if (session.InteractPrompt == null) return;
            GuiRect line = new GuiRect(0f, screen.Height - Margin - 92f, screen.Width, 24f);
            Ui.Text(Context!, draw, $"[E]  {session.InteractPrompt}", 17f, line, Ui.Parchment, Alignment.Center);
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
