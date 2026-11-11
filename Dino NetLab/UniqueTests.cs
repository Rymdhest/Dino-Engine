using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// Every unique's rule, done for real on a running server. A sentence on a card can be well made and still be about nothing, so each of these makes the thing happen and
    /// looks at what it did. Most are a stand-in on the heroes' side wearing nothing but the unique, as that's what <see cref="Combat"/> reads; what only a hero's step or a
    /// player's server decides is a real hero, wearing it.
    /// </summary>
    internal static class UniqueTests
    {
        private static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly Vector2 West = new Vector2(-1f, 0f);
        private static readonly Vector2 North = new Vector2(0f, 1f);

        // Where a stand-in stands, away from the hero, and somewhere near it to put things.
        private static readonly Vector2 Post = West * 8f;

        private static readonly WeaponProfile Sword = new WeaponProfile(20f, 20f, WeaponProfile.BareTime, 0f, Tags.Sword | Tags.OneHanded);
        private static readonly WeaponProfile Wand = new WeaponProfile(20f, 20f, WeaponProfile.BareTime, 0f, Tags.Wand | Tags.OneHanded);

        public static int Run(Options options)
        {
            var report = new Report();
            var request = GameLab.OpenIsle(options.Int("seed", 303));
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("Blows");
            Blows(report, worlds, request);

            report.Section("Ailments and reactions");
            Afflicting(report, worlds, request);

            report.Section("What flies");
            Flying(report, worlds, request);

            report.Section("Books");
            Writing(report, worlds, request);

            report.Section("Not being hurt");
            Defending(report, worlds, request);

            report.Section("Dying, and killing");
            Dying(report, worlds, request);

            report.Section("Company");
            Company(report, worlds, request);

            report.Section("What only a hero does");
            Heroes(report, worlds, request);

            report.Section("What's counted");
            Counting(report, worlds, request);

            report.Section("Bosses");
            Bosses(report);

            return report.Finish();
        }

        private static Mod[] Of(string unique) => UniqueCatalog.Find(unique)!.Mods;

        private static Arena Enter(WorldCache worlds, WorldRequest request, HeroState? hero = null, LandTerms? terms = null)
        {
            hero ??= HeroState.CreateNew();
            hero.Level = 60;
            return new Arena(worlds, request, hero, terms: terms);
        }

        private static HeroState Wearing(string unique)
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 60;
            Item item = UniqueCatalog.Make(UniqueCatalog.Find(unique)!, 60, new WorldRandom(1));
            hero.Pickup(item);
            hero.Equip(item);
            return hero;
        }

        /// <summary>Someone on the heroes' side, standing still, with a unique's lines and whatever else is asked. Its blows are always the same size, and never critical unless something says so.</summary>
        private static Actor Wielder(Arena arena, string? unique, BookPlan[] books, WeaponProfile? weapon = null, Vector2? at = null, params Mod[] more)
        {
            var mods = new List<Mod> { Flat(Stat.Health, 1000f), Flat(Stat.Mana, 1000f) };
            if (unique != null) mods.AddRange(Of(unique));
            mods.AddRange(more);

            // A wand is where Spell Damage comes from, as much of it as the wand deals.
            if (weapon is WeaponProfile held && (held.Tags & Tags.Wand) != 0) mods.Add(Added(DamageType.Physical, held.Max, Tags.Spell));
            return arena.Place(new Loadout(20, weapon ?? Sword, mods, books), at ?? Post, Team.Heroes);
        }

        private static Actor Wielder(Arena arena, string? unique, BookPlan book, params Mod[] more) => Wielder(arena, unique, new[] { book }, null, null, more);

        // A training dummy somewhere around the stand-in.
        private static Actor Near(Arena arena, Vector2 offset, float health = 1000000f, params Mod[] mods) => arena.Dummy(Post + offset, health, mods);

        private static float Blow(Arena arena, Actor from, Actor to, int book = 0)
        {
            float before = Arena.Lost(to);
            arena.Combat.Hit(from, from.Sheet.Profile(book).First, to, 1f, from.Position);
            return Arena.Lost(to) - before;
        }

        private static void Release(Arena arena, Actor from, Vector2 way, float reach = 10f, int book = 0)
        {
            arena.Combat.Release(from, from.Sheet.Profile(book), from.Position, way.Normalized(), reach);
        }

        private static int Count<T>(Arena arena) where T : struct, IComponent
        {
            int count = 0;
            foreach (Archetype archetype in arena.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(T)), BitMask.Empty)) count += archetype.EntityCount;
            return count;
        }

        private static bool Has(Actor actor, BuffDefinition buff) => actor.Buffs.Exists(active => active.Buff == buff);

        private static int Stacks(Actor actor, BuffDefinition buff) => actor.Buffs.Find(active => active.Buff == buff).Stacks;

        private static void Blows(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                PageProfile page = Wielder(arena, "honest-blade", Shelf.Strike).Sheet.Profile(0).First;
                report.Check("The Honest Blade: every Hit is Critical, and worth no more than any other", page.CritChance == 100f && page.CritMultiplier == 100f, $"{page.CritChance}%, x{page.CritMultiplier / 100f}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("thin-red-line")))
            {
                arena.Run(0.2);
                Hits.Heal(arena.Hero.Sheet, ref arena.Hero.Vitals, 100000f);
                arena.Run(0.2);
                float share = arena.Hero.Vitals.Health / arena.Hero.Sheet.MaxHealth;
                report.Check("The Thin Red Line: Health stops at Low, whatever heals it", share <= Hits.LowShare + 0.001f && (arena.Hero.State & Condition.LowHealth) != 0, $"{share:0.000}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "long-grudge", Shelf.Strike);
                Actor foe = Near(arena, North * 3f);
                float plain = Blow(arena, wielder, foe);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                arena.Run(0.15);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("The Long Grudge: each Hit taken is a stack of Grudge", Stacks(wielder, BuffCatalog.Grudge) == 2, $"{Stacks(wielder, BuffCatalog.Grudge)}");
                arena.Run(0.05);
                float grudged = Blow(arena, wielder, foe);
                report.Check("Which goes into the next blow", grudged > plain * 1.7f, $"{plain} then {grudged}");
                report.Check("And is gone with it", !Has(wielder, BuffCatalog.Grudge));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "duellists-courtesy", Shelf.Strike);
                Actor first = Near(arena, North * 3f);
                Actor second = Near(arena, North * -3f);
                float marked = Blow(arena, wielder, first);
                float other = Blow(arena, wielder, second);
                report.Check("Duellist's Courtesy: the first Enemy struck is Marked, and the rest suffer for it", wielder.Focus == first && marked > other * 4f, $"{marked} against {other}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "first-cut", Shelf.Strike);
                Actor whole = Near(arena, North * 3f);
                Actor hurt = Near(arena, North * -3f);
                hurt.Vitals.Health = hurt.Sheet.MaxHealth * 0.9f;
                arena.Run(0.05);
                float first = Blow(arena, wielder, whole);
                float later = Blow(arena, wielder, hurt);
                report.Check("First Cut: an Enemy at Full Health takes far more than a Wounded one", first > later * 6f, $"{first} against {later}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "headsmans-due", Shelf.Strike);
                Actor plain = Wielder(arena, null, new[] { Shelf.Strike }, null, Post + North * 6f);
                Actor low = Near(arena, East * 3f);
                Actor spared = Near(arena, East * -3f);
                low.Vitals.Health = spared.Vitals.Health = low.Sheet.MaxHealth * 0.1f;
                Blow(arena, wielder, low);
                Blow(arena, plain, spared);
                report.Check("The Headsman's Due: any Hit Executes what's low enough, and an ordinary one doesn't", low.Dead && !spared.Dead);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "follow-through", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor weak = Near(arena, North * 3f, 10f);
                Actor next = Near(arena, North * 6f);
                Blow(arena, wielder, weak);
                arena.Run(0.1);
                report.Check("Follow Through: what's left after a kill goes on to the next", weak.Dead && Arena.Lost(next) > 1000f, $"{Arena.Lost(next)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "great-leveller", Shelf.Strike, Set(Stat.CritChance, 0f), More(Stat.Damage, 1000f));
                Actor target = Near(arena, North * 3f, 1000f);
                report.Near("The Great Leveller: a Hit is a share of the Enemy's Maximum Health, and nothing else counts", Blow(arena, wielder, target), 1000f * Rulebook.LevellerShare, 0.5f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "round-the-houses", Shelf.Strike);
                Actor behind = Near(arena, West * 3f);
                Release(arena, wielder, East, 3f);
                arena.Run(0.1);
                report.Check("Round the Houses: a Cone goes all the way round", wielder.Sheet.Profile(0).First.Width >= MathF.Tau && Arena.Lost(behind) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "whisper", Shelf.Strike);
                Actor target = Near(arena, North * 3f);
                float unaware = Blow(arena, wielder, target);
                float aware = Blow(arena, wielder, target);
                report.Check("Whisper: an Enemy that hasn't noticed anyone takes ten times what one that has does", unaware > aware * 9f, $"{unaware} then {aware}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor never = Wielder(arena, "deadmans-hand", Shelf.Strike, Set(Stat.CritChance, 0f));
                Actor always = Wielder(arena, "deadmans-hand", new[] { Shelf.Strike }, null, Post + North * 6f, Flat(Stat.CritChance, 100f));
                Actor target = Near(arena, East * 3f);
                float plain = Blow(arena, never, target);
                float critical = Blow(arena, always, target);
                report.Check("Dead Man's Hand: an ordinary Hit deals nothing, and a Critical one a great deal", plain == 0f && critical > 50f, $"{plain} and {critical}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "long-arm", Shelf.Strike);
                Actor close = Near(arena, North * 2f);
                Actor far = Near(arena, North * 8f);
                float near = Blow(arena, wielder, close);
                float reached = Blow(arena, wielder, far);
                report.Check("The Long Arm: what's at the end of the reach takes far more than what's Up Close", reached > near * 4f, $"{reached} against {near}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "stragglers-friend", Shelf.Strike);
                Actor alone = Near(arena, North * 8f);
                Actor crowded = Near(arena, North * -3f);
                Near(arena, North * -4.5f);
                float picked = Blow(arena, wielder, alone);
                float lost = Blow(arena, wielder, crowded);
                report.Check("The Straggler's Friend: an Isolated Enemy takes far more than one in a Crowd", picked > lost * 3.5f, $"{picked} against {lost}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "hedgerow", Shelf.Strike);
                Actor target = Near(arena, North * 3f);
                arena.Run(1.6);
                report.Check("Hedgerow: standing still, spears go off around you every second", Arena.Lost(target) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "slow-verdict", Shelf.Strike);
                Actor target = Near(arena, North * 3f);
                arena.Run(5.2);
                report.Check("The Slow Verdict: Verdict comes with time", Has(wielder, BuffCatalog.Verdict));
                arena.Run(0.05);
                float verdict = Blow(arena, wielder, target);
                arena.Run(0.05);
                float plain = Blow(arena, wielder, target);
                report.Check("And lands with the next blow, which is worth four of the rest", verdict > plain * 3.5f && !Has(wielder, BuffCatalog.Verdict), $"{verdict} then {plain}");
            }
        }

        private static void Afflicting(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "butchers-bill", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor bleeding = Near(arena, North * 3f, 1000f);
                Actor beside = Near(arena, North * 5f);
                bleeding.Afflictions.BleedLeft = 4f;
                bleeding.Afflictions.BleedPerSecond = 1f;
                Blow(arena, wielder, bleeding);
                arena.Run(0.1);
                report.Near("The Butcher's Bill: what dies bleeding bursts, for a share of its Maximum Health", Arena.Lost(beside), 1000f * Rulebook.BleedBurstShare, 1f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "ringing-skull", Shelf.Strike, Chance(Tags.Stun, 100f));
                Actor struck = Near(arena, North * 3f);
                Actor beside = Near(arena, North * 6f);
                Actor away = Near(arena, North * 30f);
                Blow(arena, wielder, struck);
                report.Check("The Ringing Skull: a Stun passes to whoever's near, and no further", struck.Afflictions.StunLeft > 0f && beside.Afflictions.StunLeft > 0f && away.Afflictions.StunLeft <= 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "aftershock", Shelf.Strike, Chance(Tags.Stun, 100f));
                Actor struck = Near(arena, North * 3f);
                Actor beside = Near(arena, North * 7f);
                Blow(arena, wielder, struck);
                arena.Run(0.1);
                report.Check("Aftershock: a Stun shakes the ground around whoever took it", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "slow-kiss", Shelf.Strike, Chance(Tags.Venom, 100f));
                Actor target = Near(arena, North * 3f);
                for (int i = 0; i < AilmentRules.MostVenom; i++) Blow(arena, wielder, target);
                float before = Arena.Lost(target);
                arena.Run(0.05);
                report.Check("The Slow Kiss: a full dose of Venom lands all at once", target.Afflictions.Venom is { Count: 0 } && Arena.Lost(target) > before + 10f, $"{before} then {Arena.Lost(target)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "widows-needle", Shelf.Strike, Chance(Tags.Venom, 100f));
                Actor target = Near(arena, North * 3f);
                for (int i = 0; i < AilmentRules.MostVenom; i++) Blow(arena, wielder, target);
                report.Near("Widow's Needle: each Stack of Venom slows", target.Afflictions.ActionRate, 1f - AilmentRules.MostVenom * Rulebook.VenomSlowPerStack, 0.01f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "open-vein", Shelf.Strike);
                Actor target = Near(arena, North * 3f);
                wielder.Vitals.Health = 500f;
                target.Afflictions.BleedLeft = 5f;
                target.Afflictions.BleedPerSecond = 20000f;
                target.Afflictions.BleedSource = wielder;
                arena.Run(1);
                report.Check("Open Vein: a Bleed of yours is Health back", wielder.Vitals.Health > 600f, $"{wielder.Vitals.Health}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor caster = Wielder(arena, "mutual-ruin", new[] { Shelf.ArcaneDart }, Wand, null, Chance(Tags.Hex, 100f));
                Actor target = Near(arena, North * 3f);
                for (int i = 0; i < Rulebook.MostDeepHex + 3; i++) Blow(arena, caster, target);
                report.Check("Mutual Ruin: Hex goes deeper", target.Afflictions.HexStacks == Rulebook.MostDeepHex, $"{target.Afflictions.HexStacks}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor caster = Wielder(arena, "dowsing-rod", new[] { Shelf.Spark }, Wand);
                Actor plain = Wielder(arena, null, new[] { Shelf.Spark }, Wand, Post + North * 20f);
                Actor struck = Near(arena, North * 3f);
                Actor shocked = Near(arena, North * 6f);
                Actor plainStruck = Near(arena, North * 23f);
                Actor plainShocked = Near(arena, North * 26f);
                foreach (Actor one in new[] { struck, shocked, plainStruck, plainShocked }) one.Afflictions.ShockLeft = 3f;
                Blow(arena, caster, struck);
                Blow(arena, plain, plainStruck);
                arena.Run(0.1);
                bool dry = !arena.Combat.Land.InWater(shocked.Position) && !arena.Combat.Land.InWater(plainShocked.Position);
                report.Check("The Dowsing Rod: lightning runs through the Shocked, with no Water there", dry && Arena.Lost(shocked) > 0f && Arena.Lost(plainShocked) == 0f, $"{Arena.Lost(shocked)} and {Arena.Lost(plainShocked)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "kindling-mitts", new[] { Shelf.Firebolt }, Wand);
                Actor struck = Near(arena, North * 3f);
                Actor beside = Near(arena, North * 6f);
                struck.Afflictions.Venom = new List<VenomStack> { new VenomStack { PerSecond = 10f, Left = 3f, Source = wielder } };
                Blow(arena, wielder, struck);
                arena.Run(0.1);
                report.Check("Kindling Mitts: Flashfire leaves the Venom, and passes it on", struck.Afflictions.Venom is { Count: > 0 } && beside.Afflictions.Venom is { Count: > 0 });
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "catalyst", Shelf.Strike);
                report.Check("The Catalyst: Reactions are far stronger", wielder.Sheet.Profile(0).First.ReactionScale >= 2.4f, $"{wielder.Sheet.Profile(0).First.ReactionScale}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "kettle", new[] { Shelf.Firebolt }, Wand);
                Actor chilled = Near(arena, North * 3f);
                chilled.Afflictions.ChillLeft = 2f;
                chilled.Afflictions.ChillEffect = 0.2f;
                int zones = Count<ZoneStateComponent>(arena);
                Blow(arena, wielder, chilled);
                arena.Run(0.1);
                report.Check("The Kettle: a Reaction leaves steam", Count<ZoneStateComponent>(arena) > zones);
            }

            using (Arena arena = Enter(worlds, request))
            {
                // Cold, as a Physical blow would Shatter the ice before anything died in it.
                Actor wielder = Wielder(arena, "frostbitten-locket", new[] { Shelf.FrostShards }, Wand, null, More(Stat.Damage, 100000f));
                Actor frozen = Near(arena, North * 3f, 1000f);
                Actor beside = Near(arena, North * 6f);
                frozen.Afflictions.FreezeLeft = 1f;
                arena.Combat.Hit(wielder, wielder.Sheet.Profile(0).First, frozen, 1f, wielder.Position);
                arena.Run(0.1);
                report.Check("The Frostbitten Locket: what dies Frozen bursts", frozen.Dead && Arena.Lost(beside) > 0f);
            }
        }

        private static void Flying(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "point-blank", Shelf.Shot);
                Actor close = Near(arena, North * 3f);
                Actor far = Near(arena, North * 14f);
                float near = Blow(arena, wielder, close);
                float distant = Blow(arena, wielder, far);
                report.Check("Point Blank: Up Close is worth five times At Range", near > distant * 4.5f, $"{near} against {distant}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "long-goodbye", Shelf.Shot);
                Actor first = Near(arena, North * 4f);
                Actor second = Near(arena, North * 8f);
                Actor third = Near(arena, North * 12f);
                Release(arena, wielder, North, 20f);
                arena.Run(1);
                report.Check("The Long Goodbye: each Enemy passed through makes the next blow bigger", Arena.Lost(third) > Arena.Lost(second) && Arena.Lost(second) > Arena.Lost(first) && Arena.Lost(first) > 0f, $"{Arena.Lost(first)}, {Arena.Lost(second)}, {Arena.Lost(third)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "rainmaker", Shelf.Shot, Flat(Stat.ProjectileCount, -1f));
                Actor target = Near(arena, North * 10f);
                Release(arena, wielder, North, 10f);
                arena.Run(0.1);
                bool falling = (wielder.Sheet.Profile(0).First.Tags & Tags.Falling) != 0 && wielder.Sheet.Profile(0).Lobs;
                arena.Run(2.5);
                report.Check("Rainmaker: what's loosed goes up, counts as Falling, and comes down where it was aimed", falling && Arena.Lost(target) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "fork-in-the-road", Shelf.Shot);
                Near(arena, North * 6f);
                Release(arena, wielder, North, 20f);
                int most = 0;
                for (int i = 0; i < 60; i++)
                {
                    arena.Run(1.0 / 60.0);
                    most = Math.Max(most, Count<ProjectileComponent>(arena));
                }
                report.Check("Fork in the Road: a shot splits in two on its first Hit", most >= 2, $"{most}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "harpoon", Shelf.Shot);
                Actor target = Near(arena, North * 10f);
                target.Still = false;
                Release(arena, wielder, North, 20f);
                bool pulled = false;
                for (int i = 0; i < 60 && !pulled; i++)
                {
                    arena.Run(1.0 / 60.0);
                    if (Arena.Lost(target) > 0f) pulled = Vector2.Dot(target.Push, wielder.Position - target.Position) > 0f;
                }
                report.Check("The Harpoon: what it strikes is dragged in", pulled);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor caster = Wielder(arena, "stormglass", new[] { Shelf.Spark }, Wand);
                Actor plain = Wielder(arena, null, new[] { Shelf.Spark }, Wand, Post + North * 30f, Flat(Stat.Chain, 3f));
                Actor first = Near(arena, North * 4f);
                Near(arena, North * 7f);
                Actor plainFirst = Near(arena, North * 34f);
                Near(arena, North * 37f);
                Release(arena, caster, North, 4f);
                Release(arena, plain, North, 4f);
                arena.Run(2);
                report.Check("The Stormglass Rod: a chain goes back and forth between two", Arena.Lost(first) > Arena.Lost(plainFirst) * 1.4f, $"{Arena.Lost(first)} against {Arena.Lost(plainFirst)}");
            }
        }

        private static void Writing(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "both-ways", Shelf.Strike);
                Actor behind = Near(arena, West * 3f);
                Release(arena, wielder, East, 3f);
                arena.Run(0.1);
                report.Check("Both Ways: a Book also goes the other way", Arena.Lost(behind) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "run-on", Shelf.Strike);
                Actor target = Near(arena, East * 3f);
                float single = Blow(arena, wielder, target);
                float before = Arena.Lost(target);
                Release(arena, wielder, East, 3f);
                arena.Run(0.2);
                report.Near("Run-On Sentence: a Book starts over once more, at half", (Arena.Lost(target) - before) / single, 1f + Rulebook.RunOnStrength, 0.05f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "minds-eye", Shelf.Strike);
                Actor far = Near(arena, East * 15f);
                Release(arena, wielder, East, 15f);
                arena.Run(0.1);
                report.Check("The Mind's Eye: a blow lands where it's aimed", Arena.Lost(far) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor caster = Wielder(arena, "second-opinion", new[] { Shelf.Lance }, Wand);
                Actor target = Near(arena, East * 3f);
                // A hit is rolled within its spread, so what one comes to is the average of many, and so is what a cast comes to: one of each was a coin toss either side of twice.
                const int Tries = 40;
                float single = 0f;
                for (int i = 0; i < Tries; i++) single += Blow(arena, caster, target) / Tries;
                float before = Arena.Lost(target);
                for (int i = 0; i < Tries / 2; i++)
                {
                    Release(arena, caster, East, 3f);
                    arena.Run(0.1);
                }
                report.Near("Second Opinion: a Spell Hits twice", (Arena.Lost(target) - before) / (Tries / 2) / single, 2f, 0.05f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "butterfingers", Shelf.Strike);
                Actor target = Near(arena, East * 3f);
                float single = Blow(arena, wielder, target);
                int nothing = 0, twice = 0;
                for (int i = 0; i < 60; i++)
                {
                    float before = Arena.Lost(target);
                    Release(arena, wielder, East, 3f);
                    arena.Run(0.4);
                    float times = (Arena.Lost(target) - before) / single;
                    if (times < 0.5f) nothing++;
                    if (times > 1.5f) twice++;
                }
                report.Check("Butterfingers: some Books do nothing, and some go twice", nothing > 0 && twice > 0, $"{nothing} did nothing and {twice} went twice, in 60");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "short-story", Shelf.Strike);
                Actor longer = Wielder(arena, "short-story", new[] { Shelf.ChainLightning }, Wand, Post + North * 20f);
                float alone = wielder.Sheet.Profile(0).First.Potency;
                float turned = longer.Sheet.Profile(0).Pages[1].Potency / longer.Sheet.Profile(0).Pages[1].Page.Potency;
                report.Check("The Short Story: a Book of one Page is far stronger, and a Turned Page weaker", alone > 1.9f && turned < 0.6f, $"{alone} and {turned}");
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("hair-trigger"), 2, Shelf.Retort)))
            {
                BookWatch? watch = arena.Hero.Watches.FirstOrDefault();
                float asks = Shelf.Retort.Source!.When!.Amount;
                report.Check("The Hair Trigger: a Trigger Book waits for half as much", watch != null && MathF.Abs(watch.Amount - asks * Rulebook.QuickTriggerShare) < 0.01f, $"{watch?.Amount} of {asks}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "bedside-manner", Shelf.Mend);
                Actor beside = Near(arena, North * 3f);
                Release(arena, wielder, North, 1f);
                arena.Run(0.1);
                report.Check("Bedside Manner: what's restored hurts what's around", Arena.Lost(beside) > 0f);
            }
        }

        private static void Defending(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "turned-cheek", Shelf.Strike, Flat(Stat.BlockChance, 100f));
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 500f));
                Actor target = Near(arena, North * -3f);
                float plain = Blow(arena, wielder, target);
                for (int i = 0; i < 40 && wielder.Kept <= 0f; i++) arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                float kept = wielder.Kept;
                float back = Blow(arena, wielder, target);
                report.Check("The Turned Cheek: what's Blocked is kept, and goes into the next blow", kept > 0f && back > plain + kept * 0.9f && wielder.Kept == 0f, $"{kept} kept, {plain} then {back}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "the-door", Shelf.Strike);
                Actor foe = Near(arena, North * 3f);
                arena.Run(0.1);
                for (int i = 0; i < 20; i++) arena.Combat.Hit(foe, foe.Sheet.Profile(1).First, wielder, 1f, foe.Position);
                report.Check("The Door: standing still, nothing that flies gets through", Arena.Lost(wielder) == 0f, $"{Arena.Lost(wielder)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "third-stroke", Shelf.Strike);
                Actor foe = Near(arena, North * 3f);
                int blocked = 0;
                for (int i = 0; i < 9; i++)
                {
                    float before = Arena.Lost(wielder);
                    arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                    if (Arena.Lost(wielder) == before) blocked++;
                }
                report.Check("The Third Stroke: one Hit in three is Blocked, exactly", blocked == 3, $"{blocked} of 9");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "eater-of-words", Shelf.Strike);
                Actor foe = arena.Place(new Loadout(1, Wand, new[] { Flat(Stat.Health, 1000f), Added(DamageType.Physical, 20f, Tags.Spell) }, new[] { Shelf.Firebolt }), Post + North * 3f);
                wielder.Vitals.Mana = 0f;
                for (int i = 0; i < 60; i++) arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("Eater of Words: a Blocked Spell is Mana", wielder.Vitals.Mana > 0f, $"{wielder.Vitals.Mana}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "stillness", Shelf.Strike, Flat(Stat.Barrier, 100000f));
                Actor foe = Near(arena, North * 3f, 1000000f, Chance(Tags.Bleed, 100f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                bool spared = wielder.Afflictions.BleedLeft <= 0f;
                wielder.Vitals.Barrier = 0f;
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("The Stillness: no Ailment gets in while there's Barrier, and does without", spared && wielder.Afflictions.BleedLeft > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "panacea", Shelf.Strike);
                Actor foe = Near(arena, North * 3f, 1000000f, Chance(Tags.Bleed, 100f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("Panacea: no Ailment at all", wielder.Afflictions.BleedLeft <= 0f && wielder.Afflictions.ChillLeft <= 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "parry-and-thrust", Shelf.Strike, Flat(Stat.BlockChance, 100f));
                Actor foe = Near(arena, North * 3f);
                for (int i = 0; i < 20 && !Has(wielder, BuffCatalog.Riposte); i++) arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("Parry and Thrust: a Block is a Riposte", Has(wielder, BuffCatalog.Riposte));
                arena.Run(0.05);
                report.Check("And the next blow is a Critical Hit", wielder.Sheet.Profile(0).First.CritChance >= 100f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "hedgehog", Shelf.Strike);
                Actor foe = Near(arena, North * 3f);
                arena.Combat.Hit(foe, foe.Sheet.Profile(1).First, wielder, 1f, foe.Position);
                report.Check("The Hedgehog: even what flies is Reflected", Arena.Lost(foe) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "unbowed", Shelf.Strike);
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 100000f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("Unbowed: no Hit takes more than a share of Health", !wielder.Dead && Arena.Lost(wielder) <= wielder.Sheet.MaxHealth * Rulebook.HitCapShare + 0.01f, $"{Arena.Lost(wielder)} of {wielder.Sheet.MaxHealth}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "salamander-skin", Shelf.Strike, Resist(Tags.Fire, 75f));
                Actor plain = Wielder(arena, null, new[] { Shelf.Strike }, null, Post + North * 20f, Resist(Tags.Fire, 75f));
                Actor foe = Near(arena, North * 3f);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, plain, 1f, foe.Position);
                report.Check("Salamander Skin: everything arrives as Fire, and Fire Resistance takes it", Arena.Lost(wielder) < Arena.Lost(plain) * 0.3f, $"{Arena.Lost(wielder)} against {Arena.Lost(plain)}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "second-skin", Shelf.Strike);
                Actor burner = arena.Place(new Loadout(1, Wand, new[] { Flat(Stat.Health, 1000f), Added(DamageType.Physical, 20f, Tags.Spell) }, new[] { Shelf.Firebolt }), Post + North * 3f);
                Actor freezer = arena.Place(new Loadout(1, Wand, new[] { Flat(Stat.Health, 1000f), Added(DamageType.Physical, 20f, Tags.Spell) }, new[] { Shelf.FrostShards }), Post + North * -3f);
                arena.Combat.Hit(burner, burner.Sheet.Profile(0).First, wielder, 1f, burner.Position);
                bool fire = Has(wielder, BuffCatalog.Inured[(int)DamageType.Fire]);
                arena.Combat.Hit(freezer, freezer.Sheet.Profile(0).First, wielder, 1f, freezer.Position);
                report.Check("Second Skin: Inured to what last got through, and only that", fire && Has(wielder, BuffCatalog.Inured[(int)DamageType.Cold]) && !Has(wielder, BuffCatalog.Inured[(int)DamageType.Fire]));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "the-exchange", Shelf.Strike, Flat(Stat.Strength, 100f));
                Actor plain = Wielder(arena, null, new[] { Shelf.Strike }, null, Post + North * 20f, Flat(Stat.Strength, 100f));
                report.Check("The Exchange: Strength gives Mana, and no Health", wielder.Sheet.MaxMana > plain.Sheet.MaxMana + 90f && wielder.Sheet.MaxHealth < plain.Sheet.MaxHealth - 90f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "great-equaliser", Shelf.Strike, Resist(Tags.Fire, 60f));
                report.Check("The Great Equaliser: the best Resistance is all of them", wielder.Sheet.Resistance[(int)DamageType.Cold] == 60f && wielder.Sheet.Resistance[(int)DamageType.Lightning] == 60f);
            }
        }

        private static void Dying(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "patient-beast", Shelf.Strike);
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 100000f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("The Patient Beast: death waits for a kill", !wielder.Dead && wielder.Vitals.Health >= 1f && wielder.GaspLeft > 0f);
                arena.Run(Rulebook.GaspSeconds + 0.5);
                report.Check("And doesn't wait for ever", wielder.Dead);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "patient-beast", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 100000f));
                Actor weak = Near(arena, North * -3f, 10f);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                Blow(arena, wielder, weak);
                arena.Run(Rulebook.GaspSeconds + 0.5);
                report.Check("But a kill in time brings Health back", !wielder.Dead && wielder.Vitals.Health > wielder.Sheet.MaxHealth * 0.3f, $"{wielder.Vitals.Health}");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "last-word", Shelf.Strike, Flat(Stat.Barrier, 200f));
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 500f));
                Actor beside = Near(arena, North * -3f);
                for (int i = 0; i < 40 && wielder.Vitals.Barrier > 0f; i++) arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                arena.Run(0.1);
                report.Check("The Last Word: Barrier running out goes off around you", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "midas-touch", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor victim = Near(arena, North * 3f, 10f);
                Blow(arena, wielder, victim);
                report.Check("The Midas Touch: what it kills drops nothing", victim.Dead && victim.Plundered);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "quiet-man", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor victim = Near(arena, North * 3f, 10f);
                Blow(arena, wielder, victim);
                arena.Run(0.05);
                report.Check("The Quiet Man's Mask: a kill and you're Unseen", Has(wielder, BuffCatalog.Vanish) && wielder.Sheet.Stealth >= Rulebook.MostStealth);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "string-of-teeth", Shelf.Strike, More(Stat.Damage, 100000f));
                Actor rare = Near(arena, North * 3f, 10f);
                rare.Nature |= Condition.Rare;
                Blow(arena, wielder, rare);
                report.Check("A String of Teeth: a Rare slain is counted", wielder.RaresSlain == 1);
            }
        }

        private static void Company(Report report, WorldCache worlds, WorldRequest request)
        {
            // Two bones each, raised around whoever calls them.
            Actor Raised(Arena arena, string unique, params Mod[] more)
            {
                Actor wielder = Wielder(arena, unique, new[] { Shelf.Strike, Shelf.RaiseBones }, null, null, more);
                Release(arena, wielder, East, 1f, 1);
                arena.Run(0.2);

                // Only what's being tested does anything: bones left to themselves would join in.
                foreach (Actor minion in arena.Session.Minions) minion.Still = true;
                return wielder;
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Raised(arena, "conductors-baton");
                Actor target = Near(arena, East * 3f);
                float single = Blow(arena, wielder, target);
                float before = Arena.Lost(target);
                Release(arena, wielder, East, 3f);
                arena.Run(0.2);
                int bones = arena.Session.Minions.Count(minion => minion.Owner == wielder && !minion.Dead);
                report.Check("The Conductor's Baton: each Minion uses what you use", bones == 2 && Arena.Lost(target) - before > single * 1.3f, $"{bones} bones, {single} alone, {Arena.Lost(target) - before} together");
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Raised(arena, "bone-cage");
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 100000f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("The Bone Cage: a Minion dies in your place", !wielder.Dead && arena.Session.Minions.Count(minion => minion.Owner == wielder && !minion.Dead) == 1);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Raised(arena, "shield-of-the-host");
                Actor foe = Near(arena, North * 3f, 1000000f, More(Stat.Damage, 50f));
                Actor minion = arena.Session.Minions.First(one => one.Owner == wielder && !one.Dead);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, wielder, 1f, foe.Position);
                report.Check("Shield of the Host: a Minion takes part of a Hit", Arena.Lost(minion) > 0f && Arena.Lost(wielder) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Raised(arena, "shepherds-bell");
                Actor minion = arena.Session.Minions.First(one => one.Owner == wielder && !one.Dead);
                Actor beside = arena.Dummy(minion.Position - arena.At + North * 2f);
                arena.Combat.Dismiss(minion);
                arena.Run(0.1);
                report.Check("The Shepherd's Bell: a Minion that goes, goes with a bang", Arena.Lost(beside) > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Raised(arena, "undertakers-ring");
                arena.Combat.Dismiss(arena.Session.Minions.First(one => one.Owner == wielder && !one.Dead));
                report.Check("The Undertaker's Ring: a Minion lost is Mourning", Has(wielder, BuffCatalog.Mourning));
            }
        }

        private static void Heroes(Report report, WorldCache worlds, WorldRequest request)
        {
            int Attacks(Arena arena)
            {
                arena.Player.Script = () =>
                {
                    var intent = new HeroIntent { Aim = East * 3f };
                    intent.Use(0);
                    return intent;
                };
                arena.Run(1.5);
                arena.Player.Script = null;
                return arena.Player.Uses[0];
            }

            using (Arena arena = Enter(worlds, request))
            using (Arena hymn = Enter(worlds, request, Wearing("perpetual-hymn")))
            {
                int plain = Attacks(arena);
                int sung = Attacks(hymn);
                report.Check("The Perpetual Hymn: Active Books can't be used, where they could be without it", plain > 0 && sung == 0, $"{plain} without, {sung} with");
                report.Check("And Toggle Books pulse at full strength", hymn.Skill(Shelf.Inferno).AuraStrength == 1f && arena.Skill(Shelf.Inferno).AuraStrength < 1f);
            }

            using (Arena arena = Enter(worlds, request, AllIn()))
            {
                Actor target = arena.Dummy(East * 3f);
                arena.Hero.Vitals.Mana = arena.Hero.Sheet.MaxMana;
                arena.Run(0.1);
                arena.Player.StrikeAt = target.Position;
                arena.Run(1.5);
                float full = Arena.Lost(target);
                float spent = arena.Hero.Spent;
                bool emptied = arena.Truth.Sim.Mana < arena.Hero.Sheet.MaxMana * 0.1f;
                arena.Player.StrikeAt = target.Position;
                arena.Run(1.5);
                float empty = Arena.Lost(target) - full;
                report.Check("All In: a Book spends all the Mana there is", emptied && spent > 1000f, $"{spent} spent");
                report.Near("And what it spent is what it's worth", GameServer.Worth(arena.Hero), 1f + arena.Hero.Spent / Rulebook.ManaPowerStep / 100f, 0.001f);
                report.Check("And is worth what it spent", full > empty * 2f, $"{full} then {empty}");
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("overdrawn"), 2, Shelf.Firebolt)))
            {
                arena.Hero.Vitals.Mana = 0f;
                arena.Player.HeldSlots = 1 << 2;
                arena.Run(0.6);
                arena.Player.HeldSlots = 0;
                float mana = arena.Truth.Sim.Mana;
                float health = arena.Hero.Vitals.Health;
                arena.Run(1);
                report.Check("Overdrawn: a Book can be used with no Mana, and it bleeds you while you owe", arena.Player.Uses[2] >= 1 && mana < 0f && arena.Hero.Vitals.Health < health, $"{arena.Player.Uses[2]} uses, mana {mana}");
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("moneylenders-ring"), 2, Shelf.Firebolt)))
            {
                arena.Truth.Hero.Gold = 1000;
                arena.Run(0.2);
                float mana = arena.Truth.Sim.Mana;
                arena.Player.HeldSlots = 1 << 2;
                arena.Run(0.4);
                arena.Player.HeldSlots = 0;
                arena.Run(0.2);
                report.Check("The Moneylender's Ring: Books cost Gold, and no Mana", arena.Player.Uses[2] >= 1 && arena.Truth.Hero.Gold < 1000 && arena.Truth.Sim.Mana >= mana - 0.5f, $"{arena.Truth.Hero.Gold} gold, mana {mana} then {arena.Truth.Sim.Mana}");
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("hollow-crown"), 2, Shelf.Firebolt)))
            {
                BookProfile bolt = arena.Skill(Shelf.Firebolt);
                report.Check("The Hollow Crown: Books cost nothing, and rest instead", bolt.Cost == 0f && bolt.Cooldown > 0f, $"cost {bolt.Cost}, rests {bolt.Cooldown} s");
            }

            using (Arena arena = Enter(worlds, request, Wearing("borrowed-time")))
            {
                BookProfile nova = arena.Skill(Shelf.FrostNova);
                report.Check("Borrowed Time: no Cooldown, and what it would have been is Cost", nova.Cooldown == 0f && nova.Cost >= Rulebook.RestToCost * Rulebook.RestShort, $"cost {nova.Cost}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("seven-league")))
            {
                bool safe = true;
                arena.Player.Script = () => new HeroIntent { Dash = true, Move = East };
                arena.Run(0.1);
                arena.Player.Script = null;
                safe = arena.Truth.Sim.Safe > 0f;
                report.Check("Seven League Boots: a Dodge keeps nothing off", !safe && arena.Truth.Sim.DodgeCooldown > 0f);
            }

            using (Arena arena = Enter(worlds, request, Wearing("hares-foot")))
            {
                float mana = arena.Hero.Vitals.Mana;
                arena.Player.Script = () => new HeroIntent { Dash = true, Move = East };
                arena.Run(0.1);
                arena.Player.Script = null;
                report.Check("The Hare's Foot: a Dodge costs Mana, and is ready almost at once", arena.Truth.Sim.Mana < mana - 5f && arena.Hero.Sheet.DodgeCooldown <= Rulebook.LeastDodgeCooldown + 0.001f, $"{mana} then {arena.Truth.Sim.Mana}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("dead-weight")))
            {
                arena.Player.Script = () => new HeroIntent { Dash = true, Move = East };
                arena.Run(0.2);
                arena.Player.Script = null;
                report.Check("Dead Weight: no Dodge at all", arena.Truth.Sim.DodgeCooldown <= 0f && arena.Truth.Sim.DashLeft <= 0f);
            }

            using (Arena arena = Enter(worlds, request, Wearing("cindertread")))
            {
                Actor target = arena.Dummy(East * 3f);
                arena.Player.Script = () => new HeroIntent { Dash = true, Move = West };
                arena.Run(0.3);
                arena.Player.Script = null;
                report.Check("Cindertread: a Dodge leaves a ring of embers", Arena.Lost(target) > 0f);
            }

            using (Arena arena = Enter(worlds, request, Wearing("drunkards-vest")))
            {
                arena.Hero.Vitals.Health = arena.Hero.Sheet.MaxHealth * 0.5f;
                float health = arena.Hero.Vitals.Health;
                arena.Player.Script = () => new HeroIntent { Flask = true };
                arena.Run(0.05);
                arena.Player.Script = null;
                arena.Run(0.1);
                report.Check("The Drunkard's Vest: a Flask is Dutch Courage, and heals nothing", Has(arena.Hero, BuffCatalog.Courage) && arena.Hero.Vitals.Health <= health + 0.5f);
            }

            using (Arena arena = Enter(worlds, request, Wearing("beggars-staff")))
            {
                arena.Truth.Hero.Gold = 500;
                Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 1000000f));
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, arena.Hero, 1f, foe.Position);
                arena.Run(0.2);
                report.Check("Beggar's Staff: a fall takes all the Gold", arena.Truth.Hero.Gold == 0, $"{arena.Truth.Hero.Gold}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("gilded-aegis")))
            {
                arena.Truth.Hero.Gold = 10000;
                Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 200f));
                arena.Run(0.1);
                arena.Combat.Hit(foe, foe.Sheet.Profile(0).First, arena.Hero, 1f, foe.Position);
                report.Check("The Gilded Aegis: Gold pays for part of a Hit", arena.Truth.Hero.Gold < 10000, $"{arena.Truth.Hero.Gold}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("bookmark")))
            {
                Actor weak = arena.Dummy(East * 3f, 1f);
                arena.Hero.LastSlot = 1;
                arena.Truth.Sim.Cooldowns[1] = 5f;
                arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), weak, 1f, arena.At);
                report.Check("The Bookmark: a kill takes the last Book used off Cooldown", weak.Dead && arena.Truth.Sim.Cooldowns[1] == 0f);
            }

            using (Arena arena = Enter(worlds, request, Wearing("blindfold")))
            {
                arena.Run(0.2);
                report.Check("The Blindfold: what a client draws by knows it can't see", arena.Player.Client.Kit.Sheet.Has(Rule.Blind));
            }

            using (Arena arena = Enter(worlds, request, Wearing("lodestone")))
            {
                Actor drawn = arena.Dummy(East * 12f);
                drawn.Still = false;
                arena.Combat.Draw(arena.Hero, 0.1f);
                report.Check("The Lodestone: Enemies around are drawn in", Vector2.Dot(drawn.Push, arena.At - drawn.Position) > 0f);
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("blood-of-the-flock"), 3, Shelf.RaiseBones, 2, Shelf.Firebolt)))
            {
                arena.Player.HeldSlots = 1 << 2;
                arena.Run(0.6);
                arena.Player.HeldSlots = 0;
                int without = arena.Player.Uses[2];
                arena.Player.HeldSlots = 1 << 3;
                arena.Run(0.4);
                arena.Player.HeldSlots = 0;
                arena.Run(0.2);
                int raised = arena.Session.Minions.Count(minion => minion.Owner == arena.Hero && !minion.Dead);
                arena.Player.HeldSlots = 1 << 2;
                arena.Run(0.4);
                arena.Player.HeldSlots = 0;
                arena.Run(0.1);
                int left = arena.Session.Minions.Count(minion => minion.Owner == arena.Hero && !minion.Dead);
                report.Check("Blood of the Flock: no Minion, no Book", without == 0, $"{without}");
                report.Check("And each Book takes one", raised > 0 && left < raised && arena.Skill(Shelf.Firebolt).Cost == 0f, $"{raised} then {left}");
            }

            using (Arena arena = Enter(worlds, request, With(Wearing("many-voices"), 2, Shelf.Firebolt)))
            {
                // Two Books used Recently either way, so that only the repeat is different between the two blows measured.
                Actor target = arena.Dummy(East * 3f);
                arena.Player.StrikeAt = target.Position;
                arena.Run(1);
                arena.Player.HeldSlots = 1 << 2;
                arena.Run(0.2);
                arena.Player.HeldSlots = 0;
                arena.Run(0.8);
                float before = Arena.Lost(target);
                arena.Player.StrikeAt = target.Position;
                arena.Run(0.8);
                float fresh = Arena.Lost(target) - before;
                before = Arena.Lost(target);
                arena.Player.StrikeAt = target.Position;
                arena.Run(0.8);
                float repeated = Arena.Lost(target) - before;
                float worth = GameServer.Worth(arena.Hero);
                report.Check("Many Voices: a Book used twice running is half as strong the second time", arena.Hero.Streak == 2 && MathF.Abs(worth - (1f - Rulebook.RepeatLess)) < 0.001f && repeated < fresh * 0.75f, $"worth {worth}, {fresh} then {repeated}, at a streak of {arena.Hero.Streak}");
            }
        }

        private static HeroState AllIn()
        {
            HeroState hero = Wearing("all-in");
            hero.Equipped[WornSlot.Finger1] = new Item { Name = "Lab Ring", Slot = ItemSlot.Finger, Level = 1, Lines = { new ItemLine("mana", 2000f) } };
            return hero;
        }

        private static HeroState With(HeroState hero, params object[] slotsAndBooks)
        {
            for (int i = 0; i + 1 < slotsAndBooks.Length; i += 2) hero.Bar[(int)slotsAndBooks[i]] = GameLab.Written((BookPlan)slotsAndBooks[i + 1]);
            return hero;
        }

        private static void Counting(Report report, WorldCache worlds, WorldRequest request)
        {
            using (Arena arena = Enter(worlds, request, Wearing("beacon")))
            {
                for (int i = 0; i < 6; i++) arena.Dummy(new Vector2(MathF.Cos(i), MathF.Sin(i)) * 5f);
                arena.Run(0.3);
                report.Check("The Beacon: Enemies near you are counted, and you're Surrounded", arena.Hero.Counters.EnemiesNear >= 6 && (arena.Hero.State & Condition.Surrounded) != 0, $"{arena.Hero.Counters.EnemiesNear}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("hermits-habit")))
            {
                arena.Run(0.3);
                report.Check("The Hermit's Habit: with nobody near, you're Alone", (arena.Hero.State & Condition.Alone) != 0);
            }

            using (Arena arena = Enter(worlds, request, Wearing("keyholder"), new LandTerms { Omens = { new ItemLine(Omens.Volatile.Id, 50f) } }))
            {
                arena.Run(0.3);
                report.Check("The Keyholder's Chain: the Land Level and Omens are counted", arena.Hero.Counters.LandLevel == request.Level && arena.Hero.Counters.Omens == 1, $"{arena.Hero.Counters.LandLevel}, {arena.Hero.Counters.Omens}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("bare-truth")))
            {
                arena.Run(0.3);
                report.Check("The Bare Truth: places with nothing worn are counted", arena.Hero.Counters.EmptySlots >= 6, $"{arena.Hero.Counters.EmptySlots}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("collectors-seal")))
            {
                arena.Run(0.3);
                report.Check("The Collector's Seal: Uniques worn are counted", arena.Hero.Counters.UniquesWorn == 1);
            }

            using (Arena arena = Enter(worlds, request, Wearing("hoarders-harness")))
            {
                for (int i = 0; i < 5; i++) arena.Truth.Hero.Pickup(new Item { Name = "Stone", Slot = ItemSlot.Finger, Level = 1 });
                arena.Run(0.3);
                report.Check("The Hoarder's Harness: what's carried is counted", arena.Hero.Counters.Carried >= 5, $"{arena.Hero.Counters.Carried}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("festering-bandages")))
            {
                arena.Hero.Afflictions.BleedLeft = 5f;
                arena.Hero.Afflictions.ChillLeft = 5f;
                arena.Run(0.2);
                report.Check("Festering Bandages: Ailments on you are counted", arena.Hero.Counters.Ailments >= 2, $"{arena.Hero.Counters.Ailments}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("slow-to-anger")))
            {
                Actor target = arena.Dummy(East * 3f);
                for (int i = 0; i < 6; i++)
                {
                    arena.Combat.Hit(arena.Hero, arena.Page(Shelf.Strike), target, 1f, arena.At);
                    arena.Run(1);
                }
                report.Check("Slow to Anger: seconds spent Fighting are counted", arena.Hero.Counters.FightSeconds >= 5, $"{arena.Hero.Counters.FightSeconds}");
            }

            using (Arena arena = Enter(worlds, request, Wearing("crown-of-thorns")))
            {
                arena.Run(0.2);
                float health = arena.Hero.Vitals.Health;
                arena.Run(1);
                report.Check("Crown of Thorns: you're always Bleeding, and it drains you", (arena.Hero.State & Condition.Bleeding) != 0 && arena.Hero.Vitals.Health < health);
            }

            using (Arena arena = Enter(worlds, request, Wearing("long-road")))
            {
                arena.Hero.State = Condition.Moving;
                arena.Combat.Presence(arena.Hero, 1.1f);
                report.Check("The Long Road: walking is Long Stride", Has(arena.Hero, BuffCatalog.Stride));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "patient-eye", Shelf.Shot);
                Actor target = Near(arena, North * 6f);
                arena.Run(4.7);
                report.Check("The Patient Eye: standing still is Aim, up to three", Stacks(wielder, BuffCatalog.Aim) == 3, $"{Stacks(wielder, BuffCatalog.Aim)}");
                Blow(arena, wielder, target);
                report.Check("And a Hit spends it", !Has(wielder, BuffCatalog.Aim));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "eye-of-the-storm", Shelf.Strike);
                Actor target = Near(arena, North * 4f);
                arena.Run(1.2);
                report.Check("The Eye of the Storm: every second, what's near is Shocked", target.Afflictions.ShockLeft > 0f);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "furnace", Shelf.Strike);
                Actor target = Near(arena, North * 4f);
                arena.Run(1.2);
                report.Check("The Furnace: you're always Burning, and so is what's near you", (wielder.State & Condition.Burning) != 0 && target.Afflictions.BurnLeft > 0f && wielder.Vitals.Health < wielder.Sheet.MaxHealth);
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "mudlarks", Shelf.Strike);
                wielder.Stepped = 7f;
                int zones = Count<ZoneStateComponent>(arena);
                arena.Combat.Presence(wielder, 1f / 60f);
                arena.Run(0.1);
                report.Check("Mudlark's Clogs: walking leaves Water", Count<ZoneStateComponent>(arena) > zones && arena.Combat.Land.InWater(wielder.Position));
            }

            using (Arena arena = Enter(worlds, request))
            {
                Actor wielder = Wielder(arena, "stormcallers-crook", new[] { Shelf.Strike }, null, null, Grant(Condition.InRain));
                Actor target = Near(arena, North * 8f);
                arena.Run(3);
                report.Check("Stormcaller's Crook: in Rain, the sky strikes", Arena.Lost(target) > 0f);

                // How often that is depends on the land, which is the point of it: this one is only looked at.
                int raining = 0, asked = 0;
                for (int minute = 0; minute < 60; minute++)
                {
                    for (int place = 0; place < 4; place++)
                    {
                        Vector2 at = arena.At + new Vector2(place * 200f, place * -150f);
                        asked++;
                        if ((arena.Combat.Land.Weather(at, minute * 60f) & Condition.InRain) != 0) raining++;
                    }
                }
                report.Note($"On this land it's raining {raining * 100 / Math.Max(asked, 1)}% of the time, as the server sees it");
            }
        }

        private static void Bosses(Report report)
        {
            int heroOnly = 0;
            for (int i = 0; i < 300; i++)
            {
                Bestiary.Boss(Bestiary.Warlord, 30, Bestiary.NoTraits, new WorldRandom(1000 + i), out Item[] worn, out _);
                if (worn.Any(item => item.Unique != null && UniqueCatalog.Find(item.Unique)!.HeroOnly)) heroOnly++;
            }
            report.Check("No boss wears what only a hero can use", heroOnly == 0, $"{heroOnly} of 300");
        }
    }
}
