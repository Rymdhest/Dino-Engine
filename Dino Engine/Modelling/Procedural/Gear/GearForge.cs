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
            }
            return mesh;
        }

        /// <summary>Everything that has a generator yet.</summary>
        public static bool CanMake(GearKind kind) => kind is GearKind.Sword or GearKind.Dagger;
    }
}
