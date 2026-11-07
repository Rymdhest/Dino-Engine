using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using static Dino_Defenders.Stats.Rulebook;
using static Dino_Defenders.Tooltips.Say;

namespace Dino_Defenders.Tooltips
{
    /// <summary>What a line on a card is, which is all that decides how it's drawn.</summary>
    public enum LineLook : byte
    {
        Row,        // A name and a number, the width of the card.
        Cell,       // The same, side by side with the next.
        Text,       // A sentence.
        Rolled,     // A sentence, in the colour of what it's on.
        Dim,        // A sentence that doesn't count at the moment.
        Detail,     // How something was arrived at.
        Lore,
        Heading,
        Gap,
        Price,
        Good,
        Bad,
    }

    public readonly record struct CardLine(LineLook Look, string Text, string Value = "");

    /// <summary>
    /// Everything a tooltip says, with no idea how it's drawn. Anything that describes something builds one of these from finished numbers, and anything that shows one draws it the same way, so a
    /// card can be read by a test as well as by a person. It's built again each time it's shown, which is what makes every tooltip live. What's on it is what a player wants at a glance: the rest
    /// is one hover away, in <see cref="Details"/> and <see cref="Attached"/>.
    /// </summary>
    public sealed class Card
    {
        public string Title = "";
        public string Kind = "";
        public Tags Tags;

        /// <summary>What colours its title and what's rolled on it, if it's something found.</summary>
        public LootGrade? Grade;

        /// <summary>Picture of this, if it has one.</summary>
        public Item? Picture;

        public bool Gilded;

        public readonly List<CardLine> Lines = new();

        /// <summary>What it sets off or hands out. Resting on its name in a sentence shows it.</summary>
        public readonly List<Card> Attached = new();

        /// <summary>Everything else there is to know, for those who ask.</summary>
        public Card? Details;

        public void Row(string name, string value) => Lines.Add(new CardLine(LineLook.Row, name, value));
        public void Cell(string name, string value) => Lines.Add(new CardLine(LineLook.Cell, name, value));
        public void Text(string text, LineLook look = LineLook.Text) => Lines.Add(new CardLine(look, text));
        public void Detail(string text) => Lines.Add(new CardLine(LineLook.Detail, text));
        public void Lore(string text) => Lines.Add(new CardLine(LineLook.Lore, text));

        public void Gap()
        {
            if (Lines.Count > 0 && Lines[^1].Look != LineLook.Gap) Lines.Add(new CardLine(LineLook.Gap, ""));
        }

        public void Heading(string text)
        {
            Gap();
            Lines.Add(new CardLine(LineLook.Heading, text));
        }

        /// <summary>Every word on it and behind it that can mean something, lore aside.</summary>
        public IEnumerable<string> Words()
        {
            foreach (string name in Cards.TagNames(Tags)) yield return name;
            foreach (CardLine line in Lines)
            {
                if (line.Look is not (LineLook.Lore or LineLook.Gap)) yield return line.Text;
            }

            foreach (Card? card in Attached.Append(Details))
            {
                if (card == null) continue;
                foreach (string word in card.Words()) yield return word;
            }
        }
    }

    /// <summary>
    /// How everything is described, from finished numbers. A book's card comes from its <see cref="BookProfile"/> and a stat's from an <see cref="ActorSheet"/>, whose numbers are the ones a blow reads,
    /// so what's on a card is what happens. Nothing in here decides how the game works: anything a card can't read off something else is a sign that the number is in the wrong place.
    /// </summary>
    public static class Cards
    {
        // What each type of damage can set off on someone.
        private static readonly (Tags type, string reaction)[] Reacting =
        {
            (Tags.Physical, "Shatter"), (Tags.Fire, "Scald"), (Tags.Fire, "Flashfire"), (Tags.Cold, "Crackle"), (Tags.Lightning, "Conduction"),
        };

        private static readonly Tags[] TagOrder =
        {
            Tags.Attack, Tags.Spell, Tags.Melee, Tags.Projectile, Tags.Area, Tags.Zone, Tags.Chaining, Tags.Movement, Tags.Buff, Tags.Minion, Tags.Triggered, Tags.Reaction,
            Tags.Opening, Tags.Turned, Tags.Closing, Tags.Wild, Tags.Falling,
            Tags.Physical, Tags.Fire, Tags.Cold, Tags.Lightning, Tags.Magic, Tags.Poison, Tags.Elemental, Tags.DamageOverTime,
            Tags.Burn, Tags.Bleed, Tags.Venom, Tags.Chill, Tags.Freeze, Tags.Shock, Tags.Hex, Tags.Stun,
            Tags.Sword, Tags.Axe, Tags.Mace, Tags.Dagger, Tags.Spear, Tags.Bow, Tags.Staff, Tags.Wand, Tags.Unarmed, Tags.OneHanded, Tags.TwoHanded,
        };

        /// <summary>What's shown of some tags, always in the same order. A hit is what everything is, so it says nothing here.</summary>
        public static IEnumerable<string> TagNames(Tags tags)
        {
            foreach (Tags tag in TagOrder)
            {
                if ((tags & tag) != 0 && Lexicon.Of(tag) is Term term) yield return term.Name;
            }
        }

        /// <summary>What some modifiers are about, for showing as tags.</summary>
        public static Tags TagsOf(IEnumerable<Mod> mods)
        {
            Tags tags = Tags.None;
            foreach (Mod mod in mods)
            {
                tags |= mod.Scope;
                if (mod.Kind == ModKind.Rule)
                {
                    foreach (RuleInfo info in ModText.Rules)
                    {
                        if ((mod.Rule & info.Rule) != 0) tags |= info.Touches;
                    }
                }
                if (mod.Kind <= ModKind.Override && mod.Stat is Stat.Convert or Stat.GainAs or Stat.TakenAs) tags |= DamageKinds.Tag(mod.From) | DamageKinds.Tag(mod.To);
                foreach (DamageType type in DamageKinds.All)
                {
                    if (mod.Kind <= ModKind.Override && mod.Stat == M.AddedStat(type)) tags |= DamageKinds.TagsOf(type);
                }
                if (mod.Kind == ModKind.Trigger) tags |= Tags.Triggered;
            }
            return tags & ~Tags.Hit;
        }

        /// <summary>A line for a modifier, with what it comes to now if that depends on anything.</summary>
        private static string Line(in Mod mod, ActorSheet? viewer)
        {
            string text = mod.Describe();
            if (mod.Per == Per.None || viewer == null) return text;
            if (viewer.WorthIfHeld(mod with { Need = Condition.None, Without = Condition.None }) is not float now) return text;
            string unit = mod.Kind != ModKind.Flat || StatNames.Of(mod.Stat).Percent ? "%" : "";
            return $"{text} (now {Amount(now)}{unit})";
        }

        /// <summary>Whether a modifier that asks for something of its wearer has it at the moment.</summary>
        private static bool Holds(in Mod mod, ActorSheet? viewer) => viewer == null || mod.Kind > ModKind.Override || viewer.WorthIfHeld(mod) != null;

        private static void Attach(Card card, in Mod mod, ActorSheet? viewer)
        {
            if (viewer == null) return;
            if (mod.Kind == ModKind.Trigger) card.Attached.Add(Book(viewer.Preview(Shelf.Get(mod.Book))));
            if (mod.Kind != ModKind.Rule) return;

            foreach (RuleInfo info in ModText.Rules)
            {
                if ((mod.Rule & info.Rule) == 0) continue;
                if (info.Casts != null) card.Attached.Add(Book(viewer.Preview(info.Casts)));
                if (info.Gives != null) card.Attached.Add(Buff(info.Gives, viewer));
            }
        }

        /// <summary>Modifiers as lines, each with whatever it sets off ready to be looked at.</summary>
        public static void Mods(Card card, IEnumerable<Mod> mods, ActorSheet? viewer, LineLook look = LineLook.Text)
        {
            foreach (Mod mod in mods)
            {
                card.Text(Line(mod, viewer), Holds(mod, viewer) ? look : LineLook.Dim);
                Attach(card, mod, viewer);
            }
        }

        // ---- Books.

        /// <summary>A book as its user has it: everything here is what a blow will read. What's on it is how it's used and what each page says, and the rest is under <see cref="Card.Details"/>.</summary>
        public static Card Book(BookProfile book)
        {
            BookPlan plan = book.Book;
            bool used = (book.First.Tags & Tags.Triggered) == 0;
            var card = new Card { Title = plan.Name, Kind = used ? "Book" : "", Tags = TagsOn(book) };

            if (BookText.Mode(plan.Mode, plan.When) is { Length: > 0 } mode) card.Text(mode);
            if (used && book.Mode == BookMode.Active) card.Cell("Use Time", Seconds(book.UseTime));
            if (book.Mode == BookMode.Toggle)
            {
                card.Cell("Pulse", Seconds(book.Pulse));
                card.Cell("Cost", $"{Amount(book.Cost)} Mana Regeneration");
            }
            else if (used && book.Cost > 0f) card.Cell("Cost", $"{Amount(book.Cost)} {(book.CostsHealth ? "Health" : "Mana")}");
            if (book.Cooldown > 0f) card.Cell("Cooldown", Seconds(book.Cooldown));
            if (used && book.Pages.Any(page => page.DealsDamage)) card.Row("Damage per Second", Amount(book.DamagePerSecond));

            for (int i = 0; i < book.Pages.Length; i++)
            {
                PageProfile page = book.Pages[i];
                card.Gap();
                if (plan.Source != null)
                {
                    card.Text(BookText.Says(plan.Source.Pages[i], i));
                    foreach (Sticker sticker in plan.Source.Pages[i].Stickers)
                    {
                        card.Text(BookText.Bad(sticker).Length > 0 ? $"{BookText.Good(sticker)}, {BookText.Bad(sticker)}" : BookText.Good(sticker), LineLook.Rolled);
                    }
                }
                DamageRows(card, page);
                AilmentLines(card, page);
                for (int gift = 0; gift < page.Page.Gifts.Length; gift++)
                {
                    card.Cell("Duration", Seconds(page.GiftSeconds[gift]));
                    card.Attached.Add(Buff(page.Page.Gifts[gift].Buff, book.Sheet));
                }
                if (page.Page.Minion != null) MinionRows(card, page);
            }

            Card details = Details(book);
            if (details.Lines.Count > 0) card.Details = details;
            return card;
        }

        private static Tags TagsOn(BookProfile book)
        {
            Tags tags = Tags.None;
            foreach (PageProfile page in book.Pages) tags |= page.Tags;
            return tags & ~(Tags.Hit | Tags.Opening | Tags.Turned | Tags.Closing);
        }

        /// <summary>A sentence as it's chosen: what it says, and what that does to the page it's on.</summary>
        public static Card Sentence(Sentence sentence)
        {
            var card = new Card { Title = sentence.Words };
            foreach (string line in BookText.Trade(sentence.Weights)) card.Text(line);
            if (sentence.Buff != null) card.Attached.Add(Buff(sentence.Buff, null));
            return card;
        }

        private static void DamageRows(Card card, PageProfile page)
        {
            Span<float> low = stackalloc float[DamageKinds.Count];
            Span<float> high = stackalloc float[DamageKinds.Count];
            foreach (ref readonly DamageFlow flow in page.Flows)
            {
                float scale = PageProfile.Scale(flow);
                low[(int)flow.Type] += flow.Min * scale;
                high[(int)flow.Type] += flow.Max * scale;
            }

            foreach (DamageType type in DamageKinds.All)
            {
                if (high[(int)type] > 0f) card.Row($"{type} Damage", Range(low[(int)type], high[(int)type]));
            }
        }

        // What it's made of, which isn't on the page because a sentence only says so much.
        private static void PageCells(Card card, PageProfile page)
        {
            PagePlan plan = page.Page;
            card.Cell("Potency", Share(page.Potency));
            if (page.Cost > 0f) card.Cell("Cost", Amount(page.Cost));

            if (plan.Kind == PageKind.Projectile)
            {
                card.Cell("Projectile Speed", $"{Amount(page.Speed)} m a second");
                card.Cell("Range", Metres(page.Range));
                if (page.Projectiles > 1) card.Cell("Projectiles", page.Projectiles.ToString());
                if (page.Projectiles > 1) card.Cell("Spread", Degrees(plan.Spread));
                if (page.Pierce > 0) card.Cell("Pierce", page.Pierce.ToString());
                if (page.Chains > 0) card.Cell("Chains", page.Chains.ToString());
                return;
            }

            switch (plan.Shape)
            {
                case AreaShape.Circle:
                    card.Cell("Radius", Metres(page.Length));
                    break;
                case AreaShape.Cone:
                    card.Cell("Reach", Metres(page.Length));
                    card.Cell("Arc", Degrees(page.Width));
                    break;
                default:
                    card.Cell("Reach", Metres(page.Length));
                    card.Cell("Width", Metres(page.Width * 2f));
                    break;
            }

            if (!plan.Lingers) return;
            card.Cell("Duration", Seconds(MathF.Max(page.Duration, ShortestZone)));
            card.Cell("Pulse", Seconds(page.Interval));
        }

        private static void AilmentLines(Card card, PageProfile skill)
        {
            for (int i = 0; i < AilmentRules.Count; i++)
            {
                var ailment = (Ailment)i;
                float low = 0f;
                float high = 0f;
                Tags causes = AilmentRules.Causes(ailment);
                foreach (ref readonly DamageFlow flow in skill.Flows)
                {
                    if ((DamageKinds.Tag(flow.Type) & causes) == 0) continue;
                    float scale = PageProfile.Scale(flow);
                    low += flow.Min * scale;
                    high += flow.Max * scale;
                }

                // What's put on someone outright doesn't take a hit with any of the right type in it, and needs no chance.
                float outright = skill.Inflicts[i];
                low += outright;
                high += outright;

                // A hit with none of the right type can't do it, whatever the chance says. Showing that chance is how a tooltip comes to lie.
                if (high <= 0f) continue;

                AilmentProfile profile = skill.Ailments[i];
                bool sure = outright > 0f || ailment == Ailment.Chill || ailment == Ailment.Burn && skill.Sheet.Has(Rule.FireFeedsBurn);
                if (profile.Chance <= 0f && !sure) continue;

                string opening = sure ? Lexicon.Of(ailment)!.Forms[1] : $"{Rate(profile.Chance)} Chance to {(ailment == Ailment.Venom ? "Envenom" : ailment.ToString())}";
                float seconds = AilmentRules.SecondsOf(ailment) * profile.Duration;
                float strength = profile.Effect * skill.LingerScale[i];
                string perSecond = Range(AilmentRules.PerSecond(ailment, low) * strength, AilmentRules.PerSecond(ailment, high) * strength);
                card.Text(ailment switch
                {
                    Ailment.Burn or Ailment.Bleed => $"{opening}: {perSecond} a second for {Seconds(seconds)}",
                    Ailment.Venom => $"{opening}: {perSecond} a second per Stack for {Seconds(seconds)}",
                    Ailment.Hex => $"{opening}: {Amount(AilmentRules.HexPerStack * profile.Effect)} Resistance per Stack for {Seconds(seconds)}",
                    _ => $"{opening} for {Seconds(seconds)}",
                });
            }
        }

        private static void MinionRows(Card card, PageProfile page)
        {
            ActorSheet sheet = page.Sheet;
            card.Cell("Minions", page.MinionCount.ToString());
            card.Cell("Duration", Seconds(MinionSeconds * sheet.MinionDuration));
            if (Bestiary.Minion(page.Page.Minion) is not EnemyKind kind) return;

            ActorSheet minion = Bestiary.MinionOf(sheet.Loadout, sheet, kind).SheetFor(Condition.None);
            card.Cell("Minion Health", Whole(minion.MaxHealth));
            if (minion.Loadout.Books.Length > 0) card.Cell("Minion Damage", Amount(minion.Profile(0).First.AverageHit));
        }

        // Nothing is hidden, but this much on one card is too much to read.
        private static Card Details(BookProfile book)
        {
            var card = new Card { Title = book.Book.Name, Kind = "Details" };
            bool used = (book.First.Tags & Tags.Triggered) == 0;
            if (used && !book.Rides) card.Cell("Impact", Seconds(book.ImpactTime));
            if (used && book.MoveFactor < 1f && !book.Rides) card.Cell("Moving while using", Share(book.MoveFactor));

            for (int i = 0; i < book.Pages.Length; i++)
            {
                PageProfile page = book.Pages[i];
                card.Heading($"Page {i + 1}");
                PageCells(card, page);

                if (page.DealsDamage)
                {
                    card.Cell("Critical Chance", Rate(page.CritChance));
                    card.Cell("Critical Multiplier", Rate(page.CritMultiplier));
                    if (page.IsAttack) card.Cell("Accuracy", Whole(page.Accuracy));
                    card.Gap();
                    FromTheBuild(card, page);
                    Sets(card, page);
                }

                foreach (RuleInfo info in ModText.Rules)
                {
                    if (info.Touches != Tags.None && page.Sheet.Has(info.Rule) && (page.Tags & info.Touches) == info.Touches) card.Text(info.Text);
                }

                if (page.DealsDamage) Breakdown(card, page);
            }
            return card;
        }

        // What it comes to with everything else a build has, said as modifiers so that it reads as they do everywhere else.
        private static void FromTheBuild(Card card, PageProfile skill)
        {
            void Say(Stat stat, float value)
            {
                if (value > 0f) card.Text(M.Flat(stat, value).Describe());
            }

            Say(Stat.ArmourIgnore, skill.ArmourIgnore);
            Say(Stat.HealthLeech, skill.HealthLeech * 100f);
            Say(Stat.ManaLeech, skill.ManaLeech * 100f);
            Say(Stat.HealthOnHit, skill.HealthOnHit);
            Say(Stat.ManaOnHit, skill.ManaOnHit);
            Say(Stat.HealthOnKill, skill.HealthOnKill);
            Say(Stat.ManaOnKill, skill.ManaOnKill);
            Say(Stat.BarrierOnKill, skill.BarrierOnKill);
            Say(Stat.Execute, skill.Execute);

            foreach (FoeBonus bonus in skill.FoeBonuses)
            {
                card.Text(new Mod { Stat = bonus.Stat, Kind = bonus.Kind, Value = bonus.Value, Foe = bonus.Foe }.Describe());
            }
        }

        private static void Sets(Card card, PageProfile skill)
        {
            var reactions = new List<string>();
            foreach ((Tags type, string reaction) in Reacting)
            {
                if ((skill.Tags & type) != 0) reactions.Add(reaction);
            }
            if (reactions.Count > 0) card.Text($"Reactions: {Say.List(reactions)}");
        }

        // Everything between what a page starts from and what it deals, so that there's nothing left to guess about how it scales.
        private static void Breakdown(Card card, PageProfile skill)
        {
            WeaponProfile weapon = skill.Sheet.Loadout.Weapon;
            foreach (DamagePart part in skill.Page.Damage)
            {
                string from = part.Measure == Measure.Damage ? $" ({Range(weapon.Min, weapon.Max)})" : "";
                card.Detail($"{Share(part.Share)} of {BookText.NameOf(part.Measure)}{from}, as {part.Type}");
            }

            foreach (DamageType type in DamageKinds.All)
            {
                float added = skill.Added[(int)type];
                if (added > 0f) card.Detail($"+{Amount(added)} Added {type} Damage");
            }

            foreach (ref readonly DamageFlow flow in skill.Flows)
            {
                var parts = new List<string> { $"{Range(flow.Min, flow.Max)} {flow.Type}" };
                Tags past = (Tags)(uint)flow.Lineage & ~DamageKinds.Tag(flow.Type);
                if (past != Tags.None) parts.Add($"Converted from {string.Join(" and ", TagNames(past))}");
                if (flow.Increased != 0f) parts.Add($"{Rate(MathF.Abs(flow.Increased))} {(flow.Increased > 0f ? "increased" : "reduced")}");
                if (flow.More != 1f) parts.Add($"{Rate(MathF.Abs(flow.More - 1f) * 100f)} {(flow.More > 1f ? "more" : "less")}");
                if (parts.Count > 1 || skill.Added.Total > 0f) card.Detail(string.Join(", ", parts));
            }
        }

        // ---- What can be worn, learned and called down.

        /// <summary>A lasting effect, with how much of it there is if it's on someone.</summary>
        public static Card Buff(BuffDefinition buff, ActorSheet? viewer, int stacks = 0, float left = 0f)
        {
            var card = new Card { Title = buff.Name, Tags = TagsOf(buff.Mods) | Tags.Buff };
            if (stacks > 0)
            {
                card.Cell("Stacks", $"{stacks} of {buff.MostStacks}");
                card.Cell("Duration", Seconds(left));
            }
            else
            {
                card.Cell("Duration", Seconds(buff.Duration));
                if (buff.MostStacks > 1) card.Cell("Stacks", $"Up to {buff.MostStacks}");
            }
            card.Gap();
            if (buff.MostStacks > 1) card.Text("Per Stack:", LineLook.Detail);
            Mods(card, buff.Mods, viewer);
            return card;
        }

        /// <param name="kind">What it is, in a word or two.</param>
        /// <summary>What grade of what something is, in a few words.</summary>
        public static string KindOf(Item item) => $"{GradeName(item)} {SlotName(item)}";

        public static string GradeName(Item item) => item.Unique != null ? "Unique" : Loot.GradeOf(item) switch
        {
            LootGrade.Plain => "Common",
            LootGrade.Magic => "Magic",
            LootGrade.Rare => "Rare",
            LootGrade.Masterwork => "Masterwork",
            _ => "Legendary",
        };

        private static string SlotName(Item item) => item.Slot switch
        {
            ItemSlot.MainHand => item.IsTwoHanded ? $"Two-handed {item.Kind}" : item.Kind.ToString(),
            ItemSlot.OffHand => "Off hand",
            ItemSlot.Finger => "Ring",
            ItemSlot.Neck => "Amulet",
            _ => item.Slot.ToString(),
        };

        public static Card Item(Item item, ActorSheet? viewer, string kind)
        {
            if (item.IsFolio) return Folio(item, viewer, kind);

            var card = new Card { Title = item.Name, Kind = $"{kind}, Level {item.Level}", Grade = Loot.GradeOf(item), Picture = item, Tags = TagsOf(item.Mods()) };
            if (item.Slot == ItemSlot.MainHand) card.Tags |= item.Weapon().Tags;

            foreach ((string name, string value) in item.BaseRows()) card.Row(name, value);
            if (item.Slot == ItemSlot.MainHand) Mods(card, WeaponKinds.Of(item.Kind).Mods, viewer);

            card.Gap();
            Mods(card, item.RolledMods(), viewer, LineLook.Rolled);

            if (item.Unique != null && UniqueCatalog.Find(item.Unique) is UniqueDefinition unique)
            {
                card.Gap();
                card.Lore(unique.Lore);
            }
            return card;
        }

        // What's in a book is what it is, and it's that which is worth reading. How much room there is comes first, as that's what one is worth.
        private static Card Folio(Item item, ActorSheet? viewer, string kind)
        {
            Book? written = item.Written();
            Card card = written != null && viewer != null ? Book(viewer.Preview(Scribe.Read(written, item.Name))) : new Card { Title = item.Name };
            if (written == null && BookText.Mode(item.Mode, item.When) is { Length: > 0 } mode) card.Text(mode);
            card.Kind = $"{kind}, Level {item.Level}";
            card.Grade = Loot.GradeOf(item);
            card.Picture = item;

            int taken = item.Held?.Count(held => held != null) ?? 0;
            if (item.IsBook) card.Cell("Pages", $"{taken} of {item.Places}");
            if (item.IsPage) card.Cell("Stickers", $"{taken} of {item.Places}");
            if (item.Sticker is Sticker sticker)
            {
                card.Text(BookText.Good(sticker), LineLook.Rolled);
                if (sticker.Bad != PageEffect.None) card.Text(BookText.Bad(sticker), LineLook.Rolled);
                if (Stickers.Fits(sticker) is PageKind fits) card.Text($"Only on a {fits} Page");
            }
            return card;
        }

        /// <summary>What a node on the tree gives, in the words of what it gives. Whether it can be had is for whoever's showing it to add.</summary>
        public static Card Talent(TalentNode node, ActorSheet? viewer)
        {
            var card = new Card { Title = node.Name, Tags = TagsOf(node.Mods), Gilded = node.Kind == TalentKind.Keystone };
            card.Kind = node.Kind switch
            {
                TalentKind.Keystone => "Keystone",
                TalentKind.Notable => "Notable",
                TalentKind.Gate => "Waygate",
                TalentKind.Root => "Where every Hero starts",
                _ => node.Sector >= 0 ? TalentContent.Sectors[node.Sector].Name : "",
            };

            Mods(card, node.Mods, viewer);
            if (node.Twin >= 0) card.Text("Lets you take the Waygate across the tree from it");
            if (node.Lore != null)
            {
                card.Gap();
                card.Lore(node.Lore);
            }
            return card;
        }

        public static Card Omen(OmenDefinition omen, ActorSheet? viewer)
        {
            var card = new Card { Title = omen.Name, Kind = "Omen", Tags = TagsOf(omen.Mods) };
            card.Text(omen.Describe());
            foreach (Mod mod in omen.Mods) Attach(card, mod, viewer);
            return card;
        }

        public static Card Dodge(ActorSheet sheet)
        {
            var card = new Card { Title = "Dodge" };
            card.Cell("Cooldown", Seconds(sheet.DodgeCooldown));
            card.Cell("Distance", Metres(sheet.DodgeSpeed * HeroRules.DashDuration));
            card.Gap();
            card.Text($"Immune to Hits for {Seconds(HeroRules.DashDuration + DodgeSafeExtra)}");
            return card;
        }

        public static Card Flask(HeroState hero, ActorSheet sheet)
        {
            var card = new Card { Title = "Flask" };
            card.Cell("Flasks", $"{hero.Flasks} of {HeroState.MaxFlasks}");
            card.Cell("Heals", Whole(sheet.MaxHealth * HeroRules.FlaskHeal * sheet.FlaskScale));
            card.Gap();
            card.Text($"{Amount(hero.FlaskCharge)} of {Whole(HeroState.KillsPerFlask)} Kills to the next");
            return card;
        }

        public static Card Wind()
        {
            var card = new Card { Title = "Guiding Wind" };
            card.Cell("Cooldown", Seconds(GuideCooldown));
            card.Gap();
            card.Text("Shows the way to your next goal");
            return card;
        }

        // ---- The hero's sheet.

        /// <summary>A line of the hero's sheet: what it comes to, what it does, and how it's worked out: all the flat added up, all the increased added up, and each more on its own.</summary>
        public static Card OfRow(StatRow row, ActorSheet sheet)
        {
            var card = new Card { Title = row.Name };
            card.Row("Total", row.Reads(sheet));
            if (Lexicon.Of(row.Stats[0]) is Term term)
            {
                card.Gap();
                card.Text(term.Says);
            }

            if (row.Notes != null)
            {
                foreach (string note in row.Notes(sheet)) card.Text(note, LineLook.Detail);
            }

            card.Gap();
            foreach (Stat stat in row.Stats) Calculation(card, row, stat, sheet);
            Elsewhere(card, row, sheet);
            return card;
        }

        private static void Calculation(Card card, StatRow row, Stat stat, ActorSheet sheet)
        {
            bool first = stat == row.Stats[0];
            StatSum sum = sheet.Collect(stat, row.Within ?? Tags.None);
            float start = first ? row.Base : 0f;
            float flat = sum.Flat - start - (first ? row.Fixed : 0f);

            if (start != 0f) card.Cell("Base", Amount(start));
            if (flat != 0f) card.Cell("Flat", Signed(flat));
            if (sum.Increased != 0f) card.Cell(sum.Increased > 0f ? "Increased" : "Reduced", Rate(MathF.Abs(sum.Increased)));

            foreach (Mod mod in sheet.Loadout.On(stat))
            {
                if (mod.Kind != ModKind.More || !Applies(row, mod, sheet)) continue;
                card.Row(Line(mod, sheet), $"x{N(1f + (sheet.WorthIfHeld(mod) ?? 0f) / 100f)}");
            }

            if (sum.Overridden) card.Row("Set to", Amount(sum.Set));
            else if (row.Adds && row.Stats.Length == 1 && sum.Flat != 0f && sum.Scale != 1f) card.Detail($"{Amount(sum.Flat)} x {Rate(MathF.Max(1f + sum.Increased / 100f, 0f) * 100f)}{(sum.More != 1f ? $" x {N(sum.More)}" : "")} = {Amount(sum.Apply(0f))}");
        }

        // Whether a modifier counts towards the total shown, right now.
        private static bool Applies(StatRow row, in Mod mod, ActorSheet sheet)
        {
            return mod.Foe == Condition.None && (mod.Scope & ~(row.Within ?? Tags.None)) == 0 && sheet.WorthIfHeld(mod) != null;
        }

        // What scales with something, and what only counts for some skills or at some times, which is none of the sum above.
        private static void Elsewhere(Card card, StatRow row, ActorSheet sheet)
        {
            var scaling = new List<string>();
            var situational = new List<string>();
            foreach (Mod mod in Gathered(row, sheet))
            {
                if (!row.Counts(mod)) continue;
                if (!Applies(row, mod, sheet)) situational.Add(Line(mod, sheet));
                else if (mod.Per != Per.None) scaling.Add(Line(mod, sheet));
            }

            if (scaling.Count > 0) card.Heading("Scaling");
            foreach (string line in scaling) card.Text(line);
            if (situational.Count > 0) card.Heading("Situational");
            foreach (string line in situational) card.Text(line, LineLook.Dim);
        }

        // What goes into a line, with what could be added together added together: twelve talents of health read as one number, as that's what they are.
        private static List<Mod> Gathered(StatRow row, ActorSheet sheet)
        {
            var gathered = new List<Mod>();
            foreach (Stat stat in row.Stats)
            {
                foreach (Mod mod in sheet.Loadout.On(stat))
                {
                    int same = gathered.FindIndex(other => SameBut(other, mod));
                    if (same < 0)
                    {
                        gathered.Add(mod);
                        continue;
                    }

                    Mod held = gathered[same];
                    gathered[same] = held with { Value = mod.Kind == ModKind.More ? ((1f + held.Value / 100f) * (1f + mod.Value / 100f) - 1f) * 100f : held.Value + mod.Value };
                }
            }
            return gathered;
        }

        // Only those whose value alone differs add up, and something that's only worth what it's counted per can't.
        private static bool SameBut(in Mod a, in Mod b) => a with { Value = 0f } == b with { Value = 0f } && a.Per == Per.None && a.Kind is ModKind.Flat or ModKind.Increased or ModKind.More;
    }
}
