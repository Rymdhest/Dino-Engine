using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;

namespace Dino_Engine.Modelling.Model;

public class glModel
{

    public class ImposterData
    {
        public int TextureIndex;   // The layer index in your Texture2DArray
        public float MaxDistance;  // Distance at which it swaps to imposter

        public ImposterData(int textureIndex, float maxDistance)
        {
            TextureIndex = textureIndex;
            MaxDistance = maxDistance;
        }
    }

    private int vaoID;
    private int[] VBOS;
    private int vertexCount;
    public AABB? box = null;
    public ImposterData? Imposter { get; set; } = null;

    public glModel(int vaoID, int[] VBOS, int vertexCount)
    {
        this.vaoID = vaoID;
        this.VBOS = VBOS;
        this.vertexCount = vertexCount;
    }

    public int getVAOID()
    {
        return vaoID;
    }


    public int getVertexCount()
    {
        return vertexCount;
    }

    public override string ToString()
    {
        return $"VAO ID: {vaoID}";
    }

    public void cleanUp()
    {
        GL.DeleteVertexArray(vaoID);
        for (int i = 0; i < VBOS.Length; i++)
        {
            GL.DeleteBuffer(VBOS[i]);
        }
    }
}
