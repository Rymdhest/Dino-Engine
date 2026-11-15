using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    public static partial class SkeletonForge
    {
        /// <summary>
        /// Pairs of ribs. A person has twelve, but twelve at this size are bars and gaps of a pixel each, which only shimmer as a body moves: seven are bars of two pixels with two between, and still
        /// read as a ribcage from where the camera is. The first five reach the breastbone, the sixth reaches it by the one above's cartilage, and the last floats.
        /// </summary>
        private const int RibPairs = 7;
        private const int TrueRibs = 5;

        private const float TopRib = 3.72f;       // Where the first rib leaves the backbone.
        private const float RibSpacing = 0.115f;  // Between where each leaves it.

        // How wide and deep the ribcage is round each rib, behind the middle and in front of it, how far each falls from the back to the front, and how far round each goes, as a share of half a turn.
        private static readonly float[] CageWidth = { 0.25f, 0.36f, 0.43f, 0.47f, 0.49f, 0.49f, 0.46f };
        private static readonly float[] CageBack = { 0.2f, 0.25f, 0.28f, 0.29f, 0.3f, 0.3f, 0.29f };
        private static readonly float[] CageFront = { 0.23f, 0.29f, 0.32f, 0.34f, 0.34f, 0.33f, 0.31f };
        private static readonly float[] RibFall = { 0.12f, 0.15f, 0.18f, 0.21f, 0.24f, 0.27f, 0.2f };
        private static readonly float[] RibReach = { 0.86f, 0.84f, 0.83f, 0.82f, 0.81f, 0.78f, 0.6f };

        private const float CageMiddle = 0.0f;      // How far forward the ribcage's middle is.

        private static float RibY(int rib) => TopRib - rib * RibSpacing;

        // Where along a rib a point is, from where it leaves the backbone (0) round to the front (1, half a turn).
        private static Vector3 RibPoint(int rib, float side, float round)
        {
            float a = round * MathF.PI;
            float deep = a < MathF.PI * 0.5f ? CageBack[rib] : CageFront[rib];
            float y = RibY(rib) - RibFall[rib] * (1f - MathF.Cos(a)) * 0.5f;
            return new Vector3(side * CageWidth[rib] * MathF.Sin(a), y, CageMiddle - deep * MathF.Cos(a));
        }

        // Where the breastbone is at a height, at its front.
        private static Vector3 Sternum(float y) => new Vector3(0f, y, 0.25f + 0.08f * Math.Clamp((3.68f - y) / 0.6f, 0f, 1f));

        // ---- The neck.

        private static void Neck(SaurianMesh mesh, SkeletonBody body)
        {
            const int Count = 4;
            float from = 3.9f, to = 4.14f;
            float spacing = (to - from) / Count;
            for (int i = 0; i < Count; i++)
            {
                float y = from + spacing * (i + 0.5f);
                Vector3 centre = new Vector3(0f, y, SkeletonBody.SpineZ(y));
                Hang hang = SkeletonBody.Throat(y);
                Vector3 bone = body.Tint($"neck{i}");
                float r = 0.045f * body.Stout;
                float half = spacing * 0.36f;
                mesh.Tube(new[] { centre - Vector3.UnitY * half, centre, centre + Vector3.UnitY * half }, new[] { r * 1.1f, r * 0.85f, r * 1.1f }, 6, bone, GearMaterials.Bone, hang);
                // Its little wings out to the sides and the spine down behind it, shorter the higher up.
                for (int s = -1; s <= 1; s += 2) mesh.Tube(new[] { centre, centre + new Vector3(s * 0.075f, -0.005f, -0.01f) }, new[] { 0.02f, 0.016f }, 4, bone, GearMaterials.Bone, hang);
                float spine = 0.06f + 0.02f * (Count - 1 - i) / (Count - 1f);
                mesh.Tube(new[] { centre, centre + new Vector3(0f, -0.02f, -spine) }, new[] { 0.022f, 0.016f }, 4, bone, GearMaterials.Bone, hang);
            }
        }

        // ---- The backbone, from the hips to the neck: a vertebra where each rib leaves it, and the big ones of the small of the back below, which are what a skeleton's waist is.

        private static void Spine(SaurianMesh mesh, SkeletonBody body)
        {
            for (int rib = 0; rib < RibPairs; rib++)
            {
                float y = RibY(rib) + 0.01f;
                // Its wings are hidden behind the ribs that leave it.
                Vertebra(mesh, body, y, 0.05f * body.Stout, 0.036f, 0f, 0.1f, $"back{rib}");
            }

            const int Lumbar = 4;
            float from = 2.46f, to = RibY(RibPairs - 1) - 0.06f;
            float spacing = (to - from) / Lumbar;
            for (int i = 0; i < Lumbar; i++)
            {
                float y = from + spacing * (i + 0.5f);
                Vertebra(mesh, body, y, 0.072f * body.Stout, spacing * 0.36f, 0.14f, 0.11f, $"loin{i}");
            }
        }

        // A spool for its body, wider at its ends; wings out to the sides; and its spine back behind it.
        private static void Vertebra(SaurianMesh mesh, SkeletonBody body, float y, float radius, float half, float wing, float spine, string name)
        {
            Vector3 centre = new Vector3(0f, y, SkeletonBody.SpineZ(y));
            Hang hang = SkeletonBody.Back(y);
            Vector3 bone = body.Tint(name);
            mesh.Tube(new[] { centre - Vector3.UnitY * half, centre, centre + Vector3.UnitY * half }, new[] { radius * 1.1f, radius * 0.86f, radius * 1.1f }, 6, bone, GearMaterials.Bone, hang);
            for (int s = -1; s <= 1 && wing > 0f; s += 2)
            {
                mesh.Tube(new[] { centre + new Vector3(0f, 0f, -radius * 0.6f), centre + new Vector3(s * wing, 0.004f, -radius * 0.9f) }, new[] { radius * 0.36f, radius * 0.28f }, 4, bone, GearMaterials.Bone, hang);
            }
            mesh.Tube(new[] { centre + new Vector3(0f, 0f, -radius * 0.7f), centre + new Vector3(0f, -spine * 0.35f, -radius - spine) }, new[] { radius * 0.4f, radius * 0.24f }, 4, bone, GearMaterials.Bone, hang, Vector3.UnitX, 0.6f);
        }

        // ---- The ribs, each a flat band round from the backbone to the front, the breastbone between their ends, and the cartilage that joined them, gone dark and hard.

        private static void Ribs(SaurianMesh mesh, SkeletonBody body)
        {
            const int Steps = 6;
            for (int rib = 0; rib < RibPairs; rib++)
            {
                Hang hang = SkeletonBody.Back(RibY(rib));
                float height = 0.027f * MathF.Sqrt(body.Stout);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 bone = body.Tint($"rib{rib}{(s > 0 ? 'l' : 'r')}");
                    bool broken = rib == body.BrokenRib && s == (int)body.BrokenSide;
                    float reach = broken ? RibReach[rib] * 0.45f : RibReach[rib];

                    // From the side of its vertebra, out and back to where it turns, then round.
                    var path = new List<Vector3> { new Vector3(s * 0.07f, RibY(rib) + 0.005f, SkeletonBody.SpineZ(RibY(rib)) - 0.02f) };
                    var radii = new List<float> { height * 0.8f };
                    for (int k = 0; k <= Steps; k++)
                    {
                        float round = 0.13f + (reach - 0.13f) * k / Steps;
                        path.Add(RibPoint(rib, s, round));
                        radii.Add(height * (1f - 0.25f * k / Steps));
                    }
                    if (broken) radii[^1] = height * 0.35f;

                    // Tall and thin: a band, not a rod. The cross-section's long side is up.
                    Vector3 along = path[2] - path[1];
                    Vector3 flatten = Vector3.Cross(Vector3.UnitY, along).Normalized();
                    mesh.Tube(path, radii, 5, bone, GearMaterials.Bone, hang, flatten, 0.5f);
                    if (broken) continue;

                    // The cartilage, in to the breastbone, or for the sixth up to the fifth's, and nothing for the last.
                    if (rib < RibPairs - 1)
                    {
                        Vector3 end = path[^1];
                        float meet = rib < TrueRibs ? 3.62f - rib * 0.12f : 3.0f;
                        Vector3 sternum = Sternum(meet) + new Vector3(s * 0.045f, 0f, -0.02f);
                        Vector3 middle = Vector3.Lerp(end, sternum, 0.5f) + new Vector3(0f, rib < TrueRibs ? 0f : -0.03f, 0.02f);
                        mesh.Tube(new[] { end, middle, sternum }, new[] { height * 0.72f, height * 0.62f, height * 0.6f }, 5, bone * 0.82f, GearMaterials.Bone, hang, flatten, 0.6f);
                    }
                }
            }

            // The breastbone: a broad plate down the front, its top wider where the collarbones meet it, and a point at its foot.
            Vector3 breast = body.Tint("sternum");
            mesh.Tube(new[] { Sternum(3.7f), Sternum(3.6f), Sternum(3.45f), Sternum(3.2f), Sternum(3.03f), Sternum(2.95f) + new Vector3(0f, 0f, -0.02f) },
                new[] { 0.05f, 0.058f, 0.042f, 0.045f, 0.038f, 0.012f }, 6, breast, GearMaterials.Bone, SkeletonBody.Back(3.4f), Vector3.UnitZ, 0.45f);
        }

        // ---- The pelvis: two wings of bone flaring up and out from the hips, the wedge of the sacrum between them at the back, and the ring round under the belly, with the hip sockets in it.

        private static void Pelvis(SaurianMesh mesh, SkeletonBody body)
        {
            var field = new SaurianField();
            const SaurianPart Part = SaurianPart.Body;
            float t = body.Stout;

            for (int s = -1; s <= 1; s += 2)
            {
                // The wing, a thin plate facing forward and in, flaring out towards its top and front, with a thicker crest round its top edge.
                Vector3 wing = new Vector3(s * 0.31f, 2.33f, -0.03f);
                Vector3 forward = new Vector3(s * 0.5f, 0f, 0.87f).Normalized();
                Vector3 up = new Vector3(s * 0.32f, 1f, 0f).Normalized();
                field.Ellipsoid(Part, wing, new Vector3(0.032f, 0.17f, 0.2f), 0.03f, forward, up);
                field.Cone(Part, new Vector3(s * 0.15f, 2.45f, -0.19f), new Vector3(s * 0.33f, 2.5f, -0.07f), 0.03f, 0.034f, 0.02f);
                field.Cone(Part, new Vector3(s * 0.33f, 2.5f, -0.07f), new Vector3(s * 0.44f, 2.41f, 0.11f), 0.034f, 0.028f, 0.02f);

                // Down from the wing to the socket the thigh turns in, which opens out and down round where its head is.
                Vector3 socket = new Vector3(s * SkeletonBody.HipX, SkeletonBody.Hip, 0f);
                field.Cone(Part, new Vector3(s * 0.29f, 2.24f, -0.03f), new Vector3(s * 0.23f, 2.05f, 0f), 0.075f * t, 0.07f * t, 0.04f);
                field.Ellipsoid(Part, socket + new Vector3(-s * 0.03f, 0.03f, 0f), new Vector3(0.088f, 0.085f, 0.088f) * t, 0.03f);
                field.Ellipsoid(Part, socket + new Vector3(s * 0.01f, -0.01f, 0f), new Vector3(0.078f) * t, 0.012f, carve: true);

                // The ring: in from the back to the socket, then forward and in to where the two sides meet under the belly, and down and back to the bone that's sat on, leaving a hole between.
                field.Cone(Part, new Vector3(s * 0.08f, 2.3f, -0.17f), new Vector3(s * 0.2f, 2.12f, -0.01f), 0.03f, 0.035f, 0.02f);
                field.Cone(Part, new Vector3(s * 0.22f, 1.99f, 0.05f), new Vector3(s * 0.035f, 1.9f, 0.15f), 0.04f * t, 0.034f * t, 0.025f);
                field.Cone(Part, new Vector3(s * 0.24f, 1.95f, -0.03f), new Vector3(s * 0.15f, 1.79f, -0.05f), 0.05f * t, 0.045f * t, 0.025f);
                field.Cone(Part, new Vector3(s * 0.15f, 1.79f, -0.05f), new Vector3(s * 0.05f, 1.85f, 0.12f), 0.034f * t, 0.03f * t, 0.02f);

                // Where the sacrum's sides meet the wings.
                field.Cone(Part, new Vector3(s * 0.06f, 2.38f, -0.19f), new Vector3(s * 0.2f, 2.36f, -0.13f), 0.045f, 0.05f, 0.03f);
            }
            field.Ellipsoid(Part, new Vector3(0f, 1.9f, 0.15f), new Vector3(0.045f, 0.06f, 0.035f), 0.03f);

            // The sacrum, broad and flat, tapering down to the tail's stub.
            field.Cone(Part, new Vector3(0f, 2.44f, -0.17f), new Vector3(0f, 2.06f, -0.22f), 0.07f, 0.03f, 0.03f, 0.55f, Vector3.UnitZ);
            field.Cone(Part, new Vector3(0f, 2.06f, -0.22f), new Vector3(0f, 1.97f, -0.19f), 0.03f, 0.015f, 0.02f);

            Vector3 bone = body.Tint("pelvis");
            Hang hang = SkeletonBody.On(BoneType.Pelvis);
            var dress = new Dresser(GearMaterials.Bone, (p, n) => bone, _ => hang);
            SaurianSkin.Grow(mesh, field, 0.0105f, 650, float.PositiveInfinity, dress, settle: true);
        }

        // ---- The shoulders: a collarbone from the top of the breastbone out to each, and a shoulder blade flat on the back of the ribs, both on the bone that shrugs.

        private static void Shoulders(SaurianMesh mesh, SkeletonBody body)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Hang hang = SkeletonBody.On(s > 0 ? BoneType.ClavicleL : BoneType.ClavicleR);
                Vector3 bone = body.Tint($"collar{(s > 0 ? 'l' : 'r')}");
                float r = 0.027f * body.Stout;
                mesh.Tube(new[] { Sternum(3.69f) + new Vector3(s * 0.045f, 0.01f, -0.01f), new Vector3(s * 0.14f, 3.73f, 0.21f), new Vector3(s * 0.27f, 3.77f, 0.15f), new Vector3(s * 0.38f, 3.84f, 0.02f), new Vector3(s * 0.42f, 3.87f, -0.05f) },
                    new[] { r * 1.25f, r, r * 0.9f, r, r * 1.2f }, 5, bone, GearMaterials.Bone, hang);

                // The blade, a flat triangle on the back of the ribs with a ridge across it out to the point over the shoulder.
                Vector3 blade = body.Tint($"blade{(s > 0 ? 'l' : 'r')}");
                var outline = new[]
                {
                    new Vector3(s * 0.4f, 3.75f, -0.08f),
                    new Vector3(s * 0.31f, 3.74f, -0.21f),
                    new Vector3(s * 0.15f, 3.69f, -0.27f),
                    new Vector3(s * 0.13f, 3.5f, -0.3f),
                    new Vector3(s * 0.2f, 3.2f, -0.29f),
                    new Vector3(s * 0.36f, 3.56f, -0.19f),
                };
                Vector3 middle = new Vector3(s * 0.26f, 3.55f, -0.24f);
                Vector3 facing = new Vector3(s * 0.42f, 0.1f, -1f).Normalized();
                mesh.Sheet(outline, middle, facing, 0.024f, blade, blade * 0.85f, GearMaterials.Bone, _ => hang);
                mesh.Tube(new[] { new Vector3(s * 0.16f, 3.66f, -0.29f), new Vector3(s * 0.3f, 3.72f, -0.26f), new Vector3(s * 0.42f, 3.87f, -0.1f) }, new[] { 0.016f, 0.022f, 0.028f }, 4, blade, GearMaterials.Bone, hang);
            }
        }
    }
}
