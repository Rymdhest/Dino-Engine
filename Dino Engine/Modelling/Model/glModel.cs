using Dino_Engine.Physics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model;

public class glModel
{
    /// <summary>Where a vertex's joint is, on a plant. Above the instance matrix, which takes 6 to 9.</summary>
    public const int JointAttribute = 10;

    /// <summary>Where a mesh's <see cref="Vertex.Edges"/> go, for those that have them: everything else reads the default, which is no edges at all.</summary>
    public const int EdgeAttribute = 11;

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
    private Vector3 _boundsCentre;
    private float _boundsRadius;
    private bool _boundsKnown;
    private int _jointBuffer;
    public AABB box;
    public ImposterData? Imposter { get; set; } = null;

    /// <summary>Drawn by nothing while it's set: what's inside a hall, while nobody's near enough to see it (HallCutawaySystem).</summary>
    public bool Hidden { get; set; }

    public glModel(int vaoID, int[] VBOS, int vertexCount, AABB box)
    {
        this.vaoID = vaoID;
        this.VBOS = VBOS;
        this.vertexCount = vertexCount;
        this.box = box;
    }

    /// <summary>The buffer texture of a plant's joints (Plant.glsl), or 0 for whatever doesn't bend.</summary>
    public int JointTexture { get; private set; }

    /// <summary>How far a plant can reach from where it rests.</summary>
    public float PlantReach { get; private set; }

    /// <summary>Takes a buffer the model's vertex array reads, to delete with the rest.</summary>
    public void Own(int buffer)
    {
        Array.Resize(ref VBOS, VBOS.Length + 1);
        VBOS[^1] = buffer;
    }

    public void SetPlant(int vertexJointBuffer, int jointBuffer, int jointTexture, float reach)
    {
        Array.Resize(ref VBOS, VBOS.Length + 1);
        VBOS[^1] = vertexJointBuffer;
        _jointBuffer = jointBuffer;
        JointTexture = jointTexture;
        PlantReach = reach;
        _boundsKnown = false;
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
    /// <summary>The size of the model's box in its own units, which is what says whether it's big enough to open for the player (FocusOccluder.glsl). A model with no box counts as huge.</summary>
    public Vector3 Size => box.Min.X > box.Max.X ? new Vector3(float.MaxValue) : box.Max - box.Min;

    /// <summary>
    /// Whether it thins away for the player standing behind it (FocusCutout.glsl), if it's big enough to hide them. Only what's said to does: a tree, and nothing else
    /// unless someone asks for it, as every new kind of thing that opened by itself read as a fault. <see cref="World.WorldAssetLibrary"/> says it of trees.
    /// </summary>
    public bool OpensForFocus;

    public void WorldBounds(in Matrix4 localToWorld, out Vector3 centre, out float radius)
    {
        if (!_boundsKnown)
        {
            FindLocalBounds();
            _boundsKnown = true;
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
        _boundsRadius = (box.Max - box.Min).Length * 0.5f + PlantReach;
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
        if (JointTexture != 0)
        {
            GL.DeleteTexture(JointTexture);
            GL.DeleteBuffer(_jointBuffer);
        }
    }
}
