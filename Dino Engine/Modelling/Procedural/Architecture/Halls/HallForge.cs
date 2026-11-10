using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// Builds a hall's models from its design (<see cref="HallDesigns"/>): its shell, which is the building and is always drawn, and what's inside it, which is drawn only when it can be
    /// seen. Headless, so either can be built on any thread. Read HALLS.md.
    /// </summary>
    public static class HallForge
    {
        /// <param name="chimneys">Where smoke leaves its chimneys, in model space.</param>
        /// <param name="lights">The lights and flames it carries, in model space.</param>
        public static Mesh Build(HallDesign design, bool inside, List<Vector3> chimneys, List<AssetLight> lights)
        {
            var b = new MeshBuilder();
            if (inside) HallInteriors.Build(b, design, lights);
            else HallShells.Build(b, design, chimneys, lights);
            return b.Build();
        }
    }
}
