using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The joints of a tree. Each axis near the trunk gets a short chain, as anything long has to or the side of it stretches, and whatever's finer rides on what it grew from. A vertex
    /// walks up to PlantRig.MostLevels joints, so chains are kept to what adds up to that with a card on the end: two on a trunk, two on a limb, one on a branch, one on a card.
    /// </summary>
    internal static class Rig
    {
        private static readonly int[] LinksByOrder = { 2, 2, 1 };
        private const float ShortestLink = 0.05f;   // Metres. Less than this is nothing to bend.
        private const float StiffRadius = 0.09f;    // Metres: thinner than this bends as far as a joint can, and thicker less in proportion.
        private const float MostStretch = 0.4f;     // How much longer the outside of a bend can get at the very limit of it.

        /// <returns>For each node of the graph, the joint nearest it: what a card on that node hangs from.</returns>
        /// <param name="walkable">It's low enough to be walked through, so every part of it answers to touch. Nothing does up in a tree.</param>
        public static int[] Link(FloraMesh mesh, List<PlantAxis> axes, int nodes, bool walkable = false)
        {
            var nodeJoint = new int[nodes];
            Array.Fill(nodeJoint, -1);

            foreach (PlantAxis axis in axes)
            {
                // Bark's light comes down to a floor, as a limb inside a crown is dark and not gone.
                for (int i = 0; i < axis.Lights.Count; i++) axis.Lights[i] = 0.32f + 0.68f * axis.Lights[i];

                int parentLink = axis.Parent != null ? CarrierAt(axis.Parent, ShareAt(axis.Parent, axis.ParentPoint)) : -1;
                int links = axis.Order < LinksByOrder.Length ? LinksByOrder[axis.Order] : 0;
                if (axis.Root || axis.Length < ShortestLink * links) links = 0;

                if (links == 0 && parentLink >= 0)
                {
                    axis.Riding = parentLink;
                    axis.LinkCount = 0;
                    axis.FirstLink = axis.Length < ShortestLink ? parentLink : mesh.AddJoint(PlantJoint.Rigid.Between(axis.Points[0], axis.Points[^1], parentLink));
                }
                else
                {
                    links = Math.Max(links, 1);
                    AddChain(mesh, axis, links, parentLink, walkable);
                }

                for (int i = 0; i < axis.Nodes.Count; i++)
                {
                    int node = axis.Nodes[i];
                    if (node >= 0) nodeJoint[node] = CarrierAt(axis, ShareAt(axis, i));
                }
            }
            return nodeJoint;
        }

        private static void AddChain(FloraMesh mesh, PlantAxis axis, int links, int parent, bool walkable)
        {
            var points = new Vector3[links + 1];
            axis.LinkEnds = new float[links];
            for (int i = 0; i <= links; i++)
            {
                float share = (float)i / links;
                points[i] = PointAt(axis, share);
                if (i > 0) axis.LinkEnds[i - 1] = share;
            }

            PlantJoint like = walkable ? PlantJoint.Stem with { Give = 0.6f, MostBend = 0.8f }
                : axis.Order == 0 ? PlantJoint.Trunk : axis.Order == 1 ? PlantJoint.Bough : PlantJoint.Spray with { Flutter = 1f };

            // Thick wood bends less than thin wood, by a long way: a limb as thick as a trunk doesn't whip about like a twig.
            float stiffness = axis.Order == 0 ? 1f : Math.Clamp(StiffRadius / MathF.Max(axis.Radii[0], 1e-3f), 0.2f, 1f);
            like.Give *= stiffness;
            like.MostBend *= stiffness;

            axis.FirstLink = mesh.Joints.Count;
            axis.LinkCount = links;
            PlantJoint[] chain = like.Chain(points);
            for (int i = 0; i < chain.Length; i++)
            {
                chain[i].Parent = parent;
                chain[i].MostBend = MathF.Min(chain[i].MostBend, BendThatHolds(axis, chain[i], i / (float)links, (i + 1f) / links));
                parent = mesh.AddJoint(chain[i]);
            }
        }

        // A joint is a straight line, and the outside of a bend stretches by how far it stands off that line for how long the line is. So what's stubby can hardly bend, and nor can a
        // limb that curls back on itself, whose line is much shorter than it is.
        private static float BendThatHolds(PlantAxis axis, in PlantJoint link, float from, float to)
        {
            float furthest = 1e-3f;
            float travelled = 0f;
            float length = MathF.Max(axis.Length, 1e-3f);
            for (int i = 0; i < axis.Points.Count; i++)
            {
                if (i > 0) travelled += (axis.Points[i] - axis.Points[i - 1]).Length;
                float share = travelled / length;
                if (share < from - 0.01f || share > to + 0.01f) continue;
                Vector3 onLine = link.Base + (link.Tip - link.Base) * PlantRig.Along(link, axis.Points[i]);
                furthest = MathF.Max(furthest, (axis.Points[i] - onLine).Length + axis.Radii[i]);
            }
            return MostStretch * link.Length / furthest;
        }

        // What something growing from here hangs from. Never a rigid joint, or fine wood on fine wood would go deeper than a vertex can walk.
        private static int CarrierAt(PlantAxis axis, float share) => axis.LinkCount > 0 ? LinkAt(axis, share) : axis.Riding;

        public static int LinkAt(PlantAxis axis, float share)
        {
            if (axis.LinkCount == 0) return axis.FirstLink;
            for (int i = 0; i < axis.LinkCount; i++)
            {
                if (share <= axis.LinkEnds[i] + 1e-4f) return axis.FirstLink + i;
            }
            return axis.FirstLink + axis.LinkCount - 1;
        }

        public static float ShareAt(PlantAxis axis, int point)
        {
            float length = axis.Length;
            if (length <= 1e-4f) return 0f;
            float travelled = 0f;
            for (int i = 1; i <= point && i < axis.Points.Count; i++) travelled += (axis.Points[i] - axis.Points[i - 1]).Length;
            return travelled / length;
        }

        public static Vector3 PointAt(PlantAxis axis, float share)
        {
            float wanted = axis.Length * share;
            float travelled = 0f;
            for (int i = 1; i < axis.Points.Count; i++)
            {
                float step = (axis.Points[i] - axis.Points[i - 1]).Length;
                if (travelled + step >= wanted) return Vector3.Lerp(axis.Points[i - 1], axis.Points[i], step > 1e-5f ? (wanted - travelled) / step : 0f);
                travelled += step;
            }
            return axis.Points[^1];
        }
    }
}
