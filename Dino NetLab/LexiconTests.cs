using Dino_Defenders.AssetLexicon;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Lexicon;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// The asset lexicon (LEXICON.md), with a server on the other end of a line: the beta vendor's button takes everyone in from the Haven and from nowhere else, and never while a way to a land
    /// is open; every creature on show is stood where its plot is and sent to whoever comes near; nothing in it is hostile, counted or saved; the index takes the one asking where they asked,
    /// only in the room; and home takes everyone back to the Haven.
    /// </summary>
    internal static class LexiconTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 };
        private static readonly BitMask Shown = new BitMask(typeof(VisualComponent), typeof(PositionComponent));
        private static readonly BitMask Hostile = new BitMask(typeof(EnemyComponent));

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            worlds.Get(Lands.Haven).Wait();

            NotFromALand(report, worlds, new WorldRequest(options.Int("seed", 303), "isle", 1));
            worlds.Get(LexiconWorld.Request).Wait();
            InAndOut(report, worlds);
            NotWithTheWayOpen(report, worlds);
            return report.Finish();
        }

        private static LabPlayer Join(GameLab lab, HeroState? hero = null)
        {
            LabPlayer player = lab.AddPlayer("Hero", Line, 1, hero: hero ?? HeroState.CreateNew());
            player.Fights = false;
            lab.Run(5);
            return player;
        }

        private static void NotFromALand(Report report, WorldCache worlds, WorldRequest land)
        {
            report.Section("Only from the Haven");
            worlds.Get(land).Wait();
            using var lab = new GameLab(worlds, land, null);
            LabPlayer player = Join(lab);
            player.Client.AskLexicon(LexiconAsk.Visit);
            lab.Run(4);
            report.Check("From a land, asking to go in does nothing", !lab.Server.Session!.IsLexicon && player.Arrivals == 1);
            player.Client.AskLexicon(LexiconAsk.Leave);
            Vector2 before = lab.Server.Heroes[0].Sim.Position;
            player.Client.AskLexicon(LexiconAsk.GoTo, before + new Vector2(60f, 0f));
            lab.Run(3);
            report.Check("And nor do going home by it or being carried about by it", !lab.Server.Session!.IsHaven && player.Arrivals == 1 && (lab.Server.Heroes[0].Sim.Position - before).Length < 10f);
        }

        private static void InAndOut(Report report, WorldCache worlds)
        {
            report.Section("In, about and home");
            using var lab = new GameLab(worlds, Lands.Haven, null);
            HeroState hero = HeroState.CreateNew();
            LabPlayer player = Join(lab, hero);
            int climb = player.Client.Hero.BestLand;
            report.Check("Everyone starts in the Haven", lab.Server.Session!.IsHaven && player.Client.Session!.IsHaven && player.Arrivals == 1);

            player.Client.AskLexicon(LexiconAsk.Visit);
            lab.Run(8);
            ServerSession session = lab.Server.Session!;
            report.Check("The beta vendor's button takes everyone into the lexicon", session.IsLexicon && player.Client.Session is { IsLexicon: true } && player.Arrivals == 2);
            report.Check("Which grows the same on both ends", player.Client.Session!.World.Plan.Size == session.World.Plan.Size && player.Client.Session.World.Objects.Count == session.World.Objects.Count);

            LexiconPlan plan = LexiconLayout.Current;
            var creatures = plan.Exhibits.Where(exhibit => exhibit.Exhibit.Kind == LexiconKind.Creature).ToList();
            int stood = 0;
            foreach (PlacedExhibit exhibit in creatures)
            {
                if (Stands(lab.Server.Ecs, exhibit)) stood++;
            }
            report.Check("Every creature on show stands where its plot is", creatures.Count > 0 && stood == creatures.Count, $"{stood} of {creatures.Count}");
            report.Check("Nothing in the room is hostile", Count(lab.Server.Ecs, Hostile) == 0);
            report.Check("And nothing in its places counts as held", session.Sites.All(state => state.HostilesTotal == 0) && session.Chronicle.Objectives.Count == 0);

            var save = new SaveGame();
            session.Capture(save);
            report.Check("It's never saved as somewhere to come back to", !save.InLand);

            PlacedExhibit raptor = plan.Find("creature/raptor.reich") ?? creatures[0];
            player.Client.AskLexicon(LexiconAsk.GoTo, raptor.Viewpoint);
            lab.Run(3);
            Vector2 truth = lab.Server.Heroes[0].Sim.Position, shown = player.Client.Predictor.Current.Position;
            report.Check("The index takes the one asking to stand in front of what they chose", (truth - raptor.Viewpoint).Length < 4f && (shown - truth).Length < 1.5f, $"at {truth.X:0},{truth.Y:0}, asked {raptor.Viewpoint.X:0},{raptor.Viewpoint.Y:0}");
            report.Check("And what's there is sent to them", Stands(player.Ecs, raptor));

            player.Client.AskLexicon(LexiconAsk.GoTo, new Vector2(-500f, float.NaN));
            lab.Run(1);
            report.Check("Nowhere off the room is gone to", (lab.Server.Heroes[0].Sim.Position - truth).Length < 1f);
            player.Client.AskLexicon(LexiconAsk.Visit);
            lab.Run(2);
            report.Check("Asking to go in from inside does nothing", lab.Server.Session!.IsLexicon && player.Arrivals == 2);

            player.Client.AskLexicon(LexiconAsk.Leave);
            lab.Run(6);
            report.Check("Back to the Haven takes everyone home", lab.Server.Session!.IsHaven && player.Client.Session is { IsHaven: true } && player.Arrivals == 3);
            report.Check("And nothing done in the room counted", player.Client.Hero.BestLand == climb && lab.Server.Heroes[0].Hero.BestLand == climb);
            player.Client.AskLexicon(LexiconAsk.GoTo, raptor.Viewpoint);
            lab.Run(2);
            report.Check("Being carried about by it is only for the room", (lab.Server.Heroes[0].Sim.Position - raptor.Viewpoint).Length > 20f);
        }

        // An open way is the Haven's, and what was given for it is gone, so going to the room then would shut it for nothing.
        private static void NotWithTheWayOpen(Report report, WorldCache worlds)
        {
            report.Section("Not with the way open");
            using var lab = new GameLab(worlds, Lands.Haven, null);
            LabPlayer player = Join(lab);
            player.Client.OpenWay(1, 0, Array.Empty<int>());
            lab.Run(2);
            bool open = lab.Server.Session!.Portal != null;
            player.Client.AskLexicon(LexiconAsk.Visit);
            lab.Run(4);
            report.Check("With the way open, the button keeps everyone in the Haven and the way open", open && lab.Server.Session!.IsHaven && lab.Server.Session.Portal != null && player.Arrivals == 1);
        }

        // Whether a world has an exhibit's creature standing on its plot: its look, at its feet.
        private static bool Stands(ECSWorld world, PlacedExhibit exhibit)
        {
            foreach (Archetype archetype in world.QueryArchetypes(Shown, BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (entity.Get<VisualComponent>().Model != exhibit.Exhibit.Look) continue;
                    if ((entity.Get<PositionComponent>().value.Xz - exhibit.Centre).Length < 0.5f) return true;
                }
            }
            return false;
        }

        private static int Count(ECSWorld world, BitMask mask)
        {
            int count = 0;
            foreach (Archetype archetype in world.QueryArchetypes(mask, BitMask.Empty))
            {
                foreach (EntityView _ in new ComponentAccessor(archetype)) count++;
            }
            return count;
        }
    }
}
