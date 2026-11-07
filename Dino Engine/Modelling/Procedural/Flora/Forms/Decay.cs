using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>What happens to a tree after it dies, as a change to the same skeleton a living one has, so a dead pine is still a pine.</summary>
    public sealed class Decay
    {
        public FloatRange Broken = new FloatRange(0.5f, 0.85f);     // How much of the trunk is left.
        public FloatRange Stubs = new FloatRange(0.45f, 0.95f);     // How much of each branch is left.
        public float Lost = 0.22f;                                  // The chance of a branch being gone altogether.

        public void Apply(PlantGraph graph, List<PlantAxis> axes, Genes genes)
        {
            WorldRandom random = genes.Stream("decay");
            var gone = new HashSet<PlantAxis>();
            float height = genes.Range("broken", Broken);

            foreach (PlantAxis axis in axes)
            {
                if (axis.Parent != null && gone.Contains(axis.Parent))
                {
                    gone.Add(axis);
                    continue;
                }
                if (axis.Parent == null)
                {
                    Cut(axis, height);
                    continue;
                }
                // What's left of a branch that the trunk no longer reaches, or that's snapped.
                bool beyond = axis.ParentPoint >= axis.Parent.Points.Count - 1;
                if (beyond || random.Float() < Lost * (axis.Order + 0.5f) * 0.6f)
                {
                    gone.Add(axis);
                    continue;
                }
                Cut(axis, random.Range(Stubs));
            }
            axes.RemoveAll(gone.Contains);
        }

        private static void Cut(PlantAxis axis, float share)
        {
            int keep = Math.Clamp((int)(axis.Points.Count * share), 2, axis.Points.Count);
            int extra = axis.Points.Count - keep;
            if (extra <= 0) return;
            axis.Points.RemoveRange(keep, extra);
            axis.Radii.RemoveRange(keep, extra);
            axis.Nodes.RemoveRange(keep, extra);
        }
    }
}
