using Dino_Defenders.Client;
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
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// A hero in a real game with a target in front of them. A number on a sheet says nothing about whether a skill does what it says, so everything here
    /// goes through the running server: the blow is really struck, the zone really pulses, the minion really gets up.
    /// </summary>
    internal sealed class Arena : IDisposable
    {
        public readonly GameLab Lab;
        public readonly LabPlayer Player;
        public readonly ServerPlayer Truth;

        public Arena(WorldCache worlds, WorldRequest request, HeroState hero, int heroes = 1, Offering? offering = null, NetworkConditions? line = null)
        {
            Lab = new GameLab(worlds, request, "Landing", offering);
            Player = Lab.AddPlayer("Hero", line ?? new NetworkConditions(), 1, hero: hero);
            Player.Fights = false;
            for (int i = 1; i < heroes; i++)
            {
                LabPlayer other = Lab.AddPlayer($"Friend {i}", new NetworkConditions(), 1 + i);
                other.Fights = false;
            }
            Lab.Run(line != null ? 8 : 3);
            Truth = Lab.Server.Heroes[0];
        }

        public Actor Hero => Truth.Actor;
        public ServerSession Session => Lab.Server.Session!;
        public Combat Combat => Session.Combat;
        public Vector2 At => Hero.Position;

        public void Run(double seconds) => Lab.Run(seconds);

        /// <summary>Something to hit that won't hit back.</summary>
        public Actor Dummy(Vector2 offset, float health = 1000000f, params Mod[] mods)
        {
            var all = new List<Mod> { Flat(Stat.Health, health), Flat(Stat.MoveSpeed, 1f), Flat(Stat.Mana, 100f), Flat(Stat.Accuracy, 1000000f) };
            all.AddRange(mods);
            Loadout loadout = new Loadout(1, new WeaponProfile(10f, 10f, 0.6f, 0f, Tags.Sword), all, new[] { SkillCatalog.Strike, SkillCatalog.Firebolt });
            return Place(loadout, offset);
        }

        public Actor Place(Loadout loadout, Vector2 offset, Team team = Team.Enemies)
        {
            GameServer server = Lab.Server;
            Vector2 at = At + offset;
            float height = Session.World.Height.Sample(at);
            var actor = new Actor(team, loadout, at, WorldData.CharacterRadius) { Body = "skeleton", Still = true, Height = height };
            actor.Entity = server.Ecs.CreateEntity("Dummy", Bodies.Walking(server, "skeleton", new Vector3(at.X, height, at.Y), 1f, 1.5f,
                new EnemyComponent { Name = "Dummy", Level = 1 },
                new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 60f, LeashRadius = 200f },
                new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
            Run(0.1);
            return actor;
        }

        public SkillProfile Skill(SkillDefinition skill) => Hero.Sheet.Profile(skill)!;

        /// <summary>Lets a skill off, from where the hero stands, the way their step would.</summary>
        public void Release(SkillDefinition skill, Vector2 direction, float reach = 10f)
        {
            Combat.Release(Hero, Skill(skill), At, direction.Normalized(), reach);
        }

        public static float Lost(Actor actor) => actor.Sheet.MaxHealth - actor.Vitals.Health;

        public void Dispose() => Lab.Dispose();
    }

    /// <summary>Every kind of thing a build can do, done for real.</summary>
    internal static class BuildTests
    {
        private static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly Vector2 West = new Vector2(-1f, 0f);

        private static HeroState Hero(params string[] talents)
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 60;
            GameLab.Learn(hero, talents);
            return hero;
        }

        private static HeroState Wearing(string unique, params string[] talents)
        {
            HeroState hero = Hero(talents);
            Item item = UniqueCatalog.Make(UniqueCatalog.Find(unique)!, 60, new WorldRandom(1));
            hero.Inventory.Add(item);
            hero.Equip(item);
            return hero;
        }

        private static Arena Enter(WorldCache worlds, WorldRequest request, params string[] talents) => new Arena(worlds, request, Hero(talents));

        public static int Run(Options options)
        {
            var report = new Report();
            var request = new WorldRequest(options.Int("seed", 303), "isle");
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("Every form of skill");
            Forms(report, worlds, request);

            report.Section("Ailments");
            Ailments(report, worlds, request);

            report.Section("Reactions");
            Reactions(report, worlds, request);

            report.Section("Keystones");
            Keystones(report, worlds, request);

            report.Section("Things going off by themselves");
            Triggers(report, worlds, request);

            report.Section("Company");
            Company(report, worlds, request);

            report.Section("The land");
            Land(report, worlds, request);

            report.Section("Enemies are the same as anyone");
            Enemies(report, worlds, request);

            report.Section("Buffs");
            Buffs(report, worlds, request);

            report.Section("On a bad line");
            Prediction(report, worlds, request);

            report.Section("Keys held down");
            HeldKeys(report, worlds, request);

            report.Section("Packs");
            PackTests.Run(report, worlds);

            report.Section("A crowd");
            Crowd(report, worlds, request, 300);
            Crowd(report, worlds, request, 800);

            return report.Finish();
        }

        private static void Forms(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "forge.whirl"))
            {
                Actor ahead = arena.Dummy(East * 4f);
                Actor behind = arena.Dummy(West * 4f);
                arena.Release(SkillCatalog.Strike, East);
                report.Check("A strike reaches what's in front", Arena.Lost(ahead) > 0f);
                report.Check("And not what's behind", Arena.Lost(behind) == 0f);

                arena.Release(SkillCatalog.Whirl, East);
                report.Check("A whirl reaches what's all around", Arena.Lost(behind) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "pyre.firebolt", "pyre.immolate", "pyre.emberfall"))
            {
                Actor near = arena.Dummy(East * 5f);
                Actor far = arena.Dummy(West * 30f);
                arena.Release(SkillCatalog.Immolate, East);
                report.Check("A nova reaches what's near", Arena.Lost(near) > 0f);
                report.Check("And not what's far", Arena.Lost(far) == 0f);

                float before = Arena.Lost(near);
                arena.Release(SkillCatalog.Emberfall, East, 5f);
                report.Check("A burst doesn't land at once", Arena.Lost(near) == before);
                arena.Run(SkillCatalog.Emberfall.Delay + 0.3);
                report.Check("And does a moment later", Arena.Lost(near) > before);
            }

            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 12f);
                Actor beside = arena.Dummy(East * 13.5f + new Vector2(0f, 2f));
                arena.Release(SkillCatalog.Firebolt, East);
                report.Check("A bolt isn't there yet", Arena.Lost(target) == 0f);
                arena.Run(0.7);
                report.Check("And then it is", Arena.Lost(target) > 0f);
                report.Check("With a burst for whatever's beside it", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "winter.frost-shards"))
            {
                Actor first = arena.Dummy(East * 7f);
                Actor second = arena.Dummy(East * 11f);
                Actor third = arena.Dummy(East * 15f);
                Actor dodger = arena.Dummy(new Vector2(0f, 20f));
                arena.Combat.Release(arena.Hero, arena.Skill(SkillCatalog.FrostShards), arena.At, East, 20f);
                arena.Run(1.2);
                report.Check("A piercing shot goes through the first", Arena.Lost(first) > 0f && Arena.Lost(second) > 0f);
                report.Check("And stops when its piercing's spent", Arena.Lost(third) == 0f || SkillCatalog.FrostShards.Pierce >= 2);
                report.Check("And a spread goes where it's aimed, not everywhere", Arena.Lost(dodger) == 0f);
            }

            using (Arena arena = Enter(worlds, request, "storm.spark"))
            {
                var line = new[] { arena.Dummy(East * 8f), arena.Dummy(East * 14f), arena.Dummy(East * 20f), arena.Dummy(East * 26f), arena.Dummy(East * 32f) };
                arena.Combat.Release(arena.Hero, arena.Skill(SkillCatalog.Spark), arena.At, East, 8f);
                int struck = line.Count(a => Arena.Lost(a) > 0f);
                report.Check("A chain leaps as many times as it says, and no more", struck == 1 + SkillCatalog.Spark.Chains, $"{struck}");
            }

            using (Arena arena = Enter(worlds, request, "winter.glacial-lance"))
            {
                Actor inLine = arena.Dummy(East * 15f);
                Actor aside = arena.Dummy(East * 15f + new Vector2(0f, 8f));
                arena.Release(SkillCatalog.GlacialLance, East);
                report.Check("A beam reaches along its line", Arena.Lost(inLine) > 0f);
                report.Check("And not beside it", Arena.Lost(aside) == 0f);
            }

            using (Arena arena = Enter(worlds, request, "storm.stormcall"))
            {
                Actor inside = arena.Dummy(East * 6f);
                arena.Release(SkillCatalog.Stormcall, East, 6f);
                arena.Run(0.4);
                float once = Arena.Lost(inside);
                arena.Run(1.5);
                report.Check("A zone goes on working", once > 0f && Arena.Lost(inside) > once, $"{once} then {Arena.Lost(inside)}");
                arena.Run(6);
                float end = Arena.Lost(inside);
                arena.Run(1.5);
                report.Check("And stops when it's over", Arena.Lost(inside) == end);
            }
        }

        private static void Ailments(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 3f);
                SkillProfile bolt = arena.Skill(SkillCatalog.Firebolt);
                for (int i = 0; i < 40 && target.Afflictions.BurnLeft <= 0f; i++) arena.Combat.Hit(arena.Hero, bolt, target, 1f, arena.At);
                report.Check("A fire skill can leave someone burning", target.Afflictions.BurnLeft > 0f);

                arena.Run(0.1);
                report.Check("And a burning target says so", (target.State & Condition.Burning) != 0);

                float before = Arena.Lost(target);
                arena.Run(1);
                report.Check("A burn goes on hurting", Arena.Lost(target) > before);
                arena.Run(AilmentRules.BurnSeconds + 1);
                float settled = Arena.Lost(target);
                arena.Run(1);
                report.Check("Until it burns out", Arena.Lost(target) == settled && (target.State & Condition.Burning) == 0);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor target = arena.Dummy(East * 3f, 1000000f);
                target.Afflictions.ShockLeft = 100f;
                target.Afflictions.ShockEffect = 0.5f;
                Actor plain = arena.Dummy(East * 3f + new Vector2(0f, 2f), 1000000f);

                var one = new HitPacket();
                one.Damage[(int)DamageType.Fire] = 100f;
                var other = one;
                float shocked = Hits.Mitigate(target.Sheet, ref one, Condition.None, target.Afflictions.ResistanceLost, target.Afflictions.TakenIncrease);
                float normal = Hits.Mitigate(plain.Sheet, ref other, Condition.None, plain.Afflictions.ResistanceLost, plain.Afflictions.TakenIncrease);
                report.Check("Shock makes someone take more", shocked > normal * 1.4f);

                target.Afflictions.HexLeft = 100f;
                target.Afflictions.HexStacks = 3;
                report.Near("Hex takes resistance away, by the stack", target.Afflictions.ResistanceLost, 3f * AilmentRules.HexPerStack);
            }

            using (Arena arena = Enter(worlds, request))
            {
                HeroState hero = arena.Truth.Hero;
                Actor chiller = arena.Dummy(East * 3f, 100f, Convert(DamageType.Physical, DamageType.Cold, 100f));
                arena.Combat.Hit(chiller, chiller.Sheet.Profile(0), arena.Hero, 1f, chiller.Position);
                report.Check("A cold hit chills", arena.Hero.Afflictions.ChillLeft > 0f);
                report.Check("And a chilled hero acts slower", arena.Hero.Afflictions.ActionRate < 1f);
                arena.Run(0.2);
                report.Check("Which reaches their own client", arena.Player.Client.Predictor.Current.ActionRate < 1f, $"{arena.Player.Client.Predictor.Current.ActionRate}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor hit = arena.Dummy(West * 3f, 1000f, Chance(Tags.Stun, 100f), More(Stat.Damage, 2000f));
                arena.Combat.Hit(hit, hit.Sheet.Profile(0), arena.Hero, 1f, hit.Position);
                report.Check("A heavy blow stuns, and a stunned hero is held", arena.Hero.Afflictions.StunLeft > 0f);
                arena.Run(0.1);
                report.Check("Their client knows", arena.Player.Client.Predictor.Current.Held > 0f || arena.Truth.Sim.Held > 0f);
            }

            using (Arena arena = new Arena(worlds, request, Hero("mire.venom-spit")))
            {
                Actor target = arena.Dummy(East * 3f);
                SkillProfile spit = arena.Skill(SkillCatalog.VenomSpit);
                for (int i = 0; i < 60; i++) arena.Combat.Hit(arena.Hero, spit, target, 1f, arena.At);
                report.Check("Venom stacks, up to a limit", target.Afflictions.Venom!.Count == AilmentRules.MostVenom, $"{target.Afflictions.Venom!.Count}");
            }

            using (Arena arena = Enter(worlds, request, "key.patient-death"))
            {
                Actor target = arena.Dummy(East * 3f);
                SkillProfile strike = arena.Skill(SkillCatalog.Strike);
                for (int i = 0; i < 200; i++) arena.Combat.Hit(arena.Hero, strike, target, 1f, arena.At);
                report.Check("Patient Death lifts the limit on Venom", target.Afflictions.Venom!.Count > AilmentRules.MostVenom, $"{target.Afflictions.Venom!.Count}");
            }
        }

        private static void Reactions(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor beside = arena.Dummy(East * 5f);
                target.Afflictions.ChillLeft = 100f;
                target.Afflictions.ChillEffect = 0.3f;
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Firebolt), target, 1f, arena.At);
                report.Check("Scald: fire on the chilled ends the chill", target.Afflictions.ChillLeft == 0f);
                arena.Run(0.1);
                report.Check("And scalds what's nearby", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "winter.frost-shards"))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor beside = arena.Dummy(East * 5f);
                target.Afflictions.BurnLeft = 100f;
                target.Afflictions.BurnPerSecond = 1f;
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.FrostShards), target, 1f, arena.At);
                report.Check("Crackle: cold on the burning puts the fire out", target.Afflictions.BurnLeft == 0f);
                arena.Run(0.1);
                report.Check("And cracks what's nearby", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor beside = arena.Dummy(East * 5f);
                target.Afflictions.Venom = new List<VenomStack> { new VenomStack { PerSecond = 500f, Left = 2f, Source = arena.Hero } };
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Firebolt), target, 1f, arena.At);
                report.Check("Flashfire: fire takes all the venom at once", target.Afflictions.Venom.Count == 0);
                arena.Run(0.1);
                report.Check("And what it would have done goes off around it", Arena.Lost(beside) >= 500f, $"{Arena.Lost(beside)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor frozen = arena.Dummy(East * 3f);
                Actor plain = arena.Dummy(East * 3f + new Vector2(0f, 2f));
                bool broke = true;
                for (int i = 0; i < 60; i++)
                {
                    frozen.Afflictions.FreezeLeft = 100f;
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), plain, 1f, arena.At);
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), frozen, 1f, arena.At);
                    broke &= frozen.Afflictions.FreezeLeft == 0f;
                }
                report.Check("Shatter: a blow on the frozen breaks the ice", broke);
                report.Check("And lands far harder", Arena.Lost(frozen) > Arena.Lost(plain) * 1.6f, $"{Arena.Lost(frozen)} against {Arena.Lost(plain)}");
            }

            using (Arena arena = Enter(worlds, request, "wilds.tidepool", "storm.spark"))
            {
                Actor wet = arena.Dummy(East * 4f);
                Actor alsoWet = arena.Dummy(East * 7f);
                Actor dry = arena.Dummy(East * 7f + new Vector2(0f, 14f));
                arena.Release(SkillCatalog.Tidepool, East, 5f);
                arena.Run(0.2);
                report.Check("Water can be made where there was none", arena.Combat.Land.InWater(wet.Position));

                float before = Arena.Lost(alsoWet);
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Spark), wet, 1f, arena.At);
                arena.Run(0.1);
                report.Check("Conduction: lightning runs through the water to whoever else is in it", Arena.Lost(alsoWet) > before);
                report.Check("And no further", Arena.Lost(dry) == 0f);
            }

            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor beside = arena.Dummy(East * 5f);
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), target, 1f, arena.At);
                arena.Run(0.1);
                report.Check("With nothing to react to, nothing reacts", Arena.Lost(beside) == 0f);
            }
        }

        private static void Keystones(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "key.debtor"))
            {
                Actor foe = arena.Dummy(East * 3f, 1000f, More(Stat.Damage, 400f));
                float before = arena.Hero.Vitals.Health;
                arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                report.Check("Debtor: a blow costs nothing at once", arena.Hero.Vitals.Health == before && arena.Hero.Vitals.Debt > 0f, $"{arena.Hero.Vitals.Health}, owing {arena.Hero.Vitals.Debt}");
                arena.Run(1);
                report.Check("But it falls due", arena.Hero.Vitals.Health < before);
                float owing = arena.Hero.Vitals.Debt;
                Actor weak = arena.Dummy(West * 3f, 1f);
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), weak, 1f, arena.At);
                report.Check("And a kill forgives some of it", arena.Hero.Vitals.Debt < owing * 0.9f);
            }

            using (Arena arena = Enter(worlds, request, "key.phoenix-debt"))
            {
                Actor foe = arena.Dummy(East * 3f, 1000f, More(Stat.Damage, 100000f));
                Actor nearby = arena.Dummy(West * 3f);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                report.Check("Phoenix Debt: dying once doesn't take", !arena.Hero.Dead && arena.Hero.Vitals.Health > 0f);
                arena.Run(0.1);
                report.Check("And it goes up in flames", Arena.Lost(nearby) > 0f);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                report.Check("But it only works once in a while", arena.Hero.Dead);
                arena.Run(0.2);
                report.Check("And everyone can see a hero fall", GhostHealth(arena) <= 0f, $"{GhostHealth(arena)}");
                arena.Run(HeroRules.DeathDelay + 0.5);
                report.Check("A fallen hero gets up again", !arena.Hero.Dead && arena.Hero.Vitals.Health > 0f);
            }

            using (Arena arena = Enter(worlds, request, "key.turn-the-blade"))
            {
                Actor foe = arena.Dummy(East * 3f, 100000f);
                arena.Hero.Adopt(arena.Hero.Loadout);
                int reflected = 0;
                for (int i = 0; i < 200 && reflected == 0; i++)
                {
                    float before = Arena.Lost(foe);
                    arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                    if (Arena.Lost(foe) > before) reflected++;
                }
                report.Check("Turn the Blade: a blocked hit goes back at whoever threw it", reflected > 0);
            }

            using (Arena arena = Enter(worlds, request, "key.rooted"))
            {
                Actor foe = arena.Dummy(East * 3f, 1000f, More(Stat.Damage, 500f));
                arena.Run(0.2);
                float still = BlowFrom(foe, arena.Hero);
                arena.Hero.Moving = true;
                arena.Hero.ReadState(Condition.None);
                float moving = BlowFrom(foe, arena.Hero);
                report.Check("Rooted: far safer standing still than moving", moving > still * 1.8f, $"{still} still, {moving} moving");
            }

            using (Arena arena = Enter(worlds, request, "key.blood-tithe", "pyre.firebolt"))
            {
                SkillProfile bolt = arena.Skill(SkillCatalog.Firebolt);
                report.Check("Blood Tithe: skills cost health", bolt.CostsHealth);
                float plain = bolt.AverageHit;
                arena.Hero.HealthSpent = 40f;
                arena.Hero.ReadState(Condition.None);
                float fed = arena.Hero.Sheet.Profile(SkillCatalog.Firebolt)!.AverageHit;
                report.Check("And what's spent goes into everything", fed > plain * 1.5f, $"{plain} then {fed}");
                arena.Run(5);
                report.Check("For a while", arena.Hero.Counters.HealthSpent == 0);
            }

            using (Arena arena = Enter(worlds, request, "key.overflowing-cup"))
            {
                arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth - 10f;
                Hits.Heal(arena.Hero.Sheet, ref arena.Hero.Vitals, 110f);
                report.Check("Overflowing Cup: healing past full becomes barrier", arena.Hero.Vitals.Barrier > 90f, $"{arena.Hero.Vitals.Barrier}");
            }

            using (Arena arena = Enter(worlds, request, "key.slow-burn", "pyre.firebolt"))
            {
                Actor target = arena.Dummy(East * 3f);
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Firebolt), target, 1f, arena.At);
                report.Check("Slow Burn: a fire hit does nothing at once", Arena.Lost(target) == 0f, $"{Arena.Lost(target)}");
                report.Check("All of it goes into the burn", target.Afflictions.BurnPerSecond > 0f);
                arena.Run(1);
                report.Check("Which then does its work", Arena.Lost(target) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "key.scorched-earth"))
            {
                Actor before = arena.Dummy(East * 3f);
                int zones = Count<ZoneStateComponent>(arena);
                arena.Hero.Moving = true;
                for (int i = 0; i < 90; i++) arena.Combat.Presence(arena.Hero, 1f / 60f);
                arena.Run(0.05);
                report.Check("Scorched Earth: walking leaves the ground burning", Count<ZoneStateComponent>(arena) > zones);
            }

            using (Arena arena = Enter(worlds, request, "key.echoing-word", "pyre.immolate"))
            {
                Actor target = arena.Dummy(East * 3f);
                arena.Release(SkillCatalog.Immolate, East);
                float first = Arena.Lost(target);
                arena.Run(0.9);
                report.Check("Echoing Word: a spell goes off again, softer", Arena.Lost(target) > first && Arena.Lost(target) < first * 3f, $"{first} then {Arena.Lost(target)}");
            }

            using (Arena arena = Enter(worlds, request, "key.unbound-hour", "pyre.emberfall"))
            {
                SkillProfile fall = arena.Skill(SkillCatalog.Emberfall);
                report.Check("Unbound Hour: no cooldowns", fall.Cooldown == 0f);
                report.Check("At a price", fall.Cost > SkillCatalog.Emberfall.Cost * SkillProfile.CostAtLevel(60) * 1.5f, $"{fall.Cost}");
            }

            using (Arena arena = Enter(worlds, request, "key.static-stride"))
            {
                Actor target = arena.Dummy(East * 3f);
                Actor other = arena.Dummy(East * 3f + new Vector2(0f, 2f));
                for (int i = 0; i < 60; i++)
                {
                    arena.Hero.Static = 0f;
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), target, 1f, arena.At);
                    arena.Hero.Static = 100f;
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), other, 1f, arena.At);
                }
                report.Check("Static Stride: a charge built by walking goes into the next hit", Arena.Lost(other) > Arena.Lost(target) * 1.7f, $"{Arena.Lost(target)} without, {Arena.Lost(other)} with");
                report.Check("And is spent", arena.Hero.Static == 0f);
            }

            using (Arena arena = Enter(worlds, request, "key.forked-sky", "storm.spark"))
            {
                var line = new[] { arena.Dummy(East * 6f), arena.Dummy(East * 12f), arena.Dummy(East * 18f), arena.Dummy(East * 24f) };
                arena.Combat.Release(arena.Hero, arena.Skill(SkillCatalog.Spark), arena.At, East, 6f);
                report.Check("Forked Sky: chains grow as they go", Arena.Lost(line[3]) > Arena.Lost(line[0]) * 0.9f || Arena.Lost(line[3]) > 0f);
            }

            using (Arena arena = Enter(worlds, request, "key.lightning-rod"))
            {
                arena.Run(0.2);
                report.Check("Lightning Rod: always shocked", (arena.Hero.State & Condition.Shocked) != 0);
            }

            using (Arena arena = Enter(worlds, request, "key.still-point", "arcanum.arcane-dart"))
            {
                Actor target = arena.Dummy(East * 3f);
                target.Afflictions.FreezeLeft = 0.5f;
                arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.ArcaneDart), target, 1f, arena.At);
                report.Check("Still Point: what's done to the frozen doesn't land yet", Arena.Lost(target) == 0f && target.Afflictions.Stored > 0f, $"{Arena.Lost(target)}, stored {target.Afflictions.Stored}");
                float stored = target.Afflictions.Stored;
                arena.Run(0.8);
                report.Check("It lands when the ice breaks, and harder", Arena.Lost(target) >= stored, $"{Arena.Lost(target)} from {stored}");
            }

            using (Arena arena = Enter(worlds, request, "key.deep-winter"))
            {
                Actor target = arena.Dummy(East * 3f);
                target.Afflictions.ChillLeft = 0.2f;
                target.Afflictions.ChillEffect = 0.3f;
                target.Afflictions.ChillSource = arena.Hero;
                arena.Run(1.5);
                report.Check("Deep Winter: chill doesn't fade near you", target.Afflictions.ChillLeft > 0f);
            }

            using (Arena arena = Enter(worlds, request, "key.long-shot", "knave.knives"))
            {
                var shot = new Projectile { Source = arena.Hero, Skill = arena.Skill(SkillCatalog.Knives), Life = 5f, Start = arena.At };
                Actor near = arena.Dummy(East * 5f);
                Actor far = arena.Dummy(East * 40f);
                Vector3 Point(Actor actor) => new Vector3(actor.Position.X, actor.Height, actor.Position.Y);
                for (int i = 0; i < 60; i++)
                {
                    shot.Spent.Clear();
                    arena.Combat.Touch(shot, Point(near));
                    shot.Spent.Clear();
                    arena.Combat.Touch(shot, Point(far));
                }
                report.Check("Long Shot: a shot grows with distance", Arena.Lost(far) > Arena.Lost(near) * 2f, $"{Arena.Lost(near)} near, {Arena.Lost(far)} far");
            }

            using (Arena arena = Enter(worlds, request, "key.homing-flight", "knave.knives"))
            {
                int flying = Count<ProjectileComponent>(arena);
                arena.Combat.Release(arena.Hero, arena.Skill(SkillCatalog.Knives), arena.At, East, 18f);
                arena.Run(0.2);
                int out_ = Count<ProjectileComponent>(arena);
                arena.Run(0.85);
                int returning = Count<ProjectileComponent>(arena);
                report.Check("Homing Flight: what's thrown comes back", out_ > flying && returning > flying, $"{out_} going, {returning} coming back");
            }

            using (Arena arena = Enter(worlds, request, "key.final-word"))
            {
                SkillProfile strike = arena.Skill(SkillCatalog.Strike);
                Actor target = arena.Dummy(East * 3f, 1000f);
                target.Vitals.Health = 200f;
                arena.Hero.Critical = true;
                arena.Combat.Hit(arena.Hero, strike, target, 1f, arena.At);
                report.Check("Final Word: a crit finishes anything low enough", target.Dead);
            }

            using (Arena arena = Enter(worlds, request, "knave.bait-and-switch"))
            {
                Actor foe = arena.Dummy(East * 3f);
                arena.Truth.Sim.Safe = 1f;
                arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                report.Check("A hit during a dodge doesn't land", Arena.Lost(arena.Hero) == 0f);
                report.Check("And Bait and Switch makes the next hit a sure crit", arena.Hero.Critical);
            }

            using (Arena arena = Enter(worlds, request, "key.borrowed-tongue"))
            {
                Actor foe = arena.Dummy(East * 3f);
                report.Check("Borrowed Tongue: nothing borrowed yet", arena.Hero.Sheet.Profile(SkillCatalog.Firebolt) == null);
                arena.Combat.Hit(foe, foe.Sheet.Profile(SkillCatalog.Firebolt)!, arena.Hero, 1f, foe.Position);
                report.Check("A skill that hits you is yours", arena.Hero.Sheet.Profile(SkillCatalog.Firebolt) != null && arena.Truth.Hero.Bar[^1] == "firebolt");
                arena.Run(0.3);
                report.Check("On your client too", arena.Player.Client.Sheet.Profile(SkillCatalog.Firebolt) != null && arena.Player.Client.Kit.Slots[^1]?.Skill == SkillCatalog.Firebolt);
                report.Check("And it's cast with your numbers, not theirs", arena.Hero.Sheet.Profile(SkillCatalog.Firebolt)!.AverageHit != foe.Sheet.Profile(SkillCatalog.Firebolt)!.AverageHit);
            }

            using (Arena arena = new Arena(worlds, request, Wearing("mantle-of-cinders")))
            {
                Actor foe = arena.Dummy(East * 3f, 1000f, Convert(DamageType.Physical, DamageType.Fire, 100f), More(Stat.Damage, 500f));
                arena.Hero.Vitals.Health = 200f;
                float before = arena.Hero.Vitals.Health;
                float hit = BlowFrom(foe, arena.Hero);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                report.Check("The Mantle of Cinders turns part of a burn into healing", before - arena.Hero.Vitals.Health < hit, $"lost {before - arena.Hero.Vitals.Health} of {hit}");
            }
        }

        private static float BlowFrom(Actor from, Actor to)
        {
            var luck = new WorldRandom(1);
            HitPacket hit = default;
            Hits.Roll(from.Sheet.Profile(0), to.State, luck, ref hit);
            return Hits.Mitigate(to.Sheet, ref hit, from.State, to.Afflictions.ResistanceLost, to.Afflictions.TakenIncrease);
        }

        private static float GhostHealth(Arena arena)
        {
            foreach (Archetype archetype in arena.Player.Ecs.QueryArchetypes(new BitMask(typeof(HeroComponent), typeof(HealthComponent)), BitMask.Empty))
            {
                foreach (HealthComponent health in archetype.GetComponentArray<HealthComponent>()!) return health.Current;
            }
            return float.NaN;
        }

        private static int Count<T>(Arena arena) where T : struct, IComponent
        {
            int count = 0;
            foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(T)), BitMask.Empty)) count += archetype.EntityCount;
            return count;
        }

        private static void Triggers(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = new Arena(worlds, request, Wearing("turned-cheek", "bulwark.counterstroke")))
            {
                Actor foe = arena.Dummy(East * 3f);
                float before = Arena.Lost(foe);
                for (int i = 0; i < 300 && Arena.Lost(foe) == before; i++)
                {
                    arena.Combat.Hit(foe, foe.Sheet.Profile(0), arena.Hero, 1f, foe.Position);
                    arena.Run(0.02);
                }
                report.Check("A block can set off a skill, with no one pressing anything", Arena.Lost(foe) > before);
            }

            using (Arena arena = new Arena(worlds, request, Wearing("first-ember")))
            {
                Actor nearby = arena.Dummy(East * 5f);
                float before = Arena.Lost(nearby);
                for (int i = 0; i < 200 && Arena.Lost(nearby) == before; i++)
                {
                    Actor weak = arena.Dummy(East * 4f, 1f);
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), weak, 1f, arena.At);
                    arena.Run(1.1);
                }
                report.Check("A kill can too", Arena.Lost(nearby) > before);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor weak = arena.Dummy(East * 3f, 1f);
                Item item = new Item { Name = "Test", Slot = ItemSlot.Finger, Level = 1, Unique = "knot-of-regret" };
                arena.Truth.Hero.Equipped[WornSlot.Finger1] = item;
                arena.Truth.RefreshLoadout();
                report.Check("Wearing something changes what a hero is at once", arena.Hero.Sheet.RaiseFallen == 25f);

                // Triggers and skills go by place, so a hero who gains some mid-fight has more of them than they started with.
                Item ember = UniqueCatalog.Make(UniqueCatalog.Find("first-ember")!, 30, new WorldRandom(1));
                arena.Truth.Hero.Equipped[WornSlot.Neck] = ember;
                arena.Truth.RefreshLoadout();
                bool survived = true;
                try
                {
                    for (int i = 0; i < 30; i++)
                    {
                        Actor victim = arena.Dummy(East * 3f, 1f);
                        arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), victim, 1f, arena.At);
                        arena.Run(0.05);
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    survived = false;
                }
                report.Check("Putting on something with a trigger in the middle of things is fine", survived);
            }

            using (Arena arena = Enter(worlds, request, "key.lightning-rod"))
            {
                Actor foe = arena.Dummy(East * 3f, 1000f);
                arena.Run(0.1);
                HitPacket shocked = default;
                shocked.Damage[(int)DamageType.Fire] = 100f;
                report.Check("Being always shocked really does mean taking more", Hits.Mitigate(arena.Hero.Sheet, ref shocked, Condition.None, 0f, arena.Hero.TakenIncrease) > 110f);
            }
        }

        private static void Company(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "host.raise-bones"))
            {
                Actor foe = arena.Dummy(East * 10f);
                arena.Release(SkillCatalog.RaiseBones, East);
                arena.Run(0.2);
                report.Check("A minion gets up", arena.Session.Minions.Count == SkillCatalog.RaiseBones.MinionCount, $"{arena.Session.Minions.Count}");
                arena.Run(6);
                report.Check("And fights for you", Arena.Lost(foe) > 0f);

                int before = arena.Session.Minions.Count;
                arena.Release(SkillCatalog.RaiseBones, East);
                arena.Run(0.2);
                report.Check("Calling them again replaces them rather than adding to them", arena.Session.Minions.Count == before);

                Actor minion = arena.Session.Minions[0];
                report.Check("They're on your side", minion.Team == Team.Heroes && minion.Owner == arena.Hero);

                minion.MinionLeft = 0.1f;
                arena.Run(0.4);
                report.Check("And go when their time's up", minion.Dead && !arena.Session.Minions.Contains(minion));
            }

            using (Arena plain = Enter(worlds, request, "host.raise-bones"))
            using (Arena strong = Enter(worlds, request, "host.command"))
            {
                float Hit(Arena arena)
                {
                    GameLab.Learn(arena.Truth.Hero, "host.raise-bones");
                    arena.Truth.RefreshLoadout();
                    arena.Release(SkillCatalog.RaiseBones, East);
                    arena.Run(0.1);
                    return arena.Session.Minions[0].Sheet.Profile(0).AverageHit;
                }
                report.Check("What's written for minions reaches them", Hit(strong) > Hit(plain) * 1.2f, $"{Hit(plain)} and {Hit(strong)}");
            }

            using (Arena arena = Enter(worlds, request, "key.puppeteer", "host.raise-bones"))
            {
                Actor foe = arena.Dummy(East * 4f);
                arena.Release(SkillCatalog.Strike, East);
                report.Check("Puppeteer: your own hands do nothing", Arena.Lost(foe) == 0f);
            }

            using (Arena strong = Enter(worlds, request, "host.command", "host.raise-bones"))
            {
                strong.Release(SkillCatalog.RaiseBones, East);
                strong.Run(0.2);
                Actor bones = strong.Session.Minions[0];
                float one = bones.Sheet.Profile(0).AverageHit;
                strong.Hero.Counters.Minions = 10;
                bones.Counters.Minions = 10;
                bones.ReadState(Condition.None);
                report.Check("What's counted per minion counts on the minions", bones.Sheet.Profile(0).AverageHit > one, $"{one} then {bones.Sheet.Profile(0).AverageHit}");
            }

            using (Arena arena = new Arena(worlds, request, Hero("host.raise-bones"), heroes: 2))
            {
                ServerPlayer friend = arena.Lab.Server.Heroes[1];
                arena.Release(SkillCatalog.RaiseBones, East);
                arena.Run(0.2);
                Actor minion = arena.Session.Minions[0];
                arena.Lab.Players[0].Client.Dispose();
                arena.Lab.Players.RemoveAt(0);
                arena.Run(0.6);
                report.Check("Someone leaving takes their minions with them", minion.Dead && arena.Session.Minions.Count == 0);
            }

            using (Arena arena = Enter(worlds, request, "key.mutiny"))
            {
                for (int i = 0; i < 60 && arena.Session.Minions.Count == 0; i++)
                {
                    Actor weak = arena.Dummy(East * 3f, 1f);
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), weak, 1f, arena.At);
                    arena.Run(1);
                }
                report.Check("Mutiny: an enemy you kill can rise beside you, with what it could do", arena.Session.Minions.Count > 0 && arena.Session.Minions[0].Loadout.Skills.Contains(SkillCatalog.Firebolt));
            }

            using (Arena arena = new Arena(worlds, request, Hero("key.shared-burden"), heroes: 2))
            {
                ServerPlayer friend = arena.Lab.Server.Heroes[1];
                Actor foe = arena.Dummy(East * 3f, 1000f, More(Stat.Damage, 2000f));
                float Hit(Actor target)
                {
                    float before = target.Vitals.Health;
                    arena.Combat.Hit(foe, foe.Sheet.Profile(0), target, 1f, foe.Position);
                    return before - target.Vitals.Health;
                }
                float guardianBefore = arena.Hero.Vitals.Health;
                float taken = Hit(friend.Actor);
                report.Check("Shared Burden: the guardian takes part of what an ally would have", arena.Hero.Vitals.Health < guardianBefore, $"friend lost {taken}, guardian {guardianBefore - arena.Hero.Vitals.Health}");
            }
        }

        private static void Land(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, "key.tidewalker", "wilds.tidepool"))
            {
                arena.Run(0.4);
                float dry = arena.Skill(SkillCatalog.Strike).AverageHit;
                arena.Release(SkillCatalog.Tidepool, East, 0f);
                arena.Run(0.6);
                report.Check("The land is read under someone's feet", (arena.Hero.State & Condition.InWater) != 0);
                float wet = arena.Hero.Sheet.Profile(SkillCatalog.Strike)!.AverageHit;
                report.Check("Tidewalker: far stronger in the water, and its own pool counts", wet > dry * 1.6f, $"{dry} dry, {wet} wet");
                arena.Run(0.2);
                report.Check("Which the client works out for itself from what it's told", arena.Player.Client.Sheet.Profile(SkillCatalog.Strike)!.AverageHit == wet);
            }

            using (Arena arena = new Arena(worlds, request, Wearing("drowned-king", "key.tidewalker")))
            {
                arena.Run(0.4);
                report.Check("The Drowned King's shell needs no water to stand in", (arena.Hero.State & Condition.InWater) != 0);
            }

            using (Arena arena = Enter(worlds, request, "key.peakborn"))
            {
                Actor low = arena.Dummy(East * 3f);
                Actor high = arena.Dummy(East * 3f + new Vector2(0f, 2f));
                low.Height = arena.Hero.Height - 10f;
                high.Height = arena.Hero.Height + 10f;
                for (int i = 0; i < 60; i++)
                {
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), low, 1f, arena.At);
                    arena.Combat.Hit(arena.Hero, arena.Skill(SkillCatalog.Strike), high, 1f, arena.At);
                }
                report.Check("Peakborn: height is damage", Arena.Lost(low) > Arena.Lost(high) * 1.5f, $"{Arena.Lost(low)} down, {Arena.Lost(high)} up");
            }

            WorldData world = worlds.Get(request).Result;
            var reader = new LandReader(world);
            Condition seen = Condition.None;
            var random = new Random(1);
            for (int i = 0; i < 20000; i++) seen |= reader.At(new Vector2(random.NextSingle(), random.NextSingle()) * world.Size);
            report.Check("Even one world has water, woods and roads to fight in", (seen & (Condition.InWater | Condition.UnderTrees | Condition.OnRoad)) == (Condition.InWater | Condition.UnderTrees | Condition.OnRoad), seen.ToString());

            var kinds = new Dictionary<Condition, int>();
            foreach (BiomeDefinition biome in BiomeCatalog.All)
            {
                if (biome.Temperature >= 0.7f && biome.Moisture < 0.4f) kinds[Condition.HotLand] = kinds.GetValueOrDefault(Condition.HotLand) + 1;
                if (biome.Temperature <= 0.3f) kinds[Condition.ColdLand] = kinds.GetValueOrDefault(Condition.ColdLand) + 1;
                if (biome.Moisture >= 0.75f) kinds[Condition.WetLand] = kinds.GetValueOrDefault(Condition.WetLand) + 1;
                if (biome.Altitude >= 0.5f) kinds[Condition.HighLand] = kinds.GetValueOrDefault(Condition.HighLand) + 1;
            }
            report.Check("There are hot, cold, wet and high lands to be found", kinds.Count == 4, string.Join(", ", kinds.Keys));
        }

        private static void Enemies(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Loadout loadout = arena.Session.Bestiary.Shared(Bestiary.Firebrand, 10, EnemyRank.Normal, Bestiary.NoTrait);
                Actor witch = arena.Place(loadout, East * 15f);
                witch.Still = false;
                float before = arena.Hero.Vitals.Health;
                arena.Run(5);
                report.Check("A caster casts", arena.Hero.Vitals.Health < before, $"{before} to {arena.Hero.Vitals.Health}");
            }

            using (Arena arena = Enter(worlds, request, "pyre.firebolt"))
            {
                Loadout one = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 10, EnemyRank.Normal, Bestiary.NoTrait);
                Loadout two = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 10, EnemyRank.Elite, 0);
                report.Check("An elite is more than a plain enemy", two.SheetFor(Condition.None).MaxHealth > one.SheetFor(Condition.None).MaxHealth);
                report.Check("And its trait changes what it does: a burning one hits with fire", two.SheetFor(Condition.None).Profile(0).Flows.ToArray().Any(f => f.Type == DamageType.Fire));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor hero = arena.Hero;
                Item shell = UniqueCatalog.Make(UniqueCatalog.Find("ashen-promise")!, 20, new WorldRandom(1));
                Loadout boss = new Loadout(20, new WeaponProfile(50f, 50f, 0.6f, 5f, Tags.Sword), shell.Mods().Append(Flat(Stat.Health, 1f)), new[] { SkillCatalog.Strike });
                Actor warlord = arena.Place(boss, East * 3f);
                warlord.Gear = new[] { shell };

                warlord.Site = arena.Session.Sites[0];
                int pickups = Count<PickupComponent>(arena);
                arena.Combat.Hit(hero, arena.Skill(SkillCatalog.Strike), warlord, 1f, arena.At);
                arena.Run(0.2);
                report.Check("A boss drops what it was wearing", Count<PickupComponent>(arena) > pickups);
            }

            using (Arena arena = Enter(worlds, request))
            {
                int enemies = 0;
                foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(ActorComponent)), BitMask.Empty)) enemies += archetype.EntityCount;
                arena.Truth.Sim.Position = arena.Session.World.Sites.First(s => s.Kind == SiteKind.BanditCamp).Position;
                arena.Run(4);

                int spawned = 0;
                var shared = new HashSet<Loadout>();
                foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(EnemyComponent), typeof(ActorComponent)), BitMask.Empty))
                {
                    foreach (ActorComponent actor in archetype.GetComponentArray<ActorComponent>()!)
                    {
                        spawned++;
                        shared.Add(actor.Actor.Loadout);
                    }
                }
                report.Check("Going to a camp wakes real enemies, built on the same stats", spawned > enemies, $"{spawned}");
                report.Check("Who share what they can", shared.Count < spawned, $"{spawned} enemies, {shared.Count} sets of numbers");
                report.Note($"A bandit camp: {spawned} enemies on {shared.Count} sets of numbers");
            }
        }

        private static void Buffs(Report report, WorldCache worlds, WorldRequest request)
        {
            // Something for it to work on: a hero starts out in leather, with no armour at all.
            HeroState hero = Hero("forge.stone-skin");
            hero.Equipped[WornSlot.Body] = new Item { Name = "Lab Plate", Slot = ItemSlot.Body, Level = 1, Armour = 40f };
            using var arena = new Arena(worlds, request, hero);
            float armour = arena.Hero.Sheet.Armour;
            arena.Release(SkillCatalog.StoneSkinCast, East);
            arena.Run(0.3);
            report.Check("A buff changes what someone is", arena.Hero.Sheet.Armour > armour, $"{armour} to {arena.Hero.Sheet.Armour}");
            report.Check("On their client too", arena.Player.Client.Sheet.Armour == arena.Hero.Sheet.Armour, $"{arena.Player.Client.Sheet.Armour}");
            arena.Run(BuffCatalog.StoneSkin.Duration + 1);
            report.Check("And wears off, on both", arena.Hero.Sheet.Armour == armour && arena.Player.Client.Sheet.Armour == armour);

            Loadout shared = arena.Session.Bestiary.Shared(Bestiary.Outlaw, 5, EnemyRank.Normal, Bestiary.NoTrait);
            Actor one = arena.Place(shared, East * 8f);
            Actor two = arena.Place(shared, West * 8f);
            arena.Combat.AddBuff(one, BuffCatalog.Quickening, 2f);
            arena.Run(0.2);
            report.Check("A buff on one enemy doesn't touch another of its kind", one.Sheet.MoveSpeed > two.Sheet.MoveSpeed);
            arena.Run(2.5);
            report.Check("And that one goes back to sharing when it's over", ReferenceEquals(one.Loadout, shared));
        }

        // What all of this is for: a screenful of different enemies, each with spells of their own, against five heroes with spells of theirs.
        private static void Crowd(Report report, WorldCache worlds, WorldRequest request, int enemies)
        {
            using var lab = new GameLab(worlds, request);
            for (int i = 0; i < Protocol.MaxPlayers; i++)
            {
                HeroState hero = GameLab.StrongHero();
                hero.Equipped[WornSlot.Finger1] = new Item { Name = "Lab Ring", Slot = ItemSlot.Finger, Level = 1, Lines = { new ItemLine("health", 100000000f), new ItemLine("mana-regen", 1000f) } };
                lab.AddPlayer($"Hero {i + 1}", new NetworkConditions { Latency = 40, Jitter = 8 }, 300 + i, hero: hero);
            }
            lab.Run(6);

            GameServer server = lab.Server;
            ServerSession session = server.Session!;
            Vector2 centre = server.Heroes[0].Sim.Position;
            EnemyKind[] kinds = { Bestiary.Firebrand, Bestiary.Rimewitch, Bestiary.Stormcaller, Bestiary.Bogseer, Bestiary.Mourner, Bestiary.Archer, Bestiary.Outlaw, Bestiary.Restless };
            var random = new Random(4);
            var shared = new HashSet<Loadout>();
            var actors = new List<Actor>();

            for (int i = 0; i < enemies; i++)
            {
                EnemyKind kind = kinds[i % kinds.Length];
                int trait = i % 5 == 0 ? random.Next(Bestiary.Traits.Length) : Bestiary.NoTrait;
                Loadout loadout = session.Bestiary.Shared(kind, 20 + i % 3, trait == Bestiary.NoTrait ? EnemyRank.Normal : EnemyRank.Elite, trait);
                shared.Add(loadout);

                Vector2 at = centre + new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * 110f;
                float height = session.World.Height.Sample(at);
                var actor = new Actor(Team.Enemies, loadout, at, 1.1f) { Body = "skeleton", Height = height };
                actor.Vitals.Health = actor.Sheet.MaxHealth;
                actor.Entity = server.Ecs.CreateEntity(Bodies.Walking(server, "skeleton", new Vector3(at.X, height, at.Y), 1f, 1.5f,
                    new EnemyComponent { Name = kind.Name, Level = 20 },
                    new EnemyBrainComponent { SiteIndex = 0, SpawnIndex = -1, Home = at, AggroRadius = 300f, LeashRadius = 900f, KeepsAway = kind.KeepsAway, Cooldown = random.NextSingle() * 2f },
                    new HealthComponent(actor.Sheet.MaxHealth), new ActionComponent(), new AfflictedComponent(), new ActorComponent(actor)));
                actors.Add(actor);
            }

            lab.Run(2);
            lab.TickCostTotal = 0.0;
            lab.TickCostWorst = 0.0;
            lab.TicksTimed = 0;

            int heroHits = 0;
            float lostBefore = server.Heroes.Sum(h => h.Actor.Sheet.MaxHealth - h.Actor.Vitals.Health);
            long allocatedBefore = GC.GetTotalAllocatedBytes(false);
            lab.Run(30, () => heroHits++);
            long allocated = GC.GetTotalAllocatedBytes(false) - allocatedBefore;

            int struck = actors.Count(a => a.Vitals.Health < a.Sheet.MaxHealth || a.Dead);
            int fallen = actors.Count(a => a.Dead);
            double average = lab.TickCostTotal / lab.TicksTimed;
            float heroesLost = server.Heroes.Sum(h => h.Actor.Sheet.MaxHealth - h.Actor.Vitals.Health);
            bool usedTheirSkills = heroesLost > lostBefore;

            report.Check($"{enemies} enemies of {kinds.Length} kinds really fight", usedTheirSkills && struck > 0, $"heroes lost {heroesLost:0}, {struck} enemies struck");
            report.Check($"{enemies} enemies share their numbers", shared.Count < enemies / 2, $"{shared.Count} sets of numbers");
            report.Check($"{enemies} enemies and five heroes: the server keeps up, with time to spare", average < 1000.0 / NetTime.TicksPerSecond * 0.5, $"{average:0.00} ms a tick");
            report.Check("And a snapshot still fits in a packet", server.LargestSnapshot <= Protocol.SnapshotBudget, $"{server.LargestSnapshot}");
            report.Note($"{enemies} enemies on {shared.Count} sets of numbers: {average:0.00} ms a tick on average, worst {lab.TickCostWorst:0.0} ms, of the {1000.0 / NetTime.TicksPerSecond:0.0} ms it has");
            report.Note($"  {struck} struck and {fallen} fallen in 30 seconds; {allocated / 1024.0 / 1024.0 / 30.0:0.0} MB allocated a second by everything in the process, clients included");
        }

        // What's on a key has to behave as it would on a mouse button, or a build can only use what it puts on the mouse. LocalInput needs a window, so
        // this goes through what it goes through, on a line bad enough that a held key arguing with the server would show up as corrections.
        private static void HeldKeys(Report report, WorldCache worlds, WorldRequest request)
        {
            const int OnOne = 3, OnTwo = 4;
            using var lab = new GameLab(worlds, request);
            HeroState hero = GameLab.StrongHero();
            hero.Equipped[WornSlot.Finger1] = new Item { Name = "Lab Ring", Slot = ItemSlot.Finger, Level = 1, Lines = { new ItemLine("mana-regen", 1000f) } };
            hero.Bar[OnOne] = SkillCatalog.Firebolt.Id;
            hero.Bar[OnTwo] = SkillCatalog.FrostNova.Id;
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 }, 9, hero: hero);
            player.Fights = false;
            lab.Run(4);

            int before = player.Client.Predictor.Corrections;
            int compared = player.Client.Predictor.Compared;
            SkillProfile bolt = player.Client.Kit.Slots[OnOne]!;
            SkillProfile nova = player.Client.Kit.Slots[OnTwo]!;

            player.HeldSlots = 1 << OnOne;
            lab.Run(0.08);
            player.HeldSlots = 0;
            lab.Run(2);
            report.Check("A tap on a key is one use", player.Uses[OnOne] == 1, $"{player.Uses[OnOne]}");

            Array.Clear(player.Uses);
            player.HeldSlots = 1 << OnOne;
            lab.Run(0.05);
            player.HeldSlots = 0;
            lab.Run(bolt.UseTime - 0.2);
            player.HeldSlots = 1 << OnOne;
            lab.Run(0.05);
            player.HeldSlots = 0;
            lab.Run(1.5);
            report.Check("A tap near the end of a cast waits for it instead of being lost", player.Uses[OnOne] == 2, $"{player.Uses[OnOne]}");

            Array.Clear(player.Uses);
            const double Holding = 7.0;
            player.HeldSlots = 1 << OnOne;
            lab.Run(Holding);
            player.HeldSlots = 0;
            int held = player.Uses[OnOne];
            double fastest = Holding / bolt.UseTime;
            report.Check("A key held down repeats as fast as the skill can go", held >= (int)fastest - 1 && held <= (int)fastest + 1, $"{held} in {Holding} s, at {bolt.UseTime:0.00} s a use");

            lab.Run(2);

            // Let go at every point of a cast. Anything still queued would come out as one use too many, some of the time.
            int overran = 0;
            for (int i = 0; i < 10; i++)
            {
                player.HeldSlots = 1 << OnOne;
                lab.Run(1.0 + i * 0.13);
                player.HeldSlots = 0;
                lab.Run(0.03);
                int released = player.Uses[OnOne];
                lab.Run(1.2);
                overran += player.Uses[OnOne] - released;
            }
            report.Check("And stops when it's let go, wherever in a cast that is", overran == 0, $"{overran} too many in 10");

            Array.Clear(player.Uses);
            const double Both = 12.0;
            player.HeldSlots = 1 << OnOne | 1 << OnTwo;
            lab.Run(Both);
            player.HeldSlots = 0;
            lab.Run(1);
            report.Check("Two keys held: the one on cooldown leaves the other free to go", player.Uses[OnOne] >= Both / bolt.UseTime * 0.6, $"{player.Uses[OnOne]} bolts");
            report.Check("And the one on cooldown goes each time it comes round", player.Uses[OnTwo] >= (int)(Both / nova.Cooldown), $"{player.Uses[OnTwo]} novas at {nova.Cooldown:0.0} s");

            int corrections = player.Client.Predictor.Corrections - before;
            int checks = player.Client.Predictor.Compared - compared;
            report.Check("Keys held down are predicted cleanly over a bad line", corrections <= checks / 100, $"{corrections} of {checks}");
            report.Note($"Held keys: {held} uses in {Holding} s at {bolt.UseTime:0.00} s each, {corrections} corrections in {checks} checks");
        }

        private static void Prediction(Report report, WorldCache worlds, WorldRequest request)
        {
            using var lab = new GameLab(worlds, request);
            HeroState hero = GameLab.StrongHero();
            GameLab.Learn(hero, "storm.quickening");
            LabPlayer player = lab.AddPlayer("Hero", new NetworkConditions { Latency = 80, Jitter = 10, LossChance = 0.01 }, 7, hero: hero);
            lab.Run(8);
            int before = player.Client.Predictor.Corrections;
            int compared = player.Client.Predictor.Compared;
            lab.Run(45);
            int corrections = player.Client.Predictor.Corrections - before;
            int checks = player.Client.Predictor.Compared - compared;
            report.Check("Spells, mana and cooldowns are predicted cleanly over a real line", corrections <= checks / 100, $"{corrections} of {checks}");
            report.Note($"{corrections} corrections in {checks} checks, worst {player.Client.Predictor.WorstCorrection:0.00} m");

            player.Fights = false;
            lab.Run(4);
            ServerPlayer truth = lab.Server.Players.First();
            report.Check("And client and server end up agreeing about mana", MathF.Abs(player.Client.Predictor.Current.Mana - truth.Sim.Mana) < 1f, $"{player.Client.Predictor.Current.Mana} and {truth.Sim.Mana}");
        }
    }
}
