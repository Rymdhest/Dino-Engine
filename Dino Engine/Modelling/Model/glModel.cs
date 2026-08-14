using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model;

public class glModel
{

    public class ImposterData
    {
        public int TextureIndex;
        public float DistanceSquared;
        public Vector2 Scale;
        public Vector2 BaseSize;
        public Vector3 LocalCenter; // Local offset to AABB center
        public Vector3 BaseLength; // Store raw Lx, Ly, Lz

        public ImposterData(int textureIndex, float distance, AABB box)
        {
            TextureIndex = textureIndex;
            this.DistanceSquared = distance*distance;

            Vector3 length = box.Max - box.Min;


            // 2. Local offset from mesh origin (0,0,0) to AABB 3D center0
            LocalCenter = (box.Min + box.Max) * 0.5f;
            float maxXZ = MathF.Sqrt((length.X * length.X) + (length.Z * length.Z));

            // The base size of the quad MUST match the ortho projection dimensions
            this.BaseSize = new Vector2(maxXZ, length.Y);
            this.Scale = this.BaseSize;
            this.BaseLength = length; // Raw unscaled dimensions
        }
    }

    private int vaoID;
    private int[] VBOS;
    private int vertexCount;
    public AABB box;
    public ImposterData? Imposter { get; set; } = null;

    public glModel(int vaoID, int[] VBOS, int vertexCount, AABB box)
    {
        this.vaoID = vaoID;
        this.VBOS = VBOS;
        this.vertexCount = vertexCount;
        this.box = box;
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
