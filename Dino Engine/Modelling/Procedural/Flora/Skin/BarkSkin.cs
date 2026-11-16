using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>What covers the wood. A material is a kind of surface and its colour comes from here, so one bark can be every bark of that kind.</summary>
    public enum BarkKind
    {
        Furrowed,   // Deep ridges: oak, elm, old maple.
        Plated,     // Flaking plates: pine, spruce.
        Papery,     // Peeling, with dark marks: birch.
        Smooth,     // Tight, with lenticels: beech, cherry, young maple.
        Fibrous,    // Long soft strips: redwood, cedar, cypress.
        Ringed,     // Scarred in rings: palms.
        Cane,       // Smooth and jointed: bamboo, reeds.
        Bare,       // Silvered wood with no bark left.
        Charred,    // Burnt to scales.
        Stem,       // Green and soft: anything that isn't wood.
        Fissured,   // Thick plates in deep fissures, flaking in layers: the foot of an old pine. Gives way to Flaking (BarkForm.Upper).
        Flaking,    // Thin papery flakes, peeling: a pine's upper trunk and its limbs.
    }

    public sealed class BarkForm
    {
        public BarkKind Kind = BarkKind.Furrowed;
        public Vector3 Colour = new Vector3(0.8f, 0.72f, 0.62f);        // Multiplies the material, which is what makes it this tree's bark.
        public Vector3 YoungColour = new Vector3(0.62f, 0.52f, 0.42f);  // What it comes to on thin branches.
        public Vector3? UpperColour;                                    // What it turns to up the trunk, as a Scots pine goes orange.
        public float UpperFrom = 0.5f;

        /// <summary>
        /// The bark it gives way to up the trunk, by <see cref="UpperFrom"/> of the tree's height, and on every limb, as a pine's thick plates give way to thin flaking bark: the plates
        /// stand as islands in it, higher up one side than the other, before the last of them goes. Its material has to be the next layer after this one's, which only
        /// <see cref="BarkKind.Fissured"/> to <see cref="BarkKind.Flaking"/> is (MaterialCatalog.GivesWay). Null for a bark that's one surface all over.
        /// </summary>
        public BarkKind? Upper;
        public float Tile = 2.4f;           // Metres the texture covers, on wood thick enough to show it whole.
        public float Bumps = 0.09f;         // How lumpy a stem is, as a share of its radius.
        public bool RoundTips;              // Stems end in a dome and not a point.
        public int Ribs;                    // Ridges running its whole length: a cactus.
        public float RibDepth = 0.12f;
        public float NodeSpacing;           // Metres between the joints of a cane, or 0.
        public float Moss;                  // 0 to 1, how much of it is under moss.
        public Vector3 MossColour = new Vector3(0.2f, 0.3f, 0.07f);
        public int MostSides = 16;

        /// <summary>
        /// How far a limb swells where it leaves what it grew from, as a share of its own radius: its collar, which every limb of every tree has. A limb is never set on its parent, it
        /// grows out of it, and this is the wood the two share.
        /// </summary>
        public float Collar = 0.3f;

        /// <summary>
        /// 0 to 1: how much the shade wood stands in inside a crown darkens it. All of it for a broadleaf's, which is dark inside; less for a pine's open crown, where a trunk's orange
        /// shows between the pads all the way up.
        /// </summary>
        public float Shaded = 1f;
    }

    /// <summary>
    /// Turns axes into wood: tubes, as generalized cylinders (Bloomenthal 1985) on parallel transport frames, so nothing twists along a bend, each leaving what it grew from through
    /// a collar. Rings are spaced by how thick a stem is, and have as many sides as that's worth: a detail smaller than a pixel from this camera is a fault, so the top of a tree
    /// costs a fraction of its foot.
    ///
    /// A limb's skin starts where it comes out of its parent's, never inside it: an open ring at the heart of a trunk shows wherever the limb is nearly as thick as the trunk, or
    /// the trunk thins or dents just past the fork, which is what a gap at a join was. Its foot is laid on the faces of the parent's own mesh, found by casting along each of its
    /// lines, a little under them, with the parent's normals and colour there, so light and bark run from one onto the other; from there it swells out of the parent and draws in
    /// to its own girth (<see cref="BarkForm.Collar"/>), as a limb's collar does. See Skin in FLORA.md.
    /// </summary>
    internal static class BarkSkin
    {
        private const float RingPitch = 2.2f;       // Diameters between rings, on a stem that's going straight.
        private const float LongestRing = 2.6f;     // Metres, or a tall trunk is a cone between its foot and the first thing that changes it.

        /// <summary>
        /// How much taller than wide a bark's pattern may be drawn. Round a stem it's always a whole number of tiles, so on thin wood it's squeezed, and running it up the stem by
        /// the metre as well drew a trunk's fissures on a branch as pinstripes, eight times as tall as wide. It's run up a stem by its girth instead, so the pattern shrinks whole
        /// as the wood thins, and its relief with it, a little taller than wide as young bark is.
        /// </summary>
        private const float TallestGrain = 1.5f;

        // Collars. A limb thinner than TwigRadius only starts under its parent's bark, as a collar on it would be a pixel. Up to BranchRadius its foot is turned to lie on its
        // parent and that's all; up to LimbRadius it has a ring between its foot and its own girth, and over that two, round a foot of more sides than a stem that thin gets
        // anywhere else, as a foot is where a limb is looked at. A tree's triangles are paid for thousands of times over, and most of its joins are twigs.
        private const float TwigRadius = 0.03f;
        private const float BranchRadius = 0.06f;
        private const float LimbRadius = 0.12f;
        private const float FlareLength = 1.5f;     // How far up a limb its collar can reach, and how far out over its parent, in the limb's radii.
        private const float FilletRadius = 1.9f;    // The radius a collar rounds the corner between limb and parent with, in the limb's radii, for each unit of BarkForm.Collar.
        private const int Scans = 8;                // Steps down one of a limb's lines, looking for where it goes under.
        private const float RoundedSink = 0.003f;   // Metres a foot that lies along its parent is set under it, so the two never share a plane.

        /// <summary>
        /// How many lines of collars found a face of their parent to end on, and how many didn't and were set on its smooth shape instead, since these were last cleared: for the
        /// lab, which says how often (Dino FloraLab joins).
        /// </summary>
        internal static int Landed, Unlanded;

        internal struct Ring
        {
            public Vector3 Centre;
            public Vector3 Along;
            public Vector3 Side;
            public float Radius;
            public float Travelled;
            public float Share;
            public float Light;     // What reaches it: wood inside a crown is much darker than a sun would make it, as a sun can't get in.
            public Vector3 Stretch; // Where a stem turns, its ring lies across the turn, half way between the way in and the way out, and is longer that way
            public float StretchBy; // by this share, so that the stem is as thick either side of it as it was: a mitre, as two pipes are joined at an angle.
        }

        public static void Wrap(FloraMesh mesh, List<PlantAxis> axes, BarkForm bark, TreeForm form, Genes genes, float trunkRadius, float height, int lobes, float lobeFacing)
        {
            uint seed = (uint)genes.Stream("seed").NextULong();
            float lobeDepth = genes.Range("lobe depth", form.Trunk.LobeDepth);
            float flare = genes.Range("flare", form.Trunk.Flare);
            float flareHeight = form.Trunk.FlareHeight * height;
            float tint = genes.Range("tint", 0.88f, 1.12f);
            Vector3 mossAway = Vector3.Normalize(new Vector3(genes.Range("moss x", -1f, 1f), 0.45f, genes.Range("moss z", -1f, 1f)));
            // What's died back to bare wood is silvered and has nothing on it.
            Vector3 dead = form.Stubs?.Colour ?? bark.Colour;
            var deadWood = new BarkForm { Kind = BarkKind.Bare, Colour = dead, YoungColour = dead * 0.92f, Tile = 1.2f, Bumps = 0.05f, MostSides = 4, Collar = 0.15f };

            foreach (PlantAxis axis in axes)
            {
                bool trunk = axis.Order == 0 && !axis.Root;
                var style = new SkinStyle
                {
                    Bark = axis.Bare ? deadWood : bark,
                    Seed = seed + (uint)axis.Points.Count * 7919u,
                    Trunk = trunk,
                    Root = axis.Root,
                    Lobes = trunk ? lobes : 0,
                    LobeDepth = lobeDepth,
                    LobeFacing = lobeFacing,
                    Flare = trunk ? flare : 0f,
                    FlareHeight = flareHeight,
                    Tint = tint,
                    MossAway = mossAway,
                    TreeHeight = height,
                    ThickRadius = trunkRadius,
                };
                Skin(mesh, axis, style);
            }
        }

        public struct SkinStyle
        {
            public BarkForm Bark;
            public uint Seed;
            public bool Trunk;
            public bool Root;
            public int Lobes;
            public float LobeDepth;
            public float LobeFacing;    // Which way in the world the first ridge looks, in radians.
            public float Flare;
            public float FlareHeight;
            public float Tint;
            public Vector3 MossAway;
            public float TreeHeight;
            public float ThickRadius;
        }

        /// <summary>Wood with no tree to go with it: a log or a stump. It's given no joints, as nothing moves it.</summary>
        public static void WrapPlain(FloraMesh mesh, List<PlantAxis> axes, BarkForm bark, Genes genes, float radius, float height)
        {
            uint seed = (uint)genes.Stream("seed").NextULong();
            Vector3 mossAway = Vector3.Normalize(new Vector3(genes.Range("moss x", -0.3f, 0.3f), 1f, genes.Range("moss z", -0.3f, 0.3f)));
            float tint = genes.Range("tint", 0.88f, 1.12f);
            foreach (PlantAxis axis in axes)
            {
                axis.FirstLink = -1;
                var style = new SkinStyle { Bark = bark, Seed = seed + (uint)axis.Points.Count * 7919u, Tint = tint, MossAway = mossAway, TreeHeight = MathF.Max(height, 3f), ThickRadius = radius };
                Skin(mesh, axis, style);
            }
        }

        public static void Skin(FloraMesh mesh, PlantAxis axis, in SkinStyle style)
        {
            axis.NodeRings = style.Bark.NodeSpacing;
            if (axis.Points.Count < 2) return;

            // Where it leaves what it grew from, if that has a skin to leave: its own starts there.
            Collar? collar = axis.Parent?.Sleeve is Sleeve host ? Collar.Plan(axis, host, style) : null;
            List<Ring> rings = Rings(axis, style.Trunk ? style.FlareHeight : 0f, collar?.TopShare ?? 0f, collar?.Top ?? 0f);
            if (rings.Count < 2) return;
            if (style.Bark.RoundTips) Dome(rings);

            int material = FloraMaterials.Bark(style.Bark.Kind);
            // A bark that gives way to another runs into it through its vertices, if its material is the one before the other's, as only such a pair can be (MaterialCatalog.GivesWay).
            // Otherwise what gives way wholly takes the other bark whole, a limb, say, and a trunk keeps its own: never a triangle between two materials that aren't neighbours.
            bool runs = false;
            if (style.Bark.Upper is BarkKind upper)
            {
                int upperMaterial = FloraMaterials.Bark(upper);
                runs = upperMaterial == material + 1;
                if (!runs && GivenWay(rings[0].Centre, style) >= 1f) material = upperMaterial;
            }
            float tile = MathF.Max(style.Bark.Tile, 0.05f);
            float aroundTiles = MathF.Max(MathF.Round(MathF.Tau * rings[0].Radius / tile), 1f);
            // The moss painted into the bark also stands on it as shells (SHELLS.md), as much as was painted.
            if (style.Bark.Moss > 0f) mesh.ShellStyle = Rendering.Shells.ShellStyles.BarkMoss;

            var sections = new List<Ring>();
            var sectionSides = new List<int>();
            var spokes = new List<float[]?>();
            var strips = new List<(int First, int Count)>();
            // A limb's collar, and its rings up to the one it's free from, are held by whatever holds the wood its foot is on (Hold). That ring is its first, unless the next
            // is hard by it, as rings are through a turn: what the limb and its parent move apart by is taken up between that ring and the next, which need room for it.
            int free = 0, holder = -1;
            Foot[]? feet = null;
            if (collar != null)
            {
                while (free < Math.Min(rings.Count - 2, 3) && rings[free + 1].Travelled - rings[free].Travelled < MathF.Max(rings[free].Radius * 1.5f, 0.12f)) free++;

                // What's of it inside its parent has no skin, but it's still where the limb is, for whatever leaves it that near its foot.
                collar.Frame(rings[0]);
                for (int k = 0; k + 1 < collar.Centres.Length; k++)
                {
                    sections.Add(collar.At(collar.Arcs[k]));
                    sectionSides.Add(0);
                    spokes.Add(null);
                    strips.Add((0, 0));
                }
            }

            int previousStart = -1, previousSides = 0;
            float v = 0f, previousTravelled = rings[0].Travelled, previousUp = 0f;

            for (int r = 0; r < rings.Count; r++)
            {
                Ring ring = rings[r];
                bool last = r == rings.Count - 1;
                float girth = ring.Radius * (1f + style.Flare * FlareAt(ring.Centre.Y, style));
                int sides = Sides(girth, style.Bark.MostSides);
                if (r == 0 && collar != null)
                {
                    sides = Math.Max(sides, Math.Min(collar.Sides, style.Bark.MostSides));
                    feet = Feet(mesh, style, collar, ring, sides);
                    // One joint for the whole of its foot, or the foot is pulled apart between two as they move: the one furthest out of those its lines ended on.
                    holder = -1;
                    foreach (Foot foot in feet)
                    {
                        if (foot.Landing.Found) holder = Math.Max(holder, foot.Landing.Joint);
                    }
                    if (holder < 0) holder = collar.HostJoint;
                    Hold(mesh, axis, rings[free], holder);
                }
                int joint = collar != null && r <= free ? holder : Rig.LinkAt(axis, ring.Share);
                Vector3 other = Vector3.Cross(ring.Along, ring.Side);
                int start = mesh.VertexCount;
                int firstIndex = mesh.Indices.Count;

                // The pattern is as tall as it's wide here, or a little taller: a whole number of tiles goes round, so how far up one reaches goes by the girth.
                float up = MathF.Max(MathF.Min(tile, MathF.Tau * girth / aroundTiles * TallestGrain), 1e-3f);
                if (r > 0) v += (ring.Travelled - previousTravelled) / ((up + previousUp) * 0.5f);
                previousTravelled = ring.Travelled;
                previousUp = up;

                if (last && !axis.Cut)
                {
                    // A stem comes to a point, and that point needs no ring: one vertex closes it. Thick wood that ends, where all that grew on from it is too fine to skin,
                    // draws out to its point over a few of its own widths, as a limb's end does: closed as near as a twig is, it was a pipe with a lid.
                    // A trunk's top is left as it was: how tall a tree stands is what was asked of it.
                    Vector3 tip = ring.Centre + ring.Along * (ring.Radius * (1f + (axis.Parent != null ? 2f * Smooth(0.03f, 0.1f, ring.Radius) : 0f)));
                    float tipGiven = GivenWay(tip, style);
                    mesh.Add(new FloraVertex
                    {
                        Position = tip,
                        Normal = ring.Along,
                        Tangent = ring.Side,
                        Colour = ColourAt(tip, ring.Along, ring.Radius, ring.Share, ring.Light, tipGiven, style, out float tipMoss),
                        UV = new Vector2(aroundTiles * 0.5f, v + (tip - ring.Centre).Length / up),
                        Material = material,
                        MaterialShare = runs ? tipGiven : 0f,
                        Joint = joint,
                        Shell = tipMoss,
                    });
                    for (int i = 0; i < previousSides; i++) mesh.Triangle(previousStart + i, previousStart + i + 1, start);
                    // Its skin closes on that point, so that's where it ends, with no girth left.
                    Ring point = ring;
                    point.Centre = tip;
                    point.Radius = 0f;
                    sections.Add(point);
                    sectionSides.Add(0);
                    spokes.Add(new float[3]);
                    strips.Add((firstIndex, mesh.Indices.Count - firstIndex));
                    break;
                }

                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * (MathF.Tau / sides);
                    Vector3 at = Surface(ring, other, angle, style, out Vector3 normal, out Vector3 tangent);
                    float given = GivenWay(at, style);
                    mesh.Add(new FloraVertex
                    {
                        Position = at,
                        Normal = normal,
                        Tangent = tangent,
                        Colour = ColourAt(at, normal, ring.Radius, ring.Share, ring.Light, given, style, out float moss),
                        UV = new Vector2(aroundTiles * i / sides, v),
                        Material = material,
                        MaterialShare = runs ? given : 0f,
                        Joint = joint,
                        Shell = moss,
                    });
                }

                if (previousStart >= 0) Stitch(mesh, previousStart, previousSides, start, sides);
                else if (collar != null) AddCollar(mesh, style, collar, feet!, ring, start, sides, material, runs, aroundTiles, v, up, holder);
                else if (axis.CutFoot) AddFace(mesh, ring, start, sides, joint, style, -1f);
                if (last) AddFace(mesh, ring, start, sides, joint, style, 1f);
                previousStart = start;
                previousSides = sides;
                var ringSpokes = new float[sides];
                for (int i = 0; i < sides; i++) ringSpokes[i] = (mesh.Vertices[start + i].Position - ring.Centre).Length;
                sections.Add(ring);
                sectionSides.Add(sides);
                spokes.Add(ringSpokes);
                strips.Add((firstIndex, mesh.Indices.Count - firstIndex));
            }

            axis.Sleeve = new Sleeve
            {
                Axis = axis,
                Style = style,
                Under = collar?.Hosts[0].Sleeve,
                Clear = rings[0].Travelled,
                Sections = sections.ToArray(),
                Sides = sectionSides.ToArray(),
                Spokes = spokes.ToArray(),
                Strips = strips.ToArray(),
            };
        }

        /// <summary>
        /// What an axis's skin left behind, for whatever grows from it to land on: where it runs and how thick, section by section from its very start (those inside its own parent
        /// have no faces), and which of the mesh's triangles lie between each two. Its smooth shape is what a limb is measured against, and its faces are what a limb's foot is laid on.
        /// </summary>
        internal sealed class Sleeve
        {
            public PlantAxis Axis = null!;
            public SkinStyle Style;
            public Ring[] Sections = Array.Empty<Ring>();
            public int[] Sides = Array.Empty<int>();                                // How many sides each has, 0 for one with no ring: inside its parent, or the point of a tip.
            public float[]?[] Spokes = Array.Empty<float[]?>();                     // How far out each vertex of each ring stands, or null for one with no ring to say.
            public (int First, int Count)[] Strips = Array.Empty<(int, int)>();     // The triangles between each section and the one before, as a run of the mesh's indices.
            public Sleeve? Under;                                                   // What its own foot stands in.
            public float Clear;                                                     // How far along it its tube proper begins, in metres.

            /// <summary>Which sections lie within <paramref name="reach"/> of a point along it, and how thick it is there.</summary>
            public void Window(Vector3 point, float reach, out int lo, out int hi, out float radius, out float travelled)
            {
                int nearest = 0;
                float best = float.MaxValue, at = 0f;
                for (int i = 0; i + 1 < Sections.Length; i++)
                {
                    float t = Along(Sections[i].Centre, Sections[i + 1].Centre, point, out float distance);
                    if (distance >= best) continue;
                    best = distance;
                    nearest = i;
                    at = t;
                }
                radius = MathHelper.Lerp(Sections[nearest].Radius, Sections[nearest + 1].Radius, at);
                travelled = MathHelper.Lerp(Sections[nearest].Travelled, Sections[nearest + 1].Travelled, at);
                lo = nearest;
                hi = nearest + 1;
                while (lo > 0 && Sections[lo].Travelled > travelled - reach) lo--;
                while (hi < Sections.Length - 1 && Sections[hi].Travelled < travelled + reach) hi++;
            }

            private static readonly float[] HalfSideCosines = Enumerable.Range(0, 40).Select(sides => sides < 3 ? 1f : MathF.Cos(MathF.PI / sides)).ToArray();

            // Which section a point is nearest the middle of.
            private int Nearby(Vector3 point, int lo, int hi)
            {
                int nearest = lo;
                float best = float.MaxValue;
                for (int i = lo; i <= hi; i++)
                {
                    float distance = (Sections[i].Centre - point).LengthSquared;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = i;
                }
                return nearest;
            }

            private static float Along(Vector3 a, Vector3 b, Vector3 point, out float distanceSquared)
            {
                Vector3 line = b - a;
                float lengthSquared = line.LengthSquared;
                float t = lengthSquared > 1e-10f ? Clamp(Vector3.Dot(point - a, line) / lengthSquared, 0f, 1f) : 0f;
                distanceSquared = (point - (a + line * t)).LengthSquared;
                return t;
            }

            /// <summary>
            /// How far outside it a point is, were its surface as smooth as its rings say and not the faces between them: negative inside. <paramref name="outward"/> is the way out
            /// of it there, and <paramref name="share"/> how far along it that is.
            /// </summary>
            public float Outside(Vector3 point, int lo, int hi, out Vector3 outward, out float radius, out float share)
            {
                int section = lo;
                float best = float.MaxValue, at = 0f;
                for (int i = lo; i < hi; i++)
                {
                    float t = Along(Sections[i].Centre, Sections[i + 1].Centre, point, out float distance);
                    if (distance >= best) continue;
                    best = distance;
                    section = i;
                    at = t;
                }

                Ring a = Sections[section], b = Sections[section + 1];
                Ring here = a;
                here.Centre = Vector3.Lerp(a.Centre, b.Centre, at);
                Vector3 along = b.Centre - a.Centre;
                along = along.LengthSquared > 1e-10f ? along.Normalized() : a.Along;
                Vector3 side = Vector3.Lerp(a.Side, b.Side, at);
                side -= along * Vector3.Dot(side, along);
                side = side.LengthSquared > 1e-8f ? side.Normalized() : Scaffold.Perpendicular(along);
                here.Along = along;
                here.Side = side;
                here.Radius = MathHelper.Lerp(a.Radius, b.Radius, at);
                here.Travelled = MathHelper.Lerp(a.Travelled, b.Travelled, at);
                here.Share = MathHelper.Lerp(a.Share, b.Share, at);
                share = here.Share;

                // Past the end of the stretch it's nearest, it's measured from that end, as from a ball there, and never from the stretch carried straight on: where a stem turns
                // sharply, or ends, there's no wood on that line, and a limb measured against it was set down in the air.
                Vector3 off = point - here.Centre;
                bool beyond = at <= 0f || at >= 1f;
                Vector3 radial = beyond ? off : off - along * Vector3.Dot(off, along);
                float distanceOut = radial.Length;
                outward = distanceOut > 1e-6f ? radial / distanceOut : side;
                Vector3 round = outward - along * Vector3.Dot(outward, along);
                float angle = round.LengthSquared > 1e-10f ? MathF.Atan2(Vector3.Dot(round, Vector3.Cross(along, side)), Vector3.Dot(round, side)) : 0f;
                // As thick as its faces are there, which run straight from one ring to the next and from one vertex of a ring to the next: between the two rings' own outlines at
                // this angle round each, and not what a ring here would have had. A stem's lumps come and go between rings a trunk's width apart, and the ridges of a foot between
                // the few vertices a ring has, and its faces know nothing of either: measured against a shape smoother than the mesh, a limb was set down half a metre clear of it.
                radius = MathHelper.Lerp(Girth(section, a, angle), Girth(section + 1, b, angle), at);
                return distanceOut - radius;
            }

            // How far out a section's outline stands at an angle round it: along the straight side between the two vertices of its ring either side of that angle, as its faces
            // run, or by its smooth shape where it has no ring.
            private float Girth(int index, in Ring ring, float angle)
            {
                float[]? spokes = Spokes[index];
                if (spokes == null) return RadiusAt(ring, ring.Side * MathF.Cos(angle) + Vector3.Cross(ring.Along, ring.Side) * MathF.Sin(angle), angle, Style);
                int count = spokes.Length;
                float turn = angle / MathF.Tau;
                turn -= MathF.Floor(turn);
                float place = turn * count;
                int i = Math.Min((int)place, count - 1);
                float from = spokes[i], to = spokes[(i + 1) % count];
                // Where a ray at that angle crosses the side from one vertex to the next: for a ring whose vertices stand as far out as each other, its apothem over the
                // cosine of how far the ray is from the middle of the side, and near enough the same between two that don't. This is asked a great many times.
                float half = MathF.PI / count;
                float apothem = count < HalfSideCosines.Length ? HalfSideCosines[count] : MathF.Cos(half);
                return MathHelper.Lerp(from, to, place - i) * apothem / MathF.Cos((place - i) * 2f * half - half);
            }

            /// <summary>The nearest point of any of its faces to a point, nearer than <paramref name="reach"/> squared, which it brings in to that: which triangle, and how much of each corner.</summary>
            public bool Nearest(FloraMesh mesh, Vector3 point, int lo, int hi, ref float reach, ref int corner, ref Vector3 weights)
            {
                bool found = false;
                Span<FloraVertex> vertices = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mesh.Vertices);
                Span<int> indices = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mesh.Indices);
                for (int s = lo + 1; s <= hi; s++)
                {
                    (int first, int count) = Strips[s];
                    for (int i = first; i < first + count; i += 3)
                    {
                        Vector3 a = vertices[indices[i]].Position, b = vertices[indices[i + 1]].Position, c = vertices[indices[i + 2]].Position;
                        Vector3 share = ClosestShares(point, a, b, c);
                        float distance = (a * share.X + b * share.Y + c * share.Z - point).LengthSquared;
                        if (distance >= reach) continue;
                        reach = distance;
                        corner = i;
                        weights = share;
                        found = true;
                    }
                }
                return found;
            }

            // How much of each corner of a triangle its nearest point to a point is made of (Ericson, Real-Time Collision Detection, 5.1.5).
            private static Vector3 ClosestShares(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 ab = b - a, ac = c - a, ap = p - a;
                float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0f && d2 <= 0f) return new Vector3(1f, 0f, 0f);
                Vector3 bp = p - b;
                float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3) return new Vector3(0f, 1f, 0f);
                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                {
                    float v = d1 / MathF.Max(d1 - d3, 1e-12f);
                    return new Vector3(1f - v, v, 0f);
                }
                Vector3 cp = p - c;
                float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0f && d5 <= d6) return new Vector3(0f, 0f, 1f);
                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                {
                    float w = d2 / MathF.Max(d2 - d6, 1e-12f);
                    return new Vector3(1f - w, 0f, w);
                }
                float va = d3 * d6 - d5 * d4;
                if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                {
                    float w = (d4 - d3) / MathF.Max((d4 - d3) + (d5 - d6), 1e-12f);
                    return new Vector3(0f, 1f - w, w);
                }
                float sum = va + vb + vc;
                if (MathF.Abs(sum) < 1e-20f) return new Vector3(1f, 0f, 0f);
                return new Vector3(va / sum, vb / sum, vc / sum);
            }

            /// <summary>
            /// The nearest of its faces along a ray, nearer than <paramref name="reach"/>, which it brings in to that face: which triangle, as where its corners are in the mesh's
            /// indices, and how much of each.
            /// </summary>
            public bool Cast(FloraMesh mesh, Vector3 origin, Vector3 direction, int lo, int hi, ref float reach, ref int corner, ref Vector3 weights)
            {
                // Only the faces between the sections either end of the ray is nearest, and a couple beyond: a thick trunk has a thousand faces within reach of a twig.
                int nearStart = Nearby(origin, lo, hi), nearEnd = Nearby(origin + direction * reach, lo, hi);
                if (nearStart > nearEnd) (nearStart, nearEnd) = (nearEnd, nearStart);
                lo = Math.Max(lo, nearStart - 2);
                hi = Math.Min(hi, nearEnd + 1);
                bool hit = false;
                // Read where they lie: a vertex is a great deal more than where it is, and fetching one from a list copies all of it.
                Span<FloraVertex> vertices = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mesh.Vertices);
                Span<int> indices = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(mesh.Indices);
                for (int s = lo + 1; s <= hi; s++)
                {
                    (int first, int count) = Strips[s];
                    for (int i = first; i < first + count; i += 3)
                    {
                        Vector3 a = vertices[indices[i]].Position;
                        Vector3 ab = vertices[indices[i + 1]].Position - a;
                        Vector3 ac = vertices[indices[i + 2]].Position - a;
                        // Möller and Trumbore, from either side.
                        Vector3 p = Vector3.Cross(direction, ac);
                        float det = Vector3.Dot(ab, p);
                        if (MathF.Abs(det) < 1e-12f) continue;
                        float inverse = 1f / det;
                        Vector3 from = origin - a;
                        float u = Vector3.Dot(from, p) * inverse;
                        if (u < -1e-4f || u > 1.0001f) continue;
                        Vector3 q = Vector3.Cross(from, ab);
                        float w = Vector3.Dot(direction, q) * inverse;
                        if (w < -1e-4f || u + w > 1.0001f) continue;
                        float t = Vector3.Dot(ac, q) * inverse;
                        if (t < 0f || t >= reach) continue;
                        reach = t;
                        corner = i;
                        weights = new Vector3(1f - u - w, u, w);
                        hit = true;
                    }
                }
                return hit;
            }
        }

        private struct Host
        {
            public Sleeve Sleeve;
            public int Lo, Hi;
        }

        // What a line of a limb's collar found of the parent where it went under: everything that should run on from the parent onto the limb.
        private struct Landing
        {
            public bool Found;
            public Vector3 Normal;
            public Vector3 Colour;
            public float Shell;
            public float Given;
            public int Material;
            public int Joint;
        }

        // Where one line of a collar ends, and how it gets there.
        private struct Foot
        {
            public Vector3 At;          // On its parent, or just under it.
            public float Arc;           // How far along the limb its line met the parent, in metres from the limb's start.
            public bool Rounded;        // It doesn't end where it met the parent, but turns there to lie along it: from Start on its own line, round Corner, to At.
            public Vector3 Corner;
            public Vector3 Start;
            public float StartArc;
            public Vector3 HostOut;     // The way out of the parent where it ends.
            public Landing Landing;
            public FootKind Kind;
            public float Girth;         // How far out its line stands, for the limb's girth.
        }

        /// <summary>
        /// Where a limb leaves what it grew from, and so where its skin starts. Its line runs from the heart of its parent; this follows it out to the parent's surface, and a little
        /// further, to where the limb is clear of the swelling it leaves through, which is where its tube proper begins (<see cref="TopShare"/>). Between there and the parent is its
        /// collar, built by <see cref="AddCollar"/> once that first ring is made.
        /// </summary>
        private sealed class Collar
        {
            public Host[] Hosts = Array.Empty<Host>();
            public float TopShare;                              // How far along the axis its tube proper begins, as Sample counts it.
            public Vector3[] Centres = Array.Empty<Vector3>();  // Its line from its start to there,
            public float[] Radii = Array.Empty<float>();        // how thick it is along it,
            public float[] Arcs = Array.Empty<float>();         // and how far along each of those is, in metres.
            public Vector3[] Alongs = Array.Empty<Vector3>();
            public Vector3[] Frames = Array.Empty<Vector3>();
            public float Top => Arcs[^1];
            public float Exit;                                  // How far along it its heart comes out of its parent,
            public Vector3 ExitNormal;                          // which way the parent's surface faces there,
            public float ExitSine;                              // and how squarely it comes through it.
            public float ChildRadius, HostRadius;
            public float Flare;                                 // Metres up it that its collar can reach.
            public float Round;                                 // Metres: the radius it rounds the corner between itself and its parent with.
            public float Sag;                                   // How far the parent's faces can lie inside its smooth shape: the sagitta of its widest side.
            public int Levels;                                  // Rings below its first proper one.
            public int Sides;                                   // The fewest sides its foot is worth.
            public bool Lands;                                  // Its foot is laid on its parent's faces, with their normals; a twig's is only tucked under them.
            public int HostJoint;
            private float _topShare;

            public static Collar? Plan(PlantAxis axis, Sleeve parent, in SkinStyle style)
            {
                if (parent.Sections.Length < 2) return null;
                float length = axis.Length;
                if (length < 1e-3f) return null;
                Vector3 foot = axis.Points[0];
                float childRadius = MathF.Max(axis.Radii[0], 1e-3f);

                // What it leaves: its parent, and if it leaves that near enough the parent's own foot, what the parent stands in too, as it comes out through both.
                parent.Window(foot, 0f, out _, out _, out float hostRadius, out float hostTravelled);
                hostRadius = MathF.Max(hostRadius, 1e-3f);
                float reach = MathF.Min(length * 0.7f, hostRadius * 3.5f + childRadius * 5f + 0.1f);
                var collar = new Collar { ChildRadius = childRadius, HostRadius = hostRadius };
                parent.Window(foot, reach + hostRadius, out int lo, out int hi, out _, out _);
                var first = new Host { Sleeve = parent, Lo = lo, Hi = hi };
                if (parent.Under is Sleeve under && under.Sections.Length >= 2 && hostTravelled - reach < parent.Clear)
                {
                    under.Window(foot, reach + hostRadius * 2f, out int underLo, out int underHi, out _, out _);
                    collar.Hosts = new[] { first, new Host { Sleeve = under, Lo = underLo, Hi = underHi } };
                }
                else collar.Hosts = new[] { first };

                // Its line as far as could matter, and where along that its heart comes out.
                const int Steps = 12;
                float cap = ShareAtArc(axis, reach);
                if (cap <= 1e-4f) return null;
                var centres = new Vector3[Steps + 1];
                var arcs = new float[Steps + 1];
                int clear = -1;
                for (int k = 0; k <= Steps; k++)
                {
                    centres[k] = Sample(axis, cap * k / Steps, out _);
                    arcs[k] = k == 0 ? 0f : arcs[k - 1] + (centres[k] - centres[k - 1]).Length;
                    if (clear < 0 && collar.Outside(centres[k], out _, out _, out _, out _) > 0f) clear = k;
                }
                // It never comes out, or was never in: there's nothing to leave, and it's skinned from its start as a stem on its own is.
                if (clear <= 0) return null;

                float inside = cap * (clear - 1) / Steps, beyond = cap * clear / Steps;
                for (int i = 0; i < 6; i++)
                {
                    float middle = (inside + beyond) * 0.5f;
                    if (collar.Outside(Sample(axis, middle, out _), out _, out _, out _, out _) > 0f) beyond = middle;
                    else inside = middle;
                }
                float exitShare = (inside + beyond) * 0.5f;
                collar.Outside(Sample(axis, exitShare, out _), out int which, out collar.ExitNormal, out _, out float hostShare);
                Vector3 heading = centres[clear] - centres[clear - 1];
                heading = heading.LengthSquared > 1e-10f ? heading.Normalized() : collar.ExitNormal;
                collar.ExitSine = Clamp(Vector3.Dot(heading, collar.ExitNormal), 0.15f, 1f);
                float exitArc = MathHelper.Lerp(arcs[clear - 1], arcs[clear], Clamp(exitShare * Steps / cap - (clear - 1), 0f, 1f));
                collar.HostJoint = Rig.LinkAt(collar.Hosts[which].Sleeve.Axis, hostShare);

                Sleeve landsOn = collar.Hosts[which].Sleeve;
                int hostSides = 0;
                for (int i = collar.Hosts[which].Lo; i <= collar.Hosts[which].Hi; i++) hostSides = Math.Max(hostSides, landsOn.Sides[i]);
                collar.Sag = hostSides >= 3 ? hostRadius * 1.25f * (1f - MathF.Cos(MathF.PI / hostSides)) : hostRadius * 0.08f;

                bool twig = childRadius < TwigRadius || style.Bark.Collar <= 0f;
                collar.Lands = !twig;
                collar.Levels = twig || childRadius < BranchRadius ? 1 : childRadius < LimbRadius ? 2 : 3;
                collar.Sides = twig ? 0 : childRadius < BranchRadius ? 4 : childRadius < LimbRadius ? 6 : 8;
                collar.Round = twig ? 0f : style.Bark.Collar * FilletRadius * childRadius;
                collar.Flare = twig ? childRadius * 0.5f : Clamp(FlareLength * childRadius, 0.03f, hostRadius * 0.9f);

                // Its upper side comes out later than its heart, by how steeply it leaves, and it swells for a way beyond that.
                float cotangent = MathF.Sqrt(MathF.Max(1f - collar.ExitSine * collar.ExitSine, 0f)) / collar.ExitSine;
                float top = MathF.Min(exitArc + childRadius * MathF.Min(cotangent, 4f) + collar.Flare + childRadius * 0.25f, arcs[Steps]);
                if (top <= exitArc + 1e-4f) return null;
                int over = 1;
                while (over < Steps && arcs[over] < top) over++;
                float between = arcs[over] > arcs[over - 1] ? (top - arcs[over - 1]) / (arcs[over] - arcs[over - 1]) : 1f;
                collar.TopShare = cap * (over - 1 + Clamp(between, 0f, 1f)) / Steps;
                if (collar.TopShare >= 0.98f || collar.TopShare <= 1e-4f) return null;

                int samples = Clamp((int)MathF.Ceiling(top / MathF.Max(hostRadius * 0.3f, 0.02f)), 4, 12);
                collar.Centres = new Vector3[samples + 1];
                collar.Radii = new float[samples + 1];
                collar.Arcs = new float[samples + 1];
                for (int k = 0; k <= samples; k++)
                {
                    collar.Centres[k] = Sample(axis, collar.TopShare * k / samples, out collar.Radii[k]);
                    collar.Arcs[k] = k == 0 ? 0f : collar.Arcs[k - 1] + (collar.Centres[k] - collar.Centres[k - 1]).Length;
                }
                if (collar.Top < 1e-4f) return null;

                // Now that it's known how far the collar reaches, only what's that near of what it leaves is looked at again: every line of every foot asks, many times.
                float near = collar.Top + collar.Flare + hostRadius * 0.75f;
                for (int h = 0; h < collar.Hosts.Length; h++)
                {
                    collar.Hosts[h].Sleeve.Window(foot, near, out collar.Hosts[h].Lo, out collar.Hosts[h].Hi, out _, out _);
                }
                // By the line as it's kept, which is a little shorter than the one it was found along, as it has fewer points.
                collar.Exit = MathF.Min(exitArc, collar.Top);
                return collar;
            }

            /// <summary>Gives its line the frames its first ring's leads back to, by parallel transport, so that a line of the collar is a line of the tube carried on.</summary>
            public void Frame(in Ring top)
            {
                int count = Centres.Length;
                Alongs = new Vector3[count];
                Frames = new Vector3[count];
                Alongs[count - 1] = top.Along;
                Frames[count - 1] = top.Side;
                _topShare = top.Share;
                for (int k = count - 2; k >= 0; k--)
                {
                    Vector3 along = Centres[k + 1] - Centres[Math.Max(k - 1, 0)];
                    along = along.LengthSquared > 1e-10f ? along.Normalized() : Alongs[k + 1];
                    Frames[k] = Transport(Frames[k + 1], Alongs[k + 1], along);
                    Alongs[k] = along;
                }
            }

            /// <summary>The limb's section so far along its line, in metres from its start. Before its start it runs straight on back, as its lines have to end somewhere.</summary>
            public Ring At(float arc)
            {
                if (arc <= 0f) return new Ring { Centre = Centres[0] + Alongs[0] * arc, Along = Alongs[0], Side = Frames[0], Radius = Radii[0], Travelled = arc, Share = 0f, Light = 1f };
                int k = Arcs.Length - 2;
                while (k > 0 && Arcs[k] > arc) k--;
                float span = Arcs[k + 1] - Arcs[k];
                float t = span > 1e-6f ? Clamp((arc - Arcs[k]) / span, 0f, 1f) : 0f;
                Vector3 along = Vector3.Lerp(Alongs[k], Alongs[k + 1], t);
                along = along.LengthSquared > 1e-10f ? along.Normalized() : Alongs[k];
                Vector3 side = Vector3.Lerp(Frames[k], Frames[k + 1], t);
                side -= along * Vector3.Dot(side, along);
                side = side.LengthSquared > 1e-8f ? side.Normalized() : Scaffold.Perpendicular(along);
                return new Ring
                {
                    Centre = Vector3.Lerp(Centres[k], Centres[k + 1], t),
                    Along = along,
                    Side = side,
                    Radius = MathHelper.Lerp(Radii[k], Radii[k + 1], t),
                    Travelled = arc,
                    Share = _topShare * arc / MathF.Max(Top, 1e-4f),
                    Light = 1f,
                };
            }

            /// <summary>How far outside everything it leaves a point is, by their smooth shapes: negative inside any of them.</summary>
            public float Outside(Vector3 point, out int which, out Vector3 outward, out float radius, out float share)
            {
                which = 0;
                float least = Hosts[0].Sleeve.Outside(point, Hosts[0].Lo, Hosts[0].Hi, out outward, out radius, out share);
                for (int h = 1; h < Hosts.Length; h++)
                {
                    float distance = Hosts[h].Sleeve.Outside(point, Hosts[h].Lo, Hosts[h].Hi, out Vector3 way, out float thick, out float along);
                    if (distance >= least) continue;
                    least = distance;
                    which = h;
                    outward = way;
                    radius = thick;
                    share = along;
                }
                return least;
            }

            /// <summary>The first face of anything it leaves along a ray, and what the parent is there.</summary>
            public bool Cast(FloraMesh mesh, Vector3 origin, Vector3 direction, float reach, out float distance, out Landing landing)
            {
                int corner = -1;
                Vector3 weights = default;
                distance = reach;
                for (int h = 0; h < Hosts.Length; h++) Hosts[h].Sleeve.Cast(mesh, origin, direction, Hosts[h].Lo, Hosts[h].Hi, ref distance, ref corner, ref weights);
                return Read(mesh, corner, weights, out landing);
            }

            /// <summary>The nearest point of any face of anything it leaves to a point, and what the parent is there.</summary>
            public bool Nearest(FloraMesh mesh, Vector3 point, out Vector3 on, out Landing landing)
            {
                int corner = -1;
                Vector3 weights = default;
                float reach = float.MaxValue;
                for (int h = 0; h < Hosts.Length; h++) Hosts[h].Sleeve.Nearest(mesh, point, Hosts[h].Lo, Hosts[h].Hi, ref reach, ref corner, ref weights);
                on = point;
                if (!Read(mesh, corner, weights, out landing)) return false;
                on = mesh.Vertices[mesh.Indices[corner]].Position * weights.X + mesh.Vertices[mesh.Indices[corner + 1]].Position * weights.Y + mesh.Vertices[mesh.Indices[corner + 2]].Position * weights.Z;
                return true;
            }

            // What the parent is at a point of one of its faces: its normal, colour and moss there, and which of its joints holds it.
            private static bool Read(FloraMesh mesh, int corner, Vector3 weights, out Landing landing)
            {
                landing = default;
                if (corner < 0) return false;

                FloraVertex a = mesh.Vertices[mesh.Indices[corner]], b = mesh.Vertices[mesh.Indices[corner + 1]], c = mesh.Vertices[mesh.Indices[corner + 2]];
                Vector3 normal = a.Normal * weights.X + b.Normal * weights.Y + c.Normal * weights.Z;
                if (normal.LengthSquared < 1e-8f) return false;
                // Which joint holds the bark there. A face can lie across two, where a limb is handed from the joint that holds its foot to its own; then it's the one further out,
                // which is the later made, as what's held by that goes by where it is along it and so moves as the face does, and what's held by the other is left behind.
                landing = new Landing
                {
                    Found = true,
                    Normal = normal.Normalized(),
                    Colour = a.Colour * weights.X + b.Colour * weights.Y + c.Colour * weights.Z,
                    Shell = a.Shell * weights.X + b.Shell * weights.Y + c.Shell * weights.Z,
                    Given = a.MaterialShare * weights.X + b.MaterialShare * weights.Y + c.MaterialShare * weights.Z,
                    Material = a.Material,
                    Joint = Math.Max(a.Joint, Math.Max(b.Joint, c.Joint)),
                };
                return true;
            }
        }

        // A point of a limb's collar: on the line of it that leaves its first ring at an angle round it (given with its cosine and sine), so far along the limb from its start.
        // The line is the limb's own, no thicker than the limb: swollen all round where it comes out, a limb as thick as its parent stood proud of it in a lip where its sides
        // should run flush out of the parent's. What a collar adds is in its corners (Land).
        // How far out it stands goes by how far out its vertex of the first ring does for that ring's girth (girth), so a line runs on from its vertex with no step, and
        // the limb's lumps aren't asked for again at every point of every line, which is most of what a foot costs to find.
        private static Vector3 OnCollar(Collar collar, float arc, float cosine, float sine, float girth, out Ring section, out Vector3 outward)
        {
            section = collar.At(arc);
            outward = section.Side * cosine + Vector3.Cross(section.Along, section.Side) * sine;
            return section.Centre + outward * (section.Radius * girth);
        }

        private static float Clear(Collar collar, float arc, float cosine, float sine, float girth)
        {
            return collar.Outside(OnCollar(collar, arc, cosine, sine, girth, out _, out _), out _, out _, out _, out _);
        }

        // Where a twig's line ends: just under its parent's bark. Where that is goes by the lie of the parent where the twig's heart came out, and then by the parent's own faces,
        // looked for down the line from there, as a twig as thick as a finger set by a shape that's a few centimetres off its faces is a twig hanging in the air.
        private static Foot Tuck(FloraMesh mesh, Collar collar, float cosine, float sine, float girth, float lowest)
        {
            Ring exit = collar.At(collar.Exit);
            Vector3 way = exit.Side * cosine + Vector3.Cross(exit.Along, exit.Side) * sine;
            float radius = collar.ChildRadius;
            float top = collar.Top;
            float arc = Clamp(collar.Exit - radius * Vector3.Dot(way, collar.ExitNormal) / collar.ExitSine, lowest, top - 1e-3f);
            var foot = new Foot { Kind = FootKind.Tucked, HostOut = collar.ExitNormal, Girth = girth };
            Vector3 met = OnCollar(collar, arc, cosine, sine, girth, out Ring section, out _);
            Vector3 down = -section.Along;
            float margin = (collar.Sag + 0.02f + collar.HostRadius * 0.2f) / MathF.Max(collar.ExitSine, 0.3f);
            // From no higher than its first ring, or a face above that would turn its foot inside out.
            float back = MathF.Min(margin, (top - arc) * 0.9f);
            const float Under = 0.006f;
            if (collar.Cast(mesh, met - down * back, down, back + margin, out float along, out _))
            {
                foot.Arc = arc - (along - back) - Under;
                foot.At = met + down * (along - back + Under);
                Interlocked.Increment(ref Landed);
            }
            else
            {
                // No face down its line, as on a twig nearly as thick as what it leaves, whose sides pass it by: it ends on the nearest there is.
                foot.Arc = arc;
                if (collar.Nearest(mesh, met, out Vector3 nearest, out Landing on)) foot.At = nearest - on.Normal * 0.004f;
                else foot.At = OnCollar(collar, MathF.Max(arc - margin, lowest), cosine, sine, girth, out _, out _);
                Interlocked.Increment(ref Unlanded);
            }
            return foot;
        }

        /// <summary>
        /// Where a limb's line ends. It's followed down from the limb's first ring to where it meets the parent, which is a corner: the limb's line going one way and the parent's
        /// surface another. A limb doesn't meet its parent in a corner. The wood of the two runs together, so the line turns there to lie along the parent, rounding the corner by
        /// a radius of the limb's own size, and ends flat on the parent's faces a little way off, found by dropping onto them. How far it takes to turn goes by how open the corner
        /// is: hardly any way where the limb's side runs on from the parent's nearly straight, as the outside of a fork does, a limb's radius under a limb standing straight out,
        /// and little again in a tight crotch, which is a crease in a real tree and reads as putty filled in.
        /// </summary>
        private static Foot Land(FloraMesh mesh, Collar collar, float cosine, float sine, float girth, float lowest, float sink)
        {
            float top = collar.Top;
            float step = (top - lowest) / Scans;
            var foot = new Foot { Girth = girth };

            float outsideAbove = Clear(collar, top, cosine, sine, girth);
            if (outsideAbove < 0f)
            {
                // Its first ring is already under here, as on the inside of a tight fork: the limb only carries on back a little, inside what it's in.
                // It ends on the nearest face of that, which if it really is under is somewhere it can't be seen, and if the parent's shape was wrong about it is at least on the parent.
                foot.Kind = FootKind.Sunk;
                foot.Arc = top - collar.ChildRadius * 0.4f;
                Vector3 under = OnCollar(collar, foot.Arc, cosine, sine, girth, out _, out foot.HostOut);
                foot.At = collar.Nearest(mesh, under, out Vector3 nearest, out foot.Landing) ? nearest - foot.Landing.Normal * sink : under;
                return foot;
            }

            // Where the line goes under, looking down it from the first ring.
            bool found = false;
            float above = top, met = top, least = outsideAbove, leastArc = top;
            for (int i = 1; i <= Scans; i++)
            {
                float arc = top - i * step;
                float outside = Clear(collar, arc, cosine, sine, girth);
                if (outside < 0f)
                {
                    // Between the last step clear of it and this one under it, by where a straight line between what they measured says, three times: how far outside it is
                    // runs nearly straight along a line this short, so that's within a millimetre, in a third of the looks halving takes.
                    float inside = arc, clear = above, under = outside, over = outsideAbove;
                    for (int b = 0; b < 3; b++)
                    {
                        float middle = clear + (inside - clear) * Clamp(over / MathF.Max(over - under, 1e-9f), 0.08f, 0.92f);
                        float between = Clear(collar, middle, cosine, sine, girth);
                        if (between < 0f)
                        {
                            inside = middle;
                            under = between;
                        }
                        else
                        {
                            clear = middle;
                            over = between;
                        }
                    }
                    met = clear + (inside - clear) * Clamp(over / MathF.Max(over - under, 1e-9f), 0f, 1f);
                    found = true;
                    break;
                }
                if (outside < least)
                {
                    least = outside;
                    leastArc = arc;
                }
                above = arc;
                outsideAbove = outside;
            }
            if (!found) met = leastArc;

            Vector3 corner = OnCollar(collar, met, cosine, sine, girth, out Ring section, out Vector3 way);
            float cornerOutside = collar.Outside(corner, out _, out Vector3 hostOut, out float hostRadius, out _);
            foot.Arc = met;
            foot.Corner = corner;
            foot.HostOut = hostOut;

            float nudge = MathF.Max(step * 0.25f, 0.004f);
            Vector3 upLine = OnCollar(collar, met + nudge, cosine, sine, girth, out _, out _) - corner;
            upLine = upLine.LengthSquared > 1e-12f ? upLine.Normalized() : section.Along;

            if (!found)
            {
                // It never goes under: a limb as wide as what it leaves, or wider, overhanging its side, as the outside of a fork of two equal stems does. It's brought in to
                // the parent from where it passed nearest, but not straight in, which is a ledge round the fork: it runs on down the parent a way, the way it was going, and
                // is laid on it there, so the limb's side draws in to the parent's as the wood under a fork does.
                foot.Kind = FootKind.Brought;
                Vector3 onParent = corner - hostOut * cornerOutside;
                Vector3 onward = -upLine - hostOut * Vector3.Dot(-upLine, hostOut);
                float run = MathF.Min(collar.Flare, hostRadius * 0.6f), climb = MathF.Min(collar.Flare, top - met - 1e-3f);
                if (onward.LengthSquared > 1e-4f && run > 0.004f && climb > 0.004f)
                {
                    Vector3 beyond = onParent + onward.Normalized() * run;
                    float off = collar.Outside(beyond, out _, out Vector3 outThere, out float thickThere, out _);
                    Drop(mesh, collar, beyond - outThere * off, outThere, thickThere, RoundedSink, out foot.At, out foot.Landing);
                    foot.Rounded = true;
                    foot.StartArc = met + climb;
                    foot.Start = OnCollar(collar, foot.StartArc, cosine, sine, girth, out _, out _);
                    foot.HostOut = outThere;
                    return foot;
                }
                Drop(mesh, collar, onParent, hostOut, hostRadius, sink, out foot.At, out foot.Landing);
                return foot;
            }

            // The corner's two ways: up the limb's own line, and along the parent's surface away from the limb.
            Vector3 away = way - hostOut * Vector3.Dot(way, hostOut);
            if (away.LengthSquared < 1e-3f) away = -upLine - hostOut * Vector3.Dot(-upLine, hostOut);

            if (away.LengthSquared > 1e-6f && collar.Round > 0f)
            {
                away.Normalize();
                // How sharp the corner is goes by how far the surface turns from the limb's to the parent's: not at all where the limb's side comes flush out of the parent's, as
                // the front and back of a limb as thick as its parent do, which is no corner and wants nothing done to it; square under a limb standing straight out; and right
                // round on itself in a crotch, where the two face each other.
                float turn = MathF.Acos(Clamp(Vector3.Dot(way, hostOut), -1f, 1f));
                float reach = MathF.Min(collar.Round * MathF.Tan(MathF.Min(turn * 0.5f, 1.4f)), collar.Flare) * Smooth(0.3f, 1.3f, MathF.PI - turn);
                float rise = MathF.Min(reach, top - met - 1e-3f), spread = MathF.Min(reach, hostRadius * 0.6f);
                if (rise > 0.004f && spread > 0.004f)
                {
                    // Out along the parent, onto its smooth shape, and then down onto its faces.
                    Vector3 beyond = corner + away * spread;
                    float off = collar.Outside(beyond, out _, out Vector3 outThere, out float thickThere, out _);
                    if (Drop(mesh, collar, beyond - outThere * off, outThere, thickThere, RoundedSink, out foot.At, out foot.Landing))
                    {
                        foot.Kind = FootKind.Turned;
                        foot.Rounded = true;
                        foot.StartArc = met + rise;
                        foot.Start = OnCollar(collar, foot.StartArc, cosine, sine, girth, out _, out _);
                        foot.HostOut = outThere;
                        return foot;
                    }
                    // No face where it would have ended, past the end of what it leaves, say: it ends at the corner instead.
                }
            }

            // No corner worth rounding: it ends where its line goes under, on the parent's own faces. They're looked for along the line if it meets the parent squarely enough,
            // and otherwise straight down onto it: a line that only grazes the parent, as the side of a limb as thick as its parent does, would run on along it to wherever it
            // next happened to cross a face, and the collar would lie down the parent as a flap.
            Vector3 down = -upLine;
            float square = -Vector3.Dot(down, hostOut);
            if (square >= 0.5f)
            {
                // From no higher than its first ring, or a face above that would turn its foot inside out.
                float back = MathF.Min((collar.Sag + 0.02f + hostRadius * 0.1f) / square, (top - met) * 0.9f);
                if (collar.Cast(mesh, corner - down * back, down, back + (collar.Sag + 0.02f + hostRadius * 0.1f) / square, out float along, out foot.Landing))
                {
                    foot.Kind = FootKind.Laid;
                    foot.At = corner + down * (along - back) - foot.Landing.Normal * sink;
                    Interlocked.Increment(ref Landed);
                    return foot;
                }
            }
            foot.Kind = Drop(mesh, collar, corner - hostOut * cornerOutside, hostOut, hostRadius, sink, out foot.At, out foot.Landing) ? FootKind.Laid : FootKind.Set;
            return foot;
        }

        /// <summary>
        /// Down onto the faces of what a limb leaves, from over a point of its smooth shape: the first of them met from outside, which is what's seen there. Its faces lie a little
        /// inside that shape, and at a fork or a flared foot not so little either way, so it's looked for from well above to well below. Where there's none it's put on the
        /// nearest face there is (and it says so, by returning false): a foot on its parent somewhere near is only a collar a little out of shape, and one off it is a gap.
        /// </summary>
        private static bool Drop(FloraMesh mesh, Collar collar, Vector3 onShape, Vector3 outward, float thick, float sink, out Vector3 at, out Landing landing)
        {
            float margin = collar.Sag + 0.02f + thick * 0.2f;
            if (collar.Cast(mesh, onShape + outward * margin, -outward, margin * 2f, out float fell, out landing))
            {
                at = onShape + outward * (margin - fell) - landing.Normal * sink;
                Interlocked.Increment(ref Landed);
                return true;
            }
            // No face under it at all: past the end of what it leaves, or where that has no skin of its own. The nearest face there is, then, as that at least is wood.
            if (collar.Nearest(mesh, onShape, out at, out landing))
            {
                at -= landing.Normal * sink;
                Interlocked.Increment(ref Landed);
                return false;
            }
            at = onShape - outward * margin;
            Interlocked.Increment(ref Unlanded);
            return false;
        }

        // Where each line of a limb's collar ends, one to a side of its first ring.
        private static Foot[] Feet(FloraMesh mesh, in SkinStyle style, Collar collar, in Ring top, int sides)
        {
            var feet = new Foot[sides];
            float sink = Clamp(collar.ChildRadius * 0.06f, 0.003f, 0.01f);
            float lowest = -collar.HostRadius * 0.5f;
            Vector3 other = Vector3.Cross(top.Along, top.Side);
            for (int j = 0; j < sides; j++)
            {
                float angle = j * (MathF.Tau / sides);
                float cosine = MathF.Cos(angle), sine = MathF.Sin(angle);
                float girth = top.Radius > 1e-5f ? RadiusAt(top, top.Side * cosine + other * sine, angle, style) / top.Radius : 1f;
                feet[j] = collar.Lands ? Land(mesh, collar, cosine, sine, girth, lowest, sink) : Tuck(mesh, collar, cosine, sine, girth, lowest);
            }
            return feet;
        }

        /// <summary>
        /// Says what carries a limb. The wood where a limb comes out is its parent's as much as its own, and moves as the parent's bark does: its collar and its first ring are
        /// held by whatever joint holds the bark its foot is on, each vertex by where it is along that, which is how the bark itself is held, so they can't part. The limb is
        /// hung from that same joint and bends from the ring it's <paramref name="free"/> from on. The rig, made before there's a skin, hung every limb from its parent's own
        /// joint by where its heart starts, at the heart of the parent; but a limb that leaves another by that one's foot has its foot on their parent's bark, and one that
        /// leaves at a shallow angle is free metres up from where it starts, and held that way each swung a hand's width clear of its own collar as the tree bent.
        /// </summary>
        private static void Hold(FloraMesh mesh, PlantAxis axis, in Ring free, int holder)
        {
            if (axis.FirstLink < 0 || holder < 0) return;
            if (axis.LinkCount > 0)
            {
                // The link the ring it's free from is on is the first of it that bends, and hangs from what holds its foot. Any before that are inside its collar, and carry
                // nothing of its wood: a limb that runs up beside its parent for a way has its first there.
                int at = Rig.LinkAt(axis, free.Share);
                Model.PlantJoint link = mesh.Joints[at];
                if (holder < at) link.Parent = holder;
                if (Vector3.Dot(free.Centre - link.Base, link.Tip - link.Base) > 0f && (link.Tip - free.Centre).Length > 0.05f) link.Base = free.Centre;
                mesh.Joints[at] = link;
                return;
            }

            // What only rides: nothing to do if it rides straight on what carries it, or already with what it grew from.
            if (axis.FirstLink == axis.Riding || axis.FirstLink == holder || (axis.Parent != null && axis.Parent.FirstLink == axis.FirstLink)) return;
            Model.PlantJoint holding = mesh.Joints[holder];
            if (holding.Give == 0f && holding.MostBend <= 0.0011f)
            {
                // Its foot is on something that only rides, so it rides with that, as one piece.
                axis.FirstLink = holder;
                return;
            }
            Model.PlantJoint own = mesh.Joints[axis.FirstLink];
            if (holder < axis.FirstLink) own.Parent = holder;
            if ((own.Tip - free.Centre).Length > 0.02f) own.Base = free.Centre;
            mesh.Joints[axis.FirstLink] = own;
        }

        /// <summary>
        /// The collar a limb leaves its parent through, from its first proper ring (already made, at <paramref name="topStart"/>) back to the parent. Every vertex of that ring has a
        /// line running back down the limb, and each ends in a <see cref="Foot"/> on the parent (<see cref="Land"/>; a twig's is only tucked under the parent's bark,
        /// <see cref="Tuck"/>). The foot has the parent's own normal, colour and moss, so light and bark run from the parent onto the limb, and all of it is held as the bark
        /// its foot is on is held (<see cref="Hold"/>).
        /// </summary>
        private static void AddCollar(FloraMesh mesh, in SkinStyle style, Collar collar, Foot[] feet, in Ring top, int topStart, int sides, int material, bool runs, float aroundTiles, float topV, float up, int holder)
        {
            int levels = collar.Levels;
            float topArc = collar.Top;
            var at = new Vector3[levels * sides];
            var arcs = new float[levels * sides];       // How far along the limb each is, near enough: for how thick the limb is there.
            var runs2 = new float[levels * sides];      // Metres down its line from the first ring, which is how far the bark has run.

            for (int j = 0; j < sides; j++)
            {
                float angle = j * (MathF.Tau / sides);
                float cosine = MathF.Cos(angle), sine = MathF.Sin(angle);
                Foot foot = feet[j];
                float girth = foot.Girth;
                at[j] = foot.At;
                arcs[j] = foot.Arc;
                for (int level = 1; level < levels; level++)
                {
                    if (foot.Rounded)
                    {
                        // Round the corner: from the foot, towards the corner, and away up the limb's own line (a Bézier of the three).
                        float t = levels == 2 ? 0.5f : level == 1 ? 0.34f : 0.68f;
                        float s = 1f - t;
                        at[level * sides + j] = foot.At * (s * s) + foot.Corner * (2f * s * t) + foot.Start * (t * t);
                        arcs[level * sides + j] = MathHelper.Lerp(foot.Arc, foot.StartArc, t);
                    }
                    else
                    {
                        float arc = MathHelper.Lerp(foot.Arc, topArc, levels == 2 ? 0.4f : level == 1 ? 0.24f : 0.58f);
                        at[level * sides + j] = OnCollar(collar, arc, cosine, sine, girth, out _, out _);
                        arcs[level * sides + j] = arc;
                    }
                }

                Vector3 previous = mesh.Vertices[topStart + j].Position;
                float travelled = 0f;
                for (int level = levels - 1; level >= 0; level--)
                {
                    travelled += (at[level * sides + j] - previous).Length;
                    runs2[level * sides + j] = travelled;
                    previous = at[level * sides + j];
                }
            }

            Vector3 Where(int level, int j)
            {
                j = (j % sides + sides) % sides;
                return level >= levels ? mesh.Vertices[topStart + j].Position : at[Math.Max(level, 0) * sides + j];
            }

            bool dead = style.Bark.Kind == BarkKind.Bare;
            int first = mesh.VertexCount;
            for (int level = 0; level < levels; level++)
            {
                float low = 1f - level / (float)levels;
                for (int i = 0; i <= sides; i++)
                {
                    int j = i % sides;
                    Foot foot = feet[j];
                    // How much of the parent is in it: nearly all at its foot, and none by its first ring. A foot turned to lie along the parent is the parent's bark there, and it's
                    // the corner above it that's the crook, where less of the sky looks in; one that meets the parent in a crease is the crook itself.
                    float kinship = foot.Rounded ? (level == 0 ? 1f : 0.35f + 0.5f * low) : 0.85f * low * low;
                    float crook = foot.Rounded ? (level == 0 ? 0.03f : 0.1f * low) : 0.14f * low * low;
                    Vector3 position = at[level * sides + j];
                    Ring section = collar.At(arcs[level * sides + j]);

                    // Its own lie, from its neighbours round it and along its line.
                    Vector3 round = Where(level, j + 1) - Where(level, j - 1);
                    Vector3 along = Where(level + 1, j) - Where(level - 1, j);
                    Vector3 outward = position - section.Centre;
                    outward = outward.LengthSquared > 1e-10f ? outward.Normalized() : section.Side;
                    Vector3 normal = Vector3.Cross(round, along);
                    if (normal.LengthSquared < 1e-12f) normal = outward;
                    normal.Normalize();
                    if (Vector3.Dot(normal, outward) < 0f) normal = -normal;

                    // At its foot it takes the parent's: all of it where it was turned to lie along the parent, and otherwise as far as the two meet openly, as where they meet in
                    // a crease each keeps its own.
                    Vector3 hostNormal = foot.Landing.Found ? foot.Landing.Normal : foot.HostOut;
                    if (collar.Lands && level == 0 && hostNormal.LengthSquared > 0.5f)
                    {
                        float share = foot.Rounded ? 0.92f : 0.85f * Smooth(-0.35f, 0.5f, Vector3.Dot(normal, hostNormal));
                        normal = Vector3.Lerp(normal, hostNormal, share);
                        normal = normal.LengthSquared > 1e-8f ? normal.Normalized() : hostNormal;
                    }
                    Vector3 tangent = round - normal * Vector3.Dot(round, normal);
                    tangent = tangent.LengthSquared > 1e-10f ? tangent.Normalized() : Scaffold.Perpendicular(normal);

                    float given = GivenWay(position, style);
                    Vector3 colour = ColourAt(position, normal, section.Radius, section.Share, top.Light, given, style, out float moss);
                    if (foot.Landing.Found && !dead)
                    {
                        colour = Vector3.Lerp(colour, foot.Landing.Colour, kinship);
                        moss = MathHelper.Lerp(moss, foot.Landing.Shell, kinship);
                        if (foot.Landing.Material == material) given = MathHelper.Lerp(given, foot.Landing.Given, kinship);
                    }
                    if (collar.Lands) colour *= 1f - crook;

                    mesh.Add(new FloraVertex
                    {
                        Position = position,
                        Normal = normal,
                        Tangent = tangent,
                        Colour = colour,
                        UV = new Vector2(aroundTiles * i / sides, topV - runs2[level * sides + j] / up),
                        Material = material,
                        MaterialShare = runs ? given : 0f,
                        Joint = holder,
                        Shell = moss,
                    });
                }
            }

            for (int level = 0; level < levels; level++)
            {
                int lower = first + level * (sides + 1);
                int upper = level + 1 < levels ? first + (level + 1) * (sides + 1) : topStart;
                Stitch(mesh, lower, sides, upper, sides);
            }

            if (FloraJoins.Watch is List<FloraJoin> watch)
            {
                var faces = new List<(int, int)>();
                foreach (Host host in collar.Hosts)
                {
                    for (int s = host.Lo + 1; s <= host.Hi; s++)
                    {
                        if (host.Sleeve.Strips[s].Count > 0) faces.Add(host.Sleeve.Strips[s]);
                    }
                }
                watch.Add(new FloraJoin
                {
                    At = collar.At(collar.Exit).Centre,
                    Out = collar.ExitNormal,
                    Radius = collar.ChildRadius,
                    HostRadius = collar.HostRadius,
                    Lands = collar.Lands,
                    Kinds = Array.ConvertAll(feet, foot => foot.Kind),
                    FirstVertex = first,
                    Vertices = sides,
                    Faces = faces.ToArray(),
                });
            }
        }

        // The sawn or snapped end of a stem: a fan of bare wood, with the growth rings of the bark's own colour going out from the middle.
        private static void AddFace(FloraMesh mesh, in Ring ring, int ringStart, int sides, int joint, in SkinStyle style, float facing)
        {
            int material = FloraMaterials.Bark(BarkKind.Bare);
            Vector3 normal = ring.Along * facing;
            Vector3 heart = style.Bark.Colour * 0.2f + new Vector3(0.14f, 0.1f, 0.06f);
            int hub = mesh.Add(new FloraVertex { Position = ring.Centre + ring.Along * (ring.Radius * 0.12f * facing), Normal = normal, Tangent = ring.Side, Colour = heart * 0.6f, UV = new Vector2(0.5f), Material = material, Joint = joint });
            int first = mesh.VertexCount;
            for (int i = 0; i <= sides; i++)
            {
                FloraVertex rim = mesh.Vertices[ringStart + i];
                float jag = 0.35f * FloraNoise.Wave(i * 1.7f, style.Seed);
                rim.Position += ring.Along * (ring.Radius * jag * 0.3f);
                rim.Normal = normal;
                rim.Tangent = ring.Side;
                rim.Colour = heart;
                rim.UV = new Vector2(i / (float)sides, 0f);
                rim.Material = material;
                rim.MaterialShare = 0f;
                rim.Shell = 0f;
                mesh.Add(rim);
            }
            for (int i = 0; i < sides; i++)
            {
                if (facing > 0f) mesh.Triangle(hub, first + i, first + i + 1);
                else mesh.Triangle(hub, first + i + 1, first + i);
            }
        }

        private static int Sides(float radius, int most)
        {
            int sides = radius < 0.05f ? 3 : radius < 0.09f ? 4 : radius < 0.16f ? 5 : radius < 0.26f ? 6 : radius < 0.42f ? 8 : radius < 0.7f ? 10 : radius < 1.1f ? 12 : 16;
            return Math.Min(sides, most);
        }

        // Joins two rings with different numbers of sides, by walking both around together.
        private static void Stitch(FloraMesh mesh, int a, int sidesA, int b, int sidesB)
        {
            int i = 0, j = 0;
            while (i < sidesA || j < sidesB)
            {
                float nextA = (i + 1f) / sidesA;
                float nextB = (j + 1f) / sidesB;
                if (j >= sidesB || (i < sidesA && nextA <= nextB))
                {
                    mesh.Triangle(a + i, a + i + 1, b + j);
                    i++;
                }
                else
                {
                    mesh.Triangle(a + i, b + j + 1, b + j);
                    j++;
                }
            }
        }

        private static float FlareAt(float y, in SkinStyle style)
        {
            if (style.Flare <= 0f) return 0f;
            float h = MathF.Max(y, 0f) / MathF.Max(style.FlareHeight, 0.01f);
            return MathF.Exp(-h * 2.4f);
        }

        // How far the surface is from the axis, at an angle round it. Flare and ridges are Weber and Penn's: exponential at the foot, and a sine round it, here sharpened so that
        // the ridges run up out of the roots like a buttress rather than a corrugation.
        private static float RadiusAt(in Ring ring, Vector3 outward, float angle, in SkinStyle style)
        {
            float radius = ring.Radius;
            if (style.Bark.Bumps > 0f)
            {
                Vector3 sample = (ring.Centre + outward * ring.Radius) * (1.4f / MathF.Max(ring.Radius * 1.5f + 0.25f, 0.3f));
                radius *= 1f + style.Bark.Bumps * (FloraNoise.Fbm(sample, style.Seed, 2) * 2f - 1f) * 1.6f;
            }
            if (style.Bark.Ribs > 0) radius *= 1f + style.Bark.RibDepth * (MathF.Pow(0.5f + 0.5f * MathF.Cos(style.Bark.Ribs * angle), 1.6f) - 0.5f) * 2f;
            if (style.Bark.NodeSpacing > 0f)
            {
                float from = ring.Travelled / style.Bark.NodeSpacing;
                radius *= 1f + 0.14f * Smooth(0.12f, 0f, MathF.Abs(from - MathF.Round(from)));
            }
            if (style.Trunk)
            {
                float flare = FlareAt(ring.Centre.Y, style);
                float facing = MathF.Atan2(outward.Z, outward.X) - style.LobeFacing;
                float ridge = style.Lobes > 0 ? MathF.Pow(0.5f + 0.5f * MathF.Cos(style.Lobes * facing), 2.5f) : 0f;
                radius *= 1f + style.Flare * flare + ridge * flare * style.Flare * 1.6f;
                radius *= 1f + style.LobeDepth * 0.3f * ridge * (1f - MathF.Min(ring.Share * 2.5f, 1f));
            }
            return radius;
        }

        private static Vector3 Surface(Ring ring, Vector3 other, float angle, SkinStyle style, out Vector3 normal, out Vector3 tangent)
        {
            const float Nudge = 0.08f;
            Vector3 Out(float a) => ring.Side * MathF.Cos(a) + other * MathF.Sin(a);
            Vector3 Point(float a)
            {
                Vector3 outward = Out(a);
                Vector3 off = outward * RadiusAt(ring, outward, a, style);
                if (ring.StretchBy > 0f) off += ring.Stretch * (Vector3.Dot(off, ring.Stretch) * ring.StretchBy);
                return ring.Centre + off;
            }

            Vector3 at = Point(angle);
            tangent = (Point(angle + Nudge) - Point(angle - Nudge)).Normalized();

            // The surface leans in as a stem tapers or a flare dies away.
            Vector3 outwardHere = Out(angle);
            float slope = 0f;
            if (style.Trunk && style.Flare > 0f)
            {
                float flare = FlareAt(ring.Centre.Y, style);
                slope = style.Flare * flare * 2.4f * ring.Radius / MathF.Max(style.FlareHeight, 0.01f);
            }
            Vector3 along = (ring.Along - outwardHere * slope).Normalized();
            normal = Vector3.Cross(tangent, along);
            if (Vector3.Dot(normal, outwardHere) < 0f) normal = -normal;
            normal = normal.LengthSquared > 1e-6f ? normal.Normalized() : outwardHere;
            tangent = (tangent - normal * Vector3.Dot(tangent, normal)).Normalized();
            return at;
        }

        // `moss` is how much of the bark here is under moss, for the shells, which stand only on wood thick enough for a cushion to sit on: a twig is most of a tree's triangles, each bent through its joints for every layer, and moss on it is less than a pixel.
        // `given` is how far a bark that gives way to another has done so here (GivenWay).
        private static Vector3 ColourAt(Vector3 at, Vector3 normal, float radius, float share, float light, float given, in SkinStyle style, out float moss)
        {
            BarkForm bark = style.Bark;
            moss = 0f;
            float young = 1f - MathF.Min(radius / MathF.Max(style.ThickRadius * 0.28f, 0.05f), 1f);
            Vector3 colour = Vector3.Lerp(bark.Colour, bark.YoungColour, young * young);
            if (bark.Upper != null)
            {
                // Its colour goes with its surface, the grey of the plates to the fox-orange of what's above them, so the two change together; thin wood is its own colour.
                colour = Vector3.Lerp(Vector3.Lerp(bark.Colour, bark.UpperColour ?? bark.Colour, given), bark.YoungColour, young * young);
            }
            else if (bark.UpperColour is Vector3 upper)
            {
                float climbed = at.Y / MathF.Max(style.TreeHeight, 1f);
                colour = Vector3.Lerp(colour, upper, Smooth(bark.UpperFrom - 0.12f, bark.UpperFrom + 0.12f, climbed));
            }

            // Bark is never one colour: slow patches of damp and lichen, in a colour that stays this bark's.
            float patch = FloraNoise.Fbm(at * 0.55f, style.Seed + 3u, 3);
            colour *= style.Tint * (0.72f + 0.56f * patch);

            if (bark.Moss > 0f)
            {
                // Moss likes the damp foot of a tree, the upper side of anything leaning, and one side more than the other.
                float wet = MathF.Exp(-MathF.Max(at.Y, 0f) / MathF.Max(style.TreeHeight * 0.16f, 1f));
                float faces = Clamp(Vector3.Dot(normal, style.MossAway) * 0.6f + 0.45f, 0f, 1f);
                float grows = FloraNoise.Fbm(at * 0.9f + new Vector3(9f), style.Seed + 5u, 3);
                moss = Smooth(0.42f, 0.62f, grows + (wet * 0.5f + faces * 0.32f - 0.42f) * bark.Moss * 1.8f) * MathF.Min(bark.Moss * 1.6f, 1f);
                colour = Vector3.Lerp(colour, bark.MossColour * (0.7f + 0.6f * grows), moss);
                moss *= Smooth(0.06f, 0.14f, radius);
            }

            // A little darker where it meets the ground, and in the crook of what grew from it.
            colour *= 0.7f + 0.3f * Smooth(-0.2f, 1.4f, at.Y);
            if (bark.NodeSpacing > 0f)
            {
                float from = at.Y / bark.NodeSpacing;
                colour *= 1f - 0.55f * Smooth(0.05f, 0f, MathF.Abs(from - MathF.Round(from)));
            }
            return colour * (bark.Shaded == 1f ? light : MathHelper.Lerp(1f, light, bark.Shaded));
        }

        // How far a bark that gives way to another (BarkForm.Upper) has done so here, 0 to 1. Up a trunk by how far up it is, the plates reaching higher up one side than the other in
        // tongues, as an old pine's do; a limb is wholly the upper bark, as a pine's are thin bark all over; a root is the foot's.
        private static float GivenWay(Vector3 at, in SkinStyle style)
        {
            if (style.Bark.Upper == null || style.Root) return 0f;
            if (!style.Trunk) return 1f;
            float climbed = at.Y / MathF.Max(style.TreeHeight, 1f);
            float ragged = (FloraNoise.Fbm(new Vector3(at.X * 1.4f, at.Y * 0.22f, at.Z * 1.4f), style.Seed + 11u, 3) - 0.5f) * 0.45f;
            return Smooth(style.Bark.UpperFrom - 0.07f, style.Bark.UpperFrom + 0.09f, climbed + ragged);
        }

        // Kept between two bounds, the lower winning where they cross: Math.Clamp throws there, and a limb on a twig thinner than any limb is meant to be has bounds that do.
        private static float Clamp(float value, float low, float high) => value < low ? low : value > high ? MathF.Max(high, low) : value;

        private static int Clamp(int value, int low, int high) => value < low ? low : value > high ? Math.Max(high, low) : value;

        private static float Smooth(float from, float to, float value)
        {
            float t = Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // Replaces the last ring with a few that close in on a circle.
        private static void Dome(List<Ring> rings)
        {
            Ring last = rings[^1];
            float radius = rings[^2].Radius;
            rings.RemoveAt(rings.Count - 1);
            const int Steps = 4;
            for (int i = 0; i <= Steps; i++)
            {
                float angle = (i + 1f) / (Steps + 1f) * MathF.PI * 0.5f;
                Ring ring = last;
                ring.Centre = last.Centre + last.Along * (MathF.Sin(angle) * radius);
                ring.Radius = MathF.Cos(angle) * radius;
                ring.Travelled = last.Travelled + MathF.Sin(angle) * radius;
                ring.StretchBy = 0f;
                rings.Add(ring);
            }
        }

        // An axis as a curve through its points (Catmull and Rom), by how far along it is in points, 0 to 1, and how thick it is there.
        private static Vector3 Sample(PlantAxis axis, float share, out float radius)
        {
            List<Vector3> points = axis.Points;
            int count = points.Count;
            float wanted = Clamp(share, 0f, 1f) * (count - 1);
            int i = Math.Min((int)wanted, count - 2);
            float t = wanted - i;
            radius = MathHelper.Lerp(axis.Radii[i], axis.Radii[i + 1], t);
            Vector3 p0 = points[Math.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Math.Min(i + 2, count - 1)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        // How far along an axis, as Sample counts it, so many metres from its start are.
        private static float ShareAtArc(PlantAxis axis, float arc)
        {
            List<Vector3> points = axis.Points;
            float travelled = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                float step = (points[i] - points[i - 1]).Length;
                if (travelled + step >= arc) return (i - 1 + (step > 1e-6f ? (arc - travelled) / step : 0f)) / (points.Count - 1);
                travelled += step;
            }
            return 1f;
        }

        // Parallel transport: a side turned by what the axis turned by, and no more.
        private static Vector3 Transport(Vector3 side, Vector3 from, Vector3 to)
        {
            Vector3 axisOfTurn = Vector3.Cross(from, to);
            float sine = axisOfTurn.Length;
            if (sine > 1e-5f)
            {
                axisOfTurn /= sine;
                float angle = MathF.Atan2(sine, Vector3.Dot(from, to));
                side = Vector3.Transform(side, Quaternion.FromAxisAngle(axisOfTurn, angle));
            }
            side -= to * Vector3.Dot(side, to);
            return side.LengthSquared > 1e-8f ? side.Normalized() : Scaffold.Perpendicular(to);
        }

        /// <summary>
        /// The rings of a stem, from <paramref name="from"/> of the way along it (as Sample counts it), which is <paramref name="before"/> metres from its start: a limb's begin
        /// where it's clear of what it grew from.
        /// </summary>
        private static List<Ring> Rings(PlantAxis axis, float footHeight, float from, float before)
        {
            var rings = new List<Ring>();
            List<Vector3> points = axis.Points;
            int count = points.Count;
            float length = axis.Length;
            if (length < 1e-3f || from >= 1f) return rings;

            // Walks along the curve in steps set by how thick it is there, and closer together where it's bending.
            float nodes = axis.NodeRings;
            var centres = new List<(Vector3 at, float radius, float travelled)>();
            float share = from;
            Vector3 point = Sample(axis, from, out float r0);
            centres.Add((point, r0, before));
            float travelled = before;
            float per = 1f / MathF.Max(count - 1, 1);
            Vector3 ahead = Sample(axis, MathF.Min(from + per * 0.25f, 1f), out _) - point;
            if (ahead.LengthSquared < 1e-10f) ahead = points[^1] - points[0];
            Vector3 lastDirection = ahead.LengthSquared > 1e-10f ? ahead.Normalized() : Vector3.UnitY;
            while (share < 1f)
            {
                Vector3 here = Sample(axis, share, out float radius);
                float wanted = Clamp(radius * 2f * RingPitch, 0.22f, LongestRing);
                if (nodes > 0f) wanted = MathF.Min(wanted, nodes * 0.5f);
                if (here.Y < footHeight * 1.6f) wanted = MathF.Min(wanted, MathF.Max(footHeight * 0.28f, 0.2f));
                float step = wanted / MathF.Max(length, 1e-3f);
                float next = MathF.Min(share + step, 1f);
                Vector3 there = Sample(axis, next, out float radiusThere);
                Vector3 direction = (there - here).LengthSquared > 1e-8f ? (there - here).Normalized() : lastDirection;
                // Closer together where it's turning, and on thick wood closer again while it still turns too far in one step: a thick stem's rings are metres apart, and a
                // fork's turn taken in one of them is a pipe bent over a knee. A twig's are a hand apart already.
                int halvings = radius >= 0.08f ? 3 : 1;
                for (int halved = 0; halved < halvings && Vector3.Dot(direction, lastDirection) < 0.94f && step > per * (halvings > 1 ? 0.2f : 0.5f); halved++)
                {
                    step *= 0.5f;
                    next = MathF.Min(share + step, 1f);
                    there = Sample(axis, next, out radiusThere);
                    direction = (there - here).LengthSquared > 1e-8f ? (there - here).Normalized() : lastDirection;
                }
                travelled += (there - here).Length;
                share = next;
                if (share >= 0.999f || 1f - share < step * 0.35f)
                {
                    there = Sample(axis, 1f, out radiusThere);
                    share = 1f;
                }
                centres.Add((there, radiusThere, travelled));
                lastDirection = direction;
            }

            float total = MathF.Max(travelled, 1e-3f);
            Vector3 along = centres[1].at - centres[0].at;
            along = along.LengthSquared > 1e-10f ? along.Normalized() : lastDirection;
            Vector3 side = Scaffold.Perpendicular(along);
            for (int i = 0; i < centres.Count; i++)
            {
                // A ring lies across the way the stem's going there: half way between the way in and the way out, so that a turn is shared by the stretches either side of it.
                Vector3 into = i > 0 ? centres[i].at - centres[i - 1].at : Vector3.Zero;
                Vector3 onward = i + 1 < centres.Count ? centres[i + 1].at - centres[i].at : Vector3.Zero;
                into = into.LengthSquared > 1e-10f ? into.Normalized() : Vector3.Zero;
                onward = onward.LengthSquared > 1e-10f ? onward.Normalized() : into;
                if (into == Vector3.Zero) into = onward;
                Vector3 across = into + onward;
                Vector3 stretch = Vector3.Zero;
                float stretchBy = 0f;
                if (across.LengthSquared > 1e-8f)
                {
                    Vector3 nextAlong = across.Normalized();
                    side = Transport(side, along, nextAlong);
                    along = nextAlong;
                    // And is longer in the plane of the turn, by what cutting a round stem at that slant makes it.
                    Vector3 lean = onward - along * Vector3.Dot(onward, along);
                    float cosine = Vector3.Dot(onward, along);
                    if (lean.LengthSquared > 1e-6f && cosine > 0.2f)
                    {
                        stretch = lean.Normalized();
                        stretchBy = MathF.Min(1f / cosine - 1f, 0.6f);
                    }
                }
                float share2 = centres[i].travelled / total;
                float light = axis.Lights.Count > 0 ? axis.Lights[Math.Min((int)(share2 * (axis.Lights.Count - 1)), axis.Lights.Count - 1)] : 1f;
                rings.Add(new Ring { Centre = centres[i].at, Along = along, Side = side, Radius = centres[i].radius, Travelled = centres[i].travelled, Share = share2, Light = light, Stretch = stretch, StretchBy = stretchBy });
            }
            return rings;
        }
    }
}
