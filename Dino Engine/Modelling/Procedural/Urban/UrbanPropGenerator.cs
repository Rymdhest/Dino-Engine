using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Urban
{
    public class UrbanPropGenerator
    {
        public static Mesh GenerateStreetCone()
        {
            Material materialWhite = new Material(new Colour(235, 235, 235, 1.0f), 0.0f, 0.2f, 0.0f);
            Material materialOrange = new Material(new Colour(198, 76, 39, 1.0f), 0.0f, 0.2f, 0.0f);
            Material baseMAterial = new Material(new Colour(10, 10, 10, 1.0f), 0.0f, 0.9f, 0.0f);
            Mesh mesh = new Mesh();
            float baseHeight = 0.1f;
            int numLayers = 5;
            for (int layer = 0; layer< numLayers; layer++)
            {
                float heightFactor = layer*(1f / numLayers);
                float heightNext = (layer+1)*(1f / numLayers);
                Material material = materialOrange;
                if (layer % 2 == 1) material = materialWhite;

                List<Vector2> layers = new List<Vector2>() {
                new Vector2(1f-heightFactor, heightFactor),
                new Vector2(1f-heightNext, heightNext)};
                Mesh part = MeshGenerator.generateCylinder(layers, 8, material);
                mesh += part;
            }
            mesh.scale(new Vector3(0.3f, 1f, 0.3f));
            mesh.translate(new Vector3(0.0f, baseHeight*0.5f, 0.0f));

            Mesh basePart = MeshGenerator.generateBox(baseMAterial);
            basePart.scale(new Vector3(0.68f, baseHeight, 0.68f));
            basePart.translate(new Vector3(0, baseHeight*0.2f, 0));
            mesh += basePart;
            return mesh;
        }
            public static glModel GenerateStreetLight(out Vector3 lightPosition)
        {
            Material poleMaterial = new Material(new Colour(122, 122, 122, 1.0f), 0.0f, 0.8f, 0.0f);
            Material glowMaterial = new Material(new Colour(235, 193, 106, 1.0f), 0.0f, 0.2f, 10.0f);

            float r = 0.5f;
            float angle = MathF.PI / 2.6f;
            float h = 22f;
            Mesh mesh = new Mesh();
            Transformation transformation = new Transformation();
            List<Vector2> layers = new List<Vector2>() {
                new Vector2(r, 0),
                new Vector2(r, h*0.05f),
                new Vector2(r*0.94f, h*0.053f),
                new Vector2(r*0.94f, h*0.24f),
                new Vector2(r*0.64f, h*0.255f),
                new Vector2(r*0.64f, h*0.99f),
                new Vector2(0, h) };
            Mesh pole = MeshGenerator.generateCylinder(layers, 7, poleMaterial);
            mesh += pole;

            float h2 = h * 0.35f;
            layers = new List<Vector2>() {
                new Vector2(r*0.6f, 0),
                new Vector2(r*0.5f, h2) };
            Mesh pole2 = MeshGenerator.generateCylinder(layers, 7, poleMaterial);
            transformation *= new Transformation(new Vector3(0f, h - r * 1.5f, 0f), new Vector3(0, 0f, -angle), new Vector3(1f));
            //Console.WriteLine(transformation+"\n");
            pole2.Transform(transformation);
            mesh += pole2;



            Mesh lightHolder = MeshGenerator.generateBox(poleMaterial);
            lightHolder += (MeshGenerator.generateBox(poleMaterial)).Transformed(new Transformation(new Vector3(0f, 0.5f, 0f), new Vector3(0, 0f, 0.2f), new Vector3(0.5f, 0.5f, 0.5f)));
            lightHolder += (MeshGenerator.generateBox(glowMaterial)).Transformed(new Transformation(new Vector3(0f, -0.5f, 0f), new Vector3(0, 0f, 0.0f), new Vector3(0.7f, 0.7f, 0.7f)));

            transformation = new Transformation(new Vector3(0f, h2, 0f), new Vector3(0, 0f, angle), new Vector3(1.5f, 0.5f, 1.0f))* transformation;

            lightHolder.Transform(transformation);
            lightHolder.translate(new Vector3(0.65f, 0, 0f));


            mesh += lightHolder;
            transformation = new Transformation(new Vector3(0.4f, -0.8f, 0f), new Vector3(0, 0f, 0f), new Vector3(1)) * transformation;
            lightPosition = new Vector3(0f);
            lightPosition *= transformation;

            //mesh += ((IcoSphereGenerator.CreateIcosphere(1, poleMaterial)).scaled(new Vector3(0.5f)).translated(lightPosition));


            return glLoader.loadToVAO(mesh);
        }
    }
 }