using Dino_Engine.Modelling.Model;
using OpenTK.Graphics.OpenGL;

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

        return loadToVAO(mesh.getAllPositionsArray(), mesh.getAllColoursFloatArray(), mesh.getAllNormalsArray(),mesh.getAllTangentsArray(), mesh.getAllUVsArray(), mesh.getAllMaterialIndicesArray(), indices);
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
        return new glModel(vaoID, VBOS, indices.Length);
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
        return new glModel(vaoID, VBOS, indices.Length);
    }
    public static glModel loadToVAO(float[] positions, float[] normals, int[] indices)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[3];
        VBOS[2] = bindIndicesBuffer(indices);

        VBOS[0] = storeDataInAttributeList(0, 3, positions);
        VBOS[1] = storeDataInAttributeList(1, 3, normals);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length);
    }
    public static glModel loadToVAO(float[] positions, int[] indices, int dimensions)
    {
        int vaoID = createVAO();
        int[] VBOS = new int[2];
        VBOS[1] = bindIndicesBuffer(indices);
        VBOS[0] = storeDataInAttributeList(0, dimensions, positions);
        unbindVAO();
        return new glModel(vaoID, VBOS, indices.Length);
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

        return vboID;
    }
    private static void unbindVAO()
    {
        GL.BindVertexArray(0);
    }
}
