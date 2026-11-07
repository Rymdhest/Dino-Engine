namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>What something is, as far as its shape goes. The engine knows nothing of what any of it does in a game.</summary>
    public enum GearKind : byte
    {
        Sword,
        Axe,
        Mace,
        Dagger,
        Spear,
        Bow,
        Staff,
        Wand,
        OffHand,
        Head,
        Neck,
        Body,
        Legs,
        Feet,
        Hands,
        Finger,
        Book,
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
    /// What a thing's maker was going for, each from 0 to 1. These are where a look starts from and never a shape in themselves: a Stiletto is Slender and a Kris is Curved, and
    /// every one of either still comes out different. What each means depends on the kind of thing, as what's long on a sword is tall on a shield.
    /// </summary>
    /// <param name="Size">How big it is, for its kind.</param>
    /// <param name="Slender">How narrow, or how light, against how broad and heavy.</param>
    /// <param name="Curve">How far from straight it is.</param>
    /// <param name="Cover">How much of whatever it's on it covers: a guard around a hand, a helm around a face, a hem down the legs.</param>
    /// <param name="Fancy">How much trouble was taken over it.</param>
    /// <param name="Worn">How much it's been through.</param>
    public readonly record struct GearForm(float Size, float Slender, float Curve, float Cover, float Fancy, float Worn)
    {
        public static readonly GearForm Middling = new(0.5f, 0.5f, 0.2f, 0.5f, 0.4f, 0.2f);
    }

    /// <summary>
    /// Everything a look is made from. Two machines with the same spec make the same mesh, byte for byte, so this is what's said over a network and never the mesh. Heavy, Leather and
    /// Cloth are how much of each family it is, adding up to 1 or to nothing. See GEAR.md.
    /// </summary>
    public readonly record struct GearSpec(ulong Seed, GearKind Kind, float Heavy, float Leather, float Cloth, GearForm Form, int Grade, GearTheme Theme);
}
