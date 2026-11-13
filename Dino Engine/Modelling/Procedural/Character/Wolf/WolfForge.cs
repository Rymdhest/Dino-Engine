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
    /// Grows wolves and dire wolves, as the boars and the dinosaurs are grown: a body drawn from a seed (<see cref="WolfBody"/>), its flesh a field (<see cref="WolfSculpt"/>), a skin grown from that
    /// by surface nets and quadric decimation (<see cref="SaurianSkin"/>), and what's too small or too thin for a field put on after: the eyes, the ears, the tail, and a dire wolf's hackles. It stands on
    /// the same bones as people and every other animal, and its coat is fur (<see cref="ShellStyles.WolfFur"/>, a dire wolf's longer). Nothing is stored and nothing touches a GPU: it's safe on any
    /// thread, and a lab can grow one with no window.
    /// </summary>
    public static class WolfForge
    {
        private const int Budget = 6000;                // Triangles in the skin.
        private const float Smallest = 0.045f;          // Nothing smaller than this is made: it would be under a pixel.

        public static Rig RigOf(WolfSpec spec) => new WolfBody(spec).BuildRig();

        public static Mesh Make(WolfSpec spec) => Make(spec, out _);

        public static Mesh Make(WolfSpec spec, out WolfBody body)
        {
            body = new WolfBody(spec);
            var mesh = new SaurianMesh();
            SaurianField flesh = WolfSculpt.Flesh(body);
            var dress = new WolfDress(body);

            // Sampled finely enough that a pastern is several cells across, and decimated to the budget, with no edge so long that the skin can't bend where a leg does.
            SaurianSkin.Grow(mesh, flesh, body.Withers / 100f, Budget, body.Length / 34f, dress, settle: true);

            Eyes(mesh, body, flesh);
            Ears(mesh, body, flesh);
            Tail(mesh, body);
            if (body.Dire) Hackles(mesh, body, flesh, dress);

            Mesh built = mesh.Build();
            built.ShellStyle = body.Dire ? ShellStyles.DireFur : ShellStyles.WolfFur;
            return built;
        }

        private static void Furred(SaurianMesh mesh, int from, float amount)
        {
            for (int i = from; i < mesh.Shells.Count; i++) mesh.Shells[i] = amount;
        }

        // Amber, set under the brow and turned further forward than a boar's, as a hunter's are.
        private static void Eyes(SaurianMesh mesh, WolfBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float size = l * 0.036f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.05f, l * 0.1f, l * 0.43f));
                Vector3 way = (Vector3.UnitX * side * 0.78f + body.HeadUp * 0.22f + body.HeadForward * 0.58f).Normalized();
                Vector3 skin = inside + way * flesh.Exit(inside, way, l * 0.2f);
                mesh.Ball(skin - way * size * 0.6f, new Vector3(size), 2, body.Hide.Eye, GearMaterials.Gem, BoneType.Head);
            }
        }

        // Big, upright and pointed, set high and wide on the skull, leaning a little out and forward: a leaf of skin and gristle, thin from the front and broad from the side, furred like the rest of it.
        private static void Ears(SaurianMesh mesh, WolfBody body, SaurianField flesh)
        {
            float l = body.HeadLength;
            float length = body.Ears;
            if (length < Smallest) return;
            for (int side = -1; side <= 1; side += 2)
            {
                // It stands up from the world's up and not the head's, as the head hangs forward and an ear doesn't.
                Vector3 inside = body.FromHead(new Vector3(side * l * 0.12f, l * 0.12f, l * 0.16f));
                Vector3 up = (Vector3.UnitY * MathF.Cos(body.EarSpread) + Vector3.UnitX * side * MathF.Sin(body.EarSpread)).Normalized();
                Vector3 way = (up * MathF.Cos(body.EarLean) + Vector3.UnitZ * MathF.Sin(body.EarLean)).Normalized();
                Vector3 root = inside + way * (flesh.Exit(inside, way, l * 0.2f) - length * 0.14f);

                Vector3 facing = (Vector3.UnitZ + Vector3.UnitX * side * 0.45f).Normalized();
                facing = (facing - way * Vector3.Dot(facing, way)).Normalized();
                Vector3 across = Vector3.Cross(facing, way).Normalized();
                float half = length * 0.34f;
                Vector3 cup = facing * (length * 0.1f);
                var outline = new[]
                {
                    root - across * half,
                    root - across * half * 1.02f + way * length * 0.3f,
                    root - across * half * 0.55f + way * length * 0.7f + cup * 0.6f,
                    root + way * length + cup,
                    root + across * half * 0.55f + way * length * 0.7f + cup * 0.6f,
                    root + across * half * 1.02f + way * length * 0.3f,
                    root + across * half,
                    root - way * length * 0.12f,
                };
                int first = mesh.Positions.Count;
                mesh.Sheet(outline, root + way * length * 0.36f + cup * 0.3f, facing, length * 0.045f, body.Hide.Ears, body.Hide.Ears * 0.9f, GearMaterials.Leather, _ => BoneType.Head);
                Furred(mesh, first, 1f);
            }
        }

        // Thick with hair from the root most of the way down, hanging to the hocks, and black at the tip. The top half goes with the tail's first bone and the rest with its last.
        private static void Tail(SaurianMesh mesh, WolfBody body)
        {
            float bush = body.TailBush;
            Vector3 root = body.TailRoot + new Vector3(0f, 0f, body.W(0.03f));
            Vector3 middle = body.TailMiddle;
            Vector3 end = body.TailEnd;
            Vector3 down = (end - middle).Normalized();
            Vector3 upper = Vector3.Lerp(body.Hide.Back, body.Hide.Flank, 0.4f);
            Vector3 lower = Vector3.Lerp(body.Hide.Back, body.Hide.Flank, 0.2f);
            Vector3 tipFrom = Vector3.Lerp(middle, end, 0.72f);

            int first = mesh.Positions.Count;
            mesh.Tube(new[] { root, Vector3.Lerp(root, middle, 0.45f), middle + (middle - root).Normalized() * bush * 0.3f }, new[] { body.W(0.034f), bush * 0.78f, bush * 0.96f }, 8, upper, GearMaterials.Leather, BoneType.TailBase);
            mesh.Tube(new[] { middle - down * bush * 0.3f, Vector3.Lerp(middle, end, 0.35f), tipFrom }, new[] { bush * 0.96f, bush, bush * 0.82f }, 8, lower, GearMaterials.Leather, BoneType.TailTip);
            mesh.Tube(new[] { tipFrom, Vector3.Lerp(tipFrom, end, 0.55f), end }, new[] { bush * 0.82f, bush * 0.55f, body.W(0.01f) }, 8, body.Hide.TailTip, GearMaterials.Leather, BoneType.TailTip);
            Furred(mesh, first, 1f);
        }

        // A dire wolf's hackles: the long hair along its nape and over its shoulders standing up and back in two rows either side of the spine, longest over the withers, which is what makes it
        // look bigger than it is, and is how a dire wolf is told from the pack it leads.
        private static void Hackles(SaurianMesh mesh, WolfBody body, SaurianField flesh, WolfDress dress)
        {
            if (body.Hackles < Smallest) return;
            float from = body.Atlas.Z - body.W(0.02f);
            float to = body.ForeZ - body.W(0.3f);
            float spacing = body.W(0.022f);
            var random = new Dino_Engine.World.WorldRandom(body.Seed ^ 0x51A7_4C3BUL);
            Vector3 dark = body.Hide.Back * 0.85f;
            int row = 0;
            for (float z = from; z > to; z -= spacing, row++)
            {
                float t = (from - z) / (from - to);
                float length = body.Hackles * (0.55f + 0.45f * MathF.Sin(MathF.Min(t * 1.6f, 1f) * MathF.PI * 0.5f)) * (1f - 0.6f * MathF.Max(t - 0.6f, 0f) / 0.4f) * random.Range(0.8f, 1.15f);
                if (length < Smallest) continue;
                float x = (row % 2 == 0 ? 1f : -1f) * body.W(0.016f);
                float inside = z > body.Trunk[^1].Z ? body.Atlas.Y - body.W(0.02f) : body.Station(z).Middle;
                Vector3 start = new Vector3(x, inside, z);
                Vector3 root = start + Vector3.UnitY * (flesh.Exit(start, Vector3.UnitY, body.W(0.2f)) - length * 0.15f);
                Vector3 lean = new Vector3(x * 9f + random.Range(-0.1f, 0.1f), 0.55f, -0.83f + random.Range(-0.12f, 0.12f)).Normalized();
                int first = mesh.Positions.Count;
                mesh.Spike(root, lean, length, length * 0.2f, -Vector3.UnitZ, 0.3f, 4, dark * random.Range(0.85f, 1.1f), GearMaterials.Leather, dress.BodyHang(root), 0.45f, Vector3.UnitX);
                Furred(mesh, first, 0.6f);
            }
        }
    }
}
