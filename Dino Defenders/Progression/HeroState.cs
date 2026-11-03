namespace Dino_Defenders.Progression
{
    /// <summary>What the hero's gear and attributes add up to.</summary>
    public struct HeroStats
    {
        public float MaxHealth;
        public float Damage;
        public float Armour;
        public float CritChance;    // 0..1
        public float CritMultiplier;
        public float AttackSpeed;   // Multiplier.
        public float MoveSpeed;     // Metres per second.
        public float Regeneration;
        public float LifeOnHit;
        public float GoldFind;      // Multiplier.
        public float ItemFind;      // Share added to rarity.
        public float SkillPower;    // Multiplier on the hero's abilities.
    }

    /// <summary>The hero as they carry on from world to world. Saved as it stands.</summary>
    public sealed class HeroState
    {
        public const int MaxLevel = 60;
        public const int BaseMoveSpeed = 10;
        public const int MaxFlasks = 4;
        public const int InventorySize = 36;

        public int Level { get; set; } = 1;
        public int Experience { get; set; }
        public int Gold { get; set; }
        public int Might { get; set; } = 5;
        public int Finesse { get; set; } = 5;
        public int Spirit { get; set; } = 5;
        public int UnspentPoints { get; set; }
        public int Flasks { get; set; } = 3;
        public int WorldsCleared { get; set; }

        public List<Item> Inventory { get; set; } = new();
        public Dictionary<ItemSlot, Item> Equipped { get; set; } = new();

        public int ExperienceToNext => (int)(120f * MathF.Pow(Level, 1.7f));

        public HeroStats Compute()
        {
            float weapon = 5f;
            float flatDamage = 0f;
            float percentDamage = 0f;
            float armour = 0f;
            float health = 0f;
            float crit = 0f;
            float critDamage = 0f;
            float attackSpeed = 0f;
            float moveSpeed = 0f;
            float regeneration = 0f;
            float lifeOnHit = 0f;
            float goldFind = 0f;
            float itemFind = 0f;

            foreach (Item item in Equipped.Values)
            {
                if (item.Slot == ItemSlot.Weapon) weapon = item.WeaponDamage;
                armour += item.Armour;
                foreach (Modifier modifier in item.Modifiers)
                {
                    switch (modifier.Stat)
                    {
                        case Stat.Damage: flatDamage += modifier.Value; break;
                        case Stat.DamagePercent: percentDamage += modifier.Value; break;
                        case Stat.Armour: armour += modifier.Value; break;
                        case Stat.Health: health += modifier.Value; break;
                        case Stat.CritChance: crit += modifier.Value; break;
                        case Stat.CritDamage: critDamage += modifier.Value; break;
                        case Stat.AttackSpeed: attackSpeed += modifier.Value; break;
                        case Stat.MoveSpeed: moveSpeed += modifier.Value; break;
                        case Stat.Regeneration: regeneration += modifier.Value; break;
                        case Stat.LifeOnHit: lifeOnHit += modifier.Value; break;
                        case Stat.GoldFind: goldFind += modifier.Value; break;
                        case Stat.ItemFind: itemFind += modifier.Value; break;
                    }
                }
            }

            return new HeroStats
            {
                MaxHealth = 90f + Level * 12f + Might * 5f + health,
                // Might makes every swing heavier, however good the sword.
                Damage = (weapon + flatDamage) * (1f + percentDamage / 100f) * (1f + Might * 0.02f),
                Armour = armour,
                CritChance = Math.Min(0.05f + Finesse * 0.004f + crit / 100f, 0.75f),
                CritMultiplier = 1.5f + critDamage / 100f,
                AttackSpeed = 1f + Finesse * 0.01f + attackSpeed / 100f,
                MoveSpeed = BaseMoveSpeed * (1f + moveSpeed / 100f),
                Regeneration = 0.4f + Spirit * 0.1f + regeneration,
                LifeOnHit = lifeOnHit,
                GoldFind = 1f + goldFind / 100f,
                ItemFind = itemFind / 100f,
                SkillPower = 1f + Spirit * 0.03f,
            };
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
                UnspentPoints += 3;
                levels++;
            }
            return levels;
        }

        public bool Pickup(Item item)
        {
            if (Inventory.Count >= InventorySize) return false;
            Inventory.Add(item);
            return true;
        }

        public void Equip(Item item)
        {
            Inventory.Remove(item);
            if (Equipped.TryGetValue(item.Slot, out Item? old)) Inventory.Add(old);
            Equipped[item.Slot] = item;
        }

        public void Unequip(ItemSlot slot)
        {
            if (Inventory.Count >= InventorySize) return;
            if (Equipped.Remove(slot, out Item? item)) Inventory.Add(item);
        }

        /// <summary>A plain start: a poor sword and a poor coat.</summary>
        public static HeroState CreateNew()
        {
            var hero = new HeroState();
            hero.Equipped[ItemSlot.Weapon] = new Item { Name = "Rusted Blade", Slot = ItemSlot.Weapon, Level = 1, WeaponDamage = 7f };
            hero.Equipped[ItemSlot.Body] = new Item { Name = "Travelling Tunic", Slot = ItemSlot.Body, Level = 1, Armour = 6f };
            return hero;
        }
    }
}
