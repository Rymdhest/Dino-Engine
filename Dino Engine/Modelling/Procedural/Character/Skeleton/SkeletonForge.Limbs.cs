using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    public static partial class SkeletonForge
    {
        // How far past the wrist the middle of a fist is, which is where whatever's held goes through it (HumanoidGenerator.MainHand), and how far round that its fingers curl.
        private const float FistAt = 0.15f;
        private const float FistRound = 0.06f;

        // ---- An arm: out along x from the shoulder, as the rig has it standing with its arms out. A thick bone to the elbow, and the two that cross to the wrist side by side, seen apart from the front
        // once the arm hangs.

        private static void Arm(SaurianMesh mesh, SkeletonBody body, int side, bool holds)
        {
            float s = side;
            float y = SkeletonBody.ShoulderY;
            float t = body.Stout;
            char name = side > 0 ? 'l' : 'r';
            Hang upper = SkeletonBody.On(BodyForm.UpperArm(s));
            Hang lower = SkeletonBody.On(BodyForm.LowerArm(s));

            Vector3 humerus = body.Tint($"humerus{name}");
            mesh.Ball(new Vector3(s * (SkeletonBody.ShoulderX - 0.005f), y + 0.01f, -0.01f), new Vector3(0.066f * t), 1, humerus, GearMaterials.Bone, upper);
            LongBone(mesh, new Vector3(s * (SkeletonBody.ShoulderX + 0.04f), y, 0f), new Vector3(s * (SkeletonBody.ElbowX - 0.07f), y, 0f), 0.046f * t, humerus, upper);
            // Its foot widens across the hinge the forearm bends on.
            mesh.Ball(new Vector3(s * (SkeletonBody.ElbowX - 0.035f), y, -0.005f), new Vector3(0.04f, 0.074f, 0.048f) * t, 1, humerus, GearMaterials.Bone, upper);

            Vector3 ulna = body.Tint($"ulna{name}");
            Vector3 radius = body.Tint($"radius{name}");
            float wrist = SkeletonBody.WristX - 0.03f;
            mesh.Ball(new Vector3(s * (SkeletonBody.ElbowX + 0.005f), y - 0.02f, -0.05f), new Vector3(0.04f, 0.04f, 0.042f) * t, 1, ulna, GearMaterials.Bone, lower);
            LongBone(mesh, new Vector3(s * (SkeletonBody.ElbowX + 0.01f), y - 0.03f, -0.025f), new Vector3(s * wrist, y - 0.03f, 0f), 0.029f * t, ulna, lower, 6);
            LongBone(mesh, new Vector3(s * (SkeletonBody.ElbowX + 0.025f), y + 0.035f, 0.01f), new Vector3(s * wrist, y + 0.03f, 0.01f), 0.029f * t, radius, lower, 6);
            mesh.Ball(new Vector3(s * (wrist - 0.01f), y + 0.028f, 0.01f), new Vector3(0.03f, 0.038f, 0.036f) * t, 1, radius, GearMaterials.Bone, lower);

            Hand(mesh, body, side, holds);
        }

        // ---- A hand, its back up as the rig has it. Closed round whatever's held, which goes through the fist front to back, or hanging open and a little curled if it's empty.

        private static void Hand(SaurianMesh mesh, SkeletonBody body, int side, bool holds)
        {
            float s = side;
            Hang hang = SkeletonBody.On(BodyForm.Hand(s));
            Vector3 bone = body.Tint($"hand{(side > 0 ? 'l' : 'r')}");
            Vector3 wrist = new Vector3(s * SkeletonBody.WristX, SkeletonBody.ShoulderY, 0f);
            Vector3 a = new Vector3(s, 0f, 0f), u = Vector3.UnitY, f = Vector3.UnitZ;
            Vector3 grip = wrist + a * FistAt;
            float thin = body.Stout;

            // The knot of little bones at the wrist.
            mesh.Ball(wrist + a * 0.045f + u * 0.008f, new Vector3(0.042f, 0.032f, 0.064f) * thin, 1, bone, GearMaterials.Bone, hang);

            // Four fingers, the first nearest the thumb, which points forward with the hand's back up.
            float[] spread = { 0.05f, 0.017f, -0.017f, -0.05f };
            float[] length = { 0.95f, 1f, 0.95f, 0.8f };
            for (int i = 0; i < 4; i++)
            {
                Vector3 knuckle = wrist + a * 0.17f + u * 0.052f + f * spread[i];
                Phalanx(mesh, wrist + a * 0.06f + u * 0.024f + f * (spread[i] * 0.6f), knuckle, 0.015f * thin, bone, hang);
                float l = length[i];
                Vector3[] joints;
                if (holds)
                {
                    // Round the grip from over it to under it: each joint a point on a circle about it, the bones shortening towards the tip.
                    Vector3 On(float degrees)
                    {
                        float r = degrees * MathF.PI / 180f;
                        return grip + f * spread[i] + (a * MathF.Sin(r) + u * MathF.Cos(r)) * FistRound;
                    }
                    joints = new[] { knuckle, On(20f + 72f * l), On(20f + 128f * l), On(20f + 168f * l) };
                }
                else
                {
                    joints = new[] { knuckle, knuckle + (a * 0.075f - u * 0.014f) * l, knuckle + (a * 0.128f - u * 0.036f) * l, knuckle + (a * 0.164f - u * 0.06f) * l };
                }
                for (int j = 0; j < 3; j++) Phalanx(mesh, joints[j], joints[j + 1], (0.0145f - 0.0015f * j) * thin, bone, hang);
            }

            // The thumb, out from the wrist on the front, and round over the first finger if the hand is closed.
            Vector3 root = wrist + a * 0.04f - u * 0.012f + f * 0.045f;
            Vector3[] thumb = holds
                ? new[] { root, wrist + a * 0.09f - u * 0.045f + f * 0.085f, grip + (a * 0.766f - u * 0.643f) * 0.085f + f * 0.078f, grip + (a * 0.996f - u * 0.087f) * 0.08f + f * 0.072f }
                : new[] { root, wrist + a * 0.1f - u * 0.025f + f * 0.1f, wrist + a * 0.15f - u * 0.035f + f * 0.13f, wrist + a * 0.19f - u * 0.045f + f * 0.148f };
            for (int j = 0; j < 3; j++) Phalanx(mesh, thumb[j], thumb[j + 1], (0.017f - 0.002f * j) * thin, bone, hang);
        }

        // ---- A leg: the thigh's bone from its ball in the hip socket, the kneecap, the shin's two bones and the foot.

        private static void Leg(SaurianMesh mesh, SkeletonBody body, int side)
        {
            float s = side;
            float t = body.Stout;
            char name = side > 0 ? 'l' : 'r';
            float x = s * SkeletonBody.HipX;
            Hang upper = SkeletonBody.On(BodyForm.UpperLeg(s));
            Hang lower = SkeletonBody.On(BodyForm.LowerLeg(s));

            // The thigh: its head in the socket, a neck out to the knob on the outside of the hip, and the long shaft in again to the two knobs of the knee.
            Vector3 femur = body.Tint($"femur{name}");
            Vector3 head = new Vector3(x, SkeletonBody.Hip, 0f);
            Vector3 knob = new Vector3(x + s * 0.11f, SkeletonBody.Hip - 0.07f, -0.02f);
            mesh.Ball(head, new Vector3(0.07f * t), 1, femur, GearMaterials.Bone, upper);
            mesh.Tube(new[] { head, Vector3.Lerp(head, knob, 0.5f) + new Vector3(0f, 0.01f, 0f), knob }, new[] { 0.05f * t, 0.042f * t, 0.05f * t }, 6, femur, GearMaterials.Bone, upper);
            mesh.Ball(knob, new Vector3(0.05f, 0.064f, 0.055f) * t, 1, femur, GearMaterials.Bone, upper);
            LongBone(mesh, new Vector3(x + s * 0.1f, SkeletonBody.Hip - 0.12f, -0.01f), new Vector3(x + s * 0.01f, SkeletonBody.Knee + 0.07f, 0f), 0.054f * t, femur, upper);
            for (int k = -1; k <= 1; k += 2) mesh.Ball(new Vector3(x + k * 0.045f, SkeletonBody.Knee + 0.04f, -0.01f), new Vector3(0.05f * t), 1, femur, GearMaterials.Bone, upper);

            // The kneecap goes with the shin as the tendon under it does, and a little with the thigh it lies on.
            mesh.Ball(new Vector3(x, SkeletonBody.Knee + 0.025f, 0.075f), new Vector3(0.04f, 0.05f, 0.022f) * t, 1, body.Tint($"knee{name}"), GearMaterials.Bone,
                new Hang(BodyForm.LowerLeg(s), BodyForm.UpperLeg(s), 0.6f));

            // The shin: a broad top for the knee to sit on, the thick bone down the front and inside, and the thin one beside it outside.
            Vector3 tibia = body.Tint($"tibia{name}");
            Vector3 fibula = body.Tint($"fibula{name}");
            mesh.Ball(new Vector3(x, SkeletonBody.Knee - 0.045f, 0f), new Vector3(0.082f, 0.04f, 0.07f) * t, 1, tibia, GearMaterials.Bone, lower);
            LongBone(mesh, new Vector3(x - s * 0.005f, SkeletonBody.Knee - 0.06f, 0.012f), new Vector3(x - s * 0.015f, SkeletonBody.Ankle + 0.06f, 0f), 0.045f * t, tibia, lower);
            mesh.Ball(new Vector3(x - s * 0.045f, SkeletonBody.Ankle + 0.03f, 0f), new Vector3(0.034f) * t, 1, tibia, GearMaterials.Bone, lower);
            LongBone(mesh, new Vector3(x + s * 0.065f, SkeletonBody.Knee - 0.09f, -0.035f), new Vector3(x + s * 0.05f, SkeletonBody.Ankle + 0.06f, -0.02f), 0.022f * t, fibula, lower, 5);
            mesh.Ball(new Vector3(x + s * 0.05f, SkeletonBody.Ankle + 0.02f, -0.02f), new Vector3(0.028f) * t, 1, fibula, GearMaterials.Bone, lower);

            Foot(mesh, body, side);
        }

        // ---- A foot: the bone the shin sits on, the heel behind and under it, a knot of bones over the arch, and five long bones fanning out to the toes, the great toe inside.

        private static void Foot(SaurianMesh mesh, SkeletonBody body, int side)
        {
            float s = side;
            float x = s * SkeletonBody.HipX;
            Hang hang = SkeletonBody.On(BodyForm.Foot(s));
            Vector3 bone = body.Tint($"foot{(side > 0 ? 'l' : 'r')}");
            float t = body.Stout;

            mesh.Ball(new Vector3(x, SkeletonBody.Ankle - 0.025f, 0f), new Vector3(0.054f, 0.044f, 0.068f) * t, 1, bone, GearMaterials.Bone, hang);
            mesh.Tube(new[] { new Vector3(x, 0.19f, -0.02f), new Vector3(x + s * 0.005f, 0.1f, -0.1f), new Vector3(x + s * 0.005f, 0.062f, -0.132f) }, new[] { 0.048f * t, 0.05f * t, 0.044f * t }, 6, bone, GearMaterials.Bone, hang);
            mesh.Ball(new Vector3(x, 0.15f, 0.09f), new Vector3(0.068f, 0.044f, 0.06f) * t, 1, bone, GearMaterials.Bone, hang);

            for (int k = 0; k < 5; k++)
            {
                bool great = k == 0;
                Vector3 from = new Vector3(x + s * (-0.04f + k * 0.02f), 0.13f, 0.12f);
                Vector3 ball = new Vector3(x + s * (-0.066f + k * 0.034f), 0.054f, 0.29f - k * 0.018f);
                Vector3 middle = new Vector3(ball.X, 0.036f, ball.Z + (great ? 0.07f : 0.055f) - k * 0.004f);
                Vector3 tip = new Vector3(ball.X, 0.03f, middle.Z + (great ? 0.045f : 0.03f));
                float thick = (great ? 0.024f : 0.016f) * t;
                Phalanx(mesh, from, ball, thick, bone, hang);
                Phalanx(mesh, ball, middle, thick * 0.85f, bone, hang);
                Phalanx(mesh, middle, tip, thick * 0.75f, bone, hang);
            }
        }
    }
}
