using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>How one line of a limb's foot came to end where it does (BarkSkin.Land).</summary>
    public enum FootKind : byte
    {
        Tucked,     // A twig's: under the parent's bark by the lie of it, with nothing looked for.
        Sunk,       // The limb's first ring was already inside the parent there, as in a tight fork.
        Brought,    // It never went under, on a limb wider than its parent, and was brought in to it.
        Laid,       // It ended where it went under, on a face of the parent.
        Turned,     // It turned to lie along the parent, and ended on a face of it.
        Set,        // No face was found where it ended, and it was set on the parent's smooth shape.
    }

    /// <summary>Where a limb left what it grew from, as the skin made it: for the lab to look at and to hold (Dino FloraLab joins).</summary>
    public sealed class FloraJoin
    {
        public FootKind[] Kinds = Array.Empty<FootKind>();  // How each vertex of its foot came to be where it is.
        public Vector3 At;                  // Where the limb's heart comes out of its parent.
        public Vector3 Out;                 // The way out of the parent there.
        public float Radius;                // The limb's,
        public float HostRadius;            // and its parent's.
        public bool Lands;                  // Its foot is laid on the parent's faces: a twig's is only tucked under them.
        public int FirstVertex;             // The vertices of its foot, in the mesh,
        public int Vertices;                // and how many they are.
        public (int First, int Count)[] Faces = Array.Empty<(int, int)>();  // The parent's triangles near it, as runs of the mesh's indices.
    }

    /// <summary>
    /// What the lab asks of the skin's joins. Nothing here is read by anything that grows a plant: a list to fill, which is only filled on the thread that set it, and a count
    /// of how the feet of limbs fared.
    /// </summary>
    public static class FloraJoins
    {
        /// <summary>Set to a list to have every join of whatever this thread grows next written to it.</summary>
        [ThreadStatic] public static List<FloraJoin>? Watch;

        /// <summary>How many lines of limbs' feet found a face of their parent to end on since <see cref="Reset"/>, and how many were set on its smooth shape instead.</summary>
        public static (int Landed, int Unlanded) Counts => (BarkSkin.Landed, BarkSkin.Unlanded);

        public static void Reset()
        {
            BarkSkin.Landed = 0;
            BarkSkin.Unlanded = 0;
        }
    }
}
