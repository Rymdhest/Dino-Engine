using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>What an animal looks like. Lengths are in the units people are built in, where a person stands about 4.5 tall.</summary>
    public sealed class BeastLook
    {
        public float Height = 1.7f;         // To the shoulder.
        public float Length = 3.2f;         // Shoulder to hip.
        public float Girth = 0.5f;
        public float Neck = 0.75f;
        public float Skull = 0.34f;
        public float Muzzle = 0.5f;
        public float Tail = 1.2f;
        public float Ears = 0.22f;
        public float Tusks;
        public float Hump;                  // Rise over the shoulders.
        public Vector3 Coat = new Vector3(0.46f, 0.44f, 0.42f);
        public Vector3 Belly = new Vector3(0.62f, 0.58f, 0.52f);
        public Vector3 Nose = new Vector3(0.03f, 0.03f, 0.03f);
    }

    /// <summary>
    /// Builds animals on the same fourteen bones as people, so that everything that moves a character moves them. The arms are the forelegs: they're
    /// built out to the side as a person's are in the T-pose, and the same rotation that drops a person's arm stands a foreleg under the shoulder. Only where the
    /// joints are differs, so each look has a <see cref="Animation.Rig"/> of its own. Forward is +z, as it is for people.
    /// </summary>
    public static class BeastGenerator
    {
        private const float ShoulderWidth = 0.34f;
        private const float Ankle = 0.08f;

        private static VertexMaterial Fur(Vector3 colour) => new VertexMaterial(GearMaterials.Leather, new Colour(colour));
        private static VertexMaterial Flat(Vector3 colour) => new VertexMaterial(GearMaterials.Skin, new Colour(colour));

        private static float FrontZ(BeastLook look) => look.Length * 0.42f;
        private static float BackZ(BeastLook look) => -look.Length * 0.42f;
        private static Vector3 NeckRoot(BeastLook look) => new Vector3(0f, look.Height + look.Girth * 0.3f, look.Length * 0.46f);

        private static Vector3 HeadCentre(BeastLook look) => NeckRoot(look) + new Vector3(0f, look.Neck * 0.42f, look.Neck * 0.82f);
        private static float BodyHeight(BeastLook look) => look.Height + look.Girth * 0.1f;
        private static Vector3 TailRoot(BeastLook look) => new Vector3(0f, BodyHeight(look) + look.Girth * 0.3f, -look.Length * 0.52f - look.Girth * 0.5f);
        private static Vector3 TailBend(BeastLook look) => TailRoot(look) + new Vector3(0f, -look.Tail * 0.3f, -look.Tail * 0.5f);

        /// <summary>Where this animal's joints are.</summary>
        public static Rig RigOf(BeastLook look)
        {
            var bones = new Vector3[(int)BoneType.Count];
            void Set(BoneType bone, Vector3 pivot) => bones[(int)bone] = pivot;

            float half = look.Height * 0.5f;
            float back = BodyHeight(look);
            Set(BoneType.Pelvis, new Vector3(0f, back, BackZ(look)));
            Set(BoneType.Spine, new Vector3(0f, back, 0f));
            Set(BoneType.Chest, new Vector3(0f, back, FrontZ(look)));
            Set(BoneType.Neck, NeckRoot(look));
            Set(BoneType.Head, HeadCentre(look));
            Set(BoneType.ClavicleL, new Vector3(0f, back, FrontZ(look)));
            Set(BoneType.ClavicleR, new Vector3(0f, back, FrontZ(look)));
            Set(BoneType.TailBase, TailRoot(look));
            Set(BoneType.TailTip, TailBend(look));
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side > 0;
                float x = ShoulderWidth * side;
                Set(left ? BoneType.UpperArmL : BoneType.UpperArmR, new Vector3(x, look.Height, FrontZ(look)));
                Set(left ? BoneType.LowerArmL : BoneType.LowerArmR, new Vector3(x + half * side, look.Height, FrontZ(look)));
                Set(left ? BoneType.HandL : BoneType.HandR, new Vector3(x + look.Height * side, look.Height, FrontZ(look)));
                Set(left ? BoneType.UpperLegL : BoneType.UpperLegR, new Vector3(x, look.Height, BackZ(look)));
                Set(left ? BoneType.LowerLegL : BoneType.LowerLegR, new Vector3(x, half, BackZ(look)));
                Set(left ? BoneType.FootL : BoneType.FootR, new Vector3(x, Ankle, BackZ(look)));
            }
            return new Rig(BodyPlan.Quadruped, bones, Ankle, 0.14f, 0.1f);
        }

        // Everything on the body goes along the back, and what's on the head goes up the neck: the same as a person's skin, along another way.
        private static Mesh Spread(Mesh mesh, BeastLook look)
        {
            float hind = BackZ(look);
            float fore = FrontZ(look);
            Vector3 neck = NeckRoot(look);
            Vector3 toHead = HeadCentre(look) - neck;
            float neckLength = toHead.LengthSquared;

            for (int i = 0; i < mesh.meshVertices.Count; i++)
            {
                MeshVertex vertex = mesh.meshVertices[i];
                Vector3 at = vertex.position;
                if ((int)vertex.boneIndices.X == (int)BoneType.Chest && vertex.BoneWeights.Y <= 0f)
                {
                    float along = (at.Z - hind) / (fore - hind);
                    float pelvis = (int)BoneType.Pelvis, spine = (int)BoneType.Spine, chest = (int)BoneType.Chest;
                    if (along < 0.15f) (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(pelvis, 0f), new Vector2(1f, 0f));
                    else if (along < 0.5f) (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(pelvis, spine), Shared((along - 0.15f) / 0.35f));
                    else if (along < 0.85f) (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(spine, chest), Shared((along - 0.5f) / 0.35f));
                    else (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(chest, 0f), new Vector2(1f, 0f));
                }
                else if ((int)vertex.boneIndices.X == (int)BoneType.Head && vertex.BoneWeights.Y <= 0f)
                {
                    float along = Vector3.Dot(at - neck, toHead) / neckLength;
                    float neckBone = (int)BoneType.Neck, head = (int)BoneType.Head;
                    if (along < 0.3f) (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(neckBone, 0f), new Vector2(1f, 0f));
                    else if (along < 0.85f) (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(neckBone, head), Shared((along - 0.3f) / 0.55f));
                    else (vertex.boneIndices, vertex.BoneWeights) = (new Vector2(head, 0f), new Vector2(1f, 0f));
                }
                mesh.meshVertices[i] = vertex;
            }
            return mesh;
        }

        private static Vector2 Shared(float t)
        {
            float eased = t * t * (3f - 2f * t);
            return new Vector2(1f - eased, eased);
        }

        public static Mesh Generate(BeastLook look)
        {
            Mesh body = BuildBody(look).setAllBoneIndices(new Vector2((int)BoneType.Chest, 0f));
            if (look.Tail > 0f) body += BuildTail(look, BoneType.TailBase, 0f, 0.5f) + BuildTail(look, BoneType.TailTip, 0.5f, 1f);
            body += BuildHead(look).setAllBoneIndices(new Vector2((int)BoneType.Head, 0f));

            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side > 0;
                Vector3 outward = Vector3.UnitX * side;
                float x = ShoulderWidth * side;
                float half = look.Height * 0.5f;
                var shoulder = new Vector3(x, look.Height, FrontZ(look));
                var hip = new Vector3(x, look.Height, BackZ(look));

                // Thick at the top, thin at the bottom, as legs that run are.
                body += Limb(shoulder, shoulder + outward * half, 0.2f, 0.12f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.UpperArmL : BoneType.UpperArmR), 0f));
                body += Limb(shoulder + outward * half, shoulder + outward * look.Height * 0.93f, 0.12f, 0.08f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.LowerArmL : BoneType.LowerArmR), 0f));
                body += Paw(shoulder + outward * look.Height * 0.95f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.HandL : BoneType.HandR), 0f));

                body += Limb(hip, hip - Vector3.UnitY * half, 0.25f, 0.13f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.UpperLegL : BoneType.UpperLegR), 0f));
                body += Limb(hip - Vector3.UnitY * half, hip - Vector3.UnitY * look.Height * 0.93f, 0.13f, 0.08f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.LowerLegL : BoneType.LowerLegR), 0f));
                body += Paw(hip - Vector3.UnitY * look.Height * 0.95f, look).setAllBoneIndices(new Vector2((int)(left ? BoneType.FootL : BoneType.FootR), 0f));
            }

            body.isUsingBones = true;
            return Spread(body, look);
        }

        private static Mesh BuildTail(BeastLook look, BoneType bone, float from, float to)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            Vector3 root = TailRoot(look);
            Vector3 bend = TailBend(look);
            Vector3 tip = bend + new Vector3(0f, -look.Tail * 0.35f, -look.Tail * 0.45f);
            if (from < 0.5f) b.Tube(root, bend, 0.11f, 0.14f, 6, Fur(look.Coat));
            else b.Tube(bend, tip, 0.14f, 0.03f, 6, Fur(look.Coat));
            return b.Build().setAllBoneIndices(new Vector2((int)bone, 0f));
        }

        private static Mesh Limb(Vector3 from, Vector3 to, float top, float bottom, BeastLook look)
        {
            float thick = 0.55f + look.Girth * 0.9f;
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            b.Tube(from, to, top * thick, bottom * thick, 6, Fur(look.Coat));
            return b.Build();
        }

        private static Mesh Paw(Vector3 at, BeastLook look)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            float size = 0.1f + look.Girth * 0.08f;
            b.Add(IcoSphereGenerator.CreateIcosphere(1, Fur(look.Coat * 0.6f)), at, new Vector3(size * 1.1f, size * 0.8f, size * 1.4f));
            return b.Build();
        }

        private static Mesh BuildBody(BeastLook look)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            VertexMaterial coat = Fur(look.Coat);
            float girth = look.Girth;
            float back = -look.Length * 0.52f;
            float front = look.Length * 0.52f;
            float y = BodyHeight(look);
            float high = y + look.Hump;

            // Deep at the chest, drawn in at the loin, round at the rump.
            b.Stretch = new Vector3(0.82f, 1.08f, 1f);
            b.Tube(new Vector3(0f, y, back), new Vector3(0f, y, -look.Length * 0.12f), girth * 0.76f, girth * 0.72f, 10, coat);
            b.Tube(new Vector3(0f, y, -look.Length * 0.12f), new Vector3(0f, high, front * 0.5f), girth * 0.72f, girth * 0.96f, 10, coat, capped: false);
            b.Tube(new Vector3(0f, high, front * 0.5f), new Vector3(0f, high + 0.02f, front * 0.82f), girth * 0.96f, girth * 0.9f, 10, coat, capped: false);
            b.Tube(new Vector3(0f, high + 0.02f, front * 0.82f), new Vector3(0f, high, front * 1.08f), girth * 0.9f, girth * 0.4f, 10, coat);
            b.ResetPlacement();
            b.Stretch = Vector3.One;

            b.Add(IcoSphereGenerator.CreateIcosphere(2, Fur(look.Belly)), new Vector3(0f, y - girth * 0.55f, front * 0.3f), new Vector3(girth * 0.68f, girth * 0.5f, look.Length * 0.34f));
            b.Add(IcoSphereGenerator.CreateIcosphere(2, coat), new Vector3(0f, y, back), new Vector3(girth * 0.76f, girth * 0.82f, girth * 0.76f));
            return b.Build();
        }

        private static Mesh BuildHead(BeastLook look)
        {
            var b = new MeshBuilder { TilesPerMetre = 0.5f };
            VertexMaterial coat = Fur(look.Coat);
            Vector3 root = NeckRoot(look);
            Vector3 centre = HeadCentre(look);
            float skull = look.Skull;

            b.Tube(root, centre, look.Girth * 0.62f, skull * 0.72f, 8, coat);
            b.Add(IcoSphereGenerator.CreateIcosphere(2, coat), centre, new Vector3(skull, skull * 0.92f, skull * 1.15f));

            Vector3 tip = centre + new Vector3(0f, -skull * 0.36f, skull * 0.75f + look.Muzzle);
            b.Tube(centre + new Vector3(0f, -skull * 0.14f, skull * 0.5f), tip, skull * 0.62f, skull * 0.34f, 7, coat);
            b.Add(IcoSphereGenerator.CreateIcosphere(1, Flat(look.Nose)), tip + new Vector3(0f, skull * 0.06f, 0.02f), new Vector3(skull * 0.3f));

            for (int side = -1; side <= 1; side += 2)
            {
                b.Add(IcoSphereGenerator.CreateIcosphere(1, Flat(new Vector3(0.02f))), centre + new Vector3(side * skull * 0.55f, skull * 0.3f, skull * 0.72f), new Vector3(skull * 0.11f));

                if (look.Ears > 0f)
                {
                    Vector3 ear = centre + new Vector3(side * skull * 0.56f, skull * 0.68f, -skull * 0.15f);
                    b.Tube(ear, ear + new Vector3(side * 0.04f, look.Ears * 1.6f, -0.03f), look.Ears * 0.55f, 0.01f, 4, coat);
                }
                if (look.Tusks > 0f)
                {
                    Vector3 tusk = tip + new Vector3(side * skull * 0.34f, -skull * 0.12f, -skull * 0.3f);
                    b.Tube(tusk, tusk + new Vector3(side * 0.08f, look.Tusks, look.Tusks * 0.3f), 0.07f, 0.01f, 5, Flat(new Vector3(0.72f, 0.66f, 0.52f)));
                }
            }
            return b.Build();
        }
    }
}
