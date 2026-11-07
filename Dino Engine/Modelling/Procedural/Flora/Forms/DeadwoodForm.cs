using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>The ways wood lies around.</summary>
    public enum DeadwoodShape
    {
        Log,        // A length of trunk on the ground, with stubs where its branches were.
        Stump,      // What's left at the foot, with roots still holding the ground.
        Branch,     // A fallen limb, thin and forked.
    }

    /// <summary>
    /// Wood on the ground, in the bark of whatever tree it was, so a stump in a redwood grove is a redwood's. It's split into a skeleton like anything else and goes through the same
    /// skin, so bark, moss and rings all match the trees standing around it.
    /// </summary>
    public sealed class DeadwoodForm : PlantForm
    {
        public DeadwoodShape Shape = DeadwoodShape.Log;
        public FloatRange Length = new FloatRange(6f, 10f);
        public FloatRange Radius = new FloatRange(0.4f, 0.7f);
        public IntRange Stubs = new IntRange(3, 6);
        public IntRange RootCount = new IntRange(5, 7);
        public BarkForm Bark = new BarkForm { Moss = 0.7f };
        public FungusForm? Fungi;   // What's growing on it.
        public float Hollow;        // Some are rotted through, and show it at their ends.

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            switch (Shape)
            {
                case DeadwoodShape.Stump: GrowStump(mesh, genes); break;
                default: GrowLog(mesh, genes); break;
            }
        }

        private void GrowLog(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("log");
            float length = genes.Range("length", Length);
            float radius = genes.Range("radius", Radius);
            uint seed = (uint)random.NextULong();

            // It lies on the ground, sunk to a third of its depth, and bends a little where it fell over something.
            var trunk = new PlantAxis { Cut = true, CutFoot = true };
            const int Points = 8;
            for (int i = 0; i < Points; i++)
            {
                float t = i / (float)(Points - 1);
                float x = (t - 0.5f) * length;
                float z = FloraNoise.Wave(t * 1.7f, seed) * length * 0.05f;
                float y = radius * 0.5f + FloraNoise.Wave(t * 1.4f + 3f, seed) * radius * 0.25f;
                trunk.Points.Add(new Vector3(x, y, z));
                trunk.Radii.Add(radius * (1f - 0.3f * t));
                trunk.Nodes.Add(-1);
            }
            var axes = new List<PlantAxis> { trunk };

            int stubs = genes.Count("stubs", Stubs.Min, Stubs.Max);
            for (int i = 0; i < stubs; i++)
            {
                int at = random.Int(1, Points - 2);
                float around = random.Range(0.15f, 3f);     // Not the underside, which it's lying on.
                Vector3 direction = (new Vector3(random.Range(0.1f, 0.6f), MathF.Sin(around), MathF.Cos(around))).Normalized();
                float stubLength = random.Range(radius * 1.2f, radius * 4.5f);
                float stubRadius = radius * random.Range(0.14f, 0.3f);
                var stub = new PlantAxis { Parent = trunk, ParentPoint = at, Order = 1, Cut = random.Float() < 0.6f };
                Vector3 from = trunk.Points[at];
                for (int p = 0; p < 4; p++)
                {
                    float t = p / 3f;
                    Vector3 sway = new Vector3(0f, -0.1f * t * t, random.Range(-0.05f, 0.05f)) * stubLength;
                    stub.Points.Add(from + direction * (stubLength * t) + sway);
                    stub.Radii.Add(stubRadius * (1f - 0.5f * t));
                    stub.Nodes.Add(-1);
                }
                axes.Add(stub);
            }

            BarkSkin.WrapPlain(mesh, axes, Bark, genes.Part("bark"), radius, length);
            if (Fungi != null) AddFungi(mesh, genes, trunk, radius);
        }

        private void GrowStump(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("stump");
            float height = genes.Range("length", Length);
            float radius = genes.Range("radius", Radius);

            var stump = new PlantAxis { Cut = true };
            stump.Points.Add(new Vector3(0f, -0.4f, 0f));
            stump.Points.Add(new Vector3(random.Range(-0.05f, 0.05f), height * 0.5f, random.Range(-0.05f, 0.05f)));
            stump.Points.Add(new Vector3(random.Range(-0.1f, 0.1f), height, random.Range(-0.1f, 0.1f)));
            stump.Radii.AddRange(new[] { radius * 1.05f, radius, radius * 0.92f });
            stump.Nodes.AddRange(new[] { -1, -1, -1 });
            var axes = new List<PlantAxis> { stump };

            int roots = genes.Count("roots", RootCount.Min, RootCount.Max);
            float facing = genes.Range("facing", 0f, MathF.Tau);
            for (int i = 0; i < roots; i++)
            {
                float angle = facing + (i + random.Range(-0.1f, 0.1f)) * (MathF.Tau / roots);
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                float reach = radius * random.Range(1.3f, 2.3f);
                var root = new PlantAxis { Parent = stump, ParentPoint = 0, Order = 1, Root = true };
                for (int p = 0; p < 6; p++)
                {
                    float t = p / 5f;
                    root.Points.Add(outward * (radius * 0.5f + reach * t) + Vector3.UnitY * (radius * 0.4f * MathF.Pow(1f - t, 3f) - 0.2f * t - radius * 0.34f * t * t));
                    root.Radii.Add(radius * 0.46f * MathF.Pow(1f - t, 0.8f) + 0.05f);
                    root.Nodes.Add(-1);
                }
                axes.Add(root);
            }

            BarkSkin.WrapPlain(mesh, axes, Bark, genes.Part("bark"), radius, height);
            if (Fungi != null) AddFungi(mesh, genes, stump, radius);
        }

        private void AddFungi(FloraMesh mesh, Genes genes, PlantAxis on, float radius)
        {
            int first = mesh.VertexCount;
            Fungi!.Grow(mesh, genes.Part("fungi"));
            Vector3 spot = Rig.PointAt(on, genes.Range("fungi at", 0.2f, 0.8f));
            float side = genes.Range("fungi side", 0f, MathF.Tau);
            mesh.Move(first, Matrix4.CreateTranslation(spot + new Vector3(MathF.Cos(side) * radius * 0.5f, radius * 0.55f, MathF.Sin(side) * radius * 0.5f)));
        }
    }
}
