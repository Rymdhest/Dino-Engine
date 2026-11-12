namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Where every piece of gear comes from: a spec goes in and a mesh comes out, the same on every machine, with no GPU and nothing of the game. It's built on worker threads, so
    /// nothing in here may touch anything shared. Weapons and armour grow from a <see cref="GearLook"/>; books, pages, sentences and orders from the <see cref="GearStyle"/> their looks were made
    /// with. See GEAR.md.
    /// </summary>
    public static class GearForge
    {
        public static GearMesh Make(in GearSpec spec) => Make(spec, null);

        /// <summary>How a piece of armour is made, which its look decides when its name doesn't (<see cref="GearForm.Make"/>): for a lab, and one day for a crafting bench to show.</summary>
        public static GearMake MakeOf(in GearSpec spec) => new ArmourKit(new GearLook(spec)).Make;

        /// <summary>
        /// A necklace as it's worn: its pendant where it hangs at the origin, as always, and its chain along <paramref name="strands"/> rather than round in a loop, laid over whatever's on the chest
        /// (<see cref="Character.Outfit.Necklace"/>).
        /// </summary>
        public static GearMesh Worn(in GearSpec spec, IReadOnlyList<IReadOnlyList<OpenTK.Mathematics.Vector3>> strands, Dictionary<string, int>? costs = null)
        {
            var mesh = new GearMesh { Costs = costs, Parts = costs != null ? new() : null };
            JewelGenerator.Amulet(mesh, new GearLook(spec), strands);
            return mesh;
        }

        /// <summary>The same, counting what each part of it cost in triangles into <paramref name="costs"/> (<see cref="GearMesh.Costs"/>).</summary>
        public static GearMesh Make(in GearSpec spec, Dictionary<string, int>? costs)
        {
            var mesh = new GearMesh { Costs = costs, Parts = costs != null ? new() : null };
            switch (spec.Kind)
            {
                case GearKind.Sword:
                case GearKind.Greatsword:
                case GearKind.Dagger:
                    SwordGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Axe:
                case GearKind.Greataxe:
                    HaftGenerator.Axe(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Hammer:
                case GearKind.Greathammer:
                    HaftGenerator.Hammer(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Spear:
                    HaftGenerator.Spear(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Staff:
                    CasterGenerator.Staff(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Wand:
                    CasterGenerator.Wand(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Bow:
                    BowGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Head:
                    HeadGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Body:
                    TorsoGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Legs:
                    LegGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Feet:
                    FootGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Hands:
                    HandGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.OffHand:
                    ShieldGenerator.Build(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Finger:
                    JewelGenerator.Ring(mesh, new GearLook(spec));
                    return mesh;
                case GearKind.Neck:
                    JewelGenerator.Amulet(mesh, new GearLook(spec));
                    return mesh;
            }

            var style = new GearStyle(spec);
            switch (spec.Kind)
            {
                case GearKind.Book:
                    BookGenerator.Build(mesh, style);
                    break;
                case GearKind.OpenBook:
                    BookGenerator.Open(mesh, style);
                    break;
                case GearKind.Page:
                    BookGenerator.Leaf(mesh, style);
                    break;
                case GearKind.Sentence:
                    BookGenerator.Slip(mesh, style);
                    break;
                case GearKind.Orders:
                    BookGenerator.Orders(mesh, style);
                    break;
            }
            return mesh;
        }
    }
}
