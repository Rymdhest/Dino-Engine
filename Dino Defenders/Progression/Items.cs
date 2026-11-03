using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.Progression
{
    public enum ItemSlot
    {
        Weapon,
        Shield,
        Head,
        Body,
        Feet,
        Ring,
        Amulet,
    }

    public enum Rarity
    {
        Common,
        Magic,
        Rare,
        Legendary,
    }

    public enum Stat
    {
        Damage,             // Flat, added to the weapon's.
        DamagePercent,
        Armour,
        Health,
        CritChance,         // Percent.
        CritDamage,         // Percent on top of the base.
        AttackSpeed,        // Percent.
        MoveSpeed,          // Percent.
        Regeneration,       // Health per second.
        LifeOnHit,
        GoldFind,           // Percent.
        ItemFind,           // Percent.
    }

    /// <summary>One line of an item: a stat and how much of it.</summary>
    public sealed class Modifier
    {
        public Stat Stat { get; set; }
        public float Value { get; set; }

        public string Describe()
        {
            float v = Value;
            return Stat switch
            {
                Stat.Damage => $"+{v:0} Damage",
                Stat.DamagePercent => $"+{v:0}% Damage",
                Stat.Armour => $"+{v:0} Armour",
                Stat.Health => $"+{v:0} Health",
                Stat.CritChance => $"+{v:0.#}% Critical Chance",
                Stat.CritDamage => $"+{v:0}% Critical Damage",
                Stat.AttackSpeed => $"+{v:0}% Attack Speed",
                Stat.MoveSpeed => $"+{v:0}% Movement Speed",
                Stat.Regeneration => $"+{v:0.#} Health per Second",
                Stat.LifeOnHit => $"+{v:0.#} Health per Hit",
                Stat.GoldFind => $"+{v:0}% Gold Found",
                Stat.ItemFind => $"+{v:0}% Item Rarity",
                _ => "",
            };
        }
    }

    public sealed class Item
    {
        public string Name { get; set; } = "";
        public ItemSlot Slot { get; set; }
        public Rarity Rarity { get; set; }
        public int Level { get; set; }
        public float WeaponDamage { get; set; }
        public float Armour { get; set; }
        public List<Modifier> Modifiers { get; set; } = new();

        public int SellValue => (int)(4f * Level * (1f + (int)Rarity * 1.6f + Modifiers.Count * 0.5f));
        public int BuyValue => SellValue * 4;
    }

    /// <summary>
    /// Rolls items. As in the classic ARPGs, an item's level decides which tiers of each affix can appear on it, its rarity how
    /// many it gets, and the value within a tier is a roll of its own: so the same drop is never quite the same twice, and a
    /// deeper world holds better things without any item being placed by hand.
    /// </summary>
    public static class ItemGenerator
    {
        private sealed record BaseItem(string Name, ItemSlot Slot, int MinLevel, float Damage, float Armour);
        private sealed record Tier(int MinLevel, float Low, float High);
        private sealed record Affix(Stat Stat, bool Prefix, string[] Names, ItemSlot[] Slots, Tier[] Tiers);

        private static readonly BaseItem[] Bases =
        {
            new("Rusted Blade", ItemSlot.Weapon, 1, 7f, 0f),
            new("Short Sword", ItemSlot.Weapon, 2, 10f, 0f),
            new("Arming Sword", ItemSlot.Weapon, 5, 15f, 0f),
            new("Longsword", ItemSlot.Weapon, 9, 22f, 0f),
            new("War Blade", ItemSlot.Weapon, 14, 32f, 0f),
            new("Runed Blade", ItemSlot.Weapon, 20, 46f, 0f),
            new("Buckler", ItemSlot.Shield, 1, 0f, 5f),
            new("Round Shield", ItemSlot.Shield, 4, 0f, 11f),
            new("Heater Shield", ItemSlot.Shield, 10, 0f, 20f),
            new("Tower Shield", ItemSlot.Shield, 17, 0f, 34f),
            new("Hood", ItemSlot.Head, 1, 0f, 3f),
            new("Leather Cap", ItemSlot.Head, 3, 0f, 6f),
            new("Iron Helm", ItemSlot.Head, 8, 0f, 13f),
            new("Great Helm", ItemSlot.Head, 15, 0f, 23f),
            new("Tunic", ItemSlot.Body, 1, 0f, 6f),
            new("Gambeson", ItemSlot.Body, 3, 0f, 11f),
            new("Lamellar Coat", ItemSlot.Body, 8, 0f, 22f),
            new("Plate Harness", ItemSlot.Body, 15, 0f, 38f),
            new("Sandals", ItemSlot.Feet, 1, 0f, 2f),
            new("Leather Boots", ItemSlot.Feet, 4, 0f, 6f),
            new("Iron Greaves", ItemSlot.Feet, 11, 0f, 13f),
            new("Ring", ItemSlot.Ring, 1, 0f, 0f),
            new("Amulet", ItemSlot.Amulet, 1, 0f, 0f),
        };

        private static readonly ItemSlot[] Weapons = { ItemSlot.Weapon };
        private static readonly ItemSlot[] Jewels = { ItemSlot.Ring, ItemSlot.Amulet };
        private static readonly ItemSlot[] Armours = { ItemSlot.Shield, ItemSlot.Head, ItemSlot.Body, ItemSlot.Feet };
        private static readonly ItemSlot[] Everywhere = { ItemSlot.Weapon, ItemSlot.Shield, ItemSlot.Head, ItemSlot.Body, ItemSlot.Feet, ItemSlot.Ring, ItemSlot.Amulet };

        private static readonly Affix[] Affixes =
        {
            new(Stat.DamagePercent, true, new[] { "Sharp", "Keen", "Cruel", "Merciless", "Ruinous" }, Weapons,
                new[] { new Tier(1, 8, 15), new Tier(5, 16, 28), new Tier(10, 29, 45), new Tier(16, 46, 70), new Tier(24, 71, 100) }),
            new(Stat.Damage, true, new[] { "Jagged", "Barbed", "Rending", "Brutal" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet },
                new[] { new Tier(1, 1, 3), new Tier(6, 4, 7), new Tier(12, 8, 13), new Tier(20, 14, 22) }),
            new(Stat.Armour, true, new[] { "Sturdy", "Reinforced", "Fortified", "Unyielding" }, Armours,
                new[] { new Tier(1, 3, 7), new Tier(6, 8, 16), new Tier(12, 17, 30), new Tier(20, 31, 50) }),
            new(Stat.Health, false, new[] { "of the Ox", "of the Bear", "of the Oak", "of the Mountain" }, new[] { ItemSlot.Head, ItemSlot.Body, ItemSlot.Shield, ItemSlot.Ring, ItemSlot.Amulet },
                new[] { new Tier(1, 10, 20), new Tier(6, 21, 40), new Tier(12, 41, 70), new Tier(20, 71, 110) }),
            new(Stat.CritChance, false, new[] { "of Precision", "of the Hawk", "of the Unerring" }, new[] { ItemSlot.Weapon, ItemSlot.Ring, ItemSlot.Amulet },
                new[] { new Tier(2, 1, 3), new Tier(9, 3.5f, 6), new Tier(17, 6.5f, 10) }),
            new(Stat.CritDamage, false, new[] { "of Wounding", "of Slaughter", "of Ruin" }, new[] { ItemSlot.Weapon, ItemSlot.Amulet },
                new[] { new Tier(4, 10, 20), new Tier(11, 21, 35), new Tier(19, 36, 60) }),
            new(Stat.AttackSpeed, false, new[] { "of Haste", "of Fury", "of the Storm" }, new[] { ItemSlot.Weapon, ItemSlot.Ring },
                new[] { new Tier(3, 4, 8), new Tier(10, 9, 14), new Tier(18, 15, 22) }),
            new(Stat.MoveSpeed, false, new[] { "of the Road", "of the Wind", "of the Gale" }, new[] { ItemSlot.Feet },
                new[] { new Tier(1, 4, 8), new Tier(8, 9, 13), new Tier(16, 14, 20) }),
            new(Stat.Regeneration, false, new[] { "of Renewal", "of Vigour", "of the Spring" }, new[] { ItemSlot.Body, ItemSlot.Head, ItemSlot.Ring, ItemSlot.Amulet },
                new[] { new Tier(2, 0.5f, 1.2f), new Tier(9, 1.3f, 2.5f), new Tier(17, 2.6f, 4.5f) }),
            new(Stat.LifeOnHit, false, new[] { "of the Leech", "of the Lamprey" }, new[] { ItemSlot.Weapon, ItemSlot.Ring },
                new[] { new Tier(4, 1, 2.5f), new Tier(13, 3, 6) }),
            new(Stat.GoldFind, true, new[] { "Gilded", "Golden" }, Jewels,
                new[] { new Tier(1, 10, 25), new Tier(10, 26, 50) }),
            new(Stat.ItemFind, true, new[] { "Lucky", "Fated" }, Jewels,
                new[] { new Tier(3, 6, 14), new Tier(13, 15, 28) }),
        };

        private static readonly string[] LegendaryEpithets = { "Mercy", "Oath", "Last Word", "Promise", "Fang", "Vigil", "Grief", "Reckoning", "Lament", "Answer" };

        /// <param name="rarityBonus">Added chance of better rarity, as a share: 0.5 is half as likely again.</param>
        public static Item Roll(int level, WorldRandom random, float rarityBonus = 0f, Rarity? least = null, ItemSlot? slot = null)
        {
            Rarity rarity = RollRarity(random, rarityBonus);
            if (least.HasValue && rarity < least.Value) rarity = least.Value;

            ItemSlot chosen = slot ?? (ItemSlot)random.Int(Enum.GetValues<ItemSlot>().Length);
            BaseItem baseItem = PickBase(chosen, level, random);

            var item = new Item
            {
                Slot = chosen,
                Rarity = rarity,
                Level = level,
                WeaponDamage = baseItem.Damage * (1f + level * 0.03f) * random.Range(0.92f, 1.08f),
                Armour = baseItem.Armour * (1f + level * 0.025f) * random.Range(0.92f, 1.08f),
            };

            int affixes = rarity switch
            {
                Rarity.Common => 0,
                Rarity.Magic => random.Int(1, 2),
                Rarity.Rare => random.Int(3, 4),
                _ => random.Int(4, 5),
            };

            var names = new List<(bool prefix, string text)>();
            var taken = new HashSet<Stat>();
            for (int i = 0; i < affixes; i++)
            {
                Affix? affix = PickAffix(chosen, level, taken, random);
                if (affix == null) break;
                taken.Add(affix.Stat);

                int tier = BestTier(affix, level, random);
                Tier rolled = affix.Tiers[tier];
                float value = random.Range(rolled.Low, rolled.High);
                if (rarity == Rarity.Legendary) value *= 1.25f;
                item.Modifiers.Add(new Modifier { Stat = affix.Stat, Value = value });
                names.Add((affix.Prefix, affix.Names[Math.Min(tier, affix.Names.Length - 1)]));
            }

            item.Name = Name(baseItem.Name, rarity, names, random);
            return item;
        }

        private static Rarity RollRarity(WorldRandom random, float bonus)
        {
            float roll = random.Float() / (1f + bonus);
            if (roll < 0.012f) return Rarity.Legendary;
            if (roll < 0.11f) return Rarity.Rare;
            if (roll < 0.42f) return Rarity.Magic;
            return Rarity.Common;
        }

        private static BaseItem PickBase(ItemSlot slot, int level, WorldRandom random)
        {
            BaseItem? best = null;
            var candidates = new List<BaseItem>();
            foreach (BaseItem item in Bases)
            {
                if (item.Slot != slot) continue;
                if (item.MinLevel <= level) candidates.Add(item);
                best ??= item;
            }
            if (candidates.Count == 0) return best!;

            // Mostly the best a level allows, with an older make turning up now and then.
            return random.Chance(0.7f) ? candidates[^1] : random.Pick(candidates);
        }

        private static Affix? PickAffix(ItemSlot slot, int level, HashSet<Stat> taken, WorldRandom random)
        {
            var options = new List<Affix>();
            foreach (Affix affix in Affixes)
            {
                if (taken.Contains(affix.Stat) || !affix.Slots.Contains(slot)) continue;
                if (affix.Tiers[0].MinLevel <= level) options.Add(affix);
            }
            return options.Count == 0 ? null : random.Pick(options);
        }

        // The top tier a level reaches is the rarest roll; lower tiers stay possible.
        private static int BestTier(Affix affix, int level, WorldRandom random)
        {
            int top = 0;
            for (int i = 0; i < affix.Tiers.Length; i++)
            {
                if (affix.Tiers[i].MinLevel <= level) top = i;
            }
            int tier = top;
            while (tier > 0 && random.Chance(0.4f)) tier--;
            return tier;
        }

        private static string Name(string baseName, Rarity rarity, List<(bool prefix, string text)> names, WorldRandom random)
        {
            if (rarity == Rarity.Legendary)
            {
                string owner = PlaceNames.Root(random.Int(1 << 20), random);
                return $"{owner}'s {random.Pick(LegendaryEpithets)}";
            }

            string prefix = "";
            string suffix = "";
            foreach ((bool isPrefix, string text) in names)
            {
                if (isPrefix && prefix.Length == 0) prefix = text + " ";
                if (!isPrefix && suffix.Length == 0) suffix = " " + text;
            }
            return prefix + baseName + suffix;
        }
    }
}
