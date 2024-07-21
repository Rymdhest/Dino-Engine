using Dino_Engine.Util;
using OpenTK.Mathematics;
using System.Drawing;
using System.Linq;
using System.Transactions;
namespace Dino_Engine.Modelling.Model
{
    public struct Mesh
    {
        public List<Face> faces;
        public List<MeshVertex> meshVertices;
        public bool finishedNormals = false;
        public static bool scaleUV = false;
        public Mesh()
        {
            meshVertices = new List<MeshVertex>();
            faces = new List<Face>();
        }
        public Mesh(List<Face> faces, List<MeshVertex> vertices)
        {
            this.faces = faces;
            this.meshVertices = vertices;
        }
        public Mesh(List<Vertex> vertices, List<int> indices)
        {
            Init(vertices, indices);
        }

        private void Init(List<Vertex> vertices, List<int> indices)
        {
            meshVertices = new List<MeshVertex>();
            faces = new List<Face>();

            for (int i = 0; i < vertices.Count; i++)
            {
                meshVertices.Add(new MeshVertex(vertices[i], i));
            }
            for (int i = 0; i < indices.Count / 3; i++)
            {
                MeshVertex A = meshVertices[indices[i * 3]];
                MeshVertex B = meshVertices[indices[i * 3 + 1]];
                MeshVertex C = meshVertices[indices[i * 3 + 2]];
                Face face = new Face(A, B, C);
                A.faces.Add(face);
                B.faces.Add(face);
                C.faces.Add(face);
                faces.Add(face);
            }
            calculateAllNormals();
        }
        public void makeFlat(bool flatNormal, bool flatMaterial, bool flatUV = false)
        {
            meshVertices.Clear();
            int i = 0;
            foreach (Face face in faces)
            {
                MeshVertex vertexA = new MeshVertex(face.A, i++);
                vertexA.faces.Add(face);
                vertexA.UV = face.A.UV;


                MeshVertex vertexB = new MeshVertex(face.B,  i++);
                vertexB.faces.Add(face);
                vertexB.UV = face.B.UV;

                MeshVertex vertexC = new MeshVertex(face.C, i++);
                vertexC.faces.Add(face);
                vertexC.UV = face.C.UV;

                if (flatNormal)
                {
                    vertexA.normal = face.faceNormal;
                    vertexB.normal = face.faceNormal;
                    vertexC.normal = face.faceNormal;
                }
                else
                {
                    vertexA.normal = face.A.normal;
                    vertexB.normal = face.B.normal;
                    vertexC.normal = face.C.normal;
                }
                face.A = vertexA;
                face.B = vertexB;
                face.C = vertexC;

                meshVertices.Add(vertexA);
                meshVertices.Add(vertexB);
                meshVertices.Add(vertexC);
            }
        }

        public void calculateAllNormals()
        {
            foreach (Face face in faces)
            {
                face.calcFaceNormal();
            }
            foreach (MeshVertex vertex in meshVertices)
            {
                vertex.calculateNormalAndTangent();
            }
            finishedNormals = true;
        }

        public void setColour(Colour setTo)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.material.Colour = setTo;
                meshVertices[i] = vertex;
            }
        }

        public float[] getAllPositionsArray()
        {
            float[] positionsArray = new float[meshVertices.Count * 3];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                positionsArray[3 * i + 0] = meshVertices[i].position.X;
                positionsArray[3 * i + 1] = meshVertices[i].position.Y;
                positionsArray[3 * i + 2] = meshVertices[i].position.Z;
            }
            return positionsArray;
        }

        public float[] getAllNormalsArray()
        {
            float[] normalsArray = new float[meshVertices.Count * 3];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                normalsArray[3 * i + 0] = meshVertices[i].normal.X;
                normalsArray[3 * i + 1] = meshVertices[i].normal.Y;
                normalsArray[3 * i + 2] = meshVertices[i].normal.Z;
            }
            return normalsArray;
        }

        public float[] getAllTangentsArray()
        {
            float[] tangentsArray = new float[meshVertices.Count * 3];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                tangentsArray[3 * i + 0] = meshVertices[i].tangent.X;
                tangentsArray[3 * i + 1] = meshVertices[i].tangent.Y;
                tangentsArray[3 * i + 2] = meshVertices[i].tangent.Z;
            }
            return tangentsArray;
        }
        public Material[] getAllMaterialArray()
        {
            Material[] materialsArray = new Material[meshVertices.Count];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                materialsArray[i] = meshVertices[i].material;
            }
            return materialsArray;
        }
        public float[] getAllColoursFloatArray()
        {
            float[] coloursArray = new float[meshVertices.Count * 3];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                Vector3 colour = meshVertices[i].material.Colour.ToVector3();
                coloursArray[3 * i + 0] = colour.X;
                coloursArray[3 * i + 1] = colour.Y;
                coloursArray[3 * i + 2] = colour.Z;
            }
            return coloursArray;
        }

        public float[] getAllUVsArray()
        {
            float[] UVsArray = new float[meshVertices.Count * 2];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                UVsArray[2 * i + 0] = meshVertices[i].UV.X;
                UVsArray[2 * i + 1] = meshVertices[i].UV.Y;
            }
            return UVsArray;
        }

        public float[] getAllMaterialIndicesArray()
        {
            float[] array = new float[meshVertices.Count];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                array[i] = meshVertices[i].material.materialIndex;
            }
            return array;
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

        private Vertex[] getAllVerticesArray()
        {
            Vertex[] verticesArray = new Vertex[meshVertices.Count];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                verticesArray[i] = new Vertex(meshVertices[i].position, meshVertices[i].UV, meshVertices[i].material);
            }
            return verticesArray;
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
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.position += amount*MyMath.rng3DMinusPlus();
                meshVertices[i] = vertex;
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
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.position = (new Vector4(vertex.position, 1.0f)* transformationMatrix).Xyz;
                if (scaleUV) meshVertices[i].UV = meshVertices[i].GetTagentSpaceScaledUV(transformation.scale);
                meshVertices[i] = vertex;
            }
            calculateAllNormals();
        }
        public Mesh Transformed(Transformation transformation)
        {
            var transformationMatrix = MyMath.createTransformationMatrix(transformation);
            Vertex[] newVertices = getAllVerticesArray();
            for (int i = 0; i < meshVertices.Count; i++)
            {
                Vector3 newPosition = (new Vector4(newVertices[i].position, 1.0f)*transformationMatrix).Xyz;
                newVertices[i].position = newPosition;
                if (scaleUV) newVertices[i].UV = meshVertices[i].GetTagentSpaceScaledUV(transformation.scale);
            }
            return new Mesh(newVertices.ToList<Vertex>(), getAllIndicesArray().ToList<int>());
        }


        public static Mesh Add(Mesh a, Mesh b)
        {
            List<Vertex> vertices= new List<Vertex>();
            vertices.AddRange(a.getAllVerticesArray());
            int offset = vertices.Count;
            vertices.AddRange(b.getAllVerticesArray());


            List<int> indices = new List<int>();
            indices.AddRange(a.getAllIndicesArray());
            int[] secondIndices = b.getAllIndicesArray();
            for (int i = 0; i < secondIndices.Length ; i++)
            {
                secondIndices[i] += offset;
            }
            indices.AddRange(secondIndices);

            Mesh mesh =new Mesh(vertices, indices);
            if (a.finishedNormals && b.finishedNormals) mesh.finishedNormals = true;

            return mesh;
        }
        public static Mesh operator +(Mesh a, Mesh b)  => Mesh.Add(a, b);
    }
}
