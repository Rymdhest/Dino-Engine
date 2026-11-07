using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Saurian
{
    /// <summary>
    /// What to grow: a species, a seed, what's been done to it, and whether it's in the Reich's harness. A model's name is all of that and nothing else, as in <c>dino:raptor.reich.3</c>, so any thread on
    /// any machine grows the same animal from it.
    /// </summary>
    public readonly record struct SaurianSpec(string Species, ulong Seed, SaurianCondition Condition, bool Harnessed)
    {
        public const string Prefix = "dino:";

        /// <summary>The one with this name, or nothing if it doesn't name a dinosaur. A name can end with a number, which is which of its kind it is.</summary>
        public static SaurianSpec? Parse(string key)
        {
            if (!key.StartsWith(Prefix)) return null;
            string[] words = key.Substring(Prefix.Length).Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0 || SaurianCatalog.Find(words[0]) == null) return null;

            var condition = SaurianCondition.Living;
            bool harnessed = false;
            int variant = 0;
            for (int i = 1; i < words.Length; i++)
            {
                switch (words[i])
                {
                    case "rot": condition = SaurianCondition.Rotting; break;
                    case "bones": condition = SaurianCondition.Bones; break;
                    case "reich": harnessed = true; break;
                    default: int.TryParse(words[i], out variant); break;
                }
            }

            // The same number is the same animal whatever's been done to it, so a raptor's bones are that raptor's.
            ulong seed = 0xD1A0_5A0Bu;
            foreach (char c in words[0]) seed = (seed ^ c) * 0x100000001B3UL;
            seed = (seed ^ (ulong)variant) * 0x100000001B3UL;
            return new SaurianSpec(words[0], seed, condition, harnessed);
        }
    }

    /// <summary>
    /// Grows dinosaurs. A body is drawn from its species and seed (<see cref="SaurianBody"/>), its flesh is a field (<see cref="SaurianSculpt"/>), and its skin is grown from that (<see cref="SaurianSkin"/>)
    /// with the small hard parts put on after (<see cref="SaurianParts"/>). The rig comes from the same body, so bones and flesh always agree. Nothing is stored and nothing touches a GPU: it's safe on
    /// any thread, and a lab can grow them with no window.
    /// </summary>
    public static class SaurianForge
    {
        private const int Grid = 230;           // Cells along the body: far finer than is drawn, for the decimator to spend well.
        private const float JawGrid = 0.55f;    // Of a body's cell, for the jaw, which shows when it opens.

        /// <summary>The rig of the animal a spec names, without growing it.</summary>
        public static Rig RigOf(SaurianSpec spec) => new SaurianBody(SpeciesOf(spec), spec.Seed).BuildRig();

        public static SaurianSpecies SpeciesOf(SaurianSpec spec) => SaurianCatalog.Find(spec.Species) ?? SaurianCatalog.Raptor;

        public static Mesh Make(SaurianSpec spec) => Make(spec, out _);

        public static Mesh Make(SaurianSpec spec, out SaurianBody body)
        {
            body = new SaurianBody(SpeciesOf(spec), spec.Seed);
            var mesh = new SaurianMesh();
            switch (spec.Condition)
            {
                case SaurianCondition.Bones:
                    SaurianBones.Build(mesh, body);
                    break;
                case SaurianCondition.Rotting:
                    Flesh(mesh, body, rotting: true);
                    break;
                default:
                    Flesh(mesh, body, rotting: false);
                    break;
            }
            if (spec.Harnessed && spec.Condition != SaurianCondition.Bones) SaurianHarness.Fit(mesh, body);
            return mesh.Build();
        }

        private static void Flesh(SaurianMesh mesh, SaurianBody body, bool rotting)
        {
            SaurianSpecies species = body.Species;
            var paint = new SaurianPaint(body);
            SaurianDress dress = rotting ? new SaurianRot.Dress(body, paint) : new SaurianDress(body, paint);
            float cell = body.Length / Grid;
            float longest = body.Length / 42f;

            SaurianField flesh = SaurianSculpt.Flesh(body);
            if (rotting) SaurianRot.Wound(flesh, body);
            SaurianSkin.Grow(mesh, flesh, cell, species.Budget, longest, dress);

            SaurianField jaw = SaurianSculpt.Jaw(body);
            SaurianSkin.Grow(mesh, jaw, cell * JawGrid, Math.Max(species.Budget / 12, 260), longest * 0.5f, new JawDress(body, paint, rotting));

            float scale = 1f;
            SaurianParts.Teeth(mesh, body, scale);
            if (rotting) SaurianRot.Eyes(mesh, body);
            else SaurianParts.Eyes(mesh, body, body.Coat.Eye, 1f, GearMaterials.Gem);
            SaurianParts.Horns(mesh, body);
            SaurianParts.Crests(mesh, body);
            SaurianParts.Frill(mesh, body);
            SaurianParts.Feet(mesh, body);
            SaurianParts.Hands(mesh, body);
            if (!rotting) SaurianParts.Quills(mesh, body, dress);
            SaurianParts.Scutes(mesh, body, dress);
            if (rotting) SaurianRot.Bare(mesh, body, dress);
        }

        /// <summary>
        /// The jaw hangs from the jaw bone, and its back and throat stretch a little to the neck, as skin does across the corner of a mouth. It's darker underneath and the colour of the inside of a mouth on top.
        /// </summary>
        private sealed class JawDress : SaurianDress
        {
            private readonly SaurianPaint _paint;
            private readonly bool _rotting;

            public JawDress(SaurianBody body, SaurianPaint paint, bool rotting) : base(body, paint)
            {
                _paint = paint;
                _rotting = rotting;
            }

            public override int Mapping(Vector3 p, SaurianPart part) => Side;

            public override Hang Hang(Vector3 p, SaurianPart part, ReadOnlySpan<float> parts)
            {
                SaurianHead head = Body.Head;
                Vector3 local = head.ToHead(p);
                float back = 1f - Math.Clamp((local.Z - head.Hinge) / (head.Length * 0.25f), 0f, 1f);
                float low = Math.Clamp((head.Lip(local.Z) - local.Y) / MathF.Max(head.DepthAt(local.Z), 1e-3f), 0f, 1f);
                float neck = 0.45f * back * low;
                return new Hang(BoneType.Jaw, BoneType.NeckTop, 1f - neck);
            }

            public override Vector3 Colour(Vector3 p, Vector3 normal, SaurianPart part, out int material, out float glow)
            {
                glow = 1f;
                SaurianHead head = Body.Head;
                Vector3 local = head.ToHead(p);
                material = GearMaterials.Leather;
                SaurianCoat coat = _paint.Coat;
                if (head.Lip(local.Z) - local.Y < head.Length * 0.01f && Vector3.Dot(normal, head.Up) > 0.5f) return coat.Mouth;

                float side = MathF.Abs(Vector3.Dot(normal, Vector3.Cross(head.Up, head.Forward)));
                Vector3 colour = Vector3.Lerp(coat.Belly, coat.Flank, side * 0.8f);
                return _rotting ? SaurianRot.Sicken(colour, p, coat.Seed, Body.Length) : colour;
            }
        }
    }
}
