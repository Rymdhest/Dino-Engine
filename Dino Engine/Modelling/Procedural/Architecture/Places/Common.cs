using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// What any place with a story has about it (PLACES.md): crates and barrels, a tent, a fire with a pot over it, a lantern on its post, a banner, an open shelter. The Reich's are in its own
    /// colours, black iron, oxblood and brass, and nothing else is. Model +z is a piece's front, y = 0 where people stand. Headless.
    /// </summary>
    internal static class Common
    {
        /// <summary>Crates stacked and set about, the Reich's with its mark on them.</summary>
        public static void Crates(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 1, 6);
            bool marked = spec.Int(1) == 1;
            VertexMaterial boards = Timber(random, marked ? 0.85f : 1.05f), battens = marked ? Iron(random) : Timber(random, 0.72f);
            float x = -1.1f;
            for (int k = 0; k < count; k++)
            {
                float size = random.Range(0.6f, 0.82f);
                bool onTop = k > 1 && random.Chance(0.45f);
                Vector3 at = onTop ? new Vector3(random.Range(-0.9f, 0.9f), Ground + size + 1.45f, random.Range(-0.3f, 0.3f)) : new Vector3(x + size, Ground + size, random.Range(-0.5f, 0.5f));
                if (!onTop) x += size * 2f + 0.06f;
                if (x > 1.8f) x = -1.1f;
                Kit.Crate(b, at, random.Range(-0.25f, 0.25f), new Vector3(size, size, size * random.Range(0.85f, 1.1f)), boards, battens, random, marked && random.Chance(0.7f));
            }
        }

        /// <summary>Barrels stood together, one or two lying, bound in the Reich's iron or the land's.</summary>
        public static void Barrels(MeshBuilder b, in PlaceSpec spec, WorldRandom random, bool reich = false)
        {
            int count = Math.Clamp(spec.Int(0), 1, 7);
            for (int k = 0; k < count; k++)
            {
                float a = k * 2.4f + random.Range(-0.3f, 0.3f), r = k == 0 ? 0f : 0.95f + random.Range(0f, 0.25f) * k * 0.4f;
                Vector3 at = new Vector3(MathF.Cos(a) * r, Ground - 0.04f, MathF.Sin(a) * r);
                bool lying = k > 2 && random.Chance(0.5f);
                Kit.Barrel(b, at, random.Float() * MathF.Tau, lying, random.Range(0.42f, 0.5f), random.Range(1.15f, 1.35f), Timber(random, 0.9f), reich ? Iron(random) : LandIron(random));
            }
        }

        /// <summary>A ridge tent of canvas on two poles and a ridge, its sides pegged out on guys and its flaps tied back (<see cref="Tents.Ridge"/>), the Reich's charcoal under an oxblood ridge.</summary>
        public static void Tent(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 3f, 9f);
            bool reich = spec.Int(1) == 1;
            Tents.Look look = reich ? Tents.Look.Reich(random, random.Range(0.3f, 0.7f)) : Tents.Look.Land(random, random.Range(0.3f, 0.9f));
            Tents.Ridge(b, random, look, length * 0.5f, random.Range(2.5f, 3f), random.Range(2.9f, 3.4f), 1.1f, 0.95f, reich);
        }

        /// <summary>A fire in its ring of stones with a pot hung over it on a tripod, a log beside it to sit on.</summary>
        public static void Campfire(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            Kit.Fire(b, Vector3.Zero, 0.85f, random, lights);
            VertexMaterial wood = Timber(random, 0.8f), iron = LandIron(random);
            Vector3 apex = new Vector3(0f, 2.2f, 0f);
            for (int k = 0; k < 3; k++)
            {
                float a = k * MathF.Tau / 3f + 0.3f;
                Kit.Pole(b, new Vector3(MathF.Cos(a) * 1.25f, 0f, MathF.Sin(a) * 1.25f), apex + new Vector3(MathF.Cos(a) * 0.08f, 0.15f, MathF.Sin(a) * 0.08f), 0.05f, wood);
            }
            Kit.Chain(b, apex, apex - Vector3.UnitY * 1.1f, 0f, 0.14f, iron);
            Kit.Lathe(b, apex - Vector3.UnitY * 1.6f, Vector3.UnitY, 0.5f, Kit.Profile((0f, 0.12f), (0.3f, 0.32f), (0.85f, 0.3f), (1f, 0.26f)), 10, 3, iron);
            Kit.Pole(b, new Vector3(-1.9f, Ground + 0.28f, 0.6f), new Vector3(-1.5f, Ground + 0.28f, -1.2f), 0.28f, PlaceMaterials.Timber(random, 0.7f));
        }

        /// <summary>A lantern on its post, the Reich's of iron with its lantern in brass, the land's of timber.</summary>
        public static void Lantern(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float height = Math.Clamp(spec.Metres(0), 2.5f, 6f);
            bool reich = spec.Int(1) == 1;
            if (reich)
            {
                VertexMaterial iron = Iron(random), brass = Brass(random);
                Kit.Pole(b, new Vector3(0f, -0.3f, 0f), new Vector3(0f, height, 0f), 0.1f, iron, 0.07f);
                Kit.Lathe(b, new Vector3(0f, 0f, 0f), Vector3.UnitY, 0.5f, Kit.Profile((0f, 0.3f), (1f, 0.12f)), 8, 1, iron);
                Kit.Pole(b, new Vector3(0f, height - 0.25f, 0f), new Vector3(0f, height - 0.25f, 0.85f), 0.05f, iron);
                Kit.Lantern(b, new Vector3(0f, height - 0.27f, 0.82f), brass, lights);
                return;
            }
            Kit.LanternPost(b, height, Timber(random, 0.85f), LandIron(random), lights);
        }

        /// <summary>A banner on its pole: the Reich's oxblood, swallow-tailed, its mark in brass on both faces, on black iron with a brass finial; or a cloth of the land's on a timber pole.</summary>
        public static void Banner(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float height = Math.Clamp(spec.Metres(0), 3f, 9f);
            bool reich = spec.Int(1) == 1;
            VertexMaterial pole = reich ? Iron(random) : Timber(random, 0.85f);
            Kit.Pole(b, new Vector3(0f, -0.4f, 0f), new Vector3(0f, height, 0f), 0.08f, pole, 0.06f);
            Kit.Pole(b, new Vector3(-0.85f, height - 0.3f, 0.05f), new Vector3(0.85f, height - 0.3f, 0.05f), 0.04f, pole);
            if (reich) Kit.Lathe(b, new Vector3(0f, height, 0f), Vector3.UnitY, 0.45f, Kit.Profile((0f, 0.09f), (0.4f, 0.13f), (1f, 0f)), 6, 2, Brass(random));
            VertexMaterial cloth = reich ? Oxblood() : new VertexMaterial(Textures.MaterialCatalog.Cloth.Index, new Colour(random.Chance(0.5f) ? new Vector3(0.14f, 0.2f, 0.34f) : new Vector3(0.18f, 0.28f, 0.16f)));
            float drop = height * 0.42f;
            Vector3 top0 = new Vector3(-0.8f, height - 0.32f, 0.12f), top1 = new Vector3(0.8f, height - 0.32f, 0.12f);
            Vector3 foot0 = new Vector3(-0.8f, height - 0.32f - drop, 0.16f), foot1 = new Vector3(0.8f, height - 0.32f - drop, 0.16f);
            Kit.Sheet(b, foot0, foot1, top1, top0, cloth, cloth);
            if (reich)
            {
                // Its tails, and the mark on both faces.
                Vector3 notch = new Vector3(0f, height - 0.32f - drop * 0.82f, 0.16f);
                Kit.Face(b, foot0, notch, foot0 + new Vector3(0f, -drop * 0.22f, 0f), Vector3.UnitZ, cloth);
                Kit.Face(b, foot0, notch, foot0 + new Vector3(0f, -drop * 0.22f, 0f), -Vector3.UnitZ, cloth);
                Kit.Face(b, foot1, foot1 + new Vector3(0f, -drop * 0.22f, 0f), notch, Vector3.UnitZ, cloth);
                Kit.Face(b, foot1, foot1 + new Vector3(0f, -drop * 0.22f, 0f), notch, -Vector3.UnitZ, cloth);
                ArenaMark(b, new Vector3(0f, height - 0.32f - drop * 0.45f, 0.19f), Vector3.UnitX, 1.2f, random);
                ArenaMark(b, new Vector3(0f, height - 0.32f - drop * 0.45f, 0.13f), -Vector3.UnitX, 1.2f, random);
            }
        }

        private static void ArenaMark(MeshBuilder b, Vector3 at, Vector3 right, float size, WorldRandom random)
        {
            Arena.ArenaStone.Mark(b, at, right, Vector3.UnitY, size, 0.012f, Brass(random), Oxblood());
        }

        /// <summary>An open-fronted shelter of boards on four posts, its roof falling to the back, its back and sides boarded half way down.</summary>
        public static void Shelter(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float width = Math.Clamp(spec.Metres(0), 3f, 12f), depth = Math.Clamp(spec.Metres(1), 2.5f, 7f);
            VertexMaterial wood = Weathered(random), roof = new VertexMaterial(Textures.MaterialCatalog.Shingle.Index, new Colour(0.42f, 0.36f, 0.3f));
            float front = random.Range(4.4f, 5.2f), back = front - random.Range(0.9f, 1.4f);
            float hx = width * 0.5f, hz = depth * 0.5f;
            foreach (float sx in new[] { -1f, 1f })
            {
                Kit.Span(b, new Vector3(sx * hx - 0.14f, -0.3f, hz - 0.28f), new Vector3(sx * hx + 0.14f, front, hz), 0.02f, wood);
                Kit.Span(b, new Vector3(sx * hx - 0.14f, -0.3f, -hz), new Vector3(sx * hx + 0.14f, back, -hz + 0.28f), 0.02f, wood);
            }
            Kit.Plank(b, new Vector3(-hx - 0.3f, front, hz - 0.14f), new Vector3(hx + 0.3f, front, hz - 0.14f), Vector3.UnitY, 0.25f, 0.25f, wood);
            Kit.Plank(b, new Vector3(-hx - 0.3f, back, -hz + 0.14f), new Vector3(hx + 0.3f, back, -hz + 0.14f), Vector3.UnitY, 0.25f, 0.25f, wood);
            // The roof, overhanging all round.
            Vector3 r0 = new Vector3(-hx - 0.5f, front + 0.2f, hz + 0.6f), r1 = new Vector3(hx + 0.5f, front + 0.2f, hz + 0.6f), r2 = new Vector3(hx + 0.5f, back + 0.2f, -hz - 0.6f), r3 = new Vector3(-hx - 0.5f, back + 0.2f, -hz - 0.6f);
            Kit.Solid(b, new[] { r0, r1, r2, r3, r0 - Vector3.UnitY * 0.12f, r1 - Vector3.UnitY * 0.12f, r2 - Vector3.UnitY * 0.12f, r3 - Vector3.UnitY * 0.12f }, 0.02f, roof);
            // Boards across its back and down its sides, to half its height.
            float boardTop = back * 0.62f;
            Kit.Span(b, new Vector3(-hx, 0f, -hz), new Vector3(hx, boardTop, -hz + 0.08f), 0.01f, wood);
            foreach (float sx in new[] { -1f, 1f }) Kit.Span(b, new Vector3(sx * hx - 0.04f, 0f, -hz), new Vector3(sx * hx + 0.04f, boardTop * 0.85f, hz - 0.3f), 0.01f, wood);
        }
    }
}
