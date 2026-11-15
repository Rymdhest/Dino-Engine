namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>What something is, as far as its shape goes. The engine knows nothing of what any of it does in a game.</summary>
    public enum GearKind : byte
    {
        Sword,
        Axe,
        Hammer,
        Dagger,
        Spear,
        Bow,
        Staff,
        Wand,
        Greatsword,     // What takes both hands is a shape of its own: a long grip, and a head or a blade to match it.
        Greataxe,
        Greathammer,
        OffHand,
        Head,
        Neck,
        Body,
        Legs,
        Feet,
        Hands,
        Finger,
        Book,
        OpenBook,
        Page,
        Sentence,
        Orders,     // Sealed orders taken from the Reich: a scroll in oxblood wax. Not worn, not held, only carried and given away.
    }

    /// <summary>What power is in something, which shows in what glows on it and in its accents.</summary>
    public enum GearTheme : byte
    {
        None,
        Fire,
        Cold,
        Lightning,
        Magic,
        Poison,
    }

    /// <summary>
    /// How a piece of armour is made, when its name says so: a Mail Coif is mail and a Gambeson is quilted, whatever else is drawn. Anything that doesn't say is made however its family and its
    /// genes decide (<see cref="Any"/>). It's where a look starts from, as the rest of <see cref="GearForm"/> is, and never a shape.
    /// </summary>
    public enum GearMake : byte
    {
        Any,
        Plate,          // Steel plates, articulated where they have to move.
        Mail,           // Riveted rings.
        Scale,          // Scales laced in rows onto a coat.
        Lamellar,       // Small plates laced together in rows, the laces showing between them.
        Brigandine,     // Plates riveted inside a coat of cloth or leather, their rivets in rows on the outside.
        Padded,         // Cloth quilted over padding.
        Hide,           // Soft leather, fitted and sewn.
        Hardened,       // Leather boiled hard and shaped into plates.
        Studded,        // Leather set with studs.
        Cloth,          // Plain woven cloth, linen or wool.
        Silk,           // Fine cloth, layered and trimmed.
        Wraps,          // Strips of cloth or leather wound round.
    }

    /// <summary>
    /// What a thing's maker was going for, each from 0 to 1. These are where a look starts from and never a shape in themselves: a Stiletto is Slender and a Kris is Curved, and
    /// every one of either still comes out different. What each means depends on the kind of thing, as what's long on a sword is tall on a shield.
    /// </summary>
    /// <param name="Size">How big it is, for its kind.</param>
    /// <param name="Slender">How narrow, or how light, against how broad and heavy.</param>
    /// <param name="Curve">How far from straight it is.</param>
    /// <param name="Cover">How much of whatever it's on it covers: a guard around a hand, a helm around a face, a hem down the legs.</param>
    /// <param name="Fancy">How much trouble was taken over it.</param>
    /// <param name="Worn">How much it's been through.</param>
    /// <param name="Make">How it's made, if its name says.</param>
    public readonly record struct GearForm(float Size, float Slender, float Curve, float Cover, float Fancy, float Worn, GearMake Make = GearMake.Any)
    {
        public static readonly GearForm Middling = new(0.5f, 0.5f, 0.2f, 0.5f, 0.4f, 0.2f);
    }

    /// <summary>
    /// Everything a look is made from. Two machines with the same spec make the same mesh, byte for byte, so this is what's said over a network and never the mesh. Heavy, Leather and
    /// Cloth are how much of each family it is, adding up to 1 or to nothing. See GEAR.md.
    /// </summary>
    /// <param name="Tailoring">What a person has chosen about its look, which nothing does yet: see <see cref="GearTailoring"/>.</param>
    /// <param name="Level">How far up the climb it's from, from the first level at 0 to the last at 1: what it's worth shows (<see cref="GearLook.Power"/>), so that a hero looks more and more
    /// powerful as their gear goes up, and a glance tells someone new from someone who's been far.</param>
    /// <param name="Pages">How many pages a book holds, which is how thick it's bound: one is a thin book and <see cref="BookGenerator.MostPages"/> a really thick one. Nothing else reads it,
    /// and 0 is a book whose room nobody said, which takes its thickness from its genes alone.</param>
    public readonly record struct GearSpec(ulong Seed, GearKind Kind, float Heavy, float Leather, float Cloth, GearForm Form, int Grade, GearTheme Theme, GearTailoring Tailoring = default, float Level = 0.5f, int Pages = 0);
}
