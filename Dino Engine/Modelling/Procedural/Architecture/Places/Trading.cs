using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A trading post (PLACES.md), where the roads met and the land's merchants with them, which the turncoats hold now and sell to the Reich from: the post's beam balance with its pans and its
    /// weights; bales of wool and cloth roped and marked; the turncoats' tally desk under an awning, its ledger open and the Reich's oxblood seal on its papers; the rail the beasts are tied to
    /// with its trough; and the board of notices under its little roof, the Reich's among them. Headless.
    /// </summary>
    internal static class Trading
    {
        /// <summary>The post's beam balance: a post on a cross of feet, the beam across its top, a pan hung from each end on chains, one weighed down, and the weights stacked by it.</summary>
        public static void Scales(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            // Its pans are copper, the land's, as brass is the Reich's.
            VertexMaterial wood = Timber(random), iron = LandIron(random), brass = new VertexMaterial(Textures.MaterialCatalog.Iron.Index, new Colour(0.5f, 0.27f, 0.17f));
            const float High = 2.6f, Arm = 1.1f;
            Kit.Plank(b, new Vector3(-0.8f, Ground + 0.05f, 0f), new Vector3(0.8f, Ground + 0.05f, 0f), Vector3.UnitY, 0.2f, 0.16f, wood);
            Kit.Plank(b, new Vector3(0f, Ground + 0.05f, -0.8f), new Vector3(0f, Ground + 0.05f, 0.8f), Vector3.UnitY, 0.2f, 0.16f, wood);
            Kit.Plank(b, new Vector3(0f, Ground, 0f), new Vector3(0f, High, 0f), Vector3.UnitX, 0.16f, 0.16f, wood);
            float tilt = random.Range(0.08f, 0.18f) * (random.Chance(0.5f) ? 1f : -1f);
            Vector3 left = new Vector3(-Arm, High + MathF.Sin(tilt) * Arm, 0f), right = new Vector3(Arm, High - MathF.Sin(tilt) * Arm, 0f);
            Kit.Pole(b, left, right, 0.05f, iron);
            foreach ((Vector3 end, bool loaded) in new[] { (left, tilt < 0f), (right, tilt > 0f) })
            {
                Vector3 pan = end - Vector3.UnitY * 1.25f;
                foreach (float a in new[] { 0f, 2.1f, 4.2f }) Kit.Pole(b, end, pan + new Vector3(MathF.Cos(a) * 0.32f, 0.05f, MathF.Sin(a) * 0.32f), 0.012f, iron);
                Kit.Lathe(b, pan - Vector3.UnitY * 0.06f, Vector3.UnitY, 0.12f, Kit.Profile((0f, 0.18f), (1f, 0.38f)), 12, 1, brass);
                if (loaded) Kit.Sack(b, pan + Vector3.UnitY * 0.05f, random.Float() * MathF.Tau, false, 0.45f, Canvas(random));
            }
            // The weights, stacked on a block by its foot.
            Kit.Span(b, new Vector3(0.5f, Ground - 0.05f, 0.4f), new Vector3(1f, Ground + 0.35f, 0.9f), 0.02f, wood);
            for (int k = 0; k < 4; k++) Kit.Lathe(b, new Vector3(0.75f, Ground + 0.35f + k * 0.07f, 0.65f), Vector3.UnitY, 0.07f, Kit.Profile((0f, 0.16f - k * 0.03f), (1f, 0.15f - k * 0.03f)), 10, 1, iron);
        }

        /// <summary>Bales of wool and cloth, wrapped in sacking, roped twice round, each with its merchant's mark daubed on it, in a heap of two rows. Numbers: count.</summary>
        public static void Bales(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 2, 10);
            VertexMaterial rope = Rope(random);
            int bottom = (count + 1) / 2;
            int placed = 0;
            for (int row = 0; placed < count; row++)
            {
                int inRow = Math.Min(bottom - row, count - placed);
                if (inRow <= 0) break;
                for (int k = 0; k < inRow; k++, placed++)
                {
                    Vector3 half = new Vector3(random.Range(0.55f, 0.7f), random.Range(0.38f, 0.48f), random.Range(0.42f, 0.52f));
                    float x = (k - (inRow - 1) * 0.5f) * 1.35f;
                    Vector3 centre = new Vector3(x, Ground + half.Y + row * 0.86f - 0.04f, random.Range(-0.08f, 0.08f));
                    float yaw = random.Range(-0.12f, 0.12f);
                    Vector3 cloth = Vector3.Lerp(new Vector3(0.56f, 0.5f, 0.38f), new Vector3(0.44f, 0.4f, 0.32f), random.Float());
                    Kit.Block(b, centre, half, yaw, 0.12f, new VertexMaterial(Textures.MaterialCatalog.Cloth.Index, new Colour(cloth)));
                    float c = MathF.Cos(yaw), s = MathF.Sin(yaw);
                    foreach (float along in new[] { -0.45f, 0.45f })
                    {
                        Vector3 o = centre + new Vector3(along * half.X * c, 0f, -along * half.X * s);
                        b.Tube(o + new Vector3(-0.02f * c, 0f, 0.02f * s), o + new Vector3(0.02f * c, 0f, -0.02f * s), half.Y + 0.02f, half.Y + 0.02f, 8, rope, capped: false);
                    }
                    // A merchant's mark on its front: a stroke of the land's own colours, never the Reich's.
                    Vector3 front = centre + new Vector3(s * (half.Z + 0.01f), 0f, c * (half.Z + 0.01f));
                    Vector3 across = new Vector3(c, 0f, -s) * 0.2f;
                    Kit.Face(b, front - across + Vector3.UnitY * 0.08f, front + across + Vector3.UnitY * 0.08f, front + across - Vector3.UnitY * 0.08f, front - across - Vector3.UnitY * 0.08f, new Vector3(s, 0f, c), Painted(BoatPaints[random.Int(BoatPaints.Length)]));
                }
            }
        }

        /// <summary>The turncoats' tally desk under an awning on poles: the ledger open on it, an inkpot and a pile of coin, papers held down by a stone with the Reich's oxblood seal on them, and a stool.</summary>
        public static void Ledger(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), dark = Timber(random, 0.7f), cloth = Canvas(random, 0.9f), paper = Plaster(1.05f);
            const float Top = 0.95f;
            Kit.Span(b, new Vector3(-1f, Top - 0.08f, -0.45f), new Vector3(1f, Top, 0.45f), 0.02f, wood);
            foreach (float x in new[] { -0.85f, 0.85f })
            {
                foreach (float z in new[] { -0.35f, 0.35f }) Kit.Span(b, new Vector3(x - 0.05f, -0.05f, z - 0.05f), new Vector3(x + 0.05f, Top - 0.08f, z + 0.05f), 0.005f, dark);
            }
            // The ledger open, its pages pale, and the coin and the ink by it.
            foreach (float side in new[] { -1f, 1f }) Kit.Face(b, new Vector3(side * 0.01f, Top + 0.03f, -0.2f), new Vector3(side * 0.32f, Top + 0.015f, -0.2f), new Vector3(side * 0.32f, Top + 0.015f, 0.2f), new Vector3(side * 0.01f, Top + 0.03f, 0.2f), Vector3.UnitY, paper);
            Kit.Span(b, new Vector3(-0.34f, Top, -0.22f), new Vector3(0.34f, Top + 0.012f, 0.22f), 0.002f, ReichLeather(0.8f));
            Kit.Heap(b, new Vector3(0.6f, Top, 0.1f), new Vector2(0.12f, 0.1f), 0.06f, 0f, new VertexMaterial(Textures.MaterialCatalog.Iron.Index, new Colour(0.56f, 0.56f, 0.53f)), random, 2, 8);
            Kit.Lathe(b, new Vector3(-0.6f, Top, 0.15f), Vector3.UnitY, 0.08f, Kit.Profile((0f, 0.04f), (0.7f, 0.04f), (1f, 0.015f)), 8, 2, Dark);
            // Papers under a stone, sealed in the Reich's oxblood.
            for (int k = 0; k < 3; k++)
            {
                Vector3 at = new Vector3(-0.6f + k * 0.12f, Top + 0.005f + k * 0.003f, -0.2f);
                Kit.Face(b, at + new Vector3(-0.1f, 0f, -0.14f), at + new Vector3(0.1f, 0f, -0.14f), at + new Vector3(0.1f, 0f, 0.14f), at + new Vector3(-0.1f, 0f, 0.14f), Vector3.UnitY, paper);
                Kit.Lathe(b, at + new Vector3(0f, 0.001f, 0.08f), Vector3.UnitY, 0.01f, Kit.Profile((0f, 0.03f), (1f, 0.03f)), 8, 1, OxbloodPaint());
            }
            Kit.Block(b, new Vector3(-0.48f, Top + 0.05f, -0.2f), new Vector3(0.06f, 0.05f, 0.05f), 0.4f, 0.02f, Dressed(new Vector3(0.42f, 0.41f, 0.38f), random));
            // The stool behind it and the awning over it on four poles.
            Kit.Lathe(b, new Vector3(0f, 0f, -0.85f), Vector3.UnitY, 0.5f, Kit.Profile((0f, 0.16f), (0.9f, 0.16f), (1f, 0.2f)), 8, 2, dark);
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                Kit.Pole(b, new Vector3(x, -0.2f, 0.9f), new Vector3(x, 2.3f, 0.9f), 0.05f, dark);
                Kit.Pole(b, new Vector3(x, -0.2f, -1.2f), new Vector3(x, 2.6f, -1.2f), 0.05f, dark);
            }
            Kit.Cloth(b, new Vector3(-1.45f, 2.6f, -1.3f), new Vector3(1.45f, 2.6f, -1.3f), new Vector3(1.45f, 2.3f, 1.05f), new Vector3(-1.45f, 2.3f, 1.05f), 0.12f, 4, cloth, Canvas(random, 0.7f));
        }

        /// <summary>A rail on posts that beasts are tied to, the rings on it, a trough along its front with water in it, and the ground trodden under it. Numbers: length.</summary>
        public static void HitchingRail(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 9f);
            VertexMaterial wood = Weathered(random), iron = LandIron(random);
            float half = length * 0.5f;
            int posts = Math.Max(2, (int)(length / 2.5f) + 1);
            for (int k = 0; k < posts; k++)
            {
                float x = -half + k * length / (posts - 1);
                Kit.Span(b, new Vector3(x - 0.09f, -0.2f, -0.09f), new Vector3(x + 0.09f, 1.15f, 0.09f), 0.01f, wood);
            }
            Kit.Pole(b, new Vector3(-half - 0.15f, 1.08f, 0f), new Vector3(half + 0.15f, 1.08f, 0f), 0.07f, wood);
            for (float x = -half + 0.6f; x < half; x += 1.1f) b.Tube(new Vector3(x - 0.02f, 1.08f, 0f), new Vector3(x + 0.02f, 1.08f, 0f), 0.11f, 0.11f, 8, iron, capped: false);
            Kit.Span(b, new Vector3(-half + 0.2f, -0.05f, 0.45f), new Vector3(half - 0.2f, 0.5f, 1.05f), 0.02f, wood);
            Kit.Face(b, new Vector3(-half + 0.28f, 0.42f, 0.52f), new Vector3(half - 0.28f, 0.42f, 0.52f), new Vector3(half - 0.28f, 0.42f, 0.98f), new Vector3(-half + 0.28f, 0.42f, 0.98f), Vector3.UnitY, Glass(new Vector3(0.05f, 0.07f, 0.07f)));
        }

        /// <summary>A board of notices on two posts under a little pitched roof: the land's own, weathered and torn, and nailed over them the Reich's, new, each with its oxblood seal.</summary>
        public static void NoticeBoard(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Weathered(random), dark = Timber(random, 0.7f), paper = Plaster(1f), old = Plaster(0.82f);
            const float Wide = 2.2f, Low = 1f, High = 2.3f;
            foreach (float x in new[] { -Wide * 0.5f, Wide * 0.5f }) Kit.Span(b, new Vector3(x - 0.08f, -0.2f, -0.08f), new Vector3(x + 0.08f, High + 0.35f, 0.08f), 0.01f, dark);
            Kit.Span(b, new Vector3(-Wide * 0.5f, Low, -0.04f), new Vector3(Wide * 0.5f, High, 0.04f), 0.01f, wood);
            foreach (float side in new[] { -1f, 1f }) Kit.Face(b, new Vector3(-Wide * 0.5f - 0.25f, High + 0.15f, side * 0.45f), new Vector3(Wide * 0.5f + 0.25f, High + 0.15f, side * 0.45f), new Vector3(Wide * 0.5f + 0.25f, High + 0.6f, 0f), new Vector3(-Wide * 0.5f - 0.25f, High + 0.6f, 0f), new Vector3(0f, 1f, side), dark);
            // The land's notices, old, and the Reich's over them.
            for (int k = 0; k < 7; k++)
            {
                bool reich = k >= 4;
                float w = random.Range(0.25f, 0.4f), h = random.Range(0.32f, 0.48f);
                Vector3 at = new Vector3(random.Range(-Wide * 0.5f + w, Wide * 0.5f - w), random.Range(Low + h, High - h), 0.045f + k * 0.002f);
                float turn = random.Range(-0.1f, 0.1f);
                Vector3 ax = new Vector3(MathF.Cos(turn), MathF.Sin(turn), 0f) * w, ay = new Vector3(-MathF.Sin(turn), MathF.Cos(turn), 0f) * h;
                Kit.Face(b, at - ax - ay, at + ax - ay, at + ax + ay, at - ax + ay, Vector3.UnitZ, reich ? paper : old);
                if (reich) Kit.Face(b, at - ay * 0.6f + new Vector3(-0.05f, -0.05f, 0.002f), at - ay * 0.6f + new Vector3(0.05f, -0.05f, 0.002f), at - ay * 0.6f + new Vector3(0.05f, 0.05f, 0.002f), at - ay * 0.6f + new Vector3(-0.05f, 0.05f, 0.002f), Vector3.UnitZ, OxbloodPaint());
            }
        }
    }
}
