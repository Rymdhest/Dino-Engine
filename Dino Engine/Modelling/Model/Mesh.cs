using Dino_Engine.Util;
using OpenTK.Mathematics;
namespace Dino_Engine.Modelling.Model
{
    public class Mesh
    {
        public List<Face> faces;
        public List<Vertex> vertices;
        public Mesh()
        {
            vertices = new List<Vertex>();
            faces = new List<Face>();
        }
        public Mesh(List<Face> faces, List<Vertex> vertices, Material material)
        {
            this.faces = faces;
            this.vertices = vertices;
        }
        public Mesh(List<Vector3> positions, List<int> indices, Material material)
        {
            float[] positionsArray = new float[positions.Count*3];

            for (int i = 0; i <positions.Count; i++)
            {
                positionsArray[i * 3 + 0] = positions[i].X;
                positionsArray[i * 3 + 1] = positions[i].Y;
                positionsArray[i * 3 + 2] = positions[i].Z;
            }

            Init(positionsArray, indices.ToArray(), material);
        }
        public Mesh(List<float> positions, List<int> indices, Material material)
        {
            Init(positions.ToArray(), indices.ToArray(), material);
        }
        public Mesh(float[] positions, int[] indices, Material material)
        {
            Init(positions, indices, material);
        }

        private void Init(float[] positions, int[] indices, Material material)
        {
            vertices = new List<Vertex>();
            faces = new List<Face>();

            for (int i = 0; i < positions.Length / 3; i++)
            {
                Vertex v = new Vertex(new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]), i);
                //v.UV = new Vector2(uvs[i * 2], uvs[i * 2 + 1]);
                //v.colour = new Colour(colours[i*3], colours[i * 3+1], colours[i * 3+2]);
                v.material = material;
                vertices.Add(v);
            }
            for (int i = 0; i < indices.Length / 3; i++)
            {
                Vertex A = vertices[indices[i * 3]];
                Vertex B = vertices[indices[i * 3 + 1]];
                Vertex C = vertices[indices[i * 3 + 2]];
                Face face = new Face(A, B, C);
                A.faces.Add(face);
                B.faces.Add(face);
                C.faces.Add(face);
                faces.Add(face);
            }
            calculateAllNormals();
        }

        public void calculateAllNormals()
        {
            foreach (Face face in faces)
            {
                face.calcFaceNormal();
            }
            foreach (Vertex vertex in vertices)
            {
                vertex.calculateNormal();
            }
        }

        public void setRoughness(float setTo)
        {
            foreach (Vertex vertex in vertices)
            {
                vertex.material.roughness = setTo;
            }
        }
        public void setEmission(float setTo)
        {
            foreach (Vertex vertex in vertices)
            {
                vertex.material.emission = setTo;
            }
        }
        public void setMetalicness(float setTo)
        {
            foreach (Vertex vertex in vertices)
            {
                vertex.material.metalicness = setTo;
            }
        }

        public void setColour(Colour setTo)
        {
            foreach (Vertex vertex in vertices)
            {
                vertex.material.Colour = setTo;
            }
        }

        public float[] getAllPositionsArray()
        {
            float[] positionsArray = new float[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                positionsArray[3 * i + 0] = vertices[i].position.X;
                positionsArray[3 * i + 1] = vertices[i].position.Y;
                positionsArray[3 * i + 2] = vertices[i].position.Z;
            }
            return positionsArray;
        }

        public float[] getAllNormalsArray()
        {
            float[] normalsArray = new float[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                normalsArray[3 * i + 0] = vertices[i].normal.X;
                normalsArray[3 * i + 1] = vertices[i].normal.Y;
                normalsArray[3 * i + 2] = vertices[i].normal.Z;
            }
            return normalsArray;
        }

        public float[] getAllTangentsArray()
        {
            float[] tangentsArray = new float[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                tangentsArray[3 * i + 0] = vertices[i].tangent.X;
                tangentsArray[3 * i + 1] = vertices[i].tangent.Y;
                tangentsArray[3 * i + 2] = vertices[i].tangent.Z;
            }
            return tangentsArray;
        }

        public float[] getAllMaterialsArray()
        {
            float[] materialsArray = new float[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                materialsArray[3 * i + 0] = vertices[i].material.roughness;
                materialsArray[3 * i + 1] = vertices[i].material.emission;
                materialsArray[3 * i + 2] = vertices[i].material.metalicness;
            }
            return materialsArray;
        }

        public float[] getAllColoursArray()
        {
            float[] coloursArray = new float[vertices.Count * 3];

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 colour = vertices[i].material.Colour.ToVector3();
                coloursArray[3 * i + 0] = colour.X;
                coloursArray[3 * i + 1] = colour.Y;
                coloursArray[3 * i + 2] = colour.Z;
            }
            return coloursArray;
        }

        public float[] getAllUVsArray()
        {
            float[] UVsArray = new float[vertices.Count * 2];

            for (int i = 0; i < vertices.Count; i++)
            {
                UVsArray[2 * i + 0] = vertices[i].UV.X;
                UVsArray[2 * i + 1] = vertices[i].UV.Y;
            }
            return UVsArray;
        }

        public int[] getAllIndicesArray()
        {
            int indicesCount = faces.Count * 3;
            int[] indicesArray = new int[indicesCount];

            for (int i = 0; i < faces.Count; i++)
            {
                indicesArray[3 * i + 0] = faces[i].A.index;
                indicesArray[3 * i + 1] = faces[i].B.index;
                indicesArray[3 * i + 2] = faces[i].C.index;
            }
            return indicesArray;
        }
        public void cleanUp()
        {

        }
    }
}
