using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Shapes in each of some materials stood in a row in front of the hero (--materials, TEXTURES.md): a block of wall with its corners, a column, a log lying
    /// down and a floor, each mapped at the material's own size. What's round shows how the relief follows a curve, what's square how it meets a corner, and all of
    /// it is lit and seen as the game is. This end's own business: nothing else knows they're there.
    /// </summary>
    internal static class MaterialGallery
    {
        private const float Spacing = 9f;

        public static void Stand(string which, Vector2 hero, Func<float, float, float> groundHeight)
        {
            List<MaterialSpec> materials = which == "all"
                ? MaterialCatalog.All.Where(spec => spec.ReliefMetres > 0f).ToList()
                : which.Split(',').Select(name => MaterialCatalog.All.FirstOrDefault(spec => spec.Name == name.Trim())).Where(spec => spec != null).Cast<MaterialSpec>().ToList();

            // A little away from the camera, which looks along +z, and beside the hero rather than behind them: -x is to the right on screen.
            for (int i = 0; i < materials.Count; i++)
            {
                Vector2 at = new Vector2(hero.X - 4.5f - i * Spacing, hero.Y + 4f);
                glModel model = glLoader.loadToVAO(Shapes(materials[i]));
                Vector3 position = new Vector3(at.X, groundHeight(at.X, at.Y), at.Y);
                Engine.Instance!.world.CreateEntity(
                    new PositionComponent(position),
                    new RotationComponent(Quaternion.Identity),
                    new ScaleComponent(Vector3.One),
                    new ModelComponent(model),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent { value = MyMath.createTransformationMatrix(position, Quaternion.Identity, Vector3.One) });
            }
        }

        private static Mesh Shapes(MaterialSpec spec)
        {
            var b = new MeshBuilder();
            var material = new VertexMaterial(spec.Index, new Colour(255, 255, 255));
            // A block of wall, turned a little so that two of its faces and their corner are in view.
            b.Place(new Vector3(-2.2f, 0f, 0f), 0.5f);
            b.Box(new Vector3(0f, 1.6f, 0f), new Vector3(1.4f, 1.6f, 0.9f), material);
            b.ResetPlacement();
            // A column standing, and a log lying across the way the camera looks.
            b.Tube(new Vector3(1.2f, 0f, -1.2f), new Vector3(1.2f, 3.6f, -1.2f), 0.75f, 0.75f, 40, material);
            b.Tube(new Vector3(-0.2f, 0.55f, 2.6f), new Vector3(3.6f, 0.55f, 2.6f), 0.55f, 0.55f, 32, material);
            // A floor, a little off the ground.
            b.Box(new Vector3(0f, 0.05f, 5.2f), new Vector3(3.6f, 0.08f, 1.6f), material, closeBottom: false);
            return b.Build();
        }
    }
}
