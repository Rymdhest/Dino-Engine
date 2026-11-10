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
        glModel model;
        if (mesh.isUsingBones)
        {
            model = loadToVAO(mesh.getAllPositionsArray(), mesh.getAllColoursFloatArray(), mesh.getAllNormalsArray(), mesh.getAllTangentsArray(), mesh.getAllUVsArray(), mesh.getAllMaterialIndicesArray(), mesh.getAllBoneIndicesArray(), mesh.getAllBoneWeightsArray(), indices);
        } else
        {
            model = loadToVAO(mesh.getAllPositionsArray(), mesh.getAllColoursFloatArray(), mesh.getAllNormalsArray(), mesh.getAllTangentsArray(), mesh.getAllUVsArray(), mesh.getAllMaterialIndicesArray(), indices);
        }

        model.box = mesh.createAABB();
        if (mesh.joints != null) LoadPlant(model, mesh);
        if (mesh.getAllEdgesArray() is float[] edges) LoadEdges(model, edges);
        return model;
    }

    /// <summary>A plant, straight from the lists it was grown in.</summary>
    public static glModel loadToVAO(FloraMesh plant)
    {
        int count = plant.VertexCount;
        float[] positions = new float[count * 3], colours = new float[count * 3], normals = new float[count * 3], tangents = new float[count * 3];
        float[] uvs = new float[count * 2], materials = new float[count], joints = new float[count];
        for (int i = 0; i < count; i++)
        {
            FloraVertex vertex = plant.Vertices[i];
            positions[i * 3] = vertex.Position.X; positions[i * 3 + 1] = vertex.Position.Y; positions[i * 3 + 2] = vertex.Position.Z;
            colours[i * 3] = vertex.Colour.X; colours[i * 3 + 1] = vertex.Colour.Y; colours[i * 3 + 2] = vertex.Colour.Z;
            normals[i * 3] = vertex.Normal.X; normals[i * 3 + 1] = vertex.Normal.Y; normals[i * 3 + 2] = vertex.Normal.Z;
            tangents[i * 3] = vertex.Tangent.X; tangents[i * 3 + 1] = vertex.Tangent.Y; tangents[i * 3 + 2] = vertex.Tangent.Z;
            uvs[i * 2] = vertex.UV.X; uvs[i * 2 + 1] = vertex.UV.Y;
            materials[i] = vertex.Material;
            joints[i] = vertex.Joint;
        }

        glModel model = loadToVAO(positions, colours, normals, tangents, uvs, materials, plant.Indices.ToArray());
        plant.Bounds(out Vector3 min, out Vector3 max);
        model.box = new AABB(min, max);
        if (plant.Joints.Count > 0) LoadPlant(model, joints, plant.Joints, PlantRig.Reach(plant));
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

    /// <summary>What a vertex array without edges reads for them: none, anywhere. A generic attribute is the context's, not an array's, so say it once.</summary>
    public static void SetNoEdges()
    {
        GL.VertexAttrib4(glModel.EdgeAttribute, Vertex.NoEdges.X, Vertex.NoEdges.Y, Vertex.NoEdges.Z, Vertex.NoEdges.W);
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
