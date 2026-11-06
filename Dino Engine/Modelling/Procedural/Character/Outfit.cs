using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>Puts a person together with what they have on, as one mesh, so that a hero in everything they own still costs one draw. It's the same mesh that lies on the ground. See GEAR.md.</summary>
    public static class Outfit
    {
        private const float PendantHeight = 3.28f;

        /// <param name="worn">What's on them, and which side of the body it's on, for what could be either: 1 is the left hand.</param>
        public static Mesh Dress(HumanoidLook body, IReadOnlyList<(GearSpec spec, float side)> worn)
        {
            var dressed = new GearMesh();
            BodyRegions covered = BodyRegions.None;
            float chest = 0f;

            foreach ((GearSpec spec, float side) in worn)
            {
                GearMesh piece = GearForge.Make(spec);
                covered |= piece.Covers;

                switch (spec.Kind)
                {
                    case GearKind.OffHand:
                        piece.SetBone((int)BoneType.HandL);
                        dressed.At(HumanoidGenerator.OffHand, () => dressed.Add(piece));
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
                        if (spec.Kind == GearKind.Body) chest = FrontOf(piece);
                        dressed.Add(piece);
                        break;
                    default:
                        piece.SetBone((int)BoneType.HandR);
                        dressed.At(HumanoidGenerator.MainHand, () => dressed.Add(piece));
                        break;
                }
            }

            foreach ((GearSpec spec, float _) in worn)
            {
                if (spec.Kind != GearKind.Neck) continue;
                GearMesh pendant = GearForge.Make(spec);
                pendant.SetBone((int)BoneType.Chest);
                float front = MathF.Max(chest, BodyForm.Torso(PendantHeight) * BodyForm.Depth) + 0.05f;
                dressed.At(Matrix4.CreateTranslation(0f, PendantHeight, front), () => dressed.Add(pendant));
            }

            dressed.Add(HumanoidGenerator.Generate(body, covered));
            return dressed.Build(bones: true);
        }

        // How far forward a chest comes, for what lies on it.
        private static float FrontOf(GearMesh piece)
        {
            float front = 0f;
            foreach (Vertex vertex in piece.Vertices)
            {
                if (MathF.Abs(vertex.position.Y - PendantHeight) < 0.12f && MathF.Abs(vertex.position.X) < 0.1f) front = MathF.Max(front, vertex.position.Z);
            }
            return front;
        }
    }
}
