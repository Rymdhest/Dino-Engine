using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using System.Text.Json.Serialization;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// Where something is on a hero, as one number so that a message can say it: a cell of the pack, a place they wear it, or a place on the bar, or a place inside what's in one of those, as a page is in a book and a
    /// sticker on that page. Whatever moves an item says where from and where to with these, and a number that's none of those holds nothing.
    /// </summary>
    public readonly record struct ItemPlace(int Number)
    {
        private const int FirstOnBar = HeroState.InventorySize + 16;
        private const int OuterBits = 8;
        private const int InnerBits = 4;
        private const int InnerMask = (1 << InnerBits) - 1;

        public static ItemPlace Cell(int cell) => new(cell);
        public static ItemPlace Worn(WornSlot place) => new(HeroState.InventorySize + (int)place);
        public static ItemPlace OnBar(int slot) => new(FirstOnBar + slot);

        public bool IsCell => Number >= 0 && Number < HeroState.InventorySize;
        public bool IsWorn => Number >= HeroState.InventorySize && Number < FirstOnBar && Enum.IsDefined((WornSlot)(Number - HeroState.InventorySize));
        public bool IsBar => Number >= FirstOnBar && Number < FirstOnBar + HeroState.BarSlots;
        public WornSlot Slot => (WornSlot)(Number - HeroState.InventorySize);
        public int BarSlot => Number - FirstOnBar;

        private int First => Number >> OuterBits & InnerMask;
        private int Second => Number >> OuterBits + InnerBits & InnerMask;

        /// <summary>Whether it's a place inside something else, which is all these below mean anything for.</summary>
        public bool IsHeld => Number >= 1 << OuterBits;

        /// <summary>A place inside whatever's here.</summary>
        public ItemPlace In(int slot) => new(First == 0 ? Number | slot + 1 << OuterBits : Number | slot + 1 << OuterBits + InnerBits);

        /// <summary>What it's a place in.</summary>
        public ItemPlace Holder => Second != 0 ? new(Number & ~(InnerMask << OuterBits + InnerBits)) : new(Number & (1 << OuterBits) - 1);

        /// <summary>Which of the places in that it is.</summary>
        public int Index => (Second != 0 ? Second : First) - 1;
    }

    /// <summary>The hero as they carry on from world to world. Saved as it stands.</summary>
    public sealed class HeroState
    {
        public const int MaxLevel = 100;
        public const float BaseMoveSpeed = 10f;
        public const int MaxFlasks = 4;
        public const float KillsPerFlask = 15f;
        public const int InventorySize = 36;
        public const int BarSlots = 6;
        public const int LongestBookName = 40;

        /// <summary>What they're called, on the main menu and to anyone they travel with.</summary>
        public string Name { get; set; } = "";

        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int Gold { get; set; }
        public int Flasks { get; set; } = 3;
        public float FlaskCharge { get; set; }
        /// <summary>How many lands this hero has freed, in all.</summary>
        public int WorldsCleared { get; set; }

        /// <summary>
        /// The deepest land this hero has freed. Every level up to it can be opened again, and the one after it, which is their climb. Nothing ever lowers it: falling in a land or leaving it costs
        /// only the try. It's what the talent points for freeing lands and the orders the map table takes go by. Read LANDS.md.
        /// </summary>
        public int BestLand { get; set; }

        /// <summary>The deepest level the map table opens for this hero: the one after the deepest they've freed.</summary>
        [JsonIgnore] public int LandLevel => Math.Max(BestLand, 0) + 1;

        /// <summary>Where the lands the map table shows this hero come from, with how many times each level's have been shown again, so the choice is the same however often it's looked at.</summary>
        public ulong Offers { get; set; }

        /// <summary>
        /// How many times the lands of each level have been changed for others, by level: once each time gold is paid for it, and once each time a land of that level is freed. A level that isn't
        /// here never has been.
        /// </summary>
        public Dictionary<int, int> Rerolls { get; set; } = new();

        /// <summary>The nodes taken on the talent tree, by id, in the order they were taken.</summary>
        public List<string> Talents { get; set; } = new();

        /// <summary>What they've done, kept for the Archive in the Haven.</summary>
        public HeroRecords Records { get; set; } = new();

        /// <summary>The Haven's halls they've opened, by name. Every one is open for now, while the game's being tested (<see cref="Halls.AllOpen"/>).</summary>
        public List<string> HallsOpen { get; set; } = new();

        /// <summary>The books on the bar, which are there and not in the pack, as a ring is on a finger. See BOOKS.md.</summary>
        public Item?[] Bar { get; set; } = new Item?[BarSlots];

        /// <summary>What's carried, cell by cell. A cell is the player's: nothing moves from one unless they say so, or ask for the whole pack to be sorted.</summary>
        public Item?[] Inventory { get; set; } = new Item?[InventorySize];
        public Dictionary<WornSlot, Item> Equipped { get; set; } = new();

        public int ExperienceToNext => (int)(120f * MathF.Pow(Level, 1.7f));

        public const int PointsPerLevel = 2;

        /// <summary>Two a level, and one for each level of land climbed and freed up to the hundredth, with three to start out with.</summary>
        public int TalentPoints => 1 + PointsPerLevel * Level + Math.Clamp(BestLand, 0, Lands.MostCountedLand);

        public int UnspentPoints => Math.Max(TalentPoints - Talents.Count, 0);

        /// <summary>The wind answers those who've set a land free, and nobody before that.</summary>
        public bool HasGuidingWind => BestLand > 0 || WorldsCleared > 0;

        /// <summary>
        /// A land of this level has been freed, by a hero still standing in it. Climbs as far as the land's terms say, from that land, but never past a boss that hasn't been fought, so freeing a friend's
        /// lower land never costs anyone their own climb. The map table shows other lands for that level from now on.
        /// </summary>
        public void Freed(int level, int climb)
        {
            BestLand = Math.Max(BestLand, Lands.FreedTo(level, climb));
            WorldsCleared++;
            Reroll(level);
        }

        /// <summary>How many times the lands of a level have been changed for others. A method, so that it isn't saved.</summary>
        public int RerollsAt(int level) => Rerolls.TryGetValue(level, out int count) ? count : 0;

        /// <summary>The map table shows other lands for this level from now on.</summary>
        public void Reroll(int level) => Rerolls[level] = RerollsAt(level) + 1;

        /// <summary>Whether a land of this level can be opened by this hero: any they've freed, or the one after.</summary>
        public bool CanOpen(int level) => level >= 1 && level <= LandLevel;

        /// <summary>A start for a new hero's offers, or a step on from the last.</summary>
        public static ulong NewOffers(ulong from)
        {
            ulong mixed = (from ^ (from >> 30)) * 0xBF58476D1CE4E5B9UL;
            mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
            mixed ^= mixed >> 31;
            return mixed == 0 ? 1 : mixed;
        }

        /// <summary>Adds experience. Returns how many levels it brought.</summary>
        public int GainExperience(int amount)
        {
            int levels = 0;

            // At the last level there's nothing more to count towards.
            if (Level >= MaxLevel)
            {
                Experience = 0;
                return 0;
            }
            Experience = (int)Math.Min((long)Experience + Math.Max(amount, 0), int.MaxValue);
            while (Level < MaxLevel && Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                levels++;
            }
            return levels;
        }

        /// <summary>Puts a kill towards the next flask. Says whether that filled one.</summary>
        public bool ChargeFlask(float worth)
        {
            if (Flasks >= MaxFlasks)
            {
                FlaskCharge = 0f;
                return false;
            }

            FlaskCharge += worth;
            if (FlaskCharge < KillsPerFlask) return false;
            FlaskCharge -= KillsPerFlask;
            Flasks++;
            return true;
        }

        /// <summary>Everything carried, in the order of its cells. This and those below are methods so that they aren't saved.</summary>
        public IEnumerable<Item> Carried() => Inventory.OfType<Item>();

        /// <summary>The first empty cell, or -1 if there isn't one.</summary>
        public int FreeCell() => Array.IndexOf(Inventory, null);

        public bool HasRoom() => FreeCell() >= 0;

        /// <summary>What's in a place, if it's a place at all.</summary>
        public Item? At(ItemPlace place)
        {
            if (place.IsHeld) return At(place.Holder)?.Held is Item?[] held && place.Index >= 0 && place.Index < held.Length ? held[place.Index] : null;
            if (place.IsCell) return Inventory[place.Number];
            if (place.IsBar) return Bar[place.BarSlot];
            return place.IsWorn ? Equipped.GetValueOrDefault(place.Slot) : null;
        }

        /// <summary>Puts something in the pack: in the cell asked for if that's empty, or else in the first that is. Nothing already there moves.</summary>
        public bool Pickup(Item item, int? cell = null)
        {
            int into = cell is int asked && ItemPlace.Cell(asked).IsCell && Inventory[asked] == null ? asked : FreeCell();
            if (into < 0) return false;
            Inventory[into] = item;
            return true;
        }

        /// <summary>Takes something off the hero altogether, for what's leaving them.</summary>
        public Item? Take(ItemPlace place)
        {
            if (At(place) is not Item item) return null;
            if (place.IsHeld) At(place.Holder)!.Held![place.Index] = null;
            else if (place.IsCell) Inventory[place.Number] = null;
            else if (place.IsBar) Bar[place.BarSlot] = null;
            else Equipped.Remove(place.Slot);
            return item;
        }

        /// <summary>
        /// Where something would go: where it was asked to, if it fits there, or else the first free place for it, or else the first place. A weapon goes in the main hand unless it's
        /// put in the other, as a second weapon is a choice and a new one is usually meant to replace the old.
        /// </summary>
        public WornSlot PlaceFor(Item item, WornSlot? asked = null)
        {
            IReadOnlyList<WornSlot> places = WornSlots.For(item);
            if (asked is WornSlot wanted && places.Contains(wanted)) return wanted;
            if (item.Slot != ItemSlot.Finger) return places[0];
            foreach (WornSlot place in places)
            {
                if (!Equipped.ContainsKey(place)) return place;
            }
            return places[0];
        }

        /// <summary>What something would be set beside, if anything. A book or orders are worn nowhere, so they have no place to look in.</summary>
        public Item? WornBeside(Item item) => item.IsWorn && Equipped.TryGetValue(PlaceFor(item), out Item? worn) && worn != item ? worn : null;

        /// <summary>
        /// What wearing something would take off: whatever's in its place, both rings for a ring, both weapons for a weapon that takes one hand (but not a shield, which it wouldn't), and for
        /// something that changes how the hands are used, whatever's in the other one.
        /// </summary>
        public IEnumerable<Item> WornInPlaceOf(Item item)
        {
            foreach (WornSlot place in WornSlots.For(item))
            {
                if (!Equipped.TryGetValue(place, out Item? worn) || worn == item) continue;
                if (place == WornSlot.OffHand && item.IsOneHandedWeapon && !worn.IsOneHandedWeapon) continue;
                yield return worn;
            }
            if (item.IsTwoHanded && Equipped.TryGetValue(WornSlot.OffHand, out Item? offHand)) yield return offHand;
            if (item.Slot == ItemSlot.OffHand && Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded) yield return held;
        }

        /// <summary>
        /// Puts on what's in a cell, and what comes off goes into that same cell, so that the two change places and nothing else in the pack moves. The one thing that can need more room is
        /// something two-handed taking both hands, as the second of what comes off needs a cell of its own. Says whether it could.
        /// </summary>
        public bool Equip(int cell, WornSlot? asked = null)
        {
            if (!ItemPlace.Cell(cell).IsCell || Inventory[cell] is not Item item || !item.IsWorn) return false;
            WornSlot place = PlaceFor(item, asked);

            // Two hands are two hands.
            WornSlot? displaced = null;
            if (item.IsTwoHanded) displaced = WornSlot.OffHand;
            else if (place == WornSlot.OffHand && Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded) displaced = WornSlot.MainHand;

            Item? old = Equipped.GetValueOrDefault(place);
            Item? second = displaced is WornSlot other ? Equipped.GetValueOrDefault(other) : null;
            int spare = old != null && second != null ? FreeCell() : cell;
            if (spare < 0) return false;

            Inventory[cell] = old ?? second;
            if (old != null && second != null) Inventory[spare] = second;
            if (displaced is WornSlot gone) Equipped.Remove(gone);
            Equipped[place] = item;
            return true;
        }

        /// <summary>The same, for something known to be in the pack.</summary>
        public bool Equip(Item item, WornSlot? asked = null) => Equip(Array.IndexOf(Inventory, item), asked);

        /// <summary>Takes something off, into the cell asked for if that's empty, or else the first that is. Says whether there was room.</summary>
        public bool Unequip(WornSlot place, int? cell = null)
        {
            if (!Equipped.TryGetValue(place, out Item? item) || !Pickup(item, cell)) return false;
            Equipped.Remove(place);
            return true;
        }

        /// <summary>
        /// Moves something from one place on the hero to another, and whatever's there goes where it came from. It's the one way anything moves by the player's hand, so a swap can't half happen:
        /// this says no and changes nothing if what's there couldn't go back the other way.
        /// </summary>
        public bool Move(ItemPlace from, ItemPlace to)
        {
            if (from == to || At(from) is not Item item) return false;
            if (from.IsHeld || to.IsHeld) return MoveHeld(from, to, item);
            if (from.IsBar || to.IsBar) return MoveOnBar(from, to, item);

            if (from.IsCell && to.IsCell)
            {
                Inventory[from.Number] = Inventory[to.Number];
                Inventory[to.Number] = item;
                return true;
            }

            if (from.IsCell) return to.IsWorn && WornSlots.Fits(item, to.Slot) && Equip(from.Number, to.Slot);

            if (to.IsCell)
            {
                // Something's in the way: it's as if that had been put on, which it can only be if it fits there.
                if (Inventory[to.Number] is not Item there) return Unequip(from.Slot, to.Number);
                return WornSlots.Fits(there, from.Slot) && Equip(to.Number, from.Slot);
            }

            // From one place on the hero to another, which a ring has, and a weapon that takes one hand. Whatever's there has to fit where this came from.
            if (!to.IsWorn || !WornSlots.Fits(item, to.Slot)) return false;
            Item? other = Equipped.GetValueOrDefault(to.Slot);
            if (other != null && !WornSlots.Fits(other, from.Slot)) return false;
            Equipped[to.Slot] = item;
            if (other != null) Equipped[from.Slot] = other;
            else Equipped.Remove(from.Slot);
            return true;
        }

        // Only a book goes on the bar, and whatever's there goes where it came from, if it can.
        private bool MoveOnBar(ItemPlace from, ItemPlace to, Item item)
        {
            if (!item.IsBook || !(to.IsBar || to.IsCell) || !(from.IsBar || from.IsCell)) return false;

            Item? there = At(to);
            if (there is { IsBook: false } && from.IsBar) return false;
            Put(to, item);
            Put(from, there);
            return true;
        }

        private void Put(ItemPlace place, Item? item)
        {
            if (place.IsHeld) At(place.Holder)!.Held![place.Index] = item;
            else if (place.IsBar) Bar[place.BarSlot] = item;
            else Inventory[place.Number] = item;
        }

        // Into a book or a page, out of one, or from one to another. Whatever's there goes where this came from.
        private bool MoveHeld(ItemPlace from, ItemPlace to, Item item)
        {
            if (!CanMoveHeld(from, to, item)) return false;
            Item? there = At(to);
            Put(to, item);
            Put(from, there);
            return true;
        }

        private bool CanMoveHeld(ItemPlace from, ItemPlace to, Item item)
        {
            Item? there = At(to);
            if (!CanGo(to, item, from, there) || there != null && !CanGo(from, there, to, item)) return false;

            // Each book this touches has to read afterwards. The other end of the move isn't there yet, so it's asked what would be.
            return Reads(to, item, from, there) && Reads(from, there, to, item);
        }

        // Whether something can be put in a place at all, whatever else is around it.
        private bool CanGo(ItemPlace place, Item item, ItemPlace other, Item? leaving)
        {
            if (!place.IsHeld) return place.IsCell || place.IsBar && item.IsBook;
            if (At(place.Holder) is not Item holder || place.Index < 0 || place.Index >= holder.Places) return false;
            if (holder.IsBook) return item.IsPage && (holder.Mode != BookMode.Toggle || item.Page?.Kind == PageKind.Area);
            return holder.IsPage && item.Sticker is Sticker sticker && holder.Page != null && Stickers.GoesOn(sticker, holder.Page.Kind);
        }

        // A book reads to its first Area and no further, so nothing may end up after one.
        private bool Reads(ItemPlace place, Item? item, ItemPlace other, Item? leaving)
        {
            if (!place.IsHeld || At(place.Holder) is not { IsBook: true, Mode: not BookMode.Toggle } book) return true;

            bool ended = false;
            for (int i = 0; i < book.Places; i++)
            {
                Item? page = book.Held![i];
                if (i == place.Index) page = item;
                else if (other.IsHeld && other.Holder == place.Holder && i == other.Index) page = leaving;

                if (page == null) continue;
                if (ended) return false;
                ended = page.Page?.Kind == PageKind.Area;
            }
            return true;
        }

        /// <summary>Whether a move would work, by trying it on a copy, so that what's shown of a move can't disagree with what it does.</summary>
        public bool CanMove(ItemPlace from, ItemPlace to)
        {
            if (from == to || At(from) is not Item item) return false;
            if (from.IsHeld || to.IsHeld) return CanMoveHeld(from, to, item);

            var trial = new HeroState { Inventory = (Item?[])Inventory.Clone(), Equipped = new Dictionary<WornSlot, Item>(Equipped), Bar = (Item?[])Bar.Clone() };
            return trial.Move(from, to);
        }

        /// <summary>The first place on the bar with nothing on it, or -1 if there isn't one.</summary>
        public int FreeOnBar() => Array.IndexOf(Bar, null);

        /// <summary>Every book they have, in the order they'd be shown: what's on the bar and then what's in the pack.</summary>
        public IEnumerable<ItemPlace> BookPlaces()
        {
            for (int slot = 0; slot < BarSlots; slot++)
            {
                if (Bar[slot] != null) yield return ItemPlace.OnBar(slot);
            }
            for (int cell = 0; cell < InventorySize; cell++)
            {
                if (Inventory[cell] is { IsBook: true }) yield return ItemPlace.Cell(cell);
            }
        }

        /// <summary>Calls a book something. Says whether there was a book there and a name to call it.</summary>
        public bool Title(ItemPlace place, string name)
        {
            string title = name.Trim();
            if (title.Length > LongestBookName) title = title.Substring(0, LongestBookName);
            if (At(place) is not { IsBook: true } item || title.Length == 0) return false;
            item.Name = title;
            return true;
        }

        /// <summary>The only thing that rearranges a pack without being told where each thing goes, and it's only ever done when asked: by where things are worn, then the best first, from the first cell on.</summary>
        public void Sort()
        {
            Item[] sorted = Carried().OrderBy(item => item.Slot).ThenByDescending(item => item.Rarity).ThenByDescending(item => item.Level).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
            Array.Clear(Inventory);
            sorted.CopyTo(Inventory, 0);
        }

        /// <summary>A plain start: a poor sword and a poor coat. Everyone starts in these, but no two the same ones.</summary>
        public static HeroState CreateNew()
        {
            var hero = new HeroState();
            var looks = new Dino_Engine.World.WorldRandom((ulong)Random.Shared.NextInt64());
            hero.Equipped[WornSlot.MainHand] = new Item { Name = "Rusted Blade", Base = "Rusted Blade", Seed = ItemGenerator.NewSeed(looks.Fork(0)), Slot = ItemSlot.MainHand, Level = 1, WeaponDamage = 7f };
            hero.Equipped[WornSlot.Body] = new Item { Name = "Travelling Tunic", Base = "Leather Tunic", Seed = ItemGenerator.NewSeed(looks.Fork(1)), Slot = ItemSlot.Body, Level = 1, Evasion = 8f };
            hero.Bar[0] = ItemGenerator.Book(ItemGenerator.NewSeed(looks.Fork(2)));
            hero.Offers = NewOffers(looks.Fork(3).NextULong());
            return hero;
        }
    }

    /// <summary>
    /// What someone's hero comes to. Both ends of a connection do this from the same record, so they get the same numbers, which is what lets a
    /// client predict its own hero. Everything is gathered in a fixed order for that reason: the same things have to add up the same way.
    /// </summary>
    public static class HeroBuild
    {
        /// <summary>What every hero has by being a hero: base life and mana, and the walk that the sheet's Movement Speed is a bonus on. Nothing else, at any level.</summary>
        public static IEnumerable<Mod> Innate()
        {
            yield return Flat(Stat.Health, Rulebook.BaseHealth);
            yield return Flat(Stat.Mana, Rulebook.BaseMana);
            yield return Flat(Stat.MoveSpeed, HeroState.BaseMoveSpeed);
        }

        /// <summary>What a hero attacks with: what's in their main hand, both weapons together if they're Dual Wielding, or a weapon alone in the off hand if that's all there is.</summary>
        public static WeaponProfile WeaponOf(HeroState hero)
        {
            Item? main = hero.Equipped.GetValueOrDefault(WornSlot.MainHand);
            Item? off = hero.Equipped.TryGetValue(WornSlot.OffHand, out Item? held) && held.IsOneHandedWeapon ? held : null;
            if (main != null && off != null) return Item.Paired(main.Weapon(), off.Weapon());
            return (main ?? off)?.Weapon() ?? WeaponProfile.Fists;
        }

        private static IEnumerable<Mod> Gather(HeroState hero, IReadOnlyList<ActiveBuff>? buffs, IReadOnlyList<Mod>? land)
        {
            foreach (Mod mod in Gathered(hero, buffs)) yield return mod;

            // What the land does to whoever's on it comes last, in its own order, on both ends.
            if (land == null) yield break;
            foreach (Mod mod in land) yield return mod;
        }

        private static IEnumerable<Mod> Gathered(HeroState hero, IReadOnlyList<ActiveBuff>? buffs)
        {
            foreach (Mod mod in Innate()) yield return mod;
            foreach (Mod mod in TalentTree.Mods(hero)) yield return mod;

            foreach (WornSlot place in Enum.GetValues<WornSlot>())
            {
                if (!hero.Equipped.TryGetValue(place, out Item? item)) continue;
                foreach (Mod mod in item.Mods()) yield return mod;
            }

            // A weapon in the off hand isn't a shield. One in each hand is Dual Wielding, and one in either is not being Unarmed.
            Item? main = hero.Equipped.GetValueOrDefault(WornSlot.MainHand);
            Item? off = hero.Equipped.GetValueOrDefault(WornSlot.OffHand);
            if (off != null && !off.IsOneHandedWeapon) yield return Grant(Condition.HoldingShield);
            if (main == null && off is not { IsOneHandedWeapon: true }) yield return Grant(Condition.Unarmed);
            else if (main is { IsTwoHanded: true }) yield return Grant(Condition.TwoHanded);
            else if (main != null && off is { IsOneHandedWeapon: true }) yield return Grant(Condition.DualWielding);

            if (buffs == null) yield break;
            foreach (ActiveBuff buff in buffs)
            {
                for (int stack = 0; stack < buff.Stacks; stack++)
                {
                    foreach (Mod mod in buff.Buff.Mods) yield return mod;
                }
            }
        }

        /// <summary>Makes a hero's loadout, with what's on the bar in the order it's there.</summary>
        /// <param name="land">What the land they're in puts on every hero: see <see cref="LandTerms.HeroMods"/>.</param>
        public static Loadout Compile(HeroState hero, IReadOnlyList<ActiveBuff>? buffs = null, IReadOnlyList<Mod>? land = null)
        {
            var books = new List<BookPlan>(HeroState.BarSlots);
            foreach (Item? item in hero.Bar)
            {
                if (item?.Written() is Book book) books.Add(Scribe.Read(book, item.Name));
            }
            return new Loadout(hero.Level, WeaponOf(hero), Gather(hero, buffs, land), books);
        }

        /// <summary>The place in a loadout of what's on a slot, or -1 if nothing is.</summary>
        public static int SlotIndex(HeroState hero, int slot)
        {
            if (slot < 0 || slot >= hero.Bar.Length || hero.Bar[slot]?.Written() == null) return -1;

            int index = 0;
            for (int before = 0; before < slot; before++)
            {
                if (hero.Bar[before]?.Written() != null) index++;
            }
            return index;
        }
    }

    /// <summary>A buff in force on someone, and how much of it.</summary>
    public struct ActiveBuff
    {
        public BuffDefinition Buff;
        public int Stacks;
        public float Left;
    }
}
