using Dino_Defenders.Progression;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// What the land's people and the Reich's wear, grown from the same pieces as a hero's gear (GEAR.md): a few outfits for each kind of person, each made of the bases a hero could find, every
    /// piece of an outfit in one palette (<see cref="GearTailoring"/>) and from a level that fits who they are. So a band of outlaws or a line of greycoats reads as one at a glance, and nobody is
    /// in a mix of whatever came to hand. Whose each is decides its look (LORE.md): outlaws are turncoats in worn leather with the red rag they're known by, and never in the Reich's colours;
    /// the Reich's own are greycoats in its livery of black iron, brass and oxblood, the same as its beasts' harness; the cults wear their power's colour; villagers wear what they made.
    /// An outfit is fixed by its key, so it's the same on every machine, and where one needs a particular part, a hood or a helm with nothing on top, it's the first seed of its own that grows it.
    /// </summary>
    public static class Uniforms
    {
        /// <summary>Every kind of person dressed here, by the name their model is kept by.</summary>
        public static readonly IReadOnlyList<string> Keys = new[] { "villager0", "villager1", "villager2", "villager3", "villager4", "villager5" }
            .Concat(Enumerable.Range(0, 4).SelectMany(look => new[] { $"bandit{look}False", $"bandit{look}True" }))
            .Concat(Enumerable.Range(0, 4).Select(look => $"cutthroat{look}"))
            .Concat(Enumerable.Range(0, 3).Select(look => $"brute{look}"))
            .Concat(new[] { "Fire", "Cold", "Lightning", "Poison", "Magic" }.SelectMany(power => new[] { $"robed:{power}", $"acolyte:{power}" }))
            .Concat(new[] { "reich:legionary", "reich:marksman", "reich:wrangler", "warlord", "revenant" })
            .ToArray();

        /// <summary>Whether a kind is the Reich's own, and so in its livery.</summary>
        public static bool IsReichs(string key) => key.StartsWith(CharacterFactory.ReichPrefix, StringComparison.Ordinal);

        /// <summary>A person of one of these kinds, dressed, by the name their model is kept by.</summary>
        public static Mesh Make(string key)
        {
            Outfit dressed = Of(key) ?? throw new ArgumentException($"Nobody's dressed as {key}.");
            return Dino_Engine.Modelling.Procedural.Character.Outfit.Dress(dressed.Body, dressed.Worn);
        }

        private readonly record struct Outfit(HumanoidLook Body, List<(GearSpec Spec, float Side)> Worn);

        private static Outfit? Of(string key)
        {
            if (key.StartsWith("villager") && int.TryParse(key.AsSpan(8), out int villager)) return Villager(villager);
            if (key.StartsWith("bandit") && key.Length > 6 && char.IsDigit(key[6])) return Bandit(key[6] - '0', key.EndsWith("True"));
            if (key.StartsWith("cutthroat") && int.TryParse(key.AsSpan(9), out int cutthroat)) return Cutthroat(cutthroat);
            if (key.StartsWith("brute") && int.TryParse(key.AsSpan(5), out int brute)) return Brute(brute);
            if (key.StartsWith("robed:")) return Robed(key.Substring(6));
            if (key.StartsWith("acolyte:")) return Acolyte(key.Substring(8));
            if (key.StartsWith(CharacterFactory.ReichPrefix)) return Reich(key.Substring(CharacterFactory.ReichPrefix.Length));
            if (key == "warlord") return Warlord();
            if (key == "revenant") return Revenant();
            return null;
        }

        // ---- Pieces.

        private static readonly Vector3 Skin = new Vector3(0.62f, 0.44f, 0.32f);

        // What a base's make says of how much of each family it is.
        private static (float Heavy, float Leather, float Cloth) Families(GearMake make) => make switch
        {
            GearMake.Plate or GearMake.Mail or GearMake.Scale or GearMake.Lamellar => (1f, 0f, 0f),
            GearMake.Brigandine => (0.6f, 0.4f, 0f),
            GearMake.Padded => (0.6f, 0f, 0.4f),
            GearMake.Hide or GearMake.Hardened or GearMake.Studded => (0f, 1f, 0f),
            GearMake.Cloth or GearMake.Silk or GearMake.Wraps => (0f, 0f, 1f),
            _ => (0.5f, 0.5f, 0f),
        };

        /// <summary>One piece of an outfit: the base it's made as, in the outfit's palette, from its level, and of the family given for what has none of its own, a weapon or a shield.</summary>
        private sealed class Tailor
        {
            public ulong Seed;
            public float Level;
            public int Grade;
            public GearTailoring Palette;
            public GearTheme Theme = GearTheme.None;
            public (float Heavy, float Leather, float Cloth) Family = (0f, 1f, 0f);
            private int _pieces;

            /// <param name="cut">What's made of the base's form for this outfit, as a robe cut short to a tunic.</param>
            public GearSpec Piece(GearKind kind, string baseName, Func<GearMesh, bool>? wanted = null, Func<GearForm, GearForm>? cut = null)
            {
                GearForm form = ItemLook.FormOf(baseName);
                if (cut != null) form = cut(form);
                (float heavy, float leather, float cloth) = form.Make == GearMake.Any ? Family : Families(form.Make);
                var spec = new GearSpec(Seed * 1000003UL + (ulong)(++_pieces) * 7919UL, kind, heavy, leather, cloth, form, Grade, Theme, Palette, Level);
                return wanted == null ? spec : Grown(spec, wanted);
            }
        }

        /// <summary>What an outfit asked a piece for and never got from any of its seeds, for a lab to say: it's worn as it came, so nobody goes without.</summary>
        public static readonly System.Collections.Concurrent.ConcurrentQueue<string> Unmet = new();

        // The first of a piece's seeds that grows what's wanted of it, from its own: the same on every machine, and a few tries at most.
        private static GearSpec Grown(GearSpec spec, Func<GearMesh, bool> wanted)
        {
            for (int i = 0; i < 48; i++)
            {
                GearSpec tried = spec with { Seed = spec.Seed + (ulong)i * 104729UL };
                if (wanted(GearForge.Make(tried, new Dictionary<string, int>()))) return tried;
            }
            Unmet.Enqueue($"{spec.Kind} {spec.Seed}");
            return spec;
        }

        private static bool Has(GearMesh mesh, string part) => mesh.Parts!.Exists(p => p.Name == part && p.To > p.From);
        private static bool Marked(GearMesh mesh, string prefix) => mesh.Parts!.Exists(p => p.Name.StartsWith(prefix, StringComparison.Ordinal));

        private static Func<GearMesh, bool> Grows(string part) => mesh => Has(mesh, part);
        private static readonly Func<GearMesh, bool> Bare = mesh => !Marked(mesh, "crest ");
        private static readonly Func<GearMesh, bool> Crested = mesh => Marked(mesh, "crest ");
        private static readonly Func<GearMesh, bool> Shouldered = mesh => Has(mesh, "shoulders");
        private static readonly Func<GearMesh, bool> Caped = mesh => Marked(mesh, "cape") || mesh.Parts!.Exists(p => p.Name == "hanging" && p.To - p.From > 200);

        private static Outfit Dress(HumanoidLook body, params (GearSpec Spec, float Side)[] worn) => new Outfit(body, worn.ToList());

        // ---- The land's people.

        // Working people in what they made: undyed and homely colours, worn, hands bare, a straw hat or a hood or neither.
        private static Outfit Villager(int look)
        {
            look = Math.Abs(look) % 6;
            (string dye, string trim, string leather, int head)[] palettes =
            {
                ("oatmeal", "walnut", "tan", 1), ("russet", "undyed", "brown", 2), ("grey wool", "oatmeal", "dark brown", 0),
                ("woad", "undyed", "tan", 1), ("walnut", "oatmeal", "chestnut", 0), ("olive", "walnut", "brown", 2),
            };
            var (dye, trim, leather, head) = palettes[look];
            var t = new Tailor { Seed = 0x7011UL + (ulong)look, Level = 0.08f, Grade = 0, Palette = GearTailoring.Named(dye, trim, leather, "iron", "iron", wear: 0.55f), Family = (0f, 0f, 1f) };
            var random = new Dino_Engine.World.WorldRandom(0x7011).Fork(look);
            var body = new HumanoidLook { Skin = Skin * random.Range(0.8f, 1.1f), HeadColour = new Vector3(0.07f, 0.05f, 0.04f), Bulk = random.Range(0.9f, 1.05f) };
            var worn = new List<(GearSpec, float)>
            {
                // A long robe on some, a tunic of linen cut short on others, and a leather one on a few.
                (look == 2 ? t.Piece(GearKind.Body, "Leather Tunic") : t.Piece(GearKind.Body, "Linen Robe", cut: look % 2 == 1 ? null : form => form with { Cover = 0.28f }), -1f),
                (t.Piece(GearKind.Legs, look % 3 == 1 ? "Hide Trousers" : "Linen Trousers"), -1f),
                (t.Piece(GearKind.Feet, look % 2 == 0 ? "Leather Boots" : "Sandals"), -1f),
            };
            // A straw hat is a cowl cut wide: what's small only ever grows into a hood.
            if (head == 1) worn.Add((t.Piece(GearKind.Head, "Linen Cowl", Grows("kasa"), form => form with { Size = 0.9f }), -1f));
            if (head == 2) worn.Add((t.Piece(GearKind.Head, "Linen Cowl", Grows("hood")), -1f));
            return new Outfit(body, worn);
        }

        // Turncoats who sold the land to the Reich and work its roads: worn leather and homespun, an iron cap or a hood, and the red rag they're all known by. Never the Reich's colours.
        private static readonly string[] OutlawLeathers = { "dark brown", "tobacco", "smoked", "brown" };
        private static readonly string[] OutlawCloths = { "grey wool", "walnut", "olive", "oatmeal" };

        private static Tailor Outlaw(ulong seed, int look, float level) => new Tailor
        {
            Seed = seed,
            Level = level,
            Grade = 0,
            Palette = GearTailoring.Named(OutlawCloths[look % 4], "madder", OutlawLeathers[look % 4], "iron", "iron", wear: 0.5f),
            Family = (0.3f, 0.7f, 0f),
        };

        private static Outfit Bandit(int look, bool archer)
        {
            look = Math.Abs(look) % 4;
            Tailor t = Outlaw(0xB4D17UL + (ulong)look * 2UL + (archer ? 1UL : 0UL), look, 0.24f);
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.08f, 0.06f, 0.05f), Bulk = 1.06f };
            var worn = new List<(GearSpec, float)>
            {
                (t.Piece(GearKind.Body, look % 2 == 0 ? "Leather Tunic" : "Leather Jerkin"), -1f),
                (t.Piece(GearKind.Legs, "Hide Trousers"), -1f),
                (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                (look % 2 == 0 ? t.Piece(GearKind.Head, "Hood", Grows("hood")) : t.Piece(GearKind.Head, "Iron Helm", Bare), -1f),
            };
            if (archer) worn.Add((t.Piece(GearKind.Bow, "Short Bow"), -1f));
            else if (look % 2 == 0) worn.Add((t.Piece(GearKind.Sword, "Short Sword"), -1f));
            else worn.Add((t.Piece(GearKind.Spear, "Hunting Spear"), -1f));
            if (!archer && look == 1) worn.Add((t.Piece(GearKind.OffHand, "Round Shield"), 1f));
            return new Outfit(body, worn);
        }

        // An outlaw who kills for hire: dark, fitted leather, gloves, a hood or a bare head, and a short blade.
        private static Outfit Cutthroat(int look)
        {
            look = Math.Abs(look) % 4;
            Tailor t = Outlaw(0xC077UL + (ulong)look, (look + 2) % 4, 0.34f);
            t.Palette = t.Palette with { Leather = GearTailoring.Named(leather: look % 2 == 0 ? "smoked" : "dark brown").Leather };
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.06f, 0.045f, 0.035f), Bulk = 0.92f };
            var worn = new List<(GearSpec, float)>
            {
                (t.Piece(GearKind.Body, look == 2 ? "Leather Jerkin" : "Leather Tunic"), -1f),
                (t.Piece(GearKind.Legs, "Leather Breeches"), -1f),
                (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                (t.Piece(GearKind.Hands, "Leather Gloves"), -1f),
                (t.Piece(GearKind.Sword, "Short Sword"), -1f),
            };
            if (look % 2 == 0) worn.Add((t.Piece(GearKind.Head, "Hood", Grows("hood")), -1f));
            return new Outfit(body, worn);
        }

        // An outlaw who's big enough to need nothing else: mail and an iron cap on one, leather with something over the shoulders on the next, and a spear on the third.
        private static Outfit Brute(int look)
        {
            look = Math.Abs(look) % 3;
            Tailor t = Outlaw(0xB2073UL + (ulong)look, look + 1, 0.4f);
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.07f, 0.05f, 0.04f), Bulk = 1.34f };
            var worn = new List<(GearSpec, float)>
            {
                (look == 0 ? t.Piece(GearKind.Body, "Brigandine", Shouldered) : t.Piece(GearKind.Body, "Leather Jerkin", Shouldered), -1f),
                (t.Piece(GearKind.Legs, look == 0 ? "Mail Chausses" : "Hide Trousers"), -1f),
                (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                (t.Piece(GearKind.Hands, look == 0 ? "Mail Mitts" : "Leather Gloves"), -1f),
                (look == 2 ? t.Piece(GearKind.Spear, "Winged Spear") : t.Piece(GearKind.Sword, "War Blade"), -1f),
            };
            if (look != 1) worn.Add((t.Piece(GearKind.Head, "Iron Helm", Bare), -1f));
            return new Outfit(body, worn);
        }

        // A cult of one of the land's powers: long robes in the power's colour with a hood, and a staff it burns or freezes through.
        private static (string Dye, string Trim, string Dark, GearTheme Theme) Power(string element) => element switch
        {
            "Fire" => ("madder", "weld", "russet", GearTheme.Fire),
            "Cold" => ("woad", "undyed", "deep woad", GearTheme.Cold),
            "Lightning" => ("weld", "indigo", "ochre", GearTheme.Lightning),
            "Poison" => ("lincoln green", "ochre", "olive", GearTheme.Poison),
            _ => ("purple", "grey wool", "purple", GearTheme.Magic),
        };

        private static Outfit Robed(string element)
        {
            var (dye, trim, _, theme) = Power(element);
            var t = new Tailor { Seed = 0x20BEDUL + (ulong)(int)theme, Level = 0.5f, Grade = 2, Theme = theme, Palette = GearTailoring.Named(dye, trim, "dark brown", "steel", "steel", wear: 0.15f), Family = (0f, 0f, 1f) };
            var body = new HumanoidLook { Skin = element == "Magic" ? new Vector3(0.36f, 0.35f, 0.34f) : Skin, HeadColour = new Vector3(0.07f, 0.05f, 0.04f), Bulk = 0.96f };
            return Dress(body,
                (t.Piece(GearKind.Body, "Silk Vestments"), -1f),
                (t.Piece(GearKind.Legs, "Silk Leggings"), -1f),
                (t.Piece(GearKind.Feet, "Silk Slippers"), -1f),
                (t.Piece(GearKind.Head, "Linen Cowl", Grows("hood")), -1f),
                (t.Piece(GearKind.Staff, "Bound Staff"), -1f));
        }

        // The cult's young ones, in its colour gone dark, a shorter robe, and a blade rather than a staff.
        private static Outfit Acolyte(string element)
        {
            var (_, trim, dark, theme) = Power(element);
            var t = new Tailor { Seed = 0xAC01UL + (ulong)(int)theme, Level = 0.34f, Grade = 1, Theme = theme, Palette = GearTailoring.Named(dark, trim, "dark brown", "iron", "iron", wear: 0.3f), Family = (0f, 0f, 1f) };
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.07f, 0.05f, 0.04f), Bulk = 0.94f };
            return Dress(body,
                (t.Piece(GearKind.Body, "Linen Robe"), -1f),
                (t.Piece(GearKind.Legs, "Linen Trousers"), -1f),
                (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                (t.Piece(GearKind.Head, "Linen Cowl", Grows("hood")), -1f),
                (t.Piece(GearKind.Sword, "Arming Sword"), -1f));
        }

        // ---- The Reich's own.

        // Greycoats: the Reich's livery on everything, black iron over a charcoal coat with brass at its fittings and oxblood at its edges, its helms plain, as a soldier's is. A wrangler's
        // leather is the oxblood its beasts' straps are.
        private static Outfit Reich(string who)
        {
            var livery = new GearTailoring(Wear: (byte)2, Reich: true);
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.06f, 0.05f, 0.04f) };
            switch (who)
            {
                case "wrangler":
                {
                    var t = new Tailor { Seed = 0xE1C4UL + 1UL, Level = 0.5f, Grade = 1, Palette = livery with { Leather = 1 }, Family = (0f, 1f, 0f) };
                    body.Bulk = 1.22f;
                    return Dress(body,
                        (t.Piece(GearKind.Body, "Leather Jerkin", Shouldered), -1f),
                        (t.Piece(GearKind.Legs, "Ranger's Chaps"), -1f),
                        (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                        (t.Piece(GearKind.Hands, "Archer's Bracers"), -1f),
                        (t.Piece(GearKind.Head, "Hood", Grows("hood")), -1f),
                        (t.Piece(GearKind.Spear, "Winged Spear"), -1f));
                }
                case "marksman":
                {
                    var t = new Tailor { Seed = 0xE1C4UL + 2UL, Level = 0.5f, Grade = 1, Palette = livery, Family = (0.5f, 0f, 0.5f) };
                    return Dress(body,
                        (t.Piece(GearKind.Body, "Gambeson"), -1f),
                        (t.Piece(GearKind.Legs, "Mail Chausses"), -1f),
                        (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                        (t.Piece(GearKind.Hands, "Leather Gloves"), -1f),
                        (t.Piece(GearKind.Head, "Iron Helm", Bare), -1f),
                        (t.Piece(GearKind.Bow, "Hunting Bow"), -1f));
                }
                default:
                {
                    var t = new Tailor { Seed = 0xE1C4UL, Level = 0.56f, Grade = 1, Palette = livery, Family = (1f, 0f, 0f) };
                    body.Bulk = 1.06f;
                    return Dress(body,
                        (t.Piece(GearKind.Body, "Brigandine", Shouldered), -1f),
                        // Black iron over the thighs and marching boots below, as a line of them costs what a crowd can.
                        (t.Piece(GearKind.Legs, "Iron Cuisses"), -1f),
                        (t.Piece(GearKind.Feet, "Leather Boots"), -1f),
                        (t.Piece(GearKind.Hands, "Mail Mitts"), -1f),
                        (t.Piece(GearKind.Head, "Iron Helm", Bare), -1f),
                        (t.Piece(GearKind.Spear, "Winged Spear"), -1f));
                }
            }
        }

        // ---- Those who hold nothing now.

        // The warlord who used to hold every land: an outlaw lord in good plate, a cape and a crest in the outlaws' red, and gold, never brass.
        private static Outfit Warlord()
        {
            var t = new Tailor { Seed = 0x3A210DUL, Level = 0.86f, Grade = 3, Palette = GearTailoring.Named("madder", "weld", "dark brown", "steel", "gold", wear: 0.1f), Family = (1f, 0f, 0f) };
            var body = new HumanoidLook { Skin = Skin, HeadColour = new Vector3(0.08f, 0.06f, 0.05f), Bulk = 1.2f };
            return Dress(body,
                (t.Piece(GearKind.Body, "Plate Harness", Caped), -1f),
                (t.Piece(GearKind.Legs, "Plate Legguards"), -1f),
                (t.Piece(GearKind.Feet, "Iron Greaves"), -1f),
                (t.Piece(GearKind.Hands, "Plate Gauntlets"), -1f),
                (t.Piece(GearKind.Head, "Iron Helm", Crested), -1f),
                (t.Piece(GearKind.Sword, "Knightly Blade"), -1f),
                (t.Piece(GearKind.OffHand, "Heater Shield"), 1f));
        }

        // A grave knight the Ossuary's digging stirred: once fine harness, rusted and tattered, a shield and a sword gone dull.
        private static Outfit Revenant()
        {
            var t = new Tailor { Seed = 0x6E7EUL, Level = 0.62f, Grade = 1, Palette = GearTailoring.Named("grey wool", "walnut", "dark brown", "iron", "iron", wear: 0.95f), Family = (1f, 0f, 0f) };
            var body = new HumanoidLook { Skin = new Vector3(0.33f, 0.32f, 0.3f), HeadColour = new Vector3(0.1f, 0.1f, 0.1f), Bulk = 1.14f };
            return Dress(body,
                (t.Piece(GearKind.Body, "Plate Harness"), -1f),
                (t.Piece(GearKind.Legs, "Mail Chausses"), -1f),
                (t.Piece(GearKind.Feet, "Iron Greaves"), -1f),
                (t.Piece(GearKind.Hands, "Iron Gauntlets"), -1f),
                (t.Piece(GearKind.Head, "Great Helm"), -1f),
                (t.Piece(GearKind.Sword, "Arming Sword"), -1f),
                (t.Piece(GearKind.OffHand, "Heater Shield"), 1f));
        }
    }
}
