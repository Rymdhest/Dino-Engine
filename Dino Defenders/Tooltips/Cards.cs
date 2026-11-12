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
        Burden,     // What a unique costs, apart from what it gives.
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
        public Rarity? Rarity;

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
                        if (mod.Rule == info.Rule) tags |= info.Touches;
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
            if (mod.Kind == ModKind.Trigger)
            {
                if (mod.Book != 0) card.Attached.Add(Book(viewer.Preview(Shelf.Get(mod.Book))));
                else if (!mod.Spends && BuffCatalog.Get(mod.Gift) is BuffDefinition buff) card.Attached.Add(Buff(buff, viewer));
            }
            if (mod.Kind != ModKind.Rule || ModText.Of(mod.Rule) is not RuleInfo info) return;

            if (info.Casts != null) card.Attached.Add(Book(viewer.Preview(info.Casts)));
            if (info.Gives != null) card.Attached.Add(Buff(info.Gives, viewer));
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
                    card.Text(BookText.Says(plan.Source.Pages[i], page.Page, page.Potency));
                    foreach (Sentence sentence in plan.Source.Pages[i].Sentences)
                    {
                        card.Text(BookText.Bad(sentence).Length > 0 ? $"{BookText.Good(sentence)}, {BookText.Bad(sentence)}" : BookText.Good(sentence), LineLook.Rolled);
                    }
                    foreach (Mod mod in plan.Source.Pages[i].Mods) card.Text(mod.Describe(), LineLook.Rolled);
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

        /// <summary>A phrase as it's chosen: what it says, and what that does to the page it's on.</summary>
        public static Card Phrase(Phrase phrase)
        {
            var card = new Card { Title = phrase.Words };
            foreach (string line in BookText.Trades(phrase.Weights)) card.Text(line);
            if (phrase.Buff != null) card.Attached.Add(Buff(phrase.Buff, null));
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

        // What it's made of, which isn't on the page because a phrase only says so much.
        private static void PageCells(Card card, PageProfile page)
        {
            PagePlan plan = page.Page;
            card.Cell("Potency", Share(page.Potency));
            if (page.Cost > 0f) card.Cell("Cost", Amount(page.Cost));

            if (plan.Kind == PageKind.Summon) return;
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
            card.Cell("Duration", Seconds(MinionSeconds * page.MinionDuration));
            if (Bestiary.Minion(page.Page.Minion) is not EnemyKind kind) return;

            // What they've been given is what they are: the weapon they strike with and the book they use, and a hero's Mana to pay for it.
            BookPlan? given = Scribe.ForMinions(page.Page.MinionBook);
            ActorSheet minion = Bestiary.MinionOf(sheet.Loadout, sheet, kind, page.Page.MinionWeapon, page.Page.MinionOffHand, given).SheetFor(Condition.None);
            card.Cell("Minion Health", Whole(minion.MaxHealth));
            bool pays = given != null && !kind.Rooted;
            if (pays) card.Cell("Minion Mana", Whole(minion.MaxMana));
            if (minion.Loadout.Books.Length > 0) card.Cell("Minion Damage", Amount(minion.Profile(0).First.AverageHit));
            if (pays && minion.Profile(0).Cost > 0f) card.Cell("Minion Cost", $"{Amount(minion.Profile(0).Cost)} Mana");
            if (kind.Rooted) card.Text("Stands where it's raised, and uses its Book for nothing");
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
            foreach (DamageType type in DamageKinds.All)
            {
                if (skill.BaseMax[(int)type] > 0f) card.Detail($"{Range(skill.BaseMin[(int)type], skill.BaseMax[(int)type])} {type} Damage from your Weapon");
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
        /// <summary>What rarity of what something is, in a few words.</summary>
        public static string KindOf(Item item) => $"{item.Rarity} {SlotName(item)}";

        /// <summary>
        /// Orders: what their kind always brings, then what enemies are and what the land is, or what a unique one does and what it costs. What it all pays is said last, as that's what
        /// orders are weighed by, and so is where they go.
        /// </summary>
        public static Card Orders(Item item, string kind)
        {
            var card = new Card { Title = item.Name, Kind = $"{kind}, Level {item.Level}", Rarity = item.Rarity, Picture = item };
            var terms = new LandTerms();
            terms.Give(item);

            if (OrdersMaker.BaseOf(item) is OrdersBase basis)
            {
                card.Text($"{basis.Brings.Name}: {basis.Brings.Describe(basis.Brings.Fixed(item.Level))}", LineLook.Rolled);
            }

            if (item.Rarity == Rarity.Unique && OrdersCatalog.Find(item.Unique) is UniqueOrders unique)
            {
                card.Gap();
                card.Text(unique.Gives, LineLook.Rolled);
                card.Gap();
                card.Text(unique.Costs, LineLook.Burden);
                card.Gap();
                card.Lore(unique.Lore);
            }
            else
            {
                OmenLines(card, item, OmenSide.Prefix, "What the land gives");
                OmenLines(card, item, OmenSide.Suffix, "What it asks");
            }

            card.Gap();
            if (terms.Pays() is { Length: > 0 } pays) card.Text($"Pays {pays}", LineLook.Good);
            card.Text("Give them to the map table in the Haven before the way is opened, and the land it opens on holds what they say");
            return card;
        }

        private static void OmenLines(Card card, Item item, OmenSide side, string heading)
        {
            bool first = true;
            foreach (ItemLine line in item.Lines)
            {
                if (Progression.Omens.Find(line.Affix) is not OmenDefinition omen || omen.Side != side) continue;
                if (first) card.Heading(heading);
                first = false;
                card.Text($"{omen.Name}: {omen.Describe(line.Value)}", LineLook.Rolled);
            }
        }

        private static string SlotName(Item item) => item.Slot switch
        {
            ItemSlot.Orders => "Orders",
            ItemSlot.MainHand => item.IsTwoHanded ? $"Two-handed {item.Kind}" : item.Kind.ToString(),
            ItemSlot.OffHand => "Off hand",
            ItemSlot.Finger => "Ring",
            ItemSlot.Neck => "Amulet",
            _ => item.Slot.ToString(),
        };

        public static Card Item(Item item, ActorSheet? viewer, string kind)
        {
            if (item.IsFolio) return Folio(item, viewer, kind);
            if (item.IsOrders) return Orders(item, kind);

            var card = new Card { Title = item.Name, Kind = $"{kind}, Level {item.Level}", Rarity = item.Rarity, Picture = item, Tags = TagsOf(item.Mods()) };
            if (item.Slot == ItemSlot.MainHand) card.Tags |= item.Weapon().Tags;

            foreach ((string name, string value) in item.BaseRows()) card.Row(name, value);
            if (item.Slot == ItemSlot.MainHand) Mods(card, WeaponKinds.Of(item.Kind).Mods, viewer);

            card.Gap();
            if (item.Unique == null)
            {
                Side(card, item, true, viewer);
                Side(card, item, false, viewer);
            }
            else if (UniqueCatalog.Find(item.Unique) is UniqueDefinition unique)
            {
                // What it gives, and then what it costs, apart and in a colour of its own.
                Mods(card, unique.Boon, viewer, LineLook.Rolled);
                card.Gap();
                Mods(card, unique.Burden, viewer, LineLook.Burden);
                card.Gap();
                card.Lore(unique.Lore);
            }
            return card;
        }

        // One side of what's rolled on something, under its name. It goes by each line's affix and not by where it's written, as a save can have them in any order.
        private static void Side(Card card, Item item, bool prefix, ActorSheet? viewer)
        {
            List<ItemLine> lines = item.Lines.Where(line => AffixCatalog.Find(line.Affix)?.Prefix == prefix).ToList();
            if (lines.Count == 0) return;

            card.Heading(prefix ? lines.Count == 1 ? "Prefix" : "Prefixes" : lines.Count == 1 ? "Suffix" : "Suffixes");
            foreach (ItemLine line in lines) Mods(card, AffixCatalog.Find(line.Affix)!.Build(line.Value), viewer, LineLook.Rolled);
        }

        /// <summary>
        /// An item's card as a player is shown it, with what it's worth at the end. This is apart from <see cref="Item"/> as that's what a search reads, and everything has a price: "gold" would find the whole pack.
        /// </summary>
        /// <param name="price">What it says instead of what it would sell for, on a merchant's table.</param>
        public static Card Priced(Item item, ActorSheet? viewer, string kind, string? price = null)
        {
            Card card = Item(item, viewer, kind);
            card.Gap();
            card.Text(price ?? Sells(item), LineLook.Price);
            return card;
        }

        public static string Sells(Item item) => $"Sells for {Gold(item.SellValue)}";

        public static string Costs(Item item) => $"Costs {Gold(MerchantTable.PriceOf(item, buyingBack: false))}";

        public static string BuyBack(Item item) => $"Buy back for {Gold(MerchantTable.PriceOf(item, buyingBack: true))}";

        // What's in a book is what it is, and it's that which is worth reading. How much room there is comes first, as that's what one is worth.
        private static Card Folio(Item item, ActorSheet? viewer, string kind)
        {
            if (item.IsPage) return LoosePage(item, viewer, kind);
            Book? written = item.Written();
            BookPlan? plan = written != null ? Scribe.Read(written, item.Name) : null;
            Card card = plan != null && viewer != null ? Book(viewer.Preview(plan)) : new Card { Title = item.Name };
            if (written == null && BookText.Mode(item.Mode, item.When) is { Length: > 0 } mode) card.Text(mode);
            card.Kind = $"{kind}, Level {item.Level}";
            card.Rarity = item.Rarity;
            card.Picture = item;

            int taken = item.Held?.Count(held => held != null) ?? 0;
            if (item.IsBook) card.Cell("Pages", $"{taken} of {item.Places}");
            if (item.IsPage) card.Cell("Sentences", $"{taken} of {item.Places}");
            if (item.IsBook || item.IsPage) FolioLines(card, item);
            if (item.Sentence is Sentence sentence)
            {
                card.Text(BookText.Good(sentence), LineLook.Rolled);
                if (sentence.Bad != PageEffect.None) card.Text(BookText.Bad(sentence), LineLook.Rolled);
                if (Sentences.Fits(sentence) is PageKind fits) card.Text($"Only on a {fits} Page");
                if (item.Lines.Count > 0) FolioLines(card, item);
            }
            return card;
        }

        /// <summary>
        /// A page on its own says only what it is: what it costs and how long it takes before anything of yours makes those more or less, and what's rolled on it. What it deals is for the book it goes in, whose
        /// card works it all out, as a page alone was too much to read.
        /// </summary>
        private static Card LoosePage(Item item, ActorSheet? viewer, string kind)
        {
            var card = new Card { Title = item.Name, Kind = $"{kind}, Level {item.Level}", Rarity = item.Rarity, Picture = item };
            if (item.Written() is Book written)
            {
                written.Pages[0].Mods = Array.Empty<Mod>();
                PagePlan page = Scribe.Read(written).First;
                card.Tags = page.Tags & ~(Tags.Opening | Tags.Turned | Tags.Closing);
                float cost = page.Cost * PageProfile.CostAtLevel(viewer?.Loadout.Level ?? item.Level);
                if (cost > 0f) card.Cell("Cost", $"{Amount(cost)} Mana");
                card.Cell("Use Time", page.Hand == Hand.Spell ? Seconds(page.UseTime * page.UseScale) : page.UseScale == 1f ? "As your Weapon" : $"{Share(page.UseScale)} of your Weapon's");
            }
            card.Cell("Sentences", $"{item.Held?.Take(item.SentencePlaces).Count(held => held != null) ?? 0} of {item.SentencePlaces}");
            if (item.Summons) SummonCells(card, item);
            FolioLines(card, item);
            return card;
        }

        // What a Summon Page raises before anything of yours makes more of it or makes it last longer: what it says and what's rolled on it, and what its Minions have been given.
        private static void SummonCells(Card card, Item item)
        {
            if (item.Written() is not Book written) return;
            PagePlan page = Scribe.Read(written).First;
            Mod[] own = FolioAffixes.ModsOf(item);
            float more = own.Where(mod => mod.Stat == Stat.MinionCount && mod.Kind == ModKind.Flat).Sum(mod => mod.Value);
            float longer = own.Where(mod => mod.Stat == Stat.MinionDuration && mod.Kind == ModKind.Increased).Sum(mod => mod.Value);
            card.Cell("Minions", Whole(page.MinionCount + more));
            card.Cell("Minion Duration", Seconds(MinionSeconds * (1f + longer / 100f)));
            card.Cell("Weapon", item.MinionWeapon()?.Name ?? "none");
            card.Cell("Off hand", item.MinionOffHand()?.Name ?? "none");
            card.Cell("Book", item.MinionBook()?.Name ?? "none");
        }

        // What a book or a page holds before anything's rolled on it, then what's rolled on it, side by side as gear's are, and where that counts.
        private static void FolioLines(Card card, Item item)
        {
            card.Gap();
            if (!item.IsSentence) card.Text(FolioAffixes.Holds(item));
            foreach (bool prefix in new[] { true, false })
            {
                List<ItemLine> lines = item.Lines.Where(line => FolioAffixes.Find(line.Affix)?.Prefix == prefix).ToList();
                if (lines.Count == 0) continue;
                card.Heading(prefix ? lines.Count == 1 ? "Prefix" : "Prefixes" : lines.Count == 1 ? "Suffix" : "Suffixes");
                foreach (ItemLine line in lines)
                {
                    foreach (string text in FolioAffixes.Says(FolioAffixes.Find(line.Affix)!, line.Value)) card.Text(text, LineLook.Rolled);
                }
            }
            if (item.Lines.Count > 0) card.Text(item.IsBook ? "What's rolled on a Book counts on every Page in it" : item.IsSentence ? "What's rolled on a Sentence counts on the Page it's on" : "What's rolled on a Page counts on that Page wherever it goes");
        }

        // A region's name inside a sentence, where "The" isn't at the start of anything.
        private static string Lowered(string name) => name.StartsWith("The ") ? "the " + name[4..] : name;

        /// <summary>What a talent is called: a small node gives one thing and is called what it gives, and anything bigger has a name of its own.</summary>
        public static string TitleOf(TalentNode node) => node.Kind == TalentKind.Small && node.Mods.Length == 1 ? ModText.Name(node.Mods[0]) : node.Name;

        public static Card Talent(TalentNode node, ActorSheet? viewer)
        {
            var card = new Card { Title = TitleOf(node), Tags = TagsOf(node.Mods), Gilded = node.Kind == TalentKind.Keystone };
            card.Kind = node.Kind switch
            {
                TalentKind.Keystone => "Keystone",
                TalentKind.Notable => node.Beginning ? "One of six ways to begin" : node.Entry ? $"Where {Lowered(TalentContent.Regions[node.Region].Name)} begins" : "Notable",
                TalentKind.Gate => "Waygate",
                TalentKind.Root => "Where every Hero starts",
                _ => node.Highway ? "Highway" : node.Region >= 0 ? TalentContent.Regions[node.Region].Name : "",
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

        /// <summary>An omen, for as much of it as a land holds: what it does, and what it pays for doing it.</summary>
        public static Card Omen(OmenDefinition omen, float value, ActorSheet? viewer)
        {
            Mod[] mods = omen.Mods(value).ToArray();
            var card = new Card { Title = omen.Name, Kind = omen.IsPrefix ? "Omen the land gives" : "Omen of what it asks", Tags = TagsOf(mods) };
            card.Text(omen.Describe(value));
            foreach (Mod mod in mods) Attach(card, mod, viewer);

            var alone = new LandTerms();
            alone.Omens.Add(new ItemLine(omen.Id, value));
            if (alone.Pays() is { Length: > 0 } pays)
            {
                card.Gap();
                card.Text($"Pays {pays}", LineLook.Good);
            }
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

        /// <summary>How far the hero is through their level: what they've gained in it, what it takes, and what's left.</summary>
        public static Card Experience(HeroState hero)
        {
            var card = new Card { Title = $"Level {hero.Level}" };
            if (hero.Level >= HeroState.MaxLevel)
            {
                card.Text("The highest Level there is");
                return card;
            }
            card.Cell("Experience", $"{Whole(hero.Experience)} of {Whole(hero.ExperienceToNext)}");
            card.Gap();
            card.Text($"{Whole(hero.ExperienceToNext - hero.Experience)} more to the next Level");
            return card;
        }

        /// <summary>A word that means something, as its own card, the same as a marked word opens: an ailment the hero is suffering, for one.</summary>
        public static Card Defined(Term term)
        {
            var card = new Card { Title = term.Name };
            card.Text(term.Says);
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
