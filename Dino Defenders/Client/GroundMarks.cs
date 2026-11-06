using Dino_Defenders.Gameplay;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.Rendering.Renderers.PosGeometry;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Shows what's going to happen on the ground, and what just has. There's one rule, and everything here keeps it: <b>when the fill reaches the edge, it lands, on exactly what's marked</b>. Time is
    /// read off this player's own clock, the one their hero moves by, as that's the clock the server's decision is made on as far as they're concerned. So a dodge that's in time on screen is in time.
    ///
    /// The other thing that matters is how much there is. A pack can put a dozen of these down at once, and a screen covered in them says nothing: the map they go through merges what overlaps, and this decides how
    /// much each is worth showing. What can reach this hero stands out, and what can't stays quiet. The marks of a hero's own skills are there and gone, as what they need is to see where it went, and that's a glance.
    /// </summary>
    public sealed class GroundMarks
    {
        private const int MostMarks = 96;
        private const float LandFor = 0.2f;         // Seconds a mark stays, once it's landed.
        private const float WithdrawFor = 0.12f;
        private const float ComeIn = 0.07f;
        private const float StrikeFlash = 0.28f;    // How strongly a plain blow shows, which is often and shouldn't get in the way.
        private const float NotForYou = 0.55f;      // How much of itself something shows that can't reach this hero.
        private const float NearEnough = 5f;        // Metres from its edge that a hero is still counted as being in it, as that's where they could be by the time it lands.
        private const float FarOff = 84f;

        private sealed class Mark
        {
            public GroundShape Shape;
            public double Start;
            public double Lands;
            public bool Hostile;
            public MarkTier Tier;
            public uint Ghost;
            public float Withdrawn = -1f;
            public float Strength = 1f;
            public SkillProfile? Skill;     // A hero's own, which goes with them until it leaves their hands.
            public double Leaves;
        }

        private readonly List<Mark> _marks = new();
        private readonly Dictionary<uint, Mark?> _seen = new();     // By serial. Those a hero is already showing themselves have nothing here.
        private readonly HashSet<uint> _live = new();
        private readonly List<uint> _gone = new();
        private readonly BitMask _telegraphs = new BitMask(typeof(TelegraphComponent), typeof(PositionComponent));
        private readonly BitMask _zones = new BitMask(typeof(ZoneComponent), typeof(PositionComponent));
        private double _then;

        /// <summary>What's being shown just now, and when it lands.</summary>
        public IEnumerable<(GroundShape Shape, double Lands)> Showing => _marks.Select(mark => (mark.Shape, mark.Lands));

        /// <summary>How many of those are on their way out.</summary>
        public int Withdrawing => _marks.Count(mark => mark.Withdrawn >= 0f);

        /// <summary>Something this hero has just started, which they can see at once. It's their hands it comes from, so until it leaves them it's wherever they are and whichever way they're aiming.</summary>
        /// <param name="leaves">The tick it leaves their hands.</param>
        /// <param name="lands">The tick it does anything, which is later for what has a way to go.</param>
        public void Own(SkillProfile skill, in HeroSimState hero, double start, double leaves, double lands)
        {
            _marks.Add(new Mark { Shape = GroundShape.Of(skill, hero.Position, hero.Aim, hero.AimReach), Skill = skill, Start = start, Leaves = leaves, Lands = lands, Tier = MarkTier.Plain });
        }

        /// <summary>
        /// Keeps a hero's own marks true. The server lets a blow go from wherever they are when it lands, so that's what has to be shown until then. And if they stop before it does, by a dodge or being stunned, it isn't coming.
        /// </summary>
        public void Follow(in HeroSimState hero, double now, float deltaTime)
        {
            foreach (Mark mark in _marks)
            {
                if (mark.Skill == null || mark.Withdrawn >= 0f || now >= mark.Leaves) continue;
                if (hero.Using < 0) mark.Withdrawn = 0f;
                else mark.Shape = GroundShape.Of(mark.Skill, hero.Position, hero.Aim, hero.AimReach);
            }
        }

        /// <summary>A blow that's already landed: it's only there to show where it went.</summary>
        public void Struck(in GroundShape shape, double now)
        {
            _marks.Add(new Mark { Shape = shape, Start = now - 1.0, Lands = now, Tier = MarkTier.Plain, Strength = StrikeFlash });
        }

        public void Update(ECSWorld world, GameClient client, GameSession session, float deltaTime)
        {
            double now = client.Commands.Tick + client.Commands.Alpha;
            Vector2 hero = session.PlayerPosition;
            GroundMarkRenderer renderer = Engine.RenderEngine._groundMarkRenderer;
            renderer.Focus = new Vector3(hero.X, session.World.Height.Sample(hero), hero.Y);

            if (session.Paused && client.Alone) Hold(now - _then);
            _then = now;

            Read(world, client.PlayerId);
            Follow(client.Predictor.Current, now, deltaTime);

            int drawn = 0;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                Mark mark = _marks[i];

                // Word that it's gone before it landed means it isn't going to: whoever it was has been stopped, and it goes quietly.
                if (mark.Ghost != 0 && mark.Withdrawn < 0f && !_live.Contains(mark.Ghost) && now < mark.Lands - 1.0) mark.Withdrawn = 0f;

                float past = (float)((now - mark.Lands) * NetTime.TickSecondsPrecise);
                if (mark.Withdrawn >= 0f) mark.Withdrawn += deltaTime;
                if (past > LandFor || mark.Withdrawn > WithdrawFor)
                {
                    _marks.RemoveAt(i);
                    continue;
                }

                if (drawn >= MostMarks || (mark.Shape.Origin - hero).LengthSquared > FarOff * FarOff) continue;
                drawn++;
                renderer.SubmitCommand(Command(mark, now, past, hero));
            }

            DrawZones(world, renderer, deltaTime, hero);
        }

        // The world has stood still for this long while the clock went on. The server moves what it's marked along by the same, and what's only here has to be moved by hand.
        private void Hold(double ticks)
        {
            foreach (Mark mark in _marks)
            {
                if (mark.Ghost != 0) continue;
                mark.Start += ticks;
                mark.Leaves += ticks;
                mark.Lands += ticks;
            }
        }

        /// <summary>Picks up what the server has marked, and any change in when it lands.</summary>
        public void Read(ECSWorld world, int player)
        {
            _live.Clear();
            foreach (Archetype archetype in world.QueryArchetypes(_telegraphs, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var telegraph = entity.Get<TelegraphComponent>();
                    _live.Add(telegraph.Serial);
                    if (_seen.TryGetValue(telegraph.Serial, out Mark? known))
                    {
                        if (known == null) continue;
                        known.Start = telegraph.Start;
                        known.Lands = telegraph.Lands;
                        continue;
                    }

                    // Whoever did this has been showing it since they pressed.
                    if (telegraph.Owner != 0 && telegraph.Owner == player)
                    {
                        _seen.Add(telegraph.Serial, null);
                        continue;
                    }

                    var mark = new Mark
                    {
                        Shape = telegraph.ShapeAt(entity.Get<PositionComponent>().value.Xz),
                        Start = telegraph.Start,
                        Lands = telegraph.Lands,
                        Hostile = telegraph.Hostile,
                        Tier = telegraph.Tier,
                        Ghost = telegraph.Serial,
                    };
                    _seen.Add(telegraph.Serial, mark);
                    _marks.Add(mark);
                }
            }

            _gone.Clear();
            foreach (uint serial in _seen.Keys)
            {
                if (!_live.Contains(serial)) _gone.Add(serial);
            }
            foreach (uint serial in _gone) _seen.Remove(serial);
        }

        private static GroundMarkCommand Command(Mark mark, double now, float past, Vector2 hero)
        {
            float strength = mark.Strength * mark.Tier switch { MarkTier.Slight => 0.5f, MarkTier.Plain => 0.74f, _ => 1f };
            if (mark.Hostile && !mark.Shape.Covers(hero, NearEnough)) strength *= NotForYou;

            float span = MathF.Max((float)(mark.Lands - mark.Start), 1f);
            float fill = Math.Clamp((float)(now - mark.Start) / span, 0f, 1f);

            float shown;
            if (mark.Withdrawn >= 0f) shown = 1f - mark.Withdrawn / WithdrawFor;
            else if (past >= 0f)
            {
                // It lands: all at once, as bright as it gets, and then it's gone.
                float left = 1f - past / LandFor;
                shown = 0.4f + 1.2f * left * left;
                fill = 1f;
            }
            else
            {
                float age = (float)((now - mark.Start) * NetTime.TickSecondsPrecise);
                shown = Math.Clamp(age / ComeIn, 0.25f, 1f);
            }

            return new GroundMarkCommand
            {
                Kind = (MarkKind)mark.Shape.Kind,
                Origin = mark.Shape.Origin,
                Facing = mark.Shape.Facing,
                Length = mark.Shape.Length,
                Width = mark.Shape.Width,
                Fill = fill,
                Strength = strength * shown,
                Hostile = mark.Hostile,
            };
        }

        // Ground that's doing something for a while is an outline and not much more, so that it can be seen to be there without being looked at. Ground that wants to hurt you is more insistent.
        private void DrawZones(ECSWorld world, GroundMarkRenderer renderer, float deltaTime, Vector2 hero)
        {
            foreach (Archetype archetype in world.QueryArchetypes(_zones, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var zone = entity.Get<ZoneComponent>();
                    Vector2 at = entity.Get<PositionComponent>().value.Xz;
                    if ((at - hero).LengthSquared > FarOff * FarOff) continue;

                    float left = zone.Life - zone.Age;
                    float shown = Math.Clamp(left / 0.4f, 0f, 1f) * Math.Clamp(zone.Age / 0.2f, 0f, 1f);
                    float strength = (zone.Hostile ? 0.62f : 0.22f) * shown;
                    if (strength <= 0.01f) continue;
                    renderer.SubmitCommand(new GroundMarkCommand { Kind = MarkKind.Circle, Origin = at, Facing = Vector2.UnitY, Length = zone.Radius, Fill = 0f, Strength = strength, Hostile = zone.Hostile });
                }
            }
        }
    }

    /// <summary>What a blow leaves in the air for a moment, which is how anyone can tell where it went. Only for those worth following: a hero, and whatever's dangerous enough to watch.</summary>
    public sealed class WeaponTrails
    {
        private const float Life = 0.2f;

        private sealed class Trail
        {
            public readonly Vector3[] Hilts = new Vector3[TrailRenderer.MostPoints];
            public readonly Vector3[] Tips = new Vector3[TrailRenderer.MostPoints];
            public readonly float[] Born = new float[TrailRenderer.MostPoints];
            public readonly float[] Ages = new float[TrailRenderer.MostPoints];
            public int Count;
            public Vector3 Colour;
        }

        private readonly Dictionary<uint, Trail> _trails = new();
        private readonly List<uint> _finished = new();
        private readonly BitMask _bodies = new BitMask(typeof(ActionComponent), typeof(AnimatorComponent));

        public void Update(ECSWorld world, float deltaTime)
        {
            float now = Engine.Time;
            foreach (Archetype archetype in world.QueryArchetypes(_bodies, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Dino_Engine.Animation.CharacterAnimator animator = entity.Get<AnimatorComponent>().Animator;
                    if (!animator.Swinging || !Worth(entity)) continue;

                    uint id = entity.Entity.Id;
                    if (!_trails.TryGetValue(id, out Trail? trail))
                    {
                        trail = new Trail();
                        _trails.Add(id, trail);
                    }

                    var action = entity.Get<ActionComponent>();
                    Dino_Engine.Util.Colour glow = ClientEffects.GlowOf(SkillCatalog.Named(action.Skill).Look);
                    trail.Colour = new Vector3(glow.Red, glow.Green, glow.Blue);
                    Add(trail, animator.WeaponBase, animator.WeaponTip, now);
                }
            }

            _finished.Clear();
            foreach ((uint id, Trail trail) in _trails)
            {
                while (trail.Count > 0 && now - trail.Born[trail.Count - 1] > Life) trail.Count--;
                if (trail.Count == 0)
                {
                    _finished.Add(id);
                    continue;
                }

                for (int i = 0; i < trail.Count; i++) trail.Ages[i] = (now - trail.Born[i]) / Life;
                Engine.RenderEngine._trailRenderer.SubmitCommand(new TrailRenderCommand { Hilts = trail.Hilts, Tips = trail.Tips, Ages = trail.Ages, Count = trail.Count, Colour = trail.Colour, Strength = 1f });
            }
            foreach (uint id in _finished) _trails.Remove(id);
        }

        private static bool Worth(in EntityView entity)
        {
            if (entity.Has<HeroComponent>()) return true;
            return entity.Has<EnemyComponent>() && entity.Get<EnemyComponent>().Rank != EnemyRank.Normal;
        }

        private static void Add(Trail trail, Vector3 hilt, Vector3 tip, float now)
        {
            int last = Math.Min(trail.Count, TrailRenderer.MostPoints - 1);
            for (int i = last; i > 0; i--)
            {
                trail.Hilts[i] = trail.Hilts[i - 1];
                trail.Tips[i] = trail.Tips[i - 1];
                trail.Born[i] = trail.Born[i - 1];
            }
            trail.Hilts[0] = hilt;
            trail.Tips[0] = tip;
            trail.Born[0] = now;
            trail.Count = last + 1;
        }
    }
}
