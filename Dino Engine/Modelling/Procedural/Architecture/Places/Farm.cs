using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A rearing farm (PLACES.md), where the Reich raises what it catches in the wild to pull its guns and carry its loads: the hatching shed where the Institute's eggs are kept warm in straw
    /// over braziers, open along its front; the hay racks over troughs in the paddocks; the cistern on its legs that fills the troughs; and the paddocks' timber gates, the farm's own, that the
    /// Reich keeps chained. Headless.
    /// </summary>
    internal static class Farm
    {
        /// <summary>
        /// A long low shed open along its front (+z), its lean-to roof down to its back on posts painted the Reich's oxblood, inside it rows of straw beds with eggs laid in them, the braziers
        /// between the rows that keep them warm, and crates from the Institute stacked at its end. Numbers: length.
        /// </summary>
        public static void HatchingShed(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(0), 6f, 16f);
            const float Deep = 4.4f, Front = 2.6f, Back = 1.7f;
            VertexMaterial post = OxbloodPaint(0.9f), wood = Timber(random, 0.85f), roof = Straw(random, 0.8f), straw = Straw(random);
            float half = length * 0.5f;
            // Its back wall and ends of boards, and its posts along its open front.
            Kit.Span(b, new Vector3(-half, -0.1f, -Deep * 0.5f - 0.1f), new Vector3(half, Back, -Deep * 0.5f + 0.05f), 0.02f, wood);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * half;
                Kit.Face(b, new Vector3(x, -0.1f, -Deep * 0.5f), new Vector3(x, -0.1f, Deep * 0.5f), new Vector3(x, Front, Deep * 0.5f), new Vector3(x, Back, -Deep * 0.5f), new Vector3(s, 0f, 0f), wood);
            }
            int bays = Math.Max(2, (int)(length / 3f));
            for (int i = 0; i <= bays; i++)
            {
                float x = -half + i * length / bays;
                Kit.Span(b, new Vector3(x - 0.1f, -0.1f, Deep * 0.5f - 0.2f), new Vector3(x + 0.1f, Front, Deep * 0.5f), 0.01f, post);
            }
            // The roof, falling from the front to the back, overhanging both.
            Kit.Face(b, new Vector3(-half - 0.3f, Front + 0.05f, Deep * 0.5f + 0.4f), new Vector3(half + 0.3f, Front + 0.05f, Deep * 0.5f + 0.4f), new Vector3(half + 0.3f, Back + 0.05f, -Deep * 0.5f - 0.4f), new Vector3(-half - 0.3f, Back + 0.05f, -Deep * 0.5f - 0.4f), Vector3.UnitY, roof);
            Kit.Face(b, new Vector3(-half - 0.3f, Front, Deep * 0.5f + 0.4f), new Vector3(half + 0.3f, Front, Deep * 0.5f + 0.4f), new Vector3(half + 0.3f, Back, -Deep * 0.5f - 0.4f), new Vector3(-half - 0.3f, Back, -Deep * 0.5f - 0.4f), -Vector3.UnitY, wood);
            // Inside: straw beds in two rows along it, the eggs laid in them, and braziers between.
            VertexMaterial egg = new VertexMaterial(Textures.MaterialCatalog.Bone.Index, new Colour(new Vector3(0.6f, 0.56f, 0.44f)));
            foreach (float z in new[] { -Deep * 0.22f, Deep * 0.18f })
            {
                Kit.Span(b, new Vector3(-half + 0.5f, -0.05f, z - 0.55f), new Vector3(half - 0.5f, 0.3f, z + 0.55f), 0.02f, wood);
                Kit.Heap(b, new Vector3(0f, 0.25f, z), new Vector2(half - 0.6f, 0.5f), 0.14f, 0f, straw, random, 2, 12);
                for (float x = -half + 0.9f; x < half - 0.7f; x += random.Range(0.42f, 0.6f))
                {
                    Laboratory.Egg(b, new Vector3(x, 0.33f, z + random.Range(-0.25f, 0.25f)), random.Range(0.17f, 0.24f), random, Tint(egg, new Vector3(random.Range(0.9f, 1.08f))));
                }
            }
            int braziers = Math.Max(1, bays - 1);
            for (int i = 0; i < braziers; i++)
            {
                float x = -half + (i + 1) * length / (braziers + 1);
                Kit.Brazier(b, new Vector3(x, 0f, 0f), 0.75f, random, lights, Iron(random));
            }
            // The Institute's crates, stacked at its end.
            Kit.Crate(b, new Vector3(half + 0.9f, 0.45f, Deep * 0.2f), random.Range(-0.2f, 0.2f), new Vector3(0.6f, 0.45f, 0.5f), Timber(random), Iron(random), random, marked: true);
            Kit.Crate(b, new Vector3(half + 0.9f, 1.35f, Deep * 0.2f), random.Range(-0.2f, 0.2f), new Vector3(0.5f, 0.42f, 0.45f), Timber(random), Iron(random), random, marked: true);
        }

        /// <summary>
        /// A paddock's gate, the farm's own: five bars and a brace between two stiles, hinged at x = 0 and shutting along +x, and round its latch the Reich's black chain and lock, which is all
        /// that keeps it shut. Numbers: width, height.
        /// </summary>
        public static void Gate(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 1.5f, 5f), height = Math.Clamp(spec.Metres(1), 0.9f, 2.2f);
            VertexMaterial wood = Weathered(random), iron = Iron(random);
            const float Foot = 0.12f;
            Kit.Span(b, new Vector3(0.02f, Foot, -0.06f), new Vector3(0.16f, height + 0.05f, 0.06f), 0.01f, wood);
            Kit.Span(b, new Vector3(width - 0.16f, Foot, -0.05f), new Vector3(width - 0.04f, height, 0.05f), 0.01f, wood);
            for (int k = 0; k < 5; k++)
            {
                float y = Foot + 0.1f + k * (height - Foot - 0.2f) / 4f;
                Kit.Span(b, new Vector3(0.1f, y - 0.05f, -0.035f), new Vector3(width - 0.1f, y + 0.05f, 0.035f), 0.008f, wood);
            }
            Kit.Plank(b, new Vector3(0.15f, Foot + 0.12f, 0.045f), new Vector3(width - 0.15f, height - 0.1f, 0.045f), Vector3.UnitZ, 0.11f, 0.04f, wood);
            // Its hinges, and the chain round its latch stile and the post it shuts against, with the lock hanging off it.
            foreach (float y in new[] { Foot + 0.2f, height - 0.15f }) Kit.Span(b, new Vector3(-0.02f, y - 0.03f, -0.08f), new Vector3(0.3f, y + 0.03f, 0.08f), 0.005f, iron);
            Vector3 latch = new Vector3(width - 0.1f, height * 0.62f, 0f);
            Kit.Chain(b, latch + new Vector3(0f, 0.1f, -0.08f), latch + new Vector3(0.2f, 0.05f, -0.06f), 0.04f, 0.07f, iron);
            Kit.Chain(b, latch + new Vector3(0f, -0.08f, 0.08f), latch + new Vector3(0.2f, -0.1f, 0.06f), 0.04f, 0.07f, iron);
            Kit.Span(b, latch + new Vector3(0.12f, -0.3f, -0.05f), latch + new Vector3(0.24f, -0.16f, 0.05f), 0.01f, iron);
        }

        /// <summary>A hay rack along x: slats in a V on legs, the hay heaped in it and fallen round it, over a trough with water standing in it. Numbers: length.</summary>
        public static void FeedRack(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 2f, 8f);
            VertexMaterial wood = Weathered(random), hay = Straw(random, 1.05f);
            float half = length * 0.5f;
            const float Top = 1.9f, Bottom = 0.9f, Spread = 0.85f;
            foreach (float x in new[] { -half, half })
            {
                foreach (float z in new[] { -Spread, Spread }) Kit.Pole(b, new Vector3(x, -0.2f, z * 0.6f), new Vector3(x, Top, z), 0.06f, wood);
            }
            foreach (float z in new[] { -Spread, Spread }) Kit.Pole(b, new Vector3(-half - 0.15f, Top, z), new Vector3(half + 0.15f, Top, z), 0.05f, wood);
            Kit.Pole(b, new Vector3(-half, Bottom, 0f), new Vector3(half, Bottom, 0f), 0.06f, wood);
            for (float x = -half + 0.2f; x < half; x += 0.25f)
            {
                foreach (float z in new[] { -Spread, Spread }) Kit.Pole(b, new Vector3(x, Bottom, 0f), new Vector3(x, Top, z), 0.025f, wood);
            }
            Kit.Heap(b, new Vector3(0f, Bottom + 0.15f, 0f), new Vector2(half - 0.1f, Spread * 0.7f), 0.75f, 0f, hay, random, 3, 12);
            // The trough under it, with water in it, and hay fallen round.
            Kit.Span(b, new Vector3(-half, -0.05f, Spread + 0.15f), new Vector3(half, 0.55f, Spread + 0.85f), 0.02f, wood);
            Kit.Face(b, new Vector3(-half + 0.08f, 0.45f, Spread + 0.23f), new Vector3(half - 0.08f, 0.45f, Spread + 0.23f), new Vector3(half - 0.08f, 0.45f, Spread + 0.77f), new Vector3(-half + 0.08f, 0.45f, Spread + 0.77f), Vector3.UnitY, Glass(new Vector3(0.05f, 0.07f, 0.07f)));
            for (int k = 0; k < 3; k++) Kit.Heap(b, new Vector3(random.Range(-half, half), Ground - 0.06f, random.Range(-1.2f, -0.6f)), new Vector2(random.Range(0.4f, 0.8f), random.Range(0.3f, 0.5f)), 0.1f, random.Float(), hay, random, 2, 8);
        }

        /// <summary>A cistern of staves and hoops on four legs, a spout from its foot running into a long trough below it, and the ladder up its side. Numbers: none.</summary>
        public static void WaterTank(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            VertexMaterial wood = Timber(random), hoop = Iron(random), dark = Timber(random, 0.7f);
            float radius = random.Range(1.1f, 1.4f), high = random.Range(1.8f, 2.4f), tall = random.Range(1.4f, 1.8f);
            foreach (float x in new[] { -radius * 0.75f, radius * 0.75f })
            {
                foreach (float z in new[] { -radius * 0.75f, radius * 0.75f }) Kit.Span(b, new Vector3(x - 0.1f, -0.2f, z - 0.1f), new Vector3(x + 0.1f, high, z + 0.1f), 0.01f, dark);
            }
            Kit.Span(b, new Vector3(-radius, high - 0.05f, -radius), new Vector3(radius, high + 0.1f, radius), 0.01f, dark);
            Kit.Lathe(b, new Vector3(0f, high + 0.1f, 0f), Vector3.UnitY, tall, Kit.Profile((0f, radius * 0.96f), (0.5f, radius), (1f, radius * 0.97f)), 14, 2, wood);
            Kit.Face(b, new Vector3(-radius * 0.7f, high + tall - 0.05f, -radius * 0.7f), new Vector3(radius * 0.7f, high + tall - 0.05f, -radius * 0.7f), new Vector3(radius * 0.7f, high + tall - 0.05f, radius * 0.7f), new Vector3(-radius * 0.7f, high + tall - 0.05f, radius * 0.7f), Vector3.UnitY, Glass(new Vector3(0.05f, 0.07f, 0.07f)));
            foreach (float t in new[] { 0.15f, 0.5f, 0.85f }) b.Tube(new Vector3(0f, high + 0.1f + tall * t - 0.04f, 0f), new Vector3(0f, high + 0.1f + tall * t + 0.04f, 0f), radius + 0.025f, radius + 0.025f, 14, hoop, capped: false);
            // The spout and the trough it runs into.
            Kit.Pole(b, new Vector3(0f, high + 0.25f, radius), new Vector3(0f, 0.9f, radius + 1.1f), 0.06f, dark);
            Kit.Span(b, new Vector3(-1.6f, -0.05f, radius + 0.9f), new Vector3(1.6f, 0.6f, radius + 1.6f), 0.02f, wood);
            Kit.Face(b, new Vector3(-1.5f, 0.5f, radius + 0.98f), new Vector3(1.5f, 0.5f, radius + 0.98f), new Vector3(1.5f, 0.5f, radius + 1.52f), new Vector3(-1.5f, 0.5f, radius + 1.52f), Vector3.UnitY, Glass(new Vector3(0.05f, 0.07f, 0.07f)));
            // The ladder up to its rim.
            Vector3 foot = new Vector3(-radius - 0.7f, 0f, 0f), head = new Vector3(-radius - 0.05f, high + tall, 0f);
            foreach (float z in new[] { -0.25f, 0.25f }) Kit.Pole(b, foot + Vector3.UnitZ * z, head + Vector3.UnitZ * z, 0.035f, dark);
            for (float t = 0.08f; t < 1f; t += 0.1f) Kit.Pole(b, Vector3.Lerp(foot, head, t) - Vector3.UnitZ * 0.25f, Vector3.Lerp(foot, head, t) + Vector3.UnitZ * 0.25f, 0.025f, dark);
        }
    }
}
