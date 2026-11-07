using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Networking;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// That everyone sees the same gear, for real, with clients on the other end of a line. Nothing about a look is sent but a seed and a few traits, so the whole of this comes down to
    /// whether those arrive, and whether what they grow into on one machine is what it is on another. See GEAR.md.
    /// </summary>
    internal static class GearTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.02 };

        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("What's on the ground");
            Ground(report, worlds, request);

            report.Section("What heroes have on");
            Worn(report, worlds, request);

            return report.Finish();
        }

        private static WornLook? Look(LabPlayer watcher, int player, WornSlot place)
        {
            if (!watcher.Client.Worn.TryGetValue(player, out List<WornLook>? worn)) return null;
            foreach (WornLook look in worn)
            {
                if (look.Place == place) return look;
            }
            return null;
        }

        private static void Ground(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request, "Landing");
            LabPlayer player = lab.AddPlayer("Hero", Line, 1);
            player.Fights = false;
            lab.Run(4);
            ServerPlayer truth = lab.Server.Heroes[0];
            ServerSession session = lab.Server.Session!;

            Item item = ItemGenerator.Forge(20, new WorldRandom(5), Rarity.Rare, 0.5f, ItemSlot.MainHand);
            var at = new Vector3(truth.Sim.Position.X, session.World.Height.Sample(truth.Sim.Position), truth.Sim.Position.Y);
            session.Ground.Spill(at, new[] { item }, 0, 0);
            lab.Run(2);

            PickupComponent? seen = null;
            foreach (Entity entity in player.Ecs.QueryEntities(new BitMask(typeof(PickupComponent)), BitMask.Empty))
            {
                var pickup = player.Ecs.GetEntityView(entity).Get<PickupComponent>();
                if (pickup.Find != 0) seen = pickup;
            }

            report.Check("A find reaches a client", seen != null);
            if (seen is not PickupComponent find) return;
            report.Check("With its whole seed", find.Seed == item.LookSeed(), $"{find.Seed} against {item.LookSeed()}");
            report.Check("And everything else that decides its look", find.Traits == ItemLook.Traits(item));

            GearSpec here = ItemLook.SpecOf(find.Seed, find.Traits);
            GearSpec there = ItemLook.SpecOf(item);
            report.Check("So a client makes the same spec the server would", here == there);

            GearMesh mine = GearForge.Make(here), theirs = GearForge.Make(there);
            report.Check("And the same mesh", mine.VertexCount == theirs.VertexCount && Enumerable.Range(0, mine.VertexCount).All(i => mine.Vertices[i].position == theirs.Vertices[i].position));
        }

        private static void Worn(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request, "Landing");
            HeroState kitted = HeroState.CreateNew();
            Item extra = ItemGenerator.Forge(20, new WorldRandom(6), Rarity.Magic, 0.5f, ItemSlot.MainHand);
            kitted.Pickup(extra);
            Item carried = ItemGenerator.Forge(20, new WorldRandom(7), Rarity.Magic, 0.5f, ItemSlot.Finger);
            kitted.Pickup(carried);

            LabPlayer first = lab.AddPlayer("First", Line, 1, hero: kitted);
            first.Fights = false;
            lab.Run(4);
            ServerPlayer truth = lab.Server.Heroes[0];
            ulong swordBefore = truth.Hero.Equipped[WornSlot.MainHand].LookSeed();

            report.Check("A hero is told what they have on", Look(first, truth.Id, WornSlot.MainHand)?.Seed == swordBefore);
            report.Check("All of it", first.Client.Worn[truth.Id].Count == truth.Hero.Equipped.Count);

            LabPlayer second = lab.AddPlayer("Second", Line, 2);
            second.Fights = false;
            lab.Run(4);
            report.Check("Whoever arrives later is told what's already being worn", Look(second, truth.Id, WornSlot.MainHand)?.Seed == swordBefore);
            ServerPlayer friend = lab.Server.Heroes.First(h => h != truth);
            report.Check("And the first hero is told what they've arrived in", Look(first, friend.Id, WornSlot.MainHand)?.Seed == friend.Hero.Equipped[WornSlot.MainHand].LookSeed());

            // What the server holds is a copy that came over the line, so it's found by what it is.
            int Place(Item wanted) => Array.FindIndex(truth.Hero.Inventory, i => i?.Seed == wanted.Seed);

            first.Client.Equip(Place(extra));
            lab.Run(1.5);
            report.Check("A change reaches the one who made it", Look(first, truth.Id, WornSlot.MainHand)?.Seed == extra.LookSeed());
            report.Check("And everyone else", Look(second, truth.Id, WornSlot.MainHand)?.Seed == extra.LookSeed());

            first.Client.Equip(Place(carried), WornSlot.Finger2);
            lab.Run(1.5);
            report.Check("Which finger a ring is on comes across", Look(second, truth.Id, WornSlot.Finger2)?.Seed == carried.LookSeed() && Look(second, truth.Id, WornSlot.Finger1) == null);

            first.Client.Unequip(WornSlot.Finger2);
            lab.Run(1.5);
            report.Check("And so does taking it off", Look(second, truth.Id, WornSlot.Finger2) == null && truth.Hero.Carried().Any(i => i.Seed == carried.Seed));

            WornLook? seen = Look(second, truth.Id, WornSlot.MainHand);
            report.Check("Both machines would dress them alike", seen != null && ItemLook.SpecOf(seen.Value.Seed, seen.Value.Traits) == ItemLook.SpecOf(extra));
        }
    }
}
