using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Books;
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
        private const float MinionSight = 28f;
        private const float RisenReach = 160f;      // Metres from every hero past which what the Ossuary raised is let go, as it belongs to no place to be kept by.
        private const float RisenAggro = 34f;
        private const float LostLinger = 5f;        // Seconds between the last hero falling and everyone going home, long enough to take in what happened.

        private readonly List<Actor> _nearby = new();
        private readonly List<(double At, Vector2 Where, bool Beast, int Level)> _rising = new();
        private readonly List<Actor> _risen = new();
        private readonly LootBoon _boon;

        public readonly GameServer Server;
        public readonly Combat Combat;
        public readonly GroundLoot Ground;
        public readonly Bestiary Bestiary;
        public readonly List<Actor> Minions = new();
        public bool ObjectivesChanged;

        /// <summary>Seconds of game time.</summary>
        public double Time => Server.Tick * NetTime.TickSecondsPrecise;

        public IReadOnlyList<ServerPlayer> Heroes => Server.Heroes;

        /// <summary>Set while the game is held still, as it can be for someone playing alone.</summary>
        public bool Paused => Server.Paused;

        /// <summary>What the land's omens pay on top of what anything here gives anyway.</summary>
        public LootBoon Boon => _boon;

        /// <summary>What the tyrant's health is multiplied by on top of what every enemy's is, which what it's worth has to take out again.</summary>
        public float TyrantHealth => 1f + Terms.ValueOf(Omens.Wrath) / 100f;

        /// <summary>Where the way out of the Haven leads, once it's been opened at the map table: the land, what it holds, and what it's called.</summary>
        public (WorldRequest Request, LandTerms Terms, string Name)? Portal;

        /// <summary>The way's been opened, or shut, and the portal and everyone's heads-up need to catch up.</summary>
        public bool PortalChanged;

        /// <summary>When everyone has fallen, the moment they all go home.</summary>
        public double? HomeAt;

        /// <summary>Who has fallen here, by name, so that leaving and coming back in isn't a second life.</summary>
        public readonly HashSet<string> Fallen = new();

        public ServerSession(GameServer server, WorldData world, WorldRequest request, LandTerms? terms = null) : base(world, request, terms)
        {
            Server = server;
            Bestiary = new Bestiary(Terms.FoeMods());
            _boon = Terms.Boon();
            Combat = new Combat(this);
            Ground = new GroundLoot(this);
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
            Server.Announce(hero, $"Level {hero.Hero.Level}", $"{hero.Hero.UnspentPoints} talents to spend.");
        }

        /// <summary>A fight shared is a reward shared: everyone close enough to have been in it gets the whole of it.</summary>
        public void ShareExperience(Vector2 position, int amount)
        {
            foreach (ServerPlayer hero in Heroes)
            {
                if ((hero.Sim.Position - position).LengthSquared <= Rulebook.FlaskReach * Rulebook.FlaskReach) GiveExperience(hero, amount);
            }
        }

        // A fight goes on far from any shrine now, so flasks have to come back from fighting.
        private void ChargeFlasks(Vector2 position, Rarity rarity)
        {
            float worth = rarity switch { Rarity.Unique => Rulebook.FlaskFromBoss, Rarity.Rare => Rulebook.FlaskFromRare, _ => 1f };
            foreach (ServerPlayer hero in Heroes)
            {
                if ((hero.Sim.Position - position).LengthSquared > Rulebook.FlaskReach * Rulebook.FlaskReach) continue;
                if (hero.Hero.ChargeFlask(worth)) hero.SummaryChanged = true;
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

                // Home is never found, as it's never been lost: whoever's there has already been told where they are.
                if (IsHaven) continue;
                Server.Broadcast(new SoundEvent { Cue = SoundCue.Discovered }, state.Site.Position);
                Server.Announce(null, state.Site.Name, Chronicle.Describe(state.Site, World), 4.5f);
                foreach (ServerPlayer hero in Heroes) GiveExperience(hero, 6 * World.Plan.Level);
            }

            // A waygate wakes when someone comes near it, and from then on can be travelled to. The one at the landing wakes as heroes step out of it.
            if (IsHaven) return;
            foreach (SiteState state in Sites)
            {
                if (state.Lit || state.Wild || GameServer.WaygateSpawn(World, state.Site) is not Vector2 gate) continue;
                if (DistanceToAnyHero(gate) > Rulebook.GateWake) continue;

                state.Lit = true;
                state.Changed = true;
                if (state.Active) EncounterSystem.LightGate(Server.Ecs, Server, this, state, gate);
                Server.Broadcast(new SoundEvent { Position = new Vector3(gate.X, World.Height.Sample(gate), gate.Y), Cue = SoundCue.Waygate }, gate);
                if (state.Site != World.Sites[0]) Server.Note(null, $"The waygate at {state.Site.Name} is lit.");
            }
        }

        /// <summary>A hero has been struck down, and how hard, for those watching.</summary>
        public void HeroThrown(Actor actor, Vector3 thrown)
        {
            ECSWorld ecs = Server.Ecs;
            if (!actor.Entity.IsValid() || !ecs.TryGetEntityLocation(actor.Entity, out _, out _)) return;
            EntityView view = ecs.GetEntityView(actor.Entity);
            var hero = view.Get<HeroComponent>();
            hero.Thrown = thrown;
            view.Set(hero);
        }

        /// <summary>Someone who isn't a hero has fallen. It stays on the ground a while, and what happens next depends on whose side it was on.</summary>
        public void Fell(Actor actor, Vector3 thrown)
        {
            ECSWorld ecs = Server.Ecs;
            if (!actor.Entity.IsValid() || !ecs.TryGetEntityLocation(actor.Entity, out _, out _)) return;

            EntityView view = ecs.GetEntityView(actor.Entity);
            var enemy = view.Get<EnemyComponent>();
            var brain = view.Get<EnemyBrainComponent>();
            Vector3 position = view.Get<PositionComponent>().value;

            Combat.Withdraw(brain.Mark);
            enemy.State = EnemyState.Dead;
            enemy.Thrown = thrown;
            brain.StateTime = 0f;
            brain.Mark = default;
            view.Set(enemy);
            view.Set(brain);
            Combat.Stop(actor.Entity);
            view.Set(new VelocityComponent());
            view.Set(new HealthComponent { Current = 0f, Max = actor.Sheet.MaxHealth });

            if (actor.Owner != null)
            {
                Minions.Remove(actor);
                return;
            }
            if (_risen.Remove(actor))
            {
                ShareExperience(position.Xz, (int)(brain.Experience * Boon.Experience));
                return;
            }

            if (actor.Site is not SiteState state) return;
            state.Dead.Add(brain.SpawnIndex);
            state.Live.Remove(actor.Entity);

            ShareExperience(position.Xz, (int)(brain.Experience * Boon.Experience));
            ChargeFlasks(position.Xz, enemy.Rarity);
            Combat.DropLoot(enemy, position, actor);

            // Nobody here stays down, on a land whose orders say so. Only once, and never the tyrant.
            if (Terms.Unique() == OrdersCatalog.Writ && !actor.Tyrant) _rising.Add((Time + Rulebook.RiseDelay, position.Xz, actor.Body.StartsWith("dino:"), enemy.Level));

            if (actor.Tyrant) Advance(ObjectiveKind.SlayWarlord);
            OnHostileKilled(state);
        }

        /// <summary>What has to happen every so often that isn't anyone's to do: the fallen getting up, on a land whose orders raise them, and what they raised going when nobody's near.</summary>
        public void Tend()
        {
            for (int i = _rising.Count - 1; i >= 0; i--)
            {
                if (_rising[i].At > Time) continue;
                (_, Vector2 where, bool beast, int level) = _rising[i];
                _rising.RemoveAt(i);
                if (DistanceToAnyHero(where) < RisenReach) Rise(where, beast, level);
            }

            for (int i = _risen.Count - 1; i >= 0; i--)
            {
                Actor risen = _risen[i];
                if (risen.Dead || DistanceToAnyHero(risen.Position) < RisenReach) continue;
                _risen.RemoveAt(i);
                risen.Dead = true;
                if (risen.Entity.IsValid()) Server.Ecs.DestroyEntity(risen.Entity);
            }
        }

        // Bones where something fell, belonging to no place: nothing it does keeps a place held, and nothing about it is kept.
        private void Rise(Vector2 where, bool beast, int level)
        {
            EnemyKind kind = beast ? Bestiary.BoneRaptor : Bestiary.Restless;
            Loadout loadout = Bestiary.Shared(kind, level, Rarity.Normal, Bestiary.NoTraits);
            float height = World.Height.Sample(where);
            string model = Bestiary.ModelOf(kind, new WorldRandom((ulong)Server.Tick));
            float radius = kind.RadiusAt(kind.Scale);
            var actor = new Actor(Team.Enemies, loadout, where, radius) { Body = model, Height = height, Risen = true };
            var enemy = new EnemyComponent { Name = kind.Name, Rarity = Rarity.Normal, Level = level };
            var brain = new EnemyBrainComponent
            {
                SiteIndex = -1,
                SpawnIndex = -1,
                Home = where,
                AggroRadius = RisenAggro,
                LeashRadius = kind.LeashRadius,
                Experience = Bestiary.ExperienceAt(level, kind.Health * (1f - Rulebook.RisenBonesLess / 100f)),
                Abilities = Bestiary.AbilitiesOf(kind, Bestiary.NoTraits),
            };
            var position = new Vector3(where.X, height, where.Y);
            actor.Entity = Server.Ecs.CreateEntity(kind.Name, Bodies.Walking(Server, model, position, kind.Scale, 1.5f, radius, enemy, brain, new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            _risen.Add(actor);
            Server.Broadcast(new SparkEvent { Position = position + Vector3.UnitY * 1.5f, Kind = SparkKind.Shrine, Count = 14, Speed = 5f }, where);
        }

        /// <summary>
        /// A hero has fallen. There's one life to a land, so they're out of it: what it means for their climb is settled at once, so that closing the game can't take it back, and once everyone
        /// has fallen the land is lost and they all go home. Falling once the land is free costs nothing, as it's already been freed.
        /// </summary>
        public void HeroFell(ServerPlayer player)
        {
            if (IsHaven) return;

            player.Out = true;
            Fallen.Add(player.Name);
            Falls++;
            ObjectivesChanged = true;
            Console.WriteLine($"{player.Name} fell in {World.Plan.Name}, level {World.Plan.Level}{(Liberated ? ", which was already free" : "")}.");
            if (!Liberated) Judge(player, freed: false);
            if (SettleIfAllOut()) return;
            if (!Liberated) Server.Note(null, $"{player.Name} has fallen, and is out of this land.");
        }

        /// <summary>
        /// Once everyone here is out of the land, it's lost if it wasn't free, and in a little while they all go home. Asked whenever someone falls, arrives or leaves, as someone still standing
        /// going their own way can leave only the fallen. Says whether it's settled now.
        /// </summary>
        public bool SettleIfAllOut()
        {
            if (IsHaven || HomeAt != null || !Heroes.All(hero => hero.Out)) return false;
            Lost = !Liberated;
            HomeAt = Time + LostLinger;
            ObjectivesChanged = true;
            if (!Lost) return true;
            foreach (ServerPlayer hero in Heroes) Server.Announce(hero, "The land is lost", $"Your next land will be level {hero.Hero.LandLevel}.", LostLinger);
            return true;
        }

        /// <summary>What a land means for someone's climb: freed or lost, from this land, and only ever once.</summary>
        public void Judge(ServerPlayer player, bool freed)
        {
            if (player.Judged || IsHaven) return;
            player.Judged = true;
            if (freed) player.Hero.Freed(World.Plan.Level, Terms.Climb);
            else player.Hero.Lost(Terms.Climb);
            player.SummaryChanged = true;
            Console.WriteLine($"{player.Name} {(freed ? "freed" : "lost")} {World.Plan.Name}: their next land is level {player.Hero.LandLevel}.");
        }

        /// <summary>Everyone is going home through a waygate. Anyone whose land isn't free has lost it.</summary>
        public void Leave()
        {
            foreach (ServerPlayer hero in Heroes) Judge(hero, Liberated);
        }

        /// <summary>Puts someone's minions on their feet, as many as they're allowed, oldest going first.</summary>
        public void Summon(Actor owner, PageProfile page, Vector2 at)
        {
            if (Bestiary.Minion(page.Page.Minion) is not EnemyKind kind) return;
            Loadout loadout = Bestiary.MinionOf(owner.Loadout, owner.Sheet, kind);

            // A new call replaces whoever answered the last one, and there's only room for so many of anyone's.
            int most = Math.Min(page.MinionCount, Rulebook.MostMinions);
            for (int i = Minions.Count - 1; i >= 0; i--)
            {
                if (Minions[i].Owner == owner && Minions[i].MinionOf == page.Page.Minion) Combat.Dismiss(Minions[i]);
            }

            for (int i = 0; i < most; i++)
            {
                float angle = i * MathF.Tau / MathF.Max(most, 1);
                Vector2 spot = at + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 3f;
                if (!World.Passable.IsSet(spot)) spot = at;
                Actor minion = Raise(owner, loadout, kind.Name, kind.Look, spot, kind == Bestiary.Hound ? 0.85f : 1f, kind.AggroRadius);
                minion.MinionLeft = Rulebook.MinionSeconds * owner.Sheet.MinionDuration;
                minion.MinionOf = page.Page.Minion;
            }
        }

        /// <summary>Puts someone somewhere else, for a page that says so. It's only a hero's feet that a page moves, as it's only a hero who's asked it to.</summary>
        public void Carry(Actor actor, Vector2 to)
        {
            if (actor.Player is ServerPlayer hero) Server.Carry(hero, to);
        }

        /// <summary>Stands a fallen enemy back up on its killer's side, with everything it had.</summary>
        public void RaiseFallen(Actor killer, Actor victim)
        {
            Actor risen = Raise(killer, Bestiary.Risen(victim.Loadout, killer.Loadout.Level), "Risen", victim.Body, victim.Position, 1f, 26f);
            risen.MinionLeft = Rulebook.RisenSeconds * killer.Sheet.MinionDuration;
        }

        private Actor Raise(Actor owner, Loadout loadout, string name, string model, Vector2 at, float scale, float aggro)
        {
            ECSWorld ecs = Server.Ecs;
            float height = World.Height.Sample(at);
            var actor = new Actor(Team.Heroes, loadout, at, WorldData.CharacterRadius * scale) { Owner = owner, Body = model, Height = height };

            var enemy = new EnemyComponent { Name = name, Rarity = Rarity.Normal, Level = owner.Loadout.Level, Friendly = true };
            var brain = new EnemyBrainComponent { SiteIndex = -1, SpawnIndex = -1, Home = owner.Position, AggroRadius = aggro, LeashRadius = 45f };
            var position = new Vector3(at.X, height, at.Y);
            actor.Entity = ecs.CreateEntity(name, Bodies.Walking(Server, model, position, scale, 1.5f, enemy, brain, new HealthComponent(loadout.SheetFor(Condition.None).MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
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
            // All of what it is, not just its numbers: a loadout keeps rules and triggers apart, and a buff taking those away would turn off whatever makes it what it is.
            var mods = new List<Mod>(actor.Innate.Mods);
            mods.AddRange(actor.Innate.RuleMods);
            mods.AddRange(actor.Innate.Triggers);
            if (actor.Innate.Granted != Condition.None) mods.Add(M.Grant(actor.Innate.Granted));
            foreach (ActiveBuff buff in actor.Buffs)
            {
                for (int stack = 0; stack < buff.Stacks; stack++) mods.AddRange(buff.Buff.Mods);
            }
            actor.Adopt(actor.Buffs.Count == 0 ? actor.Innate : new Loadout(actor.Innate.Level, actor.Innate.Weapon, mods, actor.Innate.Books));
        }

        /// <summary>A hostile at a site has fallen; if it was the last, the site is free.</summary>
        public void OnHostileKilled(SiteState state)
        {
            state.Changed = true;
            if (state.Cleared || state.Dead.Count < state.HostilesTotal) return;
            state.Cleared = true;
            if (state.Wild) return;

            Server.Note(null, $"{state.Site.Name} is freed.");
            if (Chronicle.Asks(state.Site)) Advance(ObjectiveKind.FreePlaces);
        }

        public void Advance(ObjectiveKind kind)
        {
            foreach (Objective objective in Chronicle.Objectives)
            {
                if (objective.Kind != kind || objective.Done) continue;
                objective.Progress++;
                ObjectivesChanged = true;
                if (!objective.Done) continue;

                foreach (ServerPlayer hero in Heroes)
                {
                    GiveExperience(hero, objective.RewardExperience);
                    hero.Hero.Gold += objective.RewardGold;
                    hero.SummaryChanged = true;
                }
                if (!Chronicle.WorldCleared)
                {
                    foreach (ServerPlayer hero in Heroes) Server.Broadcast(new SoundEvent { Cue = SoundCue.Objective }, hero.Sim.Position);
                    Server.Announce(null, objective.IsMain ? "The tyrant has fallen" : "The land's places are free", objective.Text, 5f);
                }
            }
            if (Chronicle.WorldCleared && !Liberated) Free();
        }

        // Everything the land asked is done. Everyone still standing has freed it, which is a level on their climb, and the way home is open from any waygate.
        private void Free()
        {
            Liberated = true;
            ObjectivesChanged = true;
            foreach (ServerPlayer hero in Heroes)
            {
                if (!hero.Out) Judge(hero, freed: true);
                Server.Broadcast(new SoundEvent { Cue = SoundCue.Liberated }, hero.Sim.Position);
            }
            Server.Announce(null, "The land is free", "Go home through any waygate when you're ready. There's still what it holds, for whoever wants it.", 7f);
        }

        /// <summary>Writes where the world stands into a save. The hero goes in on the player's own machine. The Haven is always the same, so there's nothing to write, and a land that's over is over.</summary>
        public void Capture(SaveGame save)
        {
            save.InLand = !IsHaven && !Lost && HomeAt == null;
            if (!save.InLand) return;

            // The request, not what it became: drawing an archetype is part of what a seed makes, so naming it would make another world.
            save.WorldSeed = Request.Seed;
            save.WorldArchetype = Request.ArchetypeId;
            save.WorldLevel = Request.Level;
            save.Terms = Terms.Copy();

            save.Objectives = Chronicle.Objectives.Select(o => o.Progress).ToList();
            save.Sites.Clear();
            for (int i = 0; i < Sites.Count; i++)
            {
                SiteState state = Sites[i];
                if (!state.Discovered && !state.Cleared && !state.Kindled && !state.Lit && state.Dead.Count == 0 && state.Opened.Count == 0) continue;
                save.Sites.Add(new SiteRecord
                {
                    Index = i,
                    Discovered = state.Discovered,
                    Cleared = state.Cleared,
                    Kindled = state.Kindled,
                    Rewarded = state.Rewarded,
                    Lit = state.Lit,
                    Dead = state.Dead.ToList(),
                    Opened = state.Opened.ToList(),
                });
            }
        }

        /// <summary>Puts a saved land back as it was left.</summary>
        public void Restore(SaveGame save)
        {
            if (IsHaven || !save.InLand) return;
            foreach (SiteRecord record in save.Sites)
            {
                if (record.Index >= Sites.Count) continue;

                // Packs were numbered differently before version 3, and a mark against one of those would be a mark against somebody else.
                if (save.Version < 3 && record.Index >= World.Sites.Count) continue;
                SiteState state = Sites[record.Index];
                state.Discovered = record.Discovered;
                state.Cleared = record.Cleared;
                state.Kindled = record.Kindled;
                state.Rewarded = record.Rewarded;
                state.Lit = record.Lit;
                state.Dead.UnionWith(record.Dead);
                state.Opened.UnionWith(record.Opened);
            }

            for (int i = 0; i < save.Objectives.Count && i < Chronicle.Objectives.Count; i++)
            {
                Objective objective = Chronicle.Objectives[i];
                objective.Progress = save.Objectives[i];

                // A land saved when any place counted is brought up to the places it names now, unless it was already done.
                if (objective.Kind == ObjectiveKind.FreePlaces && !objective.Done) objective.Progress = Sites.Count(state => !state.Wild && state.Cleared && objective.Places.Contains(state.Site));
            }
            Liberated = Chronicle.WorldCleared;
        }
    }
}
