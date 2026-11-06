using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// Where something is on a hero, as one number so that a message can say it: a cell of the pack, or a place they wear it. Whatever moves an item says where from and where to with these,
    /// and a number that's neither holds nothing.
    /// </summary>
    public readonly record struct ItemPlace(int Number)
    {
        public static ItemPlace Cell(int cell) => new(cell);
        public static ItemPlace Worn(WornSlot place) => new(HeroState.InventorySize + (int)place);

        public bool IsCell => Number >= 0 && Number < HeroState.InventorySize;
        public bool IsWorn => Number >= HeroState.InventorySize && Enum.IsDefined((WornSlot)(Number - HeroState.InventorySize));
        public WornSlot Slot => (WornSlot)(Number - HeroState.InventorySize);
    }

    /// <summary>The hero as they carry on from world to world. Saved as it stands.</summary>
    public sealed class HeroState
    {
        public const int MaxLevel = 60;
        public const float BaseMoveSpeed = 10f;
        public const int MaxFlasks = 4;
        public const float KillsPerFlask = 15f;
        public const int InventorySize = 36;
        public const int SkillSlots = 7;

        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int Gold { get; set; }
        public int Flasks { get; set; } = 3;
        public float FlaskCharge { get; set; }
        public int WorldsCleared { get; set; }

        /// <summary>The highest peril this hero has come through, which is what says how much they can call down next.</summary>
        public int PerilProven { get; set; }

        /// <summary>The nodes taken on the talent tree, by id, in the order they were taken.</summary>
        public List<string> Talents { get; set; } = new();

        /// <summary>What's in each skill slot, by id. A skill that isn't known at the moment simply sits there doing nothing.</summary>
        public string?[] Bar { get; set; } = NewBar();

        /// <summary>What's carried, cell by cell. A cell is the player's: nothing moves from one unless they say so, or ask for the whole pack to be sorted.</summary>
        public Item?[] Inventory { get; set; } = new Item?[InventorySize];
        public Dictionary<WornSlot, Item> Equipped { get; set; } = new();

        public int ExperienceToNext => (int)(120f * MathF.Pow(Level, 1.7f));

        public const int PointsPerLevel = 2;

        /// <summary>Two a level, and one for each land set free, with three to start out with.</summary>
        public int TalentPoints => 1 + PointsPerLevel * Level + WorldsCleared;

        public int UnspentPoints => Math.Max(TalentPoints - Talents.Count, 0);

        /// <summary>The wind answers those who've set a land free, and nobody before that.</summary>
        public bool HasGuidingWind => WorldsCleared > 0;

        private static string?[] NewBar()
        {
            var bar = new string?[SkillSlots];
            bar[0] = SkillCatalog.Strike.Id;
            return bar;
        }

        /// <summary>Adds experience. Returns how many levels it brought.</summary>
        public int GainExperience(int amount)
        {
            int levels = 0;
            Experience += amount;
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
            if (place.IsCell) return Inventory[place.Number];
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
            if (place.IsCell) Inventory[place.Number] = null;
            else Equipped.Remove(place.Slot);
            return item;
        }

        /// <summary>Where something would go: where it was asked to, if it fits there, or else the first free place for it, or else the first place.</summary>
        public WornSlot PlaceFor(Item item, WornSlot? asked = null)
        {
            IReadOnlyList<WornSlot> places = WornSlots.For(item.Slot);
            if (asked is WornSlot wanted && places.Contains(wanted)) return wanted;
            foreach (WornSlot place in places)
            {
                if (!Equipped.ContainsKey(place)) return place;
            }
            return places[0];
        }

        /// <summary>What wearing something would take off: whatever's in its place, both rings for a ring, and for something that changes how the hands are used, whatever's in the other one.</summary>
        public IEnumerable<Item> WornInPlaceOf(Item item)
        {
            foreach (WornSlot place in WornSlots.For(item.Slot))
            {
                if (Equipped.TryGetValue(place, out Item? worn) && worn != item) yield return worn;
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
            if (!ItemPlace.Cell(cell).IsCell || Inventory[cell] is not Item item) return false;
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

            if (from.IsCell && to.IsCell)
            {
                Inventory[from.Number] = Inventory[to.Number];
                Inventory[to.Number] = item;
                return true;
            }

            if (from.IsCell) return to.IsWorn && WornSlots.For(item.Slot).Contains(to.Slot) && Equip(from.Number, to.Slot);

            if (to.IsCell)
            {
                // Something's in the way: it's as if that had been put on, which it can only be if it fits there.
                if (Inventory[to.Number] is not Item there) return Unequip(from.Slot, to.Number);
                return WornSlots.For(there.Slot).Contains(from.Slot) && Equip(to.Number, from.Slot);
            }

            // From one place on the hero to another, which only a ring has.
            if (!to.IsWorn || !WornSlots.For(item.Slot).Contains(to.Slot)) return false;
            Item? other = Equipped.GetValueOrDefault(to.Slot);
            Equipped[to.Slot] = item;
            if (other != null) Equipped[from.Slot] = other;
            else Equipped.Remove(from.Slot);
            return true;
        }

        /// <summary>Whether a move would work, by trying it on a copy, so that what's shown of a move can't disagree with what it does.</summary>
        public bool CanMove(ItemPlace from, ItemPlace to)
        {
            var trial = new HeroState { Inventory = (Item?[])Inventory.Clone(), Equipped = new Dictionary<WornSlot, Item>(Equipped) };
            return trial.Move(from, to);
        }

        /// <summary>The only thing that rearranges a pack without being told where each thing goes, and it's only ever done when asked: by where things are worn, then the best first, from the first cell on.</summary>
        public void Sort()
        {
            Item[] sorted = Carried().OrderBy(item => item.Slot).ThenByDescending(Loot.GradeOf).ThenByDescending(item => item.Level).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
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

        public static WeaponProfile WeaponOf(HeroState hero)
        {
            return hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? weapon) ? weapon.Weapon() : WeaponProfile.Fists;
        }

        private static IEnumerable<Mod> Gather(HeroState hero, IReadOnlyList<ActiveBuff>? buffs)
        {
            foreach (Mod mod in Innate()) yield return mod;
            foreach (Mod mod in TalentTree.Mods(hero)) yield return mod;

            foreach (WornSlot place in Enum.GetValues<WornSlot>())
            {
                if (!hero.Equipped.TryGetValue(place, out Item? item)) continue;
                foreach (Mod mod in item.Mods()) yield return mod;
            }

            if (hero.Equipped.ContainsKey(WornSlot.OffHand)) yield return Grant(Condition.HoldingShield);
            if (!hero.Equipped.ContainsKey(WornSlot.MainHand)) yield return Grant(Condition.Unarmed);
            else if (hero.Equipped[WornSlot.MainHand].IsTwoHanded) yield return Grant(Condition.TwoHanded);

            if (buffs == null) yield break;
            foreach (ActiveBuff buff in buffs)
            {
                for (int stack = 0; stack < buff.Stacks; stack++)
                {
                    foreach (Mod mod in buff.Buff.Mods) yield return mod;
                }
            }
        }

        /// <summary>Makes a hero's loadout. <paramref name="stolen"/> is what they've mimicked, if they can.</summary>
        public static Loadout Compile(HeroState hero, IReadOnlyList<ActiveBuff>? buffs = null, SkillDefinition? stolen = null)
        {
            var skills = new List<SkillDefinition> { SkillCatalog.Strike };
            if (stolen != null && !skills.Contains(stolen)) skills.Add(stolen);
            return new Loadout(hero.Level, WeaponOf(hero), Gather(hero, buffs), skills);
        }

        /// <summary>The place in a loadout of what's in a slot, or -1 if nothing usable is.</summary>
        public static int SlotIndex(HeroState hero, Loadout loadout, int slot)
        {
            if (slot < 0 || slot >= hero.Bar.Length || hero.Bar[slot] is not string id) return -1;
            SkillDefinition? skill = SkillCatalog.Find(id);
            return skill == null ? -1 : loadout.IndexOf(skill);
        }

        /// <summary>Puts a skill that's newly known into the first empty slot, as most people would.</summary>
        public static void OfferToBar(HeroState hero, SkillDefinition skill)
        {
            foreach (string? held in hero.Bar)
            {
                if (held == skill.Id) return;
            }
            for (int slot = 1; slot < hero.Bar.Length; slot++)
            {
                if (hero.Bar[slot] != null) continue;
                hero.Bar[slot] = skill.Id;
                return;
            }
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
