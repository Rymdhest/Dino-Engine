using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling;

public class glLoader
{
    public static glModel loadToVAO(Mesh mesh)
    {
        if (!mesh.finishedNormals) mesh.calculateAllNormals();
        if (!mesh.finishedBaking) mesh.bakeUVs();

        vIndex[] vindices = mesh.getAllIndicesArray();
        int[] indices = new int[vindices.Length];
        for (int i = 0; i<vindices.Length; i++)
        {
            indices[i] = vindices[i].index;
        }
        // A ghost's light is drawn on its own after its solid part (glModel.SpectralIndexCount), so nothing may reorder its triangles across where that begins: it grows nothing.
        bool spectral = mesh.SpectralFrom >= 0 && mesh.SpectralFrom < indices.Length;
        float[]? shells = mesh.ShellStyle != 0 && !spectral ? ShellData(mesh) : null;
        int shellIndices = shells != null ? ShellsFirst(indices, shells, mesh.getAllNormalsArray(), mesh.ShellStyle, -0.35f) : 0;
        glModel model;
        if (mesh.isUsingBones)
        {
            model = loadToVAO(mesh.getAllPositionsArray(), mesh.getAllColoursFloatArray(), mesh.getAllNormalsArray(), mesh.getAllTangentsArray(), mesh.getAllUVsArray(), mesh.getAllMaterialIndicesArray(), mesh.getAllBoneIndicesArray(), mesh.getAllBoneWeightsArray(), indices);
        } else
        {
            model = loadToVAO(mesh.getAllPositionsArray(), mesh.getAllColoursFloatArray(), mesh.getAllNormalsArray(), mesh.getAllTangentsArray(), mesh.getAllUVsArray(), mesh.getAllMaterialIndicesArray(), indices);
        }

        model.box = mesh.createAABB();
        if (spectral) model.SpectralIndexCount = indices.Length - mesh.SpectralFrom;
        if (mesh.joints != null) LoadPlant(model, mesh);
        if (mesh.getAllEdgesArray() is float[] edges) LoadEdges(model, edges);
        if (shells != null && shellIndices > 0)
        {
            LoadShells(model, shells, shellIndices);
            model.Shell = mesh.ShellStyle;
        }
        return model;
    }

    /// <summary>A plant, straight from the lists it was grown in.</summary>
    public static glModel loadToVAO(FloraMesh plant)
    {
        int count = plant.VertexCount;
        float[] positions = new float[count * 3], colours = new float[count * 3], normals = new float[count * 3], tangents = new float[count * 3];
        float[] uvs = new float[count * 2], materials = new float[count], joints = new float[count];
        Rendering.Shells.ShellStyle? style = Rendering.Shells.ShellStyles.Of(plant.ShellStyle);
        bool everywhere = style != null && style.Mask != Rendering.Shells.ShellMask.Vertex;
        float[]? shells = style != null ? new float[count * 4] : null;
        bool grows = false;
        for (int i = 0; i < count; i++)
        {
            FloraVertex vertex = plant.Vertices[i];
            positions[i * 3] = vertex.Position.X; positions[i * 3 + 1] = vertex.Position.Y; positions[i * 3 + 2] = vertex.Position.Z;
            colours[i * 3] = vertex.Colour.X; colours[i * 3 + 1] = vertex.Colour.Y; colours[i * 3 + 2] = vertex.Colour.Z;
            normals[i * 3] = vertex.Normal.X; normals[i * 3 + 1] = vertex.Normal.Y; normals[i * 3 + 2] = vertex.Normal.Z;
            tangents[i * 3] = vertex.Tangent.X; tangents[i * 3 + 1] = vertex.Tangent.Y; tangents[i * 3 + 2] = vertex.Tangent.Z;
            uvs[i * 2] = vertex.UV.X; uvs[i * 2 + 1] = vertex.UV.Y;
            materials[i] = vertex.Material + vertex.MaterialShare;
            joints[i] = vertex.Joint;
            if (shells != null)
            {
                Vector3 outward = vertex.ShellNormal.LengthSquared > 1e-8f ? vertex.ShellNormal : vertex.Normal;
                float amount = everywhere ? 1f : Math.Clamp(vertex.Shell, 0f, 1f);
                // A vertex that isn't anywhere grows nothing (ShellData).
                if (!Finite(vertex.Position) || !Finite(outward) || outward.LengthSquared < 1e-8f)
                {
                    outward = Vector3.UnitY;
                    amount = 0f;
                }
                shells[i * 4] = outward.X; shells[i * 4 + 1] = outward.Y; shells[i * 4 + 2] = outward.Z; shells[i * 4 + 3] = amount;
                grows |= amount > 0f;
            }
        }

        int[] indices = plant.Indices.ToArray();
        int shellIndices = grows ? ShellsFirst(indices, shells!, normals, plant.ShellStyle, plant.ShellFacing) : 0;
        glModel model = loadToVAO(positions, colours, normals, tangents, uvs, materials, indices);
        plant.Bounds(out Vector3 min, out Vector3 max);
        model.box = new AABB(min, max);
        if (plant.Joints.Count > 0) LoadPlant(model, joints, plant.Joints, PlantRig.Reach(plant));
        if (shellIndices > 0)
        {
            LoadShells(model, shells!, shellIndices);
            model.Shell = plant.ShellStyle;
        }
        return model;
    }

    // Where each vertex is from the edges of its face, for the relief to keep clear of corners. Enabled in the vertex array for good, as a plant's joints are.
    private static void LoadEdges(glModel model, float[] edges)
    {
        GL.BindVertexArray(model.getVAOID());
        int buffer = storeDataInAttributeList(glModel.EdgeAttribute, 4, edges);
        GL.EnableVertexAttribArray(glModel.EdgeAttribute);
        unbindVAO();
        model.Own(buffer);
    }

    // Which way each vertex's growth is pushed out and how much of it there is (glModel.ShellAttribute), for the shells to read (SHELLS.md). Enabled in the vertex
    // array for good, as the edges are.
    private static void LoadShells(glModel model, float[] shells, int indexCount)
    {
        GL.BindVertexArray(model.getVAOID());
        int buffer = storeDataInAttributeList(glModel.ShellAttribute, 4, shells);
        GL.EnableVertexAttribArray(glModel.ShellAttribute);
        unbindVAO();
        model.SetShellData(buffer, indexCount);
    }

    // Puts every triangle anything can grow on first, in the order they came, and says how many indices those are: the shells draw that much of the index buffer
    // and nothing else, so a tree's leaves and the bark without moss on it, and the underside of a stone, cost the shells nothing. The order of a mesh's triangles
    // changes nothing else about how it's drawn. A stone's moss is Rock.glsl's to place, on whatever faces up (Shells/ShellVertex.glsl), so of a stone only what faces
    // less up than `facing` is left out (FloraMesh.ShellFacing).
    private static int ShellsFirst(int[] indices, float[] shells, float[] normals, byte styleNumber, float facing)
    {
        Rendering.Shells.ShellStyle style = Rendering.Shells.ShellStyles.Of(styleNumber)!;
        bool rock = style.Mask == Rendering.Shells.ShellMask.Rock;
        // Under this much, a growth is so few clumps so small (Shell.glsl's fill) that a triangle with no more than it at any corner shows nothing: a pine's trunk is
        // mostly a trace of moss, and drawing every layer of it cost more than the tree.
        const float Least = 0.08f;
        bool Grows(int v) => shells[v * 4 + 3] >= Least && (!rock || normals[v * 3 + 1] >= facing);

        int[] reordered = new int[indices.Length];
        int front = 0;
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            if (!(Grows(indices[t]) || Grows(indices[t + 1]) || Grows(indices[t + 2]))) continue;
            reordered[front++] = indices[t];
            reordered[front++] = indices[t + 1];
            reordered[front++] = indices[t + 2];
        }
        int growing = front;
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            if (Grows(indices[t]) || Grows(indices[t + 1]) || Grows(indices[t + 2])) continue;
            reordered[front++] = indices[t];
            reordered[front++] = indices[t + 1];
            reordered[front++] = indices[t + 2];
        }
        for (int i = front; i < indices.Length; i++) reordered[i] = indices[i];
        Array.Copy(reordered, indices, indices.Length);
        return growing;
    }

    // A mesh's shell data, or null if nothing grows on it. Vertices in one place are pushed out the same way, whatever their own normals, so that a seam or a hard
    // edge never opens a crack through what grows over it, and as far as the face leant furthest from needs to keep its depth. A vertex that isn't anywhere (a
    // position or normal that isn't a number, which a generator can make by accident) grows nothing, and is never looked up by its place, as it has none.
    private static float[]? ShellData(Mesh mesh)
    {
        Rendering.Shells.ShellStyle? style = Rendering.Shells.ShellStyles.Of(mesh.ShellStyle);
        if (style == null) return null;
        bool everywhere = style.Mask != Rendering.Shells.ShellMask.Vertex;
        int count = mesh.meshVertices.Count;
        var sums = new Dictionary<Vector3, Vector3>(count);
        bool grows = false;
        foreach (MeshVertex vertex in mesh.meshVertices)
        {
            if (!Finite(vertex.position) || !Finite(vertex.normal)) continue;
            sums[vertex.position] = sums.GetValueOrDefault(vertex.position) + vertex.normal;
            grows |= everywhere || vertex.Shell > 0f;
        }
        if (!grows) return null;

        var least = new Dictionary<Vector3, float>(sums.Count);
        foreach (MeshVertex vertex in mesh.meshVertices)
        {
            if (!Finite(vertex.position) || !Finite(vertex.normal)) continue;
            Vector3 outward = Outward(sums[vertex.position], vertex.normal);
            least[vertex.position] = MathF.Min(least.GetValueOrDefault(vertex.position, 1f), Vector3.Dot(outward, vertex.normal));
        }

        float[] data = new float[count * 4];
        for (int i = 0; i < count; i++)
        {
            MeshVertex vertex = mesh.meshVertices[i];
            Vector3 outward = Vector3.UnitY;
            float amount = 0f;
            if (Finite(vertex.position) && Finite(vertex.normal))
            {
                outward = Outward(sums[vertex.position], vertex.normal) / Math.Clamp(least[vertex.position], 0.5f, 1f);
                amount = everywhere ? 1f : Math.Clamp(vertex.Shell, 0f, 1f);
            }
            data[i * 4] = outward.X;
            data[i * 4 + 1] = outward.Y;
            data[i * 4 + 2] = outward.Z;
            data[i * 4 + 3] = amount;
        }
        return data;
    }

    // The way a place's faces face between them, or the vertex's own where they cancel out.
    private static Vector3 Outward(Vector3 sum, Vector3 own)
    {
        if (sum.LengthSquared > 1e-12f) return sum.Normalized();
        return own.LengthSquared > 1e-12f ? own.Normalized() : Vector3.UnitY;
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>
    /// What a vertex array without edges reads for them: none, anywhere; and without amounts of growth, nothing. A generic attribute is the context's, not an
    /// array's, so say it once.
    /// </summary>
    public static void SetNoEdges()
    {
        GL.VertexAttrib4(glModel.EdgeAttribute, Vertex.NoEdges.X, Vertex.NoEdges.Y, Vertex.NoEdges.Z, Vertex.NoEdges.W);
        GL.VertexAttrib4(glModel.ShellAttribute, 0f, 0f, 0f, 0f);    // A vec4's missing w would read as 1: as much as can grow.
    }

    // What a plant adds: which joint holds each vertex, and the joints themselves, which Plant.glsl walks up from there.
    private static void LoadPlant(glModel model, Mesh mesh)
    {
        float[] vertexJoints = new float[mesh.meshVertices.Count];
        for (int i = 0; i < vertexJoints.Length; i++)
        {
            vertexJoints[i] = mesh.meshVertices[i].joint;
        }
        LoadPlant(model, vertexJoints, mesh.joints!, PlantRig.Reach(mesh));
    }

    private static void LoadPlant(glModel model, float[] vertexJoints, List<PlantJoint> plantJoints, float reach)
    {
        GL.BindVertexArray(model.getVAOID());
        int vertexBuffer = storeDataInAttributeList(glModel.JointAttribute, 1, vertexJoints);
        GL.EnableVertexAttribArray(glModel.JointAttribute);
        unbindVAO();

        float[] joints = PlantRig.Pack(plantJoints);
        int buffer = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.TextureBuffer, buffer);
        GL.BufferData(BufferTarget.TextureBuffer, joints.Length * sizeof(float), joints, BufferUsageHint.StaticDraw);
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.TextureBuffer, texture);
        GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, buffer);
        GL.BindTexture(TextureTarget.TextureBuffer, 0);
        GL.BindBuffer(BufferTarget.TextureBuffer, 0);

        model.SetPlant(vertexBuffer, buffer, texture, reach);
    }

    public static glModel loadToVAO(float[] positions, float[] colors, float[] normals, int[] indices)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[4];
        VBOS[3] = bindIndicesBuffer(indices);

        VBOS[0] = storeDataInAttributeList(0, 3, positions);
        VBOS[1] = storeDataInAttributeList(1, 3, colors);
        VBOS[2] = storeDataInAttributeList(2, 3, normals);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length, new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue)));
    }
    public static glModel loadToVAO(float[] positions, float[] colors, float[] normals, float[] tangents, float[] uvs, float[] materialIndices, int[] indices)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[7];
        VBOS[6] = bindIndicesBuffer(indices);

        VBOS[0] = storeDataInAttributeList(0, 3, positions);
        VBOS[1] = storeDataInAttributeList(1, 3, colors);
        VBOS[2] = storeDataInAttributeList(2, 3, normals);
        VBOS[3] = storeDataInAttributeList(3, 3, tangents);
        VBOS[4] = storeDataInAttributeList(4, 2, uvs);
        VBOS[5] = storeDataInAttributeList(5, 1, materialIndices);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length, new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue)));
    }

    public static glModel loadToVAO(float[] positions, float[] colors, float[] normals, float[] tangents, float[] uvs, float[] materialIndices, float[] boneIndices, float[] boneWeights, int[] indices)
    {


        int vaoID = createVAO();
        int[] VBOS = new int[9];
        VBOS[8] = bindIndicesBuffer(indices);


        VBOS[0] = storeDataInAttributeList(0, 3, positions);
        VBOS[1] = storeDataInAttributeList(1, 3, colors);
        VBOS[2] = storeDataInAttributeList(2, 3, normals);
        VBOS[3] = storeDataInAttributeList(3, 3, tangents);
        VBOS[4] = storeDataInAttributeList(4, 2, uvs);
        VBOS[5] = storeDataInAttributeList(5, 1, materialIndices);
        VBOS[6] = storeDataInAttributeList(6, 2, boneIndices);
        VBOS[7] = storeDataInAttributeList(7, 2, boneWeights);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length, new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue)));
    }
    public static glModel loadToVAO(float[] positions, float[] normals, int[] indices)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[3];
        VBOS[2] = bindIndicesBuffer(indices);

        VBOS[0] = storeDataInAttributeList(0, 3, positions);
        VBOS[1] = storeDataInAttributeList(2, 3, normals);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length, new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue)));
    }
    public static glModel loadToVAO(float[] positions, int[] indices, int dimensions)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[2];
        VBOS[1] = bindIndicesBuffer(indices);
        VBOS[0] = storeDataInAttributeList(0, dimensions, positions);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length, new AABB(new Vector3(float.MaxValue), new Vector3(float.MinValue)));
    }

    private static int createVAO()
    {
        int vaoID = GL.GenVertexArray();
        GL.BindVertexArray(vaoID);
        return vaoID;
    }
    private static int bindIndicesBuffer(int[] indices)
    {
        int vboID = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, vboID);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticCopy);
        return vboID;
    }
    private static int storeDataInAttributeList(int attributeNumber, int coordinateSize, float[] data)
    {
        int vboID = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer,vboID);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(attributeNumber, coordinateSize, VertexAttribPointerType.Float, false, 0, 0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        //GL.EnableVertexAttribArray(attributeNumber);

        return vboID;
    }
    private static void unbindVAO()
    {
        GL.BindVertexArray(0);
    }
}
