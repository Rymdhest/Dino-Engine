using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural
{
    public class ModelGenerator
    {
        public static readonly glModel UNIT_SPHERE = glLoader.loadToVAO(IcoSphereGenerator.CreateIcosphere(1, Material.ROCK));
    }
}
