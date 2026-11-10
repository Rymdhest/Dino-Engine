using Dino_Engine.Debug;
using Dino_Engine.Physics;
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
        public bool finishedBaking = false;
        public static bool scaleUV = false;
        public bool isUsingBones = false;
        public List<PlantJoint>? joints = null;
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
            List<vIndex> vIndices = new List<vIndex>();
            for (int i = 0; i<indices.Count; i++)
            {
                vIndices.Add(new vIndex(indices[i]));
            }
            Init(vertices, vIndices);
        }
        public Mesh(List<Vertex> vertices, List<vIndex> indices)
        {
            Init(vertices, indices);
        }

        public AABB createAABB()
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            for (int i = 0; i<meshVertices.Count; i++)
            {
                if (meshVertices[i].position.X < min.X) min.X = meshVertices[i].position.X;
                if (meshVertices[i].position.Y < min.Y) min.Y = meshVertices[i].position.Y;
                if (meshVertices[i].position.Z < min.Z) min.Z = meshVertices[i].position.Z;

                if (meshVertices[i].position.X > max.X) max.X = meshVertices[i].position.X;
                if (meshVertices[i].position.Y > max.Y) max.Y = meshVertices[i].position.Y;
                if (meshVertices[i].position.Z > max.Z) max.Z = meshVertices[i].position.Z;
            }
            return new AABB(min, max);
        }

        public void bakeUVs()
        {
            foreach (Face face in faces)
            {
                if (face.uvIndexA > 0)
                {
                    MeshVertex oldVertex = face.A;
                    MeshVertex newVertex = new MeshVertex(new Vertex(oldVertex), new vIndex(meshVertices.Count));
                    newVertex.normal = oldVertex.normal;
                    newVertex.tangent = oldVertex.tangent;
                    newVertex.bitangent = oldVertex.bitangent;
                    face.A = newVertex;
                    face.uvIndexA = 0;
                    meshVertices.Add(newVertex);
                }

                if (face.uvIndexB > 0)
                {
                    MeshVertex oldVertex = face.B;
                    MeshVertex newVertex = new MeshVertex(new Vertex(oldVertex), new vIndex(meshVertices.Count));
                    newVertex.normal = oldVertex.normal;
                    newVertex.tangent = oldVertex.tangent;
                    newVertex.bitangent = oldVertex.bitangent;
                    face.B = newVertex;
                    face.uvIndexB = 0;
                    meshVertices.Add(newVertex);
                }

                if (face.uvIndexC > 0)
                {
                    MeshVertex oldVertex = face.C;
                    MeshVertex newVertex = new MeshVertex(new Vertex(oldVertex), new vIndex(meshVertices.Count));
                    newVertex.normal = oldVertex.normal;
                    newVertex.tangent = oldVertex.tangent;
                    newVertex.bitangent = oldVertex.bitangent;
                    face.C = newVertex;
                    face.uvIndexC = 0;
                    meshVertices.Add(newVertex);
                }
            }

            finishedBaking = true;
        }

        private void Init(List<Vertex> vertices, List<vIndex> indices)
        {
            meshVertices = new List<MeshVertex>();
            faces = new List<Face>();

            for (int i = 0; i < vertices.Count; i++)
            {
                meshVertices.Add(new MeshVertex(vertices[i], new vIndex(i)));
            }
            for (int i = 0; i < indices.Count / 3; i++)
            {
                MeshVertex A = meshVertices[indices[i * 3].index];
                MeshVertex B = meshVertices[indices[i * 3 + 1].index];
                MeshVertex C = meshVertices[indices[i * 3 + 2].index];

                int uvIndexA = indices[i * 3 + 0].uvVariant;
                int uvIndexB = indices[i * 3 + 1].uvVariant;
                int uvIndexC = indices[i * 3 + 2].uvVariant;

                Face face = new Face(A, B, C, uvIndexA, uvIndexB, uvIndexC);
                A.faces.Add(face);
                B.faces.Add(face);
                C.faces.Add(face);
                faces.Add(face);
            }
            //calculateAllNormals();
        }
        public void makeFlat(bool flatNormal = true, bool flatMaterial= false, bool flatUV = false)
        {
            meshVertices.Clear();
            int i = 0;
            foreach (Face face in faces)
            {
                MeshVertex vertexA = new MeshVertex(face.A, new vIndex(i++)); // probably wrong logic...
                vertexA.faces.Add(face);
                vertexA.UVs = face.A.UVs;


                MeshVertex vertexB = new MeshVertex(face.B, new vIndex(i++));
                vertexB.faces.Add(face);
                vertexB.UVs = face.B.UVs;

                MeshVertex vertexC = new MeshVertex(face.C, new vIndex(i++));
                vertexC.faces.Add(face);
                vertexC.UVs = face.C.UVs;

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
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.normal = new Vector3(0);
                vertex.tangent = new Vector3(0);
                vertex.bitangent = new Vector3(0);
                meshVertices[i] = vertex;
            }

            foreach (Face face in faces)
            {
                face.calcFaceNormal();
            }

            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.calculateNormalAndTangent();
                meshVertices[i] = vertex;
            }

            finishedNormals = true;
        }

        public void setColour(Colour setTo)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.colour = setTo;
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

        /// <summary>Every vertex's <see cref="Vertex.Edges"/>, or null if none has any, as most meshes don't.</summary>
        public float[]? getAllEdgesArray()
        {
            bool any = false;
            foreach (MeshVertex vertex in meshVertices) any |= vertex.Edges != Vertex.NoEdges;
            if (!any) return null;
            float[] array = new float[meshVertices.Count * 4];
            for (int i = 0; i < meshVertices.Count; i++)
            {
                Vector4 edges = meshVertices[i].Edges;
                array[4 * i + 0] = edges.X;
                array[4 * i + 1] = edges.Y;
                array[4 * i + 2] = edges.Z;
                array[4 * i + 3] = edges.W;
            }
            return array;
        }

        public float[] getAllColoursFloatArray()
        {
            float[] coloursArray = new float[meshVertices.Count * 3];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                Vector3 colour = meshVertices[i].colour.ToVector3();
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
                UVsArray[2 * i + 0] = meshVertices[i].UVs[0].X;
                UVsArray[2 * i + 1] = meshVertices[i].UVs[0].Y;
            }
            return UVsArray;
        }

        public float[] getAllMaterialIndicesArray()
        {
            float[] array = new float[meshVertices.Count];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                array[i] = meshVertices[i].materialTextureIndex;
            }
            return array;
        }

        public float[] getAllBoneIndicesArray()
        {
            float[] boneArray = new float[meshVertices.Count * 2];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                boneArray[2 * i + 0] = meshVertices[i].boneIndices.X;
                boneArray[2 * i + 1] = meshVertices[i].boneIndices.Y;
            }
            return boneArray;
        }

        public float[] getAllBoneWeightsArray()
        {
            float[] boneArray = new float[meshVertices.Count * 2];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                boneArray[2 * i + 0] = meshVertices[i].BoneWeights.X;
                boneArray[2 * i + 1] = meshVertices[i].BoneWeights.Y;
            }
            return boneArray;
        }

        public vIndex[] getAllIndicesArray()
        {
            int indicesCount = faces.Count * 3;
            vIndex[] indicesArray = new vIndex[indicesCount];

            for (int i = 0; i < faces.Count; i++)
            {
                indicesArray[3 * i + 0] = new vIndex(faces[i].A.index.index, faces[i].uvIndexA);
                indicesArray[3 * i + 1] = new vIndex(faces[i].B.index.index, faces[i].uvIndexB);
                indicesArray[3 * i + 2] = new vIndex(faces[i].C.index.index, faces[i].uvIndexC);
            }
            return indicesArray;
        }

        public Vertex[] getAllVerticesArray()
        {
            Vertex[] verticesArray = new Vertex[meshVertices.Count];

            for (int i = 0; i < meshVertices.Count; i++)
            {
                verticesArray[i] = new Vertex(meshVertices[i]);
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

        public Mesh rotated(Quaternion rotation)
        {
            Vertex[] oldVertices = getAllVerticesArray();
            Vertex[] newVertices = new Vertex[oldVertices.Length];
            for (int i = 0; i < oldVertices.Length; i++)
            {
                Vector3 newPosition = Vector3.Transform(oldVertices[i].position, rotation);
                newVertices[i] = new Vertex(newPosition, oldVertices[i].materialTextureIndex, oldVertices[i].colour, oldVertices[i].boneIndices, oldVertices[i].BoneWeights, oldVertices[i].UVs) { joint = oldVertices[i].joint, Edges = oldVertices[i].Edges }; // TODO  potential uses same UVs
            }
            return new Mesh(newVertices.ToList<Vertex>(), getAllIndicesArray().ToList<vIndex>()) { joints = MovedJoints(p => Vector3.Transform(p, rotation)) };
        }

        public void rotate(Quaternion rotation)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.position = Vector3.Transform(vertex.position, rotation);
                //if (scaleUV) meshVertices[i].UV = meshVertices[i].GetTagentSpaceScaledUV(transformation.scale);
                meshVertices[i] = vertex;
            }
            joints = MovedJoints(p => Vector3.Transform(p, rotation));
            calculateAllNormals();
        }

        public void scale(Vector3 scale)
        {
            Transform(new Transformation(new Vector3(0), new Vector3(0), scale));
        }
        public Mesh scaled(Vector3 scale)
        {
            return Transformed(new Transformation(new Vector3(0), new Vector3(0), scale));
        }

        public void scaleUVs(Vector2 scale)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                for(int j = 0; j<vertex.UVs.Length; j++)
                {
                    vertex.UVs[j] *= scale;
                }
                meshVertices[i] = vertex;
            }
        }

        public void ProjectUVsWorldSpaceCube(float scale)
        {
            //calculateAllNormals();
            foreach (var vertex in meshVertices)
            {
                //Vector3 normal = vertex.faces[0].faceNormal;
                Vector3 normal = vertex.normal;
                Vector3 pos = vertex.position;
                // Determine the dominant axis of the normal
                Vector3 absNormal = new Vector3(Math.Abs(normal.X), Math.Abs(normal.Y), Math.Abs(normal.Z));
                Vector2 uv;

                if (absNormal.X >= absNormal.Y && absNormal.X >= absNormal.Z)
                {
                    // Project onto YZ plane (X-dominant face, left/right)
                    if (normal.X >0) uv = new Vector2(pos.Y, -pos.Z);
                    else uv = new Vector2(pos.Y, pos.Z);
                }
                else if (absNormal.Y >= absNormal.X && absNormal.Y >= absNormal.Z)
                {
                    // Project onto XZ plane (Y-dominant face, top/bottom)
                    if (normal.Y > 0) uv = new Vector2(pos.X, pos.Z);
                    else uv = new Vector2(pos.X, -pos.Z);
                }
                else
                {
                    // Project onto XY plane (Z-dominant face, front/back)
                    if (normal.Z > 0) uv = new Vector2(pos.X, -pos.Y);
                    else uv = new Vector2(pos.X, pos.Y);
                }
                uv *= scale;
                for (int i = 0; i< vertex.UVs.Length; i++)
                {
                    vertex.UVs[i] = uv;
                }
            }
            calculateAllNormals();
        }

        public void Transform(Transformation transformation)
        {
            var transformationMatrix = MyMath.createTransformationMatrix(transformation);


            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.position = (new Vector4(vertex.position, 1.0f)* transformationMatrix).Xyz;
                vertex.Edges = ScaledEdges(vertex.Edges, transformation.scale);
                //if (scaleUV) meshVertices[i].UV = meshVertices[i].GetTagentSpaceScaledUV(transformation.scale);
                meshVertices[i] = vertex;
            }
            joints = MovedJoints(p => (new Vector4(p, 1.0f) * transformationMatrix).Xyz);
            calculateAllNormals();
        }
        public Mesh Transformed(Transformation transformation)
        {
            var transformationMatrix = MyMath.createTransformationMatrix(transformation);
            Vertex[] oldVertices = getAllVerticesArray();
            Vertex[] newVertices = new Vertex[oldVertices.Length];
            for (int i = 0; i < oldVertices.Length; i++)
            {
                Vector3 newPosition = (new Vector4(oldVertices[i].position, 1.0f)*transformationMatrix).Xyz;
                Vector2[] newUVs = new Vector2[oldVertices[i].UVs.Length];
                for (int j = 0; j < oldVertices[i].UVs.Length; j++)
                {
                    newUVs[j] = new Vector2( oldVertices[i].UVs[j].X, oldVertices[i].UVs[j].Y);
                }
                newVertices[i] = new Vertex(newPosition, oldVertices[i].materialTextureIndex, oldVertices[i].colour, oldVertices[i].boneIndices, oldVertices[i].BoneWeights, newUVs) { joint = oldVertices[i].joint, Edges = ScaledEdges(oldVertices[i].Edges, transformation.scale) }; // TODO  potential uses same UVs
                //if (scaleUV) newVertices[i].UV = meshVertices[i].GetTagentSpaceScaledUV(transformation.scale);
            }
            return new Mesh(newVertices.ToList<Vertex>(), getAllIndicesArray().ToList<vIndex>()) { joints = MovedJoints(p => (new Vector4(p, 1.0f) * transformationMatrix).Xyz) };
        }

        // A face's edges are as far off as it's grown: by the mean of the stretch, which is exact for an even one.
        private static Vector4 ScaledEdges(Vector4 edges, Vector3 scale)
        {
            if (edges == Vertex.NoEdges) return edges;
            float factor = MathF.Cbrt(MathF.Abs(scale.X * scale.Y * scale.Z));
            return edges * factor;
        }

        // A plant's joints go wherever its vertices go. A new list, as a mesh's copies share theirs and each goes its own way.
        private readonly List<PlantJoint>? MovedJoints(Func<Vector3, Vector3> move)
        {
            if (joints == null) return null;
            var moved = new List<PlantJoint>(joints.Count);
            foreach (PlantJoint joint in joints)
            {
                PlantJoint next = joint;
                next.Base = move(joint.Base);
                next.Tip = move(joint.Tip);
                moved.Add(next);
            }
            return moved;
        }

        public readonly int JointCount => joints?.Count ?? 0;

        /// <summary>Adds a joint to the plant, tying nothing to it yet. Returns its number.</summary>
        public int AddJoint(PlantJoint joint)
        {
            joints ??= new List<PlantJoint>();
            joints.Add(joint);
            return joints.Count - 1;
        }

        /// <summary>Ties everything that nothing holds yet to a new joint. Returns its number.</summary>
        public int Bind(PlantJoint joint)
        {
            int number = AddJoint(joint);
            foreach (MeshVertex vertex in meshVertices)
            {
                if (vertex.joint < 0) vertex.joint = number;
            }
            return number;
        }

        /// <summary>
        /// Ties everything nothing holds yet to a chain of joints through <paramref name="points"/>, each vertex to the link it's nearest. A shape that arches has
        /// to be a chain, as one straight joint would bend everything to the side of it like the outside of a bend. Returns the number of the last link.
        /// </summary>
        public int BindChain(PlantJoint like, int parent, params Vector3[] points)
        {
            int first = JointCount;
            foreach (PlantJoint link in like.Chain(points))
            {
                PlantJoint joint = link;
                joint.Parent = JointCount > first ? JointCount - 1 : parent;
                parent = AddJoint(joint);
            }
            foreach (MeshVertex vertex in meshVertices)
            {
                if (vertex.joint < 0) vertex.joint = PlantRig.Nearest(joints!, first, parent, vertex.position);
            }
            return parent;
        }

        /// <summary>Hangs every joint from <paramref name="firstJoint"/> on that stands on nothing from <paramref name="parent"/>.</summary>
        public void Hang(int firstJoint, int parent)
        {
            if (joints == null) return;
            for (int i = firstJoint; i < joints.Count; i++)
            {
                if (joints[i].Parent >= 0) continue;
                PlantJoint joint = joints[i];
                joint.Parent = parent;
                joints[i] = joint;
            }
        }

        /// <summary>Stills a plant, for what's built like one but doesn't move.</summary>
        public void Still()
        {
            joints = null;
            foreach (MeshVertex vertex in meshVertices) vertex.joint = -1;
        }


        public static Mesh Add(Mesh a, Mesh b)
        {
            List<Vertex> vertices= new List<Vertex>();
            vertices.AddRange(a.getAllVerticesArray());
            int offset = vertices.Count;
            Vertex[] added = b.getAllVerticesArray();
            foreach (Vertex vertex in added)
            {
                if (vertex.joint >= 0) vertex.joint += a.JointCount;
            }
            vertices.AddRange(added);


            List<vIndex> indices = new List<vIndex>();
            indices.AddRange(a.getAllIndicesArray());
            vIndex[] secondIndices = b.getAllIndicesArray();
            for (int i = 0; i < secondIndices.Length ; i++)
            {
                secondIndices[i].index += offset;
            }
            indices.AddRange(secondIndices);

            Mesh mesh =new Mesh(vertices, indices);
            if (a.joints != null || b.joints != null)
            {
                mesh.joints = new List<PlantJoint>(a.JointCount + b.JointCount);
                if (a.joints != null) mesh.joints.AddRange(a.joints);
                if (b.joints != null)
                {
                    foreach (PlantJoint joint in b.joints)
                    {
                        PlantJoint next = joint;
                        if (next.Parent >= 0) next.Parent += a.JointCount;
                        mesh.joints.Add(next);
                    }
                }
            }
            if (a.finishedNormals && b.finishedNormals) mesh.finishedNormals = true;
            mesh.calculateAllNormals(); 
            return mesh;
        }

        public Mesh SetAllBoneWeights(Vector2 weight)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.BoneWeights = weight;
                meshVertices[i] = vertex;
            }
            isUsingBones = true;
            return this;
        }

        public Mesh setAllBoneIndices(Vector2 indices)
        {
            for (int i = 0; i < meshVertices.Count; i++)
            {
                MeshVertex vertex = meshVertices[i];
                vertex.boneIndices = indices;
                meshVertices[i] = vertex;
            }
            isUsingBones = true;
            return this;
        }

        public static Mesh operator +(Mesh a, Mesh b)  => Mesh.Add(a, b);
    }
}
