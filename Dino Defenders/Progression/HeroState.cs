using Dino_Defenders.Skills;
using Dino_Defenders.Stats;
using Dino_Defenders.Talents;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Progression
{
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

        public List<Item> Inventory { get; set; } = new();
        public Dictionary<WornSlot, Item> Equipped { get; set; } = new();

        public int ExperienceToNext => (int)(120f * MathF.Pow(Level, 1.7f));

        /// <summary>Two a level, and one for each land set free, with three to start out with.</summary>
        public int TalentPoints => 1 + 2 * Level + WorldsCleared;

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

        public bool Pickup(Item item)
        {
            if (Inventory.Count >= InventorySize) return false;
            Inventory.Add(item);
            return true;
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

        /// <summary>Puts on something from the pack, and takes off whatever it has to replace. Says whether it could, as all of that has to fit in the pack.</summary>
        public bool Equip(Item item, WornSlot? asked = null)
        {
            WornSlot place = PlaceFor(item, asked);

            // Two hands are two hands.
            WornSlot? displaced = null;
            if (item.IsTwoHanded) displaced = WornSlot.OffHand;
            else if (place == WornSlot.OffHand && Equipped.TryGetValue(WornSlot.MainHand, out Item? held) && held.IsTwoHanded) displaced = WornSlot.MainHand;

            int comingOff = (Equipped.ContainsKey(place) ? 1 : 0) + (displaced is WornSlot other && Equipped.ContainsKey(other) ? 1 : 0);
            if (Inventory.Count - 1 + comingOff > InventorySize) return false;

            Inventory.Remove(item);
            if (Equipped.Remove(place, out Item? old)) Inventory.Add(old);
            if (displaced is WornSlot second && Equipped.Remove(second, out Item? taken)) Inventory.Add(taken);
            Equipped[place] = item;
            return true;
        }

        public void Unequip(WornSlot place)
        {
            if (Inventory.Count >= InventorySize) return;
            if (Equipped.Remove(place, out Item? item)) Inventory.Add(item);
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
        /// <summary>What every hero has by being a hero.</summary>
        public static IEnumerable<Mod> Innate(int level)
        {
            yield return Flat(Stat.Health, 100f + 12f * level);
            yield return Flat(Stat.Mana, 40f + 6f * level);
            yield return Flat(Stat.HealthRegen, 0.8f + 0.05f * level);
            yield return Flat(Stat.ManaRegenPercent, 2.2f);
            yield return Flat(Stat.MoveSpeed, HeroState.BaseMoveSpeed);
            yield return Flat(Stat.Accuracy, 8f * level);
            yield return Flat(Stat.Evasion, 10f + 2f * level);
        }

        public static WeaponProfile WeaponOf(HeroState hero)
        {
            return hero.Equipped.TryGetValue(WornSlot.MainHand, out Item? weapon) ? weapon.Weapon() : WeaponProfile.Fists;
        }

        private static IEnumerable<Mod> Gather(HeroState hero, IReadOnlyList<ActiveBuff>? buffs)
        {
            foreach (Mod mod in Innate(hero.Level)) yield return mod;
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
