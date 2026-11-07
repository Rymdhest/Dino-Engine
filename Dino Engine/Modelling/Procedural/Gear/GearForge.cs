namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Where every piece of gear comes from: a spec goes in and a mesh comes out, the same on every machine, with no GPU and nothing of the game. It's built on worker threads, so
    /// nothing in here may touch anything shared. See GEAR.md.
    /// </summary>
    public static class GearForge
    {
        public static GearMesh Make(in GearSpec spec)
        {
            var mesh = new GearMesh();
            var style = new GearStyle(spec);
            switch (spec.Kind)
            {
                case GearKind.Sword:
                case GearKind.Dagger:
                    BladeGenerator.Build(mesh, style);
                    break;
                case GearKind.Axe:
                    PolearmGenerator.Axe(mesh, style);
                    break;
                case GearKind.Mace:
                    PolearmGenerator.Mace(mesh, style);
                    break;
                case GearKind.Spear:
                    PolearmGenerator.Spear(mesh, style);
                    break;
                case GearKind.Staff:
                    PolearmGenerator.Staff(mesh, style);
                    break;
                case GearKind.Wand:
                    PolearmGenerator.Wand(mesh, style);
                    break;
                case GearKind.Bow:
                    BowGenerator.Build(mesh, style);
                    break;
                case GearKind.OffHand:
                    ShieldGenerator.Build(mesh, style);
                    break;
                case GearKind.Finger:
                    JewelGenerator.Ring(mesh, style);
                    break;
                case GearKind.Neck:
                    JewelGenerator.Amulet(mesh, style);
                    break;
                case GearKind.Head:
                    HeadGenerator.Build(mesh, style);
                    break;
                case GearKind.Body:
                    TorsoGenerator.Build(mesh, style);
                    break;
                case GearKind.Legs:
                    LimbGenerator.Legs(mesh, style);
                    break;
                case GearKind.Feet:
                    LimbGenerator.Feet(mesh, style);
                    break;
                case GearKind.Hands:
                    LimbGenerator.Hands(mesh, style);
                    break;
                case GearKind.Book:
                    BookGenerator.Build(mesh, style);
                    break;
                case GearKind.OpenBook:
                    BookGenerator.Open(mesh, style);
                    break;
            }
            return mesh;
        }
    }
}
