using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// The wood of a plant, as points joined up. However a skeleton is grown it ends up as one of these, so that everything after it (thickness, skin, joints, where the leaves
    /// go) is written once. A point always comes after its parent, so going through them in order goes from the root outwards, and backwards from the tips in.
    /// </summary>
    public sealed class PlantGraph
    {
        public struct Node
        {
            public Vector3 Position;
            public int Parent;      // -1 for what stands on nothing.
            public int Order;       // How many times the wood has branched to get here: a trunk is 0.
            public float Radius;
            public float Scars;     // How many tips have been lost above here, as a limb keeps the girth they gave it (Palubicki 4.5).
            public float Light;     // How much reaches it, 0 to 1. See ShadowGrid.
            public bool Dead;       // Shed. It stays on the list, as numbers can't move while anything points at them.
        }

        public readonly List<Node> Nodes = new();

        public int Count => Nodes.Count;

        public int Add(Vector3 position, int parent, int order)
        {
            Nodes.Add(new Node { Position = position, Parent = parent, Order = order, Light = 1f });
            return Nodes.Count - 1;
        }

        /// <summary>
        /// How thick everything is, from the tips in, by the pipe model (Shinozaki 1964, by way of Runions 2007): r^n below a fork is the sum of r^n above it. A tip starts at
        /// <paramref name="tip"/>. Everything is then scaled so that the root comes to <paramref name="rootRadius"/>, as the number of tips is nothing to do with how thick a tree should be.
        /// </summary>
        public void Thicken(float tip, float exponent, float rootRadius)
        {
            int count = Nodes.Count;
            var pipes = new float[count];
            float tipPipe = MathF.Pow(tip, exponent);
            for (int i = count - 1; i >= 0; i--)
            {
                Node node = Nodes[i];
                if (node.Dead) continue;
                if (pipes[i] <= 0f) pipes[i] = tipPipe;
                pipes[i] += node.Scars * tipPipe;
                if (node.Parent >= 0) pipes[node.Parent] += pipes[i];
            }

            float rootPipes = 0f;
            for (int i = 0; i < count; i++)
            {
                if (Nodes[i].Parent < 0 && !Nodes[i].Dead) rootPipes = MathF.Max(rootPipes, pipes[i]);
            }
            float scale = rootRadius / MathF.Max(MathF.Pow(rootPipes, 1f / exponent), 1e-5f);

            for (int i = 0; i < count; i++)
            {
                Node node = Nodes[i];
                node.Radius = MathF.Pow(pipes[i], 1f / exponent) * scale;
                Nodes[i] = node;
            }
        }

        /// <summary>The children of every point, as a flat list and where each one's start. Dead wood has none and is nobody's.</summary>
        public void Children(out int[] start, out int[] list)
        {
            int count = Nodes.Count;
            start = new int[count + 1];
            for (int i = 0; i < count; i++)
            {
                if (Nodes[i].Parent >= 0 && !Nodes[i].Dead) start[Nodes[i].Parent + 1]++;
            }
            for (int i = 0; i < count; i++) start[i + 1] += start[i];

            list = new int[start[count]];
            var next = new int[count];
            Array.Copy(start, next, count);
            for (int i = 0; i < count; i++)
            {
                int parent = Nodes[i].Parent;
                if (parent >= 0 && !Nodes[i].Dead) list[next[parent]++] = i;
            }
        }

        /// <summary>Lets every point settle towards the line between its neighbours, which takes the kinks out of what grew in steps (after Runions 2007, section 3). Forks and tips stay put.</summary>
        public void Relax(float amount, int passes)
        {
            Children(out int[] start, out int[] list);
            var moved = new Vector3[Nodes.Count];
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Node node = Nodes[i];
                    moved[i] = node.Position;
                    if (node.Dead || node.Parent < 0 || start[i + 1] - start[i] != 1) continue;
                    Vector3 between = (Nodes[node.Parent].Position + Nodes[list[start[i]]].Position) * 0.5f;
                    moved[i] = Vector3.Lerp(node.Position, between, amount);
                }
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Node node = Nodes[i];
                    node.Position = moved[i];
                    Nodes[i] = node;
                }
            }
        }
    }

    /// <summary>A run of wood with no fork in it, from where it leaves its parent to its tip: what gets a skin and a few joints.</summary>
    public sealed class PlantAxis
    {
        public readonly List<Vector3> Points = new();
        public readonly List<float> Radii = new();
        public readonly List<int> Nodes = new();    // Which node of the graph each point came from.
        public readonly List<float> Lights = new(); // How much light each point stands in, if anything's said.
        public float NodeRings;                     // How often it needs a ring, for a cane.
        public PlantAxis? Parent;
        public int ParentPoint;     // Where on its parent it leaves.
        public int Order;
        public bool Root;           // Goes down and out into the ground, not up into the light.
        public bool Cut;            // Ends in a flat face of bare wood, and not a point.
        public bool CutFoot;        // And starts in one.

        // Filled in by the rig.
        public int FirstLink = -1;
        public int LinkCount;
        public int Riding = -1;     // With no links of its own, the link of something else that carries it.
        public float[] LinkEnds = Array.Empty<float>();  // How far along each link stops, 0 to 1.

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Points.Count; i++) length += (Points[i] - Points[i - 1]).Length;
                return length;
            }
        }

        /// <summary>
        /// Splits a graph into axes, parents first. At every fork the thickest child goes on as the same axis, and the rest start new ones, so a limb's order is how many times you
        /// have to turn off the main line to reach it. Wood thinner than <paramref name="cut"/> isn't skinned at all: that's what's on the cards.
        /// </summary>
        public static List<PlantAxis> From(PlantGraph graph, float cut)
        {
            graph.Children(out int[] start, out int[] list);
            var axes = new List<PlantAxis>();
            var waiting = new Queue<(int node, PlantAxis? parent, int parentPoint)>();
            for (int i = 0; i < graph.Count; i++)
            {
                if (graph.Nodes[i].Parent < 0 && !graph.Nodes[i].Dead) waiting.Enqueue((i, null, 0));
            }

            while (waiting.Count > 0)
            {
                (int first, PlantAxis? parent, int parentPoint) = waiting.Dequeue();
                var axis = new PlantAxis { Parent = parent, ParentPoint = parentPoint, Order = parent == null ? 0 : parent.Order + 1 };

                // It starts inside what it grew from, so that no gap shows at the join.
                if (parent != null)
                {
                    axis.Points.Add(parent.Points[parentPoint]);
                    axis.Radii.Add(MathF.Min(graph.Nodes[first].Radius, parent.Radii[parentPoint]));
                    axis.Nodes.Add(parent.Nodes[parentPoint]);
                    axis.Lights.Add(graph.Nodes[first].Light);
                }

                int at = first;
                while (true)
                {
                    PlantGraph.Node node = graph.Nodes[at];
                    axis.Points.Add(node.Position);
                    axis.Radii.Add(node.Radius);
                    axis.Nodes.Add(at);
                    axis.Lights.Add(node.Light);

                    int best = -1;
                    float thickest = cut;
                    for (int c = start[at]; c < start[at + 1]; c++)
                    {
                        float radius = graph.Nodes[list[c]].Radius;
                        if (radius < thickest) continue;
                        thickest = radius;
                        best = list[c];
                    }
                    for (int c = start[at]; c < start[at + 1]; c++)
                    {
                        int child = list[c];
                        if (child != best && graph.Nodes[child].Radius >= cut) waiting.Enqueue((child, axis, axis.Points.Count - 1));
                    }
                    if (best < 0) break;
                    at = best;
                }

                if (axis.Points.Count >= 2) axes.Add(axis);
            }
            return axes;
        }
    }
}
