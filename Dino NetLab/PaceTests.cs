using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// How playing goes, as near as a program can say: a real client walks a hero from the landing on foot, from each pack to the nearest one left, and this
    /// notes how much of the time goes on fighting. It's a plain hero who knows no tricks and spends no talents, so it's the floor of what a player can do
    /// rather than an estimate of it.
    /// </summary>
    internal static class PaceTests
    {
        private const float FightReach = 25f;

        // A little way along the way to whatever's nearest and still there.
        private static Vector2? NextStep(ServerSession session, Vector2 from, WorldPathfinder paths)
        {
            SiteState? nearest = null;
            float best = float.PositiveInfinity;
            foreach (SiteState state in session.Sites)
            {
                if (state.Cleared || state.HostilesTotal == 0) continue;
                float distance = (state.Site.Position - from).LengthSquared;
                if (distance >= best) continue;
                best = distance;
                nearest = state;
            }
            if (nearest == null) return null;

            Vector2 goal = nearest.Site.Position;
            if (WorldPathfinder.HasClearLine(session.World, from, goal)) return goal;
            List<Vector2>? path = paths.Find(session.World, from, goal);
            return path == null || path.Count == 0 ? goal : path[Math.Min(1, path.Count - 1)];
        }

        public static int Run(Options options)
        {
            int seed = options.Int("seed", 303);
            int level = options.Int("level", 1);
            int heroLevel = options.Int("hero", 1);
            int minutes = options.Int("minutes", 5);
            var request = new WorldRequest(seed, options.Text("archetype") ?? "isle", level);

            // A land's own omens as it would roll them, or those asked for, each as much as the land's level would have it.
            LandTerms terms = options.Text("rolled") != null ? Lands.Roll(level, new WorldRandom((ulong)seed)) : new LandTerms();
            if (options.Text("omens") is string omens)
            {
                foreach (string id in omens.Split(','))
                {
                    if (Omens.Find(id) is OmenDefinition omen) terms.Omens.Add(new ItemLine(omen.Id, omen.Fixed(level)));
                }
            }
            terms.Sanitize();

            var worlds = new WorldCache();
            using var lab = new GameLab(worlds, request, "Landing", terms);
            HeroState hero = options.Text("strong") != null ? GameLab.StrongHero() : HeroState.CreateNew();
            hero.Level = Math.Max(hero.Level, heroLevel);
            LabPlayer player = lab.AddPlayer("Walker", new NetworkConditions { Latency = 30, Jitter = 5 }, 11, hero: hero);
            lab.Run(3);
            lab.TickCostWorst = 0.0;    // What's left is what happens in play. The first ticks of a process are mostly the runtime finding its feet.

            ServerSession session = lab.Server.Session!;
            ServerPlayer truth = lab.Server.Players.First();
            var paths = new WorldPathfinder();
            var luck = new Random(3);
            Vector2 lastSpot = default;
            int sinceRoute = 0;
            int sidestep = 0;

            Console.WriteLine($"Land level {level}, hero level {heroLevel}, on foot from pack to pack{(terms.Count > 0 ? $", with {string.Join(", ", terms.Chosen().Select(chosen => chosen.Omen.Name))}" : "")}");
            Console.WriteLine("minute  level  kills  /min  in a fight  falls  flasks  health  walked");

            var near = new List<Actor>();
            int falls = 0;
            bool wasDown = false;
            int lastKills = 0;
            int startKills = 0;
            for (int minute = 1; minute <= minutes && falls == 0; minute++)
            {
                int ticks = 0;
                int fighting = 0;
                lab.Run(60, () =>
                {
                    ticks++;
                    if (++sinceRoute >= 12)
                    {
                        sinceRoute = 0;
                        Vector2 here = truth.Sim.Position;
                        bool stuck = (here - lastSpot).LengthSquared < 0.25f && !truth.Sim.Down;
                        lastSpot = here;

                        // Paths are coarse, and a fence can be thinner than a step of one. Anybody at the keyboard just walks round it.
                        if (stuck) sidestep = 8;
                        if (sidestep > 0 && sidestep-- == 8) player.Heading = here + new Vector2(luck.NextSingle() - 0.5f, luck.NextSingle() - 0.5f) * 30f;
                        else if (sidestep == 0) player.Heading = NextStep(session, here, paths);
                    }

                    near.Clear();
                    session.Combat.Grid.Foes(Team.Heroes, truth.Sim.Position, FightReach, near);
                    if (near.Count > 0) fighting++;
                    if (truth.Sim.Down && !wasDown) falls++;
                    wasDown = truth.Sim.Down;
                });

                int kills = session.Sites.Sum(s => s.Dead.Count) - startKills;
                float walked = (truth.Sim.Position - session.World.Sites[0].Position).Length;
                Console.WriteLine($"{minute,6} {truth.Hero.Level,6} {kills,6} {kills - lastKills,5} {(float)fighting / ticks,11:0%} {falls,6} {truth.Hero.Flasks,7} {truth.Actor.Vitals.Health / truth.Actor.Sheet.MaxHealth,7:0%} {walked,6:0}m");
                lastKills = kills;
                if (falls > 0) Console.WriteLine("Fell, and a land has one life: that's as far as this one goes.");
                if (options.Text("trace") != null)
                {
                    var next = session.Sites.Where(s => !s.Cleared && s.HostilesTotal > 0).OrderBy(s => (s.Site.Position - truth.Sim.Position).LengthSquared).First();
                    Console.WriteLine($"   at {truth.Sim.Position} heading {player.Heading}; next {next.Site.Kind}/{next.Site.Haunt} at {next.Site.Position} r{next.Site.Radius:0}, {next.Dead.Count}/{next.HostilesTotal} dead, active {next.Active}, live {next.Live.Count}, passable here {session.World.Passable.IsSet(truth.Sim.Position)}, goal {session.World.Passable.IsSet(next.Site.Position)}");
                }
            }

            // It never stops to pick anything up, so all it's dropped is still lying where it fell.
            int Dropped(Rarity rarity) => session.Ground.All.Count(lying => lying.item.Rarity == rarity);
            int good = Dropped(Rarity.Rare) + Dropped(Rarity.Unique);
            Console.WriteLine($"Dropped {Dropped(Rarity.Normal)} normal, {Dropped(Rarity.Magic)} magic, {Dropped(Rarity.Rare)} rare, {Dropped(Rarity.Unique)} unique: something good every {minutes * 60f / Math.Max(good, 1):0} s, or {lastKills / (float)Math.Max(good, 1):0} kills.");
            Console.WriteLine($"Gold {truth.Hero.Gold}, carrying {truth.Hero.Carried().Count()} items. Server: {lab.TickCostTotal / Math.Max(lab.TicksTimed, 1):0.00} ms a tick on average, worst {lab.TickCostWorst:0.0} ms.");
            return 0;
        }
    }
}
