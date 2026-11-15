using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_NetLab
{
    /// <summary>
    /// The account of how a hero fell (DEATHS.md), from real fights on a running server, read on the client it was sent to. It has to be true, so everything here is held against something else that
    /// knows: what Health really did, what the rules say each defence takes, and the server's own copy.
    /// </summary>
    internal static class DeathTests
    {
        private static readonly Vector2 East = new Vector2(1f, 0f);
        private const float Slack = 0.05f;
        private static bool _show;

        public static int Run(Options options)
        {
            var report = new Report();
            _show = options.Text("show") == "true";
            var request = GameLab.OpenIsle(options.Int("seed", 303));
            var worlds = new WorldCache();
            Console.WriteLine("Growing a world...");
            worlds.Get(request).Wait();

            report.Section("Blows");
            Blows(report, worlds, request);

            report.Section("Burning to death");
            Burning(report, worlds, request);

            report.Section("Defences");
            Resisted(report, worlds, request);
            Evaded(report, worlds, request);

            Shocked(report, worlds, request);

            report.Section("Saved");
            Saved(report, worlds, request);

            return report.Finish();
        }

        private static HeroState Hero(params ItemLine[] lines)
        {
            HeroState hero = HeroState.CreateNew();
            hero.Level = 30;
            if (lines.Length == 0) return hero;
            var ring = new Item { Name = "Lab Ring", Slot = ItemSlot.Finger, Level = 30, Rarity = Rarity.Magic };
            ring.Lines.AddRange(lines);
            hero.Equipped[WornSlot.Finger1] = ring;
            return hero;
        }

        // Strikes the hero with a page until they fall, a tenth of a second apart, and gives the account time to arrive.
        private static int StrikeDown(Arena arena, Actor foe, PageProfile page)
        {
            int struck = 0;
            for (int i = 0; i < 140 && !arena.Hero.Dead; i++)
            {
                arena.Combat.Hit(foe, page, arena.Hero, 1f, foe.Position);
                struck++;
                arena.Run(0.1);
            }
            arena.Run(1.5);
            return struck;
        }

        private static void Blows(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 300f));
            PageProfile strike = foe.Sheet.Profile(0).First;
            float most = arena.Hero.Sheet.MaxHealth;
            int struck = StrikeDown(arena, foe, strike);

            DeathRecap? recap = arena.Player.Client.Recap;
            report.Check("Whoever falls is told how", recap != null && arena.Hero.Dead);
            if (recap == null) return;

            RecapEntry? killer = recap.Killer;
            report.Check("What ended it is the blow that did, by whom and with what", killer is RecapEntry k && k.Kind == RecapKind.Hit && recap.SourceOf(k)?.Name == "Dummy" && recap.BookOf(k)?.Name == Shelf.Strike.Name,
                killer is RecapEntry e ? $"{e.Kind} by {recap.SourceOf(e)?.Name} with {recap.BookOf(e)?.Name}" : "nothing");
            report.Check("And it's the line that took Health to nothing", killer is { Health: <= 0f } && recap.Fatal == recap.Entries.FindLastIndex(entry => entry.ToHealth > 0f));
            report.Check("With the Book's card, as it stood for whoever used it", recap.BookOf(killer!.Value)?.Card is Card card && card.Title == Shelf.Strike.Name);
            int blows = recap.Entries.Count(entry => entry.Kind is RecapKind.Hit or RecapKind.Evaded or RecapKind.Blocked or RecapKind.Dodged);
            report.Check("Every blow is a line of it", blows == struck, $"{blows} lines for {struck} blows");

            var wrong = new List<string>();
            foreach (RecapEntry entry in recap.Entries.Where(entry => entry.Kind == RecapKind.Hit))
            {
                if (!AddsUp(entry, out string why)) wrong.Add(why);
            }
            report.Check("What arrived, less what each defence took off, is what got through", wrong.Count == 0, string.Join(" | ", wrong.Take(4)));
            report.Check("And what got through, less what anyone else took, is what was paid", recap.Entries.Where(entry => entry.Kind == RecapKind.Hit && !entry.Executed).All(entry =>
                MathF.Abs(entry.Got - entry.Shared - entry.Gold - entry.Capped - entry.Healed - entry.Stored - entry.Paid) < Slack));

            float lost = recap.Entries.Sum(entry => entry.ToHealth);
            float left = most + recap.Heals.Total - lost;
            report.Check("Health from full, with what came back and what every line took, comes to where they fell", MathF.Abs(left - killer.Value.Health) < 0.5f,
                $"{most} + {recap.Heals.Total:0.##} - {lost:0.##} = {left:0.##}, fell at {killer.Value.Health:0.##}");
            report.Check("Nothing touched Health that the account doesn't name", recap.Unaccounted < Slack, $"{recap.Unaccounted}");
            report.Check("And nothing came back, as nothing here heals, not even falling to nothing from below it", recap.Heals.Total < Slack, $"{recap.Heals.Total}");

            // The server's own copy, made again, against what came over the wire.
            DeathRecap again = arena.Truth.Ledger.Fall(false);
            report.Check("What arrives is what the server wrote down", again.Entries.Count == recap.Entries.Count && again.Fatal == recap.Fatal
                && MathF.Abs(again.Entries.Sum(entry => entry.Got) - recap.Entries.Sum(entry => entry.Got)) < Slack && again.Sources.Count == recap.Sources.Count);

            bool readable = true;
            try
            {
                for (int i = 0; i < recap.Entries.Count; i++) _ = RecapText.Entry(recap, i);
                readable = RecapText.Cause(recap).Contains("Dummy") && RecapText.Blow(recap).Contains("Health");
            }
            catch (Exception exception)
            {
                readable = false;
                report.Note(exception.ToString());
            }
            report.Check("And all of it can be read, a card for every line", readable);
        }

        // Everything the death screen would say of a fall, to read (--show true).
        private static void Show(DeathRecap recap)
        {
            Console.WriteLine();
            foreach (string line in new[] { RecapText.Title(recap), RecapText.Cause(recap), RecapText.Blow(recap) }) Console.WriteLine($"  {line}");
            for (int i = recap.Entries.Count - 1; i >= Math.Max(recap.Entries.Count - 4, 0); i--)
            {
                RecapRow row = RecapText.Row(recap, i);
                Console.WriteLine();
                Console.WriteLine($"  {row.When} | {row.Who} | {row.What} | {row.Outcome}");
                Print(RecapText.Entry(recap, i), "      ");
            }
        }

        private static void Print(Card card, string indent)
        {
            Console.WriteLine($"{indent}[{card.Title}] {card.Kind} {string.Join(" ", Cards.TagNames(card.Tags))}");
            foreach (CardLine line in card.Lines) Console.WriteLine(line.Look == LineLook.Gap ? "" : $"{indent}  {line.Text}{(line.Value.Length > 0 ? "  " + line.Value : "")}");
        }

        // Every type: what arrived, less each defence, is what got through.
        private static bool AddsUp(in RecapEntry entry, out string why)
        {
            for (int t = 0; t < DamageKinds.Count; t++)
            {
                Mitigation d = entry.Defences;
                float through = d.Arrived[t] - d.Resisted[t] - d.Armoured[t] - d.Reduced[t] - d.Flat[t] - d.Scaled[t] - d.Shocked[t];
                if (MathF.Abs(through - entry.Dealt[t]) > Slack + d.Arrived[t] * 1e-4f)
                {
                    why = $"{(DamageType)t}: {d.Arrived[t]:0.##} arrived, comes to {through:0.##}, {entry.Dealt[t]:0.##} got through";
                    return false;
                }
            }
            why = "";
            return true;
        }

        private static void Burning(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero());
            Actor foe = arena.Dummy(East * 3f);
            Afflictions burning = arena.Hero.Afflictions;
            burning.BurnSource = foe;
            burning.BurnLeft = 20f;
            burning.BurnPerSecond = arena.Hero.Sheet.MaxHealth * 0.37f;     // Uneven, so that the last moment goes past nothing.
            arena.Run(6);

            DeathRecap? recap = arena.Player.Client.Recap;
            report.Check("A hero burned to death is told so", recap?.Killer is { Kind: RecapKind.Burn } && recap.SourceOf(recap.Killer.Value)?.Name == "Dummy");
            if (recap == null) return;
            report.Check("As one line for the whole of the burn, however many moments it was", recap.Entries.Count(entry => entry.Kind == RecapKind.Burn) == 1);
            RecapEntry burn = recap.Entries.First(entry => entry.Kind == RecapKind.Burn);
            report.Check("Which says how long it went on", burn.Lasted > 1.5f, $"{burn.Lasted:0.##} s");
            report.Check("And it all adds up", AddsUp(burn, out string why) && MathF.Abs(burn.Got - burn.Paid) < Slack, why);
            report.Check("And nothing's unaccounted", recap.Unaccounted < Slack, $"{recap.Unaccounted}");
            report.Check("And nothing came back, though Health is set to nothing from below it in the same moment", recap.Heals.Total < Slack, $"{recap.Heals.Total}");
            report.Check("And the sentence says Burn, by whom", RecapText.Cause(recap).Contains("Burn") && RecapText.Cause(recap).Contains("Dummy"), RecapText.Cause(recap));
        }

        private static void Resisted(Report report, WorldCache worlds, WorldRequest request)
        {
            const float Resistance = 40f;
            using var arena = new Arena(worlds, request, Hero(new ItemLine("fire-resistance", Resistance)));
            report.Check("A ring of Fire Resistance gives what it says", MathF.Abs(arena.Hero.Sheet.Resistance[(int)DamageType.Fire] - Resistance) < 0.01f);
            Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 300f), More(Stat.AilmentChance, -100f));
            StrikeDown(arena, foe, foe.Sheet.Profile(1).First);

            DeathRecap? recap = arena.Player.Client.Recap;
            report.Check("A hero struck down by Fire is told so", recap?.Killer is RecapEntry fire && fire.Mostly == DamageType.Fire);
            if (recap == null) return;
            if (_show) Show(recap);
            List<RecapEntry> hits = recap.Entries.Where(entry => entry.Kind == RecapKind.Hit).ToList();
            report.Check("Fire Resistance took its share of every Hit, as the rules say", hits.Count > 0 && hits.All(entry => MathF.Abs(entry.Defences.Resisted[(int)DamageType.Fire] - entry.Defences.Arrived[(int)DamageType.Fire] * Resistance / 100f) < Slack));
        }

        // A Shock only ever adds to what's taken, and has to be said so: the account once said a Shock took damage off.
        private static void Shocked(Report report, WorldCache worlds, WorldRequest request)
        {
            const float Shock = 0.5f;
            using var arena = new Arena(worlds, request, Hero());
            Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 300f), More(Stat.AilmentChance, -100f));
            arena.Hero.Afflictions.ShockLeft = 60f;
            arena.Hero.Afflictions.ShockEffect = Shock;
            StrikeDown(arena, foe, foe.Sheet.Profile(0).First);
            DeathRecap? recap = arena.Player.Client.Recap;
            List<RecapEntry> hits = recap?.Entries.Where(entry => entry.Kind == RecapKind.Hit).ToList() ?? new();
            report.Check("Being Shocked adds what the rules say to every Hit", hits.Count > 0 && hits.All(entry => MathF.Abs(-entry.Defences.Shocked.Total - entry.Got * Shock / (1f + Shock)) < Slack));
            Card? card = recap != null && hits.Count > 0 ? RecapText.Entry(recap, recap.Entries.IndexOf(hits[0])) : null;
            string? said = card?.Lines.FirstOrDefault(line => line.Text == "Shocked").Value;
            report.Check("And a line's card says it added to what got through, not that it took any off", said != null && said.StartsWith("+"), said ?? "no line");
        }

        private static void Evaded(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, Hero(new ItemLine("evasion", 400f)));
            float evasion = arena.Hero.Sheet.Evasion;
            // Light blows, so that there are plenty of them to Evade before the hero falls.
            var mods = new List<Mod> { Flat(Stat.Health, 1000000f), Flat(Stat.Mana, 100f), Flat(Stat.Accuracy, 100f) };
            var loadout = new Loadout(1, new WeaponProfile(10f, 10f, 0.6f, 0f, Tags.Sword), mods, new[] { Shelf.Strike });
            Actor foe = arena.Place(loadout, East * 3f);
            PageProfile strike = foe.Sheet.Profile(0).First;
            StrikeDown(arena, foe, strike);

            DeathRecap? recap = arena.Player.Client.Recap;
            if (recap == null)
            {
                report.Check("A hero with Evasion is told how they fell", false);
                return;
            }
            float odds = Hits.EvadeChance(evasion, strike.Accuracy) * 100f;
            List<RecapEntry> tried = recap.Entries.Where(entry => entry.Kind is RecapKind.Hit or RecapKind.Evaded).ToList();
            report.Check("Every Attack says the odds the hero had of Evading it, as the rules work them out", evasion > 0f && tried.Count > 0 && tried.All(entry => MathF.Abs(entry.EvadeChance - odds) < 0.01f), $"{odds:0.##}%");
            report.Check("And what was Evaded is a line too", recap.Entries.Any(entry => entry.Kind == RecapKind.Evaded), $"{tried.Count} tried");
        }

        private static void Saved(Report report, WorldCache worlds, WorldRequest request)
        {
            HeroState hero = Hero();
            hero.Level = 60;
            GameLab.Learn(hero, "key.phoenix-debt");
            using var arena = new Arena(worlds, request, hero);
            report.Check("A hero who rises again once has what does it", arena.Hero.Sheet.Has(Rule.Rebirth));
            Actor foe = arena.Dummy(East * 3f, 1000000f, More(Stat.Damage, 300f));
            StrikeDown(arena, foe, foe.Sheet.Profile(0).First);
            DeathRecap? recap = arena.Player.Client.Recap;
            report.Check("Being saved from falling is a line of it", recap != null && recap.Entries.Any(entry => entry.Kind == RecapKind.Saved && entry.Save == RecapSave.Rebirth && entry.Restored > 0f));
            if (recap == null) return;
            float lost = recap.Entries.Sum(entry => entry.ToHealth);
            float restored = recap.Entries.Sum(entry => entry.Restored) - recap.Heals.Flask;
            float left = arena.Hero.Sheet.MaxHealth + recap.Heals.Total + restored - lost;
            report.Check("And Health still adds up through it", MathF.Abs(left - recap.Killer!.Value.Health) < 0.5f, $"{left:0.##} against {recap.Killer!.Value.Health:0.##}");
        }
    }
}
