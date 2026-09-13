using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    public class WeaponGenerator
    {
        public static Mesh GenerateSword()
        {
            Mesh sword = new Mesh();

            VertexMaterial ironMaterial = new VertexMaterial(TextureGenerator.iron);
            VertexMaterial leatherMaterial = new VertexMaterial(TextureGenerator.leather, new Colour(0.1f, 0.1f, 0.1f));

            float handleLength = 0.5f;
            float handleRadius = 0.05f;
            List<Vector2> handleRings = new List<Vector2> {
                new Vector2(handleRadius, 0f),
                new Vector2(handleRadius, handleLength)
            };
            Mesh handle = MeshGenerator.generateCylinder(handleRings, 6, leatherMaterial);


            sword += handle;

            List<Vector3> bladeRings = new List<Vector3> {
                new Vector3(0.1f, 0f, 0.01f),
                new Vector3(0.08f, 0.5f, 0.01f),
                new Vector3(0.05f, 0.9f, 0.005f),
                new Vector3(0.01f, 1.0f, 0.001f)
            };

            Mesh blade = MeshGenerator.generateCylinder(bladeRings, 4, ironMaterial, sealTop: 0.01f);
            blade.scale(new Vector3(2f));
            blade.makeFlat(flatNormal:true, flatMaterial:true);

            sword += blade.translated(new Vector3(0, handleLength, 0));

            Mesh nob = IcoSphereGenerator.CreateIcosphere(1, ironMaterial);
            nob.scale(new Vector3(handleRadius*2f));
            nob.makeFlat(flatNormal: true, flatMaterial: true);

            sword += nob;





            return sword;
        }
    }
}
