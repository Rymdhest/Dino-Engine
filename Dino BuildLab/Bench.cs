using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.World;
using System.Diagnostics;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>What the system costs. The promise is that a blow costs the same however complicated a build gets, and that it never allocates.</summary>
    internal static class Bench
    {
        public static int Run(Options options)
        {
            var report = new Report();
            Measure(report);
            return report.Finish();
        }

        public static Mod[] HeavyBuild(int count)
        {
            var random = new Random(7);
            Tags[] scopes = { Tags.None, Tags.Fire, Tags.Spell, Tags.Elemental, Tags.Area, Tags.Fire | Tags.Spell, Tags.Melee, Tags.Projectile };
            var mods = new List<Mod> { Flat(Stat.Health, 500f), Flat(Stat.Mana, 200f), Flat(Stat.Armour, 800f), Resist(Tags.Elemental, 50f) };
            Stat[] stats = { Stat.Damage, Stat.CritChance, Stat.CritMultiplier, Stat.AreaOfEffect, Stat.CastSpeed, Stat.Health, Stat.Armour, Stat.HealthRegen, Stat.Accuracy };
            for (int i = 0; i < count; i++)
            {
                Stat stat = stats[random.Next(stats.Length)];
                Tags scope = scopes[random.Next(scopes.Length)];
                mods.Add(random.Next(5) == 0 ? More(stat, 5f, scope) : Inc(stat, random.Next(5, 30), scope));
            }
            for (int i = 0; i < count / 10; i++) mods.Add(Inc(Stat.Damage, 20f, Tags.Fire).Against(Condition.Burning));
            mods.Add(Convert(DamageType.Physical, DamageType.Fire, 50f));
            mods.Add(GainAs(DamageType.Fire, DamageType.Cold, 20f));
            return mods.ToArray();
        }

        private static void Measure(Report report)
        {
            var weapon = new WeaponProfile(50f, 70f, 0.6f, 5f, Tags.Sword);
            Mod[] heavy = HeavyBuild(600);
            var loadout = new Loadout(40, weapon, heavy, Shelf.All);
            ActorSheet sheet = loadout.SheetFor(Condition.None);
            PageProfile profile = sheet.Preview(Shelf.Firebolt).First;
            ActorSheet target = new Loadout(40, weapon, HeavyBuild(100), Shelf.All).SheetFor(Condition.None);
            var luck = new WorldRandom(11);
            Vitals vitals = Vitals.Full(target);

            void Blow()
            {
                HitPacket hit = default;
                Hits.Roll(profile, Condition.Burning | Condition.Chilled, luck, ref hit);
                Hits.Mitigate(target, ref hit, Condition.None, 6f, 0.2f);
                Hits.Pay(target, ref vitals, hit);
                if (vitals.Health <= 0f) vitals = Vitals.Full(target);
            }

            for (int i = 0; i < 200000; i++) Blow();

            const int Blows = 5_000_000;
            var clock = new Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            clock.Start();
            for (int i = 0; i < Blows; i++) Blow();
            clock.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            double nanoseconds = clock.Elapsed.TotalMilliseconds * 1e6 / Blows;
            report.Check("A blow allocates nothing", allocated == 0, $"{allocated} bytes over {Blows} blows");
            report.Check("A blow from a build of 600 modifiers takes under a microsecond", nanoseconds < 1000.0, $"{nanoseconds:0} ns");
            report.Note($"A blow, rolled, mitigated and paid: {nanoseconds:0} ns, so {1e9 / nanoseconds / 1e6:0.0} million a second on one core");

            PageProfile lean = new Loadout(40, weapon, new[] { Flat(Stat.Health, 500f), Inc(Stat.Damage, 50f) }, Shelf.All).SheetFor(Condition.None).Preview(Shelf.Firebolt).First;
            clock.Restart();
            for (int i = 0; i < Blows; i++)
            {
                HitPacket hit = default;
                Hits.Roll(lean, Condition.None, luck, ref hit);
                Hits.Mitigate(target, ref hit);
            }
            clock.Stop();
            double leanNanoseconds = clock.Elapsed.TotalMilliseconds * 1e6 / Blows;
            report.Note($"The same from a build of 2 modifiers, rolled and mitigated: {leanNanoseconds:0} ns. What costs is what counts against particular foes, of which that heavy build has 60. The other 540 cost nothing here.");

            const int Compiles = 2000;
            clock.Restart();
            for (int i = 0; i < Compiles; i++)
            {
                var fresh = new Loadout(40, weapon, heavy, Shelf.All);
                _ = fresh.SheetFor(Condition.None).Profile(Shelf.Firebolt);
            }
            clock.Stop();
            double compile = clock.Elapsed.TotalMilliseconds * 1000.0 / Compiles;
            report.Check("Compiling 600 modifiers and a skill takes under half a millisecond", compile < 500.0, $"{compile:0} us");
            report.Note($"A full recompile of 600 modifiers, with one skill profile: {compile:0} us. It happens when gear or talents change, or when a state a build cares about does.");

            clock.Restart();
            for (int i = 0; i < 20000000; i++) _ = loadout.SheetFor(Condition.Moving);
            clock.Stop();
            report.Note($"Asking for a sheet that's already made: {clock.Elapsed.TotalMilliseconds * 1e6 / 20000000:0} ns");
        }
    }
}
