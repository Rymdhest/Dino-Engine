using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>Puts a person together with what they have on, as one mesh, so that a hero in everything they own still costs one draw. It's the same mesh that lies on the ground. See GEAR.md.</summary>
    public static class Outfit
    {
        // Where a pendant hangs, at the breastbone, and where over armour its strands go in under what's round the neck: up beside it, just above the shoulders' line.
        private const float PendantHeight = 3.28f;
        private const float StrandX = 0.2f;
        private const float StrandTop = 3.86f;

        // How far off what it lies on a pendant's back is, and how far in under a collar a strand's end goes.
        private const float PendantClear = 0.012f;
        private const float Tuck = 0.035f;

        /// <param name="worn">What's on them, and which side of the body it's on, for what could be either, a ring or a weapon that takes one hand: 1 is the left hand.</param>
        public static Mesh Dress(HumanoidLook body, IReadOnlyList<(GearSpec spec, float side)> worn)
        {
            var dressed = new GearMesh();
            BodyRegions covered = BodyRegions.None;
            GearMesh? chest = null;

            foreach ((GearSpec spec, float side) in worn)
            {
                GearMesh piece = GearForge.Make(spec);
                covered |= piece.Covers;

                switch (spec.Kind)
                {
                    case GearKind.OffHand:
                        piece.SetBone((int)BoneType.HandL);
                        dressed.At(ShieldGenerator.IsWard(spec) ? HumanoidGenerator.SecondHand : HumanoidGenerator.OffHand, () => dressed.Add(piece));
                        break;
                    case GearKind.Bow:
                        piece.SetBone((int)BoneType.HandL);
                        dressed.At(HumanoidGenerator.BowHand, () => dressed.Add(piece));
                        break;
                    case GearKind.Finger:
                        piece.SetBone((int)BodyForm.Hand(side));
                        dressed.At(HumanoidGenerator.RingOn(side), () => dressed.Add(piece));
                        break;
                    case GearKind.Neck:
                        // On top of whatever's on the chest, which isn't known until it's all been looked at.
                        piece.SetBone((int)BoneType.Chest);
                        break;
                    case GearKind.Head:
                    case GearKind.Body:
                    case GearKind.Legs:
                    case GearKind.Feet:
                    case GearKind.Hands:
                        if (spec.Kind == GearKind.Body) chest = piece;
                        dressed.Add(piece);
                        break;
                    default:
                        // A weapon, in the right hand unless it's the second of two.
                        piece.SetBone((int)(side > 0f ? BoneType.HandL : BoneType.HandR));
                        dressed.At(side > 0f ? HumanoidGenerator.SecondHand : HumanoidGenerator.MainHand, () => dressed.Add(piece));
                        break;
                }
            }

            foreach ((GearSpec spec, float _) in worn)
            {
                if (spec.Kind == GearKind.Neck) dressed.Add(Necklace(spec, chest));
            }

            dressed.Add(HumanoidGenerator.Generate(body, covered));
            return BodyForm.Spread(dressed.Build(bones: true));
        }

        /// <summary>
        /// A body that isn't a person's, on the same rig, with what it holds in its hands: a weapon in the right, and a shield or a second weapon in the left, put where a hero's go. Nothing else is worn.
        /// </summary>
        /// <param name="body">Nothing for no body at all: only what's held, in the air where hands would be, for what's wielded by nobody that can be seen.</param>
        /// <param name="held">What it holds, and which hand: 1 is the left.</param>
        public static Mesh Hold(Mesh? body, IReadOnlyList<(GearSpec spec, float side)> held)
        {
            var dressed = new GearMesh();
            foreach ((GearSpec spec, float side) in held)
            {
                GearMesh piece = GearForge.Make(spec);
                switch (spec.Kind)
                {
                    case GearKind.OffHand:
                        piece.SetBone((int)BoneType.HandL);
                        dressed.At(ShieldGenerator.IsWard(spec) ? HumanoidGenerator.SecondHand : HumanoidGenerator.OffHand, () => dressed.Add(piece));
                        break;
                    case GearKind.Bow:
                        piece.SetBone((int)BoneType.HandL);
                        dressed.At(HumanoidGenerator.BowHand, () => dressed.Add(piece));
                        break;
                    default:
                        piece.SetBone((int)(side > 0f ? BoneType.HandL : BoneType.HandR));
                        dressed.At(side > 0f ? HumanoidGenerator.SecondHand : HumanoidGenerator.MainHand, () => dressed.Add(piece));
                        break;
                }
            }
            if (body is Mesh shown) dressed.Add(shown);
            return BodyForm.Spread(dressed.Build(bones: true));
        }

        /// <summary>
        /// A necklace as worn over <paramref name="chest"/>, what's worn on the body if anything: its pendant hung at the breastbone clear of whatever's there, on two strands laid over it, which over
        /// armour go in under what's round the neck, as a chain round a neck goes under a collar, and on a bare chest go on round the neck. Nothing of it goes into what it lies on (GearLab fit).
        /// In the rig's space, on the chest's bone.
        /// </summary>
        /// <param name="costs">What each part cost, and its parts named, for a lab.</param>
        public static GearMesh Necklace(GearSpec spec, GearMesh? chest, Dictionary<string, int>? costs = null)
        {
            var field = new FrontField(-0.5f, 0.5f, 3f, 4.2f, 0.01f, BareFront);
            if (chest != null) field.Add(chest);

            // The pendant hangs clear of what's behind it, every part of it but the chain, which is laid on it after.
            GearMesh alone = GearForge.Make(spec, new Dictionary<string, int>());
            (string _, int chainFrom, int chainTo) = alone.Parts!.Find(part => part.Name == "chain");
            float front = BareFront(0f, PendantHeight) + JewelGenerator.StrandLies;
            for (int v = 0; v < alone.VertexCount; v++)
            {
                if (v >= chainFrom && v < chainTo) continue;
                Vector3 p = alone.Vertices[v].position;
                front = MathF.Max(front, field.At(p.X, PendantHeight + p.Y) + PendantClear - p.Z);
            }
            var hang = new Vector3(0f, PendantHeight, front);

            var strands = new List<IReadOnlyList<Vector3>>();
            foreach (float side in new[] { -1f, 1f }) strands.Add(chest != null ? UnderCollar(field, hang, side) : RoundNeck(field, hang, side));
            GearMesh necklace = GearForge.Worn(spec, strands, costs);
            necklace.SetBone((int)BoneType.Chest);
            necklace.Transform(Matrix4.CreateTranslation(hang));
            return necklace;
        }

        // Up over what's on the chest from the pendant and in under what's round the neck: nearly straight, as a chain lies between two points on a chest, each point lifted off the surface,
        // and its end gone in under it. In the pendant's frame.
        private static IReadOnlyList<Vector3> UnderCollar(FrontField field, Vector3 hang, float side)
        {
            const int Steps = 28;
            var strand = new List<Vector3>(Steps + 1);
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                float x = side * StrandX * (t + 0.3f * t * (1f - t));
                float y = hang.Y + (StrandTop - hang.Y) * t;
                float surface = field.Around(x, y, JewelGenerator.StrandLies) + JewelGenerator.StrandLies;
                float z = MathF.Max(surface, hang.Z + (surface - hang.Z) * MathF.Min(t * 5f, 1f));
                z -= (JewelGenerator.StrandLies + Tuck) * Smooth(0.86f, 1f, t);
                strand.Add(new Vector3(x, y, z) - hang);
            }
            return strand;
        }

        // On a bare chest: up over it from the pendant, round beside the neck over the shoulders' slope and to the back of it, where the other strand meets it. In the pendant's frame.
        private static IReadOnlyList<Vector3> RoundNeck(FrontField field, Vector3 hang, float side)
        {
            // Up the chest, lying on it, to where the shoulders slope up to the neck.
            Vector3 Lying(float x, float y) => new Vector3(x, y, field.Around(x, y, JewelGenerator.StrandLies) + JewelGenerator.StrandLies);
            var front = new[] { hang, Lying(side * 0.1f, 3.56f), Lying(side * 0.18f, 3.8f), Lying(side * 0.24f, 3.95f) };
            // Then over that slope beside the neck, and round behind it.
            var back = new[] { front[3], new Vector3(side * 0.25f, 4.04f, -0.03f), new Vector3(side * 0.17f, 4.03f, -0.21f), new Vector3(0f, 4.02f, -0.27f) };
            var strand = new List<Vector3>();
            List<Vector3> up = Spine.Through(front, 20);
            for (int i = 0; i < up.Count; i++)
            {
                Vector3 p = up[i];
                if (i > 0) p.Z = MathF.Max(p.Z, field.Around(p.X, p.Y, JewelGenerator.StrandLies) + JewelGenerator.StrandLies);
                strand.Add(p - hang);
            }
            List<Vector3> round = Spine.Through(back, 16);
            for (int i = 1; i < round.Count; i++)
            {
                Vector3 p = round[i];
                float r = JewelGenerator.StrandLies;
                float top = MathF.Max(MathF.Max(TorsoTop(p.X, p.Z), MathF.Max(TorsoTop(p.X - r, p.Z), TorsoTop(p.X + r, p.Z))), MathF.Max(TorsoTop(p.X, p.Z - r), TorsoTop(p.X, p.Z + r)));
                p.Y = MathF.Max(p.Y, top + JewelGenerator.StrandLies);
                if (p.Z > 0f) p.Z = MathF.Max(p.Z, field.Around(p.X, p.Y, JewelGenerator.StrandLies) + JewelGenerator.StrandLies);
                strand.Add(p - hang);
            }
            return strand;
        }

        // How far forward a bare body comes, at a height and across: the torso as BodyForm has it, and the neck above it (HumanoidGenerator).
        private static float BareFront(float x, float y)
        {
            float torso = float.NegativeInfinity;
            if (y <= 4.06f)
            {
                float t = BodyForm.Torso(y);
                float across = x / t;
                if (across * across < 1f) torso = t * BodyForm.Depth * MathF.Sqrt(1f - across * across);
            }
            float neck = MathF.Abs(x) < 0.17f && y > 3.8f && y < 4.14f ? -0.03f + MathF.Sqrt(0.17f * 0.17f - x * x) : float.NegativeInfinity;
            return MathF.Max(MathF.Max(torso, neck), -0.5f);
        }

        // The top of a bare torso over a point, as its sides come in towards the neck: the highest it's still inside there, or below the shoulders if it's outside them all.
        private static float TorsoTop(float x, float z)
        {
            float Inside(float y)
            {
                float t = BodyForm.Torso(y);
                float a = x / t, b = z / (t * BodyForm.Depth);
                return a * a + b * b;
            }
            float low = BodyForm.Shoulder - 0.05f, high = 4.06f;
            if (Inside(low) >= 1f) return float.NegativeInfinity;
            if (Inside(high) < 1f) return high;
            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Inside(mid) < 1f) low = mid;
                else high = mid;
            }
            return low;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
