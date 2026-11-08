using Dino_Engine.ECS.Components;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>How something is shown when nobody is wearing it, which is always the same mesh, put down. See GEAR.md.</summary>
    public static class GearDisplay
    {
        /// <summary>A view of something that shows what it is, for a picture of it. What's lying down is first stood up by <paramref name="Rise"/>, as nobody looks at a sword along its blade.</summary>
        public readonly record struct Angle(float Rise, float Yaw, float Pitch, float Roll);

        /// <summary>The same thing, lying on the ground with its middle at the origin, and resting on y = 0.</summary>
        public static GearMesh Lying(GearMesh worn, GearKind kind)
        {
            GearMesh lying = worn.Copy();

            // Sleeves and gloves lie the way they'd fall, and not out in a T.
            if (kind == GearKind.Body) lying.Pose(SleevesDown);
            if (kind == GearKind.Hands) lying.Pose(HandsTogether);

            // Held things are built standing up, and fall on their flat, as a garment falls on its back.
            if (IsHeld(kind) || kind is GearKind.Body or GearKind.Legs) lying.Transform(Matrix4.CreateRotationX(MathF.PI / 2f));

            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Model.Vertex vertex in lying.Vertices)
            {
                min = Vector3.ComponentMin(min, vertex.position);
                max = Vector3.ComponentMax(max, vertex.position);
            }
            lying.Transform(Matrix4.CreateTranslation(-(min.X + max.X) * 0.5f, -min.Y, -(min.Z + max.Z) * 0.5f));
            return lying;
        }

        public static bool IsHeld(GearKind kind) => kind <= GearKind.OffHand;

        private static Matrix4 SleevesDown(int bone)
        {
            float side = bone is (int)BoneType.UpperArmL or (int)BoneType.LowerArmL or (int)BoneType.HandL ? 1f : bone is (int)BoneType.UpperArmR or (int)BoneType.LowerArmR or (int)BoneType.HandR ? -1f : 0f;
            if (side == 0f) return Matrix4.Identity;
            Vector3 shoulder = new Vector3(side * BodyForm.ShoulderX, BodyForm.Shoulder, 0f);
            return Matrix4.CreateTranslation(-shoulder) * Matrix4.CreateRotationZ(-side * 0.75f) * Matrix4.CreateTranslation(shoulder);
        }

        private static Matrix4 HandsTogether(int bone)
        {
            float side = bone is (int)BoneType.LowerArmL or (int)BoneType.HandL ? 1f : -1f;
            return Matrix4.CreateTranslation(-side * (BodyForm.WristX - 0.42f), 0f, 0f);
        }

        /// <summary>How much bigger than life something is drawn on the ground, as a ring is lost from up where the camera is. It's still the same model.</summary>
        public static float GroundScale(GearKind kind) => kind switch
        {
            GearKind.Finger => 7f,
            GearKind.Neck => 3f,
            _ => 1f,
        };

        /// <summary>How something is looked at in the studio, from what's lying on the ground. Long things run corner to corner, as that's the most of them that fits in a square.</summary>
        public static Angle PictureOf(GearKind kind) => kind switch
        {
            GearKind.Bow => new Angle(-MathF.PI / 2f, 1.05f, 0.18f, 0.785f),
            GearKind.Sword or GearKind.Dagger or GearKind.Spear or GearKind.Staff or GearKind.Wand or GearKind.Axe or GearKind.Mace => new Angle(-MathF.PI / 2f, 0.42f, 0.18f, 0.785f),
            GearKind.OffHand => new Angle(-MathF.PI / 2f, 0.5f, 0.15f, 0f),
            GearKind.Body or GearKind.Legs => new Angle(-MathF.PI / 2f, 0.3f, 0.12f, 0f),
            GearKind.Head => new Angle(0f, 0.5f, 0.32f, 0f),
            GearKind.OpenBook or GearKind.Page or GearKind.Sticker => new Angle(0f, 0.25f, 0.8f, 0f),
            GearKind.Orders => new Angle(0f, 0.62f, 0.55f, 0f),
            _ => new Angle(0f, 0.55f, 0.35f, 0f),
        };
    }
}
