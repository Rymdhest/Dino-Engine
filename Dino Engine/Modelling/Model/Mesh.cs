using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Transactions;
namespace Dino_Engine.Modelling.Model
{
    public struct Mesh
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
        public Mesh(List<Vector3> positions, List<int> indices, List<Material> materials)
        {
            float[] positionsArray = new float[positions.Count * 3];

            for (int i = 0; i < positions.Count; i++)
            {
                positionsArray[i * 3 + 0] = positions[i].X;
                positionsArray[i * 3 + 1] = positions[i].Y;
                positionsArray[i * 3 + 2] = positions[i].Z;
            }

            Init(positionsArray, indices.ToArray(), materials.ToArray());
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
            //calculateAllNormals();
        }
        private void Init(float[] positions, int[] indices, Material[] materials)
        {
            vertices = new List<Vertex>();
            faces = new List<Face>();

            for (int i = 0; i < positions.Length / 3; i++)
            {
                Vertex v = new Vertex(new Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]), i);
                //v.UV = new Vector2(uvs[i * 2], uvs[i * 2 + 1]);
                //v.colour = new Colour(colours[i*3], colours[i * 3+1], colours[i * 3+2]);
                v.material = materials[i];
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
            //calculateAllNormals();
        }
        private Mesh(float[] positions, int[] indices, Material[] materials)
        {
            Init(positions, indices, materials);
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
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.material.roughness = setTo;
                vertices[i] = vertex;
            }
        }
        public void setEmission(float setTo)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.material.emission = setTo;
                vertices[i] = vertex;
            }
        }
        public void setMetalicness(float setTo)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.material.metalicness = setTo;
                vertices[i] = vertex;
            }
        }

        public void setColour(Colour setTo)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.material.Colour = setTo;
                vertices[i] = vertex;
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

        public Material[] getAllMaterialsTypeArray()
        {
            Material[] materialsArray = new Material[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                materialsArray[i] = vertices[i].material;
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

        public void FlatRandomness(float amount)
        {
            FlatRandomness(new Vector3(amount));
        }
        public void FlatRandomness(Vector3 amount)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.position += amount*MyMath.rng3DMinusPlus();
                vertices[i] = vertex;
            }
        }

        public void translate(Vector3 translation)
        {
            Transform(new Transformation(translation, new Vector3(0f), new Vector3(1f)));
        }
        public Mesh translated(Vector3 translation)
        {
            return Transformed(new Transformation(translation, new Vector3(0f), new Vector3(1f)));
        }
        public void rotate(Vector3 rotation)
        {
            Transform(new Transformation(new Vector3(0), rotation, new Vector3(1f)));
        }
        public Mesh rotated(Vector3 rotation)
        {
            return Transformed(new Transformation(new Vector3(0), rotation, new Vector3(1f)));
        }
        public void scale(Vector3 scale)
        {
            Transform(new Transformation(new Vector3(0), new Vector3(0), scale));
        }
        public Mesh scaled(Vector3 scale)
        {
            return Transformed(new Transformation(new Vector3(0), new Vector3(0), scale));
        }
        public void Transform(Transformation transformation)
        {
            var transformationMatrix = MyMath.createTransformationMatrix(transformation);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vertex vertex = vertices[i];
                vertex.position = (new Vector4(vertex.position, 1.0f)* transformationMatrix).Xyz;
                vertices[i] = vertex;
            }
        }
        public Mesh Transformed(Transformation transformation)
        {
            var transformationMatrix = MyMath.createTransformationMatrix(transformation);
            float[] newPositions = new float[vertices.Count*3];
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 newPosition = (new Vector4(vertices[i].position, 1.0f)*transformationMatrix).Xyz;
                newPositions[i * 3+0] = newPosition.X;
                newPositions[i * 3+1] = newPosition.Y;
                newPositions[i * 3+2] = newPosition.Z;
            }
            return new Mesh(newPositions, getAllIndicesArray(), getAllMaterialsTypeArray());
        }


        public static Mesh Add(Mesh a, Mesh b)
        {
            List<float> positions = new List<float>();
            positions.AddRange(a.getAllPositionsArray());
            int offset = positions.Count/3;
            positions.AddRange(b.getAllPositionsArray());

            List<int> indices = new List<int>();
            indices.AddRange(a.getAllIndicesArray());
            int[] secondIndices = b.getAllIndicesArray();
            for (int i = 0; i < secondIndices.Length ; i++)
            {
                secondIndices[i] += offset;
            }
            indices.AddRange(secondIndices);

            List<Material> materials = new List<Material>();
            materials.AddRange(a.getAllMaterialsTypeArray());
            materials.AddRange(b.getAllMaterialsTypeArray());

            return new Mesh(positions.ToArray(), indices.ToArray(), materials.ToArray());
        }
        public static Mesh operator +(Mesh a, Mesh b)  => Mesh.Add(a, b);
    }
}
