using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural
{
    public class ModelGenerator
    {
        public static readonly glModel UNIT_SPHERE = glLoader.loadToVAO(IcoSphereGenerator.CreateIcosphere(1, new VertexMaterial(TextureGenerator.flatGlow)));
        public static readonly glModel UNIT_CONE = glLoader.loadToVAO(MeshGenerator.GenerateCone(new VertexMaterial(0)));
        public static readonly glModel UNIT_BOX = glLoader.loadToVAO(MeshGenerator.generateBox(new VertexMaterial(0)));
    }
}
