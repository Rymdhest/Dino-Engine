using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    internal static partial class LandTests
    {
        // Playing together, on real clients over a bad line (LANDS.md, NETWORKING.md): friends are known by name, each steps through the way for themselves, it only opens while nobody's in a land
        // and only to a level of the opener's own, anyone who joins waits in the Haven, a friend can be travelled to from a waygate, a fallen friend can be raised, each climbs from their own deepest
        // land, and loot goes as the host says, with gold shared out.
        private static void Together(Report report, WorldCache worlds)
        {
            report.Section("Together");
            using var scene = new Scene(worlds, Lands.Haven, hero: Sturdy(6));
            LabPlayer friend = scene.Join("Friend", Sturdy(3));
            ServerPlayer host = scene.Truth;
            ServerPlayer other = scene.TruthOf(friend);

            report.Check("Friends are known by their names", scene.Player.Client.Party.Select(m => m.Name).SequenceEqual(new[] { "Hero", "Friend" }) && friend.Client.Party.Select(m => m.Name).SequenceEqual(new[] { "Hero", "Friend" }));
            report.Check("And the first to join is the host", scene.Player.Client.IsHost && !friend.Client.IsHost);

            friend.Client.OpenWay(6, 0, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("Nobody opens the way to a level they haven't reached", scene.Lab.Server.Way == null);

            scene.Venture(0);
            ServerRoom land = host.Room!;
            report.Check("Whoever steps through goes alone", land.IsLand && other.Room!.IsHaven && friend.Client.Session!.IsHaven);
            report.Check("And everyone's told where everyone is", friend.Client.AnyoneInLand && friend.Client.Party.Any(m => m.Name == "Hero" && m.InLand) && scene.Player.Client.Party.Any(m => m.Name == "Friend" && !m.InLand));

            friend.Client.OpenWay(3, 0, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("Nobody opens the way to another land while anyone's in one", scene.Lab.Server.Way is var (open, _, _) && open.Level == 6 && host.Room == land);
            friend.Client.CloseWay();
            scene.Lab.Run(1);
            report.Check("Or shuts it", scene.Lab.Server.Way != null && scene.Portal() != null);

            LabPlayer late = scene.Join("Late", Sturdy(1), seed: 5);
            report.Check("Someone who joins meanwhile waits in the Haven", scene.TruthOf(late).Room!.IsHaven && late.Client.Session!.IsHaven);

            scene.UsePortal(friend);
            report.Check("A friend who follows is in the same land", other.Room == land && friend.Client.Session is { IsHaven: false });

            // Every enemy has as much Health again for each hero in the game past the first, and goes with the party as it changes.
            scene.Lab.Run(2);
            List<Actor> foes = Foes(land.Ecs);
            float Share(Actor a) => a.Sheet.MaxHealth / a.Innate.SheetFor(Condition.None).MaxHealth;
            report.Check("With three in the game, every enemy has 200% more Health", foes.Count > 0 && foes.All(f => MathF.Abs(Share(f) - 3f) < 0.05f), foes.Count > 0 ? $"{Share(foes[0]):0.00} times" : "none");
            report.Check("Which the map table and the land both say", Lands.PartyText(friend.Client.Party.Count).Contains("200%"));
            Actor wounded = foes.First();
            wounded.Vitals.Health = wounded.Sheet.MaxHealth * 0.5f;
            LabPlayer fourth = scene.Join("Fourth", Sturdy(1), seed: 6);
            scene.Lab.Run(1);
            report.Check("Someone joining mid-land takes every enemy to 300% more at once", foes.All(f => f.Dead || MathF.Abs(Share(f) - 4f) < 0.05f) && land.Session.PartyHealth == 300f, $"{Share(foes[0]):0.00} times");
            report.Check("Keeping a wounded enemy at the share of it it had", MathF.Abs(wounded.Vitals.Health / wounded.Sheet.MaxHealth - 0.5f) < 0.02f, $"{wounded.Vitals.Health / wounded.Sheet.MaxHealth:0.00}");
            fourth.Client.Dispose();
            scene.Lab.Players.Remove(fourth);
            scene.Lab.Run(2);
            report.Check("And someone leaving takes it back down", foes.All(f => f.Dead || MathF.Abs(Share(f) - 3f) < 0.05f), $"{Share(foes[0]):0.00} times");

            // From a waygate to a friend, and only from one.
            SiteState far = land.Session!.Sites.Where(s => !s.Wild && !WorldProgress.HasWaygate(s)).OrderByDescending(s => s.Site.Remoteness).First();
            scene.Go(far.Site.Position);
            scene.Go(scene.GateOf(land.Session.Sites[0]), friend);
            friend.Client.TravelToHero(host.Id);
            scene.Lab.Run(1.5);
            float apart = (other.Sim.Position - host.Sim.Position).Length;
            report.Check("From a waygate, a friend can be travelled to", apart < 6f, $"{apart:0.0} m apart");
            scene.Go(scene.GateOf(land.Session.Sites[0]));
            friend.Client.TravelToHero(host.Id);
            scene.Lab.Run(1.5);
            apart = (other.Sim.Position - host.Sim.Position).Length;
            report.Check("But not from anywhere else", apart > 20f, $"{apart:0.0} m apart");

            // A kill anywhere in the land is worth all of it to everyone in it, as if each were alone.
            Actor? prey = null;
            foreach (Archetype archetype in land.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(ActorComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Actor actor = entity.Get<ActorComponent>().Actor;
                    if (actor.Team == Team.Enemies && !actor.Dead && !entity.Get<EnemyComponent>().Friendly && (prey == null || (actor.Position - other.Sim.Position).LengthSquared < (prey.Position - other.Sim.Position).LengthSquared)) prey = actor;
                }
            }
            if (prey != null)
            {
                (int Level, int Experience) Standing(ServerPlayer p) => (p.Hero.Level, p.Hero.Experience);
                var hostBefore = Standing(host);
                var friendBefore = Standing(other);
                land.Session.Combat.Harm(other.Actor, prey, 1e12f, DamageType.Magic);
                scene.Lab.Run(1.5);
                float away = (prey.Position - host.Sim.Position).Length;
                report.Check("A kill anywhere in the land is worth it to everyone in it, however far", away > Rulebook.FlaskReach && Standing(host) != hostBefore && Standing(other) != friendBefore, $"{away:0} m from the host, {hostBefore} to {Standing(host)}");
            }
            else report.Check("There's something to kill in the land", false);

            // Raising, away from the gate, as use goes to whatever's nearest.
            GameServer.TryFindGround(land.Session.World, scene.GateOf(land.Session.Sites[0]) + new Vector2(16f, 0f), out Vector2 clear);
            scene.Go(clear);
            scene.Go(host.Sim.Position + new Vector2(2f, 0f), friend);
            scene.Fall();
            report.Check("A friend who falls is out, and the land goes on", host.Out && !land.Session.Over && scene.Player.Client.Out);
            report.Check("Where friends see they can be raised", RaiseMarkOn(friend.Ecs, host.Id) && RaiseMarkOn(land.Ecs, host.Id));

            friend.PressUse = true;
            scene.Lab.Run(1);
            report.Check("Use beside them begins raising them", other.Raising == host);
            report.Check("Which the one lying there sees", Raising(scene.Player.Ecs, other.Id) == host.Id);
            friend.Script = () => new HeroIntent { Move = new Vector2(1f, 0f), Aim = new Vector2(1f, 0f) };
            scene.Lab.Run(0.5);
            friend.Script = null;
            scene.Lab.Run(Rulebook.RaiseTime + 0.5);
            report.Check("Moving breaks it", host.Out && other.Raising == null && Raising(scene.Player.Ecs, other.Id) == 0);

            scene.Go(host.Sim.Position + new Vector2(2f, 0f), friend);
            friend.PressUse = true;
            scene.Lab.Run(Rulebook.RaiseTime - 1f);
            report.Check("It takes its time", host.Out);
            scene.Lab.Run(2.5);
            float share = scene.Hero.Vitals.Health / scene.Hero.Sheet.MaxHealth;
            report.Check("Standing still beside them for long enough raises them", !host.Out && !host.Sim.Down && !land.Session.Fallen.Contains(host.Name));
            report.Check("With half of what they had", share > Rulebook.RaisedShare - 0.05f && share < Rulebook.RaisedShare + 0.2f, $"{share:0.00} of their Health");
            report.Check("On both ends, and the account of how they fell is put away", !scene.Player.Client.Out && scene.Player.Client.Recap == null && scene.Player.Client.Session!.Health > 0f);
            report.Check("And their mark goes", !RaiseMarkOn(friend.Ecs, host.Id) && !RaiseMarkOn(land.Ecs, host.Id));
            scene.Lab.Run(5);
            report.Check("For good, and from their own screen too", !RaiseMarkOn(friend.Ecs, host.Id) && !RaiseMarkOn(land.Ecs, host.Id) && !RaiseMarkOn(scene.Player.Ecs, host.Id), $"server {RaiseMarkOn(land.Ecs, host.Id)}, friend {RaiseMarkOn(friend.Ecs, host.Id)}, own {RaiseMarkOn(scene.Player.Ecs, host.Id)}");

            // Loot, as the host says.
            friend.Client.ChooseLoot(LootRule.Allocated);
            scene.Lab.Run(1);
            report.Check("Only the host chooses how loot goes", scene.Lab.Server.Loot == LootRule.FreeForAll);
            scene.Player.Client.ChooseLoot(LootRule.Allocated);
            scene.Lab.Run(1);
            report.Check("Which everyone's told", scene.Lab.Server.Loot == LootRule.Allocated && friend.Client.Loot == LootRule.Allocated && scene.Player.Client.Loot == LootRule.Allocated);

            scene.Go(host.Sim.Position + new Vector2(2f, 0f), friend);
            var lying = Finds(land.Ecs).Select(f => f.Find).ToHashSet();
            Spill(land, host.Sim.Position, 4, 0);
            scene.Lab.Run(3);
            List<(int Find, int Owner, Vector2 At)> finds = Finds(land.Ecs).Where(f => !lying.Contains(f.Find)).ToList();
            report.Check("Allocated, each find is someone's, in turn", finds.Count == 4 && finds.Count(f => f.Owner == host.Id) == 2 && finds.Count(f => f.Owner == other.Id) == 2, string.Join(", ", finds.Select(f => f.Owner)));
            report.Check("And everyone sees whose", finds.All(f => Finds(friend.Ecs).Any(seen => seen.Find == f.Find && seen.Owner == f.Owner)));

            (int theirs, _, Vector2 theirsAt) = finds.First(f => f.Owner == host.Id);
            (int mine, _, Vector2 mineAt) = finds.First(f => f.Owner == other.Id);
            scene.Go(theirsAt, friend);
            friend.Client.PickUp(theirs);
            scene.Lab.Run(1.5);
            report.Check("Nobody takes a friend's", Finds(land.Ecs).Any(f => f.Find == theirs));
            scene.Go(mineAt, friend);
            friend.Client.PickUp(mine);
            scene.Lab.Run(1.5);
            report.Check("But their own they do", !Finds(land.Ecs).Any(f => f.Find == mine));

            int hostGold = host.Hero.Gold, friendGold = other.Hero.Gold, lateGold = scene.TruthOf(late).Hero.Gold;
            scene.Go(host.Sim.Position + new Vector2(1f, 0f), friend);
            Spill(land, host.Sim.Position, 0, 301);
            scene.Lab.Run(5);
            int[] got = { host.Hero.Gold - hostGold, other.Hero.Gold - friendGold, scene.TruthOf(late).Hero.Gold - lateGold };
            report.Check("Gold is shared out evenly among everyone in the game, wherever they are", got.Sum() == 301 && got.All(g => g >= 100 && g <= 101), string.Join(", ", got));

            scene.Player.Client.ChooseLoot(LootRule.FreeForAll);
            scene.Lab.Run(1);
            Spill(land, host.Sim.Position, 2, 0);
            scene.Lab.Run(3);
            report.Check("Free for all, finds are nobody's", Finds(land.Ecs).Count(f => f.Owner == 0) >= 2);

            // Each climbs from their own.
            scene.Free();
            report.Check("Freeing it takes each a level on from their own deepest", host.Hero.LandLevel == 7 && other.Hero.LandLevel == 4 && friend.Client.Hero.LandLevel == 4, $"{host.Hero.LandLevel}, {other.Hero.LandLevel}, {friend.Client.Hero.LandLevel}");

            // Going home, one at a time.
            scene.Player.Client.ChooseLoot(LootRule.Allocated);
            scene.Lab.Run(1);
            Spill(land, other.Sim.Position, 4, 0);
            scene.Lab.Run(3);
            scene.GoHomeThroughGate();
            report.Check("Going home takes nobody else", host.Room!.IsHaven && other.Room == land);
            report.Check("And what was theirs on the ground is anyone's", !Finds(land.Ecs).Any(f => f.Owner == host.Id) && !Finds(friend.Ecs).Any(f => f.Owner == host.Id));
            scene.GoHomeThroughGate(friend);
            report.Check("Once everyone's back the land is gone, and the way shut", other.Room!.IsHaven && !scene.Lab.Server.Rooms.Contains(land) && scene.Lab.Server.Way == null && friend.Client.Portal == null);

            friend.Client.OpenWay(4, 0, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("Then anyone can open it, to a level of their own", scene.Lab.Server.Way is var (theirsOpen, _, _) && theirsOpen.Level == 4);
            if (scene.Lab.Server.Way is var (request, terms, _)) scene.Lab.Worlds.Get(GameLab.Shaped(request, terms)).Wait();
            scene.UsePortal();
            scene.UsePortal(friend);
            ServerRoom second = host.Room!;
            scene.Fall();
            scene.Player.Client.ReturnHome();
            scene.WaitForWorld();
            report.Check("Someone fallen can go home alone, whenever they like", host.Room!.IsHaven && other.Room == second);
            scene.Go(scene.Portal()!.Value);
            scene.Player.PressUse = true;
            scene.Lab.Run(3);
            report.Check("But can't step back into the land they fell in", host.Room!.IsHaven && scene.Player.Client.Session!.IsHaven);
        }

        // Everything hostile standing in a world.
        private static List<Actor> Foes(ECSWorld world)
        {
            var foes = new List<Actor>();
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(ActorComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Actor actor = entity.Get<ActorComponent>().Actor;
                    if (actor.Team == Team.Enemies && !actor.Dead && !entity.Get<EnemyComponent>().Friendly) foes.Add(actor);
                }
            }
            return foes;
        }

        // Whether a hero lies with a mark on them, which a friend can raise them by.
        private static bool RaiseMarkOn(ECSWorld world, int player)
        {
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(HeroComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var mark = entity.Get<InteractableComponent>();
                    if (mark.Kind == InteractKind.Raise && mark.SpawnIndex == player && entity.Get<HeroComponent>().Player == player) return true;
                }
            }
            return false;
        }

        // Who a hero is raising, as a world says: nobody, if none of them is. A lab's client keeps what it saw of the last world, which the game's clears, so a hero can be there twice.
        private static int Raising(ECSWorld world, int player)
        {
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(HeroComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var hero = entity.Get<HeroComponent>();
                    if (hero.Player == player && hero.Raising != 0) return hero.Raising;
                }
            }
            return 0;
        }

        // Every find lying in a world, whose it is and where.
        private static List<(int Find, int Owner, Vector2 At)> Finds(ECSWorld world)
        {
            var finds = new List<(int, int, Vector2)>();
            foreach (Archetype archetype in world.QueryArchetypes(new BitMask(typeof(PickupComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var pickup = entity.Get<PickupComponent>();
                    if (pickup.Find != 0) finds.Add((pickup.Find, pickup.Owner, entity.Get<PositionComponent>().value.Xz));
                }
            }
            return finds;
        }

        // Things and gold out of nothing, beside someone, as a kill would drop them.
        private static void Spill(ServerRoom room, Vector2 at, int items, int gold)
        {
            ServerSession session = room.Session!;
            var random = new WorldRandom(items * 31 + gold);
            var dropped = new List<Item>();
            for (int i = 0; i < items; i++) dropped.Add(ItemGenerator.Forge(session.World.Plan.Level, random.Fork(i), Rarity.Magic, 0.5f));
            session.Ground.Spill(new Vector3(at.X, session.World.Height.Sample(at), at.Y), dropped, gold, gold > 0 ? 1 : 0);
        }
    }
}
