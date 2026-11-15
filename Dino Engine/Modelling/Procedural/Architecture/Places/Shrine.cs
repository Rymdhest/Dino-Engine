using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A shrine of the land's (PLACES.md, LORE.md: put out by the Reich, and kindled again by a Defender): its hall behind the altar, roofed in shingles over a back wall with the land's
    /// figure in its niche, ribbons in the land's colours hanging from its eaves and chimes at its corners; the gateway the path comes in by, hung with bells; tall lanterns of stone
    /// along the way, which the Reich keeps out and has knocked down where it could; the table offerings were left on; the basin, the bell and its frame, ribbons round the tree that's
    /// always been there, and little cairns of stones left by whoever came. Nothing here is the Reich's but the damage, its notices and its paint. Model +z is a piece's front, y = 0
    /// where people stand. Headless.
    /// </summary>
    internal static class Shrine
    {
        /// <summary>The land's own colours for cloth, deep, as pale cloth goes white: blue, white, green, ochre and the north's warm red, never oxblood.</summary>
        private static readonly Vector3[] Ribbon =
        {
            new Vector3(0.1f, 0.18f, 0.36f), new Vector3(0.68f, 0.66f, 0.6f), new Vector3(0.12f, 0.28f, 0.16f), new Vector3(0.58f, 0.4f, 0.1f), new Vector3(0.55f, 0.16f, 0.08f),
        };

        private static VertexMaterial Cloth(Vector3 colour, float shade = 1f) => new VertexMaterial(MaterialCatalog.Cloth.Index, new Colour(colour * shade));

        /// <summary>Old bronze gone green, as the land's bells and chimes are: never the Reich's brass.</summary>
        private static VertexMaterial Bronze(WorldRandom random, float green = 0.6f) => new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(Vector3.Lerp(new Vector3(0.46f, 0.3f, 0.16f), new Vector3(0.24f, 0.4f, 0.33f), green * random.Range(0.8f, 1.1f))));

        // Shingles gone silver with weather and green with lichen: pale, as a roof is most of what the camera sees of a hall, and a dark one is a hole (CHURCHYARD.md).
        private static VertexMaterial Shingles(WorldRandom random, float shade = 1f) => new VertexMaterial(MaterialCatalog.Shingle.Index, new Colour(Vector3.Lerp(new Vector3(0.6f, 0.55f, 0.48f), new Vector3(0.54f, 0.57f, 0.5f), random.Float()) * shade * random.Range(0.94f, 1.05f)));

        /// <summary>
        /// Flags of the land's colours strung on a line, as the shrine's people hang them for the wind to carry their wishes: the line from its foot at one height along +z to another,
        /// sagging, a flag every stride, each lifted a little by the wind so it's seen from above whichever way the line runs; a pole at either end if it wants one. Numbers: length,
        /// height at its start, height at its end, sag, a pole at its end (0, 1), a pole at its start (0, 1).
        /// </summary>
        public static void PrayerLine(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float length = Math.Clamp(spec.Metres(0), 1.5f, 22f), h0 = Math.Clamp(spec.Metres(1), 1.2f, 7f), h1 = Math.Clamp(spec.Metres(2), 1.2f, 7f), sag = Math.Clamp(spec.Metres(3), 0.05f, 2f);
            Vector3 a = new Vector3(0f, h0, 0f), c = new Vector3(0f, h1, length);
            VertexMaterial wood = Timber(random, 0.6f);
            foreach ((bool pole, Vector3 at) in new[] { (spec.Int(4) == 1, c), (spec.Int(5) == 1, a) })
            {
                if (!pole) continue;
                Kit.Pole(b, new Vector3(at.X, -0.4f, at.Z), at + Vector3.UnitY * 0.35f, 0.06f, wood, 0.045f);
                Kit.Lathe(b, at + Vector3.UnitY * 0.35f, Vector3.UnitY, 0.22f, Kit.Profile((0f, 0.07f), (0.5f, 0.09f), (1f, 0f)), 6, 2, Bronze(random));
            }
            Kit.Rope(b, a, c, sag, 0.012f, Rope(random, 0.9f), Math.Max(4, (int)(length / 1.5f)));
            int flags = Math.Max(2, (int)(length / 0.42f));
            int first = random.Int(Ribbon.Length);
            float lift = random.Float() < 0.5f ? 1f : -1f;
            for (int k = 1; k < flags; k++)
            {
                float t = k / (float)flags;
                Vector3 p = Kit.Hang(a, c, sag, t);
                Vector3 along = (Kit.Hang(a, c, sag, t + 0.01f) - Kit.Hang(a, c, sag, t - 0.01f)).Normalized();
                Vector3 across = Vector3.Cross(Vector3.UnitY, along).Normalized() * lift;
                float w = 0.26f, h = random.Range(0.26f, 0.34f);
                // Lifted by the wind: its lower edge carried out to one side, so its face looks up as well as along.
                Vector3 down = Vector3.Normalize(-Vector3.UnitY + across * random.Range(1.1f, 1.6f)) * h;
                Vector3 colour = Ribbon[(first + k) % Ribbon.Length];
                Kit.Sheet(b, p - along * (w * 0.5f), p + along * (w * 0.5f), p + along * (w * 0.5f) + down, p - along * (w * 0.5f) + down, Cloth(colour), Cloth(colour, 0.7f));
            }
        }

        /// <summary>The shrine's stone, from the colour an id carries, darker and greener towards the ground and lichened on what faces the sky.</summary>
        private static Func<Vector3, VertexMaterial> Stone(Vector3 colour, WorldRandom random, float y, float moss = 0.4f)
        {
            VertexMaterial face = Dressed(colour, random, y);
            VertexMaterial top = Tint(face, Vector3.Lerp(Vector3.One, new Vector3(0.78f, 0.92f, 0.62f), moss));
            return n => n.Y > 0.6f ? top : face;
        }

        private static Vector3 StoneOfSpec(in PlaceSpec spec, int first) => spec.Count > first + 2 ? StoneColour(spec.Int(first), spec.Int(first + 1), spec.Int(first + 2)) : new Vector3(0.52f, 0.5f, 0.46f);

        // ---------------------------------------------------------------- The altar

        /// <summary>
        /// The land's altar, which a Defender kindles (the catalog's <c>altar</c>, at every shrine, ring of stones and village): an octagonal plinth of two steps, a pedestal banded in
        /// carving, and on it a bowl of old bronze gone green with its embers banked in it, glowing still, waiting to be kindled; candles burnt down on the steps, and what's been
        /// left at its foot. Within the catalog's footprint (1.8 m round, 1.6 m to the bowl's lip).
        /// </summary>
        public static void Altar(MeshBuilder b, WorldRandom random, List<AssetLight> lights)
        {
            Vector3 colour = new Vector3(0.58f, 0.56f, 0.52f) * random.Range(0.94f, 1.04f);
            Func<Vector3, VertexMaterial> Weathered(float y) => Stone(colour, random, y, 0.5f);
            VertexMaterial carved = Dressed(colour * 1.05f, random, 1.2f);
            // The steps, octagonal, the lower worn round at its edge.
            Kit.Lathe(b, new Vector3(0f, -0.35f, 0f), Vector3.UnitY, 0.57f, Kit.Profile((0f, 1.74f), (0.85f, 1.74f), (1f, 1.66f)), 8, 2, Weathered(0.1f)(Vector3.UnitX));
            Kit.Lathe(b, new Vector3(0f, 0.2f, 0f), Vector3.UnitY, 0.01f, _ => 1.66f, 8, 1, Weathered(0.25f)(Vector3.UnitY));
            Kit.Lathe(b, new Vector3(0f, 0.21f, 0f), Vector3.UnitY, 0.27f, Kit.Profile((0f, 1.36f), (0.8f, 1.36f), (1f, 1.3f)), 8, 2, Weathered(0.35f)(Vector3.UnitX));
            Kit.Lathe(b, new Vector3(0f, 0.47f, 0f), Vector3.UnitY, 0.01f, _ => 1.3f, 8, 1, Weathered(0.5f)(Vector3.UnitY));
            // The pedestal, its bands of carving.
            Kit.Lathe(b, new Vector3(0f, 0.47f, 0f), Vector3.UnitY, 0.86f, Kit.Profile((0f, 0.74f), (0.12f, 0.66f), (0.85f, 0.6f), (1f, 0.68f)), 8, 4, carved);
            foreach (float y in new[] { 0.66f, 1.16f }) b.Tube(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.08f, 0f), 0.7f, 0.7f, 8, Dressed(colour * 0.92f, random, 1f), capped: false);
            // Little figures in relief round it, one on each face that's turned out: a ring broken somewhere, as the old roads' script is.
            for (int k = 0; k < 8; k++)
            {
                float a = (k + 0.5f) * MathF.Tau / 8f;
                Vector3 n = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Vector3 t = Vector3.Cross(Vector3.UnitY, n);
                Vector3 c = n * 0.63f + Vector3.UnitY * 0.92f;
                for (int s = 0; s < 6; s++)
                {
                    if (s == (k % 6)) continue;
                    float a0 = s * MathF.Tau / 6f, a1 = (s + 1) * MathF.Tau / 6f;
                    Vector3 p0 = c + (t * MathF.Cos(a0) + Vector3.UnitY * MathF.Sin(a0)) * 0.13f, p1 = c + (t * MathF.Cos(a1) + Vector3.UnitY * MathF.Sin(a1)) * 0.13f;
                    Kit.Plank(b, p0, p1, n, 0.04f, 0.02f, Dressed(colour * 0.78f, random, 1f), 0f);
                }
            }
            // Its cap and the bowl on it, bronze gone green, the embers banked in it.
            Kit.Lathe(b, new Vector3(0f, 1.33f, 0f), Vector3.UnitY, 0.12f, Kit.Profile((0f, 0.82f), (1f, 0.92f)), 8, 1, Weathered(1.4f)(Vector3.UnitY));
            VertexMaterial bronze = Bronze(random, 0.75f);
            Kit.Lathe(b, new Vector3(0f, 1.45f, 0f), Vector3.UnitY, 0.32f, Kit.Profile((0f, 0.42f), (0.35f, 0.74f), (0.8f, 0.86f), (1f, 0.9f)), 12, 3, bronze, openEnd: true);
            b.Tube(new Vector3(0f, 1.74f, 0f), new Vector3(0f, 1.79f, 0f), 0.94f, 0.94f, 12, bronze, capped: false);
            Kit.Heap(b, new Vector3(0f, 1.58f, 0f), new Vector2(0.76f), 0.14f, 0f, Glow(1f, 0.42f, 0.12f, 0.9f), random, 3, 12);
            for (int k = 0; k < 5; k++)
            {
                float a = k * MathF.Tau / 5f + random.Range(-0.2f, 0.2f);
                Kit.Pole(b, new Vector3(MathF.Cos(a) * 0.55f, 1.66f, MathF.Sin(a) * 0.55f), new Vector3(MathF.Cos(a + 2f) * 0.15f, 1.76f, MathF.Sin(a + 2f) * 0.15f), 0.05f, new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.13f, 0.11f, 0.1f)));
            }
            lights.Add(AssetLight.Glow(new Vector3(0f, 2.2f, 0f), new Vector3(1f, 0.62f, 0.3f), 34f, 12f) with { Flicker = 0.14f, FlickerSpeed = 2.4f });
            // Candles burnt down on the upper step, and what's been left on the lower.
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathF.Tau / 8f + 0.2f;
                Vector3 at = new Vector3(MathF.Cos(a) * 1.05f, 0.48f, MathF.Sin(a) * 1.05f);
                for (int c = 0; c < random.Int(1, 3); c++) Kit.Lathe(b, at + new Vector3(random.Range(-0.08f, 0.08f), 0f, random.Range(-0.08f, 0.08f)), Vector3.UnitY, random.Range(0.05f, 0.2f), _ => 0.035f, 6, 1, Wax(random));
            }
            VertexMaterial clay = new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(0.46f, 0.3f, 0.2f));
            for (int k = 0; k < 6; k++)
            {
                float a = random.Float() * MathF.Tau;
                Vector3 at = new Vector3(MathF.Cos(a) * 1.5f, 0.22f, MathF.Sin(a) * 1.5f);
                if (random.Chance(0.5f)) Kit.Lathe(b, at, Vector3.UnitY, 0.07f, Kit.Profile((0f, 0.05f), (1f, 0.09f)), 7, 1, clay);
                else Kit.Ball(b, at + Vector3.UnitY * 0.04f, new Vector3(0.04f), 5, 3, Flat(Ribbon[random.Int(Ribbon.Length)] * 1.25f));
            }
        }

        // ---------------------------------------------------------------- The hall

        /// <summary>
        /// The shrine's hall, behind its altar and open to it: a stone plinth with steps up its front, posts of timber on stone bases, a back wall of stone with the land's figure in its
        /// niche and an offering shelf under it, a roof of shingles on broad eaves, ribbons hanging off the front eave and chimes at its corners. The Reich has torn a hole in its roof,
        /// pulled down ribbons and nailed its notice to a post. Numbers: width, depth, roof (0 hipped, 1 gabled, 2 with eaves swept up), damage (0 to 2), stone (r, g, b per mille).
        /// </summary>
        public static void Hall(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float w = Math.Clamp(spec.Metres(0), 5.5f, 9f), d = Math.Clamp(spec.Metres(1), 4f, 6.5f);
            int roof = Math.Clamp(spec.Int(2), 0, 2), damage = Math.Clamp(spec.Int(3), 0, 2);
            Vector3 colour = StoneOfSpec(spec, 4);
            float hx = w * 0.5f, hz = d * 0.5f;
            const float Plinth = 0.65f, Wall = 0.5f;
            float postTop = random.Range(3f, 3.4f);
            VertexMaterial wood = Timber(random, 0.62f), dark = Timber(random, 0.45f);

            // The plinth, a course of big blocks, and two steps up the middle of its front.
            Kit.Span(b, new Vector3(-hx - 0.6f, -0.6f, -hz - 0.6f), new Vector3(hx + 0.6f, Plinth, hz + 0.6f), 0.06f, Stone(colour, random, 0.3f, 0.55f));
            Kit.Span(b, new Vector3(-hx - 0.72f, -0.6f, -hz - 0.72f), new Vector3(hx + 0.72f, 0.18f, hz + 0.72f), 0.05f, Stone(colour * 0.92f, random, 0.1f, 0.3f));
            for (int k = 0; k < 2; k++)
            {
                float h = Plinth - 0.22f * (k + 1);
                Kit.Span(b, new Vector3(-1.4f - 0.2f * k, -0.6f, hz + 0.6f), new Vector3(1.4f + 0.2f * k, h + 0.02f, hz + 0.95f + 0.35f * k), 0.04f, Stone(colour, random, h, 0.5f));
            }

            // The back wall, its niche, the figure in it and the shelf of offerings under it.
            float wallTop = postTop - 0.05f;
            float niche = 0.65f;
            Func<Vector3, VertexMaterial> walls = Stone(colour, random, 1.6f, 0.3f);
            Kit.Span(b, new Vector3(-hx - 0.25f, Plinth - 0.05f, -hz - 0.25f), new Vector3(-niche, wallTop, -hz + Wall - 0.25f), 0.03f, walls);
            Kit.Span(b, new Vector3(niche, Plinth - 0.05f, -hz - 0.25f), new Vector3(hx + 0.25f, wallTop, -hz + Wall - 0.25f), 0.03f, walls);
            Kit.Span(b, new Vector3(-niche, Plinth - 0.05f, -hz - 0.25f), new Vector3(niche, Plinth + 0.75f, -hz + Wall - 0.25f), 0.03f, walls);
            Kit.Span(b, new Vector3(-niche, Plinth + 2.35f, -hz - 0.25f), new Vector3(niche, wallTop, -hz + Wall - 0.25f), 0.03f, walls);
            Kit.Span(b, new Vector3(-niche, Plinth + 0.75f, -hz - 0.25f), new Vector3(niche, Plinth + 2.35f, -hz - 0.15f), 0f, Dark);
            // Its lintel, a long stone, and its jambs.
            Kit.Span(b, new Vector3(-niche - 0.18f, Plinth + 2.32f, -hz + Wall - 0.3f), new Vector3(niche + 0.18f, Plinth + 2.6f, -hz + Wall - 0.17f), 0.03f, Stone(colour * 1.06f, random, 2.5f, 0.2f));
            foreach (float s in new[] { -1f, 1f }) Kit.Span(b, new Vector3(s * niche - 0.1f, Plinth + 0.72f, -hz + Wall - 0.3f), new Vector3(s * niche + 0.1f, Plinth + 2.34f, -hz + Wall - 0.18f), 0.02f, Stone(colour * 1.06f, random, 1.5f, 0.2f));
            // The figure: the land's power as it's always been carved, seated, its hands in its lap, worn smooth.
            Vector3 seat = new Vector3(0f, Plinth + 0.75f, -hz + 0.05f);
            VertexMaterial carved = Dressed(colour * 1.12f, random, 2f);
            Kit.Span(b, seat + new Vector3(-0.32f, 0f, -0.15f), seat + new Vector3(0.32f, 0.25f, 0.2f), 0.03f, carved);
            Kit.Lathe(b, seat + new Vector3(0f, 0.25f, 0.02f), Vector3.UnitY, 0.9f, Kit.Profile((0f, 0.3f), (0.35f, 0.26f), (0.75f, 0.18f), (1f, 0.08f)), 9, 4, carved);
            Kit.Ball(b, seat + new Vector3(0f, 1.32f, 0.03f), new Vector3(0.15f, 0.18f, 0.15f), 8, 5, carved);
            // The shelf, a slab on two blocks, with what was left on it: bowls, little figures of clay, candles burnt down.
            Kit.Span(b, new Vector3(-hx + 0.4f, Plinth, -hz + Wall - 0.2f), new Vector3(-hx + 0.62f, Plinth + 0.6f, -hz + Wall + 0.3f), 0.02f, walls);
            Kit.Span(b, new Vector3(hx - 0.62f, Plinth, -hz + Wall - 0.2f), new Vector3(hx - 0.4f, Plinth + 0.6f, -hz + Wall + 0.3f), 0.02f, walls);
            Kit.Span(b, new Vector3(-hx + 0.3f, Plinth + 0.6f, -hz + Wall - 0.22f), new Vector3(hx - 0.3f, Plinth + 0.72f, -hz + Wall + 0.34f), 0.02f, Stone(colour * 1.04f, random, 1f, 0.2f));
            VertexMaterial clay = new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(0.46f, 0.3f, 0.2f));
            for (float x = -hx + 0.55f; x < hx - 0.45f; x += random.Range(0.28f, 0.42f))
            {
                if (MathF.Abs(x) < niche + 0.1f) continue;
                Vector3 at = new Vector3(x, Plinth + 0.72f, -hz + Wall + random.Range(-0.1f, 0.15f));
                switch (random.Int(3))
                {
                    case 0: Kit.Lathe(b, at, Vector3.UnitY, 0.08f, Kit.Profile((0f, 0.05f), (1f, 0.09f)), 7, 1, clay); break;
                    case 1:
                        Kit.Lathe(b, at, Vector3.UnitY, 0.2f, Kit.Profile((0f, 0.05f), (0.6f, 0.04f), (1f, 0.02f)), 6, 2, clay);
                        Kit.Ball(b, at + Vector3.UnitY * 0.24f, new Vector3(0.035f), 5, 3, clay);
                        break;
                    default: Kit.Lathe(b, at, Vector3.UnitY, random.Range(0.06f, 0.18f), _ => 0.03f, 6, 1, Wax(random)); break;
                }
            }

            // The side walls, low, and the posts: four across the front on stone bases, and one at each back corner in the wall.
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Span(b, new Vector3(s * hx - 0.22f, Plinth - 0.05f, -hz + 0.1f), new Vector3(s * hx + 0.22f, Plinth + 1.05f, hz - 0.5f), 0.03f, Stone(colour, random, 1f, 0.45f));
            }
            var posts = new List<float> { -hx + 0.1f, -hx / 3f, hx / 3f, hx - 0.1f };
            foreach (float x in posts)
            {
                Kit.Span(b, new Vector3(x - 0.28f, Plinth - 0.02f, hz - 0.58f), new Vector3(x + 0.28f, Plinth + 0.32f, hz + 0.02f), 0.04f, Stone(colour * 1.05f, random, 1f, 0.4f));
                Kit.Pole(b, new Vector3(x, Plinth + 0.3f, hz - 0.28f), new Vector3(x, postTop, hz - 0.28f), 0.17f, wood);
                // A bracket off its head, out under the eave.
                Kit.Plank(b, new Vector3(x, postTop - 0.65f, hz - 0.2f), new Vector3(x, postTop - 0.02f, hz + 0.7f), Vector3.UnitX, 0.12f, 0.12f, dark);
            }
            foreach (float s in new[] { -1f, 1f }) Kit.Pole(b, new Vector3(s * (hx - 0.1f), Plinth, -hz + 0.1f), new Vector3(s * (hx - 0.1f), postTop, -hz + 0.1f), 0.16f, wood);
            // Beams round the head of it all.
            Kit.Plank(b, new Vector3(-hx - 0.5f, postTop + 0.12f, hz - 0.28f), new Vector3(hx + 0.5f, postTop + 0.12f, hz - 0.28f), Vector3.UnitY, 0.24f, 0.26f, dark);
            Kit.Plank(b, new Vector3(-hx - 0.5f, postTop + 0.12f, -hz + 0.1f), new Vector3(hx + 0.5f, postTop + 0.12f, -hz + 0.1f), Vector3.UnitY, 0.24f, 0.26f, dark);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * (hx - 0.1f), postTop + 0.34f, -hz - 0.3f), new Vector3(s * (hx - 0.1f), postTop + 0.34f, hz + 0.3f), Vector3.UnitY, 0.22f, 0.22f, dark);

            // The roof: its eaves well out over the posts, its ridge along the hall.
            float eave = postTop + 0.42f, ex = hx + 1.25f, ez = hz + 1.2f;
            float rise = roof == 1 ? random.Range(2.6f, 3.1f) : random.Range(2.1f, 2.6f);
            float ridgeY = eave + rise, ridgeX = roof == 1 ? ex : MathF.Max(0.6f, ex - ez * 0.95f);
            const float Thick = 0.16f;
            VertexMaterial shingle = Shingles(random), shingleBack = Shingles(random, 0.92f);
            // A hole torn in its front, where the Reich pulled the shingles off: the rafters show through it.
            float holeFrom = damage >= 1 ? random.Range(-ex * 0.5f, ex * 0.05f) : 0f, holeTo = damage >= 1 ? holeFrom + random.Range(1.4f, 2.4f) : 0f;
            Vector3 Front(float x, float t) => new Vector3(x * MathHelper.Lerp(1f, ridgeX / ex, t), MathHelper.Lerp(eave, ridgeY, t), MathHelper.Lerp(ez, 0f, t));
            void Slab(Vector3 a, Vector3 c, Vector3 e, Vector3 f, VertexMaterial m)
            {
                // A slope of the roof with its thickness under it: a, c along the eave, f, e along the top, wound any way.
                Vector3 n = Vector3.Cross(c - a, f - a);
                if (n.Y < 0f) n = -n;
                n = n.Normalized() * Thick;
                Kit.Solid(b, new[] { a, c, e, f, a - n, c - n, e - n, f - n }, 0f, m);
            }
            if (damage >= 1)
            {
                Slab(Front(-ex, 0f), Front(holeFrom, 0f), Front(holeFrom, 1f), Front(-ex, 1f), shingle);
                Slab(Front(holeTo, 0f), Front(ex, 0f), Front(ex, 1f), Front(holeTo, 1f), shingle);
                // The hole: the lower course left hanging and the rafters bare above it.
                Slab(Front(holeFrom, 0f), Front(holeTo, 0f), Front(holeTo, 0.22f), Front(holeFrom, 0.22f), shingle);
                for (float x = holeFrom + 0.25f; x < holeTo; x += 0.5f) Kit.Plank(b, Front(x, 0.18f) - Vector3.UnitY * 0.1f, Front(x, 1f) - Vector3.UnitY * 0.1f, Vector3.UnitY, 0.1f, 0.12f, dark);
                Kit.Plank(b, Front(holeFrom, 0.6f) - Vector3.UnitY * 0.16f, Front(holeTo, 0.6f) - Vector3.UnitY * 0.16f, Vector3.UnitY, 0.08f, 0.06f, dark);
            }
            else Slab(Front(-ex, 0f), Front(ex, 0f), Front(ex, 1f), Front(-ex, 1f), shingle);
            Vector3 Back(float x, float t) => new Vector3(x * MathHelper.Lerp(1f, ridgeX / ex, t), MathHelper.Lerp(eave, ridgeY, t), MathHelper.Lerp(-ez, 0f, t));
            Slab(Back(ex, 0f), Back(-ex, 0f), Back(-ex, 1f), Back(ex, 1f), shingleBack);
            if (roof == 1)
            {
                // Gabled: its ends boarded, with a carved bargeboard.
                foreach (float s in new[] { -1f, 1f })
                {
                    Kit.Face(b, new Vector3(s * (hx + 0.1f), postTop + 0.2f, -hz - 0.1f), new Vector3(s * (hx + 0.1f), postTop + 0.2f, hz + 0.1f), new Vector3(s * (hx + 0.1f), ridgeY - 0.3f, 0f), new Vector3(s, 0f, 0f), Weathered(random, 0.8f));
                    Kit.Plank(b, new Vector3(s * (ex + 0.05f), eave - 0.05f, ez), new Vector3(s * (ex + 0.05f), ridgeY + 0.1f, 0f), new Vector3(s, 0f, 0f), 0.3f, 0.06f, dark);
                    Kit.Plank(b, new Vector3(s * (ex + 0.05f), eave - 0.05f, -ez), new Vector3(s * (ex + 0.05f), ridgeY + 0.1f, 0f), new Vector3(s, 0f, 0f), 0.3f, 0.06f, dark);
                }
            }
            else
            {
                // Hipped: a slope down each end too.
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 f0 = new Vector3(s * ex, eave, ez), b0 = new Vector3(s * ex, eave, -ez), top = new Vector3(s * ridgeX, ridgeY, 0f);
                    Vector3 n = Vector3.Cross(b0 - f0, top - f0);
                    if (n.Y < 0f) n = -n;
                    n = n.Normalized() * Thick;
                    Kit.Solid(b, new[] { f0, b0, top, top, f0 - n, b0 - n, top - n, top - n }, 0f, Shingles(random, 0.96f));
                }
            }
            // The ridge, capped, with its ends turned up.
            Kit.Plank(b, new Vector3(-ridgeX - 0.2f, ridgeY + 0.08f, 0f), new Vector3(ridgeX + 0.2f, ridgeY + 0.08f, 0f), Vector3.UnitY, 0.3f, 0.2f, dark);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * (ridgeX + 0.1f), ridgeY + 0.08f, 0f), new Vector3(s * (ridgeX + 0.65f), ridgeY + 0.75f, 0f), Vector3.UnitZ, 0.16f, 0.14f, dark);
            if (roof == 2)
            {
                // Eaves swept up at the corners, as the oldest halls have them.
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        Vector3 corner = new Vector3(sx * ex, eave, sz * ez);
                        Kit.Plank(b, corner - new Vector3(sx * 0.7f, 0.02f, sz * 0.7f), corner + new Vector3(sx * 0.45f, 0.55f, sz * 0.45f), Vector3.UnitY, 0.2f, 0.12f, dark);
                    }
                }
            }
            // The eave's board along the front, and what hangs off it: ribbons in the land's colours, and chimes at its corners.
            Kit.Plank(b, new Vector3(-ex, eave - 0.08f, ez + 0.02f), new Vector3(ex, eave - 0.08f, ez + 0.02f), Vector3.UnitZ, 0.22f, 0.05f, dark);
            int ribbons = random.Int(10, 16);
            for (int k = 0; k < ribbons; k++)
            {
                float x = -ex + 0.4f + (2f * ex - 0.8f) * (k + random.Range(0.1f, 0.9f)) / ribbons;
                if (damage >= 1 && x > holeFrom - 0.4f && x < holeTo + 0.4f) continue;
                if (damage >= 2 && random.Chance(0.35f)) continue;
                float len = random.Range(0.7f, 1.7f), wide = random.Range(0.1f, 0.16f);
                Vector3 top = new Vector3(x, eave - 0.18f, ez + 0.05f);
                Vector3 sway = new Vector3(random.Range(-0.12f, 0.12f), 0f, random.Range(0.02f, 0.14f));
                Vector3 colourOf = Ribbon[random.Int(Ribbon.Length)];
                Kit.Sheet(b, top + new Vector3(-wide * 0.5f, 0f, 0f), top + new Vector3(wide * 0.5f, 0f, 0f), top + new Vector3(wide * 0.5f, -len, 0f) + sway, top + new Vector3(-wide * 0.5f, -len, 0f) + sway, Cloth(colourOf), Cloth(colourOf, 0.7f));
            }
            foreach (float s in new[] { -1f, 1f }) Chimes(b, new Vector3(s * (ex - 0.25f), eave - 0.15f, ez - 0.2f), random);
            // Ribbons the Reich pulled down, lying on the plinth and the steps.
            if (damage >= 1)
            {
                for (int k = 0; k < random.Int(3, 6); k++)
                {
                    Vector3 at = new Vector3(random.Range(-hx, hx), Plinth + 0.005f, random.Range(hz - 0.2f, hz + 0.5f));
                    float yaw = random.Float() * MathF.PI;
                    Vector3 along = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw)) * random.Range(0.4f, 0.8f), across = new Vector3(-MathF.Sin(yaw), 0f, MathF.Cos(yaw)) * 0.07f;
                    Kit.Face(b, at - along - across, at + along - across, at + along + across, at - along + across, Vector3.UnitY, Cloth(Ribbon[random.Int(Ribbon.Length)], 0.85f));
                }
            }
            // The Reich's notice nailed to a post, and its paint smeared across the wall either side of the niche.
            Vector3 nail = new Vector3(posts[1], Plinth + 1.6f, hz - 0.1f);
            Kit.Face(b, nail + new Vector3(-0.17f, -0.24f, 0f), nail + new Vector3(0.17f, -0.24f, 0f), nail + new Vector3(0.17f, 0.24f, 0f), nail + new Vector3(-0.17f, 0.24f, 0f), Vector3.UnitZ, Plaster(1.03f));
            Kit.Face(b, nail + new Vector3(-0.05f, -0.19f, 0.004f), nail + new Vector3(0.05f, -0.19f, 0.004f), nail + new Vector3(0.05f, -0.09f, 0.004f), nail + new Vector3(-0.05f, -0.09f, 0.004f), Vector3.UnitZ, OxbloodPaint());
            if (damage >= 1)
            {
                float face = -hz + Wall - 0.245f;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 from = new Vector3(s * (niche + 0.4f), Plinth + 2.2f, face), to = new Vector3(s * (niche + 1.5f), Plinth + 1f, face);
                    Kit.Plank(b, from, to, Vector3.UnitZ, 0.14f, 0.01f, OxbloodPaint(0.95f), 0f);
                    Kit.Plank(b, from + new Vector3(s * 0.9f, 0f, 0f), to - new Vector3(s * 0.9f, 0f, 0f), Vector3.UnitZ, 0.12f, 0.01f, OxbloodPaint(0.9f), 0f);
                }
            }
        }

        // Chimes hung from an eave: a disc of bronze with tubes on cords under it, and a clapper.
        private static void Chimes(MeshBuilder b, Vector3 at, WorldRandom random)
        {
            VertexMaterial bronze = Bronze(random), cord = Rope(random, 0.8f);
            Kit.Pole(b, at, at - Vector3.UnitY * 0.25f, 0.008f, cord);
            Vector3 disc = at - Vector3.UnitY * 0.25f;
            Kit.Lathe(b, disc - Vector3.UnitY * 0.03f, Vector3.UnitY, 0.03f, _ => 0.16f, 8, 1, bronze);
            for (int k = 0; k < 5; k++)
            {
                float a = k * MathF.Tau / 5f;
                Vector3 hang = disc + new Vector3(MathF.Cos(a) * 0.12f, -0.05f, MathF.Sin(a) * 0.12f);
                float l = 0.3f + k * 0.07f;
                Kit.Pole(b, hang, hang - Vector3.UnitY * 0.1f, 0.004f, cord);
                Kit.Pole(b, hang - Vector3.UnitY * 0.1f, hang - Vector3.UnitY * (0.1f + l), 0.02f, bronze);
            }
            Kit.Pole(b, disc, disc - Vector3.UnitY * 0.5f, 0.004f, cord);
            Kit.Lathe(b, disc - Vector3.UnitY * 0.56f, Vector3.UnitY, 0.06f, _ => 0.05f, 6, 1, bronze);
        }

        // ---------------------------------------------------------------- The way in

        /// <summary>
        /// The gateway the shrine's path comes in by: two posts of stone on their bases, a tie beam and over it a lintel whose ends sweep up, the shrine's name carved on a board between
        /// them, ribbons and little bells hung from the lintel, and the Reich's notice nailed over the name with a stroke of its paint down a post. Numbers: span, height, stone (r, g, b).
        /// </summary>
        public static void Gate(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float span = Math.Clamp(spec.Metres(0), 2.6f, 5f), high = Math.Clamp(spec.Metres(1), 3f, 4.6f);
            Vector3 colour = StoneOfSpec(spec, 2);
            float hx = span * 0.5f;
            VertexMaterial wood = Timber(random, 0.55f), board = Weathered(random, 0.95f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (hx + 0.22f);
                Kit.Span(b, new Vector3(x - 0.42f, -0.4f, -0.42f), new Vector3(x + 0.42f, 0.38f, 0.42f), 0.05f, Stone(colour, random, 0.2f, 0.5f));
                Kit.Span(b, new Vector3(x - 0.26f, 0.3f, -0.26f), new Vector3(x + 0.26f, high - 0.05f, 0.26f), 0.035f, Stone(colour * 1.04f, random, 1.8f, 0.35f));
            }
            // The tie beam through the posts, and the lintel over them, its ends lifting.
            Kit.Plank(b, new Vector3(-hx - 0.75f, high - 0.75f, 0f), new Vector3(hx + 0.75f, high - 0.75f, 0f), Vector3.UnitY, 0.26f, 0.22f, wood);
            Kit.Plank(b, new Vector3(-hx - 0.55f, high + 0.05f, 0f), new Vector3(hx + 0.55f, high + 0.05f, 0f), Vector3.UnitY, 0.42f, 0.28f, wood);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * (hx + 0.45f), high + 0.05f, 0f), new Vector3(s * (hx + 1.35f), high + 0.42f, 0f), Vector3.UnitZ, 0.38f, 0.26f, wood);
            Kit.Plank(b, new Vector3(-hx - 1.3f, high + 0.28f, 0f), new Vector3(hx + 1.3f, high + 0.28f, 0f), Vector3.UnitY, 0.52f, 0.1f, Shingles(random));
            // The name, carved, between the beams.
            Kit.Span(b, new Vector3(-0.42f, high - 0.62f, -0.06f), new Vector3(0.42f, high - 0.05f, 0.06f), 0.01f, board);
            for (int k = 0; k < 3; k++) Kit.Plank(b, new Vector3(-0.3f + k * 0.3f, high - 0.55f, 0.065f), new Vector3(-0.3f + k * 0.3f, high - 0.15f, 0.065f), Vector3.UnitZ, 0.05f, 0.004f, Fresh(random, 0.7f), 0f);
            // The Reich's notice nailed over it, and its paint down the left post.
            Kit.Face(b, new Vector3(-0.3f, high - 0.5f, 0.08f), new Vector3(0.24f, high - 0.56f, 0.08f), new Vector3(0.28f, high - 0.12f, 0.08f), new Vector3(-0.26f, high - 0.06f, 0.08f), Vector3.UnitZ, Plaster(1.04f));
            Kit.Face(b, new Vector3(-0.04f, high - 0.46f, 0.085f), new Vector3(0.06f, high - 0.46f, 0.085f), new Vector3(0.06f, high - 0.36f, 0.085f), new Vector3(-0.04f, high - 0.36f, 0.085f), Vector3.UnitZ, OxbloodPaint());
            Kit.Plank(b, new Vector3(-hx - 0.22f, high - 0.9f, 0.27f), new Vector3(-hx - 0.18f, 0.6f, 0.27f), Vector3.UnitZ, 0.16f, 0.01f, OxbloodPaint(0.9f), 0f);
            // Ribbons and bells under the lintel.
            VertexMaterial bronze = Bronze(random);
            for (float x = -hx + 0.25f; x < hx; x += random.Range(0.32f, 0.5f))
            {
                if (MathF.Abs(x) < 0.5f) continue;
                Vector3 top = new Vector3(x, high - 0.88f, random.Range(-0.08f, 0.08f));
                if (random.Chance(0.4f))
                {
                    Kit.Pole(b, top, top - Vector3.UnitY * 0.3f, 0.006f, Rope(random));
                    Kit.Lathe(b, top - Vector3.UnitY * 0.46f, Vector3.UnitY, 0.16f, Kit.Profile((0f, 0.09f), (0.5f, 0.07f), (1f, 0.02f)), 7, 2, bronze);
                }
                else
                {
                    float len = random.Range(0.6f, 1.2f);
                    Vector3 c = Ribbon[random.Int(Ribbon.Length)];
                    Kit.Sheet(b, top - Vector3.UnitX * 0.06f, top + Vector3.UnitX * 0.06f, top + new Vector3(0.06f, -len, random.Range(0f, 0.1f)), top + new Vector3(-0.06f, -len, random.Range(0f, 0.1f)), Cloth(c), Cloth(c, 0.7f));
                }
            }
        }

        /// <summary>
        /// A tall lantern of stone on the way to the shrine: a base, a shaft, a firebox with its openings dark, as the Reich keeps them out, and a roof with a knob on it. Knocked over, it
        /// lies in the pieces it broke into; or its roof alone lies beside it. Numbers: height, form (0 standing, 1 knocked down, 2 its roof knocked off), stone (r, g, b).
        /// </summary>
        public static void Lantern(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float high = Math.Clamp(spec.Metres(0), 1.8f, 3.4f);
            int form = Math.Clamp(spec.Int(1), 0, 2);
            Vector3 colour = StoneOfSpec(spec, 2);
            float s = high / 2.6f;
            Func<Vector3, VertexMaterial> stone = Stone(colour, random, 1f, 0.5f);
            VertexMaterial plain = Dressed(colour, random, 1.2f);
            // Its parts, from the foot up, each about its own middle, so a knocked-down one can lie them anywhere.
            void Base(Vector3 at, Quaternion q)
            {
                Kit.Box(b, at, Vector3.Transform(Vector3.UnitX, q), Vector3.Transform(Vector3.UnitY, q), Vector3.Transform(Vector3.UnitZ, q), new Vector3(0.48f, 0.16f, 0.48f) * s, 0.04f, stone);
            }
            void Shaft(Vector3 foot, Vector3 axis) => Kit.Lathe(b, foot, axis, 0.95f * s, Kit.Profile((0f, 0.17f * s), (0.5f, 0.14f * s), (1f, 0.16f * s)), 6, 2, plain);
            void Platform(Vector3 at, Quaternion q) => Kit.Box(b, at, Vector3.Transform(Vector3.UnitX, q), Vector3.Transform(Vector3.UnitY, q), Vector3.Transform(Vector3.UnitZ, q), new Vector3(0.36f, 0.07f, 0.36f) * s, 0.025f, stone);
            void Firebox(Vector3 at, Quaternion q)
            {
                Vector3 ax = Vector3.Transform(Vector3.UnitX, q), ay = Vector3.Transform(Vector3.UnitY, q), az = Vector3.Transform(Vector3.UnitZ, q);
                Kit.Box(b, at, ax, ay, az, new Vector3(0.15f, 0.2f, 0.15f) * s, 0f, Dark);
                foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Kit.Box(b, at + (ax * sx + az * sz) * (0.2f * s), ax, ay, az, new Vector3(0.06f, 0.21f, 0.06f) * s, 0.01f, plain);
            }
            void Roof(Vector3 at, Quaternion q)
            {
                Vector3 ay = Vector3.Transform(Vector3.UnitY, q);
                Kit.Lathe(b, at, ay, 0.34f * s, Kit.Profile((0f, 0.46f * s), (0.25f, 0.44f * s), (1f, 0.06f * s)), 6, 2, stone(ay));
                Kit.Lathe(b, at + ay * (0.32f * s), ay, 0.2f * s, Kit.Profile((0f, 0.06f * s), (0.5f, 0.1f * s), (1f, 0f)), 6, 2, plain);
            }
            Quaternion upright = Quaternion.Identity;
            if (form == 1)
            {
                // Pushed over: its base still where it stood, the rest broken off and fallen the way it was pushed.
                Base(new Vector3(0f, 0.08f * s, 0f), upright);
                float fall = random.Float() * MathF.Tau;
                Vector3 way = new Vector3(MathF.Cos(fall), 0f, MathF.Sin(fall));
                Vector3 side = Vector3.Cross(Vector3.UnitY, way);
                Shaft(new Vector3(0f, Ground + 0.17f * s, 0f) + way * (0.2f * s), way);
                Quaternion lying = Quaternion.FromAxisAngle(side, MathF.PI * 0.5f) * Quaternion.FromAxisAngle(Vector3.UnitY, random.Float());
                Platform(new Vector3(0f, Ground + 0.3f * s, 0f) + way * (1.3f * s) + side * 0.1f, lying);
                Firebox(new Vector3(0f, Ground + 0.2f * s, 0f) + way * (1.75f * s) + side * random.Range(-0.4f, 0.4f), lying);
                Roof(new Vector3(0f, Ground + 0.1f, 0f) + way * (2.4f * s) + side * random.Range(-0.6f, 0.6f), Quaternion.FromAxisAngle(side, random.Range(1.2f, 2.2f)));
                return;
            }
            Base(new Vector3(0f, 0.08f * s, 0f), upright);
            Shaft(new Vector3(0f, 0.24f * s, 0f), Vector3.UnitY);
            Platform(new Vector3(0f, 1.25f * s, 0f), upright);
            Firebox(new Vector3(0f, 1.52f * s, 0f), upright);
            if (form == 2)
            {
                float fall = random.Float() * MathF.Tau;
                Vector3 way = new Vector3(MathF.Cos(fall), 0f, MathF.Sin(fall));
                Roof(new Vector3(0f, Ground + 0.12f, 0f) + way * random.Range(0.9f, 1.4f), Quaternion.FromAxisAngle(Vector3.Cross(Vector3.UnitY, way), random.Range(1.4f, 2f)));
                return;
            }
            Platform(new Vector3(0f, 1.79f * s, 0f), upright);
            Roof(new Vector3(0f, 1.84f * s, 0f), upright);
        }

        // ---------------------------------------------------------------- What was left, and what's always been there

        /// <summary>
        /// The table offerings are left on: a slab of stone on two legs, bowls on it with fruit in them, coins, little figures of clay, flowers and candles burnt down; or kicked over by the
        /// Reich, the slab tipped off its legs and all of it in the grass. Numbers: form (0 kept, 1 kicked over), stone (r, g, b).
        /// </summary>
        public static void Offerings(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            bool kicked = spec.Int(0) == 1;
            Vector3 colour = StoneOfSpec(spec, 1);
            Func<Vector3, VertexMaterial> stone = Stone(colour, random, 0.6f, 0.45f);
            VertexMaterial clay = new VertexMaterial(MaterialCatalog.Plaster.Index, new Colour(0.46f, 0.3f, 0.2f));
            const float Top = 0.72f;
            foreach (float x in new[] { -0.55f, 0.55f }) Kit.Span(b, new Vector3(x - 0.14f, -0.2f, -0.24f), new Vector3(x + 0.14f, Top - 0.1f, 0.24f), 0.03f, stone);
            Vector3 slabCentre = kicked ? new Vector3(0.2f, Ground + 0.2f, 0.55f) : new Vector3(0f, Top - 0.05f, 0f);
            Quaternion slab = kicked ? Quaternion.FromAxisAngle(Vector3.UnitX, 0.55f) * Quaternion.FromAxisAngle(Vector3.UnitY, 0.2f) : Quaternion.Identity;
            Kit.Box(b, slabCentre, Vector3.Transform(Vector3.UnitX, slab), Vector3.Transform(Vector3.UnitY, slab), Vector3.Transform(Vector3.UnitZ, slab), new Vector3(0.82f, 0.06f, 0.36f), 0.03f, stone);
            Vector3[] fruit = { new(0.62f, 0.14f, 0.08f), new(0.66f, 0.42f, 0.1f), new(0.3f, 0.42f, 0.12f), new(0.44f, 0.12f, 0.2f) };
            int items = random.Int(9, 15);
            for (int k = 0; k < items; k++)
            {
                Vector3 at = kicked
                    ? new Vector3(random.Range(-1.4f, 1.6f), Ground, random.Range(0.3f, 1.9f))
                    : new Vector3(random.Range(-0.7f, 0.7f), Top + 0.01f, random.Range(-0.26f, 0.26f));
                switch (random.Int(5))
                {
                    case 0:
                        // A bowl, and fruit in it (or by it, kicked out of it).
                        Kit.Lathe(b, at, Vector3.UnitY, 0.07f, Kit.Profile((0f, 0.06f), (1f, 0.12f)), 8, 1, clay);
                        for (int f = 0; f < 3; f++)
                        {
                            Vector3 by = kicked ? at + new Vector3(random.Range(-0.4f, 0.4f), 0.04f, random.Range(-0.4f, 0.4f)) : at + new Vector3(random.Range(-0.05f, 0.05f), 0.08f + f * 0.02f, random.Range(-0.05f, 0.05f));
                            Kit.Ball(b, by, new Vector3(0.045f), 6, 4, Flat(fruit[random.Int(fruit.Length)]));
                        }
                        break;
                    case 1:
                        Kit.Lathe(b, at + (kicked ? Vector3.UnitY * 0.03f : Vector3.Zero), kicked ? new Vector3(random.Range(-1f, 1f), 0.1f, random.Range(-1f, 1f)).Normalized() : Vector3.UnitY, 0.18f, Kit.Profile((0f, 0.05f), (0.6f, 0.04f), (1f, 0.02f)), 6, 2, clay);
                        break;
                    case 2:
                        // Coins in a little heap.
                        for (int c = 0; c < 4; c++) Kit.Lathe(b, at + new Vector3(random.Range(-0.04f, 0.04f), c * 0.006f, random.Range(-0.04f, 0.04f)), Vector3.UnitY, 0.005f, _ => 0.022f, 6, 1, new VertexMaterial(MaterialCatalog.Iron.Index, new Colour(0.5f, 0.4f, 0.2f)));
                        break;
                    case 3:
                        // Flowers, gone over.
                        for (int f = 0; f < 3; f++)
                        {
                            float a = random.Float() * MathF.Tau;
                            Vector3 head = at + new Vector3(MathF.Cos(a) * 0.25f, 0.03f, MathF.Sin(a) * 0.25f);
                            Kit.Plank(b, at + Vector3.UnitY * 0.02f, head, Vector3.UnitY, 0.012f, 0.008f, Flat(new Vector3(0.22f, 0.3f, 0.12f)), 0f);
                            Kit.Ball(b, head, new Vector3(0.035f, 0.02f, 0.035f), 5, 3, Flat(Ribbon[random.Int(Ribbon.Length)] * 1.2f));
                        }
                        break;
                    default:
                        Kit.Lathe(b, at, kicked ? Vector3.UnitX : Vector3.UnitY, random.Range(0.05f, 0.14f), _ => 0.028f, 6, 1, Wax(random));
                        break;
                }
            }
        }

        /// <summary>A basin cut from one block, water standing in it from the spout over it on its post, and ladles across its rim. Numbers: stone (r, g, b).</summary>
        public static void Basin(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            Vector3 colour = StoneOfSpec(spec, 0);
            Func<Vector3, VertexMaterial> stone = Stone(colour, random, 0.5f, 0.6f);
            const float Hx = 0.85f, Hz = 0.5f, Top = 0.82f, Rim = 0.14f;
            Kit.Span(b, new Vector3(-Hx, -0.3f, -Hz), new Vector3(Hx, Top - 0.32f, Hz), 0.04f, stone);
            Kit.Span(b, new Vector3(-Hx, Top - 0.32f, -Hz), new Vector3(Hx, Top, -Hz + Rim), 0.03f, stone);
            Kit.Span(b, new Vector3(-Hx, Top - 0.32f, Hz - Rim), new Vector3(Hx, Top, Hz), 0.03f, stone);
            Kit.Span(b, new Vector3(-Hx, Top - 0.32f, -Hz + Rim), new Vector3(-Hx + Rim, Top, Hz - Rim), 0.03f, stone);
            Kit.Span(b, new Vector3(Hx - Rim, Top - 0.32f, -Hz + Rim), new Vector3(Hx, Top, Hz - Rim), 0.03f, stone);
            Kit.Face(b, new Vector3(-Hx + Rim, Top - 0.06f, -Hz + Rim), new Vector3(Hx - Rim, Top - 0.06f, -Hz + Rim), new Vector3(Hx - Rim, Top - 0.06f, Hz - Rim), new Vector3(-Hx + Rim, Top - 0.06f, Hz - Rim), Vector3.UnitY, new VertexMaterial(MaterialCatalog.Mirror.Index, new Colour(0.05f, 0.07f, 0.07f)));
            // The spout on its post behind it, and the ladles.
            VertexMaterial wood = Weathered(random, 0.8f);
            Kit.Span(b, new Vector3(-Hx + 0.1f, -0.3f, -Hz - 0.3f), new Vector3(-Hx + 0.3f, 1.5f, -Hz - 0.1f), 0.015f, wood);
            Kit.Pole(b, new Vector3(-Hx + 0.2f, 1.35f, -Hz - 0.2f), new Vector3(-Hx + 0.75f, 1.2f, -Hz + 0.15f), 0.05f, Bleached(random, 0.85f));
            for (int k = 0; k < 2; k++)
            {
                float x = 0.1f + k * 0.35f;
                Kit.Pole(b, new Vector3(x, Top + 0.03f, -Hz + 0.05f), new Vector3(x + 0.05f, Top + 0.03f, Hz - 0.05f), 0.012f, wood);
                Kit.Lathe(b, new Vector3(x + 0.05f, Top - 0.02f, Hz - 0.05f), Vector3.UnitY, 0.07f, Kit.Profile((0f, 0.04f), (1f, 0.05f)), 6, 1, wood);
            }
        }

        /// <summary>
        /// The shrine's bell in its frame, two posts and a beam under a little roof: hung, or cut down by the Reich, its rope cut and the bell lying cracked in the grass under it.
        /// Numbers: form (0 hung, 1 cut down).
        /// </summary>
        public static void Bell(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            bool cut = spec.Int(0) == 1;
            VertexMaterial wood = Timber(random, 0.6f), bronze = Bronze(random, 0.7f);
            const float High = 2.9f, Hx = 0.9f;
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Span(b, new Vector3(s * Hx - 0.11f, -0.4f, -0.11f), new Vector3(s * Hx + 0.11f, High, 0.11f), 0.02f, wood);
                Kit.Plank(b, new Vector3(s * Hx, 0f, -0.55f), new Vector3(s * Hx, 1f, 0f), Vector3.UnitX, 0.1f, 0.1f, wood);
                Kit.Plank(b, new Vector3(s * Hx, 0f, 0.55f), new Vector3(s * Hx, 1f, 0f), Vector3.UnitX, 0.1f, 0.1f, wood);
            }
            Kit.Plank(b, new Vector3(-Hx - 0.25f, High - 0.08f, 0f), new Vector3(Hx + 0.25f, High - 0.08f, 0f), Vector3.UnitY, 0.18f, 0.2f, wood);
            foreach (float z in new[] { -1f, 1f }) Kit.Solid(b, new[]
            {
                new Vector3(-Hx - 0.45f, High, z * 0.65f), new Vector3(Hx + 0.45f, High, z * 0.65f), new Vector3(Hx + 0.45f, High + 0.55f, 0f), new Vector3(-Hx - 0.45f, High + 0.55f, 0f),
                new Vector3(-Hx - 0.45f, High - 0.1f, z * 0.65f), new Vector3(Hx + 0.45f, High - 0.1f, z * 0.65f), new Vector3(Hx + 0.45f, High + 0.45f, 0f), new Vector3(-Hx - 0.45f, High + 0.45f, 0f),
            }, 0f, Shingles(random));
            Func<float, float> bell = Kit.Profile((0f, 0.42f), (0.12f, 0.38f), (0.55f, 0.27f), (0.85f, 0.24f), (1f, 0.1f));
            if (!cut)
            {
                Kit.Pole(b, new Vector3(0f, High - 0.18f, 0f), new Vector3(0f, High - 0.38f, 0f), 0.04f, Iron(random, 1.4f));
                Kit.Lathe(b, new Vector3(0f, High - 1.25f, 0f), Vector3.UnitY, 0.88f, bell, 10, 4, bronze);
                Kit.Pole(b, new Vector3(0f, High - 1.2f, 0f), new Vector3(0.1f, 0.5f, 0.3f), 0.02f, Rope(random));
                return;
            }
            // Cut down: the rope's end hanging from the beam, and the bell on its side, a crack down it.
            Kit.Pole(b, new Vector3(0f, High - 0.18f, 0f), new Vector3(0f, High - 0.7f, 0f), 0.02f, Rope(random));
            Vector3 lie = new Vector3(0.25f, Ground + 0.4f, 0.55f);
            Vector3 axis = new Vector3(0.9f, -0.1f, 0.4f).Normalized();
            Kit.Lathe(b, lie - axis * 0.4f, axis, 0.88f, bell, 10, 4, bronze);
            Kit.Plank(b, lie - axis * 0.3f + Vector3.UnitY * 0.38f, lie + axis * 0.3f + Vector3.UnitY * 0.28f, Vector3.UnitY, 0.03f, 0.02f, Dark, 0f);
        }

        /// <summary>Ribbons wound round the trunk of a tree that's always been there, tokens of wood hung off them on cords, as the land ties its wishes to it. Numbers: the trunk's radius, how high.</summary>
        public static void Ribbons(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float radius = Math.Clamp(spec.Metres(0), 0.12f, 1.2f), high = Math.Clamp(spec.Metres(1), 1f, 3.2f);
            int bands = random.Int(4, 7);
            for (int k = 0; k < bands; k++)
            {
                float y = 0.9f + (high - 0.9f) * (k + random.Range(0.1f, 0.9f)) / bands;
                float h = random.Range(0.08f, 0.16f);
                Vector3 c = Ribbon[random.Int(Ribbon.Length)];
                b.Tube(new Vector3(0f, y, 0f), new Vector3(random.Range(-0.02f, 0.02f), y + h, random.Range(-0.02f, 0.02f)), radius + 0.025f, radius + 0.025f, 10, Cloth(c), capped: false);
                // Its loose ends, hanging.
                float a = random.Float() * MathF.Tau;
                Vector3 at = new Vector3(MathF.Cos(a) * (radius + 0.03f), y, MathF.Sin(a) * (radius + 0.03f));
                Vector3 outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                float len = random.Range(0.4f, 0.9f);
                Kit.Sheet(b, at, at + Vector3.Cross(Vector3.UnitY, outward) * 0.1f, at + Vector3.Cross(Vector3.UnitY, outward) * 0.1f - Vector3.UnitY * len + outward * 0.1f, at - Vector3.UnitY * len + outward * 0.08f, Cloth(c), Cloth(c, 0.7f));
            }
            // Tokens on cords: little tablets of wood, some painted.
            int tokens = random.Int(5, 9);
            for (int k = 0; k < tokens; k++)
            {
                float a = random.Float() * MathF.Tau, y = random.Range(1.2f, high);
                Vector3 outward = new Vector3(MathF.Cos(a), 0f, MathF.Sin(a));
                Vector3 at = outward * (radius + 0.05f) + Vector3.UnitY * y;
                float drop = random.Range(0.15f, 0.4f);
                Kit.Pole(b, at, at - Vector3.UnitY * drop + outward * 0.03f, 0.005f, Rope(random));
                Vector3 c = random.Chance(0.5f) ? Ribbon[random.Int(Ribbon.Length)] : new Vector3(0.55f, 0.45f, 0.32f);
                Kit.Box(b, at - Vector3.UnitY * (drop + 0.07f) + outward * 0.05f, Vector3.Cross(Vector3.UnitY, outward), Vector3.UnitY, outward, new Vector3(0.05f, 0.07f, 0.01f), 0f, Painted(c));
            }
        }

        /// <summary>Little cairns of stones left by whoever came, each a few flat stones on each other, smallest on top. Numbers: how many, how far they spread, stone (r, g, b).</summary>
        public static void Cairns(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            int count = Math.Clamp(spec.Int(0), 1, 12);
            float spread = Math.Clamp(spec.Metres(1), 0.4f, 4f);
            Vector3 colour = StoneOfSpec(spec, 2);
            for (int k = 0; k < count; k++)
            {
                float a = random.Float() * MathF.Tau, r = spread * MathF.Sqrt(random.Float());
                Vector3 foot = new Vector3(MathF.Cos(a) * r, Ground - 0.02f, MathF.Sin(a) * r);
                int stones = random.Int(3, 6);
                float size = random.Range(0.14f, 0.22f);
                for (int s = 0; s < stones; s++)
                {
                    float f = 1f - s * 0.16f;
                    Vector3 half = new Vector3(size * f * random.Range(0.9f, 1.25f), size * f * 0.32f, size * f * random.Range(0.75f, 1f));
                    Kit.Block(b, foot + Vector3.UnitY * half.Y, half, random.Float() * MathF.Tau, MathF.Min(0.03f, half.Y * 0.4f), Stone(colour * random.Range(0.85f, 1.12f), random, 0.4f, 0.5f));
                    foot += new Vector3(random.Range(-0.02f, 0.02f), half.Y * 2f, random.Range(-0.02f, 0.02f));
                }
            }
        }

        private static VertexMaterial Wax(WorldRandom random) => new VertexMaterial(MaterialCatalog.Wax.Index, new Colour(new Vector3(0.82f, 0.78f, 0.68f) * random.Range(0.9f, 1.02f)));
    }
}
