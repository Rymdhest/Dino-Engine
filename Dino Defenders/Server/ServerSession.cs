using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Server
{
    /// <summary>
    /// The world as it truly stands, for one world's length. Everything that decides anything works on this; what players see
    /// comes from here. Nothing in the server may touch the GPU, or anything else belonging to the machine it happens to run on.
    /// </summary>
    public sealed class ServerSession : WorldProgress
    {
        private const float DiscoverRadius = 60f;
        private const float SharedExperienceReach = 80f;
        private const float MinionSight = 28f;

        private readonly List<Actor> _nearby = new();

        public readonly GameServer Server;
        public readonly Combat Combat;
        public readonly Bestiary Bestiary = new();
        public readonly List<Actor> Minions = new();
        public bool ObjectivesChanged;

        /// <summary>Seconds of game time.</summary>
        public double Time => Server.Tick * NetTime.TickSecondsPrecise;

        public IReadOnlyList<ServerPlayer> Heroes => Server.Heroes;

        /// <summary>Set while the game is held still, as it can be for someone playing alone.</summary>
        public bool Paused => Server.Paused;

        public ServerSession(GameServer server, WorldData world, WorldRequest request) : base(world, request)
        {
            Server = server;
            Combat = new Combat(this);
        }

        /// <summary>The nearest hero still on their feet.</summary>
        public ServerPlayer? NearestHero(Vector2 position, out float distance)
        {
            ServerPlayer? nearest = null;
            distance = float.PositiveInfinity;
            foreach (ServerPlayer hero in Heroes)
            {
                if (hero.Sim.Down) continue;
                float away = (hero.Sim.Position - position).Length;
                if (away >= distance) continue;
                distance = away;
                nearest = hero;
            }
            return nearest;
        }

        /// <summary>The nearest thing on the other side from someone. Enemies look for heroes and what's with them; minions look around themselves.</summary>
        public Actor? NearestFoe(Actor actor, out float distance)
        {
            Actor? nearest = null;
            distance = float.PositiveInfinity;

            if (actor.Team == Team.Enemies)
            {
                foreach (ServerPlayer hero in Heroes)
                {
                    if (hero.Sim.Down) continue;
                    float away = (hero.Sim.Position - actor.Position).Length;
                    if (away >= distance) continue;
                    distance = away;
                    nearest = hero.Actor;
                }

                foreach (Actor minion in Minions)
                {
                    if (minion.Dead) continue;
                    float away = (minion.Position - actor.Position).Length;
                    if (away >= distance) continue;
                    distance = away;
                    nearest = minion;
                }
                return nearest;
            }

            _nearby.Clear();
            Combat.Grid.Foes(actor.Team, actor.Position, MinionSight, _nearby);
            foreach (Actor foe in _nearby)
            {
                float away = (foe.Position - actor.Position).Length;
                if (away >= distance) continue;
                distance = away;
                nearest = foe;
            }
            return nearest;
        }

        /// <summary>How far the nearest hero is, on their feet or not: for what should be happening around them.</summary>
        public float DistanceToAnyHero(Vector2 position)
        {
            float best = float.PositiveInfinity;
            foreach (ServerPlayer hero in Heroes) best = MathF.Min(best, (hero.Sim.Position - position).Length);
            return best;
        }

        public void GiveExperience(ServerPlayer hero, int amount)
        {
            int levels = hero.Hero.GainExperience((int)(amount * (1f + hero.Actor.Sheet.ExperienceGain / 100f)));
            hero.SummaryChanged = true;
            if (levels <= 0) return;

            hero.RefreshLoadout();
            hero.Actor.Vitals = Vitals.Full(hero.Actor.Sheet);
            Server.Broadcast(new SoundEvent { Position = Server.HeroPosition(hero), Cue = SoundCue.LevelUp }, hero.Sim.Position);
            Server.Announce(hero, $"Level {hero.Hero.Level}", $"{hero.Hero.UnspentPoints} talents to spend. Press T.");
        }

        /// <summary>A fight shared is a reward shared: everyone close enough to have been in it gets the whole of it.</summary>
        public void ShareExperience(Vector2 position, int amount)
        {
            foreach (ServerPlayer hero in Heroes)
            {
                if ((hero.Sim.Position - position).LengthSquared <= SharedExperienceReach * SharedExperienceReach) GiveExperience(hero, amount);
            }
        }

        public void UpdateDiscovery()
        {
            foreach (SiteState state in Sites)
            {
                if (state.Discovered || state.Wild) continue;
                float reach = state.Site.Radius + DiscoverRadius;
                if (DistanceToAnyHero(state.Site.Position) > reach) continue;

                state.Discovered = true;
                state.Changed = true;
                Server.Broadcast(new SoundEvent { Cue = SoundCue.Discovered }, state.Site.Position);
                Server.Announce(null, state.Site.Name, Chronicle.Describe(state.Site, World), 4.5f);
                foreach (ServerPlayer hero in Heroes) GiveExperience(hero, 6 * World.Plan.Level);
            }
        }

        /// <summary>Someone who isn't a hero has fallen. It stays on the ground a while, and what happens next depends on whose side it was on.</summary>
        public void Fell(Actor actor, Vector2 from)
        {
            ECSWorld ecs = Server.Ecs;
            if (!actor.Entity.IsValid() || !ecs.TryGetEntityLocation(actor.Entity, out _, out _)) return;

            EntityView view = ecs.GetEntityView(actor.Entity);
            var enemy = view.Get<EnemyComponent>();
            var brain = view.Get<EnemyBrainComponent>();
            Vector3 position = view.Get<PositionComponent>().value;

            Vector2 away = position.Xz - from;
            away = away.LengthSquared > 0.01f ? away.Normalized() : Vector2.UnitX;
            enemy.State = EnemyState.Dead;
            enemy.FallAxis = new Vector3(away.Y, 0f, -away.X);
            brain.StateTime = 0f;
            view.Set(enemy);
            view.Set(brain);
            view.Set(new VelocityComponent());
            view.Set(new HealthComponent { Current = 0f, Max = actor.Sheet.MaxHealth });

            if (actor.Owner != null)
            {
                Minions.Remove(actor);
                return;
            }

            if (actor.Site is not SiteState state) return;
            state.Dead.Add(brain.SpawnIndex);
            state.Live.Remove(actor.Entity);

            ShareExperience(position.Xz, brain.Experience);
            Combat.DropLoot(enemy, position, actor);
            OnHostileKilled(state);
        }

        /// <summary>Puts someone's minions on their feet, as many as they're allowed, oldest going first.</summary>
        public void Summon(Actor owner, SkillProfile skill, Vector2 at)
        {
            if (Bestiary.Minion(skill.Skill.Minion) is not EnemyKind kind) return;
            Loadout loadout = Bestiary.MinionOf(owner.Loadout, owner.Sheet, kind);

            // A new call replaces whoever answered the last one.
            for (int i = Minions.Count - 1; i >= 0; i--)
            {
                if (Minions[i].Owner == owner && Minions[i].MinionOf == skill.Skill) Combat.Dismiss(Minions[i]);
            }

            for (int i = 0; i < skill.MinionCount; i++)
            {
                float angle = i * MathF.Tau / MathF.Max(skill.MinionCount, 1);
                Vector2 spot = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3f;
                if (!World.Passable.IsSet(spot)) spot = at;
                bool hound = kind == Bestiary.Hound;
                Actor minion = Raise(owner, loadout, kind.Name, "skeleton", spot, hound ? 0.72f : 1f, kind.AggroRadius);
                minion.MinionLeft = skill.Duration * owner.Sheet.MinionDuration;
                minion.MinionOf = skill.Skill;
            }
        }

        /// <summary>Stands a fallen enemy back up on its killer's side, with everything it had.</summary>
        public void RaiseFallen(Actor killer, Actor victim)
        {
            Actor risen = Raise(killer, Bestiary.Risen(victim.Loadout, killer.Loadout.Level), "Risen", victim.Body, victim.Position, 1f, 26f);
            risen.MinionLeft = 20f * killer.Sheet.MinionDuration;
        }

        private Actor Raise(Actor owner, Loadout loadout, string name, string model, Vector2 at, float scale, float aggro)
        {
            ECSWorld ecs = Server.Ecs;
            float height = World.Height.Sample(at);
            var actor = new Actor(Team.Heroes, loadout, at, WorldData.CharacterRadius * scale) { Owner = owner, Body = model, Height = height };

            var enemy = new EnemyComponent { Name = name, Rank = EnemyRank.Normal, Level = owner.Loadout.Level, Friendly = true };
            var brain = new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = owner.Position, AggroRadius = aggro, LeashRadius = 45f };
            var position = new Vector3(at.X, height, at.Y);
            actor.Entity = ecs.CreateEntity(name, Bodies.Walking(Server, model, position, scale, 1.5f, enemy, brain, new HealthComponent(loadout.SheetFor(Condition.None).MaxHealth), new SwingComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            Minions.Add(actor);
            return actor;
        }

        /// <summary>Something an actor has has changed, so it needs new numbers.</summary>
        public void Rebuild(Actor actor)
        {
            actor.LoadoutChanged = false;
            if (actor.Player is ServerPlayer hero)
            {
                hero.RefreshLoadout();
                hero.BuffsChanged = true;
                return;
            }

            // Anyone else has to stop sharing what they've been sharing, for as long as it lasts.
            var mods = new List<Mod>(actor.Innate.Mods);
            foreach (ActiveBuff buff in actor.Buffs)
            {
                for (int stack = 0; stack < buff.Stacks; stack++) mods.AddRange(buff.Buff.Mods);
            }
            actor.Adopt(actor.Buffs.Count == 0 ? actor.Innate : new Loadout(actor.Innate.Level, actor.Innate.Weapon, mods, actor.Innate.Skills));
        }

        /// <summary>A hostile at a site has fallen; if it was the last, the site is free.</summary>
        public void OnHostileKilled(SiteState state)
        {
            state.Changed = true;
            if (state.Cleared || state.Dead.Count < state.HostilesTotal) return;
            state.Cleared = true;
            if (state.Wild) return;

            Server.Note(null, $"{state.Site.Name} is cleared.");
            Advance(state.Site.Kind == SiteKind.Keep ? ObjectiveKind.SlayWarlord : ObjectiveKind.ClearSites, state.Site.Kind);
            MarkFreedVillages(state);
        }

        private void MarkFreedVillages(SiteState cleared)
        {
            foreach (SiteState state in Sites)
            {
                if (state.ThreatIndex < 0 || Sites[state.ThreatIndex] != cleared) continue;
                if (state.Site.Definition.Kind is SiteKind.Village or SiteKind.Hamlet)
                {
                    Server.Note(null, $"{state.Site.Name} breathes easier.");
                }
            }
        }

        public void Advance(ObjectiveKind kind, SiteKind site)
        {
            foreach (Objective objective in Chronicle.Objectives)
            {
                if (objective.Kind != kind || objective.Site != site || objective.Done) continue;
                objective.Progress++;
                ObjectivesChanged = true;
                if (!objective.Done) continue;

                foreach (ServerPlayer hero in Heroes)
                {
                    GiveExperience(hero, objective.RewardExperience);
                    hero.Hero.Gold += objective.RewardGold;
                    hero.SummaryChanged = true;
                }
                foreach (ServerPlayer hero in Heroes) Server.Broadcast(new SoundEvent { Cue = objective.IsMain ? SoundCue.Liberated : SoundCue.Objective }, hero.Sim.Position);
                Server.Announce(null, objective.IsMain ? "The land is free" : "Chronicle fulfilled", objective.Text, 5f);

                if (objective.IsMain)
                {
                    Liberated = true;
                    foreach (ServerPlayer hero in Heroes)
                    {
                        hero.Hero.WorldsCleared++;
                        hero.SummaryChanged = true;
                    }
                    Server.Note(null, "The waygate will reach further now.");
                }
            }
        }

        /// <summary>Writes where the world stands into a save. The hero goes in on the player's own machine.</summary>
        public void Capture(SaveGame save)
        {
            // The request, not what it became: drawing an archetype is part of what a seed makes, so naming it would make another world.
            save.WorldSeed = Request.Seed;
            save.WorldArchetype = Request.ArchetypeId;
            save.WorldLevel = Request.Level;

            save.Objectives = Chronicle.Objectives.Select(o => o.Progress).ToList();
            save.Sites.Clear();
            for (int i = 0; i < Sites.Count; i++)
            {
                SiteState state = Sites[i];
                if (!state.Discovered && !state.Cleared && !state.Kindled && state.Dead.Count == 0 && state.Opened.Count == 0) continue;
                save.Sites.Add(new SiteRecord
                {
                    Index = i,
                    Discovered = state.Discovered,
                    Cleared = state.Cleared,
                    Kindled = state.Kindled,
                    Rewarded = state.Rewarded,
                    Dead = state.Dead.ToList(),
                    Opened = state.Opened.ToList(),
                });
            }
        }

        /// <summary>Puts a saved world back as it was left.</summary>
        public void Restore(SaveGame save)
        {
            foreach (SiteRecord record in save.Sites)
            {
                if (record.Index >= Sites.Count) continue;
                SiteState state = Sites[record.Index];
                state.Discovered = record.Discovered;
                state.Cleared = record.Cleared;
                state.Kindled = record.Kindled;
                state.Rewarded = record.Rewarded;
                state.Dead.UnionWith(record.Dead);
                state.Opened.UnionWith(record.Opened);
            }

            for (int i = 0; i < save.Objectives.Count && i < Chronicle.Objectives.Count; i++)
            {
                Chronicle.Objectives[i].Progress = save.Objectives[i];
            }

            Liberated = Chronicle.WorldCleared;
        }
    }
}
