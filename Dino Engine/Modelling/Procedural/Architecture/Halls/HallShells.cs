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
                case HallKind.GamingHouse: Barn(b, d, kit, chimneys, lights, random); break;
                default: Archive(b, d, kit, chimneys, lights, random); break;
            }
        }

        // The floor, inside the walls and out through the doorways, a hair above the levelled ground under it.
        private static void Floor(MeshBuilder b, HallDesign d, HallKit kit)
        {
            Vector2 inner = d.Inner;
            const float Y = Hall.FloorTop;
            Vector3 origin = new Vector3(0f, Y, 0f);
            // u along x and v towards the back, so boards run away from the door.
            void Lay(float x0, float x1, float z0, float z1, VertexMaterial material)
            {
                b.Panel(origin, Vector3.UnitX, -Vector3.UnitZ, x0, x1, -z1, -z0, material, new Vector4(x0, x1, -z1, -z0));
            }
            if (d.Kind == HallKind.Forge)
            {
                // A smithy's floor is three: flags where it's hot, from the walls out to the anvil, as nothing dropped there can burn; boards in the tailor's corner, where
                // cloth is laid out and nobody wants it on stone; and cobbles everywhere else, which is where the water goes (Smithy.HotFloor, Smithy.BoardFloor).
                Vector4 hot = Smithy.HotFloor(d), boards = Smithy.BoardFloor(d);
                Lay(hot.X, hot.Y, hot.Z, hot.W, Hall.Flags(150, 146, 142));
                Lay(boards.X, boards.Y, boards.Z, boards.W, Hall.Wood(150, 110, 74));
                Lay(hot.Y, inner.X, -inner.Y, hot.W, kit.Floor);
                Lay(-inner.X, boards.X, hot.W, inner.Y, kit.Floor);
                Lay(boards.X, inner.X, hot.W, boards.Z, kit.Floor);
                return;
            }
            if (d.Kind == HallKind.Archive)
            {
                // Marble, slab by slab, and everything let into it (Basilica.Floor.cs).
                Basilica.Floor(b, d);
                return;
            }

            // A barn's floor: the threshing floor flagged in stone, straight through from the great doors between the posts; a bay of boards either side of it, the one by
            // the fire darker with its smoke; and the hearth's own floor of stone before it, where a spark does no harm (GamingHouse.HearthFloor).
            float bay = HallDesigns.GamingBay;
            Vector4 hearth = GamingHouse.HearthFloor(d);
            Lay(-bay, bay, -inner.Y, inner.Y, Hall.Flags(188, 192, 194));
            Lay(bay, inner.X, -inner.Y, inner.Y, kit.Floor);
            VertexMaterial smoked = Hall.Wood(108, 80, 56);
            Lay(-inner.X, -bay, -inner.Y, hearth.Z, smoked);
            Lay(-inner.X, -bay, hearth.W, inner.Y, smoked);
            Lay(hearth.Y, -bay, hearth.Z, hearth.W, smoked);
            Lay(hearth.X, hearth.Y, hearth.Z, hearth.W, Hall.Ashlar(120, 112, 104));
        }

        // ---- The Forge.

        private static void Forge(MeshBuilder b, HallDesign d, HallKit kit, List<Vector3> chimneys, List<AssetLight> lights, WorldRandom random)
        {
            var dress = new Dress { StoneSurrounds = true, Shutters = true, Lattice = false };
            HallDressing.Openings(b, d, kit, dress, lights, random);
            Quoins(b, d, kit.Dressing);

            var gable = new HallRoofs.Gable(d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, kit.Overhang, 1f);
            // A smithy's slates are black with its own smoke for a good way round its stack.
            HallRoofs.GableRoof(b, gable, kit, kit.OuterWall, kit.InnerWall, d.Wall, 11, sootAt: new Vector3(0f, 0f, -8.5f));
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

        /// <summary>
        /// The forge itself: a block of stone as high as a hip with the fire laid in a pit along its front, open to the room, and over the back of it a hood of beaten copper
        /// on two piers, set high and well back so that whoever's in the room sees the fire and not the hood: from the camera, which looks down from the south, anything hung
        /// over the bed lower than this would hide it. The hood narrows to a flue that goes up through the roof as the tallest chimney in the Haven. Bars are heating in the
        /// coals with their ends out, the tools of the fire hang on the piers, and the ash is raked out of an arch in its front.
        /// </summary>
        private static void ForgeHearth(MeshBuilder b, HallDesign d, HallKit kit, HallPiece hearth, HallRoofs.Gable gable, List<Vector3> chimneys, List<AssetLight> lights)
        {
            VertexMaterial stone = kit.Dressing;
            VertexMaterial rough = Hall.Stone(124, 116, 106);
            VertexMaterial brick = Hall.Brick(150, 122, 106);
            VertexMaterial copper = new VertexMaterial(Textures.MaterialCatalog.RivetedPlate.Index, new Util.Colour(150, 98, 66));
            VertexMaterial iron = Hall.Iron(70);
            Vector2 at = hearth.At, half = hearth.Half;
            float backFace = -d.Half.Y + d.Wall;
            float front = at.Y + half.Y;
            const float Bed = 2f;

            // The block, a kerb of dressed stone round its top, and the arch the ash is drawn from.
            b.Box(new Vector3(at.X, Bed * 0.5f, at.Y), new Vector3(half.X, Bed * 0.5f, half.Y), rough);
            b.Box(new Vector3(at.X, Bed + 0.11f, front - 0.17f), new Vector3(half.X, 0.11f, 0.17f), stone);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(at.X + s * (half.X - 0.17f), Bed + 0.11f, at.Y - 0.17f), new Vector3(0.17f, 0.11f, half.Y - 0.17f), stone);
            b.Box(new Vector3(at.X, 0.2f, front + 0.02f), new Vector3(half.X + 0.06f, 0.2f, 0.06f), stone);
            b.Box(new Vector3(at.X - 1.2f, 0.62f, front + 0.012f), new Vector3(0.52f, 0.42f, 0.02f), Hall.Flat(22, 20, 20));
            b.Box(new Vector3(at.X - 1.2f, 1.1f, front + 0.03f), new Vector3(0.66f, 0.1f, 0.05f), stone);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(at.X - 1.2f + s * 0.6f, 0.62f, front + 0.03f), new Vector3(0.09f, 0.42f, 0.05f), stone);
            b.Box(new Vector3(at.X - 1.2f, 0.24f, front + 0.3f), new Vector3(0.44f, 0.04f, 0.3f), Hall.Flat(84, 80, 76));

            // The fire: a pit of firebrick along the front of the block, its coals heaped highest and hottest in the middle, where the blast comes in.
            float pitFrom = front - 2.7f, pitTo = front - 0.4f;
            float pitMiddle = (pitFrom + pitTo) * 0.5f, pitHalf = (pitTo - pitFrom) * 0.5f;
            float pitWide = half.X - 0.5f;
            b.Box(new Vector3(at.X, Bed + 0.03f, pitMiddle), new Vector3(pitWide, 0.03f, pitHalf), brick);
            b.Box(new Vector3(at.X, Bed + 0.09f, pitMiddle), new Vector3(pitWide - 0.14f, 0.05f, pitHalf - 0.14f), Hall.Glow(0.9f, 0.18f, 0.03f, 0.75f));
            var rng = new WorldRandom(0xC0A1);
            for (int i = 0; i < 60; i++)
            {
                float x = rng.Range(-pitWide + 0.2f, pitWide - 0.2f), z = rng.Range(-pitHalf + 0.2f, pitHalf - 0.2f);
                float heart = 1f - Math.Clamp(MathF.Sqrt(x * x / (pitWide * pitWide) + z * z / (pitHalf * pitHalf)), 0f, 1f);
                float size = rng.Range(0.11f, 0.22f);
                VertexMaterial coal = heart > 0.5f && rng.Chance(0.75f) ? Hall.Glow(1f, 0.4f, 0.09f, 1.35f) : heart > 0.2f && rng.Chance(0.6f) ? Hall.Glow(0.85f, 0.16f, 0.03f, 0.7f) : Hall.Flat(30, 26, 24);
                b.Box(new Vector3(at.X + x, Bed + 0.12f + heart * 0.22f + size * 0.4f, pitMiddle + z), new Vector3(size, size * 0.6f, size * 0.9f), coal);
            }

            // What's in it: three bars with their ends in the coals, each as hot as how long it's lain there, and the rake that tends it across the kerb.
            float[] heats = { 0.9f, 0.55f, 0.25f };
            for (int i = 0; i < heats.Length; i++)
            {
                float x = at.X - 0.9f + i * 0.85f;
                Vector3 tip = new Vector3(x, Bed + 0.36f, pitMiddle + 0.1f - i * 0.12f), middle = new Vector3(x + 0.03f, Bed + 0.32f, pitTo - 0.05f), end = new Vector3(x + 0.06f, Bed + 0.27f, front + 0.5f);
                Hall.Board(b, tip, Vector3.Lerp(tip, middle, 0.6f), Vector3.UnitX, 0.045f, 0.035f, Hall.Glow(1f, 0.14f + 0.44f * heats[i], 0.02f + 0.16f * heats[i] * heats[i], 0.55f + 0.75f * heats[i]));
                Hall.Board(b, Vector3.Lerp(tip, middle, 0.6f), middle, Vector3.UnitX, 0.045f, 0.035f, Hall.Glow(0.85f, 0.16f, 0.03f, 0.5f + 0.3f * heats[i]));
                Hall.Board(b, middle, end, Vector3.UnitX, 0.045f, 0.035f, Hall.Iron(60));
            }
            b.Beam(new Vector3(at.X + 2.1f, Bed + 0.3f, pitMiddle), new Vector3(at.X + 2.5f, Bed + 0.26f, front + 0.6f), 0.022f, iron);
            b.Box(new Vector3(at.X + 2.1f, Bed + 0.3f, pitMiddle), new Vector3(0.16f, 0.02f, 0.05f), iron);

            // The piers the hood stands on, at the back corners of the pit, with the fire's tools hung on their fronts.
            const float Mouth = 6.4f, Neck = 8.6f;
            float hoodFront = pitFrom + 0.25f;
            Vector2 flueHalf = new Vector2(1.3f, 1.05f);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pier = new Vector3(at.X + s * (half.X - 0.36f), 0f, hoodFront - 0.3f);
                b.Box(pier + Vector3.UnitY * ((Bed + Mouth) * 0.5f), new Vector3(0.3f, (Mouth - Bed) * 0.5f, 0.3f), stone);
                b.Box(pier + Vector3.UnitY * (Mouth - 0.16f), new Vector3(0.38f, 0.16f, 0.38f), stone);
                b.Box(pier + new Vector3(0f, 4.6f, 0.31f), new Vector3(0.28f, 0.04f, 0.03f), iron);
                for (int tool = 0; tool < 2; tool++)
                {
                    Vector3 hook = pier + new Vector3((tool - 0.5f) * 0.3f, 4.56f, 0.36f);
                    float length = 1.5f + tool * 0.3f;
                    b.Beam(hook, hook - Vector3.UnitY * length, 0.02f, iron);
                    if (s < 0f == (tool == 0)) b.Beam(hook + Vector3.UnitX * 0.06f, hook + Vector3.UnitX * 0.06f - Vector3.UnitY * length, 0.02f, iron);
                    else b.Box(hook - Vector3.UnitY * length, new Vector3(0.1f, 0.03f, 0.03f), iron);
                }
            }

            // The hood: copper, riveted, a band of iron round its mouth, drawn in to the flue.
            float mouthHalf = half.X - 0.1f;
            Vector3 l0 = new Vector3(at.X - mouthHalf, Mouth, hoodFront), l1 = new Vector3(at.X + mouthHalf, Mouth, hoodFront);
            Vector3 n0 = new Vector3(at.X - flueHalf.X, Neck, backFace + flueHalf.Y * 2f), n1 = new Vector3(at.X + flueHalf.X, Neck, backFace + flueHalf.Y * 2f);
            Vector3 lb0 = new Vector3(at.X - mouthHalf, Mouth, backFace), lb1 = new Vector3(at.X + mouthHalf, Mouth, backFace);
            Vector3 nb0 = new Vector3(at.X - flueHalf.X, Neck, backFace), nb1 = new Vector3(at.X + flueHalf.X, Neck, backFace);
            Hall.Face(b, l0, l1, n1, n0, Vector3.UnitZ + Vector3.UnitY, copper);
            Hall.Face(b, l1, lb1, nb1, n1, Vector3.UnitX, copper);
            Hall.Face(b, lb0, l0, n0, nb0, -Vector3.UnitX, copper);
            Hall.Face(b, l1, l0, lb0, lb1, -Vector3.UnitY, Hall.Flat(30, 27, 26));
            Hall.Board(b, l0 + new Vector3(-0.04f, -0.1f, 0.03f), l1 + new Vector3(0.04f, -0.1f, 0.03f), Vector3.UnitY, 0.14f, 0.035f, iron);
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(at.X + s * (mouthHalf + 0.03f), Mouth - 0.1f, backFace), new Vector3(at.X + s * (mouthHalf + 0.03f), Mouth - 0.1f, hoodFront + 0.04f), Vector3.UnitY, 0.14f, 0.035f, iron);
            // The flue, up through the wall top and out of the roof behind the ridge.
            float top = gable.Ridge + gable.Lift + 3.2f;
            HallRoofs.Chimney(b, new Vector3(at.X, Neck, backFace + flueHalf.Y), flueHalf + new Vector2(0.2f, 0.15f), top, kit.OuterWall, stone, chimneys, pots: 1);

            // Its flames, its smoke and its sparks, along the bed, thrown towards the room, and its light.
            foreach (float x in new[] { -1.9f, 0f, 1.9f })
            {
                lights.Add(AssetLight.Emitting(new Vector3(at.X + x, Bed + 0.3f, pitMiddle), "Smithing.Coals", x == 0f ? 1.25f : 1f, Vector3.UnitZ));
            }
            lights.Add(new AssetLight
            {
                Position = new Vector3(at.X, Bed + 2.6f, front + 1f), Colour = new Vector3(1f, 0.48f, 0.16f), Intensity = 170f, Radius = 23f, Fill = 0.25f,
                Flicker = 0.3f, FlickerSpeed = 7f, Shadow = true, LightsFog = true,
            });
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

        private static void Barn(MeshBuilder b, HallDesign d, HallKit kit, List<Vector3> chimneys, List<AssetLight> lights, WorldRandom random)
        {
            var dress = new Dress { StoneSurrounds = false, Shutters = true, FlowerBoxes = false, Lattice = true };
            Framing(b, d, kit, 4.9f);
            HallDressing.Openings(b, d, kit, dress, lights, random);

            // An old barn's roof: thatch, as the homes round it are roofed, sagging along its ridge as a roof this long does after a hundred years, with a brow lifted over
            // each of the hayloft's windows. The loft's door is over the great doors, under the eaves, with the beam its hay was hoisted on.
            float ridge = Town.TownHouse.ThatchOver(b, d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, 0xBA54E1u, new[] { -8.2f, 8.2f }, kit.OuterWall, kit.Timber, lights, 0.3f);
            var gable = new HallRoofs.Gable(d.Half.X, d.Half.Y, d.WallTop, kit.RoofPitch, Town.TownHouse.ThatchEaves, 0.5f);
            LoftDoor(b, d, kit);

            // The fire's chimney, built out from the east wall from the ground up past the eaves.
            HallPiece breast = d.Outside.First(p => p.Kind == PieceKind.ChimneyBreast);
            float top = ridge + 1.3f;
            Vector3 foot = new Vector3(breast.At.X, -0.4f, breast.At.Y);
            b.Box(foot + Vector3.UnitY * 3.6f, new Vector3(breast.Half.X + 0.15f, 4f, breast.Half.Y + 0.2f), kit.Plinth);
            b.Box(foot + new Vector3(0.05f, 7.9f, 0f), new Vector3(breast.Half.X + 0.06f, 0.3f, breast.Half.Y + 0.05f), kit.Dressing);
            HallRoofs.Chimney(b, foot + new Vector3(0.1f, 7.6f, 0f), new Vector2(breast.Half.X - 0.05f, breast.Half.Y - 0.5f), top, Hall.Stone(166, 156, 140), kit.Dressing, chimneys, pots: 2);
            // A smaller one at the back, for the bar's kitchen.
            HallRoofs.Chimney(b, new Vector3(6.4f, d.WallTop, -6.6f), new Vector2(0.7f, 0.6f), gable.FaceAt(-6.6f) + 2.2f, Hall.Brick(150, 100, 76), kit.Dressing, chimneys, pots: 1);

            Entrance(b, d, kit, lights);
            StringLanterns(b, d, gable, lights);
        }

        // The hayloft's door, over the great doors: boards in a frame, shut, and over it the beam hay was hoisted on, a wheel at its end and the rope still through it.
        private static void LoftDoor(MeshBuilder b, HallDesign d, HallKit kit)
        {
            HallOpening door = d.Doors.First();
            float z = d.Half.Y;
            float sill = door.Head + 0.45f, head = d.WallTop - 0.35f;
            const float Half = 1.15f;
            b.Box(new Vector3(door.Along, (sill + head) * 0.5f, z + 0.05f), new Vector3(Half, (head - sill) * 0.5f, 0.05f), kit.DoorLeaf);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(door.Along + s * (Half + 0.12f), (sill + head) * 0.5f, z + 0.08f), new Vector3(0.12f, (head - sill) * 0.5f + 0.1f, 0.08f), kit.Timber);
            b.Box(new Vector3(door.Along, sill - 0.1f, z + 0.14f), new Vector3(Half + 0.3f, 0.1f, 0.16f), kit.Timber);
            b.Beam(new Vector3(door.Along - Half + 0.1f, sill + 0.15f, z + 0.12f), new Vector3(door.Along + Half - 0.1f, head - 0.15f, z + 0.12f), 0.07f, kit.Joinery);
            foreach (float y in new[] { sill + 0.25f, head - 0.25f }) b.Box(new Vector3(door.Along, y, z + 0.12f), new Vector3(Half - 0.05f, 0.09f, 0.03f), kit.Joinery);
            // The hoist.
            Vector3 root = new Vector3(door.Along, d.WallTop - 0.12f, z), end = root + Vector3.UnitZ * 2.1f;
            b.Box((root + end) * 0.5f, new Vector3(0.13f, 0.15f, 1.05f), kit.Timber);
            b.Beam(root + new Vector3(0f, -1.3f, 0.08f), end + new Vector3(0f, -0.1f, -0.45f), 0.08f, kit.Timber);
            Vector3 wheel = end + new Vector3(0f, -0.38f, -0.2f);
            b.Tube(wheel - Vector3.UnitX * 0.07f, wheel + Vector3.UnitX * 0.07f, 0.24f, 0.24f, 10, Hall.Wood(120, 92, 64));
            VertexMaterial rope = Hall.Cloth(160, 142, 104);
            b.Beam(wheel + new Vector3(0f, 0f, 0.24f), wheel + new Vector3(0f, -2.6f, 0.24f), 0.025f, rope);
            b.Beam(wheel + new Vector3(0f, 0f, -0.24f), new Vector3(door.Along + Half + 0.4f, sill + 0.4f, z + 0.15f), 0.025f, rope);
            b.Tube(wheel + new Vector3(0f, -2.75f, 0.24f), wheel + new Vector3(0f, -2.6f, 0.24f), 0.03f, 0.1f, 6, Hall.Iron(96));
        }

        // The way in: two turned posts before the great doors with a beam across them under a board of a roof, the house's sign hung from the middle of it, two dice, and a
        // lantern either side. Open to the sky, so that the doors are seen under it from above.
        private static void Entrance(MeshBuilder b, HallDesign d, HallKit kit, List<AssetLight> lights)
        {
            VertexMaterial timber = kit.Timber;
            const float Beam = 7.3f;
            float z = d.Half.Y + 3.1f;
            foreach (HallPiece post in d.Outside.Where(p => p.Kind == PieceKind.Post))
            {
                Vector3 foot = new Vector3(post.At.X, -0.1f, post.At.Y);
                b.Box(foot + Vector3.UnitY * 0.35f, new Vector3(0.42f, 0.35f, 0.42f), kit.Dressing);
                Hall.Turned(b, foot + Vector3.UnitY * 0.7f, timber, 8, (0f, 0.26f), (0.3f, 0.3f), (0.42f, 0.22f), (3f, 0.24f), (3.3f, 0.3f), (3.45f, 0.21f), (Beam - 0.9f, 0.23f), (Beam - 0.75f, 0.32f), (Beam - 0.7f, 0.3f));
                b.Beam(foot + new Vector3(0f, Beam - 1.9f, 0f), foot + new Vector3(-MathF.Sign(post.At.X) * 1.5f, Beam - 0.1f, 0f), 0.09f, timber);
                b.Beam(foot + new Vector3(0f, Beam - 1.9f, 0f), foot + new Vector3(MathF.Sign(post.At.X) * 1.1f, Beam - 0.1f, 0f), 0.08f, timber);
            }
            b.Box(new Vector3(0f, Beam + 0.1f, z), new Vector3(4.5f, 0.2f, 0.18f), timber);
            b.Box(new Vector3(0f, Beam + 0.36f, z), new Vector3(4.8f, 0.06f, 0.34f), Hall.Shingle(200, 176, 150));

            // The sign.
            Vector3 middle = new Vector3(0f, Beam - 1.3f, z);
            foreach (float s in new[] { -0.8f, 0.8f }) b.Beam(new Vector3(s, Beam - 0.1f, z), new Vector3(s, Beam - 0.5f, z), 0.02f, Hall.Iron(80));
            b.Box(middle, new Vector3(1.2f, 0.8f, 0.05f), Hall.Wood(44, 92, 70));
            b.Box(middle, new Vector3(1.27f, 0.87f, 0.035f), Hall.DarkOak);
            Die(b, middle + new Vector3(-0.45f, 0.06f, 0.1f), 0.27f, 0.3f);
            Die(b, middle + new Vector3(0.45f, -0.1f, 0.1f), 0.25f, -0.45f);

            // A lantern either side of it.
            foreach (float s in new[] { -2.5f, 2.5f })
            {
                Vector3 hang = new Vector3(s, Beam - 0.95f, z);
                b.Tube(hang + Vector3.UnitY * 0.85f, hang + Vector3.UnitY * 0.33f, 0.02f, 0.02f, 4, Hall.Iron(70));
                b.Box(hang, new Vector3(0.22f, 0.3f, 0.22f), Hall.Glow(1f, 0.6f, 0.24f, 2.6f));
                b.Box(hang + Vector3.UnitY * 0.34f, new Vector3(0.28f, 0.05f, 0.28f), Hall.Iron(70));
                b.Box(hang - Vector3.UnitY * 0.34f, new Vector3(0.26f, 0.05f, 0.26f), Hall.Iron(70));
                lights.Add(AssetLight.Lantern(hang) with { Intensity = 95f, Radius = 16f });
            }
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
