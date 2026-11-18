using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// The tutorial, the land every new hero starts in (TUTORIAL.md), with a real client on a line: who starts there, that it's the same land every time, that nothing in it is Magic or Rare but its
    /// beast, that nobody can call the waygates there, that falling there is getting up at its landing, that freeing it climbs nothing, and that its waygate is dark until its beast is down and
    /// then goes home.
    /// </summary>
    internal static class TutorialTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 60, Jitter = 8, LossChance = 0.01 };

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;

            public Scene(WorldCache worlds)
            {
                // Arriving where a new hero does, as the game puts them there: no place to start at.
                Lab = new GameLab(worlds, Tutorial.Request, startSite: null);
                Player = Lab.AddPlayer("Newcomer", Line, 1, hero: HeroState.CreateNew());
                Player.Fights = false;
                Lab.Run(5);
            }

            public ServerPlayer Truth => Lab.Server.Heroes[0];
            public ServerSession Session => Lab.Server.Session!;
            public SiteState Lair => Session.Sites.First(state => state.Site.Kind == SiteKind.Lair);

            public IEnumerable<(InteractableComponent Mark, Vector2 At)> Marks()
            {
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype)) yield return (entity.Get<InteractableComponent>(), entity.Get<PositionComponent>().value.Xz);
                }
            }

            public IEnumerable<(EnemyComponent Enemy, Actor Actor)> Enemies()
            {
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(ActorComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype)) yield return (entity.Get<EnemyComponent>(), entity.Get<ActorComponent>().Actor);
                }
            }

            public void Dispose() => Lab.Dispose();
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            worlds.Get(Lands.Haven).Wait();

            WhoStartsThere(report);
            TheSameLand(report);
            using (var scene = new Scene(worlds))
            {
                Arriving(report, scene);
                NoCalling(report, scene);
                FallingAndRising(report, scene);
                WhoHoldsIt(report, scene);
                TheGate(report, scene);
            }
            return report.Finish();
        }

        private static void WhoStartsThere(Report report)
        {
            report.Section("Who starts there");
            HeroState fresh = HeroState.CreateNew();
            report.Check("A new hero starts in the tutorial", Tutorial.Starts(fresh));
            HeroState tried = HeroState.CreateNew();
            tried.Experience = 1;
            report.Check("A hero with anything to show for themselves doesn't", !Tutorial.Starts(tried));
            HeroState climbed = HeroState.CreateNew();
            climbed.BestLand = 1;
            report.Check("Nor one who's freed a land", !Tutorial.Starts(climbed));
        }

        private static void TheSameLand(Report report)
        {
            report.Section("The same land every time");
            WorldData first = new WorldGenerator().Generate(Tutorial.Request);
            WorldData second = new WorldGenerator().Generate(Tutorial.Request);
            report.Check("Grown twice, it's the same", first.Plan.Name == second.Plan.Name && first.Sites.Count == second.Sites.Count
                && first.Sites.Zip(second.Sites).All(pair => pair.First.Kind == pair.Second.Kind && pair.First.Position == pair.Second.Position && pair.First.Spawns.Count == pair.Second.Spawns.Count)
                && first.Haunts.Count == second.Haunts.Count, $"{first.Plan.Name}, {first.Sites.Count} places, {first.Haunts.Count} packs");
            report.Check("It's one way through", first.Plan.Linear);
            report.Check("Smaller than a land", first.Plan.Size < ArchetypeCatalog.Isle.UsualSize.Min, $"{first.Plan.Size:0} m");
            report.Check($"It asks for {TutorialLand.Places.Length} places", first.Sites.Count(Chronicle.Counts) == TutorialLand.Places.Length, $"{first.Sites.Count(Chronicle.Counts)}");
            report.Check("No keep, village, shrine or waygate of its own", !first.Sites.Any(site => site.Kind is SiteKind.Keep or SiteKind.Village or SiteKind.Shrine or SiteKind.Waygate or SiteKind.StandingStones));
            report.Check("Its one waygate is behind its beast", first.Sites.Count(site => site.Spawns.Any(spawn => spawn.Role == SpawnRole.Waygate)) == 1
                && first.Sites.Single(site => site.Kind == SiteKind.Lair).Spawns.Any(spawn => spawn.Role == SpawnRole.Waygate));
        }

        private static void Arriving(Report report, Scene scene)
        {
            report.Section("Arriving");
            report.Check("The hero is in the tutorial", scene.Session.IsTutorial && scene.Player.Client.Session?.IsTutorial == true);
            report.Check("At its landing", (scene.Truth.Sim.Position - scene.Session.World.Sites[0].Landing).Length < 25f, $"{(scene.Truth.Sim.Position - scene.Session.World.Sites[0].Landing).Length:0} m off");
            List<Objective> asks = scene.Session.Chronicle.Objectives;
            report.Check("It asks two things: its places, and its beast", asks.Count == 2 && asks.Any(o => o.Kind == ObjectiveKind.FreePlaces && o.Goal == TutorialLand.Places.Length) && asks.Any(o => o.Kind == ObjectiveKind.SlayWarlord && o.Goal == 1));
            report.Check("Its waygate is dark", !scene.Marks().Any(mark => mark.Mark.Kind == InteractKind.Waygate) && !scene.Lair.Lit);
        }

        private static void NoCalling(Report report, Scene scene)
        {
            report.Section("Calling the waygates");
            report.Check("The client won't", !scene.Player.Client.Session!.CanCall);
            scene.Player.Client.CallRoads();
            scene.Lab.Run(0.5);
            report.Check("And the server doesn't, when asked anyway", scene.Truth.CallingSince == null && scene.Truth.RoadsOpenAt == null);
        }

        private static void FallingAndRising(Report report, Scene scene)
        {
            report.Section("Falling");
            Vector2 landing = scene.Truth.Sim.Position;
            Vector2 away = scene.Session.Sites.First(state => state.Site.Kind == TutorialLand.Places[0].Place.Kind).Site.Position;
            scene.Player.Client.TravelTo(away);
            scene.Lab.Run(1);
            int gold = scene.Truth.Hero.Gold = 50;
            scene.Truth.Actor.Vitals.Health = 0f;
            scene.Lab.Run(1);
            report.Check("Falling there isn't being out of it", scene.Truth.Sim.Down && !scene.Truth.Out && !scene.Session.Lost);
            report.Check("Nor costs any gold", scene.Truth.Hero.Gold == gold, $"{scene.Truth.Hero.Gold} of {gold}");
            report.Check("The death screen offers to rise", scene.Player.Client.Recap?.Haven == true);
            scene.Player.Client.Rise();
            scene.Lab.Run(1);
            report.Check("Rising stands them up whole", !scene.Truth.Sim.Down && scene.Truth.Actor.Vitals.Health >= scene.Truth.Actor.Sheet.MaxHealth);
            report.Check("At its landing", (scene.Truth.Sim.Position - landing).Length < 12f, $"{(scene.Truth.Sim.Position - landing).Length:0} m off");
            report.Check("And the client believes it", (scene.Player.Client.Predictor.Current.Position - scene.Truth.Sim.Position).Length < 1f);
        }

        private static void WhoHoldsIt(Report report, Scene scene)
        {
            report.Section("Who holds it");
            int seen = 0, normal = 0, beasts = 0, rareBeasts = 0;
            var placed = scene.Session.Sites.Where(state => !state.Wild && state.HostilesTotal > 0).ToList();
            foreach (SiteState state in placed)
            {
                scene.Player.Client.TravelTo(state.Site.Position);
                scene.Lab.Run(1.5);
                foreach ((EnemyComponent enemy, Actor actor) in scene.Enemies())
                {
                    if (enemy.Holds - 1 != scene.Session.Sites.IndexOf(state)) continue;
                    seen++;
                    if (actor.Tyrant)
                    {
                        beasts++;
                        if (enemy.Rarity == Rarity.Rare && enemy.Name.EndsWith(Tutorial.Beast.Name)) rareBeasts++;
                    }
                    else if (enemy.Rarity == Rarity.Normal) normal++;
                }
            }
            report.Check("Everyone but its beast is Normal", seen > 1 && normal == seen - beasts, $"{normal} of {seen - beasts}");
            report.Check($"Its beast is one Rare {Tutorial.Beast.Name}, at its lair", beasts == 1 && rareBeasts == 1, $"{beasts} beasts, {rareBeasts} Rare");
        }

        private static void TheGate(Report report, Scene scene)
        {
            report.Section("Its waygate");
            int bestLand = scene.Truth.Hero.BestLand;
            foreach (SiteState state in scene.Session.Sites.Where(state => !state.Wild && state.HostilesTotal > 0 && state.Site.Kind != SiteKind.Lair).ToList()) GameTests.FreePlace(scene.Lab, scene.Truth, state);
            var asked = scene.Session.Sites.Where(state => !state.Wild && Chronicle.Counts(state.Site)).ToList();
            report.Check("Its places are free, and its waygate still dark", asked.All(state => state.Cleared) && !scene.Marks().Any(mark => mark.Mark.Kind == InteractKind.Waygate),
                string.Join(", ", asked.Select(state => $"{state.Site.Name} {state.Dead.Count}/{state.HostilesTotal}")) + $", {scene.Marks().Count(mark => mark.Mark.Kind == InteractKind.Waygate)} gate marks");

            GameTests.FreePlace(scene.Lab, scene.Truth, scene.Lair);
            scene.Lab.Run(1);
            report.Check("Its beast down, the land is free", scene.Lair.Cleared && scene.Session.Liberated);
            (InteractableComponent mark, Vector2 at)? gate = scene.Marks().Where(mark => mark.Mark.Kind == InteractKind.Waygate).Select(mark => ((InteractableComponent, Vector2)?)mark).FirstOrDefault();
            report.Check("And its waygate lit, to be used", gate != null && scene.Lair.Lit, gate?.mark.Prompt ?? "");
            report.Check("There's a chest beside its beast", scene.Lair.Site.Spawns.Any(spawn => spawn.Role == SpawnRole.Chest));
            if (gate is not var (_, spot)) return;

            scene.Player.Client.TravelTo(spot);
            scene.Lab.Run(1.5);
            scene.Player.PressUse = true;
            scene.Lab.Run(8);
            report.Check("Stepping through goes home", scene.Lab.Server.RoomOf(scene.Truth)?.IsHaven == true && scene.Player.Client.Session?.IsHaven == true);
            report.Check("Freeing it climbs nothing", scene.Truth.Hero.BestLand == bestLand && scene.Truth.Hero.LandLevel == Lands.DeepestOpen(scene.Truth.Hero), $"best land {scene.Truth.Hero.BestLand}");
            report.Check("And nobody comes out of it new", !Tutorial.Starts(scene.Truth.Hero), $"level {scene.Truth.Hero.Level}, {scene.Truth.Hero.Experience} experience");
        }
    }
}
