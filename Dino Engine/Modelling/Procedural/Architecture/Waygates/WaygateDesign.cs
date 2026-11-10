using Dino_Engine.Particles;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture.Waygates
{
    public enum WaygateKind
    {
        Land,       // One of a land's old roads.
        Great,      // The Haven's, which reaches every land.
    }

    /// <summary>A ring of an arch: its stones run from <see cref="Inner"/> to <see cref="Outer"/> metres from the centres of its two arcs, and <see cref="HalfThick"/> either side of the way's face.</summary>
    public sealed class ArchRing
    {
        public float Inner;
        public float Outer;
        public float HalfThick;
        public int Stones;          // Each side of the keystone.
        public float KeyHalf;       // Radians of arc the keystone takes on each side of the apex, at its inner face.
        public float KeyLift;       // Metres the keystone stands above the ring's outer face.
        public float KeyProud;      // Metres it stands out of the ring's faces, front and back.
    }

    /// <summary>Where a rune is cut: its middle, the face it's on (Right across it, Up up it, Normal out of it), how big it is, which it is, and when it lights as the way opens, 0 first and 1 last.</summary>
    public readonly record struct RuneSlot(Vector3 Centre, Vector3 Right, Vector3 Up, Vector3 Normal, float Size, int Glyph, float Along);

    /// <summary>
    /// A waygate as numbers, in its own frame: x across, y up from where it stands, the face of its way at z = 0, and its front towards +z, as every model's is. Its frame
    /// (<see cref="WaygateForge.Frame"/>) and what stands in it once it's open (<see cref="WaygateForge.Way"/>) are both built from this and nothing else, so the way fills the
    /// opening exactly, and its edge runs <see cref="Tuck"/> into the stone all the way round: there's nowhere for a gap to be.
    /// The opening is a jamb either side, square up from the sill to the springing, under a pointed arch of two arcs, each centred on the springing line a little past the
    /// middle (<see cref="Offset"/>). Headless.
    /// </summary>
    public sealed class WaygateDesign
    {
        public WaygateKind Kind { get; private init; }
        public string Id { get; private init; } = "";

        // The opening.
        public float HalfWidth { get; private init; }   // From the middle to each jamb's face.
        public float Sill { get; private init; }        // The threshold's height.
        public float Spring { get; private init; }      // Where the arch begins.
        public float Radius { get; private init; }      // Of each of the arch's arcs: more than HalfWidth, so it comes to a point.

        /// <summary>Metres the way's edge runs into the stone, past the opening, all the way round.</summary>
        public const float Tuck = 0.12f;

        /// <summary>
        /// What each kind of gate's stone does to the rock it's faced with (MaterialCatalog.Rock), as a vertex colour: the Haven's dressed pale and warm, a land's older,
        /// greyer and greener. The Haven's paving is the Haven's stone at its shade on the ground (MaterialRecipes.Flagstones reads both), so they're one stone.
        /// </summary>
        public static readonly Vector3 GreatStone = new Vector3(1.26f, 1.19f, 1.07f);
        public static readonly Vector3 LandStone = new Vector3(1.16f, 1.17f, 1.09f);

        /// <summary>How much darker a stone is at the foot of a gate than up it, where the weather has had longest: its shade at height y is this, rising to 1 by 2.5 m.</summary>
        public static float WeatherShade(float y) => 0.84f + 0.16f * Math.Clamp(y / 2.5f, 0f, 1f);

        /// <summary>The shade of the stone a gate stands on, its plinth's treads: what paving laid from its steps has to match.</summary>
        public static float GroundShade => WeatherShade(0.6f);

        /// <summary>Metres a rune's dark inlay stands proud of its stone, and its light proud of that: far enough apart that the depth buffer never mistakes one for the other.</summary>
        public const float InlayLift = 0.008f;
        public const float GlowLift = 0.016f;

        // The frame.
        public ArchRing[] Rings { get; private init; } = Array.Empty<ArchRing>();
        public float JambOuter { get; private init; }       // x of a jamb's outer face.
        public float JambHalfThick { get; private init; }
        public float[] Courses { get; private init; } = Array.Empty<float>();  // A jamb's stones, bottom up, between its base and its impost.
        public float JambBase { get; private init; }        // Height of the stone a jamb stands on.
        public float ImpostHeight { get; private init; }
        public (float HalfWidth, float HalfDepth, float Top, int Stones)[] Tiers { get; private init; } = Array.Empty<(float, float, float, int)>();  // The plinth, bottom up.
        public float Footing { get; private init; }         // How far the plinth goes into the ground, for a slope.

        // What the way gives off.
        public Vector3 Light { get; private init; }         // The front light; the back one is behind it.
        public float LightIntensity { get; private init; }
        public float LightRadius { get; private init; }
        public float BackIntensity { get; private init; }
        public float BackRadius { get; private init; }

        /// <summary>Metres from <see cref="Heart"/> that everything the way draws stays within, for culling.</summary>
        public float Reach { get; private init; }

        public float Offset => Radius - HalfWidth;
        public float Apex => Spring + MathF.Sqrt(Radius * Radius - Offset * Offset);

        /// <summary>The angle at which each arc meets the middle, from the springing line.</summary>
        public float ArcTop => ArcTopAt(Radius);

        /// <summary>Where the way's throat is centred: a little under halfway up, as the arch's point is narrow.</summary>
        public Vector2 Heart => new Vector2(0f, Sill + (Apex - Sill) * 0.46f);

        public IReadOnlyList<RuneSlot> Runes => _runes ??= WaygateForge.Runes(this);
        private IReadOnlyList<RuneSlot>? _runes;

        /// <summary>The way, for an effect to fill (Particles): its opening, upright, square to the gate's front.</summary>
        public IEffectFace Face => _face ??= new WayFace(this);
        private WayFace? _face;

        public static readonly WaygateDesign Land = new WaygateDesign
        {
            Kind = WaygateKind.Land,
            Id = "waygate",
            HalfWidth = 2.95f,
            Sill = 0.34f,
            Spring = 3.9f,
            Radius = 3.54f,
            Rings = new[] { new ArchRing { Inner = 3.54f, Outer = 4.44f, HalfThick = 0.55f, Stones = 5, KeyHalf = 0.11f, KeyLift = 0.34f, KeyProud = 0.1f } },
            JambOuter = 3.95f,
            JambHalfThick = 0.55f,
            JambBase = 0.44f,
            Courses = new[] { 0.98f, 0.9f, 0.94f },
            ImpostHeight = 0.3f,
            Tiers = new[] { (4.35f, 1.55f, 0.16f, 5), (4f, 1.15f, 0.34f, 7) },
            Footing = 0.9f,
            Light = new Vector3(0f, 3.9f, 1.95f),
            LightIntensity = 150f,
            LightRadius = 18f,
            BackIntensity = 110f,
            BackRadius = 14f,
            Reach = 6.6f,
        };

        // Wider than a land's rather than much taller: the camera looks down from high in the south, and from where a hero arrives and uses it the whole of its way is in view, up to
        // the cornice. Its crown and crystal are for the village, seen from further off.
        public static readonly WaygateDesign Great = new WaygateDesign
        {
            Kind = WaygateKind.Great,
            Id = "waygate.great",
            HalfWidth = 4.3f,
            Sill = 0.9f,
            Spring = 5.6f,
            Radius = 4.95f,
            Rings = new[]
            {
                new ArchRing { Inner = 4.95f, Outer = 5.8f, HalfThick = 0.75f, Stones = 7, KeyHalf = 0.085f, KeyLift = 0f, KeyProud = 0.12f },
                new ArchRing { Inner = 5.8f, Outer = 6.55f, HalfThick = 0.62f, Stones = 8, KeyHalf = 0.075f, KeyLift = 0.3f, KeyProud = 0.1f },
            },
            JambOuter = 6f,
            JambHalfThick = 0.95f,
            JambBase = 0.7f,
            Courses = new[] { 0.92f, 0.88f, 0.92f, 0.88f },
            ImpostHeight = 0.4f,
            Tiers = new[] { (8.5f, 3.3f, 0.3f, 13), (8.2f, 2.65f, 0.6f, 13), (7.9f, 2f, 0.9f, 13) },
            Footing = 1f,
            Light = new Vector3(0f, 5.6f, 3.9f),
            LightIntensity = 280f,
            LightRadius = 22f,
            BackIntensity = 180f,
            BackRadius = 16f,
            Reach = 11f,
        };

        public static WaygateDesign Of(WaygateKind kind) => kind == WaygateKind.Great ? Great : Land;

        /// <summary>The angle at which an arc of this radius, about the same centres as the opening's, meets the middle.</summary>
        public float ArcTopAt(float radius) => MathF.Acos(Math.Clamp(Offset / radius, -1f, 1f));

        /// <summary>A point of the right-hand arc of radius <paramref name="radius"/> (about the centre left of the middle), at <paramref name="angle"/> up from the springing line. The left is its mirror.</summary>
        public Vector2 OnArc(float radius, float angle) => new Vector2(-Offset + radius * MathF.Cos(angle), Spring + radius * MathF.Sin(angle));

        /// <summary>Metres from the opening's edge, less than 0 inside it: exact inside, which is all anything asks for. Way/WayShape.glsl is the same.</summary>
        public float Distance(Vector2 p) => Distance(p, 0f);

        /// <summary>As <see cref="Distance(Vector2)"/>, for the opening grown by <paramref name="grow"/> all the way round.</summary>
        public float Distance(Vector2 p, float grow)
        {
            float across = MathF.Abs(p.X);
            float d = MathF.Max(across - HalfWidth - grow, Sill - grow - p.Y);
            if (p.Y > Spring) d = MathF.Max(d, new Vector2(across + Offset, p.Y - Spring).Length - Radius - grow);
            return d;
        }

        /// <summary>
        /// The opening's edge grown by <paramref name="grow"/>, counter-clockwise seen from the front, from the bottom of the left jamb: along the sill, up the right jamb, over the
        /// arch and down the left jamb. <paramref name="arcSteps"/> to each arc.
        /// </summary>
        public List<Vector2> Outline(float grow, int arcSteps)
        {
            float radius = Radius + grow;
            float top = ArcTopAt(radius);
            var points = new List<Vector2>
            {
                new Vector2(-HalfWidth - grow, Sill - grow),
                new Vector2(HalfWidth + grow, Sill - grow),
            };
            for (int i = 0; i <= arcSteps; i++) points.Add(OnArc(radius, top * i / arcSteps));
            for (int i = arcSteps - 1; i >= 0; i--)
            {
                Vector2 right = OnArc(radius, top * i / arcSteps);
                points.Add(new Vector2(-right.X, right.Y));
            }
            return points;
        }

        // The way as an effect's face: the opening, a few centimetres inside its edge so that nothing starts in the stone.
        private sealed class WayFace : IEffectFace
        {
            private const float Inset = 0.15f;
            private readonly WaygateDesign _design;
            private readonly List<Vector2> _edge;
            private readonly float[] _along;
            private readonly Vector2 _low, _high;

            public WayFace(WaygateDesign design)
            {
                _design = design;
                _edge = design.Outline(-Inset, 24);
                _along = new float[_edge.Count + 1];
                float area = 0f;
                for (int i = 0; i < _edge.Count; i++)
                {
                    Vector2 a = _edge[i], b = _edge[(i + 1) % _edge.Count];
                    _along[i + 1] = _along[i] + (b - a).Length;
                    area += a.X * b.Y - b.X * a.Y;
                }
                SquareMetres = MathF.Abs(area) * 0.5f;
                _low = new Vector2(-design.HalfWidth + Inset, design.Sill + Inset);
                _high = new Vector2(design.HalfWidth - Inset, design.Apex - Inset);
                Vector2 heart = design.Heart;
                Middle = new Vector3(heart.X, heart.Y, 0f);
                Reach = _edge.Max(p => (p - heart).Length);
            }

            public float SquareMetres { get; }
            public Vector3 Middle { get; }
            public float Reach { get; }

            public Vector3 Anywhere(float u, float v)
            {
                // Anywhere in its box, and whatever falls outside the arch drawn in towards its heart until it's in: a little more under the point than evenly, which nobody will count.
                Vector2 p = new Vector2(_low.X + (_high.X - _low.X) * u, _low.Y + (_high.Y - _low.Y) * v);
                Vector2 heart = _design.Heart;
                for (int i = 0; i < 6 && _design.Distance(p) > -Inset; i++) p = heart + (p - heart) * 0.8f;
                return new Vector3(p.X, p.Y, 0f);
            }

            public Vector3 Edge(float u)
            {
                float at = Math.Clamp(u, 0f, 1f) * _along[^1];
                int i = 0;
                while (i < _edge.Count - 1 && _along[i + 1] < at) i++;
                Vector2 a = _edge[i], b = _edge[(i + 1) % _edge.Count];
                float span = _along[i + 1] - _along[i];
                Vector2 p = span > 1e-5f ? Vector2.Lerp(a, b, (at - _along[i]) / span) : a;
                return new Vector3(p.X, p.Y, 0f);
            }
        }
    }
}
