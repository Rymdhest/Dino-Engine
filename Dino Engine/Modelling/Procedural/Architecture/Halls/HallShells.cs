using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The outside of each hall, and everything that's part of the building: its walls and openings, floor, roof, chimneys, and whatever's built against it. This is always drawn, and casts
    /// the shadows that light the inside through its windows.
    /// </summary>
    internal static class HallShells
    {
        public static void Build(MeshBuilder b, HallDesign d, List<Vector3> chimneys, List<AssetLight> lights)
        {
            HallKit kit = HallKit.For(d.Kind);
            var random = new WorldRandom(0x5EA1).Fork(d.Name);
            HallWalls.Build(b, d, kit);
            Floor(b, d, kit);
            switch (d.Kind)
            {
                case HallKind.Forge: Forge(b, d, kit, chimneys, lights, random); break;
                case HallKind.GamingHouse: GamingHouse(b, d, kit, chimneys, lights, random); break;
                default: Archive(b, d, kit, chimneys, lights, random); break;
            }
        }

        // The floor, inside the walls and out through the doorways, a hair above the levelled ground under it.
        private static void Floor(MeshBuilder b, HallDesign d, HallKit kit)
        {
            Vector2 inner = d.Inner;
            const float Y = Hall.FloorTop;
            Vector3 origin = new Vector3(0f, Y, 0f);
            Vector4 bounds = new Vector4(-inner.X, inner.X, -inner.Y, inner.Y);
            // u along x and v towards the back, so boards run away from the door.
            b.Panel(origin, Vector3.UnitX, -Vector3.UnitZ, -inner.X, inner.X, -inner.Y, inner.Y, kit.Floor, bounds);
        }

        // ---- The Forge.

        private static void Forge(MeshBuilder b, HallDesign d, HallKit kit, List<Vector3> chimneys, List<AssetLight> lights, WorldRandom random)
        {
            var dress = new Dress { StoneSurrounds = true, Shutters = true, Lattice = false };
            HallDressing.Openings(b, d, kit, dress, lights, random);
            Quoins(b, d, kit.Dressing);

            var gable = new HallRoofs.Gable(d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, kit.Overhang, 1f);
            HallRoofs.GableRoof(b, gable, kit, kit.OuterWall, kit.InnerWall, d.Wall, 11);
            RidgeVent(b, gable, kit, 7.5f);
            // Two dormers on the slope towards the street, lighting the loft where the stock is kept.
            foreach (float along in new[] { -8.5f, 8.5f }) HallRoofs.Dormer(b, gable, kit, kit.OuterWall, along, 6.4f, 1.5f, 2.8f, lights, (int)(along * 5f) + 70);

            // The forge: a hearth of coals under a stone hood, its flue up through the roof as the tallest chimney in the Haven.
            HallPiece hearth = d.Pieces.First(p => p.Kind == PieceKind.Hearth);
            ForgeHearth(b, d, kit, hearth, gable, chimneys, lights);

            LeanTo(b, d, kit, random);

            // The sign: an anvil, on the corner of the front where the road comes.
            HallDressing.Sign(b, HallWalls.OnWall(d, HallSide.Front, 6.2f, 6.9f), Vector3.UnitZ, Hall.Wood(122, 92, 62), Hall.Iron(70), (middle, out_, across) =>
            {
                VertexMaterial metal = Hall.Iron(64);
                Vector3 face = middle + across * 0.075f;
                Vector3 face2 = middle - across * 0.075f;
                foreach (Vector3 f in new[] { face, face2 })
                {
                    b.Box(f + Vector3.UnitY * 0.12f, new Vector3(MathF.Abs(out_.X) * 0.62f + MathF.Abs(across.X) * 0.02f, 0.11f, MathF.Abs(out_.Z) * 0.62f + MathF.Abs(across.Z) * 0.02f), metal);
                    b.Box(f - Vector3.UnitY * 0.12f, new Vector3(MathF.Abs(out_.X) * 0.2f + MathF.Abs(across.X) * 0.02f, 0.14f, MathF.Abs(out_.Z) * 0.2f + MathF.Abs(across.Z) * 0.02f), metal);
                    b.Box(f - Vector3.UnitY * 0.34f, new Vector3(MathF.Abs(out_.X) * 0.42f + MathF.Abs(across.X) * 0.02f, 0.08f, MathF.Abs(out_.Z) * 0.42f + MathF.Abs(across.Z) * 0.02f), metal);
                    b.Beam(f + Vector3.UnitY * 0.12f + out_ * 0.6f, f + Vector3.UnitY * 0.06f + out_ * 0.88f, 0.05f, metal);
                }
            });
        }

        private static void ForgeHearth(MeshBuilder b, HallDesign d, HallKit kit, HallPiece hearth, HallRoofs.Gable gable, List<Vector3> chimneys, List<AssetLight> lights)
        {
            VertexMaterial stone = kit.Dressing;
            VertexMaterial rough = Hall.Stone(120, 112, 102);
            Vector2 at = hearth.At, half = hearth.Half;
            float backFace = -d.Half.Y + d.Wall;
            float bed = 2f;
            // The hearth: a block of stone with a bed of coals sunk in its top and a stone rim.
            b.Box(new Vector3(at.X, bed * 0.5f, at.Y), new Vector3(half.X, bed * 0.5f, half.Y), rough);
            b.Box(new Vector3(at.X, bed + 0.12f, at.Y + half.Y - 0.18f), new Vector3(half.X, 0.12f, 0.18f), stone);
            b.Box(new Vector3(at.X - half.X + 0.18f, bed + 0.12f, at.Y), new Vector3(0.18f, 0.12f, half.Y), stone);
            b.Box(new Vector3(at.X + half.X - 0.18f, bed + 0.12f, at.Y), new Vector3(0.18f, 0.12f, half.Y), stone);
            b.Box(new Vector3(at.X, bed + 0.04f, at.Y - 0.1f), new Vector3(half.X - 0.36f, 0.06f, half.Y - 0.42f), Hall.Glow(1f, 0.32f, 0.06f, 2.2f));
            // Coals heaped in the middle, glowing hottest in the heart.
            var rng = new WorldRandom(0xC0A1);
            for (int i = 0; i < 26; i++)
            {
                float x = at.X + rng.Range(-half.X * 0.6f, half.X * 0.6f), z = at.Y + rng.Range(-half.Y * 0.5f, half.Y * 0.35f);
                float heart = 1f - Math.Clamp(MathF.Abs(x - at.X) / (half.X * 0.6f), 0f, 1f);
                float size = rng.Range(0.12f, 0.24f);
                VertexMaterial coal = heart > 0.55f && rng.Chance(0.7f) ? Hall.Glow(1f, 0.46f, 0.12f, 3.4f) : rng.Chance(0.5f) ? Hall.Glow(0.8f, 0.2f, 0.04f, 1.4f) : Hall.Flat(30, 26, 24);
                b.Box(new Vector3(x, bed + 0.1f + size * 0.5f, z), new Vector3(size, size * 0.6f, size * 0.9f), coal);
            }
            Hall.Flame(lights, new Vector3(at.X - 0.5f, bed + 0.2f, at.Y - 0.2f), 0.9f);
            Hall.Flame(lights, new Vector3(at.X + 0.6f, bed + 0.2f, at.Y + 0.1f), 0.7f);
            lights.Add(new AssetLight
            {
                Position = new Vector3(at.X, bed + 1.2f, at.Y + 0.4f), Colour = new Vector3(1f, 0.48f, 0.16f), Intensity = 210f, Radius = 22f, Fill = 0.25f,
                Flicker = 0.3f, FlickerSpeed = 7f, Shadow = true, LightsFog = true,
            });

            // The hood: from a lintel over the hearth, narrowing up to the flue.
            float mouth = 4.6f, neck = 8.6f;
            Vector2 flueHalf = new Vector2(1.3f, 1.05f);
            float frontZ = at.Y + half.Y + 0.25f;
            b.Box(new Vector3(at.X, mouth - 0.3f, frontZ - 0.25f), new Vector3(half.X + 0.4f, 0.3f, 0.28f), stone);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(at.X + s * (half.X + 0.15f), (bed + mouth) * 0.5f, frontZ - 0.25f), new Vector3(0.25f, (mouth - bed) * 0.5f, 0.25f), stone);
            Vector3 l0 = new Vector3(at.X - half.X - 0.4f, mouth, frontZ), l1 = new Vector3(at.X + half.X + 0.4f, mouth, frontZ);
            Vector3 n0 = new Vector3(at.X - flueHalf.X, neck, backFace + flueHalf.Y * 2f), n1 = new Vector3(at.X + flueHalf.X, neck, backFace + flueHalf.Y * 2f);
            b.Quad(l0, l1, n1, n0, rough);
            Vector3 lb0 = new Vector3(at.X - half.X - 0.4f, mouth, backFace), lb1 = new Vector3(at.X + half.X + 0.4f, mouth, backFace);
            Vector3 nb0 = new Vector3(at.X - flueHalf.X, neck, backFace), nb1 = new Vector3(at.X + flueHalf.X, neck, backFace);
            b.Quad(l1, lb1, nb1, n1, rough);
            b.Quad(lb0, l0, n0, nb0, rough);
            // Underneath it, dark with soot.
            b.Quad(l1, l0, lb0, lb1, Hall.Flat(34, 30, 28));
            // The flue, up through the wall top and out of the roof behind the ridge.
            float top = gable.Ridge + gable.Lift + 3.2f;
            HallRoofs.Chimney(b, new Vector3(at.X, neck, backFace + flueHalf.Y), flueHalf + new Vector2(0.2f, 0.15f), top, kit.OuterWall, stone, chimneys, pots: 1);
        }

        // An open shed against the east wall: posts, a roof of shingle, and the forge's wood stacked dry under it, with a few things that live out there.
        private static void LeanTo(MeshBuilder b, HallDesign d, HallKit kit, WorldRandom random)
        {
            HallPiece stack = d.Outside.First(p => p.Kind == PieceKind.LeanTo);
            float wall = -d.Half.X;
            float postX = -18.6f;
            float z0 = -8.6f, z1 = 5.6f;
            float high = 6.4f, low = 4.5f;
            VertexMaterial timber = Hall.Wood(100, 72, 50);
            foreach (HallPiece post in d.Outside.Where(p => p.Kind == PieceKind.Post))
            {
                Vector3 foot = new Vector3(post.At.X, -0.2f, post.At.Y);
                b.Box(foot + Vector3.UnitY * 0.3f, new Vector3(0.38f, 0.3f, 0.38f), kit.Plinth);
                b.Box(foot + Vector3.UnitY * (low * 0.5f + 0.3f), new Vector3(0.2f, low * 0.5f, 0.2f), timber);
                b.Beam(foot + new Vector3(0f, low - 0.9f, 0f), foot + new Vector3(0.9f, low + 0.1f, 0f), 0.08f, timber);
            }
            b.Beam(new Vector3(postX, low + 0.15f, z0), new Vector3(postX, low + 0.15f, z1), 0.16f, timber);
            b.Beam(new Vector3(wall - 0.1f, high + 0.1f, z0), new Vector3(wall - 0.1f, high + 0.1f, z1), 0.14f, timber);
            for (float z = z0 + 0.4f; z < z1; z += 1.8f) b.Beam(new Vector3(wall, high + 0.2f, z), new Vector3(postX - 0.7f, low + 0.05f, z), 0.08f, timber);
            // Its roof, sloping out and down, with its cut edges.
            Vector3 r0 = new Vector3(wall, high + 0.4f, z0 - 0.4f), r1 = new Vector3(wall, high + 0.4f, z1 + 0.4f);
            Vector3 r2 = new Vector3(postX - 0.8f, low + 0.25f, z1 + 0.4f), r3 = new Vector3(postX - 0.8f, low + 0.25f, z0 - 0.4f);
            Vector3 v = (r3 - r0).Normalized();
            var tint = HallRoofs.Weathering(Hall.Shingle(200, 176, 150), kit.Moss, low, high, 5);
            b.PlaneQuad(r0, r3, r2, r1, Hall.Shingle(200, 176, 150), r0, Vector3.UnitZ, -v, new Vector4(0f, (r1 - r0).Length, -(r3 - r0).Length, 0f), tint);
            b.Quad(r2, r3, r3 - Vector3.UnitY * 0.2f, r2 - Vector3.UnitY * 0.2f, Hall.Shingle(200, 176, 150));

            // The wood, split and stacked end out along the wall, as high as the stack says.
            VertexMaterial ends = Hall.Wood(176, 140, 100);
            VertexMaterial bark = new VertexMaterial(Textures.MaterialCatalog.BarkFurrowed.Index, new Util.Colour(120, 104, 90));
            float stackFrom = stack.At.Y - stack.Half.Y, stackTo = stack.At.Y + stack.Half.Y;
            for (float y = 0.25f; y < stack.Height - 0.1f; y += 0.34f)
            {
                float offset = ((int)(y / 0.34f) % 2) * 0.17f;
                for (float z = stackFrom + 0.18f + offset; z < stackTo - 0.15f; z += 0.36f)
                {
                    float r = random.Range(0.13f, 0.17f);
                    Vector3 from = new Vector3(wall - 0.05f, y, z + random.Range(-0.03f, 0.03f));
                    Vector3 to = new Vector3(wall - stack.Half.X * 2f + random.Range(-0.1f, 0.1f), y + random.Range(-0.02f, 0.02f), from.Z);
                    b.Tube(from, to, r, r, 5, bark, capped: false);
                    b.Tube(to + Vector3.UnitX * 0.01f, to - Vector3.UnitX * 0.02f, r * 0.96f, r * 0.96f, 5, ends);
                }
            }
            // A cart wheel leaning on the post, and a water butt.
            Vector3 hub = new Vector3(postX + 0.5f, 1.25f, 2.6f);
            for (int i = 0; i < 10; i++)
            {
                float t0 = MathF.Tau * i / 10f, t1 = MathF.Tau * (i + 1) / 10f;
                Vector3 p0 = hub + new Vector3(0.18f * MathF.Cos(t0), 1.2f * MathF.Sin(t0), 1.2f * MathF.Cos(t0));
                Vector3 p1 = hub + new Vector3(0.18f * MathF.Cos(t1), 1.2f * MathF.Sin(t1), 1.2f * MathF.Cos(t1));
                b.Beam(p0, p1, 0.09f, timber);
                b.Beam(hub, p0, 0.045f, timber);
            }
            b.Tube(hub - Vector3.UnitX * 0.2f, hub + Vector3.UnitX * 0.2f, 0.22f, 0.22f, 8, timber);
            Vector3 butt = new Vector3(postX + 1.6f, 0f, -5.4f);
            b.Tube(butt, butt + Vector3.UnitY * 2.1f, 0.85f, 0.8f, 12, Hall.Wood(118, 84, 56), capped: false);
            foreach (float y in new[] { 0.3f, 1.05f, 1.8f }) b.Tube(butt + Vector3.UnitY * (y - 0.06f), butt + Vector3.UnitY * (y + 0.06f), 0.86f, 0.86f, 12, Hall.Iron(80));
            b.Tube(butt + Vector3.UnitY * 1.92f, butt + Vector3.UnitY * 1.96f, 0.78f, 0.78f, 12, Hall.Mirror(60, 70, 72));
        }

        // A little roofed box on the ridge with louvres, for the heat of the forge to get out.
        private static void RidgeVent(MeshBuilder b, HallRoofs.Gable g, HallKit kit, float along)
        {
            float y = g.Ridge + g.Lift - 0.2f;
            b.Box(new Vector3(along, y + 0.7f, 0f), new Vector3(1.1f, 0.7f, 0.8f), kit.Joinery);
            for (int i = 0; i < 4; i++) b.Box(new Vector3(along, y + 0.3f + i * 0.3f, 0.82f), new Vector3(1f, 0.05f, 0.08f), kit.Soffit);
            b.Box(new Vector3(along, y + 1.5f, 0f), new Vector3(1.45f, 0.1f, 1.25f), kit.Roof);
            b.Tube(new Vector3(along - 1.5f, y + 1.62f, 0f), new Vector3(along + 1.5f, y + 1.62f, 0f), 0.14f, 0.14f, 6, kit.Ridge);
        }

        // Dressed stones at the corners, long and short in turn up the height of the wall.
        private static void Quoins(MeshBuilder b, HallDesign d, VertexMaterial stone)
        {
            const float Proud = 0.05f;
            float hx = d.Half.X, hz = d.Half.Y;
            int course = 0;
            for (float y = 1.4f; y < d.WallTop - 0.3f; y += 0.8f, course++)
            {
                float h = 0.36f;
                bool longX = course % 2 == 0;
                float lx = longX ? 1.1f : 0.6f, lz = longX ? 0.6f : 1.1f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Vector3 centre = new Vector3(sx * (hx + Proud - lx * 0.5f), y + h, sz * (hz + Proud - lz * 0.5f));
                        b.Box(centre, new Vector3(lx * 0.5f, h, lz * 0.5f), stone);
                    }
                }
            }
        }

        // ---- The Gaming House.

        private static void GamingHouse(MeshBuilder b, HallDesign d, HallKit kit, List<Vector3> chimneys, List<AssetLight> lights, WorldRandom random)
        {
            var dress = new Dress { StoneSurrounds = false, Shutters = true, FlowerBoxes = true, Lattice = true };
            Framing(b, d, kit, 4.9f);
            HallDressing.Openings(b, d, kit, dress, lights, random);

            var gable = new HallRoofs.Gable(d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, kit.Overhang, 1.3f);
            HallRoofs.GableRoof(b, gable, kit, kit.OuterWall, kit.InnerWall, d.Wall, 23);
            GableTimbers(b, d, gable, kit);
            foreach (float along in new[] { -8.2f, 0f, 8.2f })
            {
                HallRoofs.Dormer(b, gable, kit, kit.OuterWall, along, 6.8f, 1.7f, 3.2f, lights, (int)(along * 7f) + 40);
            }

            // The fire's chimney, built out from the east wall from the ground up past the eaves.
            HallPiece breast = d.Outside.First(p => p.Kind == PieceKind.ChimneyBreast);
            float top = gable.Ridge + gable.Lift + 1.6f;
            Vector3 foot = new Vector3(breast.At.X, -0.4f, breast.At.Y);
            b.Box(foot + Vector3.UnitY * 3.6f, new Vector3(breast.Half.X + 0.15f, 4f, breast.Half.Y + 0.2f), kit.Plinth);
            b.Box(foot + new Vector3(0.05f, 7.9f, 0f), new Vector3(breast.Half.X + 0.06f, 0.3f, breast.Half.Y + 0.05f), kit.Dressing);
            HallRoofs.Chimney(b, foot + new Vector3(0.1f, 7.6f, 0f), new Vector2(breast.Half.X - 0.05f, breast.Half.Y - 0.5f), top, Hall.Stone(166, 156, 140), kit.Dressing, chimneys, pots: 2);
            // A smaller one at the back, for the bar's kitchen.
            HallRoofs.Chimney(b, new Vector3(6.4f, d.WallTop, -6.6f), new Vector2(0.7f, 0.6f), gable.FaceAt(-6.6f) + 2.2f, Hall.Brick(150, 100, 76), kit.Dressing, chimneys, pots: 1);

            Porch(b, d, kit, lights);
            StringLanterns(b, d, gable, lights);
        }

        // Timber framing over the plaster, outside: a sill along the plinth, a rail where an upper floor would be, a plate along the top, posts at the corners and beside every opening,
        // braces in the lower storey and close studding in the upper.
        private static void Framing(MeshBuilder b, HallDesign d, HallKit kit, float storey)
        {
            const float Proud = 0.06f;
            const float W = 0.15f;      // Half the width of a timber.
            VertexMaterial timber = kit.Timber;
            foreach (HallSide which in Enum.GetValues<HallSide>())
            {
                var side = new HallWalls.Side(d, which);
                float from = -side.FaceHalf, to = side.FaceHalf;
                List<HallOpening> openings = d.Openings.Where(o => o.Side == which).ToList();
                Vector3 P(float along, float y) => side.Outer(along, y) + side.Out * Proud;

                foreach (float y in new[] { kit.PlinthTop + W, storey, d.WallTop - W })
                {
                    // Rails, broken where an opening crosses them.
                    float at = from;
                    foreach (HallOpening o in openings.Where(o => o.Sill < y && o.Head > y).OrderBy(o => o.Along))
                    {
                        if (o.Along - o.HalfWidth - 0.3f > at) HallDressing.Box(b, (P(at, y) + P(o.Along - o.HalfWidth - 0.3f, y)) * 0.5f, side.AlongDir, side.Out, (o.Along - o.HalfWidth - 0.3f - at) * 0.5f, W, Proud, timber);
                        at = o.Along + o.HalfWidth + 0.3f;
                    }
                    if (to > at) HallDressing.Box(b, (P(at, y) + P(to, y)) * 0.5f, side.AlongDir, side.Out, (to - at) * 0.5f, W, Proud, timber);
                }

                // Posts: at the corners, beside each opening, and between, about every four metres.
                var posts = new List<float> { from + W, to - W };
                foreach (HallOpening o in openings) { posts.Add(o.Along - o.HalfWidth - 0.45f); posts.Add(o.Along + o.HalfWidth + 0.45f); }
                posts = posts.Where(a => a >= from + W && a <= to - W).OrderBy(a => a).ToList();
                var filled = new List<float>(posts);
                for (int i = 0; i + 1 < posts.Count; i++)
                {
                    float gap = posts[i + 1] - posts[i];
                    float middle = (posts[i] + posts[i + 1]) * 0.5f;
                    if (openings.Any(o => MathF.Abs(o.Along - middle) < o.HalfWidth + 0.2f)) continue;
                    int extra = (int)(gap / 4.2f);
                    for (int k = 1; k <= extra; k++) filled.Add(posts[i] + gap * k / (extra + 1));
                }
                filled = filled.OrderBy(a => a).ToList();
                foreach (float a in filled)
                {
                    if (openings.Any(o => MathF.Abs(o.Along - a) < o.HalfWidth + 0.2f)) continue;
                    HallDressing.Box(b, P(a, (kit.PlinthTop + d.WallTop) * 0.5f), side.AlongDir, side.Out, W, (d.WallTop - kit.PlinthTop) * 0.5f, Proud, timber);
                }

                // In each bay below the storey rail, a brace; above it, close studding, wherever there's no opening.
                for (int i = 0; i + 1 < filled.Count; i++)
                {
                    float a0 = filled[i] + W, a1 = filled[i + 1] - W;
                    if (a1 - a0 < 0.6f) continue;
                    float middle = (a0 + a1) * 0.5f;
                    bool open = openings.Any(o => o.Along + o.HalfWidth > a0 + 0.05f && o.Along - o.HalfWidth < a1 - 0.05f);
                    if (open) continue;
                    bool rising = i % 2 == 0;
                    Vector3 lo = P(rising ? a0 : a1, kit.PlinthTop + 2 * W), hi = P(rising ? a1 : a0, storey - W);
                    b.Beam(lo, hi, W * 0.8f, timber);
                    int studs = Math.Max(1, (int)((a1 - a0) / 1.1f));
                    for (int k = 1; k <= studs; k++)
                    {
                        float a = a0 + (a1 - a0) * k / (studs + 1);
                        HallDressing.Box(b, P(a, (storey + d.WallTop) * 0.5f), side.AlongDir, side.Out, W * 0.75f, (d.WallTop - storey) * 0.5f - W, Proud * 0.9f, timber);
                    }
                }
            }
        }

        // The timbers of a gable end: a king post up to the ridge and struts out from it.
        private static void GableTimbers(MeshBuilder b, HallDesign d, HallRoofs.Gable g, HallKit kit)
        {
            foreach (float end in new[] { -1f, 1f })
            {
                float x = end * (d.Half.X + 0.06f);
                float bottom = d.WallTop;
                float peak = g.Ridge - 0.3f;
                b.Beam(new Vector3(x, bottom, 0f), new Vector3(x, peak, 0f), 0.16f, kit.Timber);
                foreach (float s in new[] { -1f, 1f })
                {
                    float z = s * d.Half.Y * 0.55f;
                    float atRoof = g.Base + (d.Half.Y - MathF.Abs(z)) * g.Pitch - 0.35f;
                    b.Beam(new Vector3(x, bottom + 0.4f, z), new Vector3(x, atRoof, z), 0.13f, kit.Timber);
                    b.Beam(new Vector3(x, bottom + 0.3f, 0f), new Vector3(x, atRoof - 0.1f, z), 0.11f, kit.Timber);
                    b.Beam(new Vector3(x, bottom + 0.2f, s * d.Half.Y), new Vector3(x, bottom + 0.2f, 0f), 0.14f, kit.Timber);
                }
            }
        }

        // A porch over the front door: two turned posts on stone bases, a beam across them, and a small gabled roof back to the wall, with a lantern hung under it and the sign.
        private static void Porch(MeshBuilder b, HallDesign d, HallKit kit, List<AssetLight> lights)
        {
            VertexMaterial timber = kit.Timber;
            float front = d.Half.Y + 3.1f;
            float beam = 6.9f;
            foreach (HallPiece post in d.Outside.Where(p => p.Kind == PieceKind.Post))
            {
                Vector3 foot = new Vector3(post.At.X, -0.1f, post.At.Y);
                b.Box(foot + Vector3.UnitY * 0.35f, new Vector3(0.42f, 0.35f, 0.42f), kit.Dressing);
                Hall.Turned(b, foot + Vector3.UnitY * 0.7f, timber, 8, (0f, 0.26f), (0.3f, 0.3f), (0.42f, 0.22f), (3f, 0.24f), (3.3f, 0.3f), (3.45f, 0.21f), (beam - 0.9f, 0.23f), (beam - 0.75f, 0.32f), (beam - 0.7f, 0.3f));
                // Knee braces up to the beam and back to the wall.
                b.Beam(foot + new Vector3(0f, beam - 2f, 0f), foot + new Vector3(-MathF.Sign(post.At.X) * 1.4f, beam - 0.2f, 0f), 0.09f, timber);
                b.Beam(foot + new Vector3(0f, beam - 2f, 0f), foot + new Vector3(0f, beam - 0.2f, -1.4f), 0.09f, timber);
            }
            b.Box(new Vector3(0f, beam, front), new Vector3(4f, 0.22f, 0.2f), timber);
            foreach (float s in new[] { -3.1f, 3.1f }) b.Box(new Vector3(s, beam, (front + d.Half.Y) * 0.5f), new Vector3(0.18f, 0.2f, (front - d.Half.Y) * 0.5f), timber);

            // Its roof: a little gable running back into the wall.
            float pitch = 0.75f, half = 4.2f;
            float eave = beam + 0.25f, ridge = eave + half * pitch;
            float z0 = d.Half.Y, z1 = front + 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 e0 = new Vector3(s * half, eave, z1), e1 = new Vector3(s * half, eave, z0), r1 = new Vector3(0f, ridge, z0), r0 = new Vector3(0f, ridge, z1);
                Vector3 normal = Vector3.Cross(e1 - e0, r0 - e0);
                VertexMaterial roof = kit.Roof;
                var tint = HallRoofs.Weathering(roof, kit.Moss, eave, ridge, 77);
                Vector3 u = (e1 - e0).Normalized();
                Vector3 n = normal.Y > 0f ? normal.Normalized() : -normal.Normalized();
                Vector3 v = Vector3.Cross(n, u).Normalized();
                if (normal.Y > 0f) b.PlaneQuad(e0, e1, r1, r0, roof, e0, u, v, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f), tint);
                else b.PlaneQuad(e1, e0, r0, r1, roof, e1, -u, v, new Vector4(-1e3f, 1e3f, -1e3f, 1e3f), tint);
                b.Quad(e0, r0, r0 - Vector3.UnitY * 0.25f, e0 - Vector3.UnitY * 0.25f, kit.Soffit);
            }
            b.Tube(new Vector3(0f, ridge + 0.02f, z0), new Vector3(0f, ridge + 0.02f, z1 + 0.1f), 0.16f, 0.16f, 6, kit.Ridge);
            // The gable of the porch, boarded.
            b.Triangle(new Vector3(-half + 0.3f, eave, z1 - 0.05f), new Vector3(half - 0.3f, eave, z1 - 0.05f), new Vector3(0f, ridge - 0.15f, z1 - 0.05f), Hall.Wood(120, 86, 56));

            // A lantern hung from the beam, and the sign, two dice.
            Vector3 hang = new Vector3(0f, beam - 0.5f, front - 0.4f);
            b.Tube(hang + Vector3.UnitY * 0.8f, hang + Vector3.UnitY * 0.33f, 0.02f, 0.02f, 4, Hall.Iron(70));
            b.Box(hang, new Vector3(0.24f, 0.32f, 0.24f), Hall.Glow(1f, 0.6f, 0.24f, 2.6f));
            b.Box(hang + Vector3.UnitY * 0.36f, new Vector3(0.3f, 0.05f, 0.3f), Hall.Iron(70));
            b.Box(hang - Vector3.UnitY * 0.36f, new Vector3(0.28f, 0.05f, 0.28f), Hall.Iron(70));
            lights.Add(AssetLight.Lantern(hang) with { Intensity = 120f, Radius = 18f });

            HallDressing.Sign(b, HallWalls.OnWall(d, HallSide.Front, -5.3f, 6.4f), Vector3.UnitZ, Hall.Wood(44, 92, 70), Hall.Paper, (middle, out_, across) =>
            {
                foreach (float face in new[] { 0.08f, -0.08f })
                {
                    Vector3 f = middle + across * face;
                    Die(b, f + out_ * -0.28f + Vector3.UnitY * 0.05f, 0.26f, 0.3f);
                    Die(b, f + out_ * 0.3f - Vector3.UnitY * 0.12f, 0.24f, -0.45f);
                }
            });
        }

        /// <summary>A die, pale with dark pips, turned a little.</summary>
        public static void Die(MeshBuilder b, Vector3 centre, float half, float yaw)
        {
            Hall.At(b, centre, yaw, () =>
            {
                b.Box(Vector3.Zero, new Vector3(half), Hall.Bone(226, 216, 196));
                VertexMaterial pip = Hall.Flat(30, 24, 22);
                float p = half * 0.45f, s = half * 0.16f, out_ = half + 0.004f;
                // Five on the front, two on top, three on the side.
                foreach (Vector2 at in new[] { new Vector2(0f, 0f), new Vector2(-p, -p), new Vector2(p, p), new Vector2(-p, p), new Vector2(p, -p) }) b.Box(new Vector3(at.X, at.Y, out_), new Vector3(s, s, 0.004f), pip);
                foreach (Vector2 at in new[] { new Vector2(-p, -p), new Vector2(p, p) }) b.Box(new Vector3(at.X, out_, at.Y), new Vector3(s, 0.004f, s), pip);
                foreach (Vector2 at in new[] { new Vector2(-p, -p), new Vector2(0f, 0f), new Vector2(p, p) }) b.Box(new Vector3(out_, at.Y, at.X), new Vector3(0.004f, s, s), pip);
            });
        }

        // Little lanterns strung under the front eaves, warm in the evening.
        private static void StringLanterns(MeshBuilder b, HallDesign d, HallRoofs.Gable g, List<AssetLight> lights)
        {
            float y = g.EaveY - 0.5f;
            float z = d.Half.Y + g.Eaves - 0.45f;
            float from = -d.Half.X - 0.6f, to = d.Half.X + 0.6f;
            int count = 14;
            Vector3 previous = new Vector3(from, y, z);
            VertexMaterial cord = Hall.Flat(46, 38, 30);
            Vector3[] shades = { new(1f, 0.62f, 0.26f), new(1f, 0.5f, 0.3f), new(0.95f, 0.75f, 0.35f) };
            for (int i = 1; i <= count; i++)
            {
                float t = i / (float)count;
                float x = MathHelper.Lerp(from, to, t);
                float sag = 0.55f * MathF.Sin(t * count * MathF.PI / 3.5f) * MathF.Sin(t * MathF.PI);
                Vector3 point = new Vector3(x, y - MathF.Abs(sag), z);
                b.Beam(previous, point, 0.012f, cord);
                if (i < count && MathF.Abs(x) > 3.6f)
                {
                    Vector3 lantern = point - Vector3.UnitY * 0.38f;
                    b.Tube(point, lantern + Vector3.UnitY * 0.22f, 0.01f, 0.01f, 3, cord);
                    Vector3 shade = shades[i % shades.Length];
                    b.Tube(lantern - Vector3.UnitY * 0.22f, lantern + Vector3.UnitY * 0.22f, 0.17f, 0.17f, 8, Hall.Glow(shade.X, shade.Y, shade.Z, 1.6f));
                    b.Tube(lantern + Vector3.UnitY * 0.22f, lantern + Vector3.UnitY * 0.28f, 0.12f, 0.08f, 8, Hall.Flat(40, 32, 26));
                }
                previous = point;
            }
        }

        // ---- The Archive.

        private static void Archive(MeshBuilder b, HallDesign d, HallKit kit, List<Vector3> chimneys, List<AssetLight> lights, WorldRandom random)
        {
            HallOpening door = d.Doors.First();
            var dress = new Dress { StoneSurrounds = true, Shutters = false, Lattice = true, LanternAlong = door.HalfWidth + 1.35f, LanternProud = 0.46f };
            HallDressing.Openings(b, d, kit, dress, lights, random);
            Cornice(b, d, kit.Dressing);
            foreach (HallPiece buttress in d.Outside.Where(p => p.Kind == PieceKind.Buttress)) Buttress(b, d, kit, buttress);
            Quoins(b, d, kit.Dressing);

            float ridge = HallRoofs.HipRoof(b, d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, kit.Overhang, kit, 31);
            Cupola(b, ridge, kit, lights);
            // Dormers on the long slope towards the square, each over a pair of windows, faced in the same stone.
            var slope = new HallRoofs.Gable(d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, kit.Overhang, 0f);
            foreach (float along in new[] { -13f, 13f }) HallRoofs.Dormer(b, slope, kit, kit.Dressing, along, 8.8f, 1.6f, 3f, lights, (int)(along * 3f) + 90);
            Pediment(b, d, kit);

            // The sign over the door: an open book.
            HallDressing.Sign(b, HallWalls.OnWall(d, HallSide.Front, -7.2f, 7.2f), Vector3.UnitZ, Hall.Wood(40, 66, 98), Hall.Paper, (middle, out_, across) =>
            {
                foreach (float face in new[] { 0.08f, -0.08f })
                {
                    Vector3 f = middle + across * face;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 page = f + out_ * (s * 0.33f) - Vector3.UnitY * 0.05f;
                        b.Box(page, new Vector3(MathF.Abs(out_.X) * 0.3f + MathF.Abs(across.X) * 0.02f, 0.4f, MathF.Abs(out_.Z) * 0.3f + MathF.Abs(across.Z) * 0.02f), Hall.Paper);
                        for (int line = 0; line < 4; line++) b.Box(page + Vector3.UnitY * (0.22f - line * 0.14f) + across * MathF.Sign(face) * 0.025f, new Vector3(MathF.Abs(out_.X) * 0.2f + MathF.Abs(across.X) * 0.005f, 0.02f, MathF.Abs(out_.Z) * 0.2f + MathF.Abs(across.Z) * 0.005f), Hall.Ink);
                    }
                }
            });
        }

        // A moulded cornice along the top of the walls, under the eaves.
        private static void Cornice(MeshBuilder b, HallDesign d, VertexMaterial stone)
        {
            foreach ((float drop, float height, float proud) in new[] { (0.95f, 0.22f, 0.16f), (0.5f, 0.3f, 0.34f), (0.12f, 0.12f, 0.46f) })
            {
                float y = d.WallTop - drop;
                b.Box(new Vector3(0f, y + height * 0.5f, d.Half.Y + proud * 0.5f), new Vector3(d.Half.X + proud, height * 0.5f, proud * 0.5f), stone);
                b.Box(new Vector3(0f, y + height * 0.5f, -d.Half.Y - proud * 0.5f), new Vector3(d.Half.X + proud, height * 0.5f, proud * 0.5f), stone);
                b.Box(new Vector3(d.Half.X + proud * 0.5f, y + height * 0.5f, 0f), new Vector3(proud * 0.5f, height * 0.5f, d.Half.Y), stone);
                b.Box(new Vector3(-d.Half.X - proud * 0.5f, y + height * 0.5f, 0f), new Vector3(proud * 0.5f, height * 0.5f, d.Half.Y), stone);
            }
        }

        // A buttress: a stepped pier against the wall with sloped weatherings, as deep as its footprint says.
        private static void Buttress(MeshBuilder b, HallDesign d, HallKit kit, HallPiece piece)
        {
            Vector2 out2 = MathF.Abs(piece.At.X) > d.Half.X - 0.01f ? new Vector2(MathF.Sign(piece.At.X), 0f) : new Vector2(0f, MathF.Sign(piece.At.Y));
            Vector3 out_ = new Vector3(out2.X, 0f, out2.Y);
            Vector3 along = new Vector3(out2.Y, 0f, out2.X);
            float width = out2.X != 0f ? piece.Half.Y : piece.Half.X;
            float depth = (out2.X != 0f ? piece.Half.X : piece.Half.Y) * 2f;
            Vector3 wall = new Vector3(piece.At.X, 0f, piece.At.Y) - out_ * depth * 0.5f;
            float firstTop = d.WallTop * 0.45f, secondTop = d.WallTop - 1.4f;
            // The lower stage, full depth, and the upper, set back.
            HallDressing.Box(b, wall + out_ * (depth * 0.5f) + Vector3.UnitY * ((firstTop - 2.5f) * 0.5f), along, out_, width, (firstTop + 2.5f) * 0.5f, depth * 0.5f, kit.OuterWall);
            HallDressing.Box(b, wall + out_ * (depth * 0.32f) + Vector3.UnitY * ((firstTop + secondTop) * 0.5f), along, out_, width * 0.88f, (secondTop - firstTop) * 0.5f, depth * 0.32f, kit.OuterWall);
            // Their sloped tops.
            foreach ((float top, float reach, float w) in new[] { (firstTop, depth, width), (secondTop, depth * 0.64f, width * 0.88f) })
            {
                Vector3 o0 = wall + out_ * reach + Vector3.UnitY * top, w0 = wall + Vector3.UnitY * (top + reach * 0.7f);
                Vector3 a = o0 - along * w, c = o0 + along * w, e = w0 + along * w, f = w0 - along * w;
                if (Vector3.Dot(Vector3.Cross(c - a, f - a), out_ + Vector3.UnitY) > 0f) b.Quad(a, c, e, f, kit.Dressing);
                else b.Quad(c, a, f, e, kit.Dressing);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 p0 = o0 + along * (s * w), p1 = w0 + along * (s * w), p2 = wall + along * (s * w) + Vector3.UnitY * top;
                    if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), along * s) > 0f) b.Triangle(p0, p1, p2, kit.Dressing);
                    else b.Triangle(p1, p0, p2, kit.Dressing);
                }
            }
        }

        // An octagonal lantern on the ridge, arches all round under a copper roof gone green, with a finial: what the Archive is known by from across the village.
        private static void Cupola(MeshBuilder b, float ridge, HallKit kit, List<AssetLight> lights)
        {
            VertexMaterial wood = Hall.Wood(214, 198, 168);
            VertexMaterial copper = Hall.Metal(84, 150, 128);
            float baseY = ridge - 1.6f;
            const int Sides = 8;
            float r = 2.1f;
            // Its base, sitting astride the ridge.
            b.Tube(new Vector3(0f, baseY, 0f), new Vector3(0f, ridge + 0.9f, 0f), r + 0.35f, r + 0.3f, Sides, kit.Dressing);
            b.Tube(new Vector3(0f, ridge + 0.9f, 0f), new Vector3(0f, ridge + 1.1f, 0f), r + 0.5f, r + 0.5f, Sides, kit.Dressing);
            float postsFrom = ridge + 1.1f, postsTo = ridge + 4.2f;
            for (int i = 0; i < Sides; i++)
            {
                float a = MathF.Tau * (i + 0.5f) / Sides;
                Vector3 at = new Vector3(MathF.Cos(a) * r, 0f, MathF.Sin(a) * r);
                b.Box(at + Vector3.UnitY * ((postsFrom + postsTo) * 0.5f), new Vector3(0.18f, (postsTo - postsFrom) * 0.5f, 0.18f), wood);
                // An arch between this post and the next, and a rail across its foot.
                float a1 = MathF.Tau * (i + 1.5f) / Sides;
                Vector3 next = new Vector3(MathF.Cos(a1) * r, 0f, MathF.Sin(a1) * r);
                Vector3 mid = (at + next) * 0.5f;
                b.Beam(at + Vector3.UnitY * (postsTo - 0.6f), mid + Vector3.UnitY * (postsTo - 0.15f), 0.08f, wood);
                b.Beam(mid + Vector3.UnitY * (postsTo - 0.15f), next + Vector3.UnitY * (postsTo - 0.6f), 0.08f, wood);
                b.Beam(at + Vector3.UnitY * (postsFrom + 0.9f), next + Vector3.UnitY * (postsFrom + 0.9f), 0.06f, wood);
            }
            b.Tube(new Vector3(0f, postsTo, 0f), new Vector3(0f, postsTo + 0.35f, 0f), r + 0.35f, r + 0.4f, Sides, wood);
            // The roof: an octagonal spire in copper, flaring at its foot, and a finial.
            b.Tube(new Vector3(0f, postsTo + 0.35f, 0f), new Vector3(0f, postsTo + 0.9f, 0f), r + 0.7f, r + 0.1f, Sides, copper);
            b.Tube(new Vector3(0f, postsTo + 0.9f, 0f), new Vector3(0f, postsTo + 4.6f, 0f), r + 0.1f, 0.12f, Sides, copper);
            Hall.Turned(b, new Vector3(0f, postsTo + 4.5f, 0f), copper, 8, (0f, 0.16f), (0.3f, 0.3f), (0.6f, 0.16f), (0.75f, 0.06f), (1.9f, 0.03f));
            // A faint warm light up there, as though someone keeps a lamp in it.
            b.Tube(new Vector3(0f, postsFrom, 0f), new Vector3(0f, postsFrom + 0.4f, 0f), 0.35f, 0.3f, 8, Hall.Glow(1f, 0.62f, 0.3f, 1.4f));
        }

        // Over the great door: pilasters either side and a triangular pediment on an entablature, standing proud of the wall.
        private static void Pediment(MeshBuilder b, HallDesign d, HallKit kit)
        {
            HallOpening door = d.Doors.First();
            float z = d.Half.Y;
            float halfSpan = door.HalfWidth + 1.9f;
            VertexMaterial stone = kit.Dressing;
            float top = door.Head + 1.1f;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = door.Along + s * (door.HalfWidth + 1.35f);
                b.Box(new Vector3(x, top * 0.5f, z + 0.2f), new Vector3(0.42f, top * 0.5f, 0.22f), stone);
                b.Box(new Vector3(x, top - 0.25f, z + 0.3f), new Vector3(0.55f, 0.25f, 0.32f), stone);
                b.Box(new Vector3(x, 0.45f, z + 0.3f), new Vector3(0.55f, 0.45f, 0.32f), stone);
            }
            b.Box(new Vector3(door.Along, top + 0.3f, z + 0.32f), new Vector3(halfSpan, 0.3f, 0.34f), stone);
            float rise = halfSpan * 0.42f;
            float baseY = top + 0.6f;
            Vector3 l = new Vector3(door.Along - halfSpan - 0.15f, baseY, z + 0.62f), r = new Vector3(door.Along + halfSpan + 0.15f, baseY, z + 0.62f), p = new Vector3(door.Along, baseY + rise, z + 0.62f);
            b.Triangle(l, r, p, stone);
            Vector3 back = new Vector3(0f, 0f, -0.62f);
            b.Quad(r, r + back, p + back, p, stone);
            b.Quad(p, p + back, l + back, l, stone);
            // The raking cornice along its top, and a carved disc in its tympanum.
            b.Beam(l + new Vector3(-0.1f, 0.1f, 0.06f), p + new Vector3(0f, 0.16f, 0.06f), 0.16f, stone);
            b.Beam(p + new Vector3(0f, 0.16f, 0.06f), r + new Vector3(0.1f, 0.1f, 0.06f), 0.16f, stone);
            b.Tube(new Vector3(door.Along, baseY + rise * 0.38f, z + 0.62f), new Vector3(door.Along, baseY + rise * 0.38f, z + 0.72f), 0.55f, 0.5f, 12, Hall.Stone(206, 194, 170));
        }
    }
}
