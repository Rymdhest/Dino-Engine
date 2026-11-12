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
    /// The Forge's crafting bench, with a server on the other end of a line (<see cref="Crafting"/>). A fuse is made on the client at once and again on the server, which has the last word, so what has to be
    /// proved is that both make exactly the same thing, take the same gold, only in the Haven, and that nothing is lost or doubled on a bad line. Read HALLS.md.
    /// </summary>
    internal static class CraftingTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 };
        private static readonly NetworkConditions Awful = new NetworkConditions { Latency = 300, Jitter = 150, LossChance = 0.15, DuplicateChance = 0.05, SpikeChance = 0.005, SpikeLength = 500 };
        private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

        private const int Pairs = 8;
        private const int Gold = 1_000_000;

        private static string Written(Item? item) => item == null ? "" : JsonSerializer.Serialize(item, Json);

        private static string Pack(HeroState hero) => string.Join("|", hero.Inventory.Select(Written));

        // Pairs that fuse, the first of each in an even cell and its second in the next, of every sort round in turn.
        private static HeroState Hero()
        {
            HeroState hero = HeroState.CreateNew();
            Array.Clear(hero.Inventory);
            hero.BestLand = 2;
            hero.Gold = Gold;
            var random = new WorldRandom(31337);
            ItemSlot[] sorts = { ItemSlot.Head, ItemSlot.Body, ItemSlot.Finger, ItemSlot.Book, ItemSlot.Sentence, ItemSlot.Feet, ItemSlot.Neck, ItemSlot.Hands };
            for (int p = 0; p < Pairs; p++)
            {
                ItemSlot slot = sorts[p % sorts.Length];
                Item Make() => slot >= ItemSlot.Book ? ItemGenerator.Folio(20 + p, random, Rarity.Magic, slot) : ItemGenerator.Forge(20 + p, random, Rarity.Magic, 0f, slot);
                Item first = Make(), second = Make();
                while (Crafting.WhyNot(first, second) != null) second = Make();
                hero.Inventory[p * 2] = first;
                hero.Inventory[p * 2 + 1] = second;
            }
            return hero;
        }

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;

            public Scene(WorldCache worlds, WorldRequest request, string? startSite, NetworkConditions line)
            {
                Lab = new GameLab(worlds, request, startSite);
                Player = Lab.AddPlayer("Hero", line, 1, hero: Hero());
                Player.Fights = false;
                Lab.Run(line == Awful ? 10 : 5);
            }

            public ServerPlayer Truth => Lab.Server.Heroes[0];
            public HeroState Shown => Player.Client.Hero;
            public HeroState Real => Truth.Hero;

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

            TheBench(report, worlds);
            Fusing(report, worlds);
            OnlyInTheHaven(report, worlds, land);
            ABadLine(report, worlds);
            return report.Finish();
        }

        private static void TheBench(Report report, WorldCache worlds)
        {
            report.Section("The bench");
            using var scene = new Scene(worlds, Lands.Haven, null, Line);
            Vector2? bench = scene.Find(InteractKind.Workbench);
            report.Check("It stands in the Haven", bench != null);
            if (bench is not Vector2 at) return;

            scene.Player.Client.TravelTo(at);
            scene.Lab.Run(1.5);
            scene.Player.PressUse = true;
            scene.Lab.Run(1.5);
            report.Check("Walking up to it and using it opens it", scene.Player.PanelsOpened.Any(p => p.kind == InteractKind.Workbench));
        }

        private static void Fusing(Report report, WorldCache worlds)
        {
            report.Section("Fusing");
            using var scene = new Scene(worlds, Lands.Haven, null, Line);

            bool atOnce = true, unheard = true, exact = true, agreed = true, paid = true;
            for (int p = 0; p < 4; p++)
            {
                int first = p * 2, second = p * 2 + 1;
                Item expected = Crafting.Fused(scene.Shown.Inventory[first]!, scene.Shown.Inventory[second]!);
                int gold = scene.Real.Gold - Crafting.FuseCost(scene.Real.Inventory[first]!);
                scene.Player.Client.Fuse(first, second);
                atOnce &= Written(scene.Shown.Inventory[first]) == Written(expected) && scene.Shown.Inventory[second] == null && scene.Shown.Gold == gold;
                unheard &= scene.Real.Inventory[second] != null;
                scene.Lab.Run(1);
                exact &= Written(scene.Real.Inventory[first]) == Written(expected) && scene.Real.Inventory[second] == null;
                paid &= scene.Real.Gold == gold && scene.Shown.Gold == gold;
                agreed &= Pack(scene.Real) == Pack(scene.Shown);
            }
            report.Check("What's made is in the pack at once, before the server has heard", atOnce && unheard);
            report.Check("And the server makes exactly the same thing in the same cell", exact);
            report.Check("And takes the same gold", paid);
            report.Check("So the two end up the same", agreed);

            string before = Pack(scene.Real);
            int goldBefore = scene.Real.Gold;
            scene.Player.Client.Fuse(0, 2);
            scene.Player.Client.Fuse(8, 8);
            scene.Player.Client.Fuse(8, 100);
            scene.Lab.Run(1);
            report.Check("What can't be fused isn't, on either end, and costs nothing", Pack(scene.Real) == before && Pack(scene.Shown) == before && scene.Real.Gold == goldBefore && scene.Shown.Gold == goldBefore);
        }

        private static void OnlyInTheHaven(Report report, WorldCache worlds, WorldRequest land)
        {
            report.Section("Anywhere else");
            using var scene = new Scene(worlds, land, "Landing", Line);
            string before = Pack(scene.Real);
            scene.Player.Client.Fuse(0, 1);
            report.Check("Fusing in a land shows nothing", Pack(scene.Shown) == before);

            // As if the two had fallen out, and the client thought it had fused them: the server's answer puts it right.
            Crafting.Fuse(scene.Shown, 2, 3);
            scene.Player.Client.Fuse(2, 3);
            scene.Lab.Run(1);
            report.Check("And the server fuses nothing, and says so", Pack(scene.Real) == before && Pack(scene.Shown) == before && scene.Shown.Gold == Gold && scene.Real.Gold == Gold);
        }

        private static void ABadLine(Report report, WorldCache worlds)
        {
            report.Section("A bad line");
            using var scene = new Scene(worlds, Lands.Haven, null, Awful);
            for (int p = 0; p < Pairs; p++)
            {
                scene.Player.Client.Fuse(p * 2, p * 2 + 1);
                scene.Lab.Run(0.05);
            }
            scene.Lab.Run(10);
            report.Check("Every fuse happens, once each, however bad the line", scene.Real.Carried().Count() == Pairs && scene.Real.Carried().All(item => item.Rarity == Rarity.Rare), $"{scene.Real.Carried().Count()} carried");
            report.Check("And both ends agree on all of it, gold too", Pack(scene.Real) == Pack(scene.Shown) && scene.Real.Gold == scene.Shown.Gold && scene.Real.Gold < Gold);
        }
    }
}
