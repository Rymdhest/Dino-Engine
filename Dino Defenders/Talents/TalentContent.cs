using Dino_Defenders.Stats;
using OpenTK.Mathematics;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>
    /// What the tree is made of. The warrior's ways are at the bottom and the mage's at the top, opposite each other; between them on the left are those
    /// who are both, with fire and the dead, and on the right those who are quick, with bows, poison, the land and knives. Going round from one to the
    /// next, each way of fighting shades into the next, so a hero can go anywhere and be anything in between.
    ///
    /// Small nodes give exactly one thing, and the same thing on every small node of a cluster. Travel nodes give a little of the land's attribute and
    /// nothing more. Only notables give several things, and only keystones have "more" and "less". Ids are saved in heroes: a cluster's id and a
    /// lane's id stay, whatever their names and numbers become.
    /// </summary>
    public static partial class TalentContent
    {
        /// <summary>How far out everything below is from where it's written: distances are written for a smaller tree, and this spreads them, so that everything moves out together and keeps its place.</summary>
        public const float Breadth = 1.12f;

        public static readonly Region[] Regions =
        {
            Arcanum(), Storm(), Mire(), Hunt(), Wilds(), Knave(), Forge(), Bulwark(), Marrow(), Pyre(), Host(), Winter(),
        };

        /// <summary>The highways: two rings all the way round, four spokes from the middle out to the outer one, and three far paths part of the way round the edge, where most keystones are.</summary>
        public static readonly Road[] Roads =
        {
            new Road { Id = "old-road", Name = "The Old Road", Ring = true, Radius = 1250f * Breadth },
            new Road { Id = "long-road", Name = "The Long Road", Ring = true, Radius = 2400f * Breadth },
            new Road { Id = "far-path-dawn", Name = "The Far Path", Ring = true, Radius = 3250f * Breadth, ArcFrom = 18f, ArcTo = 104f },
            new Road { Id = "far-path-iron", Name = "The Far Path", Ring = true, Radius = 3280f * Breadth, ArcFrom = 168f, ArcTo = 243f },
            new Road { Id = "far-path-ash", Name = "The Far Path", Ring = true, Radius = 3310f * Breadth, ArcFrom = 258f, ArcTo = 333f },
            new Road { Id = "dawn-way", Name = "The Dawn Way", Angle = 22f, From = TalentTree.StartRing, To = 2410f * Breadth },
            new Road { Id = "sun-way", Name = "The Sun Way", Angle = 112f, From = TalentTree.StartRing, To = 2410f * Breadth },
            new Road { Id = "dusk-way", Name = "The Dusk Way", Angle = 196f, From = TalentTree.StartRing, To = 2410f * Breadth },
            new Road { Id = "night-way", Name = "The Night Way", Angle = 280f, From = TalentTree.StartRing, To = 2410f * Breadth },
        };

        /// <summary>Three pairs of waygates, each joining two ways of fighting that are nearly opposite: the smith and the scholar, the archer and the pyromancer, the knave and the shepherd of the dead.</summary>
        public static readonly Gate[] Gates =
        {
            new Gate { Id = "anvil-and-quill", RadiusA = 3560f * Breadth, AngleA = 181f, RadiusB = 3540f * Breadth, AngleB = 353f },
            new Gate { Id = "arrow-and-ember", RadiusA = 3520f * Breadth, AngleA = 95f, RadiusB = 3560f * Breadth, AngleB = 266f },
            new Gate { Id = "shadow-and-bone", RadiusA = 3480f * Breadth, AngleA = 150f, RadiusB = 3520f * Breadth, AngleB = 296f },
        };

        /// <summary>Where neighbouring regions meet without a road between them, twice each: once near the middle and once further out.</summary>
        public static readonly Bridge[] Bridges =
        {
            B("storm-mire-in", "Toxic Current", 1, 880f, Inc(Stat.Damage, 5f), Inc(Stat.Damage, 15f, Tags.Lightning), Inc(Stat.Damage, 15f, Tags.Poison)),
            B("storm-mire-out", "Galvanic Rot", 1, 1960f, Inc(Stat.Damage, 5f), Inc(Stat.AilmentEffect, 20f, Tags.Shock), Inc(Stat.AilmentDuration, 15f, Tags.Venom)),
            B("mire-hunt-in", "Barbed Shafts", 2, 920f, Inc(Stat.Damage, 5f, Tags.Projectile), Chance(Tags.Venom, 8f, Tags.Projectile), Inc(Stat.ProjectileSpeed, 10f)),
            B("mire-hunt-out", "Fen Stalker", 2, 2000f, Inc(Stat.Evasion, 6f), Inc(Stat.Evasion, 25f).While(Condition.WetLand), Inc(Stat.Damage, 15f, Tags.Poison | Tags.Projectile)),
            B("wilds-knave-in", "Poacher", 4, 1060f, Flat(Stat.Stealth, 2f), Inc(Stat.Damage, 20f).Against(Condition.Unaware), Flat(Stat.Stealth, 6f)),
            B("wilds-knave-out", "Wolf's Patience", 4, 1980f, Inc(Stat.Damage, 5f), Inc(Stat.Damage, 20f).Against(Condition.Isolated), Inc(Stat.MoveSpeed, 5f)),
            B("knave-forge-in", "Dirty Fighting", 5, 900f, Inc(Stat.Damage, 5f, Tags.Melee), Inc(Stat.Damage, 15f, Tags.Melee), Chance(Tags.Bleed, 6f)),
            B("knave-forge-out", "Duelist's Edge", 5, 1940f, Flat(Stat.CritMultiplier, 4f, Tags.Melee), Flat(Stat.CritMultiplier, 18f, Tags.Melee), Inc(Stat.AttackSpeed, 6f)),
            B("bulwark-marrow-in", "Scar Tissue", 7, 880f, Flat(Stat.Health, 8f), Flat(Stat.FlatReduction, 3f), Inc(Stat.Health, 6f)),
            B("bulwark-marrow-out", "Iron Blood", 7, 1960f, Inc(Stat.Armour, 6f), Inc(Stat.Armour, 18f), Flat(Stat.HealthRegenPercent, 0.4f)),
            B("marrow-pyre-in", "Blood and Ash", 8, 920f, Inc(Stat.Damage, 5f), Inc(Stat.Damage, 15f, Tags.Fire), Inc(Stat.Health, 6f)),
            B("marrow-pyre-out", "Boiling Blood", 8, 1990f, Inc(Stat.Damage, 6f, Tags.DamageOverTime), Inc(Stat.Damage, 15f, Tags.Burn), Inc(Stat.Damage, 15f, Tags.Bleed)),
            B("host-winter-in", "Rime Shepherd", 10, 1080f, Inc(Stat.Health, 5f, Tags.Minion), Inc(Stat.Health, 15f, Tags.Minion), Inc(Stat.Barrier, 8f)),
            B("host-winter-out", "Frozen Thralls", 10, 2010f, Inc(Stat.Damage, 5f, Tags.Minion), Inc(Stat.Damage, 20f, Tags.Minion), Inc(Stat.Damage, 12f, Tags.Cold)),
            B("winter-arcanum-in", "Cold Logic", 11, 900f, Inc(Stat.Damage, 5f, Tags.Spell), Inc(Stat.Damage, 12f, Tags.Cold), Inc(Stat.CastSpeed, 4f)),
            B("winter-arcanum-out", "Glacial Mind", 11, 1980f, Flat(Stat.Barrier, 10f), Inc(Stat.Barrier, 12f), Flat(Stat.ManaRegen, 4f)),
        };

        private static Bridge B(string id, string name, int left, float radius, Mod small, params Mod[] mods) => new Bridge { Id = id, Name = name, Left = left, Radius = radius * Breadth, Small = small, Mods = mods };

        private static Pod P(string id, string name, Mod small, params Mod[] mods) => new Pod { Id = id, Name = name, Smalls = 3, Small = small, Mods = mods };
        private static Pod P2(string id, string name, Mod small, params Mod[] mods) => new Pod { Id = id, Name = name, Smalls = 2, Small = small, Mods = mods };
        private static Pod P4(string id, string name, Mod small, params Mod[] mods) => new Pod { Id = id, Name = name, Smalls = 4, Small = small, Mods = mods };

        /// <summary>A notable set into a wheel's rim.</summary>
        private static Pod N(string id, string name, params Mod[] mods) => new Pod { Id = id, Name = name, Smalls = 0, Mods = mods };

        private static Lane Lane(string id, string? from, float angle, float start, float end, float bend, params Pod[] pods) =>
            new Lane { Id = id, From = from, Angle = angle, Start = start * Breadth, End = end * Breadth, Bend = bend / Breadth, Pods = pods };

        private static Keystone Key(string id, string name, string? title, TalentGlyph emblem, float radius, float angle, string lore, params Mod[] mods) =>
            new Keystone { Id = id, Name = name, Title = title, Emblem = emblem, Radius = radius * Breadth, Angle = angle, Lore = lore, Mods = mods };

        private static Wheel Wheel(string id, string name, float radius, float angle, Mod[] ring, Mod[] centre, params Pod[] notables) =>
            new Wheel { Id = id, Name = name, Radius = radius * Breadth, Angle = angle, Ring = ring, Centre = centre, Notables = notables };

        private static Mod[] Gives(params Mod[] mods) => mods;

        private static Vector3 Rgb(int r, int g, int b) => new Vector3(r / 255f, g / 255f, b / 255f);
    }
}
