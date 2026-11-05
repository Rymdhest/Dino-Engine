using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// What's paid at a waygate, for real, on a running server with a client on the other end of a line: who pays and when they can't, what it does to what's standing on the land, what's
    /// lost by falling, what's paid out at the end, and that all of it survives a save and a friend turning up.
    /// </summary>
    internal static class OfferingTests
    {
        private static readonly WorldRequest Home = new WorldRequest(303, "isle", 6);
        private static readonly WorldRequest Road = new WorldRequest(404, "isle", 7);
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.01 };

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;
            public readonly ServerPlayer Truth;

            public Scene(WorldCache worlds, WorldRequest request, Offering? offering = null, HeroState? hero = null, SaveGame? save = null)
            {
                Lab = new GameLab(worlds, request, "Landing", offering, save);
                Player = Lab.AddPlayer("Hero", Line, 1, hero: hero ?? GameLab.StrongHero());
                Player.Fights = false;
                Lab.Run(4);
                Truth = Lab.Server.Heroes[0];
            }

            public ServerSession Session => Lab.Server.Session!;
            public GameSession Seen => Player.Client.Session!;
            public Actor Hero => Truth.Actor;

            public void Fall()
            {
                Session.Combat.Harm(Hero, Hero, 1e9f, DamageType.Physical);
                Lab.Run(HeroRules.DeathDelay + 1.5);
            }

            public void Go(Vector2 to)
            {
                Player.Client.TravelTo(to);
                Lab.Run(1.5);
            }

            public List<Actor> Members(SiteState state)
            {
                var found = new List<Actor>();
                foreach (Entity entity in state.Live)
                {
                    EntityView view = Lab.Server.Ecs.GetEntityView(entity);
                    if (view.Has<ActorComponent>()) found.Add(view.Get<ActorComponent>().Actor);
                }
                return found;
            }

            // Takes the land's keep, every last one of them, the way it's really done.
            public void Liberate()
            {
                SiteState keep = Session.Sites.First(s => s.Site.Kind == Dino_Engine.World.Content.SiteKind.Keep);
                Go(keep.Site.Position);
                for (int attempt = 0; attempt < 60 && !Session.Liberated; attempt++)
                {
                    foreach (Actor actor in Members(keep)) Session.Combat.Harm(Hero, actor, 1e12f, DamageType.Magic);
                    Lab.Run(0.3);
                }
                Lab.Run(1.5);
            }

            public void Dispose() => Lab.Dispose();
        }

        private static HeroState Rich(int proven = 30, int gold = 50000000)
        {
            HeroState hero = GameLab.StrongHero();
            hero.Gold = gold;
            hero.PerilProven = proven;
            return hero;
        }

        private static void Step(Scene scene, WorldCache worlds, in WorldRequest request, Offering offering)
        {
            worlds.Get(offering.Shape(request)).Wait();
            scene.Player.Client.StepThroughWaygate(request, offering);
            scene.Lab.Run(7);
        }

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();

            Paying(report, worlds);
            Refusing(report, worlds);
            Strength(report, worlds);
            Crowds(report, worlds);
            Falling(report, worlds);
            Reckoning(report, worlds);
            Keeping(report, worlds);
            Bursting(report, worlds);
            Rallying(report, worlds);
            Land(report, worlds);
            EverythingAtOnce(report, worlds);
            return report.Finish();
        }

        private static void Paying(Report report, WorldCache worlds)
        {
            report.Section("Paying");
            var offering = new Offering { Peril = 2, Teeming = 1, Overrun = 1, Omens = { "bloodthirst" } };
            long price = offering.Price(Road.Level);

            using var scene = new Scene(worlds, Home, hero: Rich());
            int before = scene.Truth.Hero.Gold;
            Step(scene, worlds, Road, offering);

            report.Check("Stepping through takes the party there", scene.Player.Arrivals == 2, $"{scene.Player.Arrivals} arrivals");
            report.Check("The gold is gone, and exactly that much", scene.Truth.Hero.Gold == before - price, $"{before} to {scene.Truth.Hero.Gold}, for {price}");
            report.Check("And the client has heard", scene.Player.Client.Hero.Gold == scene.Truth.Hero.Gold, $"{scene.Player.Client.Hero.Gold}");
            report.Check("The server has what was offered", scene.Session.Offering.Peril == 2 && scene.Session.Offering.Has(Omens.Bloodthirst));
            report.Check("So does the client", scene.Seen.Offering.Peril == 2 && scene.Seen.Offering.Has(Omens.Bloodthirst) && scene.Seen.Offering.Teeming == 1 && scene.Seen.Offering.Overrun == 1);

            // What matters is what a machine with none of this in memory grows, from what it was told.
            WorldData grown = new WorldGenerator().Generate(offering.Shape(Road));
            report.Check("A machine of its own grows the same land from it", Protocol.WorldHash(grown).structure == Protocol.WorldHash(scene.Session.World).structure);
            report.Check("And client and server count the same people", scene.Seen.Sites.Select(s => s.HostilesTotal).SequenceEqual(scene.Session.Sites.Select(s => s.HostilesTotal)));

            LabPlayer friend = scene.Lab.AddPlayer("Friend", Line, 2, hero: GameLab.StrongHero());
            friend.Fights = false;
            scene.Lab.Run(6);
            report.Check("A friend who turns up later is let in", friend.DisconnectReason == null && friend.Arrivals == 1, friend.DisconnectReason ?? $"{friend.Arrivals} arrivals");
            report.Check("And told what they've walked into", friend.Client.Session?.Offering.Peril == 2 && friend.Client.Session.Offering.Has(Omens.Bloodthirst));
            report.Check("Without being charged for it", friend.Client.Hero.Gold == GameLab.StrongHero().Gold);

            int walletBefore = scene.Truth.Hero.Gold;
            var nothing = new Offering();
            Step(scene, worlds, Home, nothing);
            report.Check("Stepping through with nothing offered is free", scene.Truth.Hero.Gold == walletBefore && scene.Player.Arrivals == 3, $"{walletBefore} to {scene.Truth.Hero.Gold}");
            report.Check("And the next land is an ordinary one", !scene.Session.Offering.Any && !scene.Seen.Offering.Any);
        }

        private static void Refusing(Report report, WorldCache worlds)
        {
            report.Section("Being refused");
            var offering = new Offering { Peril = 1 };
            long price = offering.Price(Road.Level);

            using (var scene = new Scene(worlds, Home, hero: Rich(0, (int)price - 1)))
            {
                ServerSession before = scene.Session;
                int gold = scene.Truth.Hero.Gold;
                scene.Player.Client.StepThroughWaygate(Road, offering);
                scene.Lab.Run(3);
                report.Check("A gold piece short and the gate doesn't open", ReferenceEquals(before, scene.Session) && scene.Player.Arrivals == 1);
                report.Check("And nothing's taken", scene.Truth.Hero.Gold == gold);
                report.Check("And they're told why", scene.Seen.Log.Any(line => line.Text.Contains("costs")));
                report.Check("And they're still playing", scene.Player.DisconnectReason == null);
            }

            using (var scene = new Scene(worlds, Home, hero: Rich(0)))
            {
                ServerSession before = scene.Session;
                int gold = scene.Truth.Hero.Gold;
                var tooMuch = new Offering { Peril = Offering.ReachBeyondProven + 1 };
                scene.Player.Client.StepThroughWaygate(Road, tooMuch);
                scene.Lab.Run(3);
                report.Check("Past what a hero can reach the gate doesn't open, whatever they can pay", ReferenceEquals(before, scene.Session) && scene.Truth.Hero.Gold == gold);

                Step(scene, worlds, Road, new Offering { Peril = Offering.ReachBeyondProven });
                report.Check("As far as they can reach, it does", scene.Session.Offering.Peril == Offering.ReachBeyondProven);
            }

            using (var scene = new Scene(worlds, Home, hero: Rich()))
            {
                worlds.Get(offering.Shape(Road)).Wait();
                int gold = scene.Truth.Hero.Gold;
                scene.Player.Client.StepThroughWaygate(Road, offering);
                scene.Player.Client.StepThroughWaygate(Road, offering);
                scene.Lab.Run(7);
                report.Check("Pressing twice pays once", scene.Truth.Hero.Gold == gold - price, $"{gold} to {scene.Truth.Hero.Gold}, for {price}");
                report.Check("And arrives once", scene.Player.Arrivals == 2);
            }
        }

        private static void Strength(Report report, WorldCache worlds)
        {
            report.Section("What peril does");
            var offering = new Offering { Peril = 10 };
            worlds.Get(offering.Shape(Home)).Wait();

            using var plain = new Scene(worlds, Home, hero: Rich());
            using var worse = new Scene(worlds, Home, offering, Rich());

            SiteState keep = worse.Session.Sites.First(s => s.Site.Kind == Dino_Engine.World.Content.SiteKind.Keep);
            int index = worse.Session.Sites.IndexOf(keep);
            plain.Go(keep.Site.Position);
            worse.Go(keep.Site.Position);
            Actor? bossBefore = plain.Members(plain.Session.Sites[index]).FirstOrDefault(a => (a.Nature & Condition.Boss) != 0);
            Actor? bossAfter = worse.Members(keep).FirstOrDefault(a => (a.Nature & Condition.Boss) != 0);
            report.Check("There's a warlord to compare", bossBefore != null && bossAfter != null);
            if (bossBefore != null && bossAfter != null)
            {
                report.Near("The warlord really has that much more health", bossAfter.Sheet.MaxHealth / bossBefore.Sheet.MaxHealth, offering.EnemyHealth, 0.002f);
                report.Check("And is otherwise the same warlord", bossBefore.Loadout.Level == bossAfter.Loadout.Level);
            }

            var pack = worse.Session.Sites.First(s => s.Wild && s.HostilesTotal >= 3);
            worse.Go(pack.Site.Position);
            var kin = worse.Members(pack);
            report.Check("Out in the wild as well", kin.Count > 0 && kin.All(a => a.Sheet.MaxHealth > Bestiary.HealthAt(a.Loadout.Level) * 0.3f * offering.EnemyHealth));

            // A find's worth comes from health, and health is what peril changes. If that weren't taken out again, this would pay for champions and be three times what it should be.
            float Rate(Scene scene)
            {
                Loadout fodder = scene.Session.Bestiary.Shared(Bestiary.Cutthroat, 6, EnemyRank.Normal, Bestiary.NoTrait);
                var actor = new Actor(Team.Enemies, fodder, scene.Truth.Sim.Position, 1f);
                var enemy = new EnemyComponent { Name = "Fodder", Rank = EnemyRank.Normal, Level = 6 };
                int before = scene.Session.Ground.Count;
                Vector2 at = scene.Truth.Sim.Position;
                var ground = new Vector3(at.X, scene.Session.World.Height.Sample(at), at.Y);
                const int kills = 3000;
                for (int i = 0; i < kills; i++) scene.Session.Combat.DropLoot(enemy, ground, actor);
                return (scene.Session.Ground.Count - before) / (float)kills;
            }

            float usual = Rate(plain);
            float paid = Rate(worse);
            float wanted = offering.Boon().Quantity;
            report.Check("Fodder stays fodder however much health it's given", paid / usual > wanted * 0.75f && paid / usual < wanted * 1.35f, $"{paid / usual:0.00}x the finds, wanted about {wanted:0.00}x");
        }

        private static void Crowds(Report report, WorldCache worlds)
        {
            report.Section("More of them");
            var crowd = new Offering { Teeming = 3, Overrun = 3 };
            WorldData usual = worlds.Get(Home).Result;
            int usualCount = usual.Haunts.Sum(h => h.Spawns.Count(s => s.Role.IsHostile()));
            (uint _, uint usualGround) = Protocol.WorldHash(usual);
            int usualPacks = usual.Haunts.Count;
            float usualSites = usual.Sites.Sum(s => s.Spawns.Count(p => p.Role.IsHostile()));

            WorldData more = worlds.Get(crowd.Shape(Home)).Result;
            int moreCount = more.Haunts.Sum(h => h.Spawns.Count(s => s.Role.IsHostile()));
            report.Check("Far more enemies stand on the land", moreCount >= usualCount * 3, $"{usualCount} to {moreCount}");
            report.Check("In more packs", more.Haunts.Count > usualPacks * 1.4f, $"{usualPacks} to {more.Haunts.Count}");
            report.Check("And more hold the places too", more.Sites.Sum(s => s.Spawns.Count(p => p.Role.IsHostile())) > usualSites * 1.3f);
            report.Check("It's the same land under them", Protocol.WorldHash(more).ground == usualGround);

            using var scene = new Scene(worlds, Home, crowd, Rich());
            report.Check("Nobody's any stronger for it", scene.Session.Bestiary.Worn.Count == 0);
            report.Check("Nothing more drops from each", scene.Session.Boon == LootBoon.None);

            SiteState pack = scene.Session.Sites.Where(s => s.Wild && s.HostilesTotal >= 8).OrderBy(s => s.Site.Remoteness).First();
            scene.Go(pack.Site.Position);
            report.Check("A big pack is really standing there", scene.Members(pack).Count == pack.HostilesTotal, $"{scene.Members(pack).Count} of {pack.HostilesTotal}");

            scene.Fall();
            report.Check("With nothing at stake a fall costs nothing", scene.Session.Falls == 0 && !scene.Session.Broken);
        }

        private static void Falling(Report report, WorldCache worlds)
        {
            report.Section("Falling");
            var offering = new Offering { Peril = 4, Omens = { "gilded" } };
            worlds.Get(offering.Shape(Home)).Wait();
            using var scene = new Scene(worlds, Home, offering, Rich());

            report.Check("It starts with everything to lose", scene.Seen.FallsLeft == Offering.AllowedFalls && !scene.Session.Broken);
            for (int fall = 1; fall <= Offering.AllowedFalls; fall++)
            {
                scene.Fall();
                report.Check($"Fall {fall} is counted, on both ends", scene.Session.Falls == fall && scene.Seen.Falls == fall, $"server {scene.Session.Falls}, client {scene.Seen.Falls}");
                report.Check($"And it holds", !scene.Session.Broken && scene.Session.Boon != LootBoon.None);
            }

            report.Check("Still gilded", scene.Session.Gilded);
            scene.Fall();
            report.Check("One more breaks it", scene.Session.Broken && scene.Seen.Broken);
            report.Check("What was paid for is gone", scene.Session.Boon == LootBoon.None && !scene.Session.Gilded);
            report.Check("The land stays as it is", scene.Session.Bestiary.Worn.Count > 0);
            report.Check("And they're told", scene.Seen.Banners.Any(b => b.Title.Contains("broken")));

            int falls = scene.Session.Falls;
            scene.Fall();
            report.Check("After that nothing more is counted", scene.Session.Falls == falls);
        }

        private static void Reckoning(Report report, WorldCache worlds)
        {
            report.Section("Seeing it through");
            var offering = new Offering { Peril = 5 };
            worlds.Get(offering.Shape(Home)).Wait();

            int flawlessFinds;
            using (var scene = new Scene(worlds, Home, offering, Rich(4)))
            {
                scene.Liberate();
                report.Check("The land can be freed", scene.Session.Liberated);
                report.Check("A clean run proves a step past what was offered", scene.Truth.Hero.PerilProven == 6, $"{scene.Truth.Hero.PerilProven}");
                report.Check("And the client knows it", scene.Player.Client.Hero.PerilProven == 6);
                report.Check("And is told", scene.Seen.Banners.Any(b => b.Title.Contains("paid in full")) || scene.Seen.Banners.Any(b => b.Subtitle.Contains("Peril 8")));
                flawlessFinds = scene.Session.Ground.Count;

                int proven = scene.Truth.Hero.PerilProven;
                scene.Fall();
                report.Check("Falling afterwards costs nothing", scene.Session.Falls == 0 && scene.Truth.Hero.PerilProven == proven);
            }

            using (var scene = new Scene(worlds, Home, offering, Rich(4)))
            {
                scene.Fall();
                scene.Liberate();
                report.Check("With a fall it proves only what was offered", scene.Truth.Hero.PerilProven == 5, $"{scene.Truth.Hero.PerilProven}");
            }

            using (var scene = new Scene(worlds, Home, offering, Rich(9)))
            {
                scene.Liberate();
                report.Check("Doing something easier takes nothing away", scene.Truth.Hero.PerilProven == 9);
            }

            using (var scene = new Scene(worlds, Home, offering, Rich(4)))
            {
                for (int i = 0; i <= Offering.AllowedFalls; i++) scene.Fall();
                scene.Liberate();
                report.Check("A broken offering proves nothing", scene.Truth.Hero.PerilProven == 4 && scene.Session.Broken);
                report.Check("And pays no hoard", scene.Session.Ground.Count < flawlessFinds, $"{scene.Session.Ground.Count} finds, against {flawlessFinds}");
            }

            var two = new Offering { Peril = 5 };
            using (var scene = new Scene(worlds, Home, two, Rich(2)))
            {
                LabPlayer friend = scene.Lab.AddPlayer("Friend", Line, 2, hero: Rich(0));
                friend.Fights = false;
                scene.Lab.Run(5);
                scene.Liberate();
                ServerPlayer other = scene.Lab.Server.Heroes.First(h => h != scene.Truth);
                report.Check("Everyone there comes out of it having done it", scene.Truth.Hero.PerilProven == 6 && other.Hero.PerilProven == 6, $"{scene.Truth.Hero.PerilProven} and {other.Hero.PerilProven}");
            }

            using (var scene = new Scene(worlds, Home, new Offering(), Rich(4)))
            {
                scene.Liberate();
                report.Check("An ordinary land proves nothing", scene.Truth.Hero.PerilProven == 4);
            }
        }

        private static void Keeping(Report report, WorldCache worlds)
        {
            report.Section("Keeping it");
            var offering = new Offering { Peril = 6, Teeming = 2, Overrun = 1, Omens = { "keen", "gilded" } };
            worlds.Get(offering.Shape(Home)).Wait();

            var save = new SaveGame();
            int index;
            int deadBefore;
            using (var scene = new Scene(worlds, Home, offering, Rich()))
            {
                SiteState pack = scene.Session.Sites.Where(s => s.Wild && s.HostilesTotal >= 4).OrderByDescending(s => s.Site.Remoteness).First();
                index = scene.Session.Sites.IndexOf(pack);
                pack.Dead.Add(2);
                pack.Dead.Add(3);
                deadBefore = pack.Dead.Count;
                scene.Fall();
                scene.Fall();
                scene.Lab.Server.CaptureWorld(save);
            }

            string file = Path.GetTempFileName();
            SaveGame.PathOverride = file;
            save.Store();
            SaveGame? loaded = SaveGame.Load();
            SaveGame.PathOverride = null;
            File.Delete(file);
            report.Check("It's written down", loaded != null && loaded.Offering.Peril == 6 && loaded.OfferingFalls == 2);
            if (loaded == null) return;

            using var again = new Scene(worlds, new WorldRequest(loaded.WorldSeed, loaded.WorldArchetype, loaded.WorldLevel), loaded.Offering, Rich(), loaded);
            report.Check("Loading it makes the same land", again.Session.World.Haunts.Count == worlds.Get(offering.Shape(Home)).Result.Haunts.Count);
            report.Check("With the same dead in it", index < again.Session.Sites.Count && again.Session.Sites[index].Dead.Count == deadBefore);
            report.Check("Under the same offering", again.Session.Offering.Peril == 6 && again.Session.Offering.Has(Omens.Keen) && again.Session.Bestiary.Worn.Count > 0);
            report.Check("With the same falls behind it", again.Session.Falls == 2 && again.Seen.Falls == 2, $"{again.Session.Falls}, client {again.Seen.Falls}");
            report.Check("And the client has been told all of it", again.Seen.Offering.Peril == 6 && again.Seen.Offering.Teeming == 2);
        }

        private static void Bursting(Report report, WorldCache worlds)
        {
            report.Section("Volatile");
            var offering = new Offering { Omens = { "volatile" } };
            worlds.Get(offering.Shape(Home)).Wait();
            using var arena = new Arena(worlds, Home, Rich(), offering: offering);

            Loadout kind = arena.Session.Bestiary.Shared(Bestiary.Cutthroat, 6, EnemyRank.Normal, Bestiary.NoTrait);
            report.Check("Enemies have it", kind.SheetFor(Condition.None).Has(Rule.BurstsOnDeath));

            Actor first = arena.Place(kind, new Vector2(2f, 0f));
            float before = arena.Hero.Vitals.Health;
            arena.Combat.Harm(arena.Hero, first, 1e9f, DamageType.Magic);
            arena.Run(0.2);
            report.Check("Nothing happens at once", arena.Hero.Vitals.Health >= before - 0.01f, $"{before} to {arena.Hero.Vitals.Health}");
            arena.Run(1.2);
            report.Check("A moment later whoever stayed gets burned", arena.Hero.Vitals.Health < before, $"{before} to {arena.Hero.Vitals.Health}");
            report.Check("And no more than a blow's worth", before - arena.Hero.Vitals.Health < arena.Hero.Sheet.MaxHealth * 0.4f, $"{before - arena.Hero.Vitals.Health} of {arena.Hero.Sheet.MaxHealth}");

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
            report.Check("What's already dead can still kill", arena.Truth.Sim.Down, $"{arena.Hero.Vitals.Health} health");
            arena.Run(HeroRules.DeathDelay + 1.0);
            report.Check("And the hero gets up as they always do", !arena.Truth.Sim.Down && arena.Hero.Vitals.Health > 0f);
            report.Check("And it counts as a fall", arena.Session.Falls == 1, $"{arena.Session.Falls}");
        }

        private static void Rallying(Report report, WorldCache worlds)
        {
            report.Section("Rallying");
            var offering = new Offering { Omens = { "rallying" } };
            worlds.Get(offering.Shape(Home)).Wait();
            using var arena = new Arena(worlds, Home, Rich(), offering: offering);

            Loadout kind = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 6, EnemyRank.Normal, Bestiary.NoTrait);
            Actor near = arena.Place(kind, new Vector2(4f, 0f));
            Actor far = arena.Place(kind, new Vector2(30f, 0f));
            float hit = near.Sheet.Profile(SkillCatalog.Strike)!.AverageHit;

            for (int i = 0; i < 7; i++)
            {
                Actor victim = arena.Place(kind, new Vector2(4f, 3f));
                arena.Combat.Harm(arena.Hero, victim, 1e9f, DamageType.Magic);
                arena.Run(0.2);
            }

            report.Check("Those around it have heard", near.Buffs.Any(b => b.Buff == BuffCatalog.Rallied));
            report.Check("And it stacks only so far", near.Buffs.First(b => b.Buff == BuffCatalog.Rallied).Stacks == BuffCatalog.Rallied.MostStacks);
            report.Check("And they really hit harder", near.Sheet.Profile(SkillCatalog.Strike)!.AverageHit > hit * 1.4f, $"{hit} to {near.Sheet.Profile(SkillCatalog.Strike)!.AverageHit}");
            report.Check("Those too far off haven't", far.Buffs.Count == 0);
            report.Check("Heroes get nothing from it", arena.Hero.Buffs.All(b => b.Buff != BuffCatalog.Rallied));

            report.Check("And being rallied leaves them what they are", near.Sheet.Has(Rule.RalliesOnDeath));

            Actor last = arena.Place(kind, new Vector2(-4f, 0f));
            arena.Combat.Harm(arena.Hero, near, 1e9f, DamageType.Magic);
            arena.Run(0.3);
            report.Check("So one who was rallied rallies others in turn", last.Buffs.Any(b => b.Buff == BuffCatalog.Rallied));

            arena.Run(BuffCatalog.Rallied.Duration + 1.0);
            report.Check("It wears off", last.Buffs.Count == 0 && MathF.Abs(last.Sheet.Profile(SkillCatalog.Strike)!.AverageHit - hit) < 0.01f);
        }

        private static void Land(Report report, WorldCache worlds)
        {
            report.Section("What changes the land");

            var night = new Offering { Omens = { "night" } };
            worlds.Get(night.Shape(Home)).Wait();
            HeroState nightwalker = Rich();
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
            worlds.Get(Home).Wait();
            using (var scene = new Scene(worlds, Home, hero: Rich()))
            {
                Vector2 open = scene.Truth.Sim.Position;
                report.Check("Not so on an ordinary land", !scene.Session.Combat.Land.At(open).HasFlag(Condition.InDarkness));
            }

            var host = new Offering { Omens = { "warhost" } };
            WorldData world = worlds.Get(host.Shape(Home)).Result;
            (int led, int elite) Count(Offering offering)
            {
                int leaders = 0;
                int elites = 0;
                for (int i = 0; i < world.Haunts.Count; i++)
                {
                    WorldSite haunt = world.Haunts[i];
                    var random = new WorldRandom((ulong)i);
                    var orders = new PackOrders(world, haunt, haunt.Folk, 10, true, random.Fork("pack"), offering);
                    (EnemyKind kind, EnemyRank rank, _) = orders.Draw(0, random.Fork("kind"));
                    if (kind.Health >= 1.5f || rank == EnemyRank.Elite) leaders++;
                    if (rank == EnemyRank.Elite) elites++;
                }
                return (leaders, elites);
            }

            (int usualLed, int usualElite) = Count(new Offering());
            (int hostLed, int hostElite) = Count(host);
            report.Check("Under a warhost, far more packs are led", hostLed > usualLed * 1.3f, $"{usualLed} to {hostLed} of {world.Haunts.Count}");
            report.Check("And far more of those who lead are elite", hostElite > usualElite * 1.8f, $"{usualElite} to {hostElite}");

            var gilded = new Offering { Omens = { "gilded" } };
            worlds.Get(gilded.Shape(Home)).Wait();
            using (var scene = new Scene(worlds, Home, gilded, Rich()))
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
                    report.Check("A camp's box gives what a place's chest does", found >= (int)LootTables.Of(LootTier.Chest).Finds, $"{found} finds");
                    report.Check("At the least the rare a place's chest promises", scene.Session.Ground.All.Any(lying => Loot.GradeOf(lying.item) >= LootGrade.Rare));
                }
            }
        }

        // Everything that can be asked for together, fought for a while by a real client, to see that nothing falls over.
        private static void EverythingAtOnce(Report report, WorldCache worlds)
        {
            report.Section("Everything at once");
            foreach (string[] omens in new[]
            {
                new[] { "ironhide", "bloodthirst", "quickened", "ashfall", "hoarfrost" },
                new[] { "stormfront", "leeching", "briars", "keen", "volatile" },
                new[] { "rallying", "night", "warhost", "gilded", "volatile" },
            })
            {
                var offering = new Offering { Peril = 12, Teeming = 3, Overrun = 3, Omens = omens.ToList() };
                report.Check($"{offering.OmenNames()} can all be carried", offering.Omens.Count == Offering.MostOmens && offering.Omens.Count <= Offering.OmenSlots(12));
                worlds.Get(offering.Shape(Home)).Wait();

                using var lab = new GameLab(worlds, Home, "BanditCamp", offering);
                LabPlayer player = lab.AddPlayer("Hero", Line, 1, hero: Rich());
                lab.Run(25);
                report.Check("A fight goes on under them, and nothing breaks", player.DisconnectReason == null && lab.Server.Session != null);
                report.Check("And the client is still in step", player.Client.State == Dino_Defenders.Client.ClientState.Playing);
                report.Note($"Worst tick {lab.TickCostWorst:0.0} ms, on average {lab.TickCostTotal / Math.Max(lab.TicksTimed, 1):0.00} ms");
            }
        }
    }
}
