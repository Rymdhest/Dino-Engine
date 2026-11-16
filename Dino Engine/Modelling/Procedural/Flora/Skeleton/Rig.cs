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
        private const float StubbyLengths = 6f;     // A limb with less than this many of its own radii clear of what it grew from is carried stiffly, and with twice that bends as one.

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
                // Only what's clear of what it grew from can bend, and there has to be enough of that to bend.
                float leaves = axis.Parent != null ? Leaves(axis) : 0f;
                float free = axis.Length * (1f - leaves);
                if (axis.Root || axis.Bare || free < ShortestLink * links) links = 0;
                // And what's stubby doesn't bend at all: a stick a few times as long as it's thick is stiff, and a joint that short folds whatever's on it.
                if (axis.Parent != null && free < axis.Radii[0] * StubbyLengths) links = 0;
                else if (axis.Parent != null && free < axis.Radii[0] * StubbyLengths * 2f) links = Math.Min(links, 1);

                if (links == 0 && parentLink >= 0)
                {
                    axis.Riding = parentLink;
                    axis.LinkCount = 0;
                    // What rides on something that itself only rides goes with it, as one piece: by a joint of its own it would be carried by where its own foot is along
                    // whatever carries them both, which for a twig at the end of a long limb is somewhere else, and the two would part at the twig's foot.
                    PlantAxis from = axis.Parent!;
                    if (from.LinkCount == 0 && from.FirstLink >= 0 && from.FirstLink != from.Riding) axis.FirstLink = from.FirstLink;
                    else axis.FirstLink = axis.Length < ShortestLink ? parentLink : mesh.AddJoint(PlantJoint.Rigid.Between(axis.Points[0], axis.Points[^1], parentLink));
                }
                else
                {
                    links = Math.Max(links, 1);
                    AddChain(mesh, axis, links, parentLink, walkable, leaves);
                }

                for (int i = 0; i < axis.Nodes.Count; i++)
                {
                    int node = axis.Nodes[i];
                    if (node >= 0) nodeJoint[node] = CarrierAt(axis, ShareAt(axis, i));
                }
            }
            return nodeJoint;
        }

        // How far along an axis is clear of the wood it grew from and of the collar it leaves it through, as a share of its length (as near as can be said before there's a skin:
        // BarkSkin.Collar.Plan is what settles it). That's where it starts to bend. A limb bends beyond its collar, not from the trunk's heart: hinged at the heart of a thick
        // trunk it swings its foot across the bark as it sways, away from the collar that holds it there, and hinged where it comes out it folds the collar itself.
        private static float Leaves(PlantAxis axis)
        {
            PlantAxis parent = axis.Parent!;
            int at = Math.Clamp(axis.ParentPoint, 0, parent.Points.Count - 1);
            float radius = parent.Radii[at];
            float length = axis.Length;
            Vector3 line = parent.Points[Math.Min(at + 1, parent.Points.Count - 1)] - parent.Points[Math.Max(at - 1, 0)];
            if (line.LengthSquared < 1e-8f || length < 1e-4f) return 0f;
            line.Normalize();

            float Off(Vector3 point)
            {
                Vector3 from = point - parent.Points[at];
                return (from - line * Vector3.Dot(from, line)).Length;
            }

            float travelled = 0f;
            for (int i = 1; i < axis.Points.Count && travelled < length * 0.5f; i++)
            {
                float step = (axis.Points[i] - axis.Points[i - 1]).Length;
                float there = Off(axis.Points[i]);
                if (there >= radius)
                {
                    float here = Off(axis.Points[i - 1]);
                    float t = there > here ? Math.Clamp((radius - here) / (there - here), 0f, 1f) : 0f;
                    // Its upper side comes out later than its heart, by how steeply it leaves, and its collar reaches a way beyond that.
                    Vector3 heading = step > 1e-6f ? (axis.Points[i] - axis.Points[i - 1]) / step : line;
                    float sine = Math.Clamp((heading - line * Vector3.Dot(heading, line)).Length, 0.15f, 1f);
                    float cotangent = MathF.Min(MathF.Sqrt(1f - sine * sine) / sine, 4f);
                    float clear = travelled + step * t + axis.Radii[0] * (1.15f * cotangent + 1.8f);
                    return Math.Clamp(clear / length, 0f, 0.6f);
                }
                travelled += step;
            }
            // It's inside its parent for half its length: something stubby on something thick. It bends from as far out as it can.
            return Math.Clamp(radius / length, 0f, 0.6f);
        }

        private static void AddChain(FloraMesh mesh, PlantAxis axis, int links, int parent, bool walkable, float from)
        {
            var points = new Vector3[links + 1];
            axis.LinkEnds = new float[links];
            for (int i = 0; i <= links; i++)
            {
                float share = from + (1f - from) * i / links;
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
                chain[i].MostBend = MathF.Min(chain[i].MostBend, BendThatHolds(axis, chain[i], from + (1f - from) * i / links, from + (1f - from) * (i + 1f) / links));
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
