using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class TerrainMeshGenerator
    {

        public static Mesh GridToMesh(Grid<float> grid, Vector2 worldSize, Material material, out Vector3Grid normals)
        {
            List<Vertex> vertices = new List<Vertex>();
            List<int> indices = new List<int>();
            Vector3 cellSizeWorld = new Vector3((worldSize.X) / (grid.Resolution.X-1f), 1f, (worldSize.Y) / (grid.Resolution.Y-1f));


            for (int z = 0; z<grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    float y = grid.Values[x, z];
                    Vector3 position = new Vector3(x, y, z) * cellSizeWorld;
                    vertices.Add(new Vertex(position, new Vector2(position.X, position.Z), material));
                }
            }

            for (int z = 0; z < grid.Resolution.Y-1; z++)
            {
                for (int x = 0; x < grid.Resolution.X-1; x++)
                {
                    Vector3 p1 = new Vector3(x, grid.Values[x, z], z)* cellSizeWorld;
                    Vector3 p2 = new Vector3(x, grid.Values[x, z+1], z+1)* cellSizeWorld;
                    Vector3 p3 = new Vector3(x+1, grid.Values[x+1, z+1], z+1)* cellSizeWorld;
                    Vector3 p4 = new Vector3(x+1, grid.Values[x+1, z], z)* cellSizeWorld;

                    Vector3 p5 = (p1+ p2+p3+p4)/4f;
                    vertices.Add(new Vertex(p5, new Vector2(p5.X, p5.Z), material));

                    int i1= z*grid.Resolution.X+x;
                    int i2 = (z+1) * grid.Resolution.X + x;
                    int i3 = (z+1) * grid.Resolution.X + (x+1);
                    int i4 = z * grid.Resolution.X + (x+1);
                    int i5 = vertices.Count-1;

                    indices.Add(i5); indices.Add(i1); indices.Add(i2);
                    indices.Add(i5); indices.Add(i2); indices.Add(i3);
                    indices.Add(i5); indices.Add(i3); indices.Add(i4);
                    indices.Add(i5); indices.Add(i4); indices.Add(i1);

                }
            }


            Mesh mesh = new Mesh(vertices, indices);
            mesh.calculateAllNormals();
            //mesh.translate(new Vector3(-grid.Resolution.X/2f, 0f, -grid.Resolution.Y/2f));

            foreach(MeshVertex vertex in mesh.meshVertices)
            {
            }

            normals = new Vector3Grid(grid.Resolution);
            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    int i = z * grid.Resolution.X + x;
                    Vector3 normal = mesh.meshVertices[i].normal;
                    normals.Values[x, z] = normal;
                }
            }

            foreach (MeshVertex vertex in mesh.meshVertices)
            {

                //vertex.material.Colour = new Colour(MyMath.rng3D());
            }

            //mesh.makeFlat(flatNormal: true, flatMaterial: true);
            mesh.scaleUVs(new Vector2(0.05f));

            return mesh;
        }
    }
}
