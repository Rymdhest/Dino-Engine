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
    public float swayAmount;
    // What Wind.glsl's VEGETATION_MAX_TOTAL_BEND holds a plant to.
    private const float MostBend = 1.5f;
    private Vector3 _boundsCentre;
    private float _boundsRadius;
    private float _boundsSway = float.NaN;
    public AABB box;
    public ImposterData? Imposter { get; set; } = null;

    public glModel(int vaoID, int[] VBOS, int vertexCount, AABB box)
    {
        this.vaoID = vaoID;
        this.VBOS = VBOS;
        this.vertexCount = vertexCount;
        this.box = box;
        swayAmount = 0.0f;
    }

    public int getVAOID()
    {
        return vaoID;
    }


    public int getVertexCount()
    {
        return vertexCount;
    }

    /// <summary>
    /// The sphere, in world space, that everything this model draws stays inside, as far over as the wind can ever lean it. Culling rests on this: what it
    /// leaves out, the model must never reach. A model with no box has no bounds, and its radius says so.
    /// </summary>
    public void WorldBounds(in Matrix4 localToWorld, out Vector3 centre, out float radius)
    {
        if (_boundsSway != swayAmount)
        {
            FindLocalBounds();
            _boundsSway = swayAmount;
        }

        centre = Vector3.TransformPosition(_boundsCentre, localToWorld);
        float scale = MathF.Max(localToWorld.Row0.Xyz.LengthSquared, MathF.Max(localToWorld.Row1.Xyz.LengthSquared, localToWorld.Row2.Xyz.LengthSquared));
        radius = _boundsRadius * MathF.Sqrt(scale);
    }

    private void FindLocalBounds()
    {
        if (box.Min.X > box.Max.X)
        {
            _boundsCentre = Vector3.Zero;
            _boundsRadius = float.PositiveInfinity;
            return;
        }

        _boundsCentre = (box.Min + box.Max) * 0.5f;
        _boundsRadius = (box.Max - box.Min).Length * 0.5f;

        // Wind turns a model about its own origin, by the amount Model_Instanced.vert works out for its furthest vertex.
        Vector3 furthest = Vector3.ComponentMax(Vector3.ComponentMax(box.Min, -box.Min), Vector3.ComponentMax(box.Max, -box.Max));
        float turn = MostBend * (furthest.Y * 0.001f + furthest.Xz.Length * 0.01f) * swayAmount;
        _boundsRadius += MathF.Min(turn * MathF.Sqrt(2f), 2f) * furthest.Length;
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
