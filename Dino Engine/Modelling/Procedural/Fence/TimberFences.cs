using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>Fences of worked wood: split rails, palings and wattle. A character is 4.6 m tall here, so a fence a person leans on is near 3 m.</summary>
    internal static class TimberFences
    {
        private static readonly float[] RailStations = { 0f, 0.045f, 0.25f, 0.5f, 0.75f, 0.955f, 1f };
        private static readonly float[] Ends = { 0f, 1f };

        /// <summary>
        /// Split rails let into hewn posts, as fields are fenced where timber's cheap. A rail runs into each post's middle, tapering as a tenon does, so it meets any post of its
        /// style, at a corner or a gate as much as along a run.
        /// </summary>
        public static void Rails(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float height = style.Range("post height", 2.6f, 3.1f);
            float postHalf = style.Range("post half", 0.14f, 0.18f);
            bool roundPosts = style.Chance("round posts", 0.35f);
            int rails = 2 + style.Pick("rails", 0.15f, 0.6f, 0.25f);
            float bottom = style.Range("bottom rail", 0.62f, 0.85f);
            float top = height - style.Range("top rail below", 0.3f, 0.45f);
            float railHalfHeight = style.Range("rail height", 0.12f, 0.155f);
            float railHalfWidth = style.Range("rail width", 0.085f, 0.11f);
            float sag = style.Range("sag", 0.01f, 0.05f);
            int cut = style.Pick("post top", 0.3f, 0.45f, 0.25f);
            Vector3 wood = FenceLook.Weathered(style);
            var surface = new Surface(FenceMaterials.Weathered, wood, 1.4f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float half = postHalf * (job.IsEnd ? 1.3f : 1f) * random.Range(0.93f, 1.07f);
                float tall = height + (job.IsEnd ? 0.22f : 0f) + random.Range(-0.05f, 0.05f);
                Post(b, half, tall, job.Sink, roundPosts, cut, surface.Tinted(random.Range(0.9f, 1.08f)), random);
                return;
            }

            WorldRandom railRandom = job.Own.Stream("rails");
            for (int k = 0; k < rails; k++)
            {
                float y = MathHelper.Lerp(bottom, top, k / (float)(rails - 1));
                Vector2[] section = FenceLook.Split(railHalfWidth, railHalfHeight, railRandom);
                float bend = sag * railRandom.Range(0.6f, 1.4f);
                float twist = railRandom.Range(-0.35f, 0.35f);
                float wander = railRandom.Range(-0.03f, 0.03f);
                Surface rail = surface.Tinted(railRandom.Range(0.84f, 1.12f));
                b.Prism(new Vector3(0f, y, 0f), new Vector3(job.Bay, y, 0f), Vector3.UnitY, section, rail, RailStations, t =>
                {
                    float middle = MathF.Sin(MathF.PI * t);
                    float scale = t <= 0f || t >= 1f ? 0.62f : 1f;
                    return (new Vector3(0f, -bend * middle, wander * middle), scale, twist * (t - 0.5f));
                }, capFoot: false, capHead: false);
            }
        }

        // A post hewn square with an axe, or left round, and cut off flat, on a slant or to a ridge to shed the rain.
        private static void Post(FenceBuilder b, float half, float tall, float sink, bool round, int cut, Surface surface, WorldRandom random)
        {
            Vector3 grain = FenceLook.EndGrain(surface.Colour);
            if (round)
            {
                b.Log(new Vector3(0f, -sink, 0f), new Vector3(0f, tall, 0f), half * 1.05f, half * 0.94f, 9, surface, (uint)random.Int(int.MaxValue), headEnd: LogEnd.Cut, rough: 0.07f, rings: 3, endColour: grain);
                return;
            }

            Vector2[] section = FenceLook.Hewn(half, random, 0.18f, 0.42f);
            float slant = random.Range(0.25f, 0.45f) * (random.Chance(0.5f) ? 1f : -1f);
            Func<Vector2, float>? top = cut switch
            {
                1 => p => p.X * slant,
                2 => p => (half - MathF.Abs(p.X)) * 0.7f,
                _ => null,
            };
            b.Prism(new Vector3(0f, -sink, 0f), new Vector3(0f, tall, 0f), Vector3.UnitZ, section, surface, Ends, capFoot: false, endColour: grain, headCut: top);
        }

        /// <summary>
        /// A paling: pales nailed to two rails on the outer face of square posts, pointed, rounded, clipped or cut flat, sometimes limewashed. They're spaced half a pitch in from
        /// each end of a span, so the rhythm runs on across a post as if it weren't there.
        /// </summary>
        public static void Paling(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float postHeight = style.Range("post height", 2.65f, 2.95f);
            float postHalf = style.Range("post half", 0.1f, 0.125f);
            float paleHeight = style.Range("pale height", 2.2f, 2.5f);
            float paleWidth = style.Range("pale width", 0.15f, 0.21f);
            float gap = style.Range("gap", 0.05f, 0.1f);
            float paleThick = style.Range("pale thickness", 0.045f, 0.06f);
            float paleFoot = style.Range("pale foot", 0.08f, 0.2f);
            int shape = style.Pick("pale top", 0.5f, 0.2f, 0.15f, 0.15f);
            bool capped = style.Chance("post caps", 0.5f);
            bool limed = style.Chance("limewash", 0.3f);
            Vector3 colour = limed ? new Vector3(0.76f, 0.75f, 0.7f) * style.Range("lime shade", 0.9f, 1.03f) : FenceLook.Weathered(style);
            var surface = new Surface(FenceMaterials.Weathered, colour, 1.2f);
            const float RailHalfHeight = 0.075f, RailHalfDepth = 0.04f;
            float railLow = paleFoot + style.Range("low rail", 0.35f, 0.5f);
            float railHigh = paleHeight - style.Range("high rail below", 0.4f, 0.55f);
            float railZ = -(postHalf + RailHalfDepth);
            float paleZ = -(postHalf + RailHalfDepth * 2f + paleThick * 0.5f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float half = postHalf * (job.IsEnd ? 1.3f : 1f);
                float tall = postHeight + (job.IsEnd ? 0.25f : 0f) + random.Range(-0.04f, 0.04f);
                Surface post = surface.Tinted(random.Range(0.88f, 1.06f));
                b.Block(new Vector3(0f, (tall - job.Sink) * 0.5f, 0f), new Vector3(half, (tall + job.Sink) * 0.5f, half), Matrix3.Identity, 0.014f, post, Faces.NegativeY);
                if (capped || job.IsEnd)
                {
                    Vector3 apex = new Vector3(0f, tall + half * 0.95f, 0f);
                    Vector3[] rim = { new(-half, tall, -half), new(half, tall, -half), new(half, tall, half), new(-half, tall, half) };
                    for (int i = 0; i < 4; i++) b.Triangle(rim[i], rim[(i + 1) % 4], apex, new Vector3(0f, tall, 0f), post.Tinted(1.12f));
                }
                return;
            }

            foreach (float y in new[] { railLow, railHigh })
            {
                b.Block(new Vector3(job.Bay * 0.5f, y, railZ), new Vector3(job.Bay * 0.5f, RailHalfHeight, RailHalfDepth), Matrix3.Identity, 0.01f, surface.Tinted(0.9f));
            }

            WorldRandom pales = job.Own.Stream("pales");
            int count = Math.Max(1, (int)MathF.Round(job.Bay / (paleWidth + gap)));
            float pitch = job.Bay / count;
            for (int i = 0; i < count; i++)
            {
                float x = (i + 0.5f) * pitch;
                float w = paleWidth * pales.Range(0.9f, 1.1f);
                float h = paleHeight + pales.Range(-0.07f, 0.07f);
                Vector2[] outline = Pale(shape, w, h, paleFoot);
                int mark = b.Mark;
                float lean = pales.Range(-0.02f, 0.02f);
                b.Prism(new Vector3(x, 0f, paleZ - paleThick * 0.5f), new Vector3(x, 0f, paleZ + paleThick * 0.5f), Vector3.UnitY, outline, surface.Tinted(pales.Range(0.86f, 1.1f)), Ends,
                    t => (Vector3.Zero, 1f, lean), endColour: colour * pales.Range(0.86f, 1.1f));
                b.Upright(mark, x);
            }
        }

        // The face of a pale, from its foot to its top, about its middle.
        private static Vector2[] Pale(int shape, float w, float h, float foot)
        {
            float half = w * 0.5f;
            return shape switch
            {
                0 => new Vector2[] { new(-half, foot), new(half, foot), new(half, h - w * 0.6f), new(0f, h), new(-half, h - w * 0.6f) },
                1 => new Vector2[] { new(-half, foot), new(half, foot), new(half, h - w * 0.45f), new(half * 0.7f, h - w * 0.13f), new(0f, h), new(-half * 0.7f, h - w * 0.13f), new(-half, h - w * 0.45f) },
                2 => new Vector2[] { new(-half, foot), new(half, foot), new(half, h), new(-half, h) },
                _ => new Vector2[] { new(-half, foot), new(half, foot), new(half, h - w * 0.28f), new(half * 0.45f, h), new(-half * 0.45f, h), new(-half, h - w * 0.28f) },
            };
        }

        /// <summary>
        /// Wattle: hurdles of rods woven in and out of upright stakes, each band of rods going the other way round from the one below, lashed to a stout post at each end.
        /// From above it's the weave along the top that says what it is.
        /// </summary>
        public static void Wattle(in FenceJob job)
        {
            Genes style = job.Style;
            FenceBuilder b = job.B;
            float height = style.Range("height", 2.3f, 2.65f);
            float pitchWanted = style.Range("stake pitch", 0.36f, 0.46f);
            float bandWanted = style.Range("band", 0.17f, 0.23f);
            float rod = style.Range("rod", 0.06f, 0.075f);
            float stake = style.Range("stake", 0.035f, 0.045f);
            float postRadius = style.Range("post", 0.075f, 0.09f);
            Vector3 hazel = Vector3.Lerp(new Vector3(0.36f, 0.29f, 0.22f), new Vector3(0.32f, 0.28f, 0.23f), style.Unit("grey")) * style.Range("shade", 0.86f, 1.06f);
            Vector3 peeled = new Vector3(0.56f, 0.48f, 0.36f);
            var cane = new Surface(FenceMaterials.Cane, hazel, 0.8f);

            if (!job.IsSpan)
            {
                WorldRandom random = job.Own.Stream("post");
                float radius = postRadius * (job.IsEnd ? 1.25f : 1f) * random.Range(0.92f, 1.08f);
                float tall = height + (job.IsEnd ? 0.38f : 0.26f) + random.Range(-0.04f, 0.04f);
                Surface post = cane.Tinted(random.Range(0.82f, 0.98f));
                b.Log(new Vector3(0f, -job.Sink - 0.2f, 0f), new Vector3(random.Range(-0.02f, 0.02f), tall, random.Range(-0.02f, 0.02f)), radius, radius * 0.88f, 8, post,
                    (uint)random.Int(int.MaxValue), headEnd: LogEnd.Cut, rough: 0.1f, rings: 3, endColour: FenceLook.EndGrain(hazel));
                var cord = new Surface(FenceMaterials.Cord, FenceLook.Cord * random.Range(0.85f, 1.1f), 0.5f);
                float[] lashings = job.IsEnd ? new[] { height - 0.12f, height * 0.45f } : new[] { height - 0.12f };
                foreach (float y in lashings)
                {
                    b.Log(new Vector3(0f, y - 0.05f, 0f), new Vector3(0f, y + 0.05f, 0f), radius + 0.028f, radius + 0.028f, 8, cord, 7u, LogEnd.Cut, LogEnd.Cut, rough: 0.05f, rings: 1);
                }
                return;
            }

            WorldRandom weave = job.Own.Stream("weave");
            int stakes = Math.Max(2, (int)MathF.Round(job.Bay / pitchWanted));
            float pitch = job.Bay / stakes;
            int bands = Math.Max(4, (int)MathF.Round((height - 0.06f) / bandWanted));
            float band = (height - 0.06f) / bands;
            float reach = stake + rod * 0.5f;
            int segments = stakes * 4;

            for (int j = 0; j < bands; j++)
            {
                float phase = j % 2 == 0 ? 0f : MathF.PI;
                float low = 0.04f + j * band, high = low + band - 0.006f;
                Vector3 colour = weave.Chance(0.12f) ? peeled * weave.Range(0.9f, 1.08f) : hazel * weave.Range(0.82f, 1.14f);
                float wobble = weave.Range(0f, 100f);
                var rods = new Surface(FenceMaterials.Cane, colour, 0.8f);
                float Z(float x) => reach * MathF.Cos(MathF.PI * x / pitch + phase);
                float Slope(float x) => -reach * MathF.PI / pitch * MathF.Sin(MathF.PI * x / pitch + phase);
                float Lift(float x) => 0.012f * FloraNoise.Wave(x * 1.4f + wobble, 3u);

                foreach (float side in new[] { -1f, 1f })
                {
                    int first = b.Mark;
                    for (int s = 0; s <= segments; s++)
                    {
                        float x = job.Bay * s / segments;
                        float z = Z(x) + side * rod * 0.5f;
                        Vector3 normal = side * new Vector3(-Slope(x), 0f, 1f).Normalized();
                        float lift = Lift(x);
                        b.Mesh.Add(new FloraVertex { Position = new Vector3(x, low + lift, z), Normal = (normal + new Vector3(0f, -0.55f, 0f)).Normalized(), Tangent = Vector3.UnitX, Colour = colour * 0.82f, UV = new Vector2(x / rods.Tile, 0f), Material = rods.Material, Joint = -1 });
                        b.Mesh.Add(new FloraVertex { Position = new Vector3(x, high + lift, z), Normal = (normal + new Vector3(0f, 0.55f, 0f)).Normalized(), Tangent = Vector3.UnitX, Colour = colour, UV = new Vector2(x / rods.Tile, band / rods.Tile), Material = rods.Material, Joint = -1 });
                    }
                    for (int s = 0; s < segments; s++)
                    {
                        int a = first + s * 2;
                        if (side > 0f)
                        {
                            b.Mesh.Triangle(a, a + 3, a + 1);
                            b.Mesh.Triangle(a, a + 2, a + 3);
                        }
                        else
                        {
                            b.Mesh.Triangle(a, a + 1, a + 3);
                            b.Mesh.Triangle(a, a + 3, a + 2);
                        }
                    }
                }

                if (j == bands - 1)
                {
                    // The top of the last band, which is what's seen from above.
                    int first = b.Mark;
                    for (int s = 0; s <= segments; s++)
                    {
                        float x = job.Bay * s / segments;
                        float lift = Lift(x);
                        foreach (float side in new[] { -1f, 1f })
                        {
                            b.Mesh.Add(new FloraVertex { Position = new Vector3(x, high + lift, Z(x) + side * rod * 0.5f), Normal = Vector3.UnitY, Tangent = Vector3.UnitX, Colour = colour * 1.06f, UV = new Vector2(x / rods.Tile, side * rod / rods.Tile), Material = rods.Material, Joint = -1 });
                        }
                    }
                    for (int s = 0; s < segments; s++)
                    {
                        int a = first + s * 2;
                        b.Mesh.Triangle(a, a + 1, a + 3);
                        b.Mesh.Triangle(a, a + 3, a + 2);
                    }
                }
            }

            for (int i = 1; i < stakes; i++)
            {
                float x = i * pitch;
                int mark = b.Mark;
                b.Log(new Vector3(x, -0.35f, 0f), new Vector3(x, height + weave.Range(0.03f, 0.13f), 0f), stake, stake * 0.85f, 5, cane.Tinted(weave.Range(0.8f, 1f)),
                    (uint)weave.Int(int.MaxValue), headEnd: LogEnd.Cut, rough: 0.1f, rings: 1, endColour: FenceLook.EndGrain(hazel));
                b.Upright(mark, x);
            }
        }
    }
}
