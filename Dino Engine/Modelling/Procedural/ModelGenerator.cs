using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural
{
    public class ModelGenerator
    {
        public static readonly glModel UNIT_SPHERE = glLoader.loadToVAO(IcoSphereGenerator.CreateIcosphere(1, Material.ROCK));
        public static readonly glModel UNIT_CONE = glLoader.loadToVAO(MeshGenerator.GenerateCone(Material.ROCK));

        public static glModel GenerateHouse()
        {
            Material wallMaterial = new Material(new Colour(38, 30, 38, 1.0f), 0f, 0.5f, 0.0f);
            Material windowGlow = new Material(new Colour(235, 193, 106, 1.0f), 0.0f, 0.2f, 40.0f);
            Material windowNormal = new Material(new Colour(15, 15, 25, 1.0f), 0.23f, 0.8f, 0.0f);
            Mesh house = new Mesh();
            int numWindows = 8;
            int numStories = 6;
            float floorheight = 3f;

            Mesh window1 = MeshGenerator.ExtrudedPlane(new Vector3(0.5f, 0.5f, -0.1f), wallMaterial, windowNormal);
            window1.translate(new Vector3(0f, 0.5f, 0f));
            window1.scale(new Vector3(1f, floorheight, 1f));

            Mesh window2 = MeshGenerator.ExtrudedPlane(new Vector3(0.5f, 0.5f, -0.1f), wallMaterial, windowGlow);
            window2.translate(new Vector3(0f, 0.5f, 0f));
            window2.scale(new Vector3(1f, floorheight, 1f));

            for (int k = 0; k < numStories; k++)
            {
                Mesh floor = new Mesh();
                for (int i = 0; i < 4; i++)
                {
                    Mesh floorWall = new Mesh();
                    for (int j = 0; j < numWindows; j++)
                    {
                        if (MyMath.rng() < 0.3f) floorWall += window2.translated(new Vector3(j, 0, 0));
                        else floorWall += window1.translated(new Vector3(j, 0, 0));

                    }
                    floorWall.translate(new Vector3(-numWindows / 2f + 0.5f, 0f, numWindows / 2f));

                    floor += floorWall.rotated(new Vector3(0, (MathF.PI / 2f) * i, 0));
                }

                house += floor.translated(new Vector3(0f, floorheight * k, 0f));
            }
            

            Mesh roof = MeshGenerator.ExtrudedPlane(new Vector3(0.92f, 0.92f, -0.1f), wallMaterial, Material.ROCK);
            roof.rotate(new Vector3(-MathF.PI/2f, 0f, 0f));
            roof.scale(new Vector3(numWindows, 1f, numWindows));
            Mesh roofBox = MeshGenerator.generateBox(wallMaterial);
            roofBox.translate(new Vector3(2f, 0f, 2f));
            roof += roofBox;
            roof.translate(new Vector3(0f, floorheight * numStories, 0f));

            house += roof;
            return glLoader.loadToVAO(house);
        }
    }
}
