using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Halls.HallProps;

namespace Dino_Engine.Modelling.Procedural.Architecture.Halls
{
    /// <summary>
    /// The soft side of the Forge: what's made of hide and of cloth. Along the back a copper of dye over its own fire and two vats, one of tanning liquor with hides in it and
    /// one of the Haven's teal; a raptor's hide laced out in a frame against the light, with the rent that killed it stitched; a hide on the beam with the knife left on it; the
    /// leather table, with a boot on its last; a saddle on its horse; the loom, part way up a length of teal; the tailor's table, with one of the Haven's banners three petals
    /// done; a coat on its form with one sleeve to go; the wheel; and the cloth, in bolts, with the cat on it. As in Smithy.Metal.cs, each is in its own frame, front towards +z.
    /// </summary>
    internal static partial class Smithy
    {
        private static VertexMaterial Scaly => new VertexMaterial(MaterialCatalog.Scales.Index, new Colour(120, 118, 80));
        private static VertexMaterial Fleece => Hall.Pelt(228, 222, 206);
        private static VertexMaterial Quilt => Hall.Quilted(214, 204, 178);

        // ---- Dye and tan.

        /// <summary>
        /// A copper of saffron dye kept hot on its own fire: a ring of stones, a tripod of iron with the pot hung from its chain, the paddle standing in it, and what's boiled
        /// over run down its side and across the floor.
        /// </summary>
        private static void Cauldron(MeshBuilder b, float r, List<AssetLight> lights)
        {
            for (int i = 0; i < 9; i++)
            {
                float a = MathF.Tau * i / 9f;
                b.Box(new Vector3(MathF.Cos(a) * 0.58f, 0.1f, MathF.Sin(a) * 0.58f), new Vector3(0.13f, 0.1f + 0.02f * (i % 3), 0.11f), Rubble);
            }
            VertexMaterial log = Hall.Bark(104, 86, 70);
            b.Tube(new Vector3(-0.4f, 0.12f, 0.1f), new Vector3(0.42f, 0.14f, -0.06f), 0.09f, 0.08f, 6, log);
            b.Tube(new Vector3(-0.1f, 0.2f, -0.4f), new Vector3(0.06f, 0.16f, 0.42f), 0.08f, 0.08f, 6, log);
            b.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.36f, 0.05f, 0.34f), Hall.Ember);
            Hall.Flame(lights, new Vector3(0f, 0.22f, 0f), 0.34f);
            lights.Add(new AssetLight
            {
                Position = new Vector3(0f, 0.5f, r + 0.4f), Colour = new Vector3(1f, 0.5f, 0.18f), Intensity = 30f, Radius = 9f, Fill = 0.25f, Flicker = 0.3f, FlickerSpeed = 9f, LightsFog = true,
            });

            VertexMaterial iron = Hall.Iron(68);
            Vector3 apex = new Vector3(0f, 2.55f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.5f;
                b.Beam(new Vector3(MathF.Cos(a) * 0.7f, 0f, MathF.Sin(a) * 0.7f), apex, 0.032f, iron);
            }
            b.Beam(apex, new Vector3(0f, 1.82f, 0f), 0.018f, iron);
            foreach (float s in new[] { -1f, 1f }) b.Beam(new Vector3(s * 0.52f, 1.44f, 0f), new Vector3(0f, 1.82f, 0f), 0.018f, iron);

            // The pot: beaten copper, black underneath, the dye in it the colour of the Haven's saffron.
            b.Tube(new Vector3(0f, 0.64f, 0f), new Vector3(0f, 0.56f, 0f), 0.32f, 0.16f, 10, Hall.Flat(30, 26, 24));
            b.Tube(new Vector3(0f, 0.64f, 0f), new Vector3(0f, 0.94f, 0f), 0.32f, 0.56f, 12, Hall.Metal(150, 84, 52), capped: false);
            b.Tube(new Vector3(0f, 0.94f, 0f), new Vector3(0f, 1.4f, 0f), 0.56f, 0.5f, 12, Hall.Copper, capped: false);
            b.Tube(new Vector3(0f, 1.4f, 0f), new Vector3(0f, 1.46f, 0f), 0.53f, 0.54f, 12, Hall.Copper, capped: false);
            b.Tube(new Vector3(0f, 1.3f, 0f), new Vector3(0f, 1.32f, 0f), 0.5f, 0.5f, 12, Hall.Mirror(206, 136, 30));
            b.Tube(new Vector3(0.16f, 1.2f, 0.1f), new Vector3(0.62f, 2.5f, 0.42f), 0.035f, 0.03f, 6, Handle);
            // What's boiled over.
            Hall.Board(b, new Vector3(0.3f, 1.44f, 0.42f), new Vector3(0.36f, 0.86f, 0.44f), Vector3.UnitX, 0.06f, 0.008f, Dyed(Saffron));
            Blot(b, new Vector3(0.5f, 0.012f, r + 0.3f), 0.34f, Dyed(Saffron), 61, 1.3f, 0.5f);
            lights.Add(AssetLight.Emitting(new Vector3(0f, 1.36f, 0f), "Smithing.Vat", 0.75f));
        }

        /// <summary>
        /// A vat: a tub of staves hooped in iron, as high as a chest. One is tanning liquor, cold and brown, with bark floating in it and a hide drawn half out over its rim.
        /// The other is the Haven's teal, warm, with a length of cloth lifted out on a pole laid across it and still running, and the floor round it blotted with what's dripped.
        /// </summary>
        private static void Vat(MeshBuilder b, float r, float h, int variant, List<AssetLight> lights)
        {
            bool dye = variant == 1;
            b.Tube(Vector3.Zero, new Vector3(0f, h, 0f), r * 0.9f, r, 14, Hall.Wood(124, 90, 60), capped: false);
            b.Tube(new Vector3(0f, h, 0f), new Vector3(0f, h - 0.24f, 0f), r - 0.07f, r - 0.09f, 14, Hall.Wood(96, 70, 48), capped: false);
            for (int i = 0; i < 14; i++)
            {
                float a0 = MathF.Tau * i / 14f, a1 = MathF.Tau * (i + 1) / 14f;
                Vector3 o0 = new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)), o1 = new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1));
                Hall.Face(b, o0 * (r - 0.07f) + Vector3.UnitY * h, o0 * r + Vector3.UnitY * h, o1 * r + Vector3.UnitY * h, o1 * (r - 0.07f) + Vector3.UnitY * h, Vector3.UnitY, Hall.Wood(150, 112, 76));
            }
            foreach (float t in new[] { 0.14f, 0.5f, 0.86f })
            {
                float radius = r * (0.9f + 0.1f * t) + 0.012f;
                b.Tube(new Vector3(0f, h * t - 0.05f, 0f), new Vector3(0f, h * t + 0.05f, 0f), radius - 0.003f, radius + 0.003f, 14, Hall.Iron(80), capped: false);
            }
            float surface = h - 0.22f;
            b.Tube(new Vector3(0f, surface - 0.02f, 0f), new Vector3(0f, surface, 0f), r - 0.09f, r - 0.09f, 14, dye ? Hall.Mirror(20, 108, 106) : Hall.Mirror(66, 44, 26));

            if (!dye)
            {
                var random = new WorldRandom(0x7A7);
                for (int i = 0; i < 12; i++)
                {
                    float a = random.Range(0f, MathF.Tau), reach = random.Range(0.1f, r - 0.25f);
                    b.Box(new Vector3(MathF.Cos(a) * reach, surface + 0.01f, MathF.Sin(a) * reach), new Vector3(random.Range(0.05f, 0.11f), 0.008f, random.Range(0.03f, 0.07f)), Hall.Timber(110, 78, 52));
                }
                // A hide, drawn half out over the rim with the hook that drew it.
                Vector3 inVat = new Vector3(0.1f, surface, 0.25f), onRim = new Vector3(0.62f, h + 0.05f, 0.78f), hanging = new Vector3(0.82f, h - 0.75f, 1.02f);
                Vector3 wide = Vector3.Cross(onRim - inVat, Vector3.UnitY).Normalized();
                Hall.Board(b, inVat, onRim, wide, 0.42f, 0.02f, DarkHide);
                Hall.Board(b, onRim, hanging, wide, 0.4f, 0.02f, DarkHide);
                b.Beam(new Vector3(-0.7f, h + 0.04f, -0.6f), new Vector3(0.75f, h + 0.42f, 0.5f), 0.028f, Handle);
                Blot(b, new Vector3(0.95f, 0.012f, 1.2f), 0.3f, Hall.Flat(74, 56, 40), 71);
                return;
            }

            // The pole across it, and the cloth hung over the pole out of the dye.
            Vector3 p0 = new Vector3(-r - 0.25f, h + 0.07f, 0.16f), p1 = new Vector3(r + 0.25f, h + 0.07f, -0.1f);
            b.Tube(p0, p1, 0.045f, 0.04f, 6, Handle);
            Vector3 along = (p1 - p0).Normalized();
            Vector3 mid = (p0 + p1) * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 out_ = Vector3.Cross(along, Vector3.UnitY) * s;
                Hall.Board(b, mid + Vector3.UnitY * 0.05f + out_ * 0.04f, mid + out_ * 0.26f - Vector3.UnitY * 0.32f, along, 0.5f, 0.012f, Dyed(Teal));
            }
            b.Box(mid + Vector3.UnitY * 0.06f, new Vector3(0.5f, 0.02f, 0.06f), Dyed(Teal));
            foreach (float x in new[] { -0.3f, 0.1f, 0.36f }) b.Beam(mid + along * x - Vector3.UnitY * 0.1f, mid + along * x - Vector3.UnitY * 0.3f, 0.008f, Hall.Mirror(20, 108, 106));
            Blot(b, new Vector3(-0.5f, 0.012f, r + 0.4f), 0.34f, Dyed(Teal), 72, 1.4f, 0.3f);
            Blot(b, new Vector3(0.6f, 0.012f, r + 0.7f), 0.2f, Dyed(Teal), 73);
            Blot(b, new Vector3(1.1f, 0.012f, r + 0.2f), 0.12f, Dyed(Teal), 74);
            lights.Add(AssetLight.Emitting(new Vector3(0f, surface + 0.05f, 0f), "Smithing.Vat"));
        }

        // ---- Hide.

        /// <summary>
        /// A hide laced out to dry in a frame leaned against the wall, with a window behind it: a raptor's, scaled, as wide as a door, drawn tight by a cord through every
        /// point of its edge. Across its shoulder is the rent that killed it, sewn up so the hide can be used, and under it is the bucket of lime it was cleaned with.
        /// It stands along x, with its face towards +z.
        /// </summary>
        private static void HideFrame(MeshBuilder b, Vector2 half, float h)
        {
            float Lean(float y) => 0.26f - 0.5f * y / h;
            Vector3 P(float x, float y, float out_ = 0f) => new Vector3(x, y, Lean(y) + out_);
            float post = half.X - 0.14f;
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, P(s * post, 0f), P(s * post, h - 0.05f), Vector3.UnitX, 0.09f, 0.08f, FrameDark);
            foreach (float y in new[] { 0.42f, h - 0.22f }) Hall.Board(b, P(-post, y), P(post, y), Vector3.UnitY, 0.08f, 0.07f, FrameDark);

            // The hide: its own shape, a leg at each corner, its neck up and the root of its tail down.
            const int Points = 18;
            Vector3 centre = P(0f, h * 0.51f, 0.09f);
            var edge = new Vector3[Points];
            for (int i = 0; i < Points; i++)
            {
                float a = MathF.Tau * i / Points;
                float legs = MathF.Pow(MathF.Abs(MathF.Sin(a * 2f)), 6f) * 0.3f;
                float ends = MathF.Pow(MathF.Abs(MathF.Sin(a)), 10f) * 0.16f;
                float reach = 0.66f + legs + ends + 0.03f * MathF.Sin(a * 5f);
                float y = h * 0.51f + MathF.Sin(a) * (h * 0.36f) * reach / 0.82f;
                edge[i] = P(MathF.Cos(a) * (post - 0.3f) * reach / 0.82f, y, 0.09f);
            }
            Vector3 face = new Vector3(0f, 0.11f, 1f).Normalized();
            for (int i = 0; i < Points; i++)
            {
                Vector3 e0 = edge[i], e1 = edge[(i + 1) % Points];
                // Scaled along its back, and paler in the belly.
                Vector3 m0 = Vector3.Lerp(centre, e0, 0.5f) + face * 0.004f, m1 = Vector3.Lerp(centre, e1, 0.5f) + face * 0.004f;
                Hall.Tri(b, centre, m0, m1, face, new VertexMaterial(MaterialCatalog.Scales.Index, new Colour(156, 148, 104)));
                Hall.Face(b, m0, e0, e1, m1, face, Scaly);
                // Its cord, to the nearest of the frame.
                Vector3 out_ = e0 - centre;
                bool sideways = MathF.Abs(out_.X) / (post - 0.3f) > MathF.Abs(out_.Y) / (h * 0.36f);
                Vector3 to = sideways ? P(MathF.Sign(out_.X) * post, e0.Y) : P(e0.X, out_.Y > 0f ? h - 0.22f : 0.42f);
                b.Beam(e0, to + Vector3.UnitZ * 0.06f, 0.012f, Twine);
            }
            // The rent, and its stitches.
            Vector3 r0 = P(-0.5f, h * 0.7f, 0.1f), r1 = P(0.2f, h * 0.54f, 0.1f);
            Vector3 across = Vector3.Cross(r1 - r0, face).Normalized();
            Hall.Board(b, r0, r1, across, 0.022f, 0.006f, Hall.Flat(34, 30, 26));
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = Vector3.Lerp(r0, r1, (i + 0.5f) / 6f) + face * 0.008f;
                b.Beam(at - across * 0.08f, at + across * 0.08f, 0.01f, Hall.Flat(206, 196, 168));
            }

            // The scraper on its nail, and the lime.
            Hall.Board(b, P(post, 2.5f, 0.1f), P(post + 0.02f, 2.05f, 0.12f), Vector3.UnitX, 0.14f, 0.01f, Steel);
            b.Tube(P(post - 0.14f, 2.52f, 0.11f), P(post + 0.14f, 2.52f, 0.11f), 0.035f, 0.035f, 6, Handle);
            Vector3 pail = new Vector3(-post + 0.2f, 0f, half.Y + 0.42f);
            b.Tube(pail, pail + Vector3.UnitY * 0.56f, 0.24f, 0.29f, 9, Hall.Wood(130, 96, 62), capped: false);
            b.Tube(pail + Vector3.UnitY * 0.44f, pail + Vector3.UnitY * 0.47f, 0.27f, 0.27f, 9, Hall.Flat(222, 218, 206));
        }

        /// <summary>
        /// The beam a hide is cleaned on: half a trunk, one end on legs and the other on the floor, with a hide thrown over it flesh side up and the two-handled knife lying
        /// where it stopped. What's come off is heaped by its foot.
        /// </summary>
        private static void FleshingBeam(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            Vector3 high = new Vector3(0f, h - 0.3f, -half.Y + 0.2f), low = new Vector3(0f, 0.3f, half.Y - 0.1f);
            b.Tube(low, high, 0.3f, 0.32f, 10, Hall.Timber(196, 166, 124));
            b.Tube(high, low, 0.32f, 0.3f, 10, Hall.Timber(196, 166, 124));
            foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(s * 0.5f, 0f, -half.Y + 0.1f), new Vector3(s * 0.12f, h - 0.5f, -half.Y + 0.3f), Vector3.UnitZ, 0.07f, 0.06f, FrameDark);

            // The hide, over its upper half, hanging either side.
            Vector3 along = (low - high).Normalized();
            Vector3 up = Vector3.Cross(along, Vector3.UnitX).Normalized();
            if (up.Y < 0f) up = -up;
            Vector3 from = high + along * 0.25f + up * 0.33f, to = high + along * 1.7f + up * 0.32f;
            Hall.Board(b, from, to, Vector3.UnitX, 0.3f, 0.012f, Rawhide);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = Vector3.UnitX * s;
                Hall.Face(b, from + side * 0.3f, to + side * 0.3f, to + side * 0.44f - Vector3.UnitY * 0.72f, from + side * 0.4f - Vector3.UnitY * 0.6f, side, s > 0f ? Rawhide : Hall.Pelt(120, 86, 56));
            }
            // The knife, across it.
            Vector3 knife = high + along * 1.15f + up * 0.36f;
            Hall.Board(b, knife - Vector3.UnitX * 0.42f, knife + Vector3.UnitX * 0.42f, along, 0.05f, 0.008f, Steel);
            foreach (float s in new[] { -1f, 1f }) b.Tube(knife + Vector3.UnitX * s * 0.42f, knife + Vector3.UnitX * s * 0.62f + up * 0.04f, 0.035f, 0.035f, 6, Handle);
            // What's come off, and the tub it goes in.
            Vector3 heap = new Vector3(half.X + 0.2f, 0f, 0.3f);
            Blob(b, heap + Vector3.UnitY * 0.06f, new Vector3(0.34f, 0.12f, 0.28f), Hall.Pelt(150, 116, 80), 8, 4);
            Vector3 tub = new Vector3(-half.X - 0.15f, 0f, half.Y - 0.5f);
            b.Tube(tub, tub + Vector3.UnitY * 0.45f, 0.3f, 0.34f, 9, Hall.Wood(124, 90, 60), capped: false);
            b.Tube(tub + Vector3.UnitY * 0.32f, tub + Vector3.UnitY * 0.34f, 0.32f, 0.32f, 9, Hall.Mirror(90, 80, 62));
        }

        /// <summary>
        /// The leather table, under its window: a hide spread on it with the soles of a pair of boots marked out, the round knife that cuts them, awls in their block, the
        /// mallet, thread and wax, rivets in a tin, and one boot of a pair finished on its last with the other only begun. Straps and belts hang from its edge by their
        /// buckles, hides are rolled under it, and what's ready stands on the floor at its end.
        /// </summary>
        private static void LeatherTable(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            HallFurniture.Table(b, half, h, Hall.Timber(142, 102, 66), Walnut);
            float top = h;
            Hall.Board(b, new Vector3(-half.X + 0.3f, 0.52f, 0f), new Vector3(half.X - 0.3f, 0.52f, 0f), Vector3.UnitZ, half.Y - 0.3f, 0.04f, Hall.Timber(120, 86, 56));
            VertexMaterial[] hides = { Tan, DarkHide, Rawhide, Hall.Leather(128, 78, 44) };
            for (int i = 0; i < 5; i++)
            {
                float z = -half.Y + 0.5f + i * 0.34f;
                b.Tube(new Vector3(-half.X + 0.5f, 0.74f, z), new Vector3(half.X - 0.5f - (i % 2) * 0.5f, 0.74f, z), 0.16f, 0.16f, 8, hides[i % hides.Length]);
            }

            // The hide, the soles chalked on it, and one of them cut out and lifted.
            Blot(b, new Vector3(-0.95f, top + 0.012f, 0f), 0.86f, Tan, 91, 1.5f, 0.1f);
            Blot(b, new Vector3(-1.35f, top + 0.02f, 0.1f), 0.19f, Rawhide, 92, 2f, 0.3f);
            Blot(b, new Vector3(-0.75f, top + 0.02f, -0.1f), 0.19f, Hall.Timber(120, 86, 56), 93, 2f, 0.2f);
            Blot(b, new Vector3(-0.2f, top + 0.03f, 0.45f), 0.19f, Rawhide, 94, 2f, -0.6f);
            for (int strip = 0; strip < 3; strip++) Hall.Board(b, new Vector3(-0.2f + strip * 0.04f, top + 0.02f, -0.75f + strip * 0.12f), new Vector3(0.9f, top + 0.02f, -0.7f + strip * 0.13f), Vector3.UnitZ, 0.04f, 0.012f, DarkHide);
            // The round knife: half a moon of steel on a turned handle.
            Vector3 moon = new Vector3(0.12f, top + 0.02f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float a0 = MathF.PI * i / 6f, a1 = MathF.PI * (i + 1) / 6f;
                Hall.Tri(b, moon, moon + new Vector3(MathF.Cos(a0) * 0.24f, 0f, -MathF.Sin(a0) * 0.2f), moon + new Vector3(MathF.Cos(a1) * 0.24f, 0f, -MathF.Sin(a1) * 0.2f), Vector3.UnitY, Bright);
            }
            b.Tube(moon, moon + new Vector3(0f, 0.02f, 0.34f), 0.035f, 0.045f, 6, Handle);

            // Awls stood in a block, the mallet, thread, wax and rivets.
            Vector3 block = new Vector3(0.5f, top, -0.3f);
            b.Box(block + Vector3.UnitY * 0.07f, new Vector3(0.24f, 0.07f, 0.1f), Oak);
            for (int i = 0; i < 4; i++)
            {
                Vector3 awl = block + new Vector3(-0.17f + i * 0.11f, 0.14f, 0f);
                b.Beam(awl, awl + Vector3.UnitY * 0.2f, 0.01f, Steel);
                b.Tube(awl + Vector3.UnitY * 0.2f, awl + Vector3.UnitY * 0.36f, 0.035f, 0.045f, 6, Handle);
            }
            b.Tube(new Vector3(0.95f, top + 0.13f, 0.3f), new Vector3(1.25f, top + 0.13f, 0.38f), 0.13f, 0.13f, 8, Rawhide);
            b.Tube(new Vector3(1.1f, top + 0.13f, 0.34f), new Vector3(1f, top + 0.06f, 0.82f), 0.03f, 0.035f, 6, Handle);
            b.Tube(new Vector3(0.95f, top, -0.55f), new Vector3(0.95f, top + 0.22f, -0.55f), 0.09f, 0.09f, 8, Hall.Flat(206, 196, 168));
            foreach (float y in new[] { 0f, 0.22f }) b.Tube(new Vector3(0.95f, top + y, -0.55f), new Vector3(0.95f, top + y + 0.02f, -0.55f), 0.13f, 0.13f, 8, Handle);
            b.Box(new Vector3(-1.95f, top + 0.05f, 0.7f), new Vector3(0.14f, 0.05f, 0.1f), Hall.Wax);
            b.Tube(new Vector3(1.25f, top, -0.15f), new Vector3(1.25f, top + 0.06f, -0.15f), 0.13f, 0.13f, 9, Hall.Pewter);
            for (int i = 0; i < 6; i++) b.Box(new Vector3(1.25f + random.Range(-0.08f, 0.08f), top + 0.07f, -0.15f + random.Range(-0.08f, 0.08f)), new Vector3(0.018f, 0.012f, 0.018f), Bright);

            // The pair: one on its last, done, and the other only a sole and a vamp yet, with a belt coiled by them.
            Boot(b, new Vector3(1.85f, top, -0.35f), 0.4f, Tan);
            Blot(b, new Vector3(2.15f, top + 0.014f, 0.3f), 0.17f, Hall.Leather(60, 40, 28), 95, 2f, 0.9f);
            b.Box(new Vector3(2.12f, top + 0.08f, 0.36f), new Vector3(0.1f, 0.07f, 0.16f), Tan);
            for (int turn = 0; turn < 3; turn++) b.Tube(new Vector3(1.55f, top + 0.01f + turn * 0.045f, 0.62f), new Vector3(1.55f, top + 0.05f + turn * 0.045f, 0.62f), 0.2f - turn * 0.035f, 0.2f - turn * 0.035f, 10, DarkHide, capped: false);
            b.Box(new Vector3(1.76f, top + 0.03f, 0.62f), new Vector3(0.045f, 0.03f, 0.06f), Steel);
            DishCandle(b, new Vector3(-2.15f, top, -0.7f), lights, 0.34f, 18f);

            // Belts and straps, hung off the edge by their buckles.
            for (int i = 0; i < 7; i++)
            {
                float x = -1.9f + i * 0.42f + random.Range(-0.05f, 0.05f);
                float length = random.Range(0.7f, 1.25f);
                Vector3 pin = new Vector3(x, top - 0.16f, half.Y + 0.03f);
                b.Box(pin, new Vector3(0.055f, 0.045f, 0.02f), Steel);
                Hall.Board(b, pin, pin - Vector3.UnitY * length, Vector3.UnitX, 0.04f, 0.012f, i % 3 == 0 ? DarkHide : i % 3 == 1 ? Tan : Hall.Leather(128, 78, 44));
            }
            // What's ready, on the floor at the end of the table: two pairs.
            foreach ((float x, float z, bool dark) in new[] { (0.3f, -0.5f, false), (0.58f, -0.44f, false), (0.34f, 0.3f, true), (0.62f, 0.38f, true) })
            {
                Boot(b, new Vector3(half.X + x, 0f, z), 0.25f, dark ? DarkHide : Tan);
            }
        }

        // A saddle on its horse: a blanket in the Haven's teal under it, its seat and skirts, the stirrups hung, the girth looped up over it.
        private static void SaddleStand(MeshBuilder b, float r, float h)
        {
            float ridge = h - 0.9f;
            b.Tube(new Vector3(-0.72f, ridge, 0f), new Vector3(0.72f, ridge, 0f), 0.16f, 0.16f, 8, Oak);
            b.Tube(new Vector3(0.72f, ridge, 0f), new Vector3(-0.72f, ridge, 0f), 0.16f, 0.16f, 8, Oak);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                foreach (float s in new[] { -1f, 1f }) Hall.Board(b, new Vector3(x, 0f, s * 0.55f), new Vector3(x, ridge - 0.05f, s * 0.08f), Vector3.UnitX, 0.06f, 0.05f, FrameDark);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 side = Vector3.UnitZ * s;
                // The blanket, then the skirt over it, each hanging from the ridge.
                Hall.Face(b, new Vector3(-0.56f, ridge + 0.17f, 0f), new Vector3(0.56f, ridge + 0.17f, 0f), new Vector3(0.56f, ridge - 0.62f, s * 0.6f), new Vector3(-0.56f, ridge - 0.62f, s * 0.6f), side + Vector3.UnitY, Dyed(Teal));
                Hall.Face(b, new Vector3(-0.56f, ridge - 0.62f, s * 0.605f), new Vector3(0.56f, ridge - 0.62f, s * 0.605f), new Vector3(0.56f, ridge - 0.74f, s * 0.62f), new Vector3(-0.56f, ridge - 0.74f, s * 0.62f), side, Dyed(Saffron));
                Hall.Face(b, new Vector3(-0.36f, ridge + 0.21f, s * 0.04f), new Vector3(0.36f, ridge + 0.21f, s * 0.04f), new Vector3(0.3f, ridge - 0.42f, s * 0.53f), new Vector3(-0.3f, ridge - 0.42f, s * 0.53f), side + Vector3.UnitY, DarkHide);
                // A stirrup on its leather.
                Vector3 hung = new Vector3(0.06f, ridge - 0.1f, s * 0.36f), iron = new Vector3(0.06f, ridge - 0.95f, s * 0.6f);
                b.Beam(hung, iron, 0.018f, Tan);
                b.Beam(iron + new Vector3(-0.12f, -0.22f, 0f), iron + new Vector3(0.12f, -0.22f, 0f), 0.022f, Steel);
                b.Beam(iron, iron + new Vector3(-0.12f, -0.22f, 0f), 0.018f, Steel);
                b.Beam(iron, iron + new Vector3(0.12f, -0.22f, 0f), 0.018f, Steel);
            }
            // The seat, its pommel and its cantle.
            b.Tube(new Vector3(-0.36f, ridge + 0.26f, 0f), new Vector3(0.36f, ridge + 0.26f, 0f), 0.19f, 0.17f, 8, Tan, capped: false);
            Blob(b, new Vector3(0.42f, ridge + 0.42f, 0f), new Vector3(0.1f, 0.17f, 0.17f), Tan, 8, 5);
            Blob(b, new Vector3(-0.42f, ridge + 0.42f, 0f), new Vector3(0.09f, 0.2f, 0.26f), Tan, 8, 5);
            b.Beam(new Vector3(-0.1f, ridge + 0.46f, -0.2f), new Vector3(0.05f, ridge + 0.48f, 0.24f), 0.022f, DarkHide);
        }

        // ---- Cloth.

        /// <summary>
        /// The loom, leaned against the wall by its window: two uprights, the beam the cloth is wound on, a length of the Haven's teal part woven, and the warp hanging under
        /// it, every thread weighted with a ring of fired clay. The sword that beats the weft up is left in the shed, and the weaver's bench has the shuttle on it and a
        /// basket of yarn beside it. It stands along x, its face towards +z.
        /// </summary>
        private static void Loom(MeshBuilder b, Vector2 half, float h, WorldRandom random)
        {
            float Lean(float y) => 0.5f - 1.4f * y / h;
            Vector3 P(float x, float y, float out_ = 0f) => new Vector3(x, y, Lean(y) + out_);
            float post = half.X - 0.3f;
            foreach (float s in new[] { -1f, 1f })
            {
                Hall.Board(b, P(s * post, 0f), P(s * post, h), Vector3.UnitX, 0.09f, 0.08f, Frame);
                b.Beam(P(s * post, 2.02f), P(s * post, 2.06f, 0.36f), 0.035f, Frame);
            }
            b.Tube(P(-post - 0.2f, h - 0.28f, 0.06f), P(post + 0.2f, h - 0.28f, 0.06f), 0.12f, 0.12f, 8, Oak);
            b.Tube(P(post + 0.2f, h - 0.28f, 0.06f), P(-post - 0.2f, h - 0.28f, 0.06f), 0.12f, 0.12f, 8, Oak);
            b.Beam(P(post + 0.14f, h - 0.5f, 0.06f), P(post + 0.14f, h - 0.02f, 0.2f), 0.022f, Handle);
            b.Tube(P(-post, 2.06f, 0.36f), P(post, 2.06f, 0.36f), 0.035f, 0.035f, 6, Pale);
            b.Tube(P(-post, 1.25f, 0.03f), P(post, 1.25f, 0.03f), 0.03f, 0.03f, 6, Pale);

            // What's woven: teal, with a band of saffron and one of cream, down to where the weft stops.
            float weave = post - 0.22f, fell = 2.75f;
            Vector3 face = new Vector3(0f, 1.4f / h, 1f).Normalized();
            Hall.Face(b, P(-weave, fell, 0.1f), P(weave, fell, 0.1f), P(weave, h - 0.3f, 0.12f), P(-weave, h - 0.3f, 0.12f), face, Dyed(Teal));
            foreach ((float y, float tall, Vector3 colour) in new[] { (3.5f, 0.16f, Saffron), (3.22f, 0.05f, Cream), (3.78f, 0.05f, Cream) })
            {
                Hall.Face(b, P(-weave, y, 0.112f), P(weave, y, 0.112f), P(weave, y + tall, 0.114f), P(-weave, y + tall, 0.114f), face, Dyed(colour));
            }
            // The warp, and its weights.
            const int Threads = 22;
            for (int i = 0; i < Threads; i++)
            {
                float x = -weave + weave * 2f * (i + 0.5f) / Threads;
                float end = 0.78f + (i % 2) * 0.1f;
                b.Beam(P(x, fell, 0.1f), i % 2 == 0 ? P(x, end, 0.06f) : P(x, end, 0.3f), 0.009f, Hall.Flat(214, 204, 178));
                if (i % 2 == 1) continue;
                Vector3 weight = P(x + weave / Threads, end - 0.08f, 0.16f);
                b.Tube(weight - Vector3.UnitZ * 0.03f, weight + Vector3.UnitZ * 0.03f, 0.085f, 0.085f, 7, Stoneware);
            }
            Hall.Board(b, P(-weave * 0.7f, fell - 0.06f, 0.13f), P(weave * 0.85f, fell - 0.02f, 0.13f), Vector3.UnitY, 0.05f, 0.012f, Pale);

            // The bench, the shuttle, and the yarn.
            Hall.Board(b, new Vector3(-0.8f, 1.02f, half.Y - 0.26f), new Vector3(0.8f, 1.02f, half.Y - 0.26f), Vector3.UnitZ, 0.22f, 0.045f, Oak);
            foreach (float x in new[] { -0.66f, 0.66f }) b.Box(new Vector3(x, 0.5f, half.Y - 0.26f), new Vector3(0.05f, 0.5f, 0.18f), Oak);
            Hall.Board(b, new Vector3(-0.2f, 1.09f, half.Y - 0.3f), new Vector3(0.3f, 1.09f, half.Y - 0.2f), Vector3.UnitZ, 0.05f, 0.025f, Pale);
            b.Tube(new Vector3(-0.06f, 1.1f, half.Y - 0.28f), new Vector3(0.16f, 1.1f, half.Y - 0.23f), 0.035f, 0.035f, 6, Dyed(Saffron));
            Vector3 basket = new Vector3(-post + 0.1f, 0f, half.Y - 0.2f);
            b.Tube(basket, basket + Vector3.UnitY * 0.5f, 0.34f, 0.42f, 10, Hall.Timber(160, 124, 78), capped: false);
            Vector3[] yarns = { Teal, Saffron, Cream, Blossom, Blue, Green };
            for (int i = 0; i < 7; i++)
            {
                Vector3 at = basket + new Vector3(random.Range(-0.2f, 0.2f), 0.42f + random.Range(0f, 0.2f), random.Range(-0.2f, 0.2f));
                Blob(b, at, new Vector3(0.14f), Dyed(yarns[i % yarns.Length]), 7, 4);
            }
            Blob(b, basket + new Vector3(0.62f, 0.13f, 0.1f), new Vector3(0.13f), Dyed(Teal), 7, 4);
        }

        /// <summary>
        /// The tailor's table: one of the Haven's banners laid out on it, teal, with three of its blossom's five petals sewn on and the other two only chalked, and its end
        /// hanging over the edge towards the door; the shears, the pins, thread of every colour the Haven dyes, the rod to measure by, buttons of bone, a coat's quilted
        /// pieces cut and stacked, and a lamp. A string of pennants is half made along its edge, and the floor under it is all snippings.
        /// </summary>
        private static void TailorTable(MeshBuilder b, Vector2 half, float h, List<AssetLight> lights, WorldRandom random)
        {
            HallFurniture.Table(b, half, h, Hall.Timber(164, 124, 82), Oak);
            float top = h;
            // The banner.
            Vector3 banner = new Vector3(-0.55f, top, 0f);
            b.Box(banner + Vector3.UnitY * 0.012f, new Vector3(1.1f, 0.012f, 0.76f), Dyed(Teal));
            Hall.Board(b, banner + new Vector3(0f, 0.01f, half.Y + 0.014f), banner + new Vector3(0f, -0.62f, half.Y + 0.03f), Vector3.UnitX, 1.1f, 0.012f, Dyed(Teal));
            Hall.Board(b, banner + new Vector3(0f, -0.5f, half.Y + 0.045f), banner + new Vector3(0f, -0.62f, half.Y + 0.045f), Vector3.UnitX, 1.1f, 0.006f, Dyed(Saffron));
            b.Box(banner + new Vector3(0f, 0.03f, -0.66f), new Vector3(1.1f, 0.006f, 0.06f), Dyed(Saffron));
            Vector3 heart = banner + Vector3.UnitY * 0.03f;
            for (int petal = 0; petal < 5; petal++)
            {
                float a = MathF.Tau * petal / 5f + 0.3f;
                Vector3 at = heart + new Vector3(MathF.Cos(a) * 0.3f, 0f, MathF.Sin(a) * 0.3f);
                if (petal < 3) Blot(b, at + Vector3.UnitY * 0.004f, 0.17f, Dyed(Blossom), 100 + petal);
                else
                {
                    // Only its outline yet, in chalk.
                    for (int k = 0; k < 8; k++)
                    {
                        float t0 = MathF.Tau * k / 8f, t1 = MathF.Tau * (k + 1) / 8f;
                        Hall.Board(b, at + new Vector3(MathF.Cos(t0), 0f, MathF.Sin(t0)) * 0.16f, at + new Vector3(MathF.Cos(t1), 0f, MathF.Sin(t1)) * 0.16f, Vector3.UnitZ, 0.012f, 0.003f, Chalk);
                    }
                }
            }
            Blot(b, heart + Vector3.UnitY * 0.008f, 0.09f, Dyed(Saffron), 110);
            b.Beam(heart + new Vector3(0.2f, 0.02f, 0.1f), heart + new Vector3(0.42f, 0.05f, 0.02f), 0.008f, Bright);
            b.Beam(heart + new Vector3(0.42f, 0.05f, 0.02f), new Vector3(0.95f, top + 0.1f, -0.68f), 0.005f, Dyed(Blossom));

            // Shears, pins, thread, the rod, chalk, buttons.
            Vector3 shears = new Vector3(0.82f, top + 0.02f, 0.5f);
            Hall.Board(b, shears + new Vector3(-0.34f, 0f, -0.06f), shears + new Vector3(0.2f, 0f, 0.05f), Vector3.UnitZ, 0.035f, 0.008f, Bright);
            Hall.Board(b, shears + new Vector3(-0.34f, 0.01f, 0.07f), shears + new Vector3(0.2f, 0.01f, -0.04f), Vector3.UnitZ, 0.035f, 0.008f, Bright);
            foreach (float s in new[] { -1f, 1f }) b.Tube(shears + new Vector3(0.28f, 0f, s * 0.08f), shears + new Vector3(0.28f, 0.02f, s * 0.08f), 0.07f, 0.07f, 8, DarkSteel, capped: false);
            Vector3 cushion = new Vector3(1.3f, top + 0.07f, 0.62f);
            Blob(b, cushion, new Vector3(0.12f, 0.08f, 0.12f), Dyed(Saffron), 8, 4);
            for (int pin = 0; pin < 6; pin++) b.Beam(cushion + new Vector3(random.Range(-0.07f, 0.07f), 0.04f, random.Range(-0.07f, 0.07f)), cushion + new Vector3(random.Range(-0.1f, 0.1f), 0.2f, random.Range(-0.1f, 0.1f)), 0.006f, Bright);
            for (int spool = 0; spool < 6; spool++)
            {
                Vector3 at = new Vector3(0.75f + spool * 0.19f, top, -0.72f + (spool % 2) * 0.1f);
                b.Tube(at, at + Vector3.UnitY * 0.16f, 0.065f, 0.065f, 7, Dyed(Festive[spool % Festive.Length]));
                b.Tube(at + Vector3.UnitY * 0.16f, at + Vector3.UnitY * 0.18f, 0.08f, 0.08f, 7, Handle);
            }
            Hall.Board(b, new Vector3(-1.75f, top + 0.015f, -0.86f), new Vector3(0.4f, top + 0.015f, -0.86f), Vector3.UnitZ, 0.035f, 0.012f, Pale);
            for (int tick = 0; tick < 9; tick++) b.Box(new Vector3(-1.6f + tick * 0.23f, top + 0.03f, -0.86f), new Vector3(0.006f, 0.002f, tick % 3 == 0 ? 0.03f : 0.016f), Hall.Ink);
            b.Box(new Vector3(0.62f, top + 0.02f, 0.18f), new Vector3(0.06f, 0.015f, 0.025f), Chalk);
            for (int button = 0; button < 9; button++)
            {
                Vector3 at = new Vector3(1.55f + random.Range(-0.2f, 0.2f), top, 0.2f + random.Range(-0.14f, 0.14f));
                b.Tube(at, at + Vector3.UnitY * 0.015f, 0.035f, 0.035f, 6, Hall.Bone(226, 214, 190));
            }
            // A coat's pieces, cut and stacked, and the lamp.
            for (int piece = 0; piece < 4; piece++)
            {
                Hall.At(b, new Vector3(1.45f, top + 0.025f + piece * 0.05f, -0.2f), 0.1f * piece - 0.15f, () => b.Box(Vector3.Zero, new Vector3(0.34f - piece * 0.03f, 0.025f, 0.26f), Quilt));
            }
            Lantern(b, new Vector3(-1.62f, top + 0.36f, -0.6f), lights, 26f, 11f, shadow: false);

            // Pennants, as far as they've got, off the edge of the table.
            Bunting(b, new Vector3(0.62f, top - 0.14f, half.Y + 0.05f), new Vector3(1.86f, top - 0.14f, half.Y + 0.05f), 0.05f, 5, 1, 0.36f);
            // Snippings.
            for (int i = 0; i < 16; i++)
            {
                Vector3 at = new Vector3(random.Range(-half.X - 0.3f, half.X + 0.3f), 0.012f, half.Y + random.Range(0.1f, 1.1f));
                Blot(b, at, random.Range(0.05f, 0.11f), Dyed(Festive[i % Festive.Length]), 120 + i, random.Range(1f, 2.4f), random.Range(0f, MathF.PI));
            }
        }

        /// <summary>
        /// A coat on its form: a padded body of sacking on a turned stand, as tall as whoever it's for, wearing the quilted coat that goes under mail. One sleeve is on. The
        /// other is pinned at the shoulder and hangs by its pins, and the tape it was measured with is still round the form's neck.
        /// </summary>
        private static void DressForm(MeshBuilder b, float h)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = MathF.Tau * i / 3f + 0.3f;
                b.Beam(new Vector3(MathF.Cos(a) * 0.5f, 0f, MathF.Sin(a) * 0.5f), new Vector3(0f, 0.5f, 0f), 0.045f, Walnut);
            }
            Hall.Turned(b, new Vector3(0f, 0.45f, 0f), Walnut, 8, (0f, 0.12f), (0.2f, 0.16f), (0.36f, 0.07f), (1f, 0.065f), (1.1f, 0.12f), (1.18f, 0.08f));
            float hip = 1.6f;
            (float y, float r)[] body = { (hip, 0.44f), (hip + 0.5f, 0.47f), (hip + 1.05f, 0.37f), (hip + 1.8f, 0.5f), (hip + 2.15f, 0.34f), (hip + 2.3f, 0.15f) };
            for (int i = 0; i + 1 < body.Length; i++)
            {
                bool coat = i < 4;
                b.Tube(new Vector3(0f, body[i].y, 0f), new Vector3(0f, body[i + 1].y, 0f), body[i].r + (coat ? 0.03f : 0f), body[i + 1].r + (coat && i < 3 ? 0.03f : 0f), 12, coat ? Quilt : Sacking, capped: false);
            }
            b.Tube(new Vector3(0f, hip + 2.3f, 0f), new Vector3(0f, hip + 2.55f, 0f), 0.15f, 0.13f, 8, Sacking);
            b.Tube(new Vector3(0f, hip + 2.55f, 0f), new Vector3(0f, h, 0f), 0.06f, 0.1f, 7, Walnut);
            b.Tube(new Vector3(0f, hip - 0.02f, 0f), new Vector3(0f, hip + 0.14f, 0f), 0.48f, 0.5f, 12, Dyed(Saffron), capped: false);
            // The sleeve that's on, and the one that isn't.
            b.Tube(new Vector3(-0.46f, hip + 1.95f, 0f), new Vector3(-0.7f, hip + 0.7f, 0.1f), 0.2f, 0.15f, 8, Quilt);
            b.Tube(new Vector3(0.5f, hip + 1.86f, 0.04f), new Vector3(0.6f, hip + 0.62f, 0.22f), 0.19f, 0.15f, 8, Hall.Quilted(196, 186, 160));
            for (int pin = 0; pin < 4; pin++)
            {
                float a = pin * 0.7f - 1f;
                Vector3 at = new Vector3(0.44f + MathF.Cos(a) * 0.08f, hip + 1.98f + MathF.Sin(a) * 0.1f, 0.1f + pin * 0.04f);
                b.Beam(at, at + new Vector3(0.1f, 0.08f, 0.1f), 0.007f, Bright);
            }
            // The fastenings down its front, and the tape.
            for (int i = 0; i < 5; i++) b.Tube(new Vector3(0f, hip + 0.4f + i * 0.34f, body[1].r - i * 0.018f + 0.05f), new Vector3(0f, hip + 0.4f + i * 0.34f, body[1].r - i * 0.018f + 0.08f), 0.035f, 0.035f, 6, Hall.Bone(226, 214, 190));
            foreach (float s in new[] { -1f, 1f })
            {
                Hall.Board(b, new Vector3(s * 0.14f, hip + 2.3f, 0.12f), new Vector3(s * 0.22f, hip + 1.2f - (s > 0f ? 0.3f : 0f), 0.52f), Vector3.UnitX, 0.025f, 0.006f, Hall.Flat(214, 204, 178));
            }
        }

        /// <summary>
        /// The cloth, in bolts on a low rack inside the door, every colour the Haven dyes and a few it doesn't, one with its end pulled out to be looked at. The Forge's cat
        /// sleeps on it, where the sun comes in.
        /// </summary>
        private static void ClothRack(MeshBuilder b, Vector2 half, WorldRandom random)
        {
            foreach (float x in new[] { -half.X + 0.05f, 0f, half.X - 0.05f })
            {
                foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x, 0.72f, s * (half.Y - 0.05f)), new Vector3(0.05f, 0.72f, 0.05f), FrameDark);
                Hall.Board(b, new Vector3(x, 1.4f, -half.Y), new Vector3(x, 1.4f, half.Y), Vector3.UnitY, 0.05f, 0.05f, FrameDark);
            }
            Hall.Board(b, new Vector3(-half.X, 0.22f, 0f), new Vector3(half.X, 0.22f, 0f), Vector3.UnitZ, half.Y - 0.02f, 0.035f, Weathered(random));
            Vector3[] undyed = { new(206, 198, 176), new(150, 140, 122), new(96, 84, 70) };
            int count = 0;
            float highest = 0f;
            for (int column = 0; column < 2; column++)
            {
                float middle = (column == 0 ? -1f : 1f) * half.X * 0.5f;
                int layers = column == 0 ? 3 : 2;
                for (int layer = 0; layer < layers; layer++)
                {
                    for (int side = 0; side < 2; side++)
                    {
                        float radius = random.Range(0.17f, 0.2f);
                        Vector3 at = new Vector3(middle + random.Range(-0.04f, 0.04f), 0.26f + 0.19f + layer * 0.37f, (side - 0.5f) * 0.42f);
                        Vector3 colour = count % 4 == 3 ? undyed[count / 4 % undyed.Length] : Festive[count % Festive.Length];
                        float reach = half.X * 0.5f - 0.14f;
                        b.Tube(at - Vector3.UnitX * reach, at + Vector3.UnitX * reach, radius, radius, 9, Dyed(colour));
                        b.Tube(at + Vector3.UnitX * reach, at - Vector3.UnitX * reach, radius, radius, 9, Dyed(colour));
                        b.Tube(at - Vector3.UnitX * (reach + 0.07f), at + Vector3.UnitX * (reach + 0.07f), 0.045f, 0.045f, 6, Pale);
                        highest = column == 0 ? MathF.Max(highest, at.Y + radius) : highest;
                        count++;
                    }
                }
            }
            // One with its end drawn out over the front of the rack.
            Vector3 loose = new Vector3(half.X * 0.5f, 0.26f + 0.19f + 0.37f + 0.19f, 0.21f);
            Hall.Board(b, loose + new Vector3(0f, 0f, 0.05f), loose + new Vector3(0f, -0.1f, half.Y - 0.16f), Vector3.UnitX, 0.5f, 0.01f, Dyed(Festive[3]));
            Hall.Board(b, loose + new Vector3(0f, -0.1f, half.Y - 0.16f), loose + new Vector3(0f, -0.75f, half.Y - 0.08f), Vector3.UnitX, 0.5f, 0.01f, Dyed(Festive[3]));

            // The cat: curled, its nose under its tail, on the highest of them.
            VertexMaterial ginger = Hall.Pelt(198, 126, 58);
            Vector3 cat = new Vector3(-half.X * 0.5f, highest + 0.15f, 0f);
            Blob(b, cat, new Vector3(0.42f, 0.18f, 0.3f), ginger, 10, 6);
            Vector3 head = cat + new Vector3(0.3f, 0.03f, 0.2f);
            Blob(b, head, new Vector3(0.15f, 0.13f, 0.14f), ginger, 8, 5);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 ear = head + new Vector3(s * 0.08f, 0.1f, -0.02f);
                Hall.Tri(b, ear + new Vector3(-0.045f, 0f, 0f), ear + new Vector3(0.045f, 0f, 0f), ear + new Vector3(0f, 0.11f, 0f), Vector3.UnitZ, ginger);
                Hall.Tri(b, ear + new Vector3(-0.045f, 0f, -0.004f), ear + new Vector3(0.045f, 0f, -0.004f), ear + new Vector3(0f, 0.11f, -0.004f), -Vector3.UnitZ, ginger);
            }
            b.Tube(cat + new Vector3(-0.4f, -0.04f, 0.02f), cat + new Vector3(-0.3f, -0.08f, 0.3f), 0.055f, 0.05f, 6, ginger, capped: false);
            b.Tube(cat + new Vector3(-0.3f, -0.08f, 0.3f), cat + new Vector3(0.1f, -0.08f, 0.36f), 0.05f, 0.045f, 6, ginger, capped: false);
            b.Tube(cat + new Vector3(0.1f, -0.08f, 0.36f), cat + new Vector3(0.34f, -0.05f, 0.3f), 0.045f, 0.03f, 6, Hall.Pelt(230, 220, 200));
        }

        /// <summary>
        /// The great wheel that the thread for the loom is spun on: a bench on three legs, the wheel at one end and the spindle at the other with a cord between them, the
        /// distaff with its wool, and a basket of fleece washed and waiting.
        /// </summary>
        private static void SpinningWheel(MeshBuilder b, float h)
        {
            VertexMaterial wood = Hall.Timber(150, 110, 70);
            Hall.Board(b, new Vector3(-0.7f, 0.92f, 0f), new Vector3(0.7f, 1f, 0f), Vector3.UnitZ, 0.17f, 0.05f, wood);
            foreach ((float x, float z) in new[] { (-0.55f, -0.3f), (-0.55f, 0.3f), (0.6f, 0f) }) b.Beam(new Vector3(x * 1.15f, 0f, z * 1.4f), new Vector3(x, 0.92f, z * 0.3f), 0.045f, wood);
            Vector3 hub = new Vector3(-0.28f, h - 0.86f, 0f);
            foreach (float s in new[] { -1f, 1f }) b.Beam(new Vector3(-0.28f, 0.95f, s * 0.13f), hub + Vector3.UnitZ * s * 0.13f, 0.04f, wood);
            CartWheel(b, hub, Vector3.UnitZ, 0.8f, wood, 8, tyre: false);
            // The spindle on its post, and the cord that turns it.
            Vector3 spindle = new Vector3(0.58f, 1.62f, 0f);
            b.Beam(new Vector3(0.58f, 0.98f, 0f), spindle, 0.04f, wood);
            b.Tube(spindle - Vector3.UnitZ * 0.26f, spindle + Vector3.UnitZ * 0.3f, 0.016f, 0.012f, 5, DarkSteel);
            b.Tube(spindle - Vector3.UnitZ * 0.02f, spindle + Vector3.UnitZ * 0.2f, 0.07f, 0.02f, 7, Hall.Flat(222, 214, 196));
            b.Beam(hub + new Vector3(0f, 0.8f, -0.1f), spindle - Vector3.UnitZ * 0.2f, 0.006f, Twine);
            b.Beam(hub + new Vector3(0f, -0.8f, -0.1f), spindle - Vector3.UnitZ * 0.2f, 0.006f, Twine);
            // The distaff, with the wool on it, and the thread drawn off it to the spindle.
            Vector3 distaff = new Vector3(0.62f, h - 0.1f, -0.22f);
            b.Tube(new Vector3(0.5f, 1f, -0.12f), distaff, 0.025f, 0.02f, 5, wood);
            Blob(b, distaff - Vector3.UnitY * 0.26f, new Vector3(0.18f, 0.3f, 0.18f), Fleece, 8, 5);
            b.Beam(distaff - Vector3.UnitY * 0.5f, spindle + Vector3.UnitZ * 0.2f, 0.005f, Hall.Flat(222, 214, 196));
            // The fleece, in its basket.
            Vector3 basket = new Vector3(0.25f, 0f, 0.62f);
            b.Tube(basket, basket + Vector3.UnitY * 0.42f, 0.32f, 0.4f, 10, Hall.Timber(160, 124, 78), capped: false);
            Blob(b, basket + Vector3.UnitY * 0.44f, new Vector3(0.36f, 0.2f, 0.34f), Fleece, 9, 5);
            Blob(b, basket + new Vector3(0.14f, 0.58f, -0.08f), new Vector3(0.2f, 0.14f, 0.2f), Fleece, 8, 4);
        }
    }
}
