using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
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

        public readonly GameServer Server;
        public readonly Combat Combat;
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

        /// <summary>How far the nearest hero is, on their feet or not: for what should be happening around them.</summary>
        public float DistanceToAnyHero(Vector2 position)
        {
            float best = float.PositiveInfinity;
            foreach (ServerPlayer hero in Heroes) best = MathF.Min(best, (hero.Sim.Position - position).Length);
            return best;
        }

        public void GiveExperience(ServerPlayer hero, int amount)
        {
            int levels = hero.Hero.GainExperience(amount);
            hero.SummaryChanged = true;
            if (levels <= 0) return;

            hero.RefreshStats();
            hero.Health = hero.Stats.MaxHealth;
            Server.Announce(hero, $"Level {hero.Hero.Level}", "Three points to spend. Press C.");
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
                Server.Announce(null, state.Site.Name, Chronicle.Describe(state.Site, World), 4.5f);
                foreach (ServerPlayer hero in Heroes) GiveExperience(hero, 6 * World.Plan.Level);
            }
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
