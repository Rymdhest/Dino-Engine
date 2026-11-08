using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Components;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// The loop the game is, for real, on a running server with clients on the other end of a bad line: the Haven, the way opened at the map table to any level that can be opened and the orders
    /// given for it, other lands bought with gold, stepping through, one life to a land, leaving it, freeing it, what each of those does to a climb on both ends, the arenas every tenth level, the
    /// stash, and everything a land's omens and orders do to what stands on it. Read LANDS.md.
    /// </summary>
    internal static class LandTests
    {
        private static readonly WorldRequest Home = new WorldRequest(303, "isle", 6);
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.01 };

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;

            public Scene(WorldCache worlds, WorldRequest request, LandTerms? terms = null, HeroState? hero = null, SaveGame? save = null, Stash? stash = null)
            {
                Lab = new GameLab(worlds, request, null, terms, save);
                _ = worlds.Get(Lands.Haven);    // Wherever this goes, it's likely to end up there, and growing it takes real time.
                Player = Lab.AddPlayer("Hero", Line, 1, hero: hero ?? Sturdy(), stash: stash);
                Player.Fights = false;
                Lab.Run(5);
            }

            public LabPlayer Join(string name, HeroState? hero = null, int seed = 2)
            {
                LabPlayer friend = Lab.AddPlayer(name, Line, seed, hero: hero ?? Sturdy());
                friend.Fights = false;
                Lab.Run(5);
                return friend;
            }

            public ServerPlayer Truth => TruthOf(Player);
            public ServerPlayer TruthOf(LabPlayer player) => Lab.Server.Players.First(p => p.Name == player.Name);
            public ServerSession Session => Lab.Server.Session!;
            public GameSession Seen => Player.Client.Session!;
            public Actor Hero => Truth.Actor;

            public void Fall(LabPlayer? who = null)
            {
                Actor actor = TruthOf(who ?? Player).Actor;
                Session.Combat.Harm(actor, actor, 1e12f, DamageType.Physical);
                Lab.Run(1.5);
            }

            public void Go(Vector2 to, LabPlayer? who = null)
            {
                (who ?? Player).Client.TravelTo(to);
                Lab.Run(1.5);
            }

            public List<Actor> Members(SiteState state)
            {
                var found = new List<Actor>();
                foreach (Entity entity in state.Live)
                {
                    if (!Lab.Server.Ecs.TryGetEntityLocation(entity, out _, out _)) continue;
                    EntityView view = Lab.Server.Ecs.GetEntityView(entity);
                    if (view.Has<ActorComponent>()) found.Add(view.Get<ActorComponent>().Actor);
                }
                return found;
            }

            /// <summary>Everyone who holds a place, brought down the way it's really done: by going there and hitting them.</summary>
            public void Clear(SiteState state)
            {
                Go(state.Site.Position);
                for (int attempt = 0; attempt < 80 && !state.Cleared; attempt++)
                {
                    foreach (Actor actor in Members(state))
                    {
                        if (!actor.Dead) Session.Combat.Harm(Hero, actor, 1e12f, DamageType.Magic);
                    }
                    Lab.Run(0.3);
                }
            }

            /// <summary>Everything a land asks: as many of its places as it wants freed, then its tyrant, or both of them.</summary>
            public void Free()
            {
                if (Session.IsArena)
                {
                    Clear(Session.Sites.First(s => s.Site.Kind == SiteKind.Arena));
                    Lab.Run(2);
                    return;
                }
                int places = Session.Chronicle.Objectives.First(o => o.Kind == ObjectiveKind.FreePlaces).Goal;
                foreach (SiteState place in Session.Sites.Where(WorldProgress.Counts).OrderBy(s => s.Site.Remoteness).Take(places).ToList()) Clear(place);
                if (Chronicle.TwinSite(Session.World) is WorldSite twin && Session.Terms.Has(OrdersCatalog.TwinPens)) Clear(Session.Sites.First(s => s.Site == twin));
                Clear(Session.Sites.First(s => s.Site.Kind == SiteKind.Keep));
                Lab.Run(2);
            }

            /// <summary>Where a land's waygate is used from, at a place that has one, found or not.</summary>
            public Vector2 GateOf(SiteState state) => GameServer.WaygateSpawn(Session.World, state.Site)!.Value;

            public SiteState Landing => Session.Sites[0];

            /// <summary>Goes to the landing's waygate and asks to go home through it.</summary>
            public void GoHomeThroughGate(LabPlayer? who = null)
            {
                Go(GateOf(Landing), who);
                (who ?? Player).Client.ReturnHome();
                WaitForWorld();
            }

            /// <summary>Waits for the world to change, which can take a few seconds of game first and then real time to grow, and for everyone to arrive in the new one.</summary>
            public void WaitForWorld()
            {
                ServerSession? before = Lab.Server.Session;
                bool Settled() => Lab.Server.Session is ServerSession now && !ReferenceEquals(now, before)
                    && Lab.Players.All(p => p.Client.State == ClientState.Playing && p.Client.Session is GameSession seen && seen.IsHaven == now.IsHaven && seen.World.Plan.Seed == now.World.Plan.Seed);
                for (int i = 0; i < 3000 && !Settled(); i++)
                {
                    Lab.Run(0.1);
                    if (Lab.Server.Session == null) Thread.Sleep(5);
                }
                Lab.Run(3);
            }

            /// <summary>Opens the way to one of the lands the map table shows at a level, the deepest that can be opened unless another is asked for, grows it ahead of time, and steps through.</summary>
            public void Venture(int choice, int level = 0, params int[] ordersCells)
            {
                Player.Client.OpenWay(level > 0 ? level : Player.Client.Hero.LandLevel, choice, ordersCells);
                Lab.Run(2);
                if (Session.Portal is var (request, terms, _)) Lab.Worlds.Get(GameLab.Shaped(request, terms)).Wait();
                UsePortal();
            }

            public void UsePortal()
            {
                Go(Portal()!.Value);
                Player.PressUse = true;
                WaitForWorld();
            }

            /// <summary>Where the open way stands on the server, if it's open.</summary>
            public Vector2? Portal()
            {
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(PortalComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype)) return entity.Get<PositionComponent>().value.Xz;
                }
                return null;
            }

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

            public bool ClientSees<T>() where T : struct, IComponent
            {
                foreach (Archetype archetype in Player.Ecs.QueryArchetypes(new BitMask(typeof(T)), BitMask.Empty))
                {
                    if (archetype.EntityCount > 0) return true;
                }
                return false;
            }

            public void Dispose() => Lab.Dispose();
        }

        /// <summary>A hero who won't fall to anything but what's meant to make them, so that what's being looked at isn't spoiled by a stray blow.</summary>
        private static HeroState Sturdy(int landLevel = 1)
        {
            HeroState hero = GameLab.StrongHero();
            hero.Equipped[WornSlot.Neck].Lines[0].Value = 200000f;
            hero.BestLand = landLevel - 1;
            hero.Gold = 1000;
            return hero;
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();

            TheHaven(report, worlds);
            OpeningTheWay(report, worlds);
            SteppingThrough(report, worlds);
            OneLife(report, worlds);
            OneLifeEach(report, worlds);
            Leaving(report, worlds);
            Freeing(report, worlds);
            ChoosingLevels(report, worlds);
            Arenas(report, worlds);
            Travelling(report, worlds);
            TheStash(report, worlds);
            UniqueOrders(report, worlds);
            Strength(report, worlds);
            Crowds(report, worlds);
            Bursting(report, worlds);
            Rallying(report, worlds);
            WhatChangesTheLand(report, worlds);
            Keeping(report, worlds);
            EverythingAtOnce(report, worlds);
            return report.Finish();
        }

        private static void TheHaven(Report report, WorldCache worlds)
        {
            report.Section("The Haven");
            using var scene = new Scene(worlds, Lands.Haven, hero: Sturdy(4));
            report.Check("Everyone starts in the Haven", scene.Session.IsHaven && scene.Seen.IsHaven && scene.Player.Arrivals == 1);
            report.Check("Where nothing is hostile", scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent)), new BitMask(typeof(DummyComponent))).All(archetype => archetype.EntityCount == 0));
            report.Check("And nothing asks anything of anyone", scene.Session.Chronicle.Objectives.Count == 0 && scene.Session.Terms.Count == 0);

            var groups = new int[DummyMeter.Groups];
            var dummies = new List<Actor>();
            var grouped = new List<(int group, Vector2 at)>();
            foreach (Archetype archetype in scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(DummyComponent), typeof(ActorComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    int group = entity.Get<DummyComponent>().Group;
                    groups[group]++;
                    dummies.Add(entity.Get<ActorComponent>().Actor);
                    grouped.Add((group, entity.Get<ActorComponent>().Actor.Position));
                }
            }
            report.Check("There's a dummy alone, a group of five and a crowd of fifteen", groups.SequenceEqual(new[] { 1, 5, 15 }), string.Join(", ", groups));
            float apart = grouped.SelectMany(a => grouped.Where(b => b.group != a.group).Select(b => (a.at - b.at).Length)).DefaultIfEmpty(0f).Min();
            report.Check("Far enough apart that what's aimed at one group meets no other", apart >= 10f, $"{apart:0.0} m");
            report.Check("And a merchant, a stash and the map table", scene.Find(InteractKind.Merchant) != null && scene.Find(InteractKind.Stash) != null && scene.Find(InteractKind.MapTable) != null);
            report.Check("But no shrine, as there's nothing here to be blessed for", scene.Find(InteractKind.Shrine) == null);
            report.Check("But no waygate to the land's roads, as it has none", scene.Find(InteractKind.Waygate) == null);

            if (dummies.Count > 0)
            {
                Actor dummy = dummies[0];
                scene.Go(dummy.Position + new Vector2(0f, -4f));
                scene.Session.Combat.Harm(scene.Hero, dummy, 1e12f, DamageType.Magic);
                scene.Lab.Run(1);
                report.Check("A dummy can't be killed", !dummy.Dead && dummy.Vitals.Health > 0f, $"{dummy.Vitals.Health}");
                scene.Lab.Run(Rulebook.DummyRest + 1.5);
                report.Check("And is as good as new once it's left alone", dummy.Vitals.Health >= dummy.Sheet.MaxHealth - 0.01f, $"{dummy.Vitals.Health} of {dummy.Sheet.MaxHealth}");
                report.Check("And the client is shown what it took", scene.Player.HitsSeen > 0);
            }

            if (scene.Find(InteractKind.MapTable) is Vector2 table)
            {
                scene.Go(table);
                scene.Player.PressUse = true;
                scene.Lab.Run(1.5);
                report.Check("The map table opens", scene.Player.PanelsOpened.Any(p => p.kind == InteractKind.MapTable));
            }
            if (scene.Find(InteractKind.Stash) is Vector2 strongbox)
            {
                scene.Go(strongbox);
                scene.Player.PressUse = true;
                scene.Lab.Run(1.5);
                report.Check("So does the stash", scene.Player.PanelsOpened.Any(p => p.kind == InteractKind.Stash));
            }
            if (scene.Find(InteractKind.Merchant) is Vector2 stall)
            {
                scene.Go(stall);
                scene.Player.PressUse = true;
                scene.Lab.Run(1.5);
                (InteractKind kind, SiteState site) opened = scene.Player.PanelsOpened.LastOrDefault(p => p.kind == InteractKind.Merchant);
                Item?[] stock = opened.site?.Table.Stock ?? Array.Empty<Item?>();
                List<Item> orders = stock.OfType<Item>().Where(item => item.IsOrders).ToList();
                report.Check("The merchant sells a few orders, for the land the hero's going to next", orders.Count == 2 && orders.All(o => o.Level >= scene.Truth.Hero.LandLevel), $"{orders.Count}");
                bool safe = true;
                try
                {
                    foreach (Item item in stock.OfType<Item>()) _ = scene.Player.Client.Hero.WornBeside(item);
                }
                catch (Exception)
                {
                    safe = false;
                }
                report.Check("And anything on the table can be looked at beside what's worn", safe);
            }

            int level = scene.Truth.Hero.LandLevel;
            scene.Fall();
            scene.Lab.Run(HeroRules.DeathDelay + 1.5);
            report.Check("Falling in the Haven costs nothing", !scene.Truth.Sim.Down && !scene.Truth.Out && scene.Truth.Hero.LandLevel == level && scene.Hero.Vitals.Health > 0f);
            report.Check("And nobody is sent anywhere", scene.Player.Arrivals == 1 && scene.Session.IsHaven);
        }

        private static void OpeningTheWay(Report report, WorldCache worlds)
        {
            report.Section("Opening the way");
            HeroState hero = Sturdy(5);
            Item orders = OrdersMaker.Roll(5, new WorldRandom(3), Rarity.Magic);
            Item sword = ItemGenerator.Roll(5, new WorldRandom(4));
            hero.Inventory[5] = orders;
            hero.Inventory[6] = sword;
            IReadOnlyList<LandOffer> offers = Lands.Offer(hero, 5);

            using var scene = new Scene(worlds, Lands.Haven, hero: hero);
            report.Check("Both ends see the same three lands", Lands.Offer(scene.Player.Client.Hero, 5).Select(o => o.Request.Seed).SequenceEqual(offers.Select(o => o.Request.Seed)) && Lands.Offer(scene.Truth.Hero, 5).Select(o => o.Request.Seed).SequenceEqual(offers.Select(o => o.Request.Seed)));
            report.Check("At the level asked for", offers.All(o => o.Request.Level == 5));

            scene.Player.Client.OpenWay(5, 1, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("Opening the way opens it to the land chosen", scene.Session.Portal is var (request, _, _) && request.Seed == offers[1].Request.Seed);
            report.Check("With what the map table said it holds", scene.Session.Portal is var (_, held, _) && held.Chosen().SequenceEqual(offers[1].Terms.Chosen()));
            report.Check("And the client knows where it leads", scene.Player.Client.Portal is var (_, seen, _) && seen.Seed == offers[1].Request.Seed);
            report.Check("And sees it standing in the gate", scene.ClientSees<PortalComponent>());

            scene.Player.Client.CloseWay();
            scene.Lab.Run(2);
            report.Check("Closing it closes it", scene.Session.Portal == null && scene.Player.Client.Portal == null && scene.Portal() == null && !scene.ClientSees<PortalComponent>());

            scene.Player.Client.OpenWay(5, 0, new[] { 6 });
            report.Check("Something that isn't orders isn't given", scene.Player.Client.Hero.Inventory[6]?.Seed == sword.Seed);
            scene.Lab.Run(2);
            report.Check("And the way opens without it", scene.Session.Portal is var (_, plain, _) && plain.Orders.Count == 0 && !plain.Any && scene.Truth.Hero.Inventory[6] != null);

            scene.Player.Client.OpenWay(6, 0, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("A level deeper than the next can't be opened", scene.Session.Portal is var (deeper, _, _) && deeper.Level == 5);
            scene.Player.Client.OpenWay(3, 2, Array.Empty<int>());
            scene.Lab.Run(2);
            report.Check("But any freed one can", scene.Session.Portal is var (lower, _, _) && lower.Level == 3 && lower.Seed == Lands.Offer(hero, 3)[2].Request.Seed);

            scene.Player.Client.OpenWay(5, 2, new[] { 5 });
            report.Check("Orders given are gone from the pack at once", scene.Player.Client.Hero.Inventory[5] == null);
            scene.Lab.Run(2);
            var expected = offers[2].Terms.Copy();
            expected.Give(orders);
            expected.Sanitize();
            report.Check("And from the server's", scene.Truth.Hero.Inventory[5] == null && scene.Player.Client.Hero.Inventory[5] == null);
            report.Check("And the land holds what they say, and nothing else", scene.Session.Portal is var (_, given, _) && given.Chosen().SequenceEqual(expected.Chosen()) && given.Count > 0 && !offers[2].Terms.Any);
            report.Check("Which the client is told", scene.Player.Client.Portal is var (_, _, told) && told.Chosen().SequenceEqual(expected.Chosen()));
        }

        private static void SteppingThrough(Report report, WorldCache worlds)
        {
            report.Section("Stepping through");
            HeroState hero = Sturdy(6);
            IReadOnlyList<LandOffer> offers = Lands.Offer(hero, 6);
            ulong offersBefore = hero.Offers;

            using var scene = new Scene(worlds, Lands.Haven, hero: hero);
            LabPlayer friend = scene.Join("Friend", Sturdy(2));
            ulong friendsBefore = scene.TruthOf(friend).Hero.Offers;
            scene.Venture(0);

            LandOffer offer = offers[0];
            report.Check("Stepping through takes the whole party there", scene.Player.Arrivals == 2 && friend.Arrivals == 2, $"{scene.Player.Arrivals}, {friend.Arrivals}");
            report.Check("To the land that was chosen", !scene.Session.IsHaven && scene.Session.Request.Seed == offer.Request.Seed && scene.Session.World.Plan.Level == 6);
            report.Check("Holding what it said, on both ends", scene.Session.Terms.Chosen().SequenceEqual(offer.Terms.Chosen()) && scene.Seen.Terms.Chosen().SequenceEqual(offer.Terms.Chosen()) && friend.Client.Session!.Terms.Chosen().SequenceEqual(offer.Terms.Chosen()));
            report.Check("Asking for as many places as it said", scene.Session.Chronicle.Objectives.Any(o => o.Kind == ObjectiveKind.FreePlaces && o.Goal == Math.Min(offer.Terms.Places, scene.Session.Sites.Count(WorldProgress.Counts))));
            report.Check("And its tyrant", scene.Session.Chronicle.Objectives.Any(o => o.Kind == ObjectiveKind.SlayWarlord && o.IsMain && o.Goal == 1));

            WorldData grown = new WorldGenerator().Generate(offer.Terms.Shape(offer.Request));
            report.Check("A machine of its own grows the same land from what it was told", Protocol.WorldHash(grown).structure == Protocol.WorldHash(scene.Session.World).structure);
            report.Check("Going somewhere changes nobody's lands", scene.Truth.Hero.Offers == offersBefore && scene.TruthOf(friend).Hero.Offers == friendsBefore && scene.Truth.Hero.Rerolls.Count == 0 && Lands.Offer(scene.Truth.Hero, 6).Select(o => o.Request.Seed).SequenceEqual(offers.Select(o => o.Request.Seed)));
            report.Check("And nothing of the Haven came along", scene.Player.Client.Ghosts.Mismatches == 0 && scene.Portal() == null);
            report.Check("They arrive out of the landing's waygate", (scene.Truth.Sim.Position - scene.GateOf(scene.Landing)).Length < 14f, $"{(scene.Truth.Sim.Position - scene.GateOf(scene.Landing)).Length:0.0} m from it");
        }

        private static void OneLife(Report report, WorldCache worlds)
        {
            report.Section("One life");
            using (var scene = new Scene(worlds, Home, hero: Sturdy(6)))
            {
                ulong[] before = Lands.Offer(scene.Truth.Hero, 6).Select(o => (ulong)o.Request.Seed).ToArray();
                scene.Fall();
                report.Check("A hero who falls in a land is out of it, on both ends", scene.Truth.Out && scene.Player.Client.Out);
                report.Check("And it costs nothing from their climb", scene.Truth.Hero.LandLevel == 6 && scene.Player.Client.Hero.LandLevel == 6 && scene.Truth.Hero.BestLand == 5, $"{scene.Truth.Hero.LandLevel}, {scene.Player.Client.Hero.LandLevel}");
                report.Check("And the land is lost", scene.Session.Lost && scene.Seen.Lost);

                Vector2 at = scene.Truth.Sim.Position;
                scene.Player.Script = () => new HeroIntent { Move = new Vector2(1f, 0f), Aim = new Vector2(1f, 0f) };
                scene.Lab.Run(1);
                scene.Player.Script = null;
                report.Check("And does nothing more in it", (scene.Truth.Sim.Position - at).Length < 0.01f);
                report.Check("And isn't stood back up", scene.Truth.Sim.Down);

                scene.WaitForWorld();
                report.Check("A moment later everyone is home", scene.Session.IsHaven && scene.Player.Arrivals == 2 && scene.Seen.IsHaven);
                report.Check("On their feet, and no longer out of anything", !scene.Truth.Sim.Down && !scene.Truth.Out && !scene.Player.Client.Out && scene.Hero.Vitals.Health > 0f);
                report.Check("With the climb as it was", scene.Truth.Hero.LandLevel == 6 && scene.Player.Client.Hero.LandLevel == 6);
                report.Check("And the same lands to try again", Lands.Offer(scene.Truth.Hero, 6).Select(o => (ulong)o.Request.Seed).SequenceEqual(before) && Lands.Offer(scene.Player.Client.Hero, 6).Select(o => (ulong)o.Request.Seed).SequenceEqual(before));
            }
        }

        private static void OneLifeEach(Report report, WorldCache worlds)
        {
            report.Section("One life each");
            using var scene = new Scene(worlds, Home, hero: Sturdy(6));
            LabPlayer friend = scene.Join("Friend", Sturdy(6));

            scene.Fall(friend);
            report.Check("A friend who falls is out", scene.TruthOf(friend).Out && friend.Client.Out && !scene.Truth.Out && !scene.Player.Client.Out);
            report.Check("And loses nothing of their climb", scene.TruthOf(friend).Hero.LandLevel == 6 && scene.Truth.Hero.LandLevel == 6);
            report.Check("But the land isn't lost while someone stands", !scene.Session.Lost && scene.Session.HomeAt == null);
            report.Check("And the rest fight on", scene.Player.Client.State == ClientState.Playing && !scene.Truth.Sim.Down);

            // Going and coming back isn't another life.
            friend.Client.Dispose();
            scene.Lab.Players.Remove(friend);
            scene.Lab.Run(2);
            LabPlayer back = scene.Join("Friend", Sturdy(5), seed: 7);
            report.Check("Someone who fell and comes back in arrives fallen", scene.TruthOf(back).Out && scene.TruthOf(back).Sim.Down && back.Client.Out);
            report.Check("With nothing taken from their climb", scene.TruthOf(back).Hero.LandLevel == 5);

            scene.Fall();
            report.Check("Once everyone's fallen the land is lost", scene.Session.Lost);
            scene.WaitForWorld();
            report.Check("And everyone goes home", scene.Session.IsHaven && !scene.TruthOf(back).Out && !scene.Truth.Out);

            // Someone still standing going their own way can leave only the fallen, who shouldn't be kept waiting for nobody.
            scene.Venture(0);
            LabPlayer last = scene.Lab.Players.First(p => p.Name == "Friend");
            scene.Fall(last);
            report.Check("A fallen friend waits on whoever's left", !scene.Session.Lost && scene.Session.HomeAt == null);
            scene.Player.Client.Dispose();
            scene.Lab.Players.Remove(scene.Player);
            scene.Lab.Run(2);
            report.Check("And if they go, the land is over", scene.Session.HomeAt != null);
        }

        private static void Leaving(Report report, WorldCache worlds)
        {
            report.Section("Leaving");
            using var scene = new Scene(worlds, Home, hero: Sturdy(6));
            scene.Lab.Worlds.Get(Lands.Haven).Wait();

            SiteState far = scene.Session.Sites.Where(s => !s.Wild && !WorldProgress.HasWaygate(s)).OrderByDescending(s => s.Site.Remoteness).First();
            scene.Go(far.Site.Position);
            scene.Player.Client.ReturnHome();
            scene.Lab.Run(2);
            report.Check("Nobody goes home from away from a waygate", !scene.Session.IsHaven && scene.Truth.Hero.LandLevel == 6);

            scene.GoHomeThroughGate();
            report.Check("From a waygate everyone goes home", scene.Session.IsHaven && scene.Player.Arrivals == 2);
            report.Check("Leaving a land unfree costs nothing but the try", scene.Truth.Hero.LandLevel == 6 && scene.Player.Client.Hero.LandLevel == 6 && scene.Truth.Hero.Rerolls.Count == 0, $"{scene.Truth.Hero.LandLevel}, {scene.Player.Client.Hero.LandLevel}");
            report.Check("And the Haven has nothing to go home from", scene.Find(InteractKind.Waygate) == null);

            int level = scene.Truth.Hero.LandLevel;
            scene.Player.Client.ReturnHome();
            scene.Lab.Run(2);
            report.Check("Asking to go home from home does nothing", scene.Truth.Hero.LandLevel == level && scene.Player.Arrivals == 2);
        }

        private static void Freeing(Report report, WorldCache worlds)
        {
            report.Section("Freeing a land");
            using (var scene = new Scene(worlds, Home, hero: Sturdy(6)))
            {
                LabPlayer friend = scene.Join("Friend", Sturdy(2));
                LabPlayer fallen = scene.Join("Fallen", Sturdy(6), seed: 3);
                scene.Fall(fallen);
                int points = scene.Truth.Hero.TalentPoints;

                scene.Free();
                report.Check("The land can be freed", scene.Session.Liberated && scene.Seen.Liberated, string.Join(", ", scene.Session.Chronicle.Objectives.Select(o => o.Summary)));
                report.Check("Which opens the next level, on both ends", scene.Truth.Hero.LandLevel == 7 && scene.Player.Client.Hero.LandLevel == 7, $"{scene.Truth.Hero.LandLevel}, {scene.Player.Client.Hero.LandLevel}");
                report.Check("And is worth a talent point", scene.Truth.Hero.BestLand == 6 && scene.Truth.Hero.TalentPoints == 1 + HeroState.PointsPerLevel * scene.Truth.Hero.Level + 6, $"{scene.Truth.Hero.TalentPoints} at level {scene.Truth.Hero.Level}, from {points}");
                report.Check("A friend with less of a climb climbs from this land", scene.TruthOf(friend).Hero.LandLevel == 7 && friend.Client.Hero.LandLevel == 7);
                report.Check("And whoever fell before it was free doesn't share in it, but loses nothing", scene.TruthOf(fallen).Hero.LandLevel == 6 && scene.TruthOf(fallen).Hero.BestLand == 5);
                report.Check("They're told", scene.Seen.Banners.Any(b => b.Title.Contains("free")));
                report.Check("The land's people are safe now", scene.Session.Sites.Where(s => !s.Wild).All(scene.Session.IsSafe));

                scene.Fall();
                report.Check("Falling once it's free costs nothing", scene.Truth.Hero.LandLevel == 7 && !scene.Session.Lost);
                scene.Fall(friend);
                scene.WaitForWorld();
                report.Check("And everyone falling only sends them home", scene.Session.IsHaven && scene.Truth.Hero.LandLevel == 7 && scene.TruthOf(friend).Hero.LandLevel == 7);
            }

            using (var scene = new Scene(worlds, Home, hero: Sturdy(6)))
            {
                int[] before = Lands.Offer(scene.Truth.Hero, 6).Select(o => o.Request.Seed).ToArray();
                scene.Free();
                scene.GoHomeThroughGate();
                report.Check("Going home from a free land keeps the climb", scene.Session.IsHaven && scene.Truth.Hero.LandLevel == 7);
                report.Check("And the level it was shows other lands, on both ends", Lands.Offer(scene.Truth.Hero, 6).All(o => !before.Contains(o.Request.Seed)) && Lands.Offer(scene.Player.Client.Hero, 6).Select(o => o.Request.Seed).SequenceEqual(Lands.Offer(scene.Truth.Hero, 6).Select(o => o.Request.Seed)));
            }

            var wager = new LandTerms { Orders = { OrdersCatalog.Wager.Id } };
            using (var scene = new Scene(worlds, Home, wager, Sturdy(6)))
            {
                scene.Free();
                report.Check("The Kommandant's Wager opens two levels", scene.Truth.Hero.LandLevel == 8 && scene.Player.Client.Hero.LandLevel == 8);
            }
            using (var scene = new Scene(worlds, Home, wager, Sturdy(6)))
            {
                scene.Fall();
                report.Check("And losing it costs nothing either", scene.Truth.Hero.LandLevel == 6);
            }
        }

        private static void ChoosingLevels(Report report, WorldCache worlds)
        {
            report.Section("Choosing a level, and other lands");
            HeroState hero = Sturdy(13);
            hero.Gold = 100000;
            Item magic = OrdersMaker.Roll(12, new WorldRandom(51), Rarity.Magic);
            Item rare = OrdersMaker.Roll(12, new WorldRandom(52), Rarity.Rare);
            Item plain = OrdersMaker.Roll(12, new WorldRandom(53), Rarity.Normal);
            hero.Inventory[3] = magic;
            hero.Inventory[4] = rare;
            hero.Inventory[5] = plain;
            int[] twelve = Lands.Offer(hero, 12).Select(o => o.Request.Seed).ToArray();
            int[] eleven = Lands.Offer(hero, 11).Select(o => o.Request.Seed).ToArray();

            using (var scene = new Scene(worlds, Lands.Haven, hero: hero))
            {
                int gold = scene.Truth.Hero.Gold;
                scene.Player.Client.Reroll(12);
                scene.Lab.Run(2);
                report.Check("Gold buys three other lands for a level", scene.Truth.Hero.Gold == gold - Lands.RerollCost(12) && scene.Truth.Hero.RerollsAt(12) == 1 && Lands.Offer(scene.Truth.Hero, 12).All(o => !twelve.Contains(o.Request.Seed)), $"{gold} to {scene.Truth.Hero.Gold}");
                report.Check("Which the client is shown", scene.Player.Client.Hero.Gold == scene.Truth.Hero.Gold && Lands.Offer(scene.Player.Client.Hero, 12).Select(o => o.Request.Seed).SequenceEqual(Lands.Offer(scene.Truth.Hero, 12).Select(o => o.Request.Seed)));
                report.Check("And no other level changes", Lands.Offer(scene.Truth.Hero, 11).Select(o => o.Request.Seed).SequenceEqual(eleven));

                scene.Player.Client.Reroll(20);
                scene.Lab.Run(1.5);
                report.Check("Nobody pays for lands they can't open", scene.Truth.Hero.RerollsAt(20) == 0 && scene.Truth.Hero.Gold == gold - Lands.RerollCost(12));
                scene.Truth.Hero.Gold = Lands.RerollCost(13) - 1;
                scene.Truth.SummaryChanged = true;
                scene.Lab.Run(1);
                scene.Player.Client.Reroll(13);
                scene.Lab.Run(1.5);
                report.Check("Nor for what they can't afford", scene.Truth.Hero.RerollsAt(13) == 0 && scene.Truth.Hero.Gold == Lands.RerollCost(13) - 1);

                report.Check("Past the first arena the table takes two orders", Lands.OrderSlots(scene.Player.Client.Hero) == 2 && Lands.OrderSlots(scene.Truth.Hero) == 2);
                scene.Player.Client.OpenWay(12, 1, new[] { 3, 4, 5 });
                report.Check("Orders past what it takes stay in the pack", scene.Player.Client.Hero.Inventory[3] == null && scene.Player.Client.Hero.Inventory[4] == null && scene.Player.Client.Hero.Inventory[5]?.Seed == plain.Seed);
                scene.Lab.Run(2);
                var expected = new LandTerms();
                expected.Give(magic);
                expected.Give(rare);
                report.Check("And the land holds what both say", scene.Session.Portal is var (request, terms, _) && request.Level == 12 && terms.Chosen().SequenceEqual(expected.Chosen()) && scene.Truth.Hero.Inventory[5] != null && scene.Truth.Hero.Inventory[3] == null);
            }

            using (var scene = new Scene(worlds, Home, hero: Sturdy(13)))
            {
                int[] six = Lands.Offer(scene.Truth.Hero, 6).Select(o => o.Request.Seed).ToArray();
                scene.Free();
                report.Check("Freeing a level already freed moves nobody's climb", scene.Truth.Hero.BestLand == 12 && scene.Player.Client.Hero.LandLevel == 13);
                report.Check("But shows other lands for it next time", scene.Truth.Hero.RerollsAt(6) == 1 && Lands.Offer(scene.Truth.Hero, 6).All(o => !six.Contains(o.Request.Seed)));
            }
        }

        // An arena's tyrant, left with this share of its health. Set rather than struck, as a blow can carry it past more than one share at once, and how blows land is no part of this.
        private static void Wound(Scene scene, Actor tyrant, float share)
        {
            tyrant.Vitals.Health = MathF.Min(tyrant.Vitals.Health, tyrant.Sheet.MaxHealth * share);
            scene.Lab.Run(0.1);
        }

        private static List<Entity> Portcullises(ECSWorld ecs)
        {
            var found = new List<Entity>();
            foreach (Archetype archetype in ecs.QueryArchetypes(new BitMask(typeof(VisualComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (entity.Get<VisualComponent>().Model == ArenaSystem.PortcullisModel) found.Add(entity.Entity);
                }
            }
            return found;
        }

        private static void Arenas(Report report, WorldCache worlds)
        {
            report.Section("Arenas");
            HeroState climber = Sturdy(10);
            IReadOnlyList<LandOffer> offers = Lands.Offer(climber, 10);
            report.Check("The tenth level's lands are three arenas", offers.Count == Lands.Choices && offers.All(o => o.Arena));
            LandOffer offer = offers[0];
            worlds.Get(offer.Request).Wait();

            using (var scene = new Scene(worlds, offer.Request, hero: Sturdy(10)))
            {
                List<Objective> asked = scene.Session.Chronicle.Objectives;
                report.Check("An arena asks for its tyrant and nothing else", asked.Count == 1 && asked[0].Kind == ObjectiveKind.SlayWarlord && asked[0].Goal == 1 && asked[0].Site == SiteKind.Arena, string.Join(", ", asked.Select(o => o.Summary)));
                report.Check("Told of on both ends", scene.Seen.Chronicle.Objectives.Count == 1);

                SiteState arena = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Arena);
                GameServer.TryFindGround(scene.Session.World, arena.Site.Position - new Vector2(0f, 9f), out Vector2 floor);
                scene.Go(floor);
                scene.Lab.Run(2);

                var crowd = new List<(Vector2 at, float height)>();
                foreach (Archetype archetype in scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(VillagerComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (entity.Get<VillagerComponent>().Crowd) crowd.Add((entity.Get<PositionComponent>().value.Xz, entity.Get<PositionComponent>().value.Y));
                    }
                }
                float floorHeight = scene.Session.World.Height.Sample(arena.Site.Position);
                report.Check("The people of the land are made to watch from the stands", crowd.Count >= 24 && crowd.All(c => c.height > floorHeight + ArenaBuilder.TierRise * 0.5f), $"{crowd.Count}");
                scene.Lab.Run(3);
                int still = 0;
                foreach (Archetype archetype in scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(VillagerComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (entity.Get<VillagerComponent>().Crowd && crowd.Any(c => (c.at - entity.Get<PositionComponent>().value.Xz).Length < 0.05f)) still++;
                    }
                }
                report.Check("And stay where they're put", still == crowd.Count, $"{still} of {crowd.Count}");

                Actor? tyrant = scene.Members(arena).FirstOrDefault(a => a.Tyrant);
                report.Check("Its tyrant is there, and only one", tyrant != null && (tyrant.Nature & Condition.Boss) != 0 && scene.Members(arena).Count(a => a.Tyrant) == 1);
                if (tyrant == null) return;

                // The same tyrant without what being fought in an arena puts on it, which is what it would be in a keep.
                var plain = new List<Mod>(tyrant.Innate.Mods);
                plain.AddRange(tyrant.Innate.RuleMods);
                plain.AddRange(tyrant.Innate.Triggers);
                if (tyrant.Innate.Granted != Condition.None) plain.Add(M.Grant(tyrant.Innate.Granted));
                bool marked = plain.Remove(M.More(Stat.Health, Rulebook.ArenaTyrantHealth)) & plain.Remove(M.More(Stat.Damage, Rulebook.ArenaTyrantDamage));
                Loadout inKeep = new Loadout(tyrant.Innate.Level, tyrant.Innate.Weapon, plain, tyrant.Innate.Books);
                report.Check("It wears what being fought in an arena puts on it", marked);
                report.Near("And it's far stronger than it would be in a keep", tyrant.Sheet.MaxHealth / inKeep.SheetFor(tyrant.Nature).MaxHealth, 1f + Rulebook.ArenaTyrantHealth / 100f, 0.01f);
                report.Near("Which what it's worth knows", scene.Session.TyrantHealth, 1f + Rulebook.ArenaTyrantHealth / 100f, 0.0001f);

                List<Entity> gates = Portcullises(scene.Lab.Server.Ecs);
                report.Check("Its gates are shut", gates.Count == ArenaBuilder.GateBearings.Length && Portcullises(scene.Player.Ecs).Count == gates.Count, $"{gates.Count} on the server, {Portcullises(scene.Player.Ecs).Count} seen");
                float Lift(Entity gate) => scene.Lab.Server.Ecs.GetEntityView(gate).Get<PositionComponent>().value.Y - scene.Session.World.Height.Sample(scene.Lab.Server.Ecs.GetEntityView(gate).Get<PositionComponent>().value.Xz);
                report.Check("All the way down", gates.All(g => Lift(g) < 0.5f));
                report.Check("And nobody's come through them", scene.Session.Arrivals.Count == 0);

                Wound(scene, tyrant, 1f - Rulebook.ArenaWaveEvery - 0.03f);
                scene.Lab.Run(2.2);
                int first = Rulebook.ArenaWaveGates * Rulebook.ArenaWaveSize;
                report.Check("As the tyrant weakens its handlers open some of the gates", gates.Count(g => Lift(g) > 2f) == Rulebook.ArenaWaveGates, string.Join(", ", gates.Select(g => Lift(g).ToString("0.0"))));
                report.Check("And let more of the brood in", scene.Session.Arrivals.Count == first, $"{scene.Session.Arrivals.Count} of {first}");
                var rarities = scene.Session.Arrivals.Select(a => scene.Lab.Server.Ecs.GetEntityView(a.Entity).Get<EnemyComponent>().Rarity).ToList();
                report.Check("Every one Magic or Rare", rarities.Count > 0 && rarities.All(r => r is Rarity.Magic or Rarity.Rare));
                report.Check("And looking for whoever's on the floor", scene.Session.Arrivals.All(a => scene.Lab.Server.Ecs.GetEntityView(a.Entity).Get<EnemyBrainComponent>().AggroRadius > ArenaBuilder.RimRadius * 2f));

                // One Magic enemy can drop nothing, as anything can: the whole of what came through can't, all but never.
                int before = scene.Session.Ground.Count;
                List<Actor> wave = scene.Session.Arrivals.ToList();
                foreach (Actor arrival in wave) scene.Session.Combat.Harm(scene.Hero, arrival, 1e12f, DamageType.Magic);
                scene.Lab.Run(1);
                report.Check("What comes through drops what anything does", scene.Session.Ground.Count > before && wave.All(a => !scene.Session.Arrivals.Contains(a)), $"{scene.Session.Ground.Count - before} finds from {wave.Count}");
                report.Check("And belongs to no place", arena.Dead.Count == 0 && !arena.Cleared);

                scene.Lab.Run(Rulebook.ArenaGateOpen + 3.5);
                report.Check("The gates come down again", gates.All(g => Lift(g) < 0.5f), string.Join(", ", gates.Select(g => Lift(g).ToString("0.0"))));
                report.Check("No more come while the tyrant holds", scene.Session.Arrivals.Count == 0);

                Wound(scene, tyrant, Rulebook.ArenaWaveEvery - 0.05f);
                scene.Lab.Run(2.2);
                int all = first + Rulebook.ArenaWaveGates * Rulebook.ArenaWaveSize + ArenaBuilder.GateBearings.Length * Rulebook.ArenaWaveSize;
                report.Check("The last time every gate goes up", gates.All(g => Lift(g) > 2f), string.Join(", ", gates.Select(g => Lift(g).ToString("0.0"))));
                report.Check("And as many come through as each time asks, however quickly they come", scene.Session.Arrivals.Count + first == all, $"{scene.Session.Arrivals.Count + first} of {all}");

                scene.Session.Combat.Harm(scene.Hero, tyrant, 1e12f, DamageType.Magic);
                scene.Lab.Run(3);
                report.Check("Bringing it down frees the arena", scene.Session.Liberated && scene.Seen.Liberated);
                report.Check("And opens the next level, and room at the table for one more orders", scene.Truth.Hero.BestLand == 10 && Lands.OrderSlots(scene.Truth.Hero) == 2 && Lands.OrderSlots(scene.Player.Client.Hero) == 2);
            }

            var twins = new LandTerms { Orders = { OrdersCatalog.TwinPens.Id } };
            worlds.Get(twins.Shape(offer.Request)).Wait();
            using (var scene = new Scene(worlds, offer.Request, twins, Sturdy(10)))
            {
                SiteState arena = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Arena);
                scene.Go(arena.Site.Position);
                List<Actor> tyrants = scene.Members(arena).Where(a => a.Tyrant).ToList();
                report.Check("Orders for the Twin Pens let a second tyrant in beside the first", tyrants.Count == 2 && scene.Session.Chronicle.Objectives[0].Goal == 2 && arena.HostilesTotal == 2);
                scene.Session.Combat.Harm(scene.Hero, tyrants[0], 1e12f, DamageType.Magic);
                scene.Lab.Run(2);
                report.Check("And one isn't enough", !scene.Session.Liberated);
            }

            var garrison = new LandTerms { Orders = { OrdersCatalog.Garrison.Id } };
            worlds.Get(garrison.Shape(offer.Request)).Wait();
            using (var scene = new Scene(worlds, offer.Request, garrison, Sturdy(10)))
            {
                SiteState arena = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Arena);
                scene.Go(arena.Site.Position);
                Actor? tyrant = scene.Members(arena).FirstOrDefault(a => a.Tyrant);
                if (tyrant != null) Wound(scene, tyrant, 1f - Rulebook.ArenaWaveEvery - 0.03f);
                scene.Lab.Run(2.2);
                int expected = Rulebook.ArenaWaveGates * (int)MathF.Round(Rulebook.ArenaWaveSize * (1f + garrison.MorePeople));
                report.Check("The Last Garrison lets as many more through each gate as it puts at a place", scene.Session.Arrivals.Count == expected, $"{scene.Session.Arrivals.Count} of {expected}");
            }

            // A boss that hasn't been fought can't be passed by.
            HeroState wagerer = Sturdy(9);
            var wager = new LandTerms { Orders = { OrdersCatalog.Wager.Id } };
            using (var scene = new Scene(worlds, new WorldRequest(303, "isle", 9), wager, wagerer))
            {
                scene.Free();
                report.Check("Nothing opens past an arena that hasn't been fought, wager or not", scene.Truth.Hero.LandLevel == 10 && Lands.IsBossLevel(scene.Truth.Hero.LandLevel));
            }
        }

        private static void Travelling(Report report, WorldCache worlds)
        {
            report.Section("The old roads");
            using var scene = new Scene(worlds, Home, hero: Sturdy(6));
            List<SiteState> gates = scene.Session.Sites.Where(WorldProgress.HasWaygate).ToList();
            report.Check("A land has waygates to travel between", gates.Count >= 3, $"{gates.Count}");
            SiteState? other = gates.Where(s => s != scene.Landing).OrderByDescending(s => s.Site.Remoteness).FirstOrDefault();
            if (other == null) return;
            int index = scene.Session.Sites.IndexOf(other);

            scene.Go(scene.GateOf(scene.Landing));
            scene.Player.Client.TravelToSite(index);
            scene.Lab.Run(1.5);
            report.Check("A gate nobody's found can't be travelled to", (scene.Truth.Sim.Position - scene.GateOf(other)).Length > 30f && !other.Discovered);

            scene.Go(other.Site.Position);
            scene.Lab.Run(1);
            report.Check("Going there finds it", other.Discovered && scene.Seen.Sites[index].Discovered);

            Vector2 middle = Vector2.Lerp(scene.Landing.Site.Position, other.Site.Position, 0.5f);
            if (GameServer.TryFindGround(scene.Session.World, middle, out Vector2 between)) scene.Go(between);
            scene.Player.Client.TravelToSite(0);
            scene.Lab.Run(1.5);
            report.Check("Nobody travels from away from a gate", (scene.Truth.Sim.Position - scene.GateOf(scene.Landing)).Length > 20f);

            scene.Go(scene.GateOf(scene.Landing));
            scene.Player.Client.TravelToSite(index);
            scene.Lab.Run(1.5);
            report.Check("From one gate to one that's been found takes them there", (scene.Truth.Sim.Position - scene.GateOf(other)).Length < 6f, $"{(scene.Truth.Sim.Position - scene.GateOf(other)).Length:0.0} m away");
            report.Check("And the client agrees", (scene.Player.Client.Predictor.Current.Position - scene.Truth.Sim.Position).Length < 1f);
            report.Check("Standing somewhere a hero can stand", scene.Session.World.Passable.IsSet(scene.Truth.Sim.Position) || scene.Session.World.Walkable.IsSet(scene.Truth.Sim.Position));

            scene.Fall();
            Vector2 lying = scene.Truth.Sim.Position;
            scene.Player.Client.TravelToSite(0);
            scene.Lab.Run(1);
            report.Check("Whoever's fallen goes nowhere", (scene.Truth.Sim.Position - lying).Length < 0.01f);
        }

        private static void TheStash(Report report, WorldCache worlds)
        {
            report.Section("The stash");
            var random = new WorldRandom(19);
            var stash = new Stash();
            Item kept = ItemGenerator.Roll(10, random.Fork(1));
            stash.Tabs[1][7] = kept;
            HeroState hero = Sturdy(3);
            Item carried = ItemGenerator.Roll(10, random.Fork(2));
            Item another = ItemGenerator.Roll(10, random.Fork(3));
            hero.Inventory[2] = carried;
            hero.Inventory[3] = another;

            using var scene = new Scene(worlds, Lands.Haven, hero: hero, stash: stash);
            report.Check("What's put away comes with them to the server", scene.Truth.Stash?.At(1, 7)?.Seed == kept.Seed);

            var bad = new NetworkConditions { Latency = 120, Jitter = 40, LossChance = 0.05 };
            scene.Player.Client.Stow(ItemPlace.Cell(2), 0, 0);
            report.Check("Putting something away is done at once", scene.Player.Client.Stash!.At(0, 0)?.Seed == carried.Seed && scene.Player.Client.Hero.Inventory[2] == null);
            scene.Lab.Run(2);
            report.Check("And the server agrees", scene.Truth.Stash!.At(0, 0)?.Seed == carried.Seed && scene.Truth.Hero.Inventory[2] == null);

            scene.Player.Client.Stow(ItemPlace.Cell(3), 0, 0);
            scene.Player.Client.Shift(0, 0, 3, 47);
            scene.Player.Client.Unstow(1, 7, 2);
            scene.Player.Client.Shift(0, 5, 0, 6);
            scene.Player.Client.Unstow(2, 2);
            scene.Player.Client.Stow(ItemPlace.Cell(30), 9, 0);
            scene.Lab.Run(3);
            static string Cells(Stash s) => string.Join(",", s.Tabs.SelectMany((tab, t) => tab.Select((item, i) => item == null ? "" : $"{t}:{i}:{item.Seed}")).Where(x => x.Length > 0));
            static string Pack(HeroState h) => string.Join(",", h.Inventory.Select((item, i) => item == null ? "" : $"{i}:{item.Seed}").Where(x => x.Length > 0));
            report.Check("Swaps, moves and nonsense come out the same on both ends", Cells(scene.Truth.Stash) == Cells(scene.Player.Client.Stash) && Pack(scene.Truth.Hero) == Pack(scene.Player.Client.Hero), $"{Cells(scene.Truth.Stash)} against {Cells(scene.Player.Client.Stash)}");
            report.Check("Where they were asked to be", scene.Truth.Stash.At(3, 47)?.Seed == another.Seed && scene.Truth.Stash.At(0, 0) == null && scene.Truth.Hero.Inventory[3]?.Seed == carried.Seed && scene.Truth.Hero.Inventory[2]?.Seed == kept.Seed);

            Stash? written = null;
            scene.Player.Client.StashKept += s => written = s;
            scene.Player.Client.Stow(ItemPlace.Cell(2), 2, 2);
            scene.Lab.Run(2);
            report.Check("And the player's own machine is given it to keep", written != null && written.At(2, 2)?.Seed == kept.Seed);

            scene.Venture(0);
            report.Check("It goes with them into a land", scene.Truth.Stash?.At(2, 2)?.Seed == kept.Seed && scene.Player.Client.Stash!.At(2, 2)?.Seed == kept.Seed);
            report.Check("Though there's no strongbox there but in a village", scene.Session.Sites.Where(s => s.Site.Spawns.Any(p => p.Role == SpawnRole.Stash)).All(s => s.Site.Kind == SiteKind.Village));
        }

        private static void UniqueOrders(Report report, WorldCache worlds)
        {
            report.Section("Unique orders");

            var garrison = new LandTerms { Orders = { OrdersCatalog.Garrison.Id } };
            WorldData usual = worlds.Get(Home).Result;
            WorldData held = worlds.Get(garrison.Shape(Home)).Result;
            report.Check("The Last Garrison leaves nothing on the roads or between places", held.Haunts.All(h => h.Spawns.All(s => !s.Role.IsHostile())) || held.Haunts.Count == 0, $"{held.Haunts.Count} haunts");
            report.Check("And holds every place with far more", held.Sites.Where(Chronicle.Counts).Sum(s => s.Spawns.Count(p => p.Role.IsHostile())) > usual.Sites.Where(Chronicle.Counts).Sum(s => s.Spawns.Count(p => p.Role.IsHostile())) * 2);

            var twins = new LandTerms { Orders = { OrdersCatalog.TwinPens.Id } };
            worlds.Get(twins.Shape(Home)).Wait();
            using (var scene = new Scene(worlds, Home, twins, Sturdy(9)))
            {
                Objective tyrants = scene.Session.Chronicle.Objectives.First(o => o.Kind == ObjectiveKind.SlayWarlord);
                report.Check("The Twin Pens ask for two tyrants", tyrants.Goal == 2 && scene.Session.Chronicle.SecondBeast != null);
                SiteState keep = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Keep);
                scene.Clear(keep);
                report.Check("And one isn't enough", !scene.Session.Liberated && tyrants.Progress == 1);
                SiteState pen = scene.Session.Sites.First(s => s.Site == Chronicle.TwinSite(scene.Session.World));
                scene.Go(pen.Site.Position);
                Actor? second = scene.Members(pen).FirstOrDefault(a => a.Tyrant);
                report.Check("A second tyrant really holds the far place", second != null && (second.Nature & Condition.Boss) != 0);
                int before = scene.Session.Ground.Count;
                scene.Clear(pen);
                report.Check("And drops what a tyrant drops, its orders among it", scene.Session.Ground.Count - before >= (int)LootTables.Of(LootTier.Boss).Finds && scene.Session.Ground.All.Any(lying => lying.item.IsOrders));
                report.Check("And with both down, there's only the places left to free", tyrants.Done);
            }

            var writ = new LandTerms { Orders = { OrdersCatalog.Writ.Id } };
            worlds.Get(writ.Shape(Home)).Wait();
            using (var scene = new Scene(worlds, Home, writ, Sturdy(9)))
            {
                SiteState pack = scene.Session.Sites.Where(s => s.Wild && s.HostilesTotal >= 3).OrderBy(s => s.Site.Remoteness).First();
                scene.Go(pack.Site.Position);
                Actor victim = scene.Members(pack).First();
                int risenBefore = CountRisen(scene.Lab);
                scene.Session.Combat.Harm(scene.Hero, victim, 1e12f, DamageType.Magic);
                scene.Lab.Run(0.4);
                report.Check("Under the Ossuary's Writ nothing rises at once", CountRisen(scene.Lab) == risenBefore);
                scene.Lab.Run(Rulebook.RiseDelay + 0.6);
                report.Check("But a moment later the fallen rise as bones", CountRisen(scene.Lab) == risenBefore + 1, $"{CountRisen(scene.Lab) - risenBefore}");
                Actor? bones = RisenActors(scene.Lab).FirstOrDefault();
                if (bones != null)
                {
                    scene.Session.Combat.Harm(scene.Hero, bones, 1e12f, DamageType.Magic);
                    scene.Lab.Run(Rulebook.RiseDelay + 1);
                    report.Check("Once, and no more", CountRisen(scene.Lab) == risenBefore);
                }
            }

            var bred = new LandTerms { Orders = { OrdersCatalog.Bred.Id } };
            worlds.Get(bred.Shape(Home)).Wait();
            using (var scene = new Scene(worlds, Home, bred, Sturdy(9)))
            {
                var met = new List<Rarity>();
                foreach (SiteState state in scene.Session.Sites.Where(s => s.HostilesTotal >= 4).OrderBy(s => s.Site.Remoteness).Take(4).ToList())
                {
                    scene.Go(state.Site.Position);
                    foreach (Entity entity in state.Live)
                    {
                        if (scene.Lab.Server.Ecs.TryGetEntityLocation(entity, out _, out _) && scene.Lab.Server.Ecs.GetEntityView(entity).Has<EnemyComponent>()) met.Add(scene.Lab.Server.Ecs.GetEntityView(entity).Get<EnemyComponent>().Rarity);
                    }
                }
                report.Check("Bred for War: nothing met is Normal", met.Count > 10 && met.All(r => r != Rarity.Normal), $"{met.Count(r => r == Rarity.Normal)} of {met.Count}");
            }

            var ledger = new LandTerms { Orders = { OrdersCatalog.Ledger.Id } };
            WorldData world = worlds.Get(ledger.Shape(Home)).Result;
            int Leaders(LandTerms terms, out int traits)
            {
                int rares = 0;
                traits = 0;
                for (int i = 0; i < world.Haunts.Count; i++)
                {
                    var random = new WorldRandom((ulong)i);
                    var orders = new PackOrders(world, world.Haunts[i], world.Haunts[i].Folk, 10, true, random.Fork("pack"), terms);
                    (_, Rarity rarity, int drawn) = orders.Draw(0, random.Fork("kind"));
                    if (rarity != Rarity.Rare) continue;
                    rares++;
                    traits += System.Numerics.BitOperations.PopCount((uint)drawn);
                }
                return rares;
            }
            int usualRares = Leaders(new LandTerms(), out _);
            int ledgerRares = Leaders(ledger, out _);
            report.Check("The Brutmeister's Ledger: every pack that can be is led by a rare", ledgerRares > usualRares * 1.5f && ledgerRares > 0, $"{usualRares} to {ledgerRares} of {world.Haunts.Count}");
        }

        private static IEnumerable<Actor> RisenActors(GameLab lab)
        {
            foreach (Archetype archetype in lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(ActorComponent), typeof(EnemyComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Actor actor = entity.Get<ActorComponent>().Actor;
                    if (actor.Risen && !actor.Dead) yield return actor;
                }
            }
        }

        private static int CountRisen(GameLab lab) => RisenActors(lab).Count();

        private static void Strength(Report report, WorldCache worlds)
        {
            report.Section("What omens do to those on the land");
            var terms = new LandTerms { Omens = { new ItemLine(Omens.Ironhide.Id, 60f), new ItemLine(Omens.Wrath.Id, 40f) } };
            worlds.Get(terms.Shape(Home)).Wait();

            using var plain = new Scene(worlds, Home, hero: Sturdy());
            using var worse = new Scene(worlds, Home, terms, Sturdy());

            SiteState keep = worse.Session.Sites.First(s => s.Site.Kind == SiteKind.Keep);
            int index = worse.Session.Sites.IndexOf(keep);
            plain.Go(keep.Site.Position);
            worse.Go(keep.Site.Position);
            Actor? bossBefore = plain.Members(plain.Session.Sites[index]).FirstOrDefault(a => a.Tyrant);
            Actor? bossAfter = worse.Members(keep).FirstOrDefault(a => a.Tyrant);
            report.Check("There's a tyrant to compare", bossBefore != null && bossAfter != null);
            if (bossBefore != null && bossAfter != null)
            {
                report.Near("The tyrant really has the Health its land and its wrath say", bossAfter.Sheet.MaxHealth / bossBefore.Sheet.MaxHealth, terms.EnemyHealth * (1f + 40f / 100f), 0.002f);
                report.Check("And is otherwise the same tyrant", bossBefore.Loadout.Level == bossAfter.Loadout.Level);
            }

            SiteState pack = worse.Session.Sites.First(s => s.Wild && s.HostilesTotal >= 3);
            worse.Go(pack.Site.Position);
            List<Actor> kin = worse.Members(pack);
            report.Check("Out in the wild as well", kin.Count > 0 && kin.All(a => a.Sheet.MaxHealth > Bestiary.HealthAt(a.Loadout.Level) * 0.3f * terms.EnemyHealth));

            // A find's worth comes from health, and health is what omens change. If that weren't taken out again, this would pay for champions.
            float Rate(Scene scene)
            {
                Loadout fodder = scene.Session.Bestiary.Shared(Bestiary.Cutthroat, 6, Rarity.Normal, Bestiary.NoTraits);
                var actor = new Actor(Team.Enemies, fodder, scene.Truth.Sim.Position, 1f);
                var enemy = new EnemyComponent { Name = "Fodder", Rarity = Rarity.Normal, Level = 6 };
                int before = scene.Session.Ground.Count;
                Vector2 at = scene.Truth.Sim.Position;
                var ground = new Vector3(at.X, scene.Session.World.Height.Sample(at), at.Y);
                const int kills = 3000;
                for (int i = 0; i < kills; i++) scene.Session.Combat.DropLoot(enemy, ground, actor);
                return (scene.Session.Ground.Count - before) / (float)kills;
            }

            float usual = Rate(plain);
            float paid = Rate(worse);
            float wanted = terms.Boon().Quantity;
            report.Check("Fodder stays fodder however much Health it's given", paid / usual > wanted * 0.75f && paid / usual < wanted * 1.35f, $"{paid / usual:0.00}x the finds, wanted about {wanted:0.00}x");

            // What a land does to a hero is part of every step, so both ends have to put it on.
            var frail = new LandTerms { Omens = { new ItemLine(Omens.Frail.Id, 12f), new ItemLine(Omens.Lagging.Id, 20f) } };
            worlds.Get(frail.Shape(Home)).Wait();
            using var weak = new Scene(worlds, Home, frail, Sturdy());
            report.Check("What a land does to a hero, they really wear", weak.Hero.Loadout.Mods.Any(m => m.Stat == Stat.DamageTaken) && weak.Hero.Sheet.Total(Stat.CooldownRecovery, 100f) < plain.Hero.Sheet.Total(Stat.CooldownRecovery, 100f));
            report.Check("On both ends", weak.Player.Client.Sheet.Total(Stat.CooldownRecovery, 100f) == weak.Hero.Sheet.Total(Stat.CooldownRecovery, 100f) && weak.Player.Client.Sheet.MaxHealth == weak.Hero.Sheet.MaxHealth);
            weak.GoHomeThroughGate();
            report.Check("And takes off again at home", weak.Hero.Sheet.Total(Stat.CooldownRecovery, 100f) == weak.Player.Client.Sheet.Total(Stat.CooldownRecovery, 100f) && !weak.Hero.Loadout.Mods.Any(m => m.Stat == Stat.DamageTaken && m.Kind == ModKind.More));
        }

        private static void Crowds(Report report, WorldCache worlds)
        {
            report.Section("More of them");
            var crowd = new LandTerms { Omens = { new ItemLine(Omens.Teeming.Id, 80f), new ItemLine(Omens.Overrun.Id, 60f) } };
            WorldData usual = worlds.Get(Home).Result;
            int usualCount = usual.Haunts.Sum(h => h.Spawns.Count(s => s.Role.IsHostile()));
            (uint _, uint usualGround) = Protocol.WorldHash(usual);
            int usualPacks = usual.Haunts.Count;
            float usualSites = usual.Sites.Sum(s => s.Spawns.Count(p => p.Role.IsHostile()));

            WorldData more = worlds.Get(crowd.Shape(Home)).Result;
            int moreCount = more.Haunts.Sum(h => h.Spawns.Count(s => s.Role.IsHostile()));
            report.Check("Far more enemies stand on the land", moreCount >= usualCount * 2, $"{usualCount} to {moreCount}");
            report.Check("In more packs", more.Haunts.Count > usualPacks * 1.3f, $"{usualPacks} to {more.Haunts.Count}");
            report.Check("And more hold the places too", more.Sites.Sum(s => s.Spawns.Count(p => p.Role.IsHostile())) > usualSites * 1.3f);
            report.Check("It's the same land under them", Protocol.WorldHash(more).ground == usualGround);

            using var scene = new Scene(worlds, Home, crowd, Sturdy());
            report.Check("Nobody's any stronger for it", scene.Session.Bestiary.Worn.Count == 0);
            report.Check("Nothing more drops from each", scene.Session.Boon == LootBoon.None);
            SiteState pack = scene.Session.Sites.Where(s => s.Wild && s.HostilesTotal >= 8).OrderBy(s => s.Site.Remoteness).First();
            scene.Go(pack.Site.Position);
            report.Check("A big pack is really standing there", scene.Members(pack).Count == pack.HostilesTotal, $"{scene.Members(pack).Count} of {pack.HostilesTotal}");
        }

        private static void Bursting(Report report, WorldCache worlds)
        {
            report.Section("Volatile");
            var terms = new LandTerms { Omens = { new ItemLine(Omens.Volatile.Id, 100f) } };
            worlds.Get(terms.Shape(Home)).Wait();
            using var arena = new Arena(worlds, Home, Sturdy(), terms: terms);

            Loadout kind = arena.Session.Bestiary.Shared(Bestiary.Cutthroat, 6, Rarity.Normal, Bestiary.NoTraits);
            report.Check("Enemies have it", kind.SheetFor(Condition.None).Has(Rule.BurstsOnDeath));

            Actor first = arena.Place(kind, new Vector2(2f, 0f));
            float before = arena.Hero.Vitals.Health;
            arena.Combat.Harm(arena.Hero, first, 1e9f, DamageType.Magic);
            arena.Run(0.2);
            report.Check("Nothing happens at once", arena.Hero.Vitals.Health >= before - 0.01f, $"{before} to {arena.Hero.Vitals.Health}");
            arena.Run(1.2);
            report.Check("A moment later whoever stayed gets burned", arena.Hero.Vitals.Health < before, $"{before} to {arena.Hero.Vitals.Health}");

            arena.Hero.Vitals = Vitals.Full(arena.Hero.Sheet);
            Actor second = arena.Place(kind, new Vector2(2f, 0f));
            float full = arena.Hero.Vitals.Health;
            arena.Combat.Harm(arena.Hero, second, 1e9f, DamageType.Magic);
            arena.Player.Client.TravelTo(arena.At + new Vector2(0f, 16f));
            arena.Run(1.6);
            report.Check("Whoever gets clear doesn't", arena.Hero.Vitals.Health >= full - 0.01f, $"{full} to {arena.Hero.Vitals.Health}");

            // A chain of them mustn't run through a list being walked, or take the rest of a pack with it.
            var crowd = new List<Actor>();
            for (int i = 0; i < 8; i++) crowd.Add(arena.Place(kind, new Vector2(MathF.Cos(i) * 3f, MathF.Sin(i) * 3f)));
            Actor bystander = arena.Place(kind, new Vector2(4f, 2f));
            foreach (Actor actor in crowd) arena.Combat.Harm(arena.Hero, actor, 1e9f, DamageType.Magic);
            arena.Run(1.5);
            report.Check("Enemies don't burn each other", !bystander.Dead && bystander.Vitals.Health >= bystander.Sheet.MaxHealth - 0.01f);

            // What killed them has been dead for a second by then, and gone from the world if the place went to sleep.
            arena.Hero.Vitals.Health = 1f;
            arena.Combat.Harm(arena.Hero, bystander, 1e9f, DamageType.Magic);
            arena.Run(1.5);
            report.Check("What's already dead can still kill", arena.Truth.Sim.Down, $"{arena.Hero.Vitals.Health} Health");
            report.Check("And that's the hero's one life in this land", arena.Truth.Out && arena.Session.Falls == 1);
        }

        private static void Rallying(Report report, WorldCache worlds)
        {
            report.Section("Rallying");
            var terms = new LandTerms { Omens = { new ItemLine(Omens.Rallying.Id, 100f) } };
            worlds.Get(terms.Shape(Home)).Wait();
            using var arena = new Arena(worlds, Home, Sturdy(), terms: terms);

            Loadout kind = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 6, Rarity.Normal, Bestiary.NoTraits);
            Actor near = arena.Place(kind, new Vector2(4f, 0f));
            Actor far = arena.Place(kind, new Vector2(30f, 0f));
            float hit = near.Sheet.Preview(Shelf.Strike).First.AverageHit;

            for (int i = 0; i < 7; i++)
            {
                Actor victim = arena.Place(kind, new Vector2(4f, 3f));
                arena.Combat.Harm(arena.Hero, victim, 1e9f, DamageType.Magic);
                arena.Run(0.2);
            }

            report.Check("Those around it have heard", near.Buffs.Any(b => b.Buff == BuffCatalog.Rallied));
            report.Check("And it stacks only so far", near.Buffs.First(b => b.Buff == BuffCatalog.Rallied).Stacks == BuffCatalog.Rallied.MostStacks);
            report.Check("And they really hit harder", near.Sheet.Preview(Shelf.Strike).First.AverageHit > hit * 1.4f, $"{hit} to {near.Sheet.Preview(Shelf.Strike).First.AverageHit}");
            report.Check("Those too far off haven't", far.Buffs.Count == 0);
            report.Check("Heroes get nothing from it", arena.Hero.Buffs.All(b => b.Buff != BuffCatalog.Rallied));
            report.Check("And being rallied leaves them what they are", near.Sheet.Has(Rule.RalliesOnDeath));

            arena.Run(BuffCatalog.Rallied.Duration + 1.0);
            report.Check("It wears off", near.Buffs.Count == 0 && MathF.Abs(near.Sheet.Preview(Shelf.Strike).First.AverageHit - hit) < 0.01f);
        }

        private static void WhatChangesTheLand(Report report, WorldCache worlds)
        {
            report.Section("What changes the land");

            var night = new LandTerms { Omens = { new ItemLine(Omens.Night.Id, 40f) } };
            worlds.Get(night.Shape(Home)).Wait();
            HeroState nightwalker = Sturdy();
            nightwalker.Level = HeroState.MaxLevel;
            GameLab.Learn(nightwalker, TalentTree.Nodes.First(n => n.Kind != TalentKind.Keystone && n.Mods.Any(m => (m.Need & Condition.InDarkness) != 0)).Id);
            using (var scene = new Scene(worlds, Home, night, nightwalker))
            {
                report.Check("Everywhere is Darkness", scene.Session.Combat.Land.At(scene.Truth.Sim.Position).HasFlag(Condition.InDarkness));
                scene.Lab.Run(2);
                report.Check("A build that goes by the dark finds it", (scene.Truth.Sim.State & Condition.InDarkness) != 0);
                report.Check("And client and server agree what that makes it", scene.Player.Client.Sheet.MaxHealth == scene.Hero.Sheet.MaxHealth && (scene.Seen.Sim.State & Condition.InDarkness) != 0);

                SiteState pack = scene.Session.Sites.First(s => s.Wild && s.HostilesTotal >= 3);
                scene.Go(pack.Site.Position);
                Actor member = scene.Members(pack).First();
                var brain = scene.Lab.Server.Ecs.GetEntityView(member.Entity).Get<EnemyBrainComponent>();
                report.Check("And they notice from further off", brain.AggroRadius >= 21f * 1.3f, $"{brain.AggroRadius}");
            }
            using (var scene = new Scene(worlds, Home, hero: Sturdy()))
            {
                report.Check("Not so on an ordinary land", !scene.Session.Combat.Land.At(scene.Truth.Sim.Position).HasFlag(Condition.InDarkness));
            }

            var host = new LandTerms { Omens = { new ItemLine(Omens.Warhost.Id, 150f) } };
            WorldData world = worlds.Get(host.Shape(Home)).Result;
            (int led, int rare) Count(LandTerms terms)
            {
                int leaders = 0;
                int rares = 0;
                for (int i = 0; i < world.Haunts.Count; i++)
                {
                    WorldSite haunt = world.Haunts[i];
                    var random = new WorldRandom((ulong)i);
                    var orders = new PackOrders(world, haunt, haunt.Folk, 10, true, random.Fork("pack"), terms);
                    (EnemyKind kind, Rarity rarity, _) = orders.Draw(0, random.Fork("kind"));
                    if (kind.Health >= 1.5f || rarity == Rarity.Rare) leaders++;
                    if (rarity == Rarity.Rare) rares++;
                }
                return (leaders, rares);
            }

            (int usualLed, int usualRare) = Count(new LandTerms());
            (int hostLed, int hostRare) = Count(host);
            report.Check("Under a warhost, far more packs are led", hostLed > usualLed * 1.3f, $"{usualLed} to {hostLed} of {world.Haunts.Count}");
            report.Check("And far more of those who lead are rare", hostRare > usualRare * 1.8f, $"{usualRare} to {hostRare}");

            var gilded = new LandTerms { Omens = { new ItemLine(Omens.Gilded.Id, 100f) } };
            worlds.Get(gilded.Shape(Home)).Wait();
            using (var scene = new Scene(worlds, Home, gilded, Sturdy()))
            {
                SiteState? camp = null;
                SiteSpawn chest = default;
                foreach (SiteState state in scene.Session.Sites.Where(s => s.Wild))
                {
                    foreach (SiteSpawn spawn in state.Site.Spawns)
                    {
                        if (spawn.Role != SpawnRole.Chest) continue;
                        camp = state;
                        chest = spawn;
                    }
                    if (camp != null) break;
                }

                report.Check("There's a camp with a chest to try", camp != null);
                if (camp != null)
                {
                    foreach (SiteState state in scene.Session.Sites) state.Cleared = state == camp;
                    scene.Go(chest.Position);
                    int before = scene.Session.Ground.Count;
                    scene.Player.PressUse = true;
                    scene.Lab.Run(2);
                    int found = scene.Session.Ground.Count - before;
                    report.Check("A gilded camp's box gives what a place's chest does", found >= (int)LootTables.Of(LootTier.Chest).Finds, $"{found} finds");
                    report.Check("At the least the rare a place's chest promises", scene.Session.Ground.All.Any(lying => lying.item.Rarity >= Rarity.Rare));
                }
            }
        }

        private static void Keeping(Report report, WorldCache worlds)
        {
            report.Section("Keeping it");
            var terms = new LandTerms { Places = 5, Orders = { OrdersCatalog.TwinPens.Id, OrdersCatalog.Writ.Id }, Omens = { new ItemLine(Omens.Keen.Id, 20f), new ItemLine(Omens.Teeming.Id, 40f) } };
            worlds.Get(terms.Shape(Home)).Wait();

            var save = new SaveGame();
            int index;
            int deadBefore;
            using (var scene = new Scene(worlds, Home, terms, Sturdy(6)))
            {
                SiteState pack = scene.Session.Sites.Where(s => s.Wild && s.HostilesTotal >= 4).OrderByDescending(s => s.Site.Remoteness).First();
                index = scene.Session.Sites.IndexOf(pack);
                pack.Dead.Add(2);
                pack.Dead.Add(3);
                deadBefore = pack.Dead.Count;
                scene.Lab.Server.CaptureWorld(save);
            }

            string file = Path.GetTempFileName();
            SaveGame.PathOverride = file;
            save.Store();
            SaveGame? loaded = SaveGame.Load();
            SaveGame.PathOverride = null;
            File.Delete(file);
            report.Check("A land is written down, with what it holds", loaded != null && loaded.InLand && loaded.Terms.Has(Omens.Keen) && loaded.Terms.Orders.SequenceEqual(terms.Orders) && loaded.Terms.Places == 5);
            if (loaded == null) return;

            using (var again = new Scene(worlds, loaded.Land(), loaded.Terms, Sturdy(6), loaded))
            {
                report.Check("Loading it makes the same land", again.Session.World.Haunts.Count == worlds.Get(terms.Shape(Home)).Result.Haunts.Count && Protocol.WorldHash(again.Session.World).structure == Protocol.WorldHash(worlds.Get(terms.Shape(Home)).Result).structure);
                report.Check("With the same dead in it", index < again.Session.Sites.Count && again.Session.Sites[index].Dead.Count == deadBefore);
                report.Check("Under the same omens and orders, on both ends", again.Session.Terms.Chosen().SequenceEqual(terms.Chosen()) && again.Seen.Terms.Chosen().SequenceEqual(terms.Chosen()) && again.Seen.Terms.Orders.SequenceEqual(terms.Orders));
                report.Check("Asking the same of them", again.Session.Chronicle.Objectives.Select(o => o.Goal).SequenceEqual(new[] { 2, Math.Min(5, again.Session.Sites.Count(WorldProgress.Counts)) }));

                again.Fall();
                var after = new SaveGame();
                again.Lab.Server.CaptureWorld(after);
                report.Check("A land that's been lost isn't kept to go back to", !after.InLand);
            }

            using (var haven = new Scene(worlds, Lands.Haven, hero: Sturdy(6)))
            {
                var home = new SaveGame();
                haven.Lab.Server.CaptureWorld(home);
                report.Check("Nothing of the Haven is kept, as it's always the same", !home.InLand && home.Sites.Count == 0);
            }
        }

        // Everything that can be on a land together, fought for a while by a real client, to see that nothing falls over.
        private static void EverythingAtOnce(Report report, WorldCache worlds)
        {
            report.Section("Everything at once");
            foreach (int seed in new[] { 1, 2, 3 })
            {
                LandTerms terms = Lands.Roll(40, new WorldRandom((ulong)seed));
                terms.Give(OrdersMaker.Roll(40, new WorldRandom((ulong)seed + 10), Rarity.Rare));
                terms.Give(OrdersMaker.Roll(40, new WorldRandom((ulong)seed + 20), Rarity.Rare));
                terms.Give(OrdersMaker.Roll(40, new WorldRandom((ulong)seed + 30), Rarity.Rare));
                terms.Sanitize();
                worlds.Get(terms.Shape(Home)).Wait();

                using var lab = new GameLab(worlds, Home, "BanditCamp", terms);
                LabPlayer player = lab.AddPlayer("Hero", Line, 1, hero: Sturdy());
                lab.Run(25);
                report.Check($"{string.Join(", ", terms.Chosen().Select(c => c.Omen.Name))}: a fight goes on under them, and nothing breaks", player.DisconnectReason == null && lab.Server.Session != null);
                report.Check("And the client is still in step", player.Client.State == ClientState.Playing && player.Client.Session!.Terms.Chosen().SequenceEqual(lab.Server.Session!.Terms.Chosen()));
                report.Note($"Worst tick {lab.TickCostWorst:0.0} ms, on average {lab.TickCostTotal / Math.Max(lab.TicksTimed, 1):0.00} ms");
            }
        }
    }
}
