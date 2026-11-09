using Dino_Defenders.Stats;
using static Dino_Defenders.Stats.M;

namespace Dino_Defenders.Talents
{
    /// <summary>The top of the tree: the pure mage in the middle, lightning to one side and cold to the other.</summary>
    public static partial class TalentContent
    {
        // The scholar's way: Spells, Mana, casting and the Books themselves. The widest region, and the one the warrior's is opposite.
        private static Region Arcanum() => new Region
        {
            Id = "arcanum", Name = "The Arcanum", Adjective = "Arcane", Noun = "Magus", Colour = Rgb(160, 104, 236),
            Centre = 0f, Width = 44f, Attribute = Stat.Intellect,
            Lanes = new[]
            {
                Lane("core", null, 0f, 0f, 1150f, 0f,
                    P("spark-of-will", "Spark of Will", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 18f, Tags.Spell), Flat(Stat.Mana, 20f)),
                    P2("studious", "Studious", Flat(Stat.Mana, 12f), Inc(Stat.Mana, 12f), Flat(Stat.ManaRegen, 3f)),
                    P("warding-sign", "Warding Sign", Flat(Stat.Barrier, 15f), Flat(Stat.Barrier, 40f), Inc(Stat.BarrierRecharge, 15f)),
                    P2("scholars-mind", "Scholar's Mind", Flat(Stat.Mana, 12f), Flat(Stat.Intellect, 15f), Inc(Stat.Mana, 8f))),
                Lane("mind", "core", -10f, 1150f, 2350f, -2f,
                    P("focused-mind", "Focused Mind", Inc(Stat.CritChance, 12f, Tags.Spell), Inc(Stat.CritChance, 35f, Tags.Spell), Flat(Stat.CritMultiplier, 15f, Tags.Spell)),
                    P("plain-force", "Plain Force", Inc(Stat.Damage, 10f, Tags.Magic), Inc(Stat.Damage, 25f, Tags.Magic), Chance(Tags.Hex, 6f)),
                    P2("weave-and-warp", "Weave and Warp", Inc(Stat.CastSpeed, 3f), Inc(Stat.CastSpeed, 9f), Inc(Stat.Damage, 12f, Tags.Spell)),
                    P("sorcerers-reserve", "Sorcerer's Reserve", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 1f, Tags.Spell).For(Per.Intellect, 10f, 40f), Flat(Stat.Intellect, 15f)),
                    P4("arcane-precision", "Arcane Precision", Flat(Stat.CritMultiplier, 8f, Tags.Spell), Flat(Stat.CritMultiplier, 30f, Tags.Spell), Inc(Stat.Damage, 20f, Tags.Spell).While(Condition.RecentlyCrit)),
                    P("overflowing-power", "Overflowing Power", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 22f, Tags.Spell), Inc(Stat.AreaOfEffect, 6f))),
                Lane("well", "core", 9f, 1150f, 2250f, 1.5f,
                    P("deep-well", "Deep Well", Inc(Stat.Mana, 6f), Inc(Stat.Mana, 18f), Flat(Stat.ManaRegen, 4f)),
                    P2("clear-thought", "Clear Thought", Flat(Stat.ManaRegen, 1.5f), Flat(Stat.ManaRegen, 6f), Inc(Stat.Recovery, 8f)),
                    P("siphoning-touch", "Siphoning Touch", Flat(Stat.ManaLeech, 0.3f), Flat(Stat.ManaLeech, 1f), Flat(Stat.ManaOnHit, 3f)),
                    P("mana-tide", "Mana Tide", Flat(Stat.ManaOnKill, 4f), Flat(Stat.ManaOnKill, 15f), Inc(Stat.Mana, 10f)),
                    P("reservoir", "Reservoir", Inc(Stat.Mana, 6f), Flat(Stat.Barrier, 1f).For(Per.MaxMana, 10f), Inc(Stat.Mana, 10f)),
                    P2("still-mind", "Still Mind", Flat(Stat.ManaRegen, 1.5f), Flat(Stat.ManaRegen, 5f), Flat(Stat.ManaRegenPercent, 0.3f))),
                Lane("word", "mind", -17f, 2350f, 3400f, -1f,
                    P("incantation", "Incantation", Inc(Stat.CastSpeed, 3f), Inc(Stat.CastSpeed, 10f), Inc(Stat.Potency, 8f, Tags.Opening)),
                    P("resonance", "Resonance", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 25f, Tags.Spell | Tags.Area), Inc(Stat.AreaOfEffect, 8f, Tags.Spell)),
                    P2("silver-tongue", "Silver Tongue", Inc(Stat.BookCost, -4f, Tags.Spell), Inc(Stat.BookCost, -14f, Tags.Spell), Flat(Stat.ManaRegen, 3f)),
                    P("chorus", "Brimming", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 30f, Tags.Spell).While(Condition.FullMana), Inc(Stat.CastSpeed, 6f).While(Condition.FullMana)),
                    P("true-name", "True Name", Inc(Stat.CritChance, 12f, Tags.Spell), Flat(Stat.CritChance, 1.5f, Tags.Spell), Flat(Stat.CritMultiplier, 20f, Tags.Spell)),
                    P4("grand-design", "Grand Design", Inc(Stat.Damage, 10f, Tags.Spell), Inc(Stat.Damage, 25f, Tags.Spell), Inc(Stat.CastSpeed, 6f), Inc(Stat.Mana, 8f))),
                Lane("lore", "mind", -5f, 2350f, 3050f, 0.5f,
                    P("unravelling", "Unravelling", Chance(Tags.Hex, 3f), Chance(Tags.Hex, 12f), Inc(Stat.AilmentEffect, 25f, Tags.Hex)),
                    P("hexweaver", "Hexweaver", Inc(Stat.AilmentDuration, 10f, Tags.Hex), Inc(Stat.AilmentDuration, 30f, Tags.Hex), Inc(Stat.Damage, 20f).Against(Condition.Hexed)),
                    P2("minds-edge", "Mind's Edge", Inc(Stat.Damage, 10f, Tags.Magic), Flat(Stat.Penetration, 8f, Tags.Magic), Inc(Stat.Damage, 15f, Tags.Magic)),
                    P("arcane-attunement", "Arcane Attunement", Inc(Stat.Damage, 10f, Tags.Magic), GainAs(DamageType.Physical, DamageType.Magic, 10f), Inc(Stat.Damage, 15f, Tags.Magic)),
                    P("witchcraft", "Witchcraft", Chance(Tags.Hex, 3f), Chance(Tags.Hex, 8f), Inc(Stat.Damage, 15f, Tags.Magic))),
                Lane("deep", "well", 5f, 2250f, 3300f, 0f,
                    P("mind-over-flesh", "Mind over Flesh", Flat(Stat.TakenFromMana, 2f), Flat(Stat.TakenFromMana, 8f), Inc(Stat.Mana, 10f)),
                    P("font-of-power", "Font of Power", Flat(Stat.ManaRegenPercent, 0.15f), Flat(Stat.ManaRegenPercent, 1f), Inc(Stat.Mana, 8f)),
                    P2("measured-breath", "Measured Breath", Inc(Stat.Mana, 6f), Inc(Stat.Mana, 15f), Inc(Stat.Recovery, 10f)),
                    P("ward-of-thought", "Ward of Thought", Flat(Stat.Barrier, 15f), Flat(Stat.Barrier, 1f).For(Per.Intellect, 5f), Inc(Stat.BarrierRecharge, 15f)),
                    P("boundless", "Boundless", Inc(Stat.Mana, 6f), Inc(Stat.Mana, 20f), Flat(Stat.ManaRegenPercent, 0.3f)),
                    P("thought-shield", "Thought Shield", Flat(Stat.TakenFromMana, 2f), Flat(Stat.TakenFromMana, 6f), Flat(Stat.ManaRegen, 3f))),
                Lane("script", "well", 16f, 2250f, 2800f, 1f,
                    P("second-reading", "Second Reading", Inc(Stat.Potency, 4f, Tags.Turned), Inc(Stat.Potency, 14f, Tags.Turned), Inc(Stat.BookCost, -6f)),
                    P2("economy-of-words", "Economy of Words", Inc(Stat.BookCost, -4f), Inc(Stat.BookCost, -14f), Inc(Stat.CooldownRecovery, 6f)),
                    P("opening-gambit", "Opening Gambit", Inc(Stat.Potency, 4f, Tags.Opening), Inc(Stat.Potency, 15f, Tags.Opening), Inc(Stat.UseSpeed, 4f)),
                    P2("marginal-notes", "Marginal Notes", Inc(Stat.PageDuration, 6f), Inc(Stat.PageDuration, 20f), Inc(Stat.Potency, 8f, Tags.Zone)),
                    P2("illumination", "Illumination", Inc(Stat.Potency, 4f), Inc(Stat.Potency, 10f), Inc(Stat.BookCost, -5f))),
            },
            Wheels = new[]
            {
                Wheel("wellspring", "The Wellspring", 1650f, -1f,
                    new[] { Inc(Stat.Mana, 7f), Flat(Stat.ManaRegen, 2f) },
                    Gives(Flat(Stat.ManaRegenPercent, 1.2f), Inc(Stat.Mana, 15f), Inc(Stat.Recovery, 10f)),
                    N("bottomless", "Bottomless", Inc(Stat.Mana, 20f), Flat(Stat.Mana, 40f)),
                    N("ebb-and-flow", "Ebb and Flow", Flat(Stat.ManaOnKill, 10f), Flat(Stat.ManaRegen, 5f)),
                    N("lucid", "Lucid", Inc(Stat.Damage, 15f, Tags.Spell).While(Condition.FullMana), Flat(Stat.ManaRegen, 3f))),
                new Wheel
                {
                    Id = "hourglass", Name = "The Hourglass", Radius = 3080f * Breadth, Angle = 15f,
                    Ring = new[] { Inc(Stat.CooldownRecovery, 5f), Inc(Stat.UseSpeed, 3f) },
                    Heart = Key("shorthand", "Shorthand", "Scribe", TalentGlyph.Feather, 0f, 0f, "Half the words, twice the meaning, if you know how to read it.",
                        Rules(Rule.Shorthand), More(Stat.Potency, -25f, Tags.Turned)),
                    Notables = new[]
                    {
                        N("borrowed-seconds", "Borrowed Seconds", Inc(Stat.CooldownRecovery, 15f), Inc(Stat.PageDuration, 12f)),
                        N("swift-recital", "Swift Recital", Inc(Stat.UseSpeed, 8f), Inc(Stat.CastSpeed, 5f)),
                        N("patient-hand", "Patient Hand", Inc(Stat.Potency, 10f), Inc(Stat.CooldownRecovery, 8f)),
                    },
                },
            },
            Keystones = new[]
            {
                Key("manaskin", "Manaskin", "Manaskin", TalentGlyph.Mask, 3430f, 4f, "You stopped being a body some time ago.",
                    Flat(Stat.TakenFromMana, 60f), Inc(Stat.Mana, 50f), Inc(Stat.Health, -30f)),
                Key("echoing-word", "Echoing Word", "Cantor", TalentGlyph.Bell, 3520f, -18f, "Say it again, and they'll believe it.",
                    Rules(Rule.SpellEcho), Inc(Stat.BookCost, 50f, Tags.Spell)),
                Key("palimpsest", "Palimpsest", "Palimpsest", TalentGlyph.Quill, 2570f, 20f, "Written over and over, and only ever paid for once.",
                    Rules(Rule.OneCost), More(Stat.Potency, -20f)),
                Key("inner-ward", "Inner Ward", "Spellwarden", TalentGlyph.Seal, 2060f, -3f, "The ward was never there to stop blows. It was there to be spent.",
                    Rules(Rule.BarrierPays), Inc(Stat.BarrierRecharge, 40f)),
                Key("prism", "Prism", "Prismatic", TalentGlyph.Prism, 3260f, -22f, "One light, broken three ways, and every way of it yours.",
                    Rules(Rule.Prism), Flat(Stat.MaxResistance, -10f, Tags.Elemental)),
            },
        };

        // Lightning, speed and chains: the caller of storms, between the scholar and the poisoner, quick on their feet.
        private static Region Storm() => new Region
        {
            Id = "storm", Name = "The Storm", Adjective = "Storm", Noun = "Caller", Colour = Rgb(112, 184, 255),
            Centre = 37f, Width = 30f, Attribute = Stat.Intellect, Second = Stat.Dexterity,
            Lanes = new[]
            {
                Lane("gale", null, 0f, 0f, 1350f, 0.5f,
                    P2("quickened", "Quickened", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 6f), Inc(Stat.CastSpeed, 6f)),
                    P("static-touch", "Static Touch", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 20f, Tags.Lightning), Chance(Tags.Shock, 5f)),
                    P2("storm-sense", "Storm Sense", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f), Resist(Tags.Lightning, 12f)),
                    P2("charged-air", "Charged Air", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 15f, Tags.Lightning), Inc(Stat.MoveSpeed, 4f))),
                Lane("charge", "gale", -8f, 1350f, 2300f, 1f,
                    P("high-voltage", "High Voltage", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 28f, Tags.Lightning), Flat(Stat.Penetration, 5f, Tags.Lightning)),
                    P("live-wire", "Live Wire", Inc(Stat.CritChance, 12f, Tags.Lightning), Inc(Stat.CritChance, 40f, Tags.Lightning), Flat(Stat.CritMultiplier, 20f, Tags.Lightning)),
                    P2("arc-welder", "Arc Welder", Inc(Stat.Damage, 10f, Tags.Lightning), Convert(DamageType.Physical, DamageType.Lightning, 25f, Tags.Attack), Inc(Stat.Damage, 12f, Tags.Lightning)),
                    P("overcharge", "Overcharge", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 30f, Tags.Lightning).While(Condition.FullMana), Flat(Stat.ManaRegen, 3f)),
                    P("galvanic", "Galvanic", Inc(Stat.Damage, 10f, Tags.Lightning), Flat(Stat.Penetration, 8f, Tags.Lightning), Inc(Stat.Damage, 10f, Tags.Elemental)),
                    P("capacitor", "Capacitor", Flat(Stat.Mana, 12f), Flat(Stat.Mana, 30f), Inc(Stat.Damage, 15f, Tags.Lightning).While(Condition.FullMana))),
                Lane("squall", "gale", 7f, 1350f, 2350f, 0.5f,
                    P("shocking-touch", "Shocking Touch", Chance(Tags.Shock, 3f), Chance(Tags.Shock, 12f), Inc(Stat.AilmentEffect, 25f, Tags.Shock)),
                    P2("arc-flash", "Arc Flash", Inc(Stat.Damage, 10f, Tags.Chaining), Flat(Stat.Chain, 1f), Inc(Stat.Damage, 15f, Tags.Chaining)),
                    P("forked-path", "Forked Path", Inc(Stat.Potency, 4f, Tags.Turned), Inc(Stat.Potency, 12f, Tags.Turned), Inc(Stat.Damage, 12f, Tags.Lightning)),
                    P("lingering-charge", "Lingering Charge", Inc(Stat.AilmentDuration, 10f, Tags.Shock), Inc(Stat.AilmentDuration, 35f, Tags.Shock), Inc(Stat.Damage, 20f).Against(Condition.Shocked)),
                    P4("storm-front", "Storm Front", Inc(Stat.AreaOfEffect, 5f), Inc(Stat.AreaOfEffect, 15f, Tags.Lightning), Inc(Stat.Damage, 15f, Tags.Lightning | Tags.Area)),
                    P("sky-splitter", "Sky Splitter", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 20f, Tags.Lightning | Tags.Spell), Inc(Stat.CastSpeed, 5f))),
                Lane("thunder", "charge", -6f, 2300f, 3250f, -0.5f,
                    P("thundercrack", "Thundercrack", Flat(Stat.CritMultiplier, 8f, Tags.Lightning), Flat(Stat.CritMultiplier, 35f, Tags.Lightning), Inc(Stat.CritChance, 20f, Tags.Lightning)),
                    P2("tailwind", "Tailwind", Inc(Stat.MoveSpeed, 2f), Inc(Stat.MoveSpeed, 8f), Inc(Stat.DodgeCooldown, -12f)),
                    P("rising-storm", "Rising Storm", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 30f, Tags.Lightning).While(Condition.HighLand), Inc(Stat.CastSpeed, 8f).While(Condition.HighLand)),
                    P("conductor", "Conductor", Inc(Stat.ReactionDamage, 10f), Inc(Stat.ReactionDamage, 30f), Inc(Stat.ReactionArea, 20f)),
                    P("eye-of-the-storm", "Eye of the Storm", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 25f).While(Condition.Moving), Inc(Stat.Damage, 15f).While(Condition.Moving))),
                Lane("tempest", "charge", -1f, 2300f, 3300f, 0f,
                    P("maelstrom", "Maelstrom", Inc(Stat.AreaOfEffect, 5f), Inc(Stat.AreaOfEffect, 15f), Inc(Stat.Damage, 15f, Tags.Lightning | Tags.Area)),
                    P("static-field", "Static Field", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 25f, Tags.Lightning).Against(Condition.Shocked), Chance(Tags.Shock, 6f)),
                    P2("storm-born", "Storm Born", Resist(Tags.Lightning, 6f), Resist(Tags.Lightning, 15f), Flat(Stat.MaxResistance, 2f, Tags.Lightning)),
                    P("lightning-reflexes", "Lightning Reflexes", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f), Inc(Stat.DodgeCooldown, -10f)),
                    P("arc-light", "Arc Light", Inc(Stat.Damage, 10f, Tags.Chaining), Flat(Stat.Chain, 1f), Inc(Stat.Damage, 15f, Tags.Chaining))),
                Lane("skyfall", "squall", 10f, 2350f, 3380f, 1.2f,
                    P("skyfire", "Skyfire", Inc(Stat.Potency, 4f, Tags.Falling), Inc(Stat.Potency, 15f, Tags.Falling), Inc(Stat.Damage, 12f, Tags.Lightning)),
                    P("downpour", "Downpour", Inc(Stat.Damage, 10f, Tags.Lightning), Inc(Stat.Damage, 30f, Tags.Lightning).While(Condition.InRain), Inc(Stat.MoveSpeed, 6f).While(Condition.InRain)),
                    P2("grounding", "Grounding", Resist(Tags.Lightning, 6f), Resist(Tags.Lightning, 20f), TakenAs(DamageType.Fire, DamageType.Lightning, 20f)),
                    P("chain-reaction", "Chain Reaction", Inc(Stat.Damage, 10f, Tags.Chaining), Flat(Stat.Chain, 1f), Inc(Stat.Potency, 8f, Tags.Turned)),
                    P("voltaic-surge", "Voltaic Surge", Inc(Stat.CastSpeed, 3f), Inc(Stat.CastSpeed, 10f).While(Condition.RecentlyKilled), Inc(Stat.Damage, 15f, Tags.Lightning))),
            },
            Wheels = new[]
            {
                Wheel("thunderhead", "The Thunderhead", 1820f, 36f,
                    new[] { Flat(Stat.Penetration, 2f, Tags.Lightning), Inc(Stat.Damage, 8f, Tags.Lightning) },
                    Gives(Flat(Stat.Penetration, 12f, Tags.Lightning), Inc(Stat.Damage, 25f, Tags.Lightning), Chance(Tags.Shock, 8f)),
                    N("ionised", "Ionised", Inc(Stat.AilmentEffect, 30f, Tags.Shock), Chance(Tags.Shock, 8f)),
                    N("storm-conduit", "Storm Conduit", GainAs(DamageType.Physical, DamageType.Lightning, 12f), Inc(Stat.Damage, 10f, Tags.Lightning)),
                    N("static-shield", "Static Shield", Resist(Tags.Lightning, 15f), Flat(Stat.MaxResistance, 3f, Tags.Lightning))),
                Wheel("gale", "The Gale", 2860f, 42f,
                    new[] { Inc(Stat.MoveSpeed, 2f), Inc(Stat.CastSpeed, 3f) },
                    Gives(Inc(Stat.MoveSpeed, 8f), Inc(Stat.CastSpeed, 8f), Inc(Stat.UseSpeed, 4f)),
                    N("windborne", "Windborne", Inc(Stat.MoveSpeed, 10f), Inc(Stat.DodgeSpeed, 15f)),
                    N("quicksilver", "Quicksilver", Inc(Stat.CastSpeed, 8f), Inc(Stat.AttackSpeed, 8f)),
                    N("second-gust", "Second Gust", Inc(Stat.DodgeCooldown, -15f), Inc(Stat.MoveSpeed, 5f))),
            },
            Keystones = new[]
            {
                Key("static-stride", "Static Stride", "Stormrunner", TalentGlyph.Comet, 2700f, 39f, "The world's a long carpet, and you're wearing socks.",
                    Rules(Rule.StaticCharge), Inc(Stat.MoveSpeed, 10f), More(Stat.Damage, -20f).While(Condition.Stationary)),
                Key("forked-sky", "Forked Sky", null, TalentGlyph.Fork, 3520f, 48f, "It finds a way, and it keeps finding them.",
                    Flat(Stat.Chain, 2f), Rules(Rule.ChainsGrow), More(Stat.Damage, -25f, Tags.Chaining)),
                Key("lightning-rod", "Lightning Rod", "Lightning Rod", TalentGlyph.Tower, 3400f, 30f, "You'd be surprised what you can get used to.",
                    Rules(Rule.AlwaysShocked), More(Stat.Damage, 40f, Tags.Lightning)),
            },
        };

        // Cold, stillness and the ward: the rimewalker freezes the world and stands behind a wall of ice, between the scholar and the dead.
        private static Region Winter() => new Region
        {
            Id = "winter", Name = "The Long Winter", Adjective = "Frost", Noun = "Rimewalker", Colour = Rgb(158, 230, 245),
            Centre = 324f, Width = 28f, Attribute = Stat.Intellect,
            Lanes = new[]
            {
                Lane("frost", null, 0f, 0f, 1100f, -0.5f,
                    P("splinters", "Splinters", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 20f, Tags.Cold | Tags.Projectile), Inc(Stat.ProjectileSpeed, 10f)),
                    P2("white-ring", "White Ring", Inc(Stat.AilmentEffect, 6f, Tags.Chill), Inc(Stat.Damage, 20f, Tags.Cold | Tags.Area), Inc(Stat.AreaOfEffect, 8f)),
                    P("rime-shell", "Rime Shell", Flat(Stat.Barrier, 15f), Flat(Stat.Barrier, 40f), Flat(Stat.BarrierDelay, -0.4f)),
                    P2("biting-wind", "Biting Wind", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 15f, Tags.Cold), Inc(Stat.MoveSpeed, 4f))),
                Lane("ice", "frost", -6f, 1100f, 2350f, -1f,
                    P("biting-cold", "Biting Cold", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 28f, Tags.Cold), Flat(Stat.Penetration, 5f, Tags.Cold)),
                    P("permafrost", "Permafrost", Chance(Tags.Freeze, 3f), Chance(Tags.Freeze, 10f), Inc(Stat.AilmentDuration, 25f, Tags.Freeze)),
                    P2("cold-snap", "Cold Snap", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 28f).Against(Condition.Chilled), Inc(Stat.Damage, 20f).Against(Condition.Frozen)),
                    P("ice-lance", "Ice Lance", Inc(Stat.CritChance, 12f, Tags.Cold), Inc(Stat.CritChance, 35f, Tags.Cold), Flat(Stat.CritMultiplier, 15f, Tags.Cold)),
                    P("hoarfrost", "Hoarfrost", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 1f, Tags.Cold).For(Per.Intellect, 10f, 40f), Flat(Stat.Intellect, 12f)),
                    P("rimeheart", "Rimeheart", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 20f, Tags.Cold | Tags.Spell), Inc(Stat.CastSpeed, 4f))),
                Lane("rime", "frost", 7f, 1100f, 2300f, 0.5f,
                    P("frost-ward", "Frost Ward", Flat(Stat.Barrier, 15f), Inc(Stat.Barrier, 20f), Inc(Stat.BarrierRecharge, 15f)),
                    P2("crystal-skin", "Crystal Skin", Inc(Stat.Barrier, 6f), Inc(Stat.Barrier, 20f), Resist(Tags.Cold, 15f)),
                    P("quick-thaw", "Quick Thaw", Flat(Stat.BarrierDelay, -0.15f), Flat(Stat.BarrierDelay, -0.6f), Inc(Stat.BarrierRecharge, 10f)),
                    P("thick-fur", "Thick Fur", Resist(Tags.Cold, 6f), Resist(Tags.Cold, 18f), Rules(Rule.CannotBeChilled)),
                    P4("glacial-shield", "Glacial Shield", Flat(Stat.Barrier, 15f), Inc(Stat.Damage, 15f, Tags.Cold).While(Condition.HasBarrier), Flat(Stat.Barrier, 30f)),
                    P2("winter-coat", "Winter Coat", Resist(Tags.Cold, 6f), Resist(Tags.Cold, 12f), Inc(Stat.Barrier, 10f))),
                Lane("glacier", "ice", -6f, 2350f, 3350f, -0.5f,
                    P("numbing", "Numbing", Inc(Stat.AilmentEffect, 8f, Tags.Chill), Inc(Stat.AilmentEffect, 25f, Tags.Chill), Inc(Stat.AilmentDuration, 25f, Tags.Chill)),
                    P2("long-ice", "Long Ice", Chance(Tags.Freeze, 3f), Chance(Tags.Freeze, 8f), Inc(Stat.Damage, 15f, Tags.Cold).Against(Condition.Chilled)),
                    P("snowbound", "Snowbound", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 25f, Tags.Cold).While(Condition.ColdLand), Inc(Stat.Barrier, 20f).While(Condition.ColdLand)),
                    P("shatter", "Shatter", Inc(Stat.ReactionDamage, 10f), Inc(Stat.ReactionDamage, 30f), Inc(Stat.Damage, 20f, Tags.Physical).Against(Condition.Frozen)),
                    P("avalanche", "Avalanche", Inc(Stat.AreaOfEffect, 5f, Tags.Cold), Inc(Stat.AreaOfEffect, 15f, Tags.Cold), Inc(Stat.Damage, 15f, Tags.Cold | Tags.Area))),
                Lane("aurora", "rime", 5f, 2300f, 3300f, 0.5f,
                    P2("snowblind", "Snowblind", Inc(Stat.Evasion, 10f), Inc(Stat.Evasion, 20f), Inc(Stat.Damage, 15f, Tags.Cold).While(Condition.InSnow)),
                    P("ageless-ward", "Ageless Ward", Inc(Stat.Barrier, 6f), Inc(Stat.Barrier, 25f), Flat(Stat.BarrierOnKill, 15f)),
                    P("frozen-heart", "Frozen Heart", Inc(Stat.BarrierRecharge, 8f), Inc(Stat.BarrierRecharge, 25f), Flat(Stat.BarrierDelay, -0.4f)),
                    P("northern-lights", "Northern Lights", Inc(Stat.Damage, 10f, Tags.Cold), Inc(Stat.Damage, 15f, Tags.Cold), Inc(Stat.Damage, 15f, Tags.Lightning)),
                    P2("solstice", "Solstice", Inc(Stat.CooldownRecovery, 4f), Inc(Stat.CooldownRecovery, 12f), Inc(Stat.Damage, 15f, Tags.Cold))),
            },
            Wheels = new[]
            {
                Wheel("rime-wheel", "The Rime", 1690f, 322f,
                    new[] { Flat(Stat.Penetration, 2f, Tags.Cold), Inc(Stat.Damage, 8f, Tags.Cold) },
                    Gives(Flat(Stat.Penetration, 12f, Tags.Cold), Inc(Stat.Damage, 25f, Tags.Cold), Flat(Stat.MaxResistance, 2f, Tags.Cold)),
                    N("frostbound", "Frostbound", Convert(DamageType.Physical, DamageType.Cold, 25f), Inc(Stat.Damage, 10f, Tags.Cold)),
                    N("icebound", "Icebound", Resist(Tags.Cold, 15f), Flat(Stat.MaxResistance, 3f, Tags.Cold)),
                    N("brittle", "Brittle", Inc(Stat.Damage, 25f).Against(Condition.Frozen), Chance(Tags.Freeze, 5f))),
                new Wheel
                {
                    Id = "aegis", Name = "The Aegis", Radius = 3000f * Breadth, Angle = 324f,
                    Ring = new[] { Flat(Stat.Barrier, 15f), Inc(Stat.BarrierRecharge, 5f) },
                    Heart = Key("still-waters", "Still Waters", "Wellwarden", TalentGlyph.Pool, 0f, 0f, "Fill the well to the brim, and what spills over is a wall.",
                        Rules(Rule.StillWaters), Inc(Stat.Barrier, 25f)),
                    Notables = new[]
                    {
                        N("unbroken-ward", "Unbroken Ward", Inc(Stat.Barrier, 25f), Flat(Stat.Barrier, 50f)),
                        N("swift-ward", "Swift Ward", Inc(Stat.BarrierRecharge, 25f), Flat(Stat.BarrierDelay, -0.5f)),
                        N("warded-soul", "Warded Soul", Flat(Stat.Barrier, 1f).For(Per.MaxHealth, 10f), Inc(Stat.Barrier, 10f)),
                    },
                },
            },
            Keystones = new[]
            {
                Key("still-point", "Still Point", "Still Point", TalentGlyph.Crystal, 3500f, 319f, "Everything stops. It doesn't stop being owed.",
                    Rules(Rule.FreezeStores), Chance(Tags.Freeze, 15f)),
                Key("deep-winter", "Deep Winter", "Long Winter", TalentGlyph.Glacier, 2620f, 325f, "It doesn't end. You only hope to outlast it.",
                    Rules(Rule.ChillNeverFades), Rules(Rule.CannotBeChilled), Rules(Rule.CannotBeFrozen), Inc(Stat.AilmentEffect, 60f, Tags.Chill), More(Stat.Damage, -20f, Tags.Hit)),
                Key("long-count", "The Long Count", "Timekeeper", TalentGlyph.Sundial, 3420f, 330f, "Patience isn't waiting. It's knowing exactly when.",
                    Rules(Rule.LongCount)),
                Key("equilibrium", "Equilibrium", "Equilibrist", TalentGlyph.Scales, 820f, 309.5f, "Body, mind and ward, each carrying a third, and none of them alone.",
                    Rules(Rule.Equilibrium)),
            },
        };
    }
}
