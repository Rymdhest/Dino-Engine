using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using Dino_Defenders.Tooltips;
using Dino_Engine.World;
using System.Text.RegularExpressions;
using static Dino_Defenders.Stats.M;

namespace Dino_BuildLab
{
    /// <summary>
    /// What a player is told, held to what's true. A tooltip that's vague or wrong can't be seen from the code that writes it, so all of this reads what comes out: every sentence the game can
    /// show has to be made of defined words, say everything its modifier says, and agree with what a blow really does.
    /// </summary>
    internal static class TooltipChecks
    {
        private const int LongestTerm = 170;        // Characters. A definition is a line or two, read while looking at something else.
        private const int MostLinesOnACard = 18;    // What's on show at once. A person asked for less, so past this it goes in the details.

        // Past these, what's on a skill's card only depends on what it has, so this is a hero with some of everything.
        private static readonly string[] Build = { "key.echoing-word", "pyre.searing-heat", "storm.high-voltage", "mire.toxic-blood", "marrow.leech", "knave.cut-throat" };

        public static void Run(Report report)
        {
            report.Section("Tooltips: everything has words");
            Complete(report);

            report.Section("Tooltips: the words mean one thing");
            Vocabulary(report);
            Sentences(report);

            report.Section("Tooltips: no number is typed by hand");
            HandTyped(report);

            report.Section("Tooltips: what's on a card is what happens");
            Truth(report);
            Live(report);

            report.Section("Tooltips: every line does something");
            NothingIsEmpty(report);

            report.Section("Tooltips: a hero starts with nothing");
            Naked(report);

            report.Section("Tooltips: they stay readable");
            Readable(report);
        }

        private static IEnumerable<T> Single<T>(ulong except = 0) where T : struct, Enum
        {
            foreach (T value in Enum.GetValues<T>())
            {
                ulong bits = System.Convert.ToUInt64(value);
                if (bits != 0 && (bits & (bits - 1)) == 0 && (bits & except) == 0) yield return value;
            }
        }

        private static void Complete(Report report)
        {
            var stats = Enum.GetValues<Stat>().Where(s => s != Stat.Count).ToList();
            report.Check("Every stat has a term", stats.All(s => Lexicon.Of(s) != null), string.Join(", ", stats.Where(s => Lexicon.Of(s) == null)));
            report.Check("Every stat has a line on the sheet", stats.All(s => StatBook.Rows.Any(r => r.Stats.Contains(s))), string.Join(", ", stats.Where(s => !StatBook.Rows.Any(r => r.Stats.Contains(s)))));

            var rules = Single<Rule>().ToList();
            report.Check("Every rule has words", rules.All(r => ModText.Of(r) != null), string.Join(", ", rules.Where(r => ModText.Of(r) == null)));

            var conditions = Single<Condition>().ToList();
            report.Check("Every condition has a term", conditions.All(c => Lexicon.Of(c) != null), string.Join(", ", conditions.Where(c => Lexicon.Of(c) == null)));
            var unsaid = conditions.Where(c => string.IsNullOrEmpty(new Mod { Kind = ModKind.Flat, Stat = Stat.Health, Value = 1f, Need = c }.Describe().Replace("+1 Health", ""))).ToList();
            report.Check("Every condition has a way of being said", unsaid.Count == 0, string.Join(", ", unsaid));

            var tags = Single<Tags>().ToList();
            report.Check("Every tag has a term", tags.All(t => Lexicon.Of(t) != null), string.Join(", ", tags.Where(t => Lexicon.Of(t) == null)));

            report.Check("Every ailment has a term", Enum.GetValues<Ailment>().All(a => Lexicon.Of(a) != null));
            report.Check("Every type of damage has a term", DamageKinds.All.All(t => Lexicon.Named(t.ToString()) != null));

            var kinds = new HashSet<string>();
            foreach (TriggerEvent on in Enum.GetValues<TriggerEvent>()) kinds.Add(Trigger(on, SkillCatalog.Strike).Describe());
            report.Check("Every trigger says what sets it off", kinds.Count == Enum.GetValues<TriggerEvent>().Length);

            var nameless = Enum.GetValues<Per>().Where(p => p != Per.None && !new Mod { Kind = ModKind.Flat, Stat = Stat.Health, Value = 1f, Per = p, PerStep = 2f }.Describe().Contains(" per ")).ToList();
            report.Check("Everything that can be counted says what it counts", nameless.Count == 0, string.Join(", ", nameless));

            report.Check("Every rule that has a skill or a buff to show has it", ModText.Of(Rule.Carrier)!.Casts == SkillCatalog.VenomCloud && ModText.Of(Rule.BurningTrail)!.Casts != null && ModText.Of(Rule.WaterTrail)!.Casts != null && ModText.Of(Rule.RalliesOnDeath)!.Gives != null);
            report.Note($"{Lexicon.All.Count} terms");
        }

        // A hero with some of everything, so that what only shows for some of it shows.
        private static ActorSheet Everyone()
        {
            var hero = new HeroState { Level = 30, Gold = 900 };
            hero.Equipped[WornSlot.MainHand] = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 30, WeaponDamage = 40f };
            foreach (string id in Build)
            {
                foreach (int step in TalentTree.PathTo(hero, TalentTree.Find(id)!.Index)) hero.Talents.Add(TalentTree.Nodes[step].Id);
            }
            return HeroBuild.Compile(hero).SheetFor(Condition.None);
        }

        private static IEnumerable<Card> EveryCard()
        {
            ActorSheet sheet = Everyone();
            foreach (SkillDefinition skill in SkillCatalog.All) yield return Cards.Skill(sheet.Preview(skill));
            foreach (BuffDefinition buff in BuffCatalog.All) yield return Cards.Buff(buff, sheet);
            foreach (TalentNode node in TalentTree.Nodes) yield return Cards.Talent(node, sheet);
            foreach (OmenDefinition omen in Omens.All) yield return Cards.Omen(omen, sheet);
            foreach (StatRow row in StatBook.Rows) yield return Cards.OfRow(row, sheet);

            var random = new WorldRandom(11);
            foreach (UniqueDefinition unique in UniqueCatalog.All) yield return Cards.Item(UniqueCatalog.Make(unique, 30, random), sheet, "Unique");
            for (int i = 0; i < 400; i++) yield return Cards.Item(ItemGenerator.Roll(1 + i % 60, random.Fork(i)), sheet, "Item");

            yield return Cards.Dodge(sheet);
            yield return Cards.Flask(new HeroState(), sheet);
            yield return Cards.Wind();
        }

        private static IEnumerable<(string where, string text)> EverySentence()
        {
            foreach (Card card in EveryCard())
            {
                foreach (string line in card.Words()) yield return (card.Title, line);
            }
            foreach (Term term in Lexicon.All) yield return (term.Name, term.Says);
            foreach (OmenDefinition omen in Omens.All) yield return (omen.Name, omen.Describe());
        }

        // Names of things are their own, and nothing has to define them. They're shown with a card of their own.
        private static HashSet<string> ProperNames()
        {
            var names = new HashSet<string>();
            void Add(string name)
            {
                foreach (string word in name.Split(' ')) names.Add(word);
            }

            foreach (SkillDefinition skill in SkillCatalog.All) Add(skill.Name);
            foreach (BuffDefinition buff in BuffCatalog.All) Add(buff.Name);
            foreach (OmenDefinition omen in Omens.All) Add(omen.Name);
            foreach (string reaction in new[] { "Scald", "Crackle", "Flashfire", "Shatter", "Conduction" }) Add(reaction);
            return names;
        }

        private static void Vocabulary(Report report)
        {
            HashSet<string> names = ProperNames();
            var unknown = new Dictionary<string, string>();
            var lower = new Dictionary<string, string>();
            var marks = new List<TermMark>();
            int count = 0;

            foreach ((string where, string text) in EverySentence().Distinct())
            {
                count++;
                Lexicon.Find(text, marks);
                char[] masked = text.ToCharArray();
                foreach (TermMark mark in marks) Array.Fill(masked, '#', mark.Start, mark.Length);
                string rest = new string(masked);

                foreach (Match match in Regex.Matches(rest, @"[\p{L}']+"))
                {
                    string word = match.Value.TrimEnd('\'');
                    if (word.EndsWith("'s")) word = word[..^2];
                    if (word.Length == 0) continue;

                    bool opening = rest[..match.Index].TrimEnd() is "" or { Length: > 0 } && (rest[..match.Index].TrimEnd().Length == 0 || rest[..match.Index].TrimEnd()[^1] is '.' or ':');
                    if (char.IsUpper(word[0]))
                    {
                        // The first word of a sentence is capitalised whatever it is, unless it's something else entirely.
                        if (!opening && !names.Contains(word)) unknown.TryAdd(word, $"{where}: {text}");
                        continue;
                    }

                    string capital = char.ToUpperInvariant(word[0]) + word[1..];
                    if (Lexicon.Named(capital) is { AfterPercent: false }) lower.TryAdd(word, $"{where}: {text}");
                }
            }

            report.Note($"{count} different sentences");
            report.Check("Every capitalised word in every sentence is a defined term", unknown.Count == 0, string.Join(" | ", unknown.Take(40).Select(pair => $"'{pair.Key}' in {pair.Value}")));
            report.Check("And no term is written without its capital, as then nothing explains it", lower.Count == 0, string.Join(" | ", lower.Take(40).Select(pair => $"'{pair.Key}' in {pair.Value}")));

            var marked = new List<TermMark>();
            Lexicon.Find("20% increased Attack Speed and 10% more Fire Damage over Time", marked);
            report.Check("The longest form wins", marked.Any(m => m.Term.Name == "Attack Speed") && marked.All(m => m.Term.Name != "Attack") && marked.Any(m => m.Term.Name == "Damage over Time"));
            report.Check("And a number's words are only terms beside a number", marked.Any(m => m.Term.Name == "increased") && marked.Any(m => m.Term.Name == "more"));

            Lexicon.Find("nothing more to it", marked);
            report.Check("More in an ordinary sentence is just a word", marked.Count == 0);
        }

        private static IEnumerable<Mod> EveryMod()
        {
            foreach (TalentNode node in TalentTree.Nodes) foreach (Mod mod in node.Mods) yield return mod;
            foreach (Affix affix in AffixCatalog.All) foreach (Mod mod in affix.Build(affix.Tiers[^1].High)) yield return mod;
            foreach (UniqueDefinition unique in UniqueCatalog.All) foreach (Mod mod in unique.Mods) yield return mod;
            foreach (OmenDefinition omen in Omens.All) foreach (Mod mod in omen.Mods) yield return mod;
            foreach (SkillDefinition skill in SkillCatalog.All) foreach (Mod mod in skill.Mods) yield return mod;
            foreach (BuffDefinition buff in BuffCatalog.All) foreach (Mod mod in buff.Mods) yield return mod;
            foreach (EnemyTrait trait in Bestiary.Traits) foreach (Mod mod in trait.Mods) yield return mod;
            foreach (WeaponKind kind in Enum.GetValues<WeaponKind>()) foreach (Mod mod in WeaponKinds.Of(kind).Mods) yield return mod;
        }

        // Each part of a modifier has to be in its sentence, and each kind has to use its own words and no other's.
        private static void Sentences(Report report)
        {
            var wrongWord = new List<string>();
            var lostScope = new List<string>();
            var lostCondition = new List<string>();
            var lostStep = new List<string>();
            var marks = new List<TermMark>();
            int count = 0;

            foreach (Mod mod in EveryMod().Distinct())
            {
                count++;
                string text = mod.Describe();
                Lexicon.Find(text, marks);
                bool Says(Term? term) => term != null && marks.Any(m => m.Term == term);

                bool scaled = Regex.IsMatch(text, @"\d% (increased|reduced)\b");
                bool multiplied = Regex.IsMatch(text, @"\d% (more|less)\b");
                bool right = mod.Kind switch
                {
                    ModKind.Increased => scaled && !multiplied && text.Contains(mod.Value >= 0f ? "% increased" : "% reduced"),
                    ModKind.More => multiplied && !scaled && text.Contains(mod.Value >= 0f ? "% more" : "% less"),
                    ModKind.Override => !scaled && !multiplied,
                    ModKind.Flat when mod.Stat is not (Stat.Convert or Stat.GainAs or Stat.TakenAs) => !scaled && !multiplied,
                    _ => true,
                };
                if (!right) wrongWord.Add($"{mod.Kind} {mod.Value}: {text}");

                if (mod.Kind <= ModKind.Override)
                {
                    foreach (Tags tag in Single<Tags>())
                    {
                        if ((mod.Scope & tag) == 0) continue;
                        if (tag == Tags.Elemental && (mod.Scope & (Tags.Fire | Tags.Cold | Tags.Lightning)) != 0) continue;
                        if (!Says(Lexicon.Of(tag))) lostScope.Add($"{tag}: {text}");
                    }

                    foreach (Condition condition in Single<Condition>())
                    {
                        if (((mod.Need | mod.Without | mod.Foe) & condition) != 0 && !Says(Lexicon.Of(condition))) lostCondition.Add($"{condition}: {text}");
                    }

                    if (mod.Per != Per.None && !text.Contains(" per ")) lostStep.Add(text);
                    if (mod.PerCap > 0f && !text.Contains("up to")) lostStep.Add(text);
                }
            }

            report.Note($"{count} different modifiers");
            report.Check("Flat is signed, increased and reduced share a sum, more and less multiply, and none says what it isn't", wrongWord.Count == 0, string.Join(" | ", wrongWord.Take(8)));
            report.Check("Every sentence names all it's narrowed to", lostScope.Count == 0, string.Join(" | ", lostScope.Take(8)));
            report.Check("Every sentence says when it counts", lostCondition.Count == 0, string.Join(" | ", lostCondition.Take(8)));
            report.Check("Every sentence says what it's counted per, and where that stops", lostStep.Count == 0, string.Join(" | ", lostStep.Take(8)));
        }

        // What's written between the quotes on a line of code, leaving out anything between the braces of an interpolated string, which is code itself.
        private static IEnumerable<string> Literals(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '"') continue;

                bool interpolated = i > 0 && line[i - 1] == '$';
                var text = new System.Text.StringBuilder();
                int depth = 0;
                int at = i + 1;
                for (; at < line.Length; at++)
                {
                    char c = line[at];
                    if (depth > 0)
                    {
                        if (c == '{') depth++;
                        else if (c == '}') depth--;
                        else if (c == '"')
                        {
                            at++;
                            while (at < line.Length && line[at] != '"') at++;
                        }
                        continue;
                    }

                    if (c == '\\')
                    {
                        text.Append(c).Append(line[Math.Min(++at, line.Length - 1)]);
                        continue;
                    }
                    if (c == '"') break;
                    if (c == '{' && interpolated) depth = 1;
                    else text.Append(c);
                }
                yield return text.ToString();
                i = at;
            }
        }

        private static string? Sources()
        {
            string? at = Directory.GetCurrentDirectory();
            while (at != null && !Directory.Exists(Path.Combine(at, "Dino Defenders", "Tooltips"))) at = Path.GetDirectoryName(at);
            return at == null ? null : Path.Combine(at, "Dino Defenders");
        }

        // A digit in a string is a number someone typed, which nothing will change when the rule does. What's between braces is read from where the rule reads it.
        private static void HandTyped(Report report)
        {
            if (Sources() is not string sources)
            {
                report.Note("Sources not found, so this can only run from inside the repository");
                return;
            }

            var typed = new List<string>();
            foreach (string path in Directory.GetFiles(Path.Combine(sources, "Tooltips")).Append(Path.Combine(sources, "Progression", "Offering.cs")))
            {
                foreach (string line in File.ReadLines(path))
                {
                    string code = line.Trim();
                    if (code.StartsWith("//") || code.StartsWith("///") || code.Contains("Format") || code.Contains("value.ToString")) continue;

                    foreach (string text in Literals(line))
                    {
                        if (Regex.IsMatch(Regex.Replace(text, @"\\u[0-9A-Fa-f]{4}", ""), @"\d")) typed.Add($"{Path.GetFileName(path)}: {text}");
                    }
                }
            }
            report.Check("No sentence has a digit typed into it", typed.Count == 0, string.Join(" | ", typed.Take(8)));

            // Every number in the rules goes through a name, so that there's something for a tooltip to read. These are what's left in the code that decides a fight, and they aren't about a fight's numbers:
            // nothing, one, a hundred for a percentage, and what's only for show. A new one means a new rule with a number nobody will be told.
            var allowed = new HashSet<string> { "0", "1", "2", "3", "4", "6", "100", "0.5", "0.01", "0.05", "1e-3", "1e-4", "1e-5", "4", "0.25", "1.5", "3.2", "3.4", "5", "0.6", "0.8", "1.6", "11", "0.3", "0.12", "0.1", "1.25", "1.15", "0.56", "30", "34", "2.5", "1000", "1e-4f" };
            var stray = new List<string>();
            foreach (string name in new[] { "Server/Combat.cs", "Server/Actor.cs", "Server/ActorSystems.cs", "Stats/Hits.cs", "Stats/SkillProfile.cs", "Stats/ActorSheet.cs" })
            {
                foreach (string line in File.ReadLines(Path.Combine(sources, name)))
                {
                    string code = line.Split("//")[0];
                    if (code.Contains(" const ")) continue;
                    foreach (Match number in Regex.Matches(code, @"(?<![\w.])\d+(\.\d+)?(e-?\d+)?f\b"))
                    {
                        string value = number.Value.TrimEnd('f');
                        if (!allowed.Contains(value)) stray.Add($"{name}: {value} in {code.Trim()}");
                    }
                }
            }
            report.Check("No number in the rules is outside the rulebook", stray.Count == 0, string.Join(" | ", stray.Take(8)));

            var mechanical = new[] { "armour", "damage", "chance", "mana", "health", "cooldown", "stun", "ignores", "stuns", "bleeding", "second" };
            var claims = SkillCatalog.All.Where(s => Regex.IsMatch(s.Text, @"\d") || mechanical.Any(word => Regex.IsMatch(s.Text, $@"\b{word}\b", RegexOptions.IgnoreCase))).Select(s => $"{s.Name}: {s.Text}").ToList();
            report.Check("A skill's flavour makes no claim about what it does: that's what the card is for", claims.Count == 0, string.Join(" | ", claims.Take(6)));
        }

        private static Card CardOf(ActorSheet sheet, SkillDefinition skill) => Cards.Skill(sheet.Preview(skill));

        private static string? Value(Card card, string name) => card.Lines.FirstOrDefault(l => l.Text == name && l.Look is LineLook.Row or LineLook.Cell).Value is { Length: > 0 } value ? value : card.Details != null ? Value(card.Details, name) : null;

        private static bool Mentions(Card card, string word) => card.Lines.Any(l => l.Look != LineLook.Lore && l.Text.Contains(word)) || card.Details != null && Mentions(card.Details, word);

        private static Loadout Kit(params Mod[] mods)
        {
            var all = new List<Mod> { Flat(Stat.Health, 100f) };
            all.AddRange(mods);
            return new Loadout(10, new WeaponProfile(20f, 30f, WeaponProfile.BareTime, 5f, Tags.Sword | Tags.OneHanded), all, SkillCatalog.All);
        }

        // Whatever a card says, a blow is asked the same thing and has to agree.
        private static void Truth(Report report)
        {
            ActorSheet plain = Kit(Chance(Tags.Hex, 6f), Chance(Tags.Burn, 6f)).SheetFor(Condition.None);

            Card pool = CardOf(plain, SkillCatalog.Tidepool);
            report.Check("Tidepool deals nothing, so it can't Hex, and doesn't claim to", !Mentions(pool, "Hex") && !Mentions(pool, "Chance"));
            report.Check("It says how long, how far, and what it does", Value(pool, "Duration") == "10 s" && Value(pool, "Radius") == "8 m" && Mentions(pool, "Counts as Water"));
            report.Check("A Burn needs Fire, so Frost Shards can't, whatever the Chance", !Mentions(CardOf(plain, SkillCatalog.FrostShards), "Burn"));
            report.Check("But Firebolt can", Mentions(CardOf(plain, SkillCatalog.Firebolt), "Chance to Burn"));
            report.Check("And Magic can Hex", Mentions(CardOf(plain, SkillCatalog.ArcaneDart), "Chance to Hex"));

            Card bash = CardOf(Kit(Flat(Stat.Armour, 500f)).SheetFor(Condition.None), SkillCatalog.ShieldBash);
            report.Check("Shield Bash says how armour scales it", Mentions(bash, "2% increased Damage per 50 Armour, up to 200%"));
            report.Check("And what that comes to now", Mentions(bash, "(now 20%)"));
            report.Check("And what it's made of", Mentions(bash, "90% of Weapon Damage"));
            report.Check("And that it's all shown", Value(bash, "Cooldown") == "2 s" && Value(bash, "Arc") != null && Value(bash, "Reach") != null && Value(bash, "Critical Chance") == "5%");

            Card carrier = Cards.Talent(TalentTree.Find("key.carrier")!, plain);
            report.Check("Carrier says how near is nearby, on the card itself", Mentions(carrier, $"within {Say.Metres(SkillCatalog.VenomCloud.Radius)}"));
            report.Check("And its name opens the Skill, with its own numbers", carrier.Attached.Any(c => c.Title == "Contagion" && Value(c, "Radius") == "8 m"));
            report.Check("A talent that teaches a Skill is one card, not two", Cards.Talent(TalentTree.Find("host.bone-spear")!, plain) is { Title: "Bone Spear", Attached.Count: 0 });
            report.Check("And nothing taught is called Triggered", !Cards.TagNames(Cards.TagsOf(TalentTree.Find("host.bone-spear")!.Mods)).Contains("Trigger"));
            report.Check("What a whole build gets isn't on a Skill's card", !Cards.Skill(Kit(Flat(Stat.HealthOnKill, 9f)).SheetFor(Condition.None).Preview(SkillCatalog.BoneSpear)).Lines.Any(l => l.Text.Contains("on Kill")));
            report.Check("But it's in the details", Mentions(Cards.Skill(Kit(Flat(Stat.HealthOnKill, 9f)).SheetFor(Condition.None).Preview(SkillCatalog.BoneSpear)), "on Kill"));
            report.Check("Second Opinion's Spell is a tag anyone can check", Cards.TagNames(SkillCatalog.Firebolt.Tags).Contains("Spell") && Lexicon.Named("Spell")!.Says.Contains("Cast Speed"));
            report.Check("What Leech is, is said: instantly", Lexicon.Named("Leech")!.Says.Contains("Instantly"));

            ActorSheet sheet = Kit(Inc(Stat.Damage, 40f), More(Stat.Damage, 25f, Tags.Spell), Added(DamageType.Fire, 6f), Flat(Stat.CritChance, 0f)).SheetFor(Condition.None);
            var luck = new WorldRandom(7);
            var wrong = new List<string>();
            foreach (SkillDefinition skill in SkillCatalog.All.Where(s => s.DealsDamage))
            {
                SkillProfile profile = sheet.Preview(skill);
                Card card = Cards.Skill(profile);
                foreach (DamageType type in DamageKinds.All)
                {
                    string? shown = Value(card, $"{type} Damage");
                    if (shown == null) continue;

                    string[] ends = shown.Split(Say.To);
                    float low = float.Parse(ends[0], System.Globalization.CultureInfo.InvariantCulture);
                    float high = float.Parse(ends[^1], System.Globalization.CultureInfo.InvariantCulture);
                    float least = float.PositiveInfinity;
                    float most = 0f;
                    for (int i = 0; i < 800; i++)
                    {
                        HitPacket hit = default;
                        Hits.Roll(profile, Condition.None, luck, ref hit);
                        if (hit.Critical) continue;
                        least = MathF.Min(least, hit.Damage[(int)type]);
                        most = MathF.Max(most, hit.Damage[(int)type]);
                    }
                    if (least < low * 0.99f - 0.51f || most > high * 1.01f + 0.51f || most < low - 0.51f) wrong.Add($"{skill.Name}: card {shown}, blows {least}-{most}");
                }
            }
            report.Check("Every skill's Damage on its card is the range its blows really roll", wrong.Count == 0, string.Join(" | ", wrong.Take(5)));

            // Each number on the sheet is one a fight reads.
            var hero = new HeroState { Level = 12 };
            ActorSheet own = HeroBuild.Compile(hero).SheetFor(Condition.None);
            StatRow Row(string name) => StatBook.Rows.First(r => r.Name == name);
            report.Check("Armour on the sheet is the armour Hits read", Row("Armour").Reads(own) == Say.Whole(own.Armour));
            report.Check("A resistance on the sheet is the one for its type", Row("Fire Resistance").Reads(own) == Say.Percent(own.Resistance[(int)DamageType.Fire]));
            report.Check("Every line on the sheet reads as a number", StatBook.Rows.All(r => r.Reads(own).Any(char.IsDigit) && !r.Reads(own).Contains("NaN")), string.Join(", ", StatBook.Rows.Where(r => !r.Reads(own).Any(char.IsDigit)).Select(r => r.Name)));

            Card armour = Cards.OfRow(Row("Armour"), HeroBuild.Compile(new HeroState { Talents = { } }).SheetFor(Condition.None));
            report.Check("Armour's card says exactly what armour does", Mentions(armour, $"Armour / (Armour + {Hits.ArmourFactor:0} x Damage)"));

            var worn = new HeroState { Level = 5 };
            worn.Equipped[WornSlot.Body] = new Item { Name = "Coat", Slot = ItemSlot.Body, Level = 5, Armour = 90f };
            ActorSheet dressed = HeroBuild.Compile(worn).SheetFor(Condition.None);
            report.Check("And says what it's made of", Value(Cards.OfRow(Row("Armour"), dressed), "Flat") == "+90");

            var sword = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 5, Kind = WeaponKind.Sword, WeaponDamage = 10f };
            report.Check("What a weapon has for being what it is isn't hidden", Mentions(Cards.Item(sword, null, "Sword"), "+20 Accuracy"));
            report.Check("And its Critical Chance isn't", Value(Cards.Item(sword, null, "Sword"), "Critical Chance") == "5%");
        }

        // A card is made from whatever a hero is now, so there's nothing to go stale. This is that, end to end, from a talent to what it says.
        private static void Live(Report report)
        {
            var hero = new HeroState { Level = 20 };
            foreach (int step in TalentTree.PathTo(hero, TalentTree.Find("pyre.firebolt")!.Index)) hero.Talents.Add(TalentTree.Nodes[step].Id);

            string? Damage() => Value(CardOf(HeroBuild.Compile(hero).SheetFor(Condition.None), SkillCatalog.Firebolt), "Fire Damage");
            string? before = Damage();
            foreach (int step in TalentTree.PathTo(hero, TalentTree.Find("arcanum.spellweaver")!.Index)) hero.Talents.Add(TalentTree.Nodes[step].Id);
            string? after = Damage();
            report.Check("Taking a talent that adds Spell Damage changes the Spell's card", before != null && after != null && before != after, $"{before} and {after}");

            Loadout conditional = Kit(Inc(Stat.Damage, 100f).While(Condition.Moving));
            string? still = Value(CardOf(conditional.SheetFor(Condition.Stationary), SkillCatalog.Firebolt), "Fire Damage");
            string? moving = Value(CardOf(conditional.SheetFor(Condition.Moving), SkillCatalog.Firebolt), "Fire Damage");
            report.Check("What only counts sometimes shows when it does", still != moving, $"{still} and {moving}");

            var gear = new HeroState { Level = 20 };
            string? bare = Value(CardOf(HeroBuild.Compile(gear).SheetFor(Condition.None), SkillCatalog.Strike), "Physical Damage");
            gear.Equipped[WornSlot.MainHand] = new Item { Name = "Sword", Slot = ItemSlot.MainHand, Level = 20, WeaponDamage = 60f };
            string? armed = Value(CardOf(HeroBuild.Compile(gear).SheetFor(Condition.None), SkillCatalog.Strike), "Physical Damage");
            report.Check("Putting on a weapon changes an Attack's card", bare != armed, $"{bare} and {armed}");

            float cooldown = Kit().SheetFor(Condition.None).Preview(SkillCatalog.Emberfall).Cooldown;
            float quicker = Kit(Inc(Stat.CooldownRecovery, 100f)).SheetFor(Condition.None).Preview(SkillCatalog.Emberfall).Cooldown;
            report.Near("Cooldown Recovery says what it does: 100% increased is half the Cooldown", quicker, cooldown / 2f);

            float own = Kit().SheetFor(Condition.None).Preview(SkillCatalog.ArcaneDart).SkillLevel;
            report.Near("And so do Levels of a type of Skill", Kit(Flat(Stat.SkillLevel, 1f, Tags.Magic)).SheetFor(Condition.None).Preview(SkillCatalog.ArcaneDart).SkillLevel, own + 1f);
            report.Near("For that type alone", Kit(Flat(Stat.SkillLevel, 1f, Tags.Magic)).SheetFor(Condition.None).Preview(SkillCatalog.Firebolt).SkillLevel, own);

            ActorSheet preview = Kit().SheetFor(Condition.None);
            report.Check("A skill not yet known is still shown with real numbers", Value(Cards.Talent(TalentTree.Find("pyre.firebolt")!, preview), "Fire Damage") != null);
        }

        // Everything a sheet and a skill's numbers come to, as one string, so that two can be told apart however they differ.
        private static string Fingerprint(ActorSheet sheet)
        {
            var text = new System.Text.StringBuilder();
            void Read(object owner)
            {
                foreach (var field in owner.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    object? value = field.GetValue(owner);
                    if (value is float or int or bool) text.Append(value).Append(' ');
                    else if (value is DamageTable table) for (int i = 0; i < DamageKinds.Count; i++) text.Append(table[i]).Append(' ');
                    else if (value is float[] array) foreach (float one in array) text.Append(one).Append(' ');
                    else if (value is ScopedDefence[] defences) foreach (ScopedDefence one in defences) text.Append(one).Append(' ');
                    else if (value is AilmentProfile[] ailments) foreach (AilmentProfile a in ailments) text.Append(a.Chance).Append(' ').Append(a.Duration).Append(' ').Append(a.Effect).Append(' ');
                }
            }

            Read(sheet);
            for (int i = 0; i < sheet.Loadout.Skills.Length; i++)
            {
                SkillProfile profile = sheet.Profile(i);
                Read(profile);
                foreach (ref readonly DamageFlow flow in profile.Flows) text.Append(flow.Type).Append(' ').Append(flow.Min).Append(' ').Append(flow.Max).Append(' ').Append(flow.Increased).Append(' ').Append(flow.More).Append(' ');
            }
            return text.ToString();
        }

        // Whoever comes when called is part of what a build is too.
        private static string Everything(Loadout loadout)
        {
            ActorSheet sheet = loadout.SheetFor(Condition.None);
            return Fingerprint(sheet) + Fingerprint(Bestiary.MinionOf(loadout, sheet, Bestiary.Minion("bones")!).SheetFor(Condition.None));
        }

        // A hero with no flat amount of anything but what heroes really have, so that something that only works on what isn't there shows up as doing nothing.
        private static Loadout Plain(IEnumerable<Mod> extra)
        {
            var hero = new HeroState { Level = 20 };
            var mods = new List<Mod>(HeroBuild.Innate()) { Flat(Stat.Barrier, 100f) };
            mods.AddRange(extra);
            return new Loadout(hero.Level, new WeaponProfile(20f, 30f, WeaponProfile.BareTime, 5f, Tags.Sword | Tags.OneHanded | Tags.Unarmed | Tags.Axe | Tags.Mace | Tags.Dagger | Tags.Spear | Tags.Bow | Tags.Staff | Tags.Wand | Tags.TwoHanded), mods, SkillCatalog.All);
        }

        // What it takes for a line to be read at all: whatever it asks for, as if it held. What only counts against a foe is read by a blow, and StatChecks has those.
        private static Mod AsIfHeld(in Mod mod) => mod with { Need = Condition.None, Without = Condition.None, Per = Per.None, PerStep = 0f, PerCap = 0f };

        // A sentence on a tooltip is a promise that something changes. This takes each one a player can read and looks for the change, on a hero who only has what any hero has.
        // Those that need a flat amount from elsewhere to work on are given some, if the game has any to give: otherwise nobody could ever see them do anything.
        private static void NothingIsEmpty(Report report)
        {
            var mods = EveryMod().Where(mod => mod.Kind <= ModKind.More && mod.Foe == Condition.None && mod.Skill == SkillDefinition.None).Select(mod => AsIfHeld(mod)).Distinct().ToList();
            string plain = Everything(Plain(Array.Empty<Mod>()));
            var flats = EveryMod().Where(mod => mod.Kind == ModKind.Flat).Select(mod => AsIfHeld(mod)).ToList();

            var empty = new List<string>();
            foreach (Mod mod in mods)
            {
                if (Everything(Plain(new[] { mod })) != plain) continue;

                // It might be scaling something that isn't there yet, which is fine as long as something gives it. A limit only shows on what's reached it.
                Mod? flat = mod.Stat == Stat.MaxBlock ? Flat(Stat.BlockChance, 200f) : flats.Where(other => other.Stat == mod.Stat).Select(other => (Mod?)other).FirstOrDefault();
                if (flat != null && (mod.Kind != ModKind.Flat || mod.Stat == Stat.MaxBlock))
                {
                    if (Everything(Plain(new[] { flat.Value, mod })) != Everything(Plain(new[] { flat.Value }))) continue;
                }
                empty.Add(mod.Describe());
            }
            report.Note($"{mods.Count} different lines looked at");
            report.Check("Every line on a tooltip changes a number", empty.Count == 0, string.Join(" | ", empty.Distinct().Take(24)));
        }

        // Nothing on, no talents, at any level: Life and Mana are all there is. What can't be nothing, such as walking, is a rule, and the sheet shows what's on top of it.
        private static void Naked(Report report)
        {
            foreach (int level in new[] { 1, 30, 60 })
            {
                ActorSheet sheet = HeroBuild.Compile(new HeroState { Level = level }).SheetFor(Condition.None);
                var others = StatBook.Rows.Where(row => row.Name is not ("Health" or "Mana")).Where(row => Regex.IsMatch(row.Reads(sheet), "[1-9]")).Select(row => $"{row.Name} {row.Reads(sheet)}").ToList();
                report.Check($"Naked at level {level}: {Say.Whole(Rulebook.BaseHealth)} Health, {Say.Whole(Rulebook.BaseMana)} Mana and nothing else", sheet.MaxHealth == Rulebook.BaseHealth && sheet.MaxMana == Rulebook.BaseMana && others.Count == 0, string.Join(", ", others));
            }

            ActorSheet strong = Kit(Flat(Stat.Strength, 100f)).SheetFor(Condition.None);
            ActorSheet plain = Kit().SheetFor(Condition.None);
            report.Near("Strength gives Health", strong.MaxHealth - plain.MaxHealth, 100f * Rulebook.StrengthHealth);
            report.Near("And Physical Damage", strong.Preview(SkillCatalog.Strike).Flows[0].Increased, 100f * Rulebook.StrengthPhysical);
            ActorSheet quick = Kit(Flat(Stat.Dexterity, 100f)).SheetFor(Condition.None);
            report.Near("Dexterity gives Accuracy and Evasion", quick.Evasion, 100f * Rulebook.DexterityEvasion);
            report.Check("And Attack Speed", quick.Preview(SkillCatalog.Strike).UseTime < plain.Preview(SkillCatalog.Strike).UseTime);
            ActorSheet clever = Kit(Flat(Stat.Intellect, 100f)).SheetFor(Condition.None);
            report.Near("Intellect gives Mana", clever.MaxMana, 100f * Rulebook.IntellectMana);
            report.Near("And Spell Damage", clever.Preview(SkillCatalog.Firebolt).Flows[0].Increased, 100f * Rulebook.IntellectSpellDamage);
            report.Check("Which does nothing for an Attack", clever.Preview(SkillCatalog.Strike).Flows[0].Increased == 0f);

            report.Check("Attributes come from the tree", TalentTree.Nodes.Where(n => n.Sector >= 0 && n.Kind != TalentKind.Gate).All(n => n.Mods.Any(m => Attributes.Is(m.Stat))));
            int[] given = TalentContent.Sectors.Select(s => (int)TalentContent.AttributesOf(s.Id).Sum(m => m.Value)).ToArray();
            report.Check("And each sector gives the same amount", given.All(g => g == Rulebook.NodeAttribute), string.Join(",", given));

            // The sum on a card has to come to what the sheet says.
            var armoured = new HeroState { Level = 5 };
            armoured.Equipped[WornSlot.Body] = new Item { Name = "Coat", Slot = ItemSlot.Body, Level = 5, Armour = 100f };
            ActorSheet worn = new Loadout(5, WeaponProfile.Fists, HeroBuild.Innate().Concat(armoured.Equipped[WornSlot.Body].Mods()).Concat(new[] { Inc(Stat.Armour, 20f), Inc(Stat.Armour, 10f), Inc(Stat.Armour, -5f), More(Stat.Armour, 50f), More(Stat.Armour, -10f) }), new[] { SkillCatalog.Strike }).SheetFor(Condition.None);
            Card card = Cards.OfRow(StatBook.Rows.First(r => r.Name == "Armour"), worn);
            report.Check("A stat's card adds up all the flat", Value(card, "Flat") == "+100", Value(card, "Flat") ?? "");
            report.Check("And all the increased and reduced into one", Value(card, "Increased") == "25%", Value(card, "Increased") ?? "");
            report.Check("And lists each multiplier on its own", Value(card, "50% more Armour") == "x1.5" && Value(card, "10% less Armour") == "x0.9");
            report.Check("And the sum comes to the total", Mentions(card, $"= {Say.Amount(worn.Armour)}") && Value(card, "Total") == Say.Whole(worn.Armour));
            Card health = Cards.OfRow(StatBook.Rows.First(r => r.Name == "Health"), Kit(Flat(Stat.Strength, 10f)).SheetFor(Condition.None));
            report.Check("Base Life is shown as base, and what comes from Strength is shown as scaling", Mentions(health, "per Strength"));
        }

        private static void Readable(Report report)
        {
            int mostLines = 0;
            string longest = "";
            foreach (Card card in EveryCard())
            {
                int lines = card.Lines.Count(line => line.Look != LineLook.Gap);
                if (lines <= mostLines) continue;
                mostLines = lines;
                longest = card.Title;
            }
            report.Note($"The most lines on any card is {mostLines}, on {longest}");
            report.Check("No skill, item or talent is a wall of words", mostLines <= MostLinesOnACard, $"{mostLines} on {longest}");

            var windy = Lexicon.All.Where(t => t.Says.Length > LongestTerm).Select(t => $"{t.Name} ({t.Says.Length})").ToList();
            report.Check("Every definition is short", windy.Count == 0, string.Join(", ", windy));
            report.Check("And every term says something", Lexicon.All.All(t => t.Says.Length > 3));
            report.Check("Nothing reads badly", EverySentence().All(s => !s.text.Contains("  ") && !s.text.Contains("NaN") && !s.text.Contains("Infinity")), string.Join(" | ", EverySentence().Where(s => s.text.Contains("  ") || s.text.Contains("NaN")).Take(4).Select(s => s.text)));
        }
    }
}
