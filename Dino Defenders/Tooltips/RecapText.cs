using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Stats;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>A row of the account, in the columns it's shown in.</summary>
    public readonly record struct RecapRow(string When, string Who, Rarity? WhoRarity, string What, string Outcome, bool Good, bool Missed, DamageType Type, float Health, float Barrier, bool Fatal);

    /// <summary>
    /// Everything the account of a fall says, from what the server wrote down (DEATHS.md): what ended it, and a row and a card for each line. Nothing in here works anything out about the fight
    /// again: it only says what's in the <see cref="DeathRecap"/>, in the same words every other card uses, so any word that means something can be rested on.
    /// </summary>
    public static class RecapText
    {
        /// <summary>Below this an amount isn't worth a row on a card, as it's arithmetic rather than anything that happened.</summary>
        private const float Trace = 0.5f;

        /// <summary>Below this, nothing got through at all.</summary>
        private const float Nothing = 0.005f;

        // ---- Who and what.

        /// <summary>Who someone was, as a name to read.</summary>
        public static string Name(RecapSource? source) => source == null ? "Nobody" : source.Who switch
        {
            RecapWho.You => "You",
            RecapWho.Arena => "The arena",
            RecapWho.Unknown => "Something",
            _ => source.Name,
        };

        /// <summary>Who someone was, in the middle of a sentence.</summary>
        public static string Who(RecapSource? source) => source == null ? "nobody" : source.Who switch
        {
            RecapWho.You => "you",
            RecapWho.Arena => "the arena",
            RecapWho.Unknown => "something",
            _ => source.Name,
        };

        /// <summary>What someone was: an Enemy's rarity and Level, or whose Minion.</summary>
        public static string Kind(RecapSource source) => source.Who switch
        {
            RecapWho.Minion => $"{RarityWord(source.Rarity)} Minion of {source.Owner}, Level {source.Level}",
            RecapWho.Enemy => $"{RarityWord(source.Rarity)} Enemy{(source.Tyrant ? ", a Boss" : "")}, Level {source.Level}",
            RecapWho.Friend => $"Hero, Level {source.Level}",
            RecapWho.Arena => "What the arena does by itself",
            RecapWho.You => "Your own doing",
            _ => "",
        };

        private static string RarityWord(Rarity rarity) => rarity.ToString();

        /// <summary>What a line was, in a word or two: the Book, the Ailment, or what kind of thing it was.</summary>
        public static string What(DeathRecap recap, in RecapEntry entry)
        {
            string book = recap.BookOf(entry)?.Name ?? "";
            return entry.Kind switch
            {
                RecapKind.Hit => book.Length > 0 ? book : "A Hit",
                RecapKind.Indirect => Cause(entry.Cause),
                RecapKind.Burn => "Burning",
                RecapKind.Bleed => "Bleeding",
                RecapKind.Venom => "Venom",
                RecapKind.Debt => "Debt",
                RecapKind.Drain => "Health Drain",
                RecapKind.Cost => book.Length > 0 ? $"Cost of {book}" : "Cost",
                RecapKind.Shouldered => "Shared a Hit",
                RecapKind.Evaded or RecapKind.Blocked or RecapKind.Dodged => book.Length > 0 ? book : "A Hit",
                RecapKind.Flask => "Flask",
                RecapKind.Saved => Saved(entry.Save),
                RecapKind.GaspEnded => "Last Gasp ran out",
                _ => "",
            };
        }

        private static string Cause(HarmCause cause) => cause switch
        {
            HarmCause.Reflected => "Reflect",
            HarmCause.BlockReflected => "A Block turned back",
            HarmCause.Burst => "A dying Explosion",
            HarmCause.Explosion => "Explosion",
            HarmCause.Overflow => "What a Kill left over",
            HarmCause.Conducted => "Conduction",
            _ => "Indirect Damage",
        };

        private static string Saved(RecapSave save) => save switch
        {
            RecapSave.MinionShield => "A Minion took your place",
            RecapSave.Rebirth => "Rose again",
            RecapSave.LastGasp => "Last Gasp",
            _ => "Saved",
        };

        /// <summary>What something came to: one type as it is ("317 Cold"), and more than one as the sum first and then each type, largest first ("450 (412 Physical, 38 Fire)").</summary>
        public static string Types(in DamageTable table)
        {
            var parts = new List<(float amount, string text)>();
            float total = 0f;
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (table[i] < Nothing) continue;
                parts.Add((table[i], $"{Amount(table[i])} {DamageKinds.Name((DamageType)i)}"));
                total += table[i];
            }
            if (parts.Count <= 1) return parts.Count == 1 ? parts[0].text : "";
            return $"{Amount(total)} ({string.Join(", ", parts.OrderByDescending(p => p.amount).Select(p => p.text))})";
        }

        // ---- The headline.

        public static string Title(DeathRecap recap) => "You have fallen";

        /// <summary>What ended it, in a sentence.</summary>
        public static string Cause(DeathRecap recap)
        {
            if (recap.Killer is not RecapEntry killer)
            {
                return recap.Unaccounted >= Trace ? "Nothing this account can name took the last of your Health." : "You fell.";
            }

            RecapSource? source = recap.SourceOf(killer);
            string who = Who(source);
            string book = recap.BookOf(killer)?.Name ?? "";
            return killer.Kind switch
            {
                RecapKind.Hit when source?.Who == RecapWho.Arena => book.Length > 0 ? $"Slain by the arena's {book}." : "Slain by the arena.",
                RecapKind.Hit => book.Length > 0 ? $"Slain by {who} with {book}." : $"Slain by {who}.",
                RecapKind.Indirect => killer.Cause switch
                {
                    HarmCause.Reflected => $"Slain by your own blow, turned back by the Reflect of {who}.",
                    HarmCause.BlockReflected => $"Slain by your own blow, turned back by a Block of {who}.",
                    HarmCause.Burst => $"Slain by {who} going up as it died.",
                    HarmCause.Overflow => $"Slain by what was left over from a Kill by {who}.",
                    HarmCause.Conducted => $"Slain by Conduction from {who}.",
                    HarmCause.Explosion => $"Slain by an Explosion of {who}.",
                    _ => $"Slain by Indirect Damage from {who}.",
                },
                RecapKind.Burn => $"Burned to death, by a Burn {who} left on you.",
                RecapKind.Bleed => $"Bled out, from a Bleed {who} left on you.",
                RecapKind.Venom => $"Killed by Venom {who} left on you.",
                RecapKind.Debt => "Your Debt fell due.",
                RecapKind.Drain => "Your own Health Drain took the last of it.",
                RecapKind.Cost => book.Length > 0 ? $"You paid the last of your Health for {book}." : "You paid the last of your Health for a Book.",
                RecapKind.Shouldered => $"You fell taking part of a Hit for {who}.",
                RecapKind.GaspEnded => "Last Gasp ran out before you made a Kill.",
                _ => "You fell.",
            };
        }

        /// <summary>What the last blow was, and what there was to take it: Health, and Barrier if any was left.</summary>
        public static string Blow(DeathRecap recap)
        {
            if (recap.Killer is not RecapEntry killer) return "";
            float health = MathF.Max(killer.Health + killer.ToHealth, 0f);
            float barrier = killer.Barrier + killer.ToBarrier;
            string had = barrier >= Trace ? $"You had {Amount(health)} Health and {Amount(barrier)} Barrier." : $"You had {Amount(health)} Health.";

            if (killer.Kind == RecapKind.GaspEnded) return $"Last Gasp holds you at {Amount(killer.ToHealth)} Health until it ends.";
            if (killer.OverTime) return $"{Types(killer.Dealt)} over {Seconds(Tenths(MathF.Max(killer.Lasted, DeathRecap.SampleSeconds)))}. {had}";
            if (killer.Kind == RecapKind.Cost) return $"You paid {Amount(killer.ToHealth)} Health to use it. {had}";
            if (killer.Kind == RecapKind.Shouldered) return $"{Amount(killer.Shared)} came over to you. {had}";

            string what = killer.Critical ? "A Critical Hit" : killer.Kind == RecapKind.Indirect ? "Indirect Damage" : "A Hit";
            string executed = killer.Executed ? " Execute finished you." : "";
            string got = killer.Got >= Nothing ? $"{what} of {Types(killer.Dealt)} got through your defences." : $"{what} that your defences took all of.";
            return $"{got} {had}{executed}";
        }

        // ---- Rows.

        public static RecapRow Row(DeathRecap recap, int index)
        {
            RecapEntry entry = recap.Entries[index];
            RecapSource? source = recap.SourceOf(entry);
            string when = Seconds(Tenths(entry.Ago));
            string who = entry.Kind is RecapKind.Drain or RecapKind.Debt or RecapKind.Cost or RecapKind.Flask or RecapKind.Saved or RecapKind.GaspEnded ? "You" : Name(source);
            Rarity? rarity = source?.Who is RecapWho.Enemy or RecapWho.Minion ? source.Rarity : null;
            return new RecapRow(when, who, rarity, What(recap, entry), Outcome(entry), entry.Kind is RecapKind.Flask or RecapKind.Saved, !entry.Landed && entry.Kind is RecapKind.Evaded or RecapKind.Blocked or RecapKind.Dodged,
                entry.Mostly, MathF.Max(entry.Health, 0f), MathF.Max(entry.Barrier, 0f), index == recap.Fatal);
        }

        /// <summary>What came of a line, shortly.</summary>
        public static string Outcome(in RecapEntry entry)
        {
            switch (entry.Kind)
            {
                case RecapKind.Evaded: return "Evaded";
                case RecapKind.Blocked: return "Blocked";
                case RecapKind.Dodged: return "Dodged";
                case RecapKind.Flask: return $"+{Amount(entry.Restored)} Health";
                case RecapKind.Saved: return $"+{Amount(entry.Restored)} Health";
                case RecapKind.GaspEnded: return $"{Amount(entry.ToHealth)} Health";
                case RecapKind.Cost:
                case RecapKind.Drain:
                case RecapKind.Debt:
                    return entry.OverTime && entry.Lasted > DeathRecap.SampleSeconds ? $"{Amount(entry.ToHealth)} Health over {Seconds(Tenths(entry.Lasted))}" : $"{Amount(entry.ToHealth)} Health";
                case RecapKind.Shouldered: return $"{Amount(entry.Shared)} shared";
            }

            string types = entry.Got >= Nothing ? Types(entry.Dealt) : "Nothing got through";
            if (entry.OverTime && entry.Lasted > DeathRecap.SampleSeconds) types += $" over {Seconds(Tenths(entry.Lasted))}";
            if (entry.Critical) types += ", a Critical Hit";
            return types;
        }

        // ---- A line's card.

        /// <summary>Everything about one line: what left them, what each defence took off it, who else took some, what each pool paid, what it left, and what the hero's chances were.</summary>
        public static Card Entry(DeathRecap recap, int index)
        {
            RecapEntry entry = recap.Entries[index];
            RecapSource? source = recap.SourceOf(entry);
            RecapBook? book = recap.BookOf(entry);
            var card = new Card { Title = What(recap, entry), Kind = source != null && entry.Kind is not (RecapKind.Drain or RecapKind.Debt or RecapKind.Cost) ? $"{Name(source)}, {Kind(source)}" : "", Tags = entry.Tags & ~Tags.Hit };
            if (source?.Who is RecapWho.Enemy or RecapWho.Minion && source.Rarity != Rarity.Normal) card.Rarity = source.Rarity;
            if (book?.Card != null) card.Attached.Add(book.Card);

            card.Text(entry.Ago <= DeathRecap.SampleSeconds ? "As you fell." : $"{Seconds(Tenths(entry.Ago))} before you fell.", LineLook.Detail);
            if (book?.Card != null && entry.Kind is RecapKind.Hit or RecapKind.Evaded or RecapKind.Blocked or RecapKind.Dodged) card.Text($"{Name(source)} used {book.Name}.");

            switch (entry.Kind)
            {
                case RecapKind.Evaded:
                    card.Text($"It missed: you Evaded it, at {Rate(entry.EvadeChance)} odds.", LineLook.Good);
                    return card;
                case RecapKind.Blocked:
                    card.Text($"You Blocked it, at {Rate(entry.BlockChance)} odds. Nothing of it reached you.", LineLook.Good);
                    return card;
                case RecapKind.Dodged:
                    card.Text("You were in a Dodge, and nothing can Hit you then.", LineLook.Good);
                    return card;
                case RecapKind.Flask:
                    card.Text($"Healed {Amount(entry.Restored)} Health, leaving you at {Amount(entry.Health)}.", LineLook.Good);
                    return card;
                case RecapKind.Saved:
                    card.Text(entry.Save switch
                    {
                        RecapSave.MinionShield => $"A Minion of yours went in your place, and you stood again with {Amount(entry.Health)} Health.",
                        RecapSave.Rebirth => $"You rose again with {Amount(entry.Health)} Health.",
                        RecapSave.LastGasp => "Last Gasp held you up, until a Kill or the end of it.",
                        _ => $"Something held you up with {Amount(entry.Health)} Health.",
                    }, LineLook.Good);
                    return card;
                case RecapKind.GaspEnded:
                    card.Text($"Last Gasp ended with no Kill, and took the {Amount(entry.ToHealth)} Health it held you at.", LineLook.Bad);
                    return card;
                case RecapKind.Cost:
                    card.Text($"You paid {Amount(entry.ToHealth)} Health to use it.");
                    card.Row("Health after", Amount(entry.Health));
                    return card;
                case RecapKind.Drain:
                    card.Text($"Your Health Drain took {Amount(entry.ToHealth)} Health over {Seconds(MathF.Max(entry.Lasted, DeathRecap.SampleSeconds))}.");
                    card.Row("Health after", Amount(entry.Health));
                    return card;
                case RecapKind.Debt:
                    card.Text($"Debt you put off came due: {Amount(entry.ToHealth)} Health over {Seconds(MathF.Max(entry.Lasted, DeathRecap.SampleSeconds))}.");
                    card.Row("Health after", Amount(entry.Health));
                    return card;
                case RecapKind.Shouldered:
                    card.Text($"You took {Amount(entry.Shared)} of a Hit on {Who(source)} for them. No defence of yours touches what comes over.");
                    Pools(card, entry);
                    return card;
            }

            if (entry.OverTime) card.Text($"Damage over Time for {Seconds(MathF.Max(entry.Lasted, DeathRecap.SampleSeconds))}, left on you by {Who(source)}. Only Resistance and Damage Taken reduce it.");
            else if (entry.Kind == RecapKind.Indirect) card.Text("Indirect Damage: it can't be Evaded, Blocked or Dodged.");
            if (entry.Critical) card.Text("A Critical Hit.", LineLook.Bad);

            card.Heading("Before your defences");
            Rows(card, entry.Rolled, "");
            bool shifted = false;
            for (int i = 0; i < DamageKinds.Count; i++) shifted |= MathF.Abs(entry.Rolled[i] - entry.Defences.Arrived[i]) >= Trace;
            if (shifted)
            {
                card.Heading("Taken as");
                Rows(card, entry.Defences.Arrived, "");
            }

            card.Heading("Your defences");
            int before = card.Lines.Count;
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (MathF.Abs(entry.Defences.Resisted[i]) >= Trace) card.Row($"{DamageKinds.Name((DamageType)i)} Resistance", Off(entry.Defences.Resisted[i]));
            }
            Defence(card, "Armour", entry.Defences.Armoured.Total);
            Defence(card, "Physical Damage Reduction", entry.Defences.Reduced.Total);
            Defence(card, "Damage from Hits", entry.Defences.Flat.Total);
            Defence(card, "Damage Taken", entry.Defences.Scaled.Total);
            Defence(card, "Shocked", entry.Defences.Shocked.Total);
            if (card.Lines.Count == before) card.Text("None of yours touched it.", LineLook.Dim);
            card.Row("Got through", Amount(entry.Got));

            if (entry.Shared >= Trace) card.Row("Taken by a friend or a Minion", Off(entry.Shared));
            if (entry.Gold >= Trace) card.Row("Paid in Gold", Off(entry.Gold));
            if (entry.Capped >= Trace) card.Row("Over the most one Hit can take", Off(entry.Capped));
            if (entry.Healed >= Trace) card.Row("Turned to Health", Off(entry.Healed));
            if (entry.Stored >= Trace) card.Row("Held in the ice", Off(entry.Stored));
            Pools(card, entry);

            if (entry.Executed) card.Text("Execute finished you.", LineLook.Bad);
            if (entry.Left != 0)
            {
                var left = new List<string>();
                for (int i = 0; i < AilmentRules.Count; i++)
                {
                    if ((entry.Left & (1 << i)) != 0) left.Add(((Ailment)i).ToString());
                }
                card.Text($"It left {List(left)} on you.", LineLook.Bad);
            }
            if (entry.Kind == RecapKind.Hit && (entry.EvadeChance > 0f || entry.BlockChance > 0f))
            {
                var chances = new List<string>();
                if (entry.EvadeChance > 0f) chances.Add($"{Rate(entry.EvadeChance)} to Evade it");
                if (entry.BlockChance > 0f) chances.Add($"{Rate(entry.BlockChance)} to Block it");
                card.Text($"You had {List(chances)}, as odds.", LineLook.Detail);
            }
            else if (entry.Kind == RecapKind.Hit && (entry.Tags & Tags.Attack) != 0) card.Text("You had no way to Evade or Block it.", LineLook.Detail);
            return card;
        }

        private static void Pools(Card card, in RecapEntry entry)
        {
            card.Heading("Paid from");
            if (entry.ToBarrier >= Trace) card.Row("Barrier", Amount(entry.ToBarrier));
            if (entry.ToMana >= Trace) card.Row("Mana", Amount(entry.ToMana));
            if (entry.ToDebt >= Trace) card.Row("Debt, to pay later", Amount(entry.ToDebt));
            card.Row("Health", Amount(MathF.Max(entry.ToHealth, 0f)));
            card.Row("Health after", Amount(MathF.Max(entry.Health, 0f)));
        }

        private static void Rows(Card card, in DamageTable table, string prefix)
        {
            for (int i = 0; i < DamageKinds.Count; i++)
            {
                if (table[i] >= Trace) card.Row(prefix + DamageKinds.Name((DamageType)i), Amount(table[i]));
            }
        }

        private static void Defence(Card card, string name, float off)
        {
            if (MathF.Abs(off) >= Trace) card.Row(name, Off(off));
        }

        // Taken off is shown taken off, and added added.
        private static string Off(float off) => off >= 0f ? To + Amount(off) : "+" + Amount(-off);

        // Seconds to a tenth, which is finer than anyone can tell apart in a fight.
        private static float Tenths(float seconds) => MathF.Round(seconds * 10f) / 10f;
    }
}
