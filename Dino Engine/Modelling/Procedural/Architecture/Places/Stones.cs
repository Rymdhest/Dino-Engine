using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceMaterials;

namespace Dino_Engine.Modelling.Procedural.Architecture.Places
{
    /// <summary>
    /// A ring of old stones (LORE.md: raised by no people anyone remembers, over bones nobody was meant to find; the stones hum faintly, like held breath): standing stones of the land's
    /// own rock, faceted as rock breaks, darker and greener towards the ground and lichened on top, some cut with the script the old roads are cut in (<see cref="Glyphs"/>), a few of
    /// those humming with a faint light; stones with a lintel across them; the barrow they were raised over, its kerb and its portal, which the Ossuary has dug into in some lands; and
    /// what the cult that holds them has put up, its totems and its fire, for whichever of the land's powers it serves (<see cref="Cult"/>). Model +z is a piece's front, y = 0 where
    /// people stand. Headless.
    /// </summary>
    internal static class Stones
    {
        /// <summary>The light the old stones hum with: faint, and nobody's colour, as nobody knows whose they were.</summary>
        private static readonly Vector3 Hum = new Vector3(1f, 0.9f, 0.68f);

        /// <summary>What each cult's light is: the fire's flame, the frost's cold violet, the storm's lightning-white and the mire's marsh-light, none of them the Reich's or the Ossuary's.</summary>
        public static Vector3 CultLight(Cult cult) => cult switch
        {
            Cult.Fire => new Vector3(1f, 0.52f, 0.2f),
            Cult.Frost => Camp.Frost,
            Cult.Storm => new Vector3(1f, 0.94f, 0.72f),
            _ => new Vector3(1f, 0.72f, 0.36f),
        };

        private static Vector3 StoneOfSpec(in PlaceSpec spec, int first) => spec.Count > first + 2 ? StoneColour(spec.Int(first), spec.Int(first + 1), spec.Int(first + 2)) : new Vector3(0.46f, 0.45f, 0.42f);

        // A face of old stone at a height: darker and greener at the foot, where the wet rises, lichened where it faces the sky, and patched pale here and there.
        private static VertexMaterial Weather(Vector3 colour, Vector3 normal, float y, WorldRandom random)
        {
            float wet = 1f - Math.Clamp(y / 2.2f, 0f, 1f);
            Vector3 c = colour * Vector3.Lerp(Vector3.One, new Vector3(0.74f, 0.8f, 0.7f), wet * 0.8f);
            if (normal.Y > 0.45f) c *= new Vector3(0.84f, 0.96f, 0.7f);
            else if (random.Chance(0.16f)) c *= new Vector3(1.1f, 1.08f, 0.88f);
            return new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(c * random.Range(0.94f, 1.05f)));
        }

        // ---------------------------------------------------------------- The stones

        /// <summary>
        /// A standing stone: a slab of the land's rock lofted up from below the ground through rings that each push its sides about, so its faces are rock's fractures and its top
        /// slopes and breaks; leaning a little as old stones do. Carved, it's cut up its front with the old roads' script, and the carving hums with a faint light; broken, it's a stump
        /// with a jagged top; fallen, it lies half sunk where it went over. Numbers: height, width, form (0 plain, 1 carved, 2 broken, 3 fallen), lean (degrees), stone (r, g, b).
        /// </summary>
        public static void Menhir(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float high = Math.Clamp(spec.Metres(0), 1.2f, 9f), wide = Math.Clamp(spec.Metres(1), 0.6f, 3.4f);
            int form = Math.Clamp(spec.Int(2), 0, 3);
            float lean = Math.Clamp(spec.Radians(3), -0.35f, 0.35f);
            Vector3 colour = StoneOfSpec(spec, 4);
            if (form == 2) high *= random.Range(0.35f, 0.55f);
            const int Sides = 8, Rings = 6;
            float rx = wide * 0.5f, rz = wide * 0.5f * random.Range(0.55f, 0.78f);
            var bump = new float[Sides];
            var top = new float[Sides];
            float slope = random.Range(-0.18f, 0.18f);
            for (int k = 0; k < Sides; k++)
            {
                bump[k] = random.Range(0.8f, 1.16f);
                float a = k * MathF.Tau / Sides;
                top[k] = form == 2 ? random.Range(0.72f, 1f) : 1f + slope * MathF.Cos(a) - random.Range(0f, 0.08f);
            }
            // Its rings, from well under the ground to its top: widest a little above the ground, and narrowing as it goes up.
            var points = new Vector3[Rings + 1, Sides];
            for (int i = 0; i <= Rings; i++)
            {
                float t = i / (float)Rings;
                float y = MathHelper.Lerp(-0.7f, high, t);
                float r = (1f + 0.1f * MathF.Sin(t * 2.6f)) * (1f - 0.34f * t * t) * (i == Rings ? 0.82f : 1f);
                for (int k = 0; k < Sides; k++)
                {
                    float a = k * MathF.Tau / Sides;
                    float yy = i == Rings ? y * top[k] : y;
                    // Each ring's sides pushed a little of their own way, so its faces aren't planes all the way up.
                    float wobble = 1f + (i > 0 && i < Rings ? random.Range(-0.06f, 0.06f) : 0f);
                    points[i, k] = new Vector3(MathF.Cos(a) * rx * bump[k] * r * wobble + MathF.Tan(lean) * MathF.Max(yy, 0f), yy, MathF.Sin(a) * rz * bump[k] * r * wobble);
                }
            }
            Vector3 peak = Vector3.Zero;
            for (int k = 0; k < Sides; k++) peak += points[Rings, k] / Sides;
            peak += Vector3.UnitY * (form == 2 ? 0.04f : 0.12f);
            // Fallen, the whole of it turned onto its side and sunk half its depth into the ground.
            Quaternion lay = Quaternion.Identity;
            Vector3 shift = Vector3.Zero;
            if (form == 3)
            {
                lay = Quaternion.FromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f + random.Range(-0.08f, 0.08f)) * Quaternion.FromAxisAngle(Vector3.UnitY, random.Range(-0.2f, 0.2f));
                shift = new Vector3(high * 0.45f, Ground + rz * 0.35f, 0f);
            }
            Vector3 P(Vector3 p) => Vector3.Transform(p, lay) + shift;
            for (int i = 0; i < Rings; i++)
            {
                for (int k = 0; k < Sides; k++)
                {
                    Vector3 p0 = P(points[i, k]), p1 = P(points[i, (k + 1) % Sides]), p2 = P(points[i + 1, (k + 1) % Sides]), p3 = P(points[i + 1, k]);
                    Vector3 axis = P(new Vector3(MathF.Tan(lean) * MathF.Max((points[i, k].Y + points[i + 1, k].Y) * 0.5f, 0f), (points[i, k].Y + points[i + 1, k].Y) * 0.5f, 0f));
                    Vector3 want = (p0 + p1 + p2 + p3) * 0.25f - axis;
                    Vector3 n = Vector3.Cross(p2 - p0, p3 - p1);
                    n = n.LengthSquared > 1e-10f ? n.Normalized() : Vector3.UnitY;
                    if (Vector3.Dot(n, want) < 0f) n = -n;
                    Kit.Face(b, p0, p1, p2, p3, want, Weather(colour, n, (p0.Y + p2.Y) * 0.5f, random));
                }
            }
            for (int k = 0; k < Sides; k++) Kit.Face(b, P(peak), P(points[Rings, k]), P(points[Rings, (k + 1) % Sides]), Vector3.Transform(Vector3.UnitY, lay), Weather(colour, Vector3.Transform(Vector3.UnitY, lay), high, random));

            if (form != 1) return;
            // Carved: the old roads' script cut up the face it turns to the south, glowing faintly in the cut.
            int front = 2;      // The side whose face looks most along +z: angles a quarter of the way round.
            float best = -2f;
            for (int k = 0; k < Sides; k++)
            {
                Vector3 mid = (points[2, k] + points[2, (k + 1) % Sides]) * 0.5f;
                float facing = mid.Z / (mid.Length + 1e-4f);
                if (facing > best) { best = facing; front = k; }
            }
            VertexMaterial cut = Glow(Hum.X, Hum.Y, Hum.Z, 0.55f);
            int glyphs = 0;
            for (int i = 1; i < Rings - 1 && glyphs < 3; i++)
            {
                Vector3 p0 = points[i, front], p1 = points[i, (front + 1) % Sides], p2 = points[i + 1, (front + 1) % Sides], p3 = points[i + 1, front];
                if (p3.Y < 0.4f) continue;
                Vector3 centre = (p0 + p1 + p2 + p3) * 0.25f;
                Vector3 u = (p1 - p0).Normalized();
                Vector3 n = Vector3.Cross(p1 - p0, p3 - p0).Normalized();
                if (n.Z < 0f) { n = -n; u = -u; }
                Vector3 v = Vector3.Cross(n, u).Normalized();
                if (v.Y < 0f) v = -v;
                float size = MathF.Min((p1 - p0).Length, (p3 - p0).Length) * 0.75f;
                foreach (Glyphs.Stroke stroke in Glyphs.Of((int)(spec.Seed % 977) + glyphs * 31))
                {
                    for (int s = 1; s < stroke.Points.Length; s++)
                    {
                        Vector3 a = centre + (u * stroke.Points[s - 1].X + v * stroke.Points[s - 1].Y) * size + n * 0.014f;
                        Vector3 c = centre + (u * stroke.Points[s].X + v * stroke.Points[s].Y) * size + n * 0.014f;
                        if ((c - a).LengthSquared < 1e-6f) c = a + u * 0.02f;
                        Kit.Plank(b, a, c, n, stroke.Width * size, 0.01f, cut, 0f);
                    }
                }
                glyphs++;
            }
            Vector3 glowAt = (points[2, front] + points[3, (front + 1) % Sides]) * 0.5f;
            lights.Add(AssetLight.Glow(glowAt + new Vector3(0f, 0f, 0.9f), Hum, 9f, 4.5f) with { Flicker = 0.06f, FlickerSpeed = 0.7f });
        }

        /// <summary>
        /// Two stones with a third laid across their heads, its ends cut to sit on them: standing, or its lintel down in front of them where it fell and one upright leaning out.
        /// Numbers: height, span, form (0 standing, 1 its lintel fallen), stone (r, g, b).
        /// </summary>
        public static void Trilithon(MeshBuilder b, in PlaceSpec spec, WorldRandom random)
        {
            float high = Math.Clamp(spec.Metres(0), 3f, 8f), span = Math.Clamp(spec.Metres(1), 1.4f, 3.6f);
            bool fallen = spec.Int(2) == 1;
            Vector3 colour = StoneOfSpec(spec, 3);
            float hx = span * 0.5f;
            Func<Vector3, VertexMaterial> face(float y) => n => Weather(colour, n, y, random);
            Vector3 J(float s) => new Vector3(random.Range(-s, s), random.Range(-s * 0.5f, s * 0.5f), random.Range(-s, s));
            foreach (float side in new[] { -1f, 1f })
            {
                float w = random.Range(1.1f, 1.5f), d = random.Range(0.75f, 1f);
                float x = side * (hx + w * 0.5f);
                float tilt = fallen && side > 0f ? 0.22f : random.Range(-0.03f, 0.03f);
                Vector3 up = new Vector3(MathF.Sin(tilt) * side, MathF.Cos(tilt), 0f);
                Vector3 across = new Vector3(up.Y, -up.X, 0f);
                // Two courses of it, the upper a little narrower, so its sides break as rock does.
                Vector3 foot = new Vector3(x, -0.7f, 0f), mid = foot + up * ((high + 0.7f) * 0.55f), head = foot + up * (high + 0.7f);
                float w2 = w * random.Range(0.86f, 0.95f), d2 = d * random.Range(0.86f, 0.95f);
                Kit.Solid(b, new[]
                {
                    foot - across * (w * 0.5f) + new Vector3(0f, 0f, d * 0.5f), foot + across * (w * 0.5f) + new Vector3(0f, 0f, d * 0.5f), mid + across * (w2 * 0.52f) + new Vector3(0f, 0f, d2 * 0.52f) + J(0.05f), mid - across * (w2 * 0.52f) + new Vector3(0f, 0f, d2 * 0.52f) + J(0.05f),
                    foot - across * (w * 0.5f) - new Vector3(0f, 0f, d * 0.5f), foot + across * (w * 0.5f) - new Vector3(0f, 0f, d * 0.5f), mid + across * (w2 * 0.52f) - new Vector3(0f, 0f, d2 * 0.52f) + J(0.05f), mid - across * (w2 * 0.52f) - new Vector3(0f, 0f, d2 * 0.52f) + J(0.05f),
                }, 0.06f, face(1f));
                Kit.Solid(b, new[]
                {
                    mid - across * (w2 * 0.5f) + new Vector3(0f, 0f, d2 * 0.5f), mid + across * (w2 * 0.5f) + new Vector3(0f, 0f, d2 * 0.5f), head + across * (w2 * 0.46f) + new Vector3(0f, 0f, d2 * 0.44f) + J(0.06f), head - across * (w2 * 0.46f) + new Vector3(0f, 0f, d2 * 0.44f) + J(0.06f),
                    mid - across * (w2 * 0.5f) - new Vector3(0f, 0f, d2 * 0.5f), mid + across * (w2 * 0.5f) - new Vector3(0f, 0f, d2 * 0.5f), head + across * (w2 * 0.46f) - new Vector3(0f, 0f, d2 * 0.44f) + J(0.06f), head - across * (w2 * 0.46f) - new Vector3(0f, 0f, d2 * 0.44f) + J(0.06f),
                }, 0.06f, face(high));
                // The knob on its head the lintel was cut to sit on.
                if (!fallen) Kit.Block(b, head + Vector3.UnitY * 0.06f, new Vector3(0.14f, 0.08f, 0.14f), 0f, 0.03f, face(high));
            }
            float length = span + 2.6f;
            if (!fallen)
            {
                // The lintel in two lengths meeting at a slight angle, as the old builders curved them to the ring.
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(0f, high, 0.04f), c = new Vector3(s * length * 0.5f, high, -0.12f);
                    Kit.Solid(b, new[]
                    {
                        a + new Vector3(0f, 0f, 0.42f), c + new Vector3(0f, 0f, 0.4f), c + new Vector3(0f, 0.85f, 0.38f), a + new Vector3(0f, 0.88f, 0.4f),
                        a - new Vector3(0f, 0f, 0.42f), c - new Vector3(0f, 0f, 0.42f), c + new Vector3(0f, 0.85f, -0.4f), a + new Vector3(0f, 0.88f, -0.4f),
                    }, 0.06f, face(high + 0.5f));
                }
                return;
            }
            // Fallen in front of them, broken in two.
            float turn = random.Range(-0.4f, 0.4f);
            for (int k = 0; k < 2; k++)
            {
                Vector3 c = new Vector3((k - 0.5f) * length * 0.55f + random.Range(-0.2f, 0.2f), Ground + 0.32f, random.Range(1.6f, 2.4f));
                Kit.Block(b, c, new Vector3(length * 0.26f, 0.42f, 0.44f), turn + k * random.Range(0.1f, 0.4f), 0.06f, face(0.3f));
            }
        }

        /// <summary>
        /// The barrow the ring was raised over: a long mound of turf with its kerb of stones round its foot, some gone, and at its south end its portal, two stones and a capstone over a
        /// passage going dark, with lower stones either side of it. Shut, a great slab still blocks the passage; dug into by the Ossuary, the slab's been dragged aside, the passage is
        /// propped with timbers, the spoil's heaped by it and its cold light shows inside. Numbers: length, width, height, opened (0, 1), stone (r, g, b).
        /// </summary>
        public static void Barrow(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float length = Math.Clamp(spec.Metres(0), 6f, 18f), width = Math.Clamp(spec.Metres(1), 4f, 9f), high = Math.Clamp(spec.Metres(2), 1.2f, 3.2f);
            bool opened = spec.Int(3) == 1;
            Vector3 colour = StoneOfSpec(spec, 4);
            float hz = length * 0.5f, hx = width * 0.5f;
            Kit.Heap(b, new Vector3(0f, Ground - 0.1f, -0.4f), new Vector2(hx, hz), high, 0f, Turf(0.95f), random, 7, 22);
            Kit.Heap(b, new Vector3(random.Range(-0.4f, 0.4f), Ground + high * 0.55f, -hz * 0.15f), new Vector2(hx * 0.55f, hz * 0.6f), high * 0.5f, 0f, Turf(1.08f), random, 4, 14);
            // Its kerb: stones round the foot of it, half sunk, a few gone.
            int kerb = (int)(MathF.PI * (hx + hz) / 1.15f);
            for (int k = 0; k < kerb; k++)
            {
                float a = k * MathF.Tau / kerb;
                Vector3 at = new Vector3(MathF.Cos(a) * (hx + 0.15f), Ground + 0.12f, MathF.Sin(a) * (hz + 0.15f) - 0.4f);
                if (at.Z > hz - 1.6f && MathF.Abs(at.X) < 1.6f) continue;
                if (random.Chance(0.14f)) continue;
                Kit.Block(b, at, new Vector3(random.Range(0.3f, 0.45f), random.Range(0.25f, 0.42f), random.Range(0.18f, 0.28f)), -a + MathF.PI * 0.5f + random.Range(-0.2f, 0.2f), 0.05f, n => Weather(colour, n, 0.3f, random));
            }
            // The portal at its south end: two stones, a capstone, the dark of the passage between them, and lower stones out either side of it.
            float face = hz - 0.9f;
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Block(b, new Vector3(s * 0.85f, 0.55f, face), new Vector3(0.32f, 1.25f, 0.42f), random.Range(-0.05f, 0.05f), 0.06f, n => Weather(colour, n, 1f, random));
                for (int k = 1; k <= 2; k++)
                {
                    Vector3 at = new Vector3(s * (0.95f + k * 1.15f), 0.3f, face - k * 0.55f);
                    Kit.Block(b, at, new Vector3(0.42f, 0.95f - k * 0.2f, 0.32f), s * (0.25f + k * 0.2f), 0.06f, n => Weather(colour, n, 0.8f, random));
                }
            }
            Kit.Block(b, new Vector3(0f, 1.95f, face - 0.15f), new Vector3(1.35f, 0.26f, 0.62f), random.Range(-0.04f, 0.04f), 0.06f, n => Weather(colour, n, 2f, random));
            Kit.Face(b, new Vector3(-0.55f, Ground - 0.05f, face - 0.3f), new Vector3(0.55f, Ground - 0.05f, face - 0.3f), new Vector3(0.55f, 1.72f, face - 0.3f), new Vector3(-0.55f, 1.72f, face - 0.3f), Vector3.UnitZ, Dark);
            if (!opened)
            {
                Kit.Block(b, new Vector3(0f, 0.7f, face + 0.48f), new Vector3(0.78f, 0.95f, 0.18f), random.Range(-0.08f, 0.08f), 0.05f, n => Weather(colour, n, 0.8f, random));
                return;
            }
            // Dug into: the slab dragged aside and lying, timbers propping the passage, the spoil heaped by it, and the Ossuary's cold light down in the dark.
            Kit.Block(b, new Vector3(2.4f, Ground + 0.2f, face + 1.4f), new Vector3(0.78f, 0.2f, 0.95f), random.Range(0.3f, 0.8f), 0.05f, n => Weather(colour, n, 0.2f, random));
            VertexMaterial wood = Fresh(random, 0.85f);
            foreach (float s in new[] { -1f, 1f }) Kit.Plank(b, new Vector3(s * 0.5f, -0.1f, face - 0.1f), new Vector3(s * 0.46f, 1.7f, face - 0.12f), Vector3.UnitZ, 0.16f, 0.16f, wood);
            Kit.Plank(b, new Vector3(-0.65f, 1.66f, face - 0.12f), new Vector3(0.65f, 1.66f, face - 0.12f), Vector3.UnitY, 0.18f, 0.16f, wood);
            Kit.Heap(b, new Vector3(-2.6f, Ground - 0.05f, face + 0.6f), new Vector2(1.6f, 1.2f), 0.9f, random.Float(), Soil(0.85f), random, 4, 12);
            Kit.Face(b, new Vector3(-0.4f, 0.3f, face - 0.95f), new Vector3(0.4f, 0.3f, face - 0.95f), new Vector3(0.4f, 1.2f, face - 0.95f), new Vector3(-0.4f, 1.2f, face - 0.95f), Vector3.UnitZ, OssuaryGlow(1.4f));
            lights.Add(Cold(new Vector3(0f, 0.9f, face + 0.6f), 26f, 7f));
        }

        // ---------------------------------------------------------------- What the cult has put up

        /// <summary>
        /// A totem of the cult that holds the stones, for whichever of the land's powers it serves: the fire's, a charred post hung with horned skulls and red rags under a bowl of
        /// fire; the frost's, bleached wood racked with antlers and hung with icicles under a cold violet flame; the storm's, an iron rod up a post bound with stones on chains, its tip
        /// burning white; the mire's, a stake of bog-oak bound in reeds, bones lashed to it and a cage of bones at its head with the marsh-light in it. Numbers: height, cult.
        /// </summary>
        public static void Totem(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            float high = Math.Clamp(spec.Metres(0), 2f, 5f);
            var cult = (Cult)Math.Clamp(spec.Int(1), 0, 3);
            Vector3 light = CultLight(cult);
            Vector3 head = new Vector3(0f, high, 0f);
            switch (cult)
            {
                case Cult.Fire:
                {
                    VertexMaterial wood = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.15f, 0.12f, 0.1f));
                    Kit.Pole(b, new Vector3(0f, -0.5f, 0f), head, 0.14f, wood, 0.11f);
                    Kit.Plank(b, head + new Vector3(-0.8f, -0.9f, 0f), head + new Vector3(0.8f, -0.9f, 0f), Vector3.UnitY, 0.12f, 0.12f, wood);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 hang = head + new Vector3(s * 0.7f, -0.95f, 0f);
                        Kit.Pole(b, hang, hang - Vector3.UnitY * 0.3f, 0.01f, Rope(random, 0.7f));
                        Skull(b, hang - Vector3.UnitY * 0.5f, random, true);
                        Vector3 rag = hang + new Vector3(-s * 0.2f, 0f, 0.02f);
                        Kit.Sheet(b, rag, rag + new Vector3(0.12f, 0f, 0f), rag + new Vector3(0.14f, -0.9f, 0.05f), rag + new Vector3(0.02f, -0.8f, 0.05f), Painted(new Vector3(0.45f, 0.1f, 0.04f)), Painted(new Vector3(0.3f, 0.07f, 0.03f)));
                    }
                    // Ochre hands smeared up it, and the bowl of fire on its head.
                    for (int k = 0; k < 3; k++) Kit.Plank(b, new Vector3(-0.15f + k * 0.15f, 0.9f + k * 0.35f, 0.13f), new Vector3(-0.1f + k * 0.15f, 1.15f + k * 0.35f, 0.13f), Vector3.UnitZ, 0.1f, 0.01f, Painted(new Vector3(0.62f, 0.34f, 0.1f)), 0f);
                    Kit.Lathe(b, head - Vector3.UnitY * 0.05f, Vector3.UnitY, 0.3f, Kit.Profile((0f, 0.12f), (0.4f, 0.32f), (1f, 0.38f)), 8, 2, LandIron(random, 0.8f));
                    Kit.Heap(b, head + Vector3.UnitY * 0.2f, new Vector2(0.3f), 0.1f, 0f, Coals, random, 2, 7);
                    lights.Add(AssetLight.Burning(head + Vector3.UnitY * 0.25f, 0.45f));
                    lights.Add(Camp.TorchLight(head + Vector3.UnitY * 0.8f) with { Colour = light, Intensity = 80f, Radius = 12f });
                    break;
                }
                case Cult.Frost:
                {
                    VertexMaterial wood = Bleached(random, 1f), ice = Ice(1.1f), bone = Bone(random, 1.1f);
                    Kit.Pole(b, new Vector3(0f, -0.5f, 0f), head, 0.12f, wood, 0.09f);
                    // Antlers racked up it, each a beam with its tines.
                    for (int k = 0; k < 2; k++)
                    {
                        float y = high - 0.5f - k * 0.9f;
                        foreach (float s in new[] { -1f, 1f })
                        {
                            var beam = new List<Vector3> { new Vector3(0f, y, 0f), new Vector3(s * 0.35f, y + 0.25f, 0.05f), new Vector3(s * 0.62f, y + 0.6f, 0.1f), new Vector3(s * 0.7f, y + 0.95f, 0.05f) };
                            Kit.Bend(b, beam, 0.045f, 0.015f, bone);
                            for (int t = 1; t < 3; t++) Kit.Pole(b, beam[t], beam[t] + new Vector3(s * 0.05f, 0.3f, 0.12f), 0.025f, bone, 0.004f);
                        }
                    }
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * MathF.Tau / 6f;
                        Vector3 at = new Vector3(MathF.Cos(a) * 0.13f, high - random.Range(0.6f, 1.6f), MathF.Sin(a) * 0.13f);
                        Kit.Lathe(b, at, -Vector3.UnitY, random.Range(0.25f, 0.6f), Kit.Profile((0f, 0.04f), (1f, 0f)), 4, 1, ice);
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 at = head + new Vector3(random.Range(-0.06f, 0.06f), 0.02f, random.Range(-0.06f, 0.06f));
                        Kit.Lathe(b, at, Vector3.UnitY, random.Range(0.4f, 0.7f), Kit.Profile((0f, 0.1f), (0.35f, 0.13f), (1f, 0f)), 5, 2, Glow(light.X, light.Y, light.Z, 2.2f));
                    }
                    lights.Add(AssetLight.Glow(head + Vector3.UnitY * 0.5f, light, 38f, 10f) with { Flicker = 0.12f, FlickerSpeed = 3f });
                    break;
                }
                case Cult.Storm:
                {
                    VertexMaterial wood = Weathered(random, 0.7f), iron = LandIron(random, 0.9f);
                    Kit.Pole(b, new Vector3(0f, -0.5f, 0f), head - Vector3.UnitY * 0.6f, 0.15f, wood, 0.12f);
                    foreach (float y in new[] { 0.6f, 1.6f, high - 0.9f }) b.Tube(new Vector3(0f, y, 0f), new Vector3(0f, y + 0.1f, 0f), 0.17f, 0.15f, 8, iron, capped: false);
                    Kit.Pole(b, head - Vector3.UnitY * 1.2f, head + Vector3.UnitY * 1.4f, 0.035f, iron, 0.012f);
                    // Stones bound in iron, hung off it on chains, and the scorch where the lightning ran down it.
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * MathF.Tau / 3f + 0.4f;
                        Vector3 from = new Vector3(MathF.Cos(a) * 0.16f, high - 0.9f, MathF.Sin(a) * 0.16f);
                        Vector3 stone = from + new Vector3(MathF.Cos(a) * 0.45f, -0.8f, MathF.Sin(a) * 0.45f);
                        Kit.Chain(b, from, stone + Vector3.UnitY * 0.2f, 0.05f, 0.12f, iron);
                        Kit.Block(b, stone, new Vector3(0.17f, 0.2f, 0.15f), a, 0.05f, Dressed(new Vector3(0.4f, 0.4f, 0.38f), random));
                        b.Tube(stone - Vector3.UnitY * 0.04f, stone + Vector3.UnitY * 0.04f, 0.2f, 0.2f, 6, iron, capped: false);
                    }
                    Kit.Plank(b, new Vector3(0.03f, 0.4f, 0.16f), new Vector3(-0.04f, high - 0.7f, 0.16f), Vector3.UnitZ, 0.1f, 0.01f, Flat(new Vector3(0.05f, 0.045f, 0.04f)), 0f);
                    Kit.Ball(b, head + Vector3.UnitY * 1.42f, new Vector3(0.09f), 6, 4, Glow(light.X, light.Y, light.Z, 2.6f));
                    lights.Add(AssetLight.Glow(head + Vector3.UnitY * 1.3f, light, 34f, 10f) with { Flicker = 0.45f, FlickerSpeed = 11f });
                    break;
                }
                default:
                {
                    VertexMaterial oak = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.2f, 0.17f, 0.12f)), reed = Straw(random, 0.75f), bone = Bone(random, 0.9f);
                    var points = new List<Vector3>();
                    for (int k = 0; k <= 4; k++) points.Add(new Vector3(random.Range(-0.15f, 0.15f) * k / 4f, -0.5f + (high + 0.5f) * k / 4f, random.Range(-0.15f, 0.15f) * k / 4f));
                    Kit.Bend(b, points, 0.16f, 0.1f, oak);
                    for (int k = 0; k < 4; k++) b.Tube(points[1] + Vector3.UnitY * (k * 0.45f), points[1] + Vector3.UnitY * (k * 0.45f + 0.28f), 0.18f, 0.17f, 8, reed, capped: false);
                    // Long bones lashed across it, and the cage of ribs at its head with the light in it.
                    for (int k = 0; k < 2; k++) Kit.Pole(b, new Vector3(-0.55f, 1.2f + k * 0.8f, 0.12f), new Vector3(0.55f, 1.35f + k * 0.8f, 0.12f), 0.05f, bone);
                    Vector3 top = points[^1];
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * MathF.Tau / 6f;
                        var rib = new List<Vector3> { top, top + new Vector3(MathF.Cos(a) * 0.3f, 0.25f, MathF.Sin(a) * 0.3f), top + new Vector3(MathF.Cos(a) * 0.15f, 0.62f, MathF.Sin(a) * 0.15f) };
                        Kit.Bend(b, rib, 0.025f, 0.015f, bone);
                    }
                    Kit.Ball(b, top + Vector3.UnitY * 0.3f, new Vector3(0.11f), 6, 4, Glow(light.X, light.Y, light.Z, 2.2f));
                    // Its rags of moss, hanging.
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 at = top + new Vector3(random.Range(-0.2f, 0.2f), -random.Range(0.1f, 0.6f), random.Range(-0.2f, 0.2f));
                        Kit.Sheet(b, at, at + new Vector3(0.1f, 0f, 0f), at + new Vector3(0.1f, -0.8f, 0.04f), at + new Vector3(0f, -0.7f, 0.04f), Flat(new Vector3(0.18f, 0.22f, 0.1f)), Flat(new Vector3(0.12f, 0.15f, 0.07f)));
                    }
                    lights.Add(AssetLight.Glow(top + Vector3.UnitY * 0.4f, light, 30f, 9f) with { Flicker = 0.2f, FlickerSpeed = 1.6f });
                    break;
                }
            }
        }

        // A beast's skull, its snout along +z, horned or not, its sockets dark.
        private static void Skull(MeshBuilder b, Vector3 at, WorldRandom random, bool horned)
        {
            VertexMaterial bone = Bone(random, 1.05f);
            Kit.Lathe(b, at - Vector3.UnitZ * 0.18f, Vector3.UnitZ, 0.48f, Kit.Profile((0f, 0.14f), (0.4f, 0.15f), (0.7f, 0.08f), (1f, 0.04f)), 7, 4, bone);
            foreach (float s in new[] { -1f, 1f })
            {
                Kit.Block(b, at + new Vector3(s * 0.09f, 0.06f, 0.02f), new Vector3(0.035f), 0f, 0f, Dark);
                if (!horned) continue;
                var horn = new List<Vector3> { at + new Vector3(s * 0.1f, 0.1f, -0.1f), at + new Vector3(s * 0.3f, 0.2f, -0.15f), at + new Vector3(s * 0.42f, 0.42f, -0.05f) };
                Kit.Bend(b, horn, 0.05f, 0.012f, Bone(random, 0.85f));
            }
        }

        /// <summary>
        /// The cult's heart, where it does whatever it does in the ring's middle: the fire's great fire in a ring of blackened stones; the frost's spire of ice growing out of a frozen
        /// pool with its light inside; the storm's stone split by lightning, iron rods driven in round it with chains, its crack burning white; the mire's black pool, reeds round it,
        /// bones in it and a bog-oak stake with a cage of the marsh-light over it. Numbers: cult, radius.
        /// </summary>
        public static void CultHeart(MeshBuilder b, in PlaceSpec spec, WorldRandom random, List<AssetLight> lights)
        {
            var cult = (Cult)Math.Clamp(spec.Int(0), 0, 3);
            float radius = Math.Clamp(spec.Metres(1), 1.2f, 3.5f);
            Vector3 light = CultLight(cult);
            switch (cult)
            {
                case Cult.Fire:
                {
                    int stones = (int)(radius * 7f);
                    for (int k = 0; k < stones; k++)
                    {
                        float a = k * MathF.Tau / stones + random.Range(-0.05f, 0.05f);
                        Kit.Block(b, new Vector3(MathF.Cos(a) * radius, Ground + 0.1f, MathF.Sin(a) * radius), new Vector3(random.Range(0.22f, 0.32f), random.Range(0.16f, 0.24f), random.Range(0.18f, 0.26f)), a, 0.04f, Flat(new Vector3(0.12f, 0.11f, 0.1f) * random.Range(0.8f, 1.3f)));
                    }
                    // The fire: logs stood together and burning, the coals heaped under them.
                    VertexMaterial charred = new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.16f, 0.13f, 0.11f));
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k * MathF.Tau / 7f;
                        Kit.Pole(b, new Vector3(MathF.Cos(a) * radius * 0.55f, Ground, MathF.Sin(a) * radius * 0.55f), new Vector3(MathF.Cos(a) * 0.12f, 1.4f, MathF.Sin(a) * 0.12f), 0.11f, charred, 0.06f);
                    }
                    Kit.Heap(b, new Vector3(0f, Ground, 0f), new Vector2(radius * 0.6f), 0.22f, 0f, Coals, random, 3, 10);
                    lights.Add(AssetLight.Burning(new Vector3(0f, Ground + 0.2f, 0f), radius * 1.1f));
                    lights.Add(AssetLight.Campfire(new Vector3(0f, 1.6f, 0f)) with { Intensity = 150f, Radius = 22f, Colour = light });
                    break;
                }
                case Cult.Frost:
                {
                    const int Around = 12;
                    for (int k = 0; k < Around; k++)
                    {
                        float a0 = k * MathF.Tau / Around, a1 = (k + 1) * MathF.Tau / Around;
                        Kit.Face(b, new Vector3(0f, Ground + 0.03f, 0f), new Vector3(MathF.Cos(a0), 0f, MathF.Sin(a0)) * radius + Vector3.UnitY * (Ground + 0.03f), new Vector3(MathF.Cos(a1), 0f, MathF.Sin(a1)) * radius + Vector3.UnitY * (Ground + 0.03f), Vector3.UnitY, Ice(0.9f));
                    }
                    for (int k = 0; k < 9; k++)
                    {
                        float a = random.Float() * MathF.Tau, r = radius * MathF.Sqrt(random.Float()) * 0.55f;
                        Vector3 foot = new Vector3(MathF.Cos(a) * r, Ground - 0.1f, MathF.Sin(a) * r);
                        Vector3 up = Vector3.Normalize(Vector3.UnitY * 2.2f + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * (r / radius + 0.2f));
                        float tall = k == 0 ? random.Range(2.6f, 3.4f) : random.Range(0.8f, 2.2f);
                        Kit.Lathe(b, foot, up, tall, Kit.Profile((0f, 0.22f * (tall / 2f)), (0.7f, 0.17f * (tall / 2f)), (1f, 0f)), 6, 2, Ice(random.Range(1f, 1.25f)));
                    }
                    Kit.Ball(b, new Vector3(0f, 1.1f, 0f), new Vector3(0.18f, 0.3f, 0.18f), 6, 4, Glow(light.X, light.Y, light.Z, 1.8f));
                    lights.Add(AssetLight.Glow(new Vector3(0f, 1.4f, 0f), light, 55f, 13f) with { Flicker = 0.08f, FlickerSpeed = 1.2f });
                    break;
                }
                case Cult.Storm:
                {
                    // The struck stone: two halves leaning apart, blackened, the crack between them burning.
                    VertexMaterial scorched = new VertexMaterial(MaterialCatalog.Rock.Index, new Colour(0.18f, 0.17f, 0.16f));
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 c = new Vector3(s * 0.55f, 0.65f, 0f);
                        Kit.Box(b, c, Vector3.Normalize(new Vector3(1f, s * 0.12f, 0f)), Vector3.Normalize(new Vector3(-s * 0.12f, 1f, 0f)), Vector3.UnitZ, new Vector3(0.5f, 0.85f, 0.7f), 0.06f, scorched);
                    }
                    Kit.Box(b, new Vector3(0f, 0.75f, 0f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0.05f, 0.75f, 0.6f), 0f, Glow(light.X, light.Y, light.Z, 2f));
                    VertexMaterial iron = LandIron(random, 0.9f);
                    int rods = 6;
                    for (int k = 0; k < rods; k++)
                    {
                        float a = k * MathF.Tau / rods + 0.3f;
                        Vector3 foot = new Vector3(MathF.Cos(a) * radius, -0.3f, MathF.Sin(a) * radius);
                        Kit.Pole(b, foot, foot + new Vector3(-MathF.Cos(a) * 0.3f, 2.4f, -MathF.Sin(a) * 0.3f), 0.035f, iron, 0.015f);
                        Vector3 next = new Vector3(MathF.Cos(a + MathF.Tau / rods) * radius, 1.6f, MathF.Sin(a + MathF.Tau / rods) * radius);
                        Kit.Chain(b, foot + new Vector3(-MathF.Cos(a) * 0.2f, 1.9f, -MathF.Sin(a) * 0.2f), next, 0.45f, 0.14f, iron);
                    }
                    lights.Add(AssetLight.Glow(new Vector3(0f, 1.3f, 0.6f), light, 48f, 12f) with { Flicker = 0.5f, FlickerSpeed = 13f });
                    break;
                }
                default:
                {
                    // The black pool, reeds round it, bones in it, and the stake with its cage of the marsh-light.
                    const int Around = 14;
                    for (int k = 0; k < Around; k++)
                    {
                        float a0 = k * MathF.Tau / Around, a1 = (k + 1) * MathF.Tau / Around;
                        Kit.Face(b, new Vector3(0f, Ground + 0.01f, 0f), new Vector3(MathF.Cos(a0) * radius, Ground + 0.01f, MathF.Sin(a0) * radius * 0.8f), new Vector3(MathF.Cos(a1) * radius, Ground + 0.01f, MathF.Sin(a1) * radius * 0.8f), Vector3.UnitY, new VertexMaterial(MaterialCatalog.Mirror.Index, new Colour(0.03f, 0.035f, 0.025f)));
                    }
                    VertexMaterial reed = Straw(random, 0.7f), bone = Bone(random, 0.9f);
                    for (int k = 0; k < 40; k++)
                    {
                        float a = random.Float() * MathF.Tau, r = radius * random.Range(0.95f, 1.35f);
                        Vector3 foot = new Vector3(MathF.Cos(a) * r, Ground - 0.05f, MathF.Sin(a) * r * 0.8f);
                        Kit.Pole(b, foot, foot + new Vector3(random.Range(-0.15f, 0.15f), random.Range(0.8f, 1.6f), random.Range(-0.15f, 0.15f)), 0.015f, reed, 0.004f);
                    }
                    for (int k = 0; k < 5; k++)
                    {
                        float a = random.Float() * MathF.Tau, r = radius * random.Range(0.2f, 0.75f);
                        Vector3 at = new Vector3(MathF.Cos(a) * r, Ground, MathF.Sin(a) * r * 0.8f);
                        Vector3 d = new Vector3(MathF.Cos(a * 3f), 0.25f, MathF.Sin(a * 3f)) * 0.4f;
                        Kit.Pole(b, at - d, at + d, 0.05f, bone);
                    }
                    Vector3 stake = new Vector3(radius * 0.4f, 0f, -radius * 0.2f);
                    Kit.Pole(b, stake - Vector3.UnitY * 0.5f, stake + new Vector3(0.1f, 2.4f, 0f), 0.11f, new VertexMaterial(MaterialCatalog.BarkCharred.Index, new Colour(0.2f, 0.17f, 0.12f)), 0.08f);
                    Vector3 cage = stake + new Vector3(0.1f, 1.9f, 0.35f);
                    Kit.Pole(b, stake + new Vector3(0.1f, 2.3f, 0f), cage + Vector3.UnitY * 0.4f, 0.03f, bone);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * MathF.Tau / 5f;
                        Kit.Bend(b, new List<Vector3> { cage + Vector3.UnitY * 0.4f, cage + new Vector3(MathF.Cos(a) * 0.22f, 0.1f, MathF.Sin(a) * 0.22f), cage + new Vector3(MathF.Cos(a) * 0.12f, -0.25f, MathF.Sin(a) * 0.12f) }, 0.02f, 0.012f, bone);
                    }
                    Kit.Ball(b, cage, new Vector3(0.1f), 6, 4, Glow(light.X, light.Y, light.Z, 2.4f));
                    lights.Add(AssetLight.Glow(cage, light, 36f, 10f) with { Flicker = 0.25f, FlickerSpeed = 1.3f });
                    break;
                }
            }
        }
    }
}
