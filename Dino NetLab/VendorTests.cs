using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dino_NetLab
{
    /// <summary>
    /// The beta vendor in the Haven, with a server on the other end of a line. A client shows what he has without being sent it and asks for one by which tab, roll and level it's on, and the server makes
    /// the same thing again, so the things to prove are that what lands in the pack is exactly what was shown, at once and on both ends, that he gives nothing anywhere but the Haven, and that
    /// nothing is lost or doubled however bad the line is. Read his part of The Haven in LANDS.md.
    /// </summary>
    internal static class VendorTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 };
        private static readonly NetworkConditions Awful = new NetworkConditions { Latency = 300, Jitter = 150, LossChance = 0.15, DuplicateChance = 0.05, SpikeChance = 0.005, SpikeLength = 500 };
        private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

        private const int Roll = 4242;
        private const int Level = 10;

        private static string Written(Item? item) => item == null ? "" : JsonSerializer.Serialize(item, Json);

        private static string Pack(HeroState hero) => string.Join("|", hero.Inventory.Select(Written));

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;

            public Scene(WorldCache worlds, WorldRequest request, string? startSite, NetworkConditions line)
            {
                Lab = new GameLab(worlds, request, startSite);
                HeroState hero = HeroState.CreateNew();
                Array.Clear(hero.Inventory);
                hero.BestLand = 2;
                Player = Lab.AddPlayer("Hero", line, 1, hero: hero);
                Player.Fights = false;
                Lab.Run(line == Awful ? 10 : 5);
            }

            public ServerPlayer Truth => Lab.Server.Heroes[0];
            public HeroState Shown => Player.Client.Hero;
            public HeroState Real => Truth.Hero;

            /// <summary>Where something that can be used of this kind stands, on the server.</summary>
            public Vector2? Find(InteractKind kind)
            {
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (entity.Get<InteractableComponent>().Kind == kind) return entity.Get<PositionComponent>().value.Xz;
                    }
                }
                return null;
            }

            public void Dispose() => Lab.Dispose();
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            worlds.Get(Lands.Haven).Wait();
            var land = new WorldRequest(options.Int("seed", 303), "isle", 1);
            worlds.Get(land).Wait();

            HisStall(report, worlds);
            Taking(report, worlds);
            OnlyInTheHaven(report, worlds, land);
            ABadLine(report, worlds);
            return report.Finish();
        }

        private static void HisStall(Report report, WorldCache worlds)
        {
            report.Section("His stall");
            using var scene = new Scene(worlds, Lands.Haven, null, Line);
            Vector2? stall = scene.Find(InteractKind.BetaVendor);
            report.Check("He stands in the Haven", stall != null && scene.Lab.Server.Session!.IsHaven);
            if (stall is not Vector2 at) return;

            scene.Player.Client.TravelTo(at);
            scene.Lab.Run(1.5);
            scene.Player.PressUse = true;
            scene.Lab.Run(1.5);
            report.Check("Walking up to him and using him opens his table", scene.Player.PanelsOpened.Any(p => p.kind == InteractKind.BetaVendor));
            report.Check("And not the merchant's", !scene.Player.PanelsOpened.Any(p => p.kind == InteractKind.Merchant));
        }

        private static void Taking(Report report, WorldCache worlds)
        {
            report.Section("Taking from him");
            using var scene = new Scene(worlds, Lands.Haven, null, Line);

            bool atOnce = true, unheard = true, agreed = true, exact = true;
            for (int tab = 0; tab < BetaVendor.Tabs.Length; tab++)
            {
                int index = tab * 3 % BetaVendor.Stock(tab, Roll, Level).Count;
                int cell = tab * 2 + 1;
                string shown = Written(BetaVendor.Offer(tab, Roll, Level, index));
                scene.Player.Client.TakeOffer(tab, Roll, Level, index, cell);
                atOnce &= Written(scene.Shown.Inventory[cell]) == shown;
                unheard &= scene.Real.Inventory[cell] == null;
                scene.Lab.Run(1);
                exact &= Written(scene.Real.Inventory[cell]) == shown;
                agreed &= Pack(scene.Real) == Pack(scene.Shown);
            }
            report.Check("What's taken is in the pack at once, before the server has heard", atOnce && unheard);
            report.Check("And the server puts exactly what was shown in the same cell, from every tab", exact);
            report.Check("So the two end up the same", agreed);

            scene.Player.Client.TakeOffer(0, Roll, Level, 0, 20);
            scene.Player.Client.TakeOffer(0, Roll, Level, 0, 21);
            scene.Lab.Run(1);
            report.Check("The same thing can be taken twice, as another of itself", Written(scene.Real.Inventory[20]) == Written(scene.Real.Inventory[21]) && scene.Real.Inventory[20] != null
                && !ReferenceEquals(scene.Real.Inventory[20], scene.Real.Inventory[21]) && Pack(scene.Real) == Pack(scene.Shown));

            int carried = scene.Real.Carried().Count();
            scene.Player.Client.TakeOffer(BetaVendor.Tabs.Length, Roll, Level, 0);
            scene.Player.Client.TakeOffer(0, Roll, Level, 100000);
            scene.Lab.Run(1);
            report.Check("Nothing that isn't on his table can be asked for", scene.Real.Carried().Count() == carried && Pack(scene.Real) == Pack(scene.Shown));

            for (int i = 0; i < HeroState.InventorySize; i++) scene.Player.Client.TakeOffer(0, Roll, Level, 1, i);
            scene.Lab.Run(1.5);
            bool full = scene.Real.FreeCell() < 0;
            scene.Player.Client.TakeOffer(0, Roll, Level, 2);
            scene.Lab.Run(1);
            report.Check("Into a full pack nothing goes, on either end", full && scene.Real.Carried().Count() == HeroState.InventorySize && Pack(scene.Real) == Pack(scene.Shown));
        }

        private static void OnlyInTheHaven(Report report, WorldCache worlds, WorldRequest land)
        {
            report.Section("Anywhere else");
            using var scene = new Scene(worlds, land, "Landing", Line);
            report.Check("He doesn't stand in a land", scene.Find(InteractKind.BetaVendor) == null && !scene.Lab.Server.Session!.IsHaven);

            scene.Player.Client.TakeOffer(0, Roll, Level, 0, 4);
            report.Check("Asking for something there shows nothing", scene.Shown.Inventory[4] == null);

            // As if the two had fallen out, and the client thought it had been given something: the server's answer puts it right.
            BetaVendor.Take(scene.Shown, 0, Roll, Level, 0, 5);
            scene.Player.Client.TakeOffer(0, Roll, Level, 0, 5);
            scene.Lab.Run(1);
            report.Check("And the server gives nothing, and says so", scene.Real.Carried().Count() == 0 && scene.Shown.Inventory[5] == null && Pack(scene.Real) == Pack(scene.Shown));
        }

        private static void ABadLine(Report report, WorldCache worlds)
        {
            report.Section("A bad line");
            using var scene = new Scene(worlds, Lands.Haven, null, Awful);
            const int Taken = 30;
            for (int i = 0; i < Taken; i++)
            {
                int tab = i % BetaVendor.Tabs.Length;
                scene.Player.Client.TakeOffer(tab, Roll + i, Level + i, i * 7 % BetaVendor.Stock(tab, Roll + i, Level + i).Count);
                scene.Lab.Run(0.05);
            }
            scene.Lab.Run(10);
            report.Check("Everything asked for arrives, once each, however bad the line", scene.Real.Carried().Count() == Taken, $"{scene.Real.Carried().Count()} of {Taken}");
            report.Check("And both ends agree on all of it, to the last number", Pack(scene.Real) == Pack(scene.Shown));
        }
    }
}
