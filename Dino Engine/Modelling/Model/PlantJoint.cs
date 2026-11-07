using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Model
{
    /// <summary>
    /// One thing on a plant that bends: a trunk, a bough, a stem, a leaf. It runs from <see cref="Base"/> to <see cref="Tip"/> and hangs off its
    /// <see cref="Parent"/>, and every vertex tied to it reads what's pushing the plant once, here, and not where that vertex happens to be. That is what
    /// holds a plant together: all of a leaf moves as one, however sharply what's around it changes from one side of it to the other.
    /// </summary>
    public struct PlantJoint
    {
        public Vector3 Base;
        public Vector3 Tip;
        public int Parent;      // Another joint of the same mesh, or -1 for one that stands on the ground.
        public float Give;      // How much of the lean of the plants around it this takes: radians at its tip, for each radian of theirs.
        public float MostBend;  // Radians its tip can ever reach. It comes to this gently, so nothing stops dead.
        public float Touch;     // How much of what walks through it reaches it, 0..1. Nothing does up in a tree.
        public float Flutter;   // How much it shivers of its own accord, as a share of what WindSingleton.VegetationFlutter says.
        public bool Heavy;      // Follows the canopy's slow spring rather than the quick one, as a trunk has too much mass to answer a gust at once.

        public readonly float Length => (Tip - Base).Length;

        private static PlantJoint Of(float give, float mostBend, float touch, float flutter, bool heavy = false)
        {
            return new PlantJoint { Parent = -1, Give = give, MostBend = mostBend, Touch = touch, Flutter = flutter, Heavy = heavy };
        }

        // Up in a tree, out of reach of anyone walking.
        public static PlantJoint Trunk => Of(0.1f, 0.22f, 0f, 0f, heavy: true);
        public static PlantJoint Bough => Of(0.3f, 0.7f, 0f, 0.5f);
        public static PlantJoint Spray => Of(0.45f, 0.55f, 0f, 2.5f);
        public static PlantJoint Cane => Of(0.42f, 0.55f, 0f, 0.3f);

        // Down where a body goes.
        public static PlantJoint Stem => Of(0.9f, 1.35f, 1f, 1f);
        public static PlantJoint Blade => Of(0.55f, 0.75f, 0.8f, 1.5f);     // A link of a chain, whose bends add up.
        public static PlantJoint Frond => Of(0.4f, 0.5f, 0.7f, 1.5f);       // Likewise, and wide.
        public static PlantJoint Leaf => Of(0.5f, 0.7f, 0.6f, 3f);
        public static PlantJoint Head => Of(0.6f, 0.6f, 0.4f, 2f);

        /// <summary>
        /// The links of a chain through <paramref name="points"/>, for whoever's adding them to hang each from the one before. A link's share of the bend goes by
        /// its share of the length, so that the whole chain curves evenly and no short link folds.
        /// </summary>
        public readonly PlantJoint[] Chain(Vector3[] points)
        {
            float total = 0f;
            for (int i = 0; i + 1 < points.Length; i++) total += (points[i + 1] - points[i]).Length;

            var links = new PlantJoint[points.Length - 1];
            for (int i = 0; i < links.Length; i++)
            {
                float share = (points[i + 1] - points[i]).Length / MathF.Max(total, 1e-4f) * links.Length;
                links[i] = Between(points[i], points[i + 1]);
                links[i].Give *= share;
                links[i].MostBend *= share;
            }
            return links;
        }

        /// <summary>This joint, running from one point to another.</summary>
        public readonly PlantJoint Between(Vector3 from, Vector3 to, int parent = -1)
        {
            PlantJoint joint = this;
            joint.Base = from;
            joint.Tip = to;
            joint.Parent = parent;
            return joint;
        }
    }
}
