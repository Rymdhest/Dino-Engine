using Dino_Defenders.Stats;
using Dino_Engine.Modelling.Procedural.Gear;

namespace Dino_Defenders.Progression
{
    /// <summary>What one thing someone has on looks like.</summary>
    public readonly record struct WornLook(WornSlot Place, ulong Seed, uint Traits);

    /// <summary>
    /// What an item looks like, as far as anyone needs telling: its seed, and the few things about it that say what shape that seed grows into. Those go in one integer so that they can
    /// be sent with it, and <see cref="SpecOf"/> turns the two into what the engine makes a mesh from. Nothing in here touches a GPU, as a server has to say what's lying on the ground.
    /// </summary>
    public static class ItemLook
    {
        private const int GradeBits = 8;

        // The top of what a look is grown from, which has a step between rare and unique that nothing stands on. Bringing this down would give every unique a different look.
        public const int UniqueGrade = 4;

        private const int ThemeBits = 11;
        private const int HeavyBits = 14;
        private const int LeatherBits = 18;
        private const int SlotBits = 22;
        private const int KindBits = 26;
        private const int FamilyBit = 29;
        private const float ShareSteps = 15f;

        /// <summary>What goes with the seed. Base numbers aren't saved, as these are only ever said between two copies of one build.</summary>
        public static uint Traits(Item item)
        {
            FamilyShares shares = item.Shares();
            bool family = shares != default;
            return (uint)(ItemGenerator.NumberOf(ItemGenerator.BaseOf(item)) & 0xFF)
                | (uint)(item.Rarity == Rarity.Unique ? UniqueGrade : (int)item.Rarity) << GradeBits
                | (uint)ThemeOf(item) << ThemeBits
                | (uint)MathF.Round(shares.Heavy * ShareSteps) << HeavyBits
                | (uint)MathF.Round(shares.Leather * ShareSteps) << LeatherBits
                | (uint)item.Slot << SlotBits
                | (uint)item.Kind << KindBits
                | (family ? 1u : 0u) << FamilyBit;
        }

        /// <summary>What an item is, for making it.</summary>
        public static GearSpec SpecOf(Item item) => SpecOf(item.LookSeed(), Traits(item));

        /// <summary>What kind of weapon something is, for how it's carried.</summary>
        public static WeaponKind WeaponOf(uint traits) => (WeaponKind)(traits >> KindBits & 7);

        /// <summary>What something is worn as, which tells a second weapon in the off hand from a shield.</summary>
        public static ItemSlot SlotOf(uint traits) => (ItemSlot)(traits >> SlotBits & 15);

        /// <summary>What something is that's only known by what was said about it.</summary>
        public static GearSpec SpecOf(ulong seed, uint traits)
        {
            string baseName = ItemGenerator.NameOf((int)(traits & 0xFF));
            int grade = (int)(traits >> GradeBits & 7);
            var theme = (GearTheme)(traits >> ThemeBits & 7);
            var slot = (ItemSlot)(traits >> SlotBits & 15);
            var kind = (WeaponKind)(traits >> KindBits & 7);

            float heavy = 0f, leather = 0f, cloth = 0f;
            if ((traits >> FamilyBit & 1) != 0)
            {
                heavy = (traits >> HeavyBits & 15) / ShareSteps;
                leather = (traits >> LeatherBits & 15) / ShareSteps;
                cloth = MathF.Max(1f - heavy - leather, 0f);
            }
            return new GearSpec(seed, KindOf(slot, kind), heavy, leather, cloth, Forms.GetValueOrDefault(baseName, GearForm.Middling), grade, theme);
        }

        public static GearKind KindOf(ItemSlot slot, WeaponKind weapon) => slot switch
        {
            ItemSlot.MainHand => weapon switch
            {
                WeaponKind.Axe => GearKind.Axe,
                WeaponKind.Mace => GearKind.Mace,
                WeaponKind.Dagger => GearKind.Dagger,
                WeaponKind.Spear => GearKind.Spear,
                WeaponKind.Bow => GearKind.Bow,
                WeaponKind.Staff => GearKind.Staff,
                WeaponKind.Wand => GearKind.Wand,
                _ => GearKind.Sword,
            },
            ItemSlot.OffHand => GearKind.OffHand,
            ItemSlot.Head => GearKind.Head,
            ItemSlot.Neck => GearKind.Neck,
            ItemSlot.Body => GearKind.Body,
            ItemSlot.Legs => GearKind.Legs,
            ItemSlot.Feet => GearKind.Feet,
            ItemSlot.Hands => GearKind.Hands,
            ItemSlot.Book => GearKind.Book,
            ItemSlot.Page => GearKind.Page,
            ItemSlot.Sticker => GearKind.Sticker,
            ItemSlot.Orders => GearKind.Orders,
            _ => GearKind.Finger,
        };

        /// <summary>What power shows in something: whichever it's most given to, by what it does. So a blade that burns looks like it, whoever made it.</summary>
        public static GearTheme ThemeOf(Item item)
        {
            Span<int> counts = stackalloc int[6];
            foreach (Mod mod in item.Mods())
            {
                if (mod.Stat == Stat.Convert) counts[(int)Of(mod.To)] += 2;
                counts[(int)Of(mod.Stat)] += 2;
                if ((mod.Scope & Tags.Fire) != 0) counts[(int)GearTheme.Fire]++;
                if ((mod.Scope & Tags.Cold) != 0) counts[(int)GearTheme.Cold]++;
                if ((mod.Scope & Tags.Lightning) != 0) counts[(int)GearTheme.Lightning]++;
                if ((mod.Scope & Tags.Magic) != 0) counts[(int)GearTheme.Magic]++;
                if ((mod.Scope & Tags.Poison) != 0) counts[(int)GearTheme.Poison]++;
            }

            var theme = GearTheme.None;
            int most = 0;
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] <= most) continue;
                most = counts[i];
                theme = (GearTheme)i;
            }
            return theme;
        }

        private static GearTheme Of(DamageType type) => type switch
        {
            DamageType.Fire => GearTheme.Fire,
            DamageType.Cold => GearTheme.Cold,
            DamageType.Lightning => GearTheme.Lightning,
            DamageType.Magic => GearTheme.Magic,
            DamageType.Poison => GearTheme.Poison,
            _ => GearTheme.None,
        };

        private static GearTheme Of(Stat stat) => stat switch
        {
            Stat.AddedFire => GearTheme.Fire,
            Stat.AddedCold => GearTheme.Cold,
            Stat.AddedLightning => GearTheme.Lightning,
            Stat.AddedMagic => GearTheme.Magic,
            Stat.AddedPoison => GearTheme.Poison,
            _ => GearTheme.None,
        };

        private static GearForm F(float size, float slender, float curve, float cover, float fancy, float worn) => new(size, slender, curve, cover, fancy, worn);

        /// <summary>
        /// What each base is going for. These are where a look starts from, and not looks: a Stiletto is narrow and a Kris is curved, but no two of either are alike. It's the only hand-written
        /// data in the whole of how gear looks, and there are only numbers in it. Size, slender, curve, cover, fancy, worn. `check` fails if a base has none.
        /// </summary>
        private static readonly Dictionary<string, GearForm> Forms = new()
        {
            ["Rusted Blade"] = F(0.32f, 0.5f, 0.1f, 0.25f, 0f, 0.9f),
            ["Short Sword"] = F(0.2f, 0.45f, 0.08f, 0.35f, 0.15f, 0.25f),
            ["Arming Sword"] = F(0.45f, 0.5f, 0.05f, 0.5f, 0.3f, 0.2f),
            ["Longsword"] = F(0.72f, 0.58f, 0.04f, 0.55f, 0.35f, 0.15f),
            ["War Blade"] = F(0.82f, 0.28f, 0.15f, 0.6f, 0.4f, 0.2f),
            ["Runed Blade"] = F(0.72f, 0.5f, 0.1f, 0.5f, 0.85f, 0.05f),
            ["Knightly Blade"] = F(0.9f, 0.45f, 0.03f, 0.8f, 0.8f, 0.05f),

            ["Hatchet"] = F(0.2f, 0.6f, 0.2f, 0.3f, 0.05f, 0.35f),
            ["Bearded Axe"] = F(0.5f, 0.4f, 0.7f, 0.5f, 0.3f, 0.2f),
            ["Broad Axe"] = F(0.72f, 0.12f, 0.45f, 0.6f, 0.4f, 0.15f),
            ["Reaver Axe"] = F(0.88f, 0.2f, 0.85f, 0.8f, 0.7f, 0.1f),

            ["Maul"] = F(0.35f, 0.3f, 0.05f, 0.3f, 0.05f, 0.4f),
            ["Warhammer"] = F(0.6f, 0.45f, 0.2f, 0.5f, 0.35f, 0.15f),
            ["Great Maul"] = F(0.82f, 0.12f, 0.1f, 0.6f, 0.45f, 0.15f),
            ["Titan's Hammer"] = F(1f, 0.05f, 0.25f, 0.8f, 0.8f, 0.05f),

            ["Shiv"] = F(0.15f, 0.55f, 0.15f, 0.1f, 0f, 0.7f),
            ["Stiletto"] = F(0.6f, 1f, 0f, 0.3f, 0.4f, 0.1f),
            ["Kris"] = F(0.6f, 0.45f, 0.95f, 0.3f, 0.65f, 0.1f),
            ["Assassin's Blade"] = F(0.85f, 0.6f, 0.45f, 0.5f, 0.75f, 0.05f),

            ["Boar Spear"] = F(0.45f, 0.3f, 0f, 0.6f, 0.15f, 0.3f),
            ["Partisan"] = F(0.65f, 0.3f, 0.2f, 0.7f, 0.45f, 0.15f),
            ["Glaive"] = F(0.85f, 0.2f, 0.75f, 0.4f, 0.6f, 0.1f),

            ["Short Bow"] = F(0.25f, 0.6f, 0.1f, 0.2f, 0.1f, 0.3f),
            ["Hunting Bow"] = F(0.5f, 0.55f, 0.25f, 0.3f, 0.3f, 0.2f),
            ["Yew Longbow"] = F(0.95f, 0.75f, 0.1f, 0.2f, 0.4f, 0.15f),
            ["Horn Recurve"] = F(0.6f, 0.4f, 0.95f, 0.5f, 0.75f, 0.1f),

            ["Walking Staff"] = F(0.5f, 0.7f, 0.1f, 0.15f, 0.05f, 0.45f),
            ["Ash Staff"] = F(0.55f, 0.6f, 0.2f, 0.3f, 0.2f, 0.35f),
            ["Bound Staff"] = F(0.65f, 0.5f, 0.3f, 0.5f, 0.45f, 0.2f),
            ["Runed Staff"] = F(0.85f, 0.4f, 0.2f, 0.7f, 0.9f, 0.05f),

            ["Twig"] = F(0.25f, 0.8f, 0.6f, 0.05f, 0f, 0.5f),
            ["Carved Wand"] = F(0.35f, 0.7f, 0.4f, 0.2f, 0.2f, 0.35f),
            ["Bone Wand"] = F(0.5f, 0.6f, 0.25f, 0.4f, 0.45f, 0.2f),
            ["Sceptre"] = F(0.8f, 0.35f, 0.05f, 0.8f, 0.9f, 0.05f),

            ["Buckler"] = F(0.15f, 0.5f, 0.6f, 0.2f, 0.15f, 0.3f),
            ["Round Shield"] = F(0.45f, 0.4f, 0.5f, 0.45f, 0.25f, 0.3f),
            ["Heater Shield"] = F(0.6f, 0.5f, 0.3f, 0.6f, 0.45f, 0.15f),
            ["Tower Shield"] = F(1f, 0.3f, 0.2f, 0.9f, 0.45f, 0.15f),
            ["Wicker Ward"] = F(0.3f, 0.6f, 0.5f, 0.3f, 0.2f, 0.3f),
            ["Runed Ward"] = F(0.5f, 0.5f, 0.5f, 0.5f, 0.9f, 0.05f),
            ["Targe"] = F(0.35f, 0.4f, 0.7f, 0.4f, 0.5f, 0.2f),

            ["Hood"] = F(0.45f, 0.7f, 0.5f, 0.55f, 0.1f, 0.3f),
            ["Leather Cap"] = F(0.35f, 0.5f, 0.3f, 0.3f, 0.25f, 0.2f),
            ["Mail Coif"] = F(0.45f, 0.5f, 0.3f, 0.6f, 0.15f, 0.25f),
            ["Iron Helm"] = F(0.5f, 0.4f, 0.3f, 0.45f, 0.3f, 0.2f),
            ["Great Helm"] = F(0.8f, 0.15f, 0.15f, 1f, 0.5f, 0.15f),
            ["Linen Cowl"] = F(0.4f, 0.7f, 0.5f, 0.45f, 0.1f, 0.3f),
            ["Circlet"] = F(0.2f, 0.9f, 0.3f, 0.05f, 0.7f, 0.05f),
            ["Sage's Coif"] = F(0.6f, 0.65f, 0.6f, 0.4f, 0.75f, 0.1f),

            ["Leather Tunic"] = F(0.35f, 0.6f, 0.2f, 0.3f, 0.1f, 0.3f),
            ["Leather Jerkin"] = F(0.45f, 0.5f, 0.2f, 0.4f, 0.3f, 0.2f),
            ["Hunter's Coat"] = F(0.65f, 0.5f, 0.3f, 0.75f, 0.5f, 0.15f),
            ["Gambeson"] = F(0.4f, 0.55f, 0.2f, 0.45f, 0.1f, 0.3f),
            ["Lamellar Coat"] = F(0.6f, 0.35f, 0.2f, 0.6f, 0.4f, 0.2f),
            ["Plate Harness"] = F(0.85f, 0.15f, 0.15f, 0.8f, 0.6f, 0.1f),
            ["Linen Robe"] = F(0.45f, 0.7f, 0.4f, 0.7f, 0.15f, 0.25f),
            ["Silk Vestments"] = F(0.7f, 0.6f, 0.5f, 1f, 0.8f, 0.05f),
            ["Brigandine"] = F(0.7f, 0.3f, 0.2f, 0.6f, 0.55f, 0.15f),

            ["Hide Trousers"] = F(0.35f, 0.6f, 0.2f, 0.3f, 0.05f, 0.35f),
            ["Leather Breeches"] = F(0.5f, 0.5f, 0.2f, 0.45f, 0.3f, 0.2f),
            ["Ranger's Chaps"] = F(0.6f, 0.5f, 0.2f, 0.65f, 0.5f, 0.15f),
            ["Mail Chausses"] = F(0.45f, 0.45f, 0.2f, 0.5f, 0.15f, 0.25f),
            ["Iron Cuisses"] = F(0.6f, 0.3f, 0.2f, 0.6f, 0.35f, 0.2f),
            ["Plate Legguards"] = F(0.85f, 0.15f, 0.15f, 0.9f, 0.55f, 0.1f),
            ["Linen Trousers"] = F(0.4f, 0.7f, 0.3f, 0.3f, 0.1f, 0.3f),
            ["Silk Leggings"] = F(0.6f, 0.75f, 0.3f, 0.5f, 0.7f, 0.05f),

            ["Sandals"] = F(0.2f, 0.8f, 0.2f, 0.05f, 0.1f, 0.35f),
            ["Leather Boots"] = F(0.5f, 0.5f, 0.2f, 0.55f, 0.3f, 0.2f),
            ["Mail Sabatons"] = F(0.45f, 0.45f, 0.2f, 0.45f, 0.15f, 0.25f),
            ["Iron Greaves"] = F(0.75f, 0.2f, 0.2f, 0.85f, 0.4f, 0.15f),
            ["Linen Wraps"] = F(0.25f, 0.75f, 0.2f, 0.3f, 0.05f, 0.35f),
            ["Silk Slippers"] = F(0.3f, 0.8f, 0.7f, 0.15f, 0.7f, 0.05f),

            ["Leather Gloves"] = F(0.35f, 0.6f, 0.2f, 0.3f, 0.2f, 0.25f),
            ["Archer's Bracers"] = F(0.5f, 0.5f, 0.2f, 0.75f, 0.45f, 0.15f),
            ["Mail Mitts"] = F(0.45f, 0.45f, 0.2f, 0.45f, 0.15f, 0.25f),
            ["Iron Gauntlets"] = F(0.65f, 0.3f, 0.2f, 0.6f, 0.35f, 0.2f),
            ["Plate Gauntlets"] = F(0.85f, 0.15f, 0.15f, 0.85f, 0.55f, 0.1f),
            ["Cloth Wraps"] = F(0.25f, 0.75f, 0.2f, 0.45f, 0.05f, 0.35f),
            ["Silk Gloves"] = F(0.35f, 0.8f, 0.2f, 0.6f, 0.7f, 0.05f),

            ["Ring"] = F(0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.1f),
            ["Amulet"] = F(0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.1f),
        };

        /// <summary>Whether a base has been thought about, for `check`.</summary>
        public static bool HasForm(string baseName) => Forms.ContainsKey(baseName);
    }
}
