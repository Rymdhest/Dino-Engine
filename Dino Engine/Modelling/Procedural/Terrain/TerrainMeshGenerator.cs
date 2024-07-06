using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class TerrainMeshGenerator
    {

        public static Mesh GridToMesh(Grid grid)
        {
            Material material = Material.LEAF;
            List<Vector3> positions = new List<Vector3>();
            List<int> indices = new List<int>();


            for (int z = 0; z<grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    float y = grid.Values[x, z];
                    positions.Add(new Vector3(x, y, z));
                }
            }

            for (int z = 0; z < grid.Resolution.Y-1; z++)
            {
                for (int x = 0; x < grid.Resolution.X-1; x++)
                {
                    Vector3 p1 = new Vector3(x, grid.Values[x, z], z);
                    Vector3 p2 = new Vector3(x, grid.Values[x, z+1], z+1);
                    Vector3 p3 = new Vector3(x+1, grid.Values[x+1, z+1], z+1);
                    Vector3 p4 = new Vector3(x+1, grid.Values[x+1, z], z);

                    Vector3 p5 = (p1+ p2+p3+p4)/4f;
                    positions.Add(p5);

                    int i1= z*grid.Resolution.X+x;
                    int i2 = (z+1) * grid.Resolution.X + x;
                    int i3 = (z+1) * grid.Resolution.X + (x+1);
                    int i4 = z * grid.Resolution.X + (x+1);
                    int i5 = positions.Count-1;

                    indices.Add(i5); indices.Add(i1); indices.Add(i2);
                    indices.Add(i5); indices.Add(i2); indices.Add(i3);
                    indices.Add(i5); indices.Add(i3); indices.Add(i4);
                    indices.Add(i5); indices.Add(i4); indices.Add(i1);

                }
            }
            Mesh mesh = new Mesh(positions, indices, material);
            mesh.calculateAllNormals();
            //mesh.translate(new Vector3(-grid.Resolution.X/2f, 0f, -grid.Resolution.Y/2f));

            foreach(Vertex vertex in mesh.vertices)
            {
                if (vertex.position.Y < 5)
                {
                    vertex.material = Material.SAND;
                }

                if (Vector3.Dot( vertex.normal, new Vector3(0f, 1f, 0f)) < 0.7f)
                {
                    vertex.material = Material.ROCK;
                    vertex.material.roughness = 0.5f;
                }

                if (vertex.position.Y < 0)
                {
                    vertex.material.Colour = new Colour(32, 32, 210);
                    vertex.material.roughness = 0.2f;
                    vertex.material.metalicness = 0.75f;
                }

                if (vertex.position.Y > 65)
                {
                    vertex.material.Colour = new Colour(200, 200, 210);
                    vertex.material.roughness = 0.15f;
                }
            }
            mesh.makeFlat(flatNormal: true, flatMaterial: false);

            return mesh;
        }
    }
}
