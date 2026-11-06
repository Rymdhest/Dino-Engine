using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    public enum Headwear
    {
        None,
        Hair,
        Hood,
        StrawHat,
        Helm,
    }

    public enum HeldItem
    {
        None,
        Sword,
        Spear,
        Bow,
    }

    /// <summary>What a person looks like. Everyone in a world is drawn from a few of these.</summary>
    public sealed class HumanoidLook
    {
        public Vector3 Skin = new Vector3(0.62f, 0.44f, 0.32f);
        public Vector3 Tunic = new Vector3(0.3f, 0.26f, 0.2f);
        public Vector3 Legs = new Vector3(0.22f, 0.2f, 0.17f);
        public Vector3 Boots = new Vector3(0.14f, 0.1f, 0.08f);
        public Vector3 Trim = new Vector3(0.5f, 0.15f, 0.1f);
        public Headwear Head = Headwear.Hair;
        public Vector3 HeadColour = new Vector3(0.1f, 0.07f, 0.05f);
        public HeldItem Held = HeldItem.None;
        public bool Shield;
        public bool Armoured;       // Plates on the shoulders and chest.
        public bool LongGarment;    // A robe or long coat over the legs.
        public float Bulk = 1f;     // How broad the body is.
    }

    /// <summary>
    /// Builds people on the same rig as the skeleton in <see cref="Indoor.SkeletonGenerator"/>, so they share every animation.
    /// Each limb is its own rigid piece, built around the joints of the T-pose that the rig's bind matrices expect.
    /// </summary>
    public static class HumanoidGenerator
    {
        private const float ArmLength = 1.7f;
        private const float LegLength = 2f;
        private const float ShoulderX = 0.45f;
        private const float ShoulderY = 1.85f;
        private const float HipX = 0.25f;

        private static Matrix4 Turned(float x, float y, float z) => Util.MyMath.createTransformationMatrix(Vector3.Zero, new Vector3(x, y, z), Vector3.One);

        /// <summary>Where something held in the left hand goes, built facing +z and standing up y, along the arm. On the same bone as the hand.</summary>
        public static readonly Matrix4 OffHand =
            Turned(0f, MathF.PI / 2f, 0f) * Matrix4.CreateTranslation(0f, -ArmLength - 0.4f, 0f) * Turned(0f, 0f, MathF.PI / 2f) * Matrix4.CreateTranslation(ShoulderX, ShoulderY + LegLength, 0f);

        /// <summary>Where a bow goes, which is in the left hand, with its limbs along the arm.</summary>
        public static readonly Matrix4 BowHand =
            Matrix4.CreateTranslation(0f, -ArmLength - 0.1f, 0f) * Turned(0f, 0f, MathF.PI / 2f) * Matrix4.CreateTranslation(ShoulderX, ShoulderY + LegLength, 0f);

        /// <summary>Where a ring goes, on either hand, for what's built lying around y.</summary>
        public static Matrix4 RingOn(float side) => Turned(0f, 0f, MathF.PI / 2f) * Matrix4.CreateTranslation(side * (ShoulderX + ArmLength + 0.1f), ShoulderY + LegLength, 0f);

        /// <summary>Where something held in the right hand goes, for what's built standing up with the middle of its grip at the origin. On the same bone as the hand.</summary>
        public static readonly Matrix4 MainHand =
            Turned(0f, MathF.PI / 2f, 0f) * Turned(MathF.PI / 2f, 0f, 0f) * Matrix4.CreateTranslation(0f, -ArmLength - 0.15f, 0f)
            * Turned(0f, 0f, -MathF.PI / 2f) * Matrix4.CreateTranslation(-ShoulderX, ShoulderY + LegLength, 0f);

        private static VertexMaterial Cloth(Vector3 colour) => new VertexMaterial(GearMaterials.Cloth, new Colour(colour));
        private static VertexMaterial Hide(Vector3 colour) => new VertexMaterial(GearMaterials.Leather, new Colour(colour));
        private static VertexMaterial Skin(Vector3 colour) => new VertexMaterial(GearMaterials.Skin, new Colour(colour));

        public static Mesh Generate(HumanoidLook look) => Generate(look, BodyRegions.None);

        /// <param name="covered">What's under something else, and so isn't there at all.</param>
        public static Mesh Generate(HumanoidLook look, BodyRegions covered)
        {
            Mesh body = new Mesh();
            if ((covered & BodyRegions.Torso) == 0) body += BuildTorso(look).setAllBoneIndices(new Vector2((int)BoneType.Chest, 0f));
            body += BuildHead(look, (covered & BodyRegions.Hair) != 0).setAllBoneIndices(new Vector2((int)BoneType.Head, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side > 0;
                if ((covered & BodyRegions.UpperArms) == 0) body += BuildUpperArm(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.UpperArmL : BoneType.UpperArmR), 0f));
                if ((covered & BodyRegions.LowerArms) == 0) body += BuildLowerArm(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.LowerArmL : BoneType.LowerArmR), 0f));
                if ((covered & BodyRegions.Hands) == 0) body += BuildHand(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.HandL : BoneType.HandR), 0f));
                if ((covered & BodyRegions.UpperLegs) == 0) body += BuildUpperLeg(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.UpperLegL : BoneType.UpperLegR), 0f));
                if ((covered & BodyRegions.LowerLegs) == 0) body += BuildLowerLeg(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.LowerLegL : BoneType.LowerLegR), 0f));
                if ((covered & BodyRegions.Feet) == 0) body += BuildFoot(look, side).setAllBoneIndices(new Vector2((int)(left ? BoneType.FootL : BoneType.FootR), 0f));
            }

            body.translate(new Vector3(0f, LegLength, 0f));
            body.isUsingBones = true;
            return Armed(body, look.Held, look.Shield, SeedOf(look));
        }

        // Everyone with the same look carries the same thing, and a different look carries something else.
        private static ulong SeedOf(HumanoidLook look)
        {
            ulong hash = 0xCBF29CE484222325UL;
            foreach (Vector3 colour in new[] { look.Tunic, look.Legs, look.Trim, look.HeadColour })
            {
                hash = (hash ^ BitConverter.SingleToUInt32Bits(colour.X)) * 0x100000001B3UL;
                hash = (hash ^ BitConverter.SingleToUInt32Bits(colour.Y)) * 0x100000001B3UL;
                hash = (hash ^ BitConverter.SingleToUInt32Bits(colour.Z)) * 0x100000001B3UL;
            }
            return (hash ^ (ulong)look.Held ^ BitConverter.SingleToUInt32Bits(look.Bulk)) * 0x100000001B3UL;
        }

        /// <summary>A body on this rig with what it holds in its hands, which comes from the same place as a hero's. These are nobody's best work.</summary>
        public static Mesh Armed(Mesh body, HeldItem held, bool shield, ulong seed)
        {
            if (held == HeldItem.None && !shield) return body;

            var armed = new GearMesh();
            armed.Add(body);
            var form = new GearForm(0.45f, 0.5f, 0.15f, 0.4f, 0.12f, 0.45f);

            if (held != HeldItem.None)
            {
                GearKind kind = held == HeldItem.Spear ? GearKind.Spear : held == HeldItem.Bow ? GearKind.Bow : GearKind.Sword;
                GearMesh weapon = GearForge.Make(new GearSpec(seed, kind, 1f, 0f, 0f, form, 0, GearTheme.None));
                weapon.SetBone((int)(kind == GearKind.Bow ? BoneType.HandL : BoneType.HandR));
                armed.At(kind == GearKind.Bow ? BowHand : MainHand, () => armed.Add(weapon));
            }

            if (shield)
            {
                GearMesh guard = GearForge.Make(new GearSpec(seed + 1, GearKind.OffHand, 1f, 0f, 0f, form, 0, GearTheme.None));
                guard.SetBone((int)BoneType.HandL);
                armed.At(OffHand, () => armed.Add(guard));
            }
            return armed.Build(bones: true);
        }

        private static Mesh BuildTorso(HumanoidLook look)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            VertexMaterial tunic = Cloth(look.Tunic);
            float bulk = look.Bulk;

            // Flattened front to back, as a body is.
            b.Stretch = new Vector3(1f, 1f, 0.68f);
            b.Tube(new Vector3(0f, -0.1f, 0f), new Vector3(0f, 1.05f, 0f), 0.56f * bulk, 0.6f * bulk, 12, tunic, capped: false);
            b.Tube(new Vector3(0f, 1.05f, 0f), new Vector3(0f, 1.82f, 0f), 0.6f * bulk, 0.68f * bulk, 12, tunic, capped: false);
            b.Tube(new Vector3(0f, 1.82f, 0f), new Vector3(0f, 2.06f, 0f), 0.68f * bulk, 0.22f, 12, tunic);

            // The hem of the tunic, over the hips.
            float hem = look.LongGarment ? -1.75f : -0.55f;
            b.Tube(new Vector3(0f, hem, 0f), new Vector3(0f, -0.1f, 0f), 0.76f * bulk, 0.56f * bulk, 12, tunic, capped: false);

            b.Tube(new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.26f, 0f), 0.6f * bulk, 0.6f * bulk, 12, Hide(look.Trim * 0.6f), capped: false);
            b.ResetPlacement();
            b.Stretch = Vector3.One;

            if (look.Armoured)
            {
                VertexMaterial plate = new VertexMaterial(GearMaterials.Metal, new Colour(66, 66, 70));
                b.Stretch = new Vector3(1f, 1f, 0.72f);
                b.Tube(new Vector3(0f, 0.68f, 0f), new Vector3(0f, 1.74f, 0f), 0.64f * bulk, 0.72f * bulk, 12, plate);
                b.Stretch = Vector3.One;
                foreach (float x in new[] { -1f, 1f })
                {
                    b.Add(IcoSphereGenerator.CreateIcosphere(2, plate), new Vector3(x * (ShoulderX + 0.14f), ShoulderY + 0.04f, 0f), new Vector3(0.42f, 0.3f, 0.42f));
                }
            }
            return b.Build();
        }

        private static Mesh BuildHead(HumanoidLook look, bool bare)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            VertexMaterial skin = Skin(look.Skin);
            b.Tube(new Vector3(0f, 1.8f, -0.03f), new Vector3(0f, 2.14f, -0.03f), 0.17f, 0.16f, 8, skin, capped: false);
            Vector3 centre = new Vector3(0f, 2.46f, 0f);
            b.Add(IcoSphereGenerator.CreateIcosphere(3, skin), centre, new Vector3(0.33f, 0.4f, 0.36f));

            switch (bare ? Headwear.None : look.Head)
            {
                case Headwear.Hair:
                    VertexMaterial hair = Hide(look.HeadColour);
                    b.Add(IcoSphereGenerator.CreateIcosphere(2, hair), centre + new Vector3(0f, 0.12f, -0.07f), new Vector3(0.36f, 0.36f, 0.37f));
                    b.Add(IcoSphereGenerator.CreateIcosphere(1, hair), centre + new Vector3(0f, 0.42f, -0.12f), new Vector3(0.13f, 0.17f, 0.13f));
                    break;
                case Headwear.Hood:
                    VertexMaterial hood = Cloth(look.HeadColour);
                    b.Add(IcoSphereGenerator.CreateIcosphere(2, hood), centre + new Vector3(0f, 0.06f, -0.08f), new Vector3(0.43f, 0.48f, 0.46f));
                    b.Tube(new Vector3(0f, 1.82f, -0.02f), new Vector3(0f, 2.22f, -0.04f), 0.52f, 0.36f, 10, hood, capped: false);
                    break;
                case Headwear.StrawHat:
                    VertexMaterial straw = new VertexMaterial(GearMaterials.Straw, new Colour(214, 190, 138));
                    b.Tube(centre + new Vector3(0f, 0.24f, 0f), centre + new Vector3(0f, 0.74f, 0f), 1.02f, 0.05f, 16, straw);
                    b.Tube(centre + new Vector3(0f, 0.22f, 0f), centre + new Vector3(0f, 0.24f, 0f), 1.04f, 1.02f, 16, straw, capped: false);
                    break;
                case Headwear.Helm:
                    VertexMaterial iron = new VertexMaterial(GearMaterials.Metal, new Colour(62, 62, 66));
                    b.Add(IcoSphereGenerator.CreateIcosphere(2, iron), centre + new Vector3(0f, 0.1f, 0f), new Vector3(0.42f, 0.42f, 0.44f));
                    b.Tube(centre + new Vector3(0f, -0.02f, 0f), centre + new Vector3(0f, 0.04f, 0f), 0.48f, 0.46f, 12, iron);
                    b.Tube(centre + new Vector3(0f, 0.46f, 0f), centre + new Vector3(0f, 0.74f, 0f), 0.06f, 0.02f, 6, iron);
                    break;
            }
            return b.Build();
        }

        // Arms lie along x in the T-pose, out from the shoulder.
        private static Mesh BuildUpperArm(HumanoidLook look, int side)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 shoulder = new Vector3(side * ShoulderX, ShoulderY, 0f);
            Vector3 elbow = shoulder + new Vector3(side * ArmLength * 0.5f, 0f, 0f);
            b.Tube(shoulder, elbow, 0.21f * look.Bulk, 0.17f * look.Bulk, 8, Cloth(look.Tunic));
            return b.Build();
        }

        private static Mesh BuildLowerArm(HumanoidLook look, int side)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 elbow = new Vector3(side * (ShoulderX + ArmLength * 0.5f), ShoulderY, 0f);
            Vector3 wrist = new Vector3(side * (ShoulderX + ArmLength), ShoulderY, 0f);
            b.Tube(elbow, wrist, 0.17f * look.Bulk, 0.12f, 8, Cloth(look.Tunic * 0.9f));
            return b.Build();
        }

        private static Mesh BuildHand(HumanoidLook look, int side)
        {
            var b = new MeshBuilder();
            Vector3 wrist = new Vector3(side * (ShoulderX + ArmLength + 0.08f), ShoulderY, 0f);
            b.Add(IcoSphereGenerator.CreateIcosphere(1, Skin(look.Skin)), wrist, new Vector3(0.14f, 0.1f, 0.13f));
            return b.Build();
        }

        private static Mesh BuildUpperLeg(HumanoidLook look, int side)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 hip = new Vector3(side * HipX, 0f, 0f);
            b.Tube(hip + Vector3.UnitY * 0.1f, hip - Vector3.UnitY * (LegLength * 0.5f), 0.26f * look.Bulk, 0.2f * look.Bulk, 8, Cloth(look.Legs));
            return b.Build();
        }

        private static Mesh BuildLowerLeg(HumanoidLook look, int side)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 knee = new Vector3(side * HipX, -LegLength * 0.5f, 0f);
            b.Tube(knee, knee - Vector3.UnitY * (LegLength * 0.5f - 0.12f), 0.2f * look.Bulk, 0.14f, 8, Cloth(look.Legs * 0.9f));
            return b.Build();
        }

        private static Mesh BuildFoot(HumanoidLook look, int side)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 ankle = new Vector3(side * HipX, -LegLength, 0f);
            b.Box(ankle + new Vector3(0f, 0.13f, 0.12f), new Vector3(0.17f, 0.15f, 0.34f), Hide(look.Boots));
            return b.Build();
        }
    }
}
