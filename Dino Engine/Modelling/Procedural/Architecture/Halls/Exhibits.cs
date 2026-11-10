using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The Archive's displays, one for each record a player's heroes keep, each something that says what it keeps at a glance from the camera: a tyrant's skull for the tyrants felled, a
    /// boulder split by a blade for the hardest blow, an hourglass for the swiftest land. Each faces +z, the carpet up the middle of the hall, with a small stone placard at its foot. What
    /// the record is comes from the game when someone walks up to it.
    /// </summary>
    internal static class Exhibits
    {
        private static VertexMaterial Bone => Hall.Bone(214, 200, 172);
        private static VertexMaterial OldBone => Hall.Bone(176, 160, 132);
        private static VertexMaterial Plinth => Hall.Stone(200, 190, 172);
        private static VertexMaterial PlinthDark => Hall.Stone(150, 142, 128);

        public static void Build(MeshBuilder b, ExhibitKind kind, Vector2 half, float h, HallKit kit, List<AssetLight> lights, WorldRandom random)
        {
            switch (kind)
            {
                case ExhibitKind.TyrantsFelled: TyrantSkull(b, half, h, lights); break;
                case ExhibitKind.FoesSlain: Tally(b, half, h, random); break;
                case ExhibitKind.HardestBlow: SplitStone(b, half, h); break;
                case ExhibitKind.UniquesFound: Cabinet(b, half, h, lights, random); break;
                case ExhibitKind.SwiftestLand: Hourglass(b, half, h, lights); break;
                case ExhibitKind.Falls: Memorial(b, half, h, lights, random); break;
                case ExhibitKind.LandsFreed: MapTable(b, half, h, random); break;
                case ExhibitKind.GoldGathered: Hoard(b, half, h, lights, random); break;
                default: Climb(b, half, h, lights); break;
            }
            Placard(b, new Vector3(0f, 0f, half.Y - 0.25f));
        }

        // A small slanted stone at an exhibit's foot, where its record would be cut.
        private static void Placard(MeshBuilder b, Vector3 at)
        {
            b.Box(at + Vector3.UnitY * 0.35f, new Vector3(0.12f, 0.35f, 0.1f), PlinthDark);
            Hall.At(b, at + Vector3.UnitY * 0.72f, 0f, () =>
            {
                b.PlaneQuad(new Vector3(-0.42f, -0.06f, 0.2f), new Vector3(0.42f, -0.06f, 0.2f), new Vector3(0.42f, 0.16f, -0.14f), new Vector3(-0.42f, 0.16f, -0.14f), Plinth,
                    Vector3.Zero, Vector3.UnitX, Vector3.UnitY, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
                b.Box(new Vector3(0f, 0.04f, 0.02f), new Vector3(0.42f, 0.1f, 0.15f), PlinthDark);
                for (int line = 0; line < 3; line++)
                {
                    float y = 0.11f - line * 0.07f, z = -0.08f + line * 0.1f;
                    b.Box(new Vector3(0f, y + 0.01f, z), new Vector3(0.3f - line * 0.05f, 0.006f, 0.012f), Hall.Flat(60, 54, 46));
                }
            });
        }

        // A moulded stone plinth of two tiers: a base, a body, and a cap.
        private static float Tiers(MeshBuilder b, Vector2 half, float height)
        {
            b.Box(new Vector3(0f, height * 0.12f, 0f), new Vector3(half.X, height * 0.12f, half.Y), PlinthDark);
            b.Box(new Vector3(0f, height * 0.52f, 0f), new Vector3(half.X - 0.18f, height * 0.3f, half.Y - 0.18f), Plinth);
            b.Box(new Vector3(0f, height * 0.91f, 0f), new Vector3(half.X - 0.06f, height * 0.09f, half.Y - 0.06f), PlinthDark);
            return height;
        }

        /// <summary>
        /// The skull of a tyrant, as long as a person is tall, on a stone plinth, its jaws a little open: the cranium high at the back with its eyes and the holes in front of them, the long
        /// snout, the lower jaw, and a row of teeth along each.
        /// </summary>
        private static void TyrantSkull(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            float top = Tiers(b, new Vector2(half.X, half.Y - 0.4f), 1.4f);
            // A cradle of dark wood under it.
            foreach (float z in new[] { -0.8f, 0.7f })
            {
                b.Box(new Vector3(0f, top + 0.25f, z), new Vector3(0.9f, 0.25f, 0.12f), Hall.Wood(76, 50, 32));
            }
            Hall.At(b, new Vector3(0f, top + 1.6f, 0.1f), 0f, () => Skull(b));
            // A low light on it, so that it reads from the far end of the hall.
            lights.Add(AssetLight.Glow(new Vector3(0f, top + 3.5f, half.Y + 0.6f), new Vector3(1f, 0.78f, 0.5f), 18f, 9f));
        }

        private static void Skull(MeshBuilder b)
        {
            VertexMaterial bone = Bone, old = OldBone;
            VertexMaterial hollow = Hall.Flat(26, 22, 20);
            // Upper skull: a tapering run of sections from the back of the head to the tip of the snout, each a box a little narrower and lower than the last.
            (float z, float w, float top, float bottom)[] sections =
            {
                (-1.7f, 0.62f, 0.95f, -0.25f), (-1.25f, 0.72f, 1.05f, -0.35f), (-0.75f, 0.66f, 0.85f, -0.4f), (-0.2f, 0.52f, 0.6f, -0.4f),
                (0.4f, 0.42f, 0.42f, -0.38f), (1.0f, 0.34f, 0.3f, -0.36f), (1.6f, 0.27f, 0.2f, -0.32f), (2.05f, 0.2f, 0.12f, -0.28f),
            };
            for (int i = 0; i + 1 < sections.Length; i++)
            {
                var a = sections[i];
                var c = sections[i + 1];
                Loft(b, a.z, a.w, a.top, a.bottom, c.z, c.w, c.top, c.bottom, i % 2 == 0 ? bone : old);
            }
            // Its back, closed.
            var back = sections[0];
            b.Quad(new Vector3(back.w, back.bottom, back.z), new Vector3(-back.w, back.bottom, back.z), new Vector3(-back.w, back.top, back.z), new Vector3(back.w, back.top, back.z), old);
            // The ridges over the eyes, and the eyes and the great holes before them, dark.
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(s * 0.62f, 0.68f, -1.0f), new Vector3(0.08f, 0.2f, 0.18f), hollow);
                b.Box(new Vector3(s * 0.6f, 0.92f, -0.98f), new Vector3(0.14f, 0.07f, 0.26f), bone);
                b.Box(new Vector3(s * 0.48f, 0.25f, -0.15f), new Vector3(0.06f, 0.24f, 0.4f), hollow);
                b.Box(new Vector3(s * 0.33f, 0.05f, 1.05f), new Vector3(0.03f, 0.1f, 0.22f), hollow);
                // The holes at the back of the head, behind the eyes.
                b.Box(new Vector3(s * 0.55f, 0.35f, -1.5f), new Vector3(0.1f, 0.25f, 0.18f), hollow);
            }
            // The lower jaw, dropped a little open, from under the back of the head to the chin.
            Hall.At(b, new Vector3(0f, -0.36f, -1.2f), 0f, () =>
            {
                float drop = 0.18f;
                (float z, float w, float top, float bottom)[] jaw =
                {
                    (-0.3f, 0.6f, 0.05f, -0.75f), (0.4f, 0.55f, -0.05f - drop * 0.3f, -0.62f - drop * 0.3f), (1.3f, 0.44f, -0.12f - drop * 0.6f, -0.5f - drop * 0.6f),
                    (2.2f, 0.34f, -0.15f - drop, -0.42f - drop), (3.1f, 0.22f, -0.16f - drop * 1.3f, -0.36f - drop * 1.3f),
                };
                for (int i = 0; i + 1 < jaw.Length; i++)
                {
                    var a = jaw[i];
                    var c = jaw[i + 1];
                    Loft(b, a.z, a.w, a.top, a.bottom, c.z, c.w, c.top, c.bottom, old);
                }
                // Lower teeth.
                for (int i = 0; i < 11; i++)
                {
                    float t = i / 10f;
                    float z = MathHelper.Lerp(0.5f, 3.0f, t);
                    float w = MathHelper.Lerp(0.5f, 0.24f, t) - 0.06f;
                    float y = MathHelper.Lerp(-0.08f, -0.16f, t) - drop * MathHelper.Lerp(0.4f, 1.25f, t);
                    foreach (float s in new[] { -1f, 1f }) Tooth(b, new Vector3(s * w, y, z), 0.24f + 0.08f * MathF.Sin(i * 1.7f), true);
                }
            });
            // Upper teeth, long and curved back, along the edge of the upper jaw.
            for (int i = 0; i < 13; i++)
            {
                float t = i / 12f;
                float z = MathHelper.Lerp(-0.5f, 1.95f, t);
                float w = MathHelper.Lerp(0.56f, 0.2f, t) - 0.05f;
                float y = MathHelper.Lerp(-0.4f, -0.3f, t);
                foreach (float s in new[] { -1f, 1f }) Tooth(b, new Vector3(s * w, y, z), 0.32f + 0.1f * MathF.Sin(i * 2.3f + 0.4f), false);
            }
        }

        // One section of a skull to the next: four sides from one rectangle to another, each wound to face out.
        private static void Loft(MeshBuilder b, float z0, float w0, float t0, float b0, float z1, float w1, float t1, float b1, VertexMaterial material)
        {
            Vector3 a0 = new(-w0, b0, z0), a1 = new(w0, b0, z0), a2 = new(w0, t0, z0), a3 = new(-w0, t0, z0);
            Vector3 c0 = new(-w1, b1, z1), c1 = new(w1, b1, z1), c2 = new(w1, t1, z1), c3 = new(-w1, t1, z1);
            b.Quad(a2, a3, c3, c2, material);   // top
            b.Quad(a0, a1, c1, c0, material);   // bottom
            b.Quad(a1, a2, c2, c1, material);   // +x side
            b.Quad(a3, a0, c0, c3, material);   // -x side
            if (w1 < 0.25f) b.Quad(c0, c1, c2, c3, material);   // the tip
        }

        private static void Tooth(MeshBuilder b, Vector3 root, float length, bool upward)
        {
            Vector3 tip = root + new Vector3(0f, upward ? length : -length, upward ? -0.04f : -0.06f);
            b.Tube(root, tip, 0.055f, 0.006f, 5, Hall.Bone(232, 222, 198));
        }

        /// <summary>A tall frame of dark wood hung with what was taken off the fallen, claws and teeth on cords, round a board cut with tally marks in fives.</summary>
        private static void Tally(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(66, 44, 30);
            b.Box(new Vector3(0f, 0.3f, 0f), new Vector3(half.X - 0.2f, 0.3f, half.Y - 0.6f), PlinthDark);
            float top = h - 0.3f;
            foreach (float x in new[] { -half.X + 0.4f, half.X - 0.4f })
            {
                Hall.Turned(b, new Vector3(x, 0.6f, -0.2f), wood, 8, (0f, 0.16f), (0.2f, 0.12f), (top - 0.8f, 0.11f), (top - 0.6f, 0.17f), (top - 0.5f, 0.12f));
            }
            for (int rail = 0; rail < 3; rail++)
            {
                float y = top - 0.35f - rail * 1.55f;
                b.Box(new Vector3(0f, y, -0.2f), new Vector3(half.X - 0.3f, 0.08f, 0.08f), wood);
                // Trophies on cords from this rail.
                for (float x = -half.X + 0.75f; x < half.X - 0.6f; x += random.Range(0.22f, 0.34f))
                {
                    float cord = random.Range(0.3f, 0.7f);
                    Vector3 hang = new Vector3(x, y - 0.08f, -0.2f + random.Range(-0.05f, 0.05f));
                    b.Beam(hang, hang - Vector3.UnitY * cord, 0.008f, Hall.Flat(70, 56, 40));
                    Vector3 end = hang - Vector3.UnitY * cord;
                    if (random.Chance(0.6f))
                    {
                        // A claw, curved, hanging point down.
                        b.Tube(end, end + new Vector3(0.05f, -0.22f, 0.06f), 0.06f, 0.035f, 5, Bone);
                        b.Tube(end + new Vector3(0.05f, -0.22f, 0.06f), end + new Vector3(0.0f, -0.38f, 0.14f), 0.035f, 0.004f, 5, Bone);
                    }
                    else b.Tube(end, end - Vector3.UnitY * random.Range(0.18f, 0.3f), 0.05f, 0.005f, 5, OldBone);
                }
            }
            // The board in the middle, cut with tallies.
            float boardY = h * 0.42f;
            b.Box(new Vector3(0f, boardY, -0.1f), new Vector3(half.X * 0.6f, 0.75f, 0.06f), Hall.Wood(150, 116, 80));
            VertexMaterial cut = Hall.Flat(44, 32, 22);
            for (int row = 0; row < 4; row++)
            {
                float y = boardY + 0.5f - row * 0.32f;
                for (int group = 0; group < 4; group++)
                {
                    float x0 = -half.X * 0.5f + group * half.X * 0.27f;
                    for (int mark = 0; mark < 4; mark++) b.Box(new Vector3(x0 + mark * 0.07f, y, -0.03f), new Vector3(0.012f, 0.11f, 0.01f), cut);
                    b.Beam(new Vector3(x0 - 0.05f, y - 0.1f, -0.025f), new Vector3(x0 + 0.26f, y + 0.1f, -0.025f), 0.012f, cut);
                }
            }
        }

        /// <summary>A boulder split clean through by one blow, its halves fallen apart on a low plinth, and the greatsword that did it broken off in the cut, its point lying in front.</summary>
        private static void SplitStone(MeshBuilder b, Vector2 half, float h)
        {
            b.Box(new Vector3(0f, 0.3f, 0f), new Vector3(half.X, 0.3f, half.Y - 0.35f), PlinthDark);
            b.Box(new Vector3(0f, 0.64f, 0f), new Vector3(half.X - 0.12f, 0.04f, half.Y - 0.47f), Plinth);
            VertexMaterial rock = Hall.Rough(150);
            float top = 0.68f;
            foreach (float s in new[] { -1f, 1f })
            {
                // Each half a heap of tapering blocks, its cut face flat and pale towards the other.
                Hall.At(b, new Vector3(s * 0.62f, top, -0.1f), s * 0.18f, () =>
                {
                    b.Box(new Vector3(s * 0.45f, 0.75f, 0f), new Vector3(0.55f, 0.75f, 0.9f), rock);
                    b.Box(new Vector3(s * 0.35f, 1.75f, -0.05f), new Vector3(0.42f, 0.3f, 0.72f), rock);
                    b.Box(new Vector3(s * 0.28f, 2.2f, 0f), new Vector3(0.3f, 0.18f, 0.5f), rock);
                    b.Box(new Vector3(-s * 0.08f, 1.15f, 0f), new Vector3(0.02f, 1.1f, 0.85f), Hall.Rough(196));
                });
            }
            // The sword, its hilt standing out of the cut and its blade gone in.
            VertexMaterial steel = Hall.Metal(186, 188, 196);
            b.Box(new Vector3(0f, top + 2.2f, -0.1f), new Vector3(0.05f, 0.95f, 0.13f), steel);
            b.Box(new Vector3(0f, top + 3.2f, -0.1f), new Vector3(0.09f, 0.06f, 0.55f), Hall.Iron(90));
            b.Tube(new Vector3(0f, top + 3.25f, -0.1f), new Vector3(0f, top + 3.95f, -0.1f), 0.06f, 0.05f, 6, Hall.Leather(64, 38, 24));
            b.Tube(new Vector3(0f, top + 3.95f, -0.1f), new Vector3(0f, top + 4.12f, -0.1f), 0.12f, 0.1f, 8, Hall.Iron(96));
            // Its point, snapped off, on the plinth in front.
            Hall.At(b, new Vector3(0.3f, top + 0.04f, half.Y - 0.85f), 0.4f, () =>
            {
                b.Box(new Vector3(0f, 0f, 0f), new Vector3(0.62f, 0.02f, 0.11f), steel);
                b.Tube(new Vector3(0.62f, 0f, 0f), new Vector3(0.95f, 0f, 0f), 0.11f, 0.005f, 4, steel);
            });
        }

        /// <summary>A cabinet of curiosities: a carved base, and above it shelves behind slender glazing bars with the uniques found set out on them, each glowing, and a lamp inside.</summary>
        private static void Cabinet(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(74, 48, 32);
            VertexMaterial trim = Hall.Wood(110, 76, 50);
            float baseTop = 1.5f;
            b.Box(new Vector3(0f, baseTop * 0.5f, 0f), new Vector3(half.X, baseTop * 0.5f, half.Y), wood);
            foreach (float x in new[] { -half.X * 0.5f, half.X * 0.5f }) b.Box(new Vector3(x, baseTop * 0.5f, half.Y + 0.01f), new Vector3(half.X * 0.42f, baseTop * 0.32f, 0.03f), trim);
            // The case: back, sides, top, and glazing bars across the front.
            float caseTop = h - 0.3f;
            b.Box(new Vector3(0f, (baseTop + caseTop) * 0.5f, -half.Y + 0.06f), new Vector3(half.X - 0.05f, (caseTop - baseTop) * 0.5f, 0.06f), Hall.Cloth(32, 44, 70));
            foreach (float x in new[] { -half.X + 0.06f, half.X - 0.06f }) b.Box(new Vector3(x, (baseTop + caseTop) * 0.5f, 0f), new Vector3(0.06f, (caseTop - baseTop) * 0.5f, half.Y - 0.05f), wood);
            b.Box(new Vector3(0f, caseTop + 0.15f, 0.04f), new Vector3(half.X + 0.12f, 0.15f, half.Y + 0.08f), wood);
            for (int i = 0; i <= 4; i++) b.Box(new Vector3(-half.X + i * half.X * 0.5f, (baseTop + caseTop) * 0.5f, half.Y - 0.04f), new Vector3(0.035f, (caseTop - baseTop) * 0.5f, 0.035f), trim);
            b.Box(new Vector3(0f, (baseTop + caseTop) * 0.5f, half.Y - 0.04f), new Vector3(half.X, 0.03f, 0.03f), trim);
            // Shelves with what's been found, each a little glowing thing.
            Vector3[] colours = { new(1f, 0.55f, 0.15f), new(1f, 0.7f, 0.25f), new(0.95f, 0.45f, 0.12f) };
            for (int shelf = 0; shelf < 3; shelf++)
            {
                float y = baseTop + 0.1f + shelf * (caseTop - baseTop - 0.2f) / 3f;
                b.Box(new Vector3(0f, y, 0f), new Vector3(half.X - 0.12f, 0.03f, half.Y - 0.12f), trim);
                for (float x = -half.X + 0.4f; x < half.X - 0.3f; x += random.Range(0.5f, 0.75f))
                {
                    Vector3 c = random.Pick(colours);
                    VertexMaterial glow = Hall.Gem(c.X, c.Y, c.Z, 1.6f);
                    Vector3 at = new Vector3(x, y + 0.03f, random.Range(-0.15f, 0.15f));
                    switch (random.Int(4))
                    {
                        case 0:     // A ring on a little stand.
                            b.Tube(at, at + Vector3.UnitY * 0.1f, 0.06f, 0.05f, 6, trim);
                            for (int k = 0; k < 8; k++)
                            {
                                float a0 = MathF.Tau * k / 8f, a1 = MathF.Tau * (k + 1) / 8f;
                                b.Beam(at + new Vector3(MathF.Cos(a0) * 0.1f, 0.2f + MathF.Sin(a0) * 0.1f, 0f), at + new Vector3(MathF.Cos(a1) * 0.1f, 0.2f + MathF.Sin(a1) * 0.1f, 0f), 0.02f, Hall.Metal(220, 180, 90));
                            }
                            b.Box(at + new Vector3(0f, 0.31f, 0f), new Vector3(0.04f), glow);
                            break;
                        case 1:     // An amulet on a cushion.
                            b.Box(at + Vector3.UnitY * 0.04f, new Vector3(0.18f, 0.04f, 0.14f), Hall.Cloth(110, 40, 60));
                            b.Box(at + Vector3.UnitY * 0.1f, new Vector3(0.08f, 0.03f, 0.08f), glow);
                            break;
                        case 2:     // A dagger laid flat.
                            b.Box(at + new Vector3(0.05f, 0.03f, 0f), new Vector3(0.22f, 0.012f, 0.04f), Hall.Metal(200, 202, 210));
                            b.Box(at + new Vector3(-0.22f, 0.04f, 0f), new Vector3(0.08f, 0.02f, 0.03f), glow);
                            break;
                        default:    // A stone set upright in a claw.
                            b.Tube(at, at + Vector3.UnitY * 0.06f, 0.08f, 0.07f, 6, Hall.Metal(150, 120, 80));
                            b.Box(at + Vector3.UnitY * 0.16f, new Vector3(0.06f, 0.1f, 0.06f), glow);
                            break;
                    }
                }
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, (baseTop + caseTop) * 0.5f, half.Y + 0.3f), new Vector3(1f, 0.62f, 0.25f), 16f, 7f));
        }

        /// <summary>A great hourglass on a round plinth: two bulbs of glass between turned posts, the sand nearly all run through.</summary>
        private static void Hourglass(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            float r = MathF.Min(half.X, half.Y);
            b.Tube(Vector3.Zero, new Vector3(0f, 0.25f, 0f), r, r * 0.95f, 16, PlinthDark);
            b.Tube(new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.85f, 0f), r * 0.78f, r * 0.7f, 16, Plinth);
            b.Tube(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0.98f, 0f), r * 0.82f, r * 0.82f, 16, PlinthDark);
            VertexMaterial wood = Hall.Wood(88, 58, 38);
            float bottom = 0.98f, top = h - 0.15f;
            b.Tube(new Vector3(0f, bottom, 0f), new Vector3(0f, bottom + 0.22f, 0f), r * 0.68f, r * 0.68f, 14, wood);
            b.Tube(new Vector3(0f, top - 0.22f, 0f), new Vector3(0f, top, 0f), r * 0.68f, r * 0.68f, 14, wood);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.5f;
                Vector3 at = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * r * 0.56f;
                Hall.Turned(b, at + Vector3.UnitY * (bottom + 0.22f), wood, 6, (0f, 0.08f), (0.15f, 0.12f), (0.3f, 0.06f), ((top - bottom) * 0.5f - 0.22f, 0.07f), ((top - bottom) * 0.5f - 0.1f, 0.11f), ((top - bottom) * 0.5f, 0.07f), (top - bottom - 0.44f, 0.08f));
            }
            // The glass, two cones meeting at a narrow waist, and the sand in the lower one.
            float waist = (bottom + top) * 0.5f;
            VertexMaterial glass = Hall.Mirror(150, 170, 176);
            b.Tube(new Vector3(0f, bottom + 0.22f, 0f), new Vector3(0f, waist - 0.12f, 0f), r * 0.46f, 0.12f, 14, glass, capped: false);
            b.Tube(new Vector3(0f, waist - 0.12f, 0f), new Vector3(0f, waist + 0.12f, 0f), 0.12f, 0.12f, 10, glass, capped: false);
            b.Tube(new Vector3(0f, waist + 0.12f, 0f), new Vector3(0f, top - 0.22f, 0f), 0.12f, r * 0.46f, 14, glass, capped: false);
            VertexMaterial sand = Hall.Flat(206, 168, 104);
            b.Tube(new Vector3(0f, bottom + 0.22f, 0f), new Vector3(0f, bottom + 0.75f, 0f), r * 0.44f, r * 0.33f, 14, sand);
            b.Tube(new Vector3(0f, bottom + 0.75f, 0f), new Vector3(0f, bottom + 1.1f, 0f), r * 0.33f, 0.04f, 12, sand);
            b.Tube(new Vector3(0f, top - 0.6f, 0f), new Vector3(0f, top - 0.24f, 0f), 0.15f, r * 0.4f, 10, sand);
            b.Tube(new Vector3(0f, waist, 0f), new Vector3(0f, bottom + 1.1f, 0f), 0.02f, 0.02f, 4, sand);
            lights.Add(AssetLight.Glow(new Vector3(0f, waist, r + 0.6f), new Vector3(1f, 0.8f, 0.55f), 10f, 7f));
        }

        /// <summary>A stone for those who fell, rounded at its head and cut with their marks, a wreath against it and candles burning at its foot.</summary>
        private static void Memorial(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(half.X, 0.2f, half.Y - 0.3f), PlinthDark);
            b.Box(new Vector3(0f, 0.55f, -0.2f), new Vector3(half.X - 0.35f, 0.15f, half.Y - 0.75f), Plinth);
            VertexMaterial stone = Hall.Stone(184, 178, 166);
            float slab = h - 0.7f;
            b.Box(new Vector3(0f, 0.7f + slab * 0.42f, -0.35f), new Vector3(half.X * 0.55f, slab * 0.42f, 0.2f), stone);
            // Its rounded head, in steps of a half round.
            for (int i = 0; i < 6; i++)
            {
                float t0 = MathF.PI * i / 6f, t1 = MathF.PI * (i + 1) / 6f;
                float w = half.X * 0.55f;
                float y0 = 0.7f + slab * 0.84f;
                Vector3 p0 = new Vector3(MathF.Cos(t0) * w, y0 + MathF.Sin(t0) * w * 0.5f, -0.15f), p1 = new Vector3(MathF.Cos(t1) * w, y0 + MathF.Sin(t1) * w * 0.5f, -0.15f);
                Vector3 c = new Vector3(0f, y0, -0.15f);
                b.Triangle(c, p0, p1, stone);
                b.Triangle(c - Vector3.UnitZ * 0.4f, p1 - Vector3.UnitZ * 0.4f, p0 - Vector3.UnitZ * 0.4f, stone);
                b.Quad(p0 - Vector3.UnitZ * 0.4f, p1 - Vector3.UnitZ * 0.4f, p1, p0, stone);
            }
            // Lines cut in it.
            for (int line = 0; line < 6; line++)
            {
                b.Box(new Vector3(0f, 0.7f + slab * 0.72f - line * 0.24f, -0.14f), new Vector3(half.X * (0.4f - (line % 2) * 0.08f), 0.025f, 0.012f), Hall.Flat(70, 64, 56));
            }
            // A wreath leaning against it: leaves in a ring, a few flowers.
            VertexMaterial leaf = new VertexMaterial(MaterialCatalog.FoliageWhite.Index, new Colour(48, 86, 38));
            Vector3 wreath = new Vector3(0f, 1.25f, 0.0f);
            for (int i = 0; i < 18; i++)
            {
                float a = MathF.Tau * i / 18f;
                Vector3 p = wreath + new Vector3(MathF.Cos(a) * 0.5f, MathF.Sin(a) * 0.5f, 0.08f);
                b.Box(p, new Vector3(0.1f, 0.09f, 0.07f), leaf);
                if (i % 4 == 0) b.Box(p + Vector3.UnitZ * 0.07f, new Vector3(0.06f), Hall.Flat(232, 226, 210));
            }
            // Candles along its foot, some burnt down.
            for (int i = 0; i < 7; i++)
            {
                float x = -half.X + 0.4f + i * (half.X * 2f - 0.8f) / 6f;
                Hall.Candle(b, new Vector3(x, 0.4f, half.Y - 0.55f + random.Range(-0.08f, 0.08f)), random.Range(0.2f, 0.5f), 0.06f, lights, lit: i % 3 != 1);
            }
            lights.Add(AssetLight.VigilCandles(new Vector3(0f, 1.3f, half.Y - 0.3f)) with { Intensity = 26f, Radius = 11f });
        }

        /// <summary>A sloping table with the lands drawn on it, those set free picked out and flagged, and rolled maps beside it.</summary>
        private static void MapTable(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            VertexMaterial wood = Hall.Wood(98, 66, 42);
            foreach (float x in new[] { -half.X + 0.3f, half.X - 0.3f })
            {
                foreach (float z in new[] { -half.Y + 0.4f, half.Y - 0.6f }) Hall.Leg(b, new Vector3(x, 0f, z), z < 0f ? 2.35f : 1.85f, 0.14f, wood);
            }
            // The top, tilted towards whoever stands before it.
            Vector3 lo = new Vector3(0f, 1.85f, half.Y - 0.45f), hi = new Vector3(0f, 2.4f, -half.Y + 0.25f);
            Vector3 v = (hi - lo).Normalized();
            Vector3 n = Vector3.Cross(Vector3.UnitX, v);
            b.PlaneQuad(lo - Vector3.UnitX * half.X, lo + Vector3.UnitX * half.X, hi + Vector3.UnitX * half.X, hi - Vector3.UnitX * half.X, wood, lo, Vector3.UnitX, v, new Vector4(-half.X, half.X, 0f, (hi - lo).Length));
            Vector3 mapLo = lo + n * 0.02f + v * 0.12f, mapHi = hi + n * 0.02f - v * 0.12f;
            float mapHalf = half.X - 0.15f;
            b.PlaneQuad(mapLo - Vector3.UnitX * mapHalf, mapLo + Vector3.UnitX * mapHalf, mapHi + Vector3.UnitX * mapHalf, mapHi - Vector3.UnitX * mapHalf, Hall.Flat(206, 190, 150), mapLo, Vector3.UnitX, v, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f));
            // Lands on it, those freed in green with a flag in each.
            float length = (mapHi - mapLo).Length;
            for (int i = 0; i < 9; i++)
            {
                Vector3 at = mapLo + Vector3.UnitX * random.Range(-mapHalf + 0.3f, mapHalf - 0.3f) + v * random.Range(0.2f, length - 0.2f) + n * 0.01f;
                bool freed = i % 3 != 2;
                b.Box(at, new Vector3(random.Range(0.18f, 0.32f), 0.01f, random.Range(0.14f, 0.26f)), freed ? Hall.Flat(96, 140, 80) : Hall.Flat(150, 120, 90));
                if (freed)
                {
                    b.Beam(at, at + n * 0.45f, 0.012f, Hall.Wood(140, 100, 60));
                    b.Box(at + n * 0.38f + Vector3.UnitX * 0.08f, new Vector3(0.08f, 0.05f, 0.005f), Hall.Cloth(40, 110, 120));
                }
            }
            // Maps rolled up in a basket beside it.
            Vector3 basket = new Vector3(half.X - 0.4f, 0f, half.Y + 0.15f);
            b.Tube(basket, basket + Vector3.UnitY * 0.6f, 0.32f, 0.38f, 10, Hall.Wood(150, 116, 70), capped: false);
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.Tau * i / 5f;
                Vector3 foot = basket + new Vector3(MathF.Cos(a) * 0.16f, 0.1f, MathF.Sin(a) * 0.16f);
                b.Tube(foot, foot + new Vector3(MathF.Cos(a) * 0.1f, 1.2f, MathF.Sin(a) * 0.1f), 0.07f, 0.07f, 6, Hall.Paper);
            }
        }

        /// <summary>A chest bound in iron, thrown open on a plinth and heaped over with gold, more of it spilled at its foot, a few stones among the coin.</summary>
        private static void Hoard(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            b.Box(new Vector3(0f, 0.25f, 0f), new Vector3(half.X, 0.25f, half.Y - 0.3f), PlinthDark);
            VertexMaterial wood = Hall.Wood(104, 70, 46);
            VertexMaterial iron = Hall.Iron(86);
            float floor = 0.5f;
            b.Box(new Vector3(0f, floor + 0.45f, -0.1f), new Vector3(half.X - 0.4f, 0.45f, half.Y * 0.45f), wood);
            foreach (float x in new[] { -half.X + 0.6f, 0f, half.X - 0.6f }) b.Box(new Vector3(x, floor + 0.45f, -0.1f), new Vector3(0.06f, 0.46f, half.Y * 0.45f + 0.02f), iron);
            // The lid, thrown back.
            Hall.At(b, new Vector3(0f, floor + 0.9f, -0.1f - half.Y * 0.45f), 0f, () =>
            {
                b.Box(new Vector3(0f, 0.38f, -0.12f), new Vector3(half.X - 0.4f, 0.4f, 0.08f), wood);
                foreach (float x in new[] { -half.X + 0.6f, 0f, half.X - 0.6f }) b.Box(new Vector3(x, 0.38f, -0.12f), new Vector3(0.06f, 0.41f, 0.09f), iron);
            });
            // The gold, heaped over its rim.
            VertexMaterial gold = Hall.Metal(222, 176, 76);
            for (int i = 0; i < 70; i++)
            {
                float x = random.Range(-half.X + 0.5f, half.X - 0.5f), z = random.Range(-half.Y * 0.4f, half.Y * 0.3f) - 0.1f;
                float mound = 1f - MathF.Abs(x) / half.X * 0.7f;
                Vector3 at = new Vector3(x, floor + 0.85f + mound * 0.25f + random.Range(0f, 0.08f), z);
                Hall.At(b, at, random.Range(0f, 3f), () => b.Tube(Vector3.Zero, new Vector3(0.02f, 0.03f, 0f), 0.1f, 0.1f, 7, gold));
            }
            for (int i = 0; i < 18; i++)
            {
                Vector3 at = new Vector3(random.Range(-half.X + 0.2f, half.X - 0.2f), floor + 0.005f, random.Range(half.Y * 0.4f, half.Y - 0.4f));
                b.Tube(at, at + Vector3.UnitY * 0.03f, 0.1f, 0.1f, 7, gold);
            }
            Vector3[] gems = { new(0.3f, 0.6f, 1f), new(0.4f, 1f, 0.5f), new(1f, 0.4f, 0.8f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = gems[i % gems.Length];
                b.Box(new Vector3(random.Range(-0.8f, 0.8f), floor + 1.12f, random.Range(-0.3f, 0.1f)), new Vector3(0.06f), Hall.Gem(c.X, c.Y, c.Z, 1.2f));
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, floor + 1.6f, half.Y + 0.4f), new Vector3(1f, 0.78f, 0.4f), 10f, 6f));
        }

        /// <summary>The climb, cut in stone: steps going up, each with a pale blue stone set in it like a waygate's light, and a little flag on the topmost.</summary>
        private static void Climb(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights)
        {
            b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(half.X, 0.2f, half.Y - 0.3f), PlinthDark);
            const int Steps = 7;
            float rise = (h - 1.2f) / Steps;
            for (int i = 0; i < Steps; i++)
            {
                float depth = (half.Y * 1.6f) * (1f - i / (float)Steps);
                float w = half.X * (1f - i * 0.08f);
                float y = 0.4f + i * rise;
                b.Box(new Vector3(0f, y + rise * 0.5f, -half.Y * 0.8f + depth * 0.5f), new Vector3(w, rise * 0.5f, depth * 0.5f), i % 2 == 0 ? Plinth : Hall.Stone(186, 176, 158));
                b.Box(new Vector3(0f, y + rise * 0.6f, -half.Y * 0.8f + depth + 0.01f), new Vector3(0.06f, 0.05f, 0.02f), Hall.Gem(0.4f, 0.8f, 1f, 1.2f));
            }
            float topY = 0.4f + Steps * rise;
            b.Beam(new Vector3(0f, topY, -half.Y * 0.6f), new Vector3(0f, topY + 0.9f, -half.Y * 0.6f), 0.02f, Hall.Wood(140, 100, 60));
            b.Box(new Vector3(0.18f, topY + 0.75f, -half.Y * 0.6f), new Vector3(0.18f, 0.12f, 0.006f), Hall.Cloth(40, 110, 120));
            lights.Add(AssetLight.Glow(new Vector3(0f, topY * 0.6f, half.Y + 0.5f), new Vector3(0.6f, 0.82f, 1f), 9f, 6f));
        }
    }
}
