using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>The main stem. Weber and Penn's level 0, with their Flare and Lobes.</summary>
    public sealed class TrunkForm
    {
        public IntRange Stems = new IntRange(1, 1);             // Several, for what grows from one root as a clump.
        public FloatRange StemSpread = new FloatRange(0f, 0f);  // Metres apart at the foot.
        public FloatRange Reach = new FloatRange(1f, 1f);       // How much of the tree's height it goes up: a broadleaf hands over to its limbs long before the top.
        public FloatRange Girth = new FloatRange(0.03f, 0.04f); // Radius at the foot, as a share of the tree's height.
        public FloatRange Lean = new FloatRange(0f, 0.06f);     // Radians off upright at the foot.
        public StemCurve Curve = new StemCurve { Wander = 0.018f, Waves = 2.5f };
        public FloatRange Flare = new FloatRange(0.35f, 0.6f);  // How much wider the foot is than the bole.
        public float FlareHeight = 0.06f;                       // As a share of the tree's height.
        public IntRange Lobes = new IntRange(5, 7);             // Ridges running up from the roots. Odd numbers look less made.
        public FloatRange LobeDepth = new FloatRange(0.12f, 0.22f);
        public float Step = 1.2f;                               // Metres between points, which is how often it can bear a limb or a bud.
    }

    /// <summary>Stems that run out from the foot of a trunk and go into the ground.</summary>
    public sealed class RootForm
    {
        public FloatRange Reach = new FloatRange(0.1f, 0.16f);  // As a share of the tree's height.
        public FloatRange Girth = new FloatRange(0.3f, 0.45f);  // As a share of the trunk's radius.
        public float Rise = 0.5f;       // How high up the trunk a root starts, in trunk radii.
    }

    /// <summary>One level of the scaffold: Weber and Penn's nBranches, nDownAngle, nRotate and nLength, with the crown as their pruning envelope.</summary>
    public sealed class LimbForm
    {
        public FloatRange Count = new FloatRange(6f, 9f);   // How many groups along a stem, unless Spacing says.
        public float Spacing;                               // Metres between groups, if more than 0: for what grows in tiers whatever its height.
        public int Whorl = 1;                               // How many come out together.
        public float Missing;                               // The chance of any one of them not being there.
        public float From = 0.4f;
        public float To = 1f;
        public float Jitter = 0.5f;                         // How far they stray from even spacing, in spaces.
        public float Divergence = 137.5f;                   // Degrees from one group to the next.
        public float DivergenceJitter = 0.25f;              // Radians.
        public Ramp Angle = new Ramp(1.1f, 0.6f);           // Radians off the parent, from the bottom of the stem to the top.
        public float AngleJitter = 0.15f;
        public Ramp Length = new Ramp(0.35f, 0.6f);         // What a random one comes to, between these.
        public Ramp Profile = 1f;                           // And how that goes along the stem.
        public bool OfParent;                               // Length is a share of what's left of the parent, and not of the tree's height.
        public float Shrink = 0.6f;                         // How much further up the parent matters, if so: Weber and Penn's 0.6.
        public bool Fit = true;                             // Stops at the crown's edge.
        public float FitRoom = 0.1f;
        public float Shortest = 0.5f;
        public float Wander = 0.06f;
        public float Waves = 2f;
        public Ramp Rise = 0.02f;                           // Radians a metre towards the sky, or below 0 towards the ground.
        public float RiseFrom = 0.3f;
        public float Sag;                                   // Radians a metre towards the ground, up to RiseFrom.
        public float StepLength = 0.8f;
    }

    /// <summary>Palubicki's parameters, as ranges.</summary>
    public sealed class BudForm
    {
        public IntRange Years = new IntRange(5, 6);
        public FloatRange Apical = new FloatRange(0.49f, 0.51f);    // Lambda: above a half the main axis takes more, below it the laterals do.
        public FloatRange Vigour = new FloatRange(2f, 2.2f);        // Alpha: resource for each unit of light.
        public FloatRange LightSeek = new FloatRange(0.5f, 0.8f);   // Xi: how strongly a shoot turns to free space and the light.
        public FloatRange Tropism = new FloatRange(0.02f, 0.08f);   // Eta: how strongly it grows upwards, or downwards below 0.
        public float TropismByOrder;                                // And how that changes as branches get finer, for what weeps.
        public FloatRange BranchAngle = new FloatRange(0.6f, 0.85f);
        public float Divergence = 137.5f;
        public bool Opposite;                                       // Buds in pairs, as a maple has.
        public float Internode = 0.55f;
        public int MostPerYear = 3;
        public int MostNodes = 2600;
        public int LeafYears = 3;                                   // How long a shoot keeps its leaves, and so casts shade.
        public float Shed = 0.16f;                                  // Light for each internode a branch has to earn to stay.
        public float ScaffoldBuds = 0.65f;                          // The chance of each node of the scaffold having a bud.
        public float BudDeath = 0.2f;
        public float Wobble = 0.12f;
        public float Containment = 2.5f;
        public float Room = 0.12f;                                  // How far outside the crown a scaffold can still have buds.
        public float MarkerDensity = 0.7f;                          // Markers of free space to a cubic metre.
        public float Perception = 2.6f;                             // How far a bud can sense free space, in metres.
        public float PerceptionAngle = 1.3f;                        // Radians either side of where it's pointing.
        public float Occupancy = 0.8f;                              // How near wood has to be to use a marker up, in metres.
        public float Hollow = 0.75f;                                // How much emptier the inside of a crown is than its surface, 0 to 1.
        public float ClumpSize = 3.4f;                              // Metres across the masses a crown comes in.
        public float Gaps = 0.42f;                                  // How much of a crown is air between them, as a threshold of noise.
        public float ShadeStrength = 0.35f;
        public float ShadeFalloff = 1.6f;
        public int ShadeDepth = 6;
    }

    /// <summary>
    /// A tree. It's the same seven steps for everything with wood: a scaffold, which is the species, fine wood by competition, thickness by the pipe model, then light, axes, a
    /// skin and joints, and leaves. See FLORA.md.
    /// </summary>
    public sealed class TreeForm : PlantForm
    {
        public FloatRange Height = new FloatRange(18f, 24f);
        public TrunkForm Trunk = new TrunkForm();
        public RootForm? Roots;
        public IntRange RootCount = new IntRange(5, 7);
        public LimbForm[] Limbs = Array.Empty<LimbForm>();
        public BudForm? Buds;

        // The crown, as shares of the tree's height.
        public CrownShape Crown = CrownShape.Spherical;
        public FloatRange CrownBase = new FloatRange(0.3f, 0.4f);
        public FloatRange CrownRadius = new FloatRange(0.4f, 0.5f);
        public float CrownBumps = 0.12f;

        public bool Walkable;               // Low enough to push through, as a shrub is.
        public float PipeExponent = 2.4f;
        public float LimbGirth = 1f;        // Thins everything but the trunk, for a trunk far too big for what it bears.
        public float TipRadius = 0.02f;
        public float WoodBudget = 150f;     // How many metres of skinned stem a tree can afford below its limbs: the rest is on the cards.
        public int MostAxes = 140;
        public BarkForm Bark = new BarkForm();
        public FoliageForm? Foliage;
        public FoliageForm? Hangers;        // A second layer, of what hangs from a tree and isn't its own: old man's beard, lianas.
        public RosetteForm? Head;           // Leaves from the top of each stem: palms, tree ferns.
        public float HeadScale = 1f;

        /// <summary>What's left of a tree when it's dead. Null: it's alive.</summary>
        public Decay? Dead;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            float height = genes.Range("height", Height);
            Envelope envelope = MakeEnvelope(genes, height);

            var graph = new PlantGraph();
            List<List<int>> stems = GrowScaffold(graph, envelope, height, genes.Part("scaffold"), out float trunkRadius);

            if (Buds != null)
            {
                var growth = new BudGrowth(graph, Buds, envelope, genes.Part("fine wood"));
                growth.Seed(stems);
                growth.Run(genes.Count("years", Buds.Years.Min, Buds.Years.Max));
            }

            graph.Relax(0.5f, 2);
            graph.Thicken(TipRadius, PipeExponent, trunkRadius);
            if (Head != null || Bark.RoundTips) Taper(graph, trunkRadius);
            if (LimbGirth != 1f) ThinLimbs(graph);
            Light(graph, envelope);

            float cut = WoodCut(graph, trunkRadius);
            List<PlantAxis> axes = PlantAxis.From(graph, cut);
            if (axes.Count == 0) return;
            Limit(axes);
            if (Dead != null) Dead.Apply(graph, axes, genes.Part("decay"));

            // Roots come out of the ridges of the foot, or the two would look like something stuck on.
            int rootCount = Roots != null ? genes.Count("root count", RootCount.Min, RootCount.Max) : genes.Count("lobes", Trunk.Lobes.Min, Trunk.Lobes.Max);
            float rootFacing = genes.Range("root facing", 0f, MathF.Tau);
            if (Roots != null) AddRoots(axes, axes[0], genes.Part("roots"), height, trunkRadius, rootCount, rootFacing);

            int[] nodeJoint = Rig.Link(mesh, axes, graph.Count, Walkable);
            BarkSkin.Wrap(mesh, axes, Bark, this, genes.Part("bark"), trunkRadius, height, rootCount, rootFacing);

            if (Foliage != null && Dead == null)
            {
                Crowning.Dress(mesh, graph, axes, nodeJoint, envelope, Foliage, genes.Part("foliage"), Walkable);
            }
            if (Hangers != null) Crowning.Dress(mesh, graph, axes, nodeJoint, envelope, Hangers, genes.Part("hangers"), Walkable);

            if (Head != null && Dead == null)
            {
                for (int i = 0; i < axes.Count; i++)
                {
                    PlantAxis axis = axes[i];
                    if (axis.Order > 0 || axis.Root) continue;
                    int joint = Rig.LinkAt(axis, 1f);
                    Head.Grow(mesh, genes.Part("head").Part(i), axis.Points[^1], joint, HeadScale);
                }
            }
        }

        private Envelope MakeEnvelope(Genes genes, float height)
        {
            float bottom = genes.Range("crown base", CrownBase) * height;
            float radius = genes.Range("crown radius", CrownRadius) * height;
            Vector2 offset = new Vector2(genes.Range("crown off x", -1f, 1f), genes.Range("crown off z", -1f, 1f)) * radius * 0.08f;
            return new Envelope(Crown, bottom, height, radius, offset, CrownBumps, (uint)genes.Stream("crown").NextULong());
        }

        private List<List<int>> GrowScaffold(PlantGraph graph, Envelope envelope, float height, Genes genes, out float trunkRadius)
        {
            int count = genes.Count("stems", Trunk.Stems.Min, Trunk.Stems.Max);
            float spread = genes.Range("spread", Trunk.StemSpread);
            trunkRadius = genes.Range("girth", Trunk.Girth) * height;
            float reach = genes.Range("reach", Trunk.Reach) * height;

            var all = new List<List<int>>();
            for (int s = 0; s < count; s++)
            {
                Genes stem = genes.Part(s);
                float angle = stem.Range("foot", 0f, MathF.Tau);
                Vector3 foot = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (count > 1 ? spread : 0f);
                float lean = stem.Range("lean", Trunk.Lean) + (count > 1 ? 0.06f : 0f);
                Vector3 direction = new Vector3(MathF.Cos(angle) * MathF.Sin(lean), MathF.Cos(lean), MathF.Sin(angle) * MathF.Sin(lean));

                StemCurve curve = Trunk.Curve;
                List<int> trunk = Scaffold.Stem(graph, -1, 0, foot - Vector3.UnitY * 0.4f, direction, reach + 0.4f, curve, stem.Stream("trunk"), Trunk.Step);
                all.Add(trunk);

                var level = new List<List<int>> { trunk };
                for (int l = 0; l < Limbs.Length; l++)
                {
                    var next = new List<List<int>>();
                    Genes levelGenes = stem.Part("level").Part(l);
                    for (int i = 0; i < level.Count; i++)
                    {
                        next.AddRange(Scaffold.Limbs(graph, level[i], Limbs[l], envelope, l == 0 && !Limbs[l].OfParent ? height : 1f, levelGenes.Part(i)));
                    }
                    all.AddRange(next);
                    level = next;
                }
            }
            return all;
        }

        // A palm's stem doesn't thin as branches leave it, and nor does a cactus: each is about as thick at the top as the foot.
        private static void Taper(PlantGraph graph, float radius)
        {
            float top = 1f;
            foreach (PlantGraph.Node node in graph.Nodes) top = MathF.Max(top, node.Position.Y);
            for (int i = 0; i < graph.Count; i++)
            {
                PlantGraph.Node node = graph.Nodes[i];
                node.Radius = radius * (1f - 0.3f * MathF.Min(node.Position.Y / top, 1f)) * (node.Order > 0 ? 0.68f : 1f);
                graph.Nodes[i] = node;
            }
        }

        private void ThinLimbs(PlantGraph graph)
        {
            for (int i = 0; i < graph.Count; i++)
            {
                PlantGraph.Node node = graph.Nodes[i];
                if (node.Order == 0) continue;
                node.Radius *= LimbGirth;
                graph.Nodes[i] = node;
            }
        }

        // Shade from everything, read back at everything: what the fine wood fought over, but with the tree as it ended up.
        private void Light(PlantGraph graph, Envelope envelope)
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (PlantGraph.Node node in graph.Nodes)
            {
                if (node.Dead) continue;
                min = Vector3.ComponentMin(min, node.Position);
                max = Vector3.ComponentMax(max, node.Position);
            }
            var shadow = new ShadowGrid(min - new Vector3(1f), max + new Vector3(1f), MathF.Max((max.Y - min.Y) / 48f, 0.4f), 0.14f, 1.35f, 8);
            for (int i = 0; i < graph.Count; i++)
            {
                if (!graph.Nodes[i].Dead && graph.Nodes[i].Radius < TipRadius * 6f) shadow.Cast(graph.Nodes[i].Position);
            }
            for (int i = 0; i < graph.Count; i++)
            {
                PlantGraph.Node node = graph.Nodes[i];
                node.Light = shadow.Light(node.Position);
                graph.Nodes[i] = node;
            }
        }

        // How thin wood gets before it's left to the cards. Finer than a pixel from up here is a fault, and the budget says how much of the rest a tree can pay for.
        private float WoodCut(PlantGraph graph, float trunkRadius)
        {
            var radii = new List<(float radius, float length)>(graph.Count);
            for (int i = 0; i < graph.Count; i++)
            {
                PlantGraph.Node node = graph.Nodes[i];
                if (node.Dead || node.Parent < 0) continue;
                radii.Add((node.Radius, (node.Position - graph.Nodes[node.Parent].Position).Length));
            }
            radii.Sort((a, b) => b.radius.CompareTo(a.radius));

            float spent = 0f;
            foreach ((float radius, float length) in radii)
            {
                spent += length;
                if (spent >= WoodBudget) return MathF.Max(radius, trunkRadius * 0.012f);
            }
            return MathF.Max(trunkRadius * 0.012f, 0.005f);
        }

        private void Limit(List<PlantAxis> axes)
        {
            if (axes.Count <= MostAxes) return;
            // Thickest first, as what's kept has to include everything a kept axis hangs from: parents always come first, and so are always thicker or already kept.
            var kept = new HashSet<PlantAxis>();
            foreach (PlantAxis axis in axes.OrderByDescending(a => a.Radii[Math.Min(1, a.Radii.Count - 1)]))
            {
                if (kept.Count >= MostAxes) break;
                if (axis.Parent == null || kept.Contains(axis.Parent)) kept.Add(axis);
            }
            axes.RemoveAll(axis => !kept.Contains(axis));
        }

        private void AddRoots(List<PlantAxis> axes, PlantAxis trunk, Genes genes, float height, float trunkRadius, int count, float facing)
        {
            WorldRandom random = genes.Stream("wander");
            for (int i = 0; i < count; i++)
            {
                float angle = facing + (i + random.Range(-0.08f, 0.08f)) * (MathF.Tau / count);
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 across = new Vector3(-outward.Z, 0f, outward.X);
                float reach = genes.Part(i).Range("reach", Roots!.Reach) * height;
                float girth = genes.Part(i).Range("girth", Roots.Girth) * trunkRadius;

                // It leaves the flare high up, dives, and runs on half buried until the ground has it. Only ever the top of a root shows.
                var root = new PlantAxis { Parent = trunk, ParentPoint = 0, Order = 1, Root = true };
                const int Points = 8;
                uint seed = (uint)random.NextULong();
                for (int p = 0; p < Points; p++)
                {
                    float t = p / (float)(Points - 1);
                    float along = trunkRadius * 0.5f + MathF.Pow(t, 1.2f) * reach;
                    float y = trunkRadius * Roots.Rise * MathF.Pow(1f - t, 3.5f) - girth * 0.3f * t - 0.5f * t * t;
                    Vector3 side = across * (FloraNoise.Wave(t * 2.4f, seed) * reach * 0.2f * t);
                    root.Points.Add(outward * along + side + Vector3.UnitY * y);
                    root.Radii.Add(girth * MathF.Pow(1f - t, 1.3f) + 0.04f);
                    root.Nodes.Add(-1);
                }
                axes.Add(root);
            }
        }
    }
}
