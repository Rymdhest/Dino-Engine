using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// The shape of a blade, drawn once and grown along its length by <see cref="SwordGenerator.Blade"/>: everything a sword, a dagger or a javelin's head is made of. Widths are shares of
    /// <see cref="Width"/>, the blade's half width at its widest, and positions along it shares of <see cref="Length"/>.
    /// </summary>
    internal sealed class BladeShape
    {
        public float Length, Width, Thick;
        public float BaseWidth = 0.85f, Belly = 0.35f, BellyWidth = 1f, MidWidth = 0.95f, Shoulder = 0.9f;
        public float TipStart = 0.82f, TipRound = 0.6f, TipNeedle = 1f;
        public float Bias;              // 0 for two edges, 1 for one edge with a spine at the back.
        public float Spine = 0.6f;      // A single edge's back, as a share of the thickness.
        public float Bend;              // How far the tip is from straight, in metres, the edge on the outside of the bend.
        public float Sway;
        public float Waves, WaveHeight; // A flamberge's or a kris's edges.
        public float Flat = 0.45f;      // How much of the half width is the flat, from 0 for a diamond up.
        public float EdgeBand = 0.22f;  // The polished bevel at the edge, as a share of the half width.
        public float Fuller, FullerWidth = 0.5f, FullerFrom = 0.04f, FullerTo = 0.6f;
        public float Ricasso, RicassoWidth = 0.75f;
        public int Teeth;
        public float TeethFrom, TeethTo, TeethDepth;
        public (float at, float depth, float span)[] Notches = Array.Empty<(float, float, float)>();
        public float Rust;
        public bool Runes;              // Marks inlaid down the fuller.
        public VertexMaterial? RuneMaterial;
        public float Etch;              // A band etched and gilded near the base, as long as this share of the blade.
    }

    /// <summary>
    /// Swords, greatswords and daggers: a blade on a hilt, standing up with the blade up +y, its width across x and its thickness along z. A one-handed weapon is held at the middle of its grip, which
    /// is at the origin; anything that takes both hands is held by its upper hand at the origin, just under the guard, with the grip running down past where the other hand goes. Nothing is picked
    /// from a list of swords: a blade's outline is curves through drawn points, its section is drawn too, and the hilt's parts are continuous, worked in the piece's motif. See GEAR.md.
    /// </summary>
    public static class SwordGenerator
    {
        private readonly record struct Metrics(float ShortBlade, float LongBlade, float Narrow, float Broad, float Grip, bool TwoHands);

        private static readonly Metrics SwordMetrics = new(1.35f, 2.6f, 0.09f, 0.22f, 0.44f, false);
        private static readonly Metrics GreatswordMetrics = new(2.5f, 3.4f, 0.12f, 0.26f, 1.02f, true);
        private static readonly Metrics DaggerMetrics = new(0.62f, 1.15f, 0.055f, 0.15f, 0.36f, false);

        public static void Build(GearMesh mesh, GearLook look)
        {
            GearKind kind = look.Spec.Kind;
            Metrics metrics = kind == GearKind.Dagger ? DaggerMetrics : kind == GearKind.Greatsword ? GreatswordMetrics : SwordMetrics;
            Genes genes = look.Genes;
            GearForm form = look.Spec.Form;

            BladeShape blade = DrawBlade(look, genes.Part("blade"), metrics.ShortBlade + (metrics.LongBlade - metrics.ShortBlade) * form.Size, metrics.Narrow + (metrics.Broad - metrics.Narrow) * (1f - form.Slender), kind == GearKind.Dagger);
            float grip = metrics.Grip * genes.Range("grip.length", 0.9f, 1.2f) * (0.9f + 0.2f * form.Size);

            // Where the hand goes: the middle of the grip for one, the upper hand for two, with the grip running down past the lower one.
            float gripTop = metrics.TwoHands ? genes.Range("grip.above", 0.14f, 0.2f) : grip * 0.5f;
            float gripBottom = gripTop - grip;

            float guardTop = Guard(mesh, look, genes.Part("guard"), blade, gripTop, grip, metrics.TwoHands);
            Blade(mesh, look, genes.Part("blade"), blade, guardTop);
            Grip(mesh, look, genes.Part("grip"), gripBottom, gripTop, metrics.TwoHands);
            Pommel(mesh, look, genes.Part("pommel"), blade, gripBottom, metrics.TwoHands);
            if (look.Legendary || look.Ornament > 0.55f && genes.Chance("knot", look.Ornament * 0.5f)) Knot(mesh, look, genes.Part("knot"), gripBottom);
        }

        // Everything about a blade, drawn from what the base and the look say.
        private static BladeShape DrawBlade(GearLook look, Genes genes, float length, float width, bool dagger)
        {
            GearForm form = look.Spec.Form;
            var b = new BladeShape();
            b.Length = genes.Around("length", length, length * 0.06f);
            b.Width = genes.Around("width", width, width * 0.12f) * (0.85f + 0.3f * look.Heft) * (look.Legendary ? genes.Range("width.grand", 1f, 1.2f) : 1f);

            // Its outline is one of the ways a smith thinks of a blade, and everything within it is drawn: one that tapers from a broad base to its point, one whose edges run nearly parallel to
            // a short point, a leaf that waists and swells again before it, or a cleaver that widens to its end. Each is a set of ranges, never a shape.
            float slender = form.Slender;
            float curve = form.Curve;
            int outline = genes.Pick("outline", 3f + slender * 2f, 2f + (1f - slender) * 1.5f, 0.6f + (1f - look.Craft) * 0.6f + (dagger ? 0.8f : 0f), curve * 2.5f + (1f - slender) * 0.6f);
            switch (outline)
            {
                case 0:     // Tapering.
                    b.BaseWidth = genes.Range("width.base", 0.92f, 1f);
                    b.Belly = genes.Range("belly.at", 0.02f, 0.18f);
                    b.BellyWidth = genes.Range("width.belly", 0.97f, 1.02f);
                    b.Shoulder = genes.Range("width.shoulder", 0.42f, 0.7f);
                    b.MidWidth = b.Shoulder + (b.BellyWidth - b.Shoulder) * genes.Range("width.mid", 0.45f, 0.65f);
                    b.TipStart = 1f - genes.Range("tip.length", 0.12f, 0.28f);
                    break;
                case 1:     // Parallel.
                    b.BaseWidth = genes.Range("width.base", 0.9f, 1f);
                    b.Belly = genes.Range("belly.at", 0.05f, 0.3f);
                    b.BellyWidth = genes.Range("width.belly", 0.96f, 1.01f);
                    b.Shoulder = genes.Range("width.shoulder", 0.78f, 0.95f);
                    b.MidWidth = b.Shoulder + (b.BellyWidth - b.Shoulder) * genes.Range("width.mid", 0.4f, 0.7f);
                    b.TipStart = 1f - genes.Range("tip.length", 0.06f, 0.16f);
                    break;
                case 2:     // A leaf.
                    b.BaseWidth = genes.Range("width.base", 0.62f, 0.8f);
                    b.Belly = genes.Range("belly.at", 0.55f, 0.75f);
                    b.BellyWidth = genes.Range("width.belly", 0.98f, 1.06f);
                    b.MidWidth = genes.Range("width.mid", 0.66f, 0.82f);
                    b.Shoulder = genes.Range("width.shoulder", 0.8f, 0.98f);
                    b.TipStart = 1f - genes.Range("tip.length", 0.14f, 0.26f);
                    break;
                default:    // A cleaver, widening to its end, as a falchion or a seax does.
                    b.BaseWidth = genes.Range("width.base", 0.68f, 0.82f);
                    b.Belly = genes.Range("belly.at", 0.6f, 0.8f);
                    b.BellyWidth = genes.Range("width.belly", 1f, 1.06f);
                    b.MidWidth = genes.Range("width.mid", 0.82f, 0.95f);
                    b.Shoulder = genes.Range("width.shoulder", 0.92f, 1.04f);
                    b.TipStart = 1f - genes.Range("tip.length", 0.1f, 0.2f);
                    break;
            }
            b.TipRound = genes.Range("tip.round", 0.15f, 1f) * (1f - slender * 0.6f);
            b.TipNeedle = genes.Range("tip.needle", 0.75f, 1.35f) + slender * 0.4f;

            // One edge or two. A curved blade is nearly always single-edged with its edge on the outside of the curve, and so is a cleaver.
            b.Bias = Math.Clamp(genes.Around("bias", curve * 0.85f + (outline == 3 ? 0.6f : 0f) - 0.08f, 0.2f), 0f, 1f);
            if (b.Bias < 0.35f) b.Bias = 0f;
            b.Spine = genes.Range("spine", 0.45f, 0.85f);
            b.Bend = curve * genes.Range("bend", 0.04f, 0.2f) * b.Length * (b.Bias > 0f && !genes.Chance("forward", 0.12f) ? -1f : genes.Sign("bend.way"));
            b.Sway = genes.Around("sway", 0f, 0.008f) * b.Length;
            bool wavy = genes.Chance("wave", curve > 0.8f ? 0.85f : curve * curve * 0.25f);
            b.WaveHeight = wavy ? b.Width * genes.Range("wave.height", 0.22f, 0.5f) : 0f;
            b.Waves = genes.Range("wave.count", dagger ? 3.5f : 5f, dagger ? 6.5f : 11f);

            // Across it.
            b.Thick = b.Width * genes.Range("thick", 0.16f, 0.26f) * (0.8f + 0.45f * look.Heft);
            b.Flat = genes.Pick("section", 2f, 3f, 2f) switch { 0 => genes.Range("flat.diamond", 0.02f, 0.12f), 1 => genes.Range("flat", 0.3f, 0.6f), _ => genes.Range("flat.wide", 0.6f, 0.8f) };
            b.EdgeBand = genes.Range("edge.band", 0.14f, 0.3f);
            bool fuller = b.Flat > 0.25f && genes.Chance("fuller", 0.65f + look.Craft * 0.2f);
            b.Fuller = fuller ? genes.Range("fuller.depth", 0.25f, 0.6f) : 0f;
            b.FullerWidth = genes.Range("fuller.width", 0.3f, 0.8f);
            b.FullerTo = genes.Range("fuller.to", 0.35f, 0.85f);

            // A blunt stretch above the guard for a finger or a second hand, the longer on what takes two.
            float ricasso = look.Spec.Kind == GearKind.Greatsword ? 0.75f : 0.2f + look.Craft * 0.2f;
            b.Ricasso = genes.Chance("ricasso", ricasso) ? genes.Range("ricasso.length", 0.04f, look.Spec.Kind == GearKind.Greatsword ? 0.17f : 0.09f) : 0f;
            b.RicassoWidth = genes.Range("ricasso.width", 0.55f, 0.85f);
            b.FullerFrom = b.Ricasso + genes.Range("fuller.from", 0.01f, 0.05f);

            // What's been done to its edges, on purpose or otherwise.
            b.Teeth = genes.Chance("teeth", look.Spikes * 0.22f * (look.Motif == Motif.Fang ? 3f : 1f)) ? genes.Count("teeth.count", 3, 9) : 0;
            b.TeethFrom = genes.Range("teeth.from", 0.08f, 0.35f);
            b.TeethTo = b.TeethFrom + genes.Range("teeth.reach", 0.15f, 0.4f);
            b.TeethDepth = genes.Range("teeth.depth", 0.08f, 0.22f);
            int notches = (int)(look.Wear * genes.Range("notches", 0f, 7f));
            b.Notches = new (float, float, float)[notches];
            for (int i = 0; i < notches; i++) b.Notches[i] = (genes.Range($"notch{i}.at", 0.1f, 0.9f), genes.Range($"notch{i}.depth", 0.06f, 0.24f), genes.Range($"notch{i}.span", 0.01f, 0.025f));
            b.Rust = look.Wear * look.Wear * genes.Range("rust", 0.3f, 1f);
            b.Runes = fuller && look.Shines("runes");
            b.RuneMaterial = look.Theme != GearTheme.None || look.Legendary ? look.Colours.Glow : look.Colours.Precious;
            b.Etch = look.Craft > 0.55f && genes.Chance("etch", look.Ornament) ? genes.Range("etch.length", 0.08f, 0.2f) : 0f;
            return b;
        }

        // The width of a blade at a share of its length, as a share of its widest, with the teeth and notches taken out of it.
        private static float WidthAt(BladeShape b, float t)
        {
            float w;
            if (t < b.Ricasso) w = b.BaseWidth * b.RicassoWidth;
            else if (t < b.TipStart)
            {
                float s = (t - b.Ricasso) / MathF.Max(b.TipStart - b.Ricasso, 1e-3f);
                Profile outline = new Profile((0f, b.BaseWidth), (b.Belly, b.BellyWidth), (b.Belly + (1f - b.Belly) * 0.55f, b.MidWidth), (1f, b.Shoulder));
                w = outline.At(s);
                if (b.Ricasso > 0f) w = MathF.Min(w, b.BaseWidth * b.RicassoWidth + (w - b.BaseWidth * b.RicassoWidth) * Math.Clamp((t - b.Ricasso) / 0.025f, 0f, 1f));
            }
            else
            {
                // The point: from a straight taper to a round end, and a needle where it's drawn in.
                float u = Math.Clamp((t - b.TipStart) / MathF.Max(1f - b.TipStart, 1e-3f), 0f, 1f);
                float a = 1f + b.TipRound * 1.2f;
                w = b.Shoulder * MathF.Pow(MathF.Max(1f - MathF.Pow(u, a), 0f), 1f / MathF.Max(b.TipNeedle * (0.7f + b.TipRound * 0.8f), 0.3f));
            }
            if (b.WaveHeight > 0f) w *= 1f - 0.14f * MathF.Abs(MathF.Sin(t * b.Waves * MathF.Tau)) * (1f - t * t);
            if (b.Teeth > 0 && t > b.TeethFrom && t < b.TeethTo)
            {
                float phase = (t - b.TeethFrom) / (b.TeethTo - b.TeethFrom) * b.Teeth;
                w -= b.TeethDepth * (phase - MathF.Floor(phase));
            }
            foreach ((float at, float depth, float span) in b.Notches)
            {
                float d = (t - at) / span;
                w -= depth * MathF.Exp(-d * d);
            }
            return MathF.Max(w, 0.02f);
        }

        /// <summary>
        /// A blade from <paramref name="baseY"/> up, on whatever it's on: a sword's, a dagger's or a javelin's head. Its section changes as it goes: blunt and square at the ricasso, a polished bevel to
        /// each edge, a flat or a ridge between, a fuller run into the flat and out again, and a spine down the back of a single edge.
        /// </summary>
        internal static void Blade(GearMesh mesh, GearLook look, Genes genes, BladeShape b, float baseY)
        {
            GearColours colours = look.Colours;
            int steps = Math.Clamp(18 + (int)(b.Length * 5f) + (b.WaveHeight > 0f ? (int)(b.Waves * 6f) : 0) + b.Teeth * 3 + b.Notches.Length * 2, 18, b.WaveHeight > 0f ? 68 : 56);
            var rings = new Ring[steps + 1];
            var ts = new float[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                // Closer together near the point, where the outline does most of its turning.
                float s = (float)i / steps;
                float t = 1f - MathF.Pow(1f - s, 1.35f);
                ts[i] = t;
                float w = MathF.Max(WidthAt(b, t) * b.Width, 0.004f);

                // A single edge keeps its back straight and the edge comes back to meet it at the point.
                float back = -b.Width * b.BaseWidth;
                float centre = b.Bias * (back + w) + b.Bend * t * t + b.Sway * MathF.Sin(t * MathF.PI) + b.WaveHeight * MathF.Sin(t * b.Waves * MathF.Tau) * (1f - t * t);
                float ricasso = b.Ricasso > 0f ? 1f - Math.Clamp((t - b.Ricasso) / 0.03f, 0f, 1f) : 0f;
                float groove = b.Fuller * Math.Clamp((t - b.FullerFrom) / 0.04f, 0f, 1f) * Math.Clamp((b.FullerTo - t) / 0.06f, 0f, 1f);
                float thick = MathF.Max(MathF.Min(b.Thick * MathF.Max(1f - t * 0.6f, 0.12f), w * 0.75f), 0.003f);
                rings[i] = new Ring
                {
                    Centre = new Vector3(centre, baseY + t * b.Length, 0f),
                    Side = Vector3.UnitX,
                    Front = Vector3.UnitZ,
                    HalfWidth = w,
                    HalfDepth = thick,
                    Shape = Section.BladeSection(b.Flat * (1f - ricasso) + ricasso * 0.9f, b.FullerWidth, groove * (1f - ricasso), b.EdgeBand * (1f - ricasso), MathF.Max(b.Bias > 0f ? b.Spine : 0.04f, ricasso * 0.85f), 0.04f + ricasso * 0.8f),
                };
            }

            VertexMaterial spotted = GearColours.Of(GearMaterials.Metal, new Vector3(0.24f, 0.13f, 0.07f));
            mesh.Loft(Section.BladeHard(), rings, (ring, point, after) =>
            {
                float t = ts[ring];
                bool edge = Section.IsFrontBand(point, after) || b.Bias <= 0f && Section.IsBackBand(point, after);
                VertexMaterial material = edge && t > b.Ricasso ? colours.Edge : Section.IsFuller(point) && b.Fuller > 0f && t > b.FullerFrom && t < b.FullerTo ? colours.MetalDark : colours.Metal;
                return b.Rust > 0.05f && Patch(ring, point, genes) < b.Rust ? Mixed(material, spotted, 0.65f) : material;
            }, capStart: true, capEnd: true);

            if (b.Runes) Runes(mesh, genes.Part("runes"), b, rings, ts);
            if (b.Etch > 0f) Etching(mesh, genes.Part("etch"), b, rings, ts, colours.Precious);
        }

        // A panel etched and gilded on each face just above the base, its sides following the flat and its end coming to a point: laid on, a hair proud, so its edge is crisp.
        private static void Etching(GearMesh mesh, Genes genes, BladeShape b, Ring[] rings, float[] ts, VertexMaterial gilt)
        {
            float from = b.Ricasso + 0.01f, to = MathF.Min(from + b.Etch, b.TipStart - 0.05f);
            if (to - from < 0.03f) return;
            float share = b.Flat * genes.Range("width", 0.55f, 0.85f);
            foreach (float face in new[] { -1f, 1f })
            {
                var panel = new List<Ring>();
                for (int i = 0; i < rings.Length; i++)
                {
                    float t = ts[i];
                    if (t < from || t > to) continue;
                    float end = Math.Clamp((to - t) / ((to - from) * 0.3f), 0f, 1f);
                    Ring at = rings[i];
                    float lift = at.HalfDepth * (b.Fuller > 0f && t > b.FullerFrom && t < b.FullerTo ? 1f - b.Fuller : 1f);
                    panel.Add(new Ring { Centre = at.Centre + Vector3.UnitZ * face * (lift + 0.0015f), Side = Vector3.UnitX, Front = Vector3.UnitZ * face, HalfWidth = MathF.Max(at.HalfWidth * share * MathF.Sqrt(end), 0.002f), HalfDepth = 0.0012f });
                }
                if (panel.Count >= 2) mesh.Loft(Section.Slab(0.3f), panel, gilt);
            }
        }

        // Marks set into the fuller on both faces, small enough to read as a line of them, lit from inside on what has a power in it or is one of a kind, and gilded on anything else.
        private static void Runes(GearMesh mesh, Genes genes, BladeShape b, Ring[] rings, float[] ts)
        {
            int count = genes.Count("count", 4, 9);
            float from = b.FullerFrom + 0.04f, to = b.FullerTo - 0.06f;
            if (to <= from) return;
            for (int i = 0; i < count; i++)
            {
                float t = from + (to - from) * (i + 0.5f) / count;
                int ring = Array.FindIndex(ts, x => x >= t);
                if (ring < 0) continue;
                Ring at = rings[ring];
                float size = MathF.Min(at.HalfWidth * b.FullerWidth * b.Flat * 0.8f, (to - from) * b.Length / count * 0.35f);
                if (size < 0.004f) continue;
                int sides = genes.Count($"r{i}.sides", 3, 6);
                foreach (float face in new[] { -1f, 1f })
                {
                    float z = at.HalfDepth * face * (1f - b.Fuller * 0.85f);
                    mesh.At(Matrix4.CreateRotationX(face * MathF.PI / 2f) * Matrix4.CreateTranslation(at.Centre.X, at.Centre.Y, z), () =>
                    {
                        mesh.Lathe(Section.Polygon(sides, genes.Unit($"r{i}.turn")), at.HalfDepth * 0.18f, 1, _ => size, b.RuneMaterial!.Value);
                    });
                }
            }
        }

        // The guard: a bar across where the blade meets the grip, from a block round the tang, its arms drawn as curves and finished in the motif, with whatever else covers the hand. What
        // comes back is the top of it, where the blade starts.
        private static float Guard(GearMesh mesh, GearLook look, Genes genes, BladeShape blade, float gripTop, float grip, bool twoHands)
        {
            GearColours colours = look.Colours;
            GearForm form = look.Spec.Form;
            float cover = form.Cover;
            float baseHalf = blade.Width * blade.BaseWidth * (blade.Ricasso > 0f ? blade.RicassoWidth : 1f);
            bool dagger = look.Spec.Kind == GearKind.Dagger;

            // A disc for a rondel dagger, now and then, instead of a bar.
            float thick = genes.Range("thick", 0.024f, 0.045f) * (0.75f + 0.8f * look.Heft) * MathF.Sqrt(MathF.Max(blade.Width / 0.15f, 0.45f)) * (twoHands ? 1.3f : 1f);
            float fullHalf = blade.Width * blade.BaseWidth;
            float blockHeight = thick * genes.Range("block.height", 1.8f, 3.6f);
            float blockWidth = MathF.Max(baseHalf * genes.Range("block.width", 1.05f, 1.35f), thick * 1.4f);
            float deep = MathF.Max(blade.Thick * 1.6f, thick * genes.Range("deep", 1f, 1.5f));
            if (dagger && genes.Chance("rondel", 0.3f))
            {
                float disc = baseHalf * genes.Range("disc", 1.8f, 2.8f);
                mesh.At(Matrix4.CreateTranslation(0f, gripTop, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(genes.Count("disc.sides", 6, 10)) : Section.Round(16), thick * 1.2f, 2, t => disc * (1f - 0.08f * MathF.Abs(t * 2f - 1f)), colours.Fitting, squash: 0.9f));
                return gripTop + thick * 1.2f;
            }

            float span = MathF.Max(fullHalf * genes.Around("span", 2.4f + 2.6f * cover, 0.5f) * (0.8f + 0.4f * look.Flare) * (twoHands ? 1.35f : 1f), blockWidth * 1.4f);
            if (look.Legendary) span *= genes.Range("span.grand", 1.05f, 1.3f);
            float droop = span * genes.Around("droop", look.Curl > 0.5f ? 0.16f : 0.05f, 0.14f);
            float recurve = look.Curl * genes.Range("recurve", 0f, 0.22f) * span;
            float swell = genes.Range("swell", 0.6f, 1.5f) * (0.7f + 0.6f * look.Flare);
            float y = gripTop + blockHeight * 0.55f;
            Section bar = look.Motif == Motif.Flute ? Section.Fluted(12, 4, 0.2f) : look.Angular > 0.55f ? Section.Slab(genes.Range("bevel", 0.12f, 0.45f)) : look.Angular > 0.35f ? Section.Polygon(8) : Section.Super(10, genes.Range("round", 1.8f, 2.8f));

            Vector3 Point(float s, float side) => new Vector3(s * span * side, y + droop * s * s - recurve * MathF.Sin(s * MathF.PI) * s, recurve * 0.3f * s * s * side * 0f);
            void Arm(float side)
            {
                var keys = new[] { Point(0f, side), Point(0.35f, side), Point(0.7f, side), Point(1f, side) };
                List<Vector3> spine = Spine.Through(keys, 10);
                bool pointed = look.Motif == Motif.Fang || genes.Chance("pointed", look.Spikes * 0.6f);
                mesh.Sweep(bar, spine, t =>
                {
                    float end = pointed ? MathF.Pow(1f - t, 0.7f) * 0.9f + 0.1f : 1f + (swell - 1f) * t * t;
                    return new Vector2(deep * end * 0.62f, thick * end);
                }, colours.Fitting, Vector3.UnitY, capStart: false, capEnd: true);
                if (!pointed)
                {
                    Vector3 tip = Point(1f, side), before = Point(0.96f, side);
                    GearKit.Tip(mesh, look, genes.Part("tip"), tip, tip - before, Vector3.UnitZ, thick * swell * genes.Range("tip.size", 0.9f, 1.4f), colours.Fitting);
                }
            }
            Arm(1f);
            if (!genes.Chance("one.sided", dagger ? 0.08f : 0.03f)) Arm(-1f);

            // The block the arms come out of, round where the blade goes in, with an escutcheon running a little way down the blade on finer work.
            mesh.At(Matrix4.CreateTranslation(0f, gripTop, 0f), () => mesh.Block(blockWidth, blockHeight, deep * 0.62f, look.Angular > 0.5f ? 0.3f : 0.55f, colours.Fitting));
            if (genes.Chance("escutcheon", 0.25f + look.Craft * 0.5f))
            {
                float reach = baseHalf * genes.Range("escutcheon.reach", 1f, 2.4f);
                float half = baseHalf * genes.Range("escutcheon.width", 0.45f, 0.85f);
                foreach (float face in new[] { -1f, 1f })
                {
                    mesh.At(Matrix4.CreateTranslation(0f, gripTop + blockHeight * 0.9f, face * (blade.Thick * 0.62f + 0.004f)), () =>
                    {
                        mesh.Loft(Section.Slab(0.35f), new[] { Ring.Flat(0f, half, 0.006f, true), Ring.Flat(reach * 0.55f, half * 0.7f, 0.006f), Ring.Flat(reach, half * 0.06f, 0.004f, true) }, colours.Fitting);
                    });
                }
            }

            // A ring at the side, or a bow round the knuckles, for those who asked for their hand to be covered.
            if (!dagger && !twoHands && genes.Chance("side.ring", MathF.Max(cover - 0.45f, 0f) * 1.4f))
            {
                float ring = baseHalf * genes.Range("side.ring.size", 1.6f, 2.4f);
                mesh.At(Matrix4.CreateRotationZ(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y - thick * 0.2f, 0f) * Matrix4.CreateRotationY(genes.Sign("side.ring.side") * MathF.PI / 2f), () =>
                    mesh.Hoop(Section.Round(6), ring, thick * 0.4f, thick * 0.4f, 18, colours.Fitting));
            }
            if (!twoHands && genes.Chance("knuckle.bow", MathF.Max(cover - 0.6f, 0f) * 1.6f))
            {
                float side = genes.Sign("knuckle.side");
                float out_ = genes.Range("knuckle.out", 0.1f, 0.18f) + baseHalf * 0.4f;
                var keys = new[]
                {
                    Point(0.55f, side), new Vector3(out_ * 1.1f * side, gripTop - grip * 0.15f, 0f), new Vector3(out_ * side, gripTop - grip * 0.6f, 0f), new Vector3(out_ * 0.4f * side, gripTop - grip * 1.02f, 0f),
                };
                mesh.Sweep(bar, Spine.Through(keys, 12), _ => new Vector2(deep * 0.4f, thick * 0.55f), colours.Fitting, Vector3.UnitZ);
            }

            // Lugs at the top of a long ricasso, to catch a blade that's slid down it.
            if (twoHands && blade.Ricasso > 0.05f && genes.Chance("lugs", 0.6f))
            {
                float at = gripTop + blockHeight + blade.Ricasso * blade.Length;
                mesh.BothSides(() =>
                {
                    var keys = new[] { new Vector3(baseHalf * blade.RicassoWidth * 0.9f, at, 0f), new Vector3(baseHalf * 1.5f, at - baseHalf * 0.15f, 0f), new Vector3(baseHalf * 1.85f, at - baseHalf * 0.6f, 0f) };
                    mesh.Sweep(Section.Slab(0.4f), Spine.Through(keys, 6), t => new Vector2(blade.Thick * 0.9f * (1f - t * 0.75f), blade.Thick * 0.9f * (1f - t * 0.75f)), colours.Fitting, Vector3.UnitZ);
                });
            }

            // A stone in the block, on both faces, on what's worth it.
            if (look.Shines("guard.stone"))
            {
                float stone = MathF.Min(blockHeight, blockWidth) * genes.Range("stone", 0.32f, 0.45f);
                foreach (float face in new[] { -1f, 1f })
                {
                    mesh.At(Matrix4.CreateRotationX(face * MathF.PI / 2f) * Matrix4.CreateTranslation(0f, gripTop + blockHeight * 0.5f, face * deep * 0.55f), () => GearKit.Setting(mesh, genes.Part("stone"), stone, colours.Precious, look.Theme != GearTheme.None ? colours.Glow : colours.Gem));
                }
            }
            if (look.Legendary) Filigree(mesh, look, genes.Part("filigree"), Point, span, y, thick);
            return gripTop + blockHeight;
        }

        // Scrolls of precious wire over a legendary guard's arms, both faces.
        private static void Filigree(GearMesh mesh, GearLook look, Genes genes, Func<float, float, Vector3> point, float span, float y, float thick)
        {
            int scrolls = genes.Count("count", 1, 2);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < scrolls; i++)
                {
                    float s = 0.25f + 0.6f * (i + 0.5f) / scrolls;
                    Vector3 at = point(s, side);
                    foreach (float face in new[] { -1f, 1f })
                    {
                        GearKit.Scroll(mesh, at + new Vector3(0f, -thick * 0.7f, face * thick * 0.95f), new Vector3(side, 0f, 0f), Vector3.UnitY, thick * genes.Range("size", 0.9f, 1.4f), genes.Range("turns", 1f, 1.5f), thick * 0.16f, look.Colours.Precious);
                    }
                }
            }
        }

        // Something to hold: a core that swells or waists, wrapped as its maker liked, with a metal collar at either end on finer work.
        private static void Grip(GearMesh mesh, GearLook look, Genes genes, float bottom, float top, bool twoHands)
        {
            GearColours colours = look.Colours;
            float radius = genes.Range("radius", 0.05f, 0.072f) * (0.85f + 0.35f * look.Heft) * (twoHands ? 1.3f : 1f);
            Profile shape = new Profile((0f, genes.Range("foot", 0.84f, 1f)), (genes.Range("swell.at", 0.35f, 0.65f), genes.Range("swell", 1f, 1.2f)), (1f, genes.Range("head", 0.8f, 1f)));
            float collar = look.Craft > 0.35f && genes.Chance("ferrules", 0.3f + look.Craft * 0.6f) ? radius * genes.Range("ferrule", 0.5f, 0.9f) : 0f;
            float from = bottom + collar, to = top - collar;

            int wrap = genes.Pick("wrap", 4f, 1.5f + look.Craft * 2.5f, 1.5f + look.Leather * 2f, 1f + (1f - look.Craft) * 2f);
            switch (wrap)
            {
                case 0:
                    // A strap of leather, wound round and round.
                    mesh.Wrap(from, to, t => radius * shape.At(t), genes.Range("pitch", 0.04f, 0.07f) * (twoHands ? 1.35f : 1f), genes.Range("ridge", 0.07f, 0.13f), colours.Strap, sides: 12);
                    break;
                case 1:
                    // Twisted wire, close wound: so close that at any distance it's seen from it's turns standing up in rings, and it's made as that.
                    int turns = Math.Clamp((int)((to - from) / genes.Range("wire.pitch", 0.022f, 0.032f)), 6, 40);
                    float ridge = genes.Range("wire.ridge", 0.05f, 0.09f);
                    var wire = new Ring[turns * 2 + 1];
                    for (int i = 0; i < wire.Length; i++)
                    {
                        float t = (float)i / (wire.Length - 1);
                        float r = radius * shape.At(t) * (1f + (i % 2 == 1 ? ridge : 0f));
                        wire[i] = Ring.Flat(from + (to - from) * t, r, r * 0.95f);
                    }
                    mesh.Loft(Section.Round(12), wire, genes.Chance("wire.precious", look.Craft) ? colours.Precious : colours.Fitting, false, false);
                    break;
                case 2:
                    // Cord over risers, which stand up in rings under it.
                    int risers = genes.Count("risers", 2, 5);
                    var rings = new Ring[risers * 6 + 1];
                    for (int i = 0; i < rings.Length; i++)
                    {
                        float t = (float)i / (rings.Length - 1);
                        float riser = MathF.Pow(MathF.Abs(MathF.Sin(t * (risers + 1) * MathF.PI)), 6f);
                        float r = radius * shape.At(t) * (1f + 0.12f * riser);
                        rings[i] = Ring.Flat(from + (to - from) * t, r, r * 0.92f);
                    }
                    mesh.Loft(Section.Round(12), rings, colours.Leather, false, false);
                    break;
                default:
                    // Bare wood, grooved for the fingers.
                    int grooves = genes.Count("grooves", 3, 6);
                    var plain = new Ring[grooves * 4 + 1];
                    for (int i = 0; i < plain.Length; i++)
                    {
                        float t = (float)i / (plain.Length - 1);
                        float r = radius * shape.At(t) * (1f - 0.06f * MathF.Pow(MathF.Abs(MathF.Sin(t * grooves * MathF.PI)), 0.5f));
                        plain[i] = Ring.Flat(from + (to - from) * t, r, r * 0.9f);
                    }
                    mesh.Loft(look.Angular > 0.6f ? Section.Polygon(8) : Section.Round(12), plain, colours.Wood, false, false);
                    break;
            }

            if (collar <= 0f) return;
            foreach ((float at, float r) in new[] { (bottom, radius * shape.At(0f)), (top - collar, radius * shape.At(1f)) })
            {
                mesh.At(Matrix4.CreateTranslation(0f, at, 0f), () => mesh.Lathe(look.Angular > 0.5f ? Section.Polygon(8) : Section.Round(12), collar, 2, t => r * (1.12f + 0.06f * MathF.Sin(t * MathF.PI)), colours.Fitting));
            }
        }

        // The weight at the end, which keeps a blade in balance: the motif's turned end, a wheel, a fishtail, or a ring, with a peen on top where the tang comes through.
        private static void Pommel(GearMesh mesh, GearLook look, Genes genes, BladeShape blade, float y, bool twoHands)
        {
            GearColours colours = look.Colours;
            float size = genes.Range("size", 0.085f, 0.14f) * (0.75f + 0.55f * look.Heft) * MathF.Sqrt(MathF.Max(blade.Width / 0.16f, 0.5f)) * (twoHands ? 1.4f : 1f) * (look.Legendary ? 1.15f : 1f);
            int kind = genes.Pick("kind", 4f, 1.5f + look.Heavy * 2f + look.Angular, 1f + look.Curl * 2f, look.Spec.Kind == GearKind.Dagger ? 1.5f : 0.3f);
            float height;
            switch (kind)
            {
                case 1:
                    // A wheel: a disc on its edge, its faces sunk, with a boss in the middle of each.
                    float wheel = size * genes.Range("wheel", 1.15f, 1.5f);
                    float across = size * genes.Range("wheel.thick", 0.55f, 0.8f);
                    height = wheel * 2f;
                    mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y - wheel, -across * 0.5f), () =>
                    {
                        mesh.Lathe(look.Angular > 0.6f ? Section.Polygon(genes.Count("wheel.sides", 8, 12)) : Section.Round(18), across, 3, t => wheel * (0.94f + 0.06f * MathF.Sin(t * MathF.PI)), colours.Fitting);
                        mesh.Lathe(Section.Round(12), across * 1.3f, 2, t => wheel * 0.4f, colours.FittingDark);
                    });
                    break;
                case 2:
                    // A fishtail, flat and spreading.
                    height = size * genes.Range("tail.height", 1.3f, 1.9f);
                    float spread = size * genes.Range("tail.spread", 1.2f, 1.8f);
                    var tail = new Ring[6];
                    for (int i = 0; i < tail.Length; i++)
                    {
                        float t = i / 5f;
                        tail[i] = new Ring { Centre = new Vector3(0f, y - t * height, 0f), Side = Vector3.UnitX, Front = Vector3.UnitZ, HalfWidth = size * 0.45f + (spread - size * 0.45f) * t * t, HalfDepth = size * 0.42f * (1f - 0.3f * t), Hard = i == 0 || i == 5 };
                    }
                    mesh.Loft(Section.Slab(0.45f), tail, colours.Fitting);
                    break;
                case 3:
                    // A ring, as a seax's or an old dagger's.
                    float ring = size * genes.Range("ring", 0.9f, 1.3f);
                    height = ring * 2.2f;
                    mesh.At(Matrix4.CreateRotationX(MathF.PI / 2f) * Matrix4.CreateTranslation(0f, y - ring * 1.1f, 0f), () => mesh.Hoop(Section.Round(8), ring, size * 0.22f, size * 0.22f, 20, colours.Fitting));
                    break;
                default:
                    height = size * genes.Range("height", 1.2f, 2.3f);
                    mesh.At(Matrix4.CreateTranslation(0f, y - height, 0f), () => GearKit.Terminal(mesh, look, genes.Part("end"), height, size, colours.Fitting));
                    break;
            }

            // The peen, where the tang is hammered over the end.
            if (kind != 3 && genes.Chance("peen", 0.55f))
            {
                float peen = size * genes.Range("peen", 0.22f, 0.35f);
                mesh.At(Matrix4.CreateTranslation(0f, y - height - peen * 0.7f, 0f), () => mesh.Lathe(Section.Round(8), peen * 0.8f, 2, t => peen * (1f - t * 0.4f), colours.FittingDark));
            }

            if (kind != 3 && look.Shines("pommel.stone"))
            {
                float stone = size * genes.Range("stone", 0.32f, 0.48f);
                float middle = y - height * 0.5f;
                foreach (float face in new[] { -1f, 1f })
                {
                    mesh.At(Matrix4.CreateRotationX(face * MathF.PI / 2f) * Matrix4.CreateTranslation(0f, middle, face * size * 0.62f), () => GearKit.Setting(mesh, genes.Part("stone"), stone, colours.Precious, look.Theme != GearTheme.None ? colours.Glow : colours.Gem));
                }
            }
        }

        // A knot of cord through the hilt with a tassel on it, as the finest are carried.
        private static void Knot(GearMesh mesh, GearLook look, Genes genes, float y)
        {
            GearKit.Tassel(mesh, genes, new Vector3(0f, y - 0.05f, 0f), genes.Range("length", 0.22f, 0.4f), genes.Range("radius", 0.035f, 0.05f), look.Colours.Trim, look.Colours.Precious);
        }

        // Two colours mixed as albedos, and kept clear of the Reich's as everything is (GearColours.Of): rust on a dark blade is rust, not black iron.
        private static VertexMaterial Mixed(in VertexMaterial material, in VertexMaterial other, float share) =>
            GearColours.Of(material.materialTextureIndex, Vector3.Lerp(GearColours.AlbedoOf(material), GearColours.AlbedoOf(other), share));

        // Fixed for a place on a thing, so a patch of rust is the same patch wherever it's drawn.
        private static float Patch(int ring, int point, Genes genes) => genes.Part(ring / 3).Unit($"patch{point / 3}");
    }
}
