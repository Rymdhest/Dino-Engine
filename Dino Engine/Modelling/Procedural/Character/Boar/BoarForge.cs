using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Modelling.Procedural.Saurian;
using Dino_Engine.Rendering.Shells;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Character
{
    /// <summary>
    /// Grows wild boars, as the dinosaurs are grown: a body drawn from a seed (<see cref="BoarBody"/>), its flesh a field (<see cref="BoarSculpt"/>), a skin grown from that by surface nets and
    /// quadric decimation (<see cref="SaurianSkin"/>), with what's too small or too hard for a field put on after: the eyes, the ears, the tusks and the tail. It stands on the same bones as people
    /// and every other animal, so everything that moves a character moves it, and its coat is bristles (<see cref="ShellStyles.Bristles"/>). Nothing is stored and nothing touches a GPU: it's safe
    /// on any thread, and a lab can grow one with no window.
    /// </summary>
    public static class BoarForge
    {
        private const int Budget = 5600;                // Triangles in the skin, which is plenty for something seen from where the camera is.
        private const float Smallest = 0.045f;          // Nothing smaller than this is made: it would be under a pixel.

        public static Rig RigOf(BoarSpec spec) => new BoarBody(spec).BuildRig();

        public static Mesh Make(BoarSpec spec) => Make(spec, out _);

        public static Mesh Make(BoarSpec spec, out BoarBody body)
        {
            body = new BoarBody(spec);
            var mesh = new SaurianMesh();
            SaurianField flesh = BoarSculpt.Flesh(body);
            var dress = new BoarDress(body);

            // Sampled finely enough that a pastern is several cells across, and decimated to the budget, with no edge so long that the skin can't bend where a leg does.
            SaurianSkin.Grow(mesh, flesh, body.Withers / 95f, Budget, body.Length / 34f, dress, settle: true);

            Eyes(mesh, body, flesh);
            Ears(mesh, body, flesh);
            Mane(mesh, body, flesh, dress);
            Tusks(mesh, body, flesh);
            Tail(mesh, body);

            Mesh built = mesh.Build();
            built.ShellStyle = ShellStyles.Bristles;
            return built;
        }

        // Whatever was added from here on has the coat on it too.
        private static void Furred(SaurianMesh mesh, int from, float amount)
        {
            for (int i = from; i < mesh.Shells.Count; i++) mesh.Shells[i] = amount;
        }

        // Small and dark, high on the side of the head and well back, close under the ears, each in its socket under the brow.
        private static void Eyes(SaurianMesh mesh, BoarBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float size = l * 0.028f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.05f, l * 0.11f, l * 0.4f));
                Vector3 way = (Vector3.UnitX * side * 0.92f + body.HeadUp * 0.25f + body.HeadForward * 0.2f).Normalized();
                Vector3 skin = inside + way * flesh.Exit(inside, way, l * 0.2f);
                mesh.Ball(skin - way * size * 0.7f, new Vector3(size), 2, body.Hide.Eye, GearMaterials.Gem, BoneType.Head);
            }
        }

        // Upright, broad at the base and pointed, set high at the back of the head and leaning out and a little forward, the tip cupped forward, furred like the rest of it: a leaf of skin
        // and gristle, thin from the front and broad from the side.
        private static void Ears(SaurianMesh mesh, BoarBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float length = body.Ears;
            if (length < Smallest) return;
            Vector3 colour = Vector3.Lerp(body.Hide.Back, body.Hide.Face, 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                // It stands up from the world's up and not the head's, as the head hangs forward and an ear doesn't.
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.12f, l * 0.12f, l * 0.13f));
                Vector3 up = (Vector3.UnitY * MathF.Cos(body.EarSpread) + Vector3.UnitX * side * MathF.Sin(body.EarSpread)).Normalized();
                Vector3 way = (up * MathF.Cos(body.EarLean) + Vector3.UnitZ * MathF.Sin(body.EarLean)).Normalized();
                Vector3 root = inside + way * (flesh.Exit(inside, way, l * 0.2f) - length * 0.14f);

                // It faces forward and out, and is as wide at its base as three quarters of its length.
                Vector3 facing = (Vector3.UnitZ + Vector3.UnitX * side * 0.6f).Normalized();
                facing = (facing - way * Vector3.Dot(facing, way)).Normalized();
                Vector3 across = Vector3.Cross(facing, way).Normalized();
                float half = length * 0.32f;
                Vector3 cup = facing * (length * 0.12f);
                var outline = new[]
                {
                    root - across * half,
                    root - across * half * 1.05f + way * length * 0.28f,
                    root - across * half * 0.5f + way * length * 0.68f + cup * 0.6f,
                    root + way * length + cup,
                    root + across * half * 0.5f + way * length * 0.68f + cup * 0.6f,
                    root + across * half * 1.05f + way * length * 0.28f,
                    root + across * half,
                    root - way * length * 0.12f,
                };
                int first = mesh.Positions.Count;
                mesh.Sheet(outline, root + way * length * 0.36f + cup * 0.3f, facing, length * 0.045f, colour, colour * 0.9f, GearMaterials.Hide, _ => BoneType.Head);
                Furred(mesh, first, 1f);
            }
        }

        // The mane: a ragged crest of long dark bristles standing up and back along the top of the neck and the back, from behind the ears to the middle of the back, longest over the withers,
        // in two rows either side of the spine. It's what a wild boar is known by from above, and what it raises when it's angry. Each tuft stands on the skin where it really is.
        private static void Mane(SaurianMesh mesh, BoarBody body, SaurianField flesh, BoarDress dress)
        {
            float longest = body.W(0.085f + 0.06f * body.Age);
            float from = body.Atlas.Z - body.W(0.01f);
            float to = body.HindZ + (body.ForeZ - body.HindZ) * 0.35f;
            float spacing = body.W(0.024f);
            var random = new Dino_Engine.World.WorldRandom(body.Seed ^ 0x3A9E_11D5UL);
            Vector3 dark = body.Hide.Back * 0.7f;
            int row = 0;
            for (float z = from; z > to; z -= spacing, row++)
            {
                float t = (from - z) / (from - to);
                float length = longest * (0.45f + 0.55f * MathF.Sin(MathF.Min(t * 2f, 1f) * MathF.PI * 0.5f)) * (1f - 0.7f * MathF.Pow(MathF.Max(t - 0.55f, 0f) / 0.45f, 1.5f)) * random.Range(0.75f, 1.15f);
                if (length < Smallest) continue;
                float x = (row % 2 == 0 ? 1f : -1f) * body.W(0.012f);
                float inside = z > body.Trunk[^1].Z ? body.Atlas.Y : body.Station(z).Middle;
                Vector3 from3 = new Vector3(x, inside, z);
                Vector3 root = from3 + Vector3.UnitY * (flesh.Exit(from3, Vector3.UnitY, body.W(0.2f)) - length * 0.12f);
                Vector3 lean = new Vector3(x * 8f + random.Range(-0.12f, 0.12f), 0.62f, -0.78f + random.Range(-0.15f, 0.15f)).Normalized();
                mesh.Spike(root, lean, length, length * 0.17f, -Vector3.UnitZ, 0.3f, 4, dark * random.Range(0.85f, 1.1f), GearMaterials.Hide, dress.BodyHang(root), 0.45f, Vector3.UnitX);
            }
        }

        // Two pairs, ivory. The lower come out of the side of the mouth a third of the way back from the disc and curve up, out and back; the upper, shorter, come out over them and curve up
        // and out, as the two wear against each other. An old tusker's are far longer than a young sow's.
        private static void Tusks(SaurianMesh mesh, BoarBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            Vector3 ivory = body.Hide.Tusk;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 outward = Vector3.UnitX * side;
                if (body.Tusks >= Smallest)
                {
                    Vector3 inside = body.FromHead(new Vector3(side * l * 0.04f, -l * 0.1f, l * 0.67f));
                    Vector3 root = inside + outward * (flesh.Exit(inside, outward, l * 0.1f) - body.Tusks * 0.12f);
                    Vector3 way = (body.HeadUp * 0.78f + outward * 0.5f - body.HeadForward * 0.1f).Normalized();
                    mesh.Spike(root, way, body.Tusks, body.Tusks * 0.15f, -body.HeadForward + outward * 0.3f, 0.38f, 6, ivory, GearMaterials.Bone, BoneType.Head);
                }
                float upper = body.Tusks * 0.5f;
                if (upper >= Smallest)
                {
                    Vector3 inside = body.FromHead(new Vector3(side * l * 0.05f, -l * 0.03f, l * 0.74f));
                    Vector3 root = inside + outward * (flesh.Exit(inside, outward, l * 0.1f) - upper * 0.15f);
                    Vector3 way = (body.HeadUp * 0.55f + outward * 0.75f + body.HeadForward * 0.1f).Normalized();
                    mesh.Spike(root, way, upper, upper * 0.2f, body.HeadUp - body.HeadForward * 0.4f, 0.42f, 6, ivory * 0.94f, GearMaterials.Bone, BoneType.Head);
                }
            }
        }

        // Thin and straight, hanging from the top of the rump's end, with a flat tuft of long bristles at the end of it. The top half goes with the tail's first bone and the rest with its last.
        private static void Tail(SaurianMesh mesh, BoarBody body)
        {
            float thick = body.W(0.022f);
            Vector3 root = body.TailRoot + new Vector3(0f, 0f, body.W(0.03f));
            Vector3 middle = body.TailMiddle;
            Vector3 end = body.TailEnd;
            Vector3 dark = Vector3.Lerp(body.Hide.Back, body.Hide.Legs, 0.5f);

            int first = mesh.Positions.Count;
            mesh.Tube(new[] { root, Vector3.Lerp(root, middle, 0.5f), middle + (middle - root) * 0.06f }, new[] { thick * 1.3f, thick, thick * 0.85f }, 6, dark, GearMaterials.Hide, BoneType.TailBase);
            Vector3 tuftFrom = Vector3.Lerp(middle, end, 0.55f);
            Vector3 down = (end - middle).Normalized();
            mesh.Tube(new[] { middle - down * thick, tuftFrom, Vector3.Lerp(tuftFrom, end, 0.5f), end + down * thick }, new[] { thick * 0.85f, thick * 0.75f, thick * 1.5f, thick * 0.4f }, 6,
                dark * 0.85f, GearMaterials.Hide, BoneType.TailTip, Vector3.UnitX, 0.55f);
            Furred(mesh, first, 1f);
        }
    }
}
