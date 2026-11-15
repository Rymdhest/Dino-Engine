using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// A form that lives under the water, and the most it can stand above its foot at a scale of one, which the sea floor keeps under the surface (SEA.md, WorldSeaFloor): what's
    /// rigid under the troughs of the waves and what sways under the still water. A bound and not a measure, worked out from the form's own numbers as its spines are laid, so it's
    /// the same on every thread with nothing grown; Dino FloraLab's check grows every one and fails any that stands taller than it says.
    /// </summary>
    public interface IUnderWater
    {
        float Tallest { get; }
        bool Rigid { get; }
    }

    /// <summary>
    /// How what lives under water bends (SEA.md). Nothing flutters, as water damps a shiver long before it could show; what stands in the sea gives to the waves passing over it, which
    /// the vegetation spring is driven by under the water (Wind_Vegetation.frag), and what floats gives to nothing but whoever wades through it.
    /// </summary>
    public static class SeaJoints
    {
        /// <summary>A link of a seagrass blade or a frond of weed: buoyant, standing up in still water and streaming with every surge.</summary>
        public static PlantJoint Ribbon => PlantJoint.Blade with { Give = 0.85f, MostBend = 1.05f, Touch = 0.9f, Flutter = 0f };

        /// <summary>A kelp's stalk: stiff enough to hold its fronds up off the bed.</summary>
        public static PlantJoint Stipe => PlantJoint.Stem with { Give = 0.32f, MostBend = 0.42f, Touch = 0.5f, Flutter = 0f };

        /// <summary>A giant kelp's stalk, a rope up to the surface held up by its floats, swinging with the swell.</summary>
        public static PlantJoint Rope => PlantJoint.Stem with { Give = 0.5f, MostBend = 0.5f, Touch = 0.4f, Flutter = 0f };

        /// <summary>A kelp's frond, a strap that streams wherever the water goes.</summary>
        public static PlantJoint Frond => PlantJoint.Frond with { Give = 0.95f, MostBend = 1.1f, Touch = 0.8f, Flutter = 0f };

        /// <summary>A soft coral's finger: flesh on a skeleton of spicules, which leans a little and comes back.</summary>
        public static PlantJoint Finger => PlantJoint.Stem with { Give = 0.22f, MostBend = 0.28f, Touch = 0.3f, Flutter = 0f };

        /// <summary>A sea fan, which grows broadside to the waves and rocks to and fro with them.</summary>
        public static PlantJoint Fan => PlantJoint.Stem with { Give = 0.5f, MostBend = 0.42f, Touch = 0.4f, Flutter = 0f };

        /// <summary>A pad floating on still water: the wind slides over it, and a body pushes it aside or under.</summary>
        public static PlantJoint Pad => PlantJoint.Leaf with { Give = 0f, MostBend = 0.35f, Touch = 0.8f, Flutter = 0f };
    }

    /// <summary>How a limb ends.</summary>
    public enum LimbTip
    {
        Open,       // Left as it is: it ends inside something else.
        Point,      // Drawn to a point: a coral's growing tip, a few pixels across.
        Round,      // Domed over: a soft coral's finger, a bud.
    }

    internal static class SeaShape
    {
        /// <summary>The highest a spine laid by <see cref="Strip.Arc"/> with these numbers rises over its foot: the same steps, so it's exact, and the most any shorter or more bent one can.</summary>
        public static float ArcHeight(float length, float lean, float arch, int segments, float curl)
        {
            float step = length / segments, y = 0f, most = 0f;
            for (int i = 1; i <= segments; i++)
            {
                float t = (i - 0.5f) / segments;
                y += MathF.Cos(lean + arch * MathF.Pow(t, curl)) * step;
                most = MathF.Max(most, y);
            }
            return most;
        }

        public static float Smooth(float from, float to, float value)
        {
            // An empty range is a step: divided by nothing, the tip of a blade with nothing grown on it came out black.
            if (to <= from) return value >= to ? 1f : 0f;
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// A round limb along a spine, with as many sides as it's worth and its colour going from foot to tip, and its end left open, drawn to a point or domed over (<paramref name="tip"/>):
        /// a coral's branch, a finger, a kelp's stalk. Its rings are turned by a frame carried from one to the next, so it never twists, and a taper leans its normals forward, as a cone's.
        /// </summary>
        public static void Limb(FloraMesh mesh, IReadOnlyList<Vector3> spine, Func<float, float> radius, int sides, Func<float, Vector3> colour, LimbTip tip, int material, int joint)
        {
            int count = spine.Count;
            if (count < 2) return;
            var along = new float[count];
            for (int i = 1; i < count; i++) along[i] = along[i - 1] + (spine[i] - spine[i - 1]).Length;
            float length = MathF.Max(along[count - 1], 1e-4f);

            Vector3 side = Scaffold.Perpendicular((spine[1] - spine[0]).Normalized());
            Vector3 other = Vector3.Cross(Vector3.UnitY, side);
            int first = mesh.VertexCount;
            Vector3 forward = Vector3.UnitY;
            for (int i = 0; i < count; i++)
            {
                forward = i + 1 < count ? spine[i + 1] - spine[i] : spine[i] - spine[i - 1];
                forward = forward.LengthSquared > 1e-9f ? forward.Normalized() : Vector3.UnitY;
                side = side - forward * Vector3.Dot(side, forward);
                side = side.LengthSquared > 1e-8f ? side.Normalized() : Scaffold.Perpendicular(forward);
                other = Vector3.Cross(forward, side);
                float t = along[i] / length;
                float r = radius(t);
                float slope = (radius(MathF.Min(t + 0.05f, 1f)) - radius(MathF.Max(t - 0.05f, 0f))) / MathF.Max(0.1f * length, 1e-4f);
                Vector3 tint = colour(t);
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * (MathF.Tau / sides);
                    Vector3 outward = side * MathF.Cos(angle) + other * MathF.Sin(angle);
                    mesh.Add(new FloraVertex
                    {
                        Position = spine[i] + outward * r,
                        Normal = (outward - forward * slope).Normalized(),
                        Tangent = -side * MathF.Sin(angle) + other * MathF.Cos(angle),
                        Colour = tint,
                        UV = new Vector2((float)s / sides, along[i]),
                        Material = material,
                        Joint = joint,
                    });
                }
            }
            int row = sides + 1;
            for (int i = 0; i + 1 < count; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = first + i * row + s;
                    mesh.Quad(a, a + 1, a + row + 1, a + row);
                }
            }
            if (tip == LimbTip.Open) return;

            float end = radius(1f);
            Vector3 point = spine[count - 1];
            Vector3 ending = colour(1f);
            int last = first + (count - 1) * row;
            if (tip == LimbTip.Round)
            {
                // Domed over in two more rings, as a fingertip is: a single point reads as a crystal's.
                for (int k = 0; k < 2; k++)
                {
                    float lift = k == 0 ? 0.42f : 0.78f;
                    float shrink = k == 0 ? 0.9f : 0.6f;
                    int ring = mesh.VertexCount;
                    for (int s = 0; s <= sides; s++)
                    {
                        float angle = s * (MathF.Tau / sides);
                        Vector3 outward = side * MathF.Cos(angle) + other * MathF.Sin(angle);
                        mesh.Add(new FloraVertex
                        {
                            Position = point + forward * (end * lift) + outward * (end * shrink),
                            Normal = (outward * shrink + forward * lift).Normalized(),
                            Tangent = -side * MathF.Sin(angle) + other * MathF.Cos(angle),
                            Colour = ending,
                            UV = new Vector2((float)s / sides, length + end * lift),
                            Material = material,
                            Joint = joint,
                        });
                    }
                    for (int s = 0; s < sides; s++) mesh.Quad(last + s, last + s + 1, ring + s + 1, ring + s);
                    last = ring;
                }
            }
            int top = mesh.Add(new FloraVertex
            {
                Position = point + forward * (end * (tip == LimbTip.Round ? 1f : 0.85f)),
                Normal = forward,
                Tangent = side,
                Colour = ending,
                UV = new Vector2(0.5f, length + end),
                Material = material,
                Joint = joint,
            });
            for (int s = 0; s < sides; s++) mesh.Triangle(last + s, last + s + 1, top);
        }

        /// <summary>
        /// Turns a section about the y axis, from the first point to the last: a sponge, the stalk of a table, a mound. Each ring is (radius, height, colour), and normals come from
        /// the section's own slope, so a rim reads as a rim. <paramref name="wobble"/> pushes a ring in and out round its circle, by angle and by which ring it is.
        /// </summary>
        public static void Lathe(FloraMesh mesh, Vector3 foot, IReadOnlyList<(float r, float y, Vector3 colour)> section, int sides, int material, Func<float, int, float>? wobble = null)
        {
            int first = mesh.VertexCount;
            for (int row = 0; row < section.Count; row++)
            {
                var (r, y, colour) = section[row];
                int before = Math.Max(row - 1, 0), after = Math.Min(row + 1, section.Count - 1);
                float rise = section[after].y - section[before].y;
                float wide = section[after].r - section[before].r;
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * (MathF.Tau / sides);
                    Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    float push = wobble?.Invoke(angle, row) ?? 1f;
                    Vector3 normal = new Vector3(outward.X * rise, -wide, outward.Z * rise);
                    normal = normal.LengthSquared > 1e-10f ? normal.Normalized() : Vector3.UnitY;
                    mesh.Add(new FloraVertex
                    {
                        Position = foot + outward * (r * push) + Vector3.UnitY * y,
                        Normal = normal,
                        Tangent = new Vector3(-outward.Z, 0f, outward.X),
                        Colour = colour,
                        UV = new Vector2((float)s / sides, y),
                        Material = material,
                        Joint = -1,
                    });
                }
            }
            int stride = sides + 1;
            for (int row = 0; row + 1 < section.Count; row++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = first + row * stride + s;
                    // Going up a section's rows, a quad faces out the way its normals do whichever way the section turns.
                    mesh.Quad(a, a + stride, a + stride + 1, a + 1);
                }
            }
        }

        /// <summary>A unit direction turned off <paramref name="heading"/> by <paramref name="angle"/>, towards <paramref name="roll"/> radians round it.</summary>
        public static Vector3 Diverge(Vector3 heading, float angle, float roll)
        {
            Vector3 side = Scaffold.Perpendicular(heading);
            Vector3 other = Vector3.Cross(heading, side);
            Vector3 away = side * MathF.Cos(roll) + other * MathF.Sin(roll);
            return (heading * MathF.Cos(angle) + away * MathF.Sin(angle)).Normalized();
        }
    }

    /// <summary>
    /// Ribbons from runners under the bed: seagrass, wrack, sea lettuce, pondweed. Seagrass grows in rows from its rhizomes, a shoot every few hand's widths with a few blades from each, so
    /// a clump is shoots strewn along a runner or two and never a ball; the outer blades of a shoot are its oldest, longest and leaning most, carrying what's grown on their ends. They're
    /// buoyant, standing up in still water, and every wave passing over leans them all one way and back (SEA.md).
    /// </summary>
    public sealed class RibbonForm : PlantForm, IUnderWater
    {
        // A shoot's blades lean and arch more and run longer as they age (Grow), so the tallest is found over its ages, at the longest length, least arch and most upright lean of each.
        public float Tallest
        {
            get
            {
                float most = 0f;
                for (int i = 0; i <= 10; i++)
                {
                    float age = i / 10f;
                    float length = BladeLength.Max * MathHelper.Lerp(0.72f, 1.1f, age);
                    float arch = Arch.Min * MathHelper.Lerp(0.8f, 1.15f, age);
                    float lean = MathHelper.Lerp(Lean.Min, Lean.Max, age);
                    for (int j = 0; j <= 8; j++)
                        most = MathF.Max(most, SeaShape.ArcHeight(length, lean + (j / 8f - 0.5f) * 0.1f, arch, Segments, Curl));
                }
                return most;
            }
        }

        public bool Rigid => false;

        public IntRange Shoots = new IntRange(6, 10);
        public IntRange BladesPerShoot = new IntRange(3, 5);
        public FloatRange BladeLength = new FloatRange(0.5f, 0.9f);   // Metres.
        public float BladeWidth = 0.028f;                             // Half its width at its foot, metres.
        public float Taper = 0.25f;                                   // How much narrower it is near its tip, which is rounded off.
        public FloatRange Lean = new FloatRange(0.06f, 0.3f);         // Radians from upright at its foot, from the youngest blade of a shoot to the oldest.
        public FloatRange Arch = new FloatRange(0.35f, 1.2f);         // Radians more by its tip.
        public float Curl = 1.6f;
        public float Spread = 0.6f;                                   // Metres the runners reach from the middle.
        public int Runners = 2;
        public int Segments = 4;
        public Palette Colours = new Palette();
        public Vector3 Tips = new Vector3(0.11f, 0.1f, 0.05f);        // What grows on old blades' ends: a film of algae, a crust, or the brown of dying back.
        public float TipShare = 0.3f;                                 // How far down the oldest blade it reaches.
        public float Dead = 0.08f;                                    // Share of blades dead and brown.
        public Vector3 DeadColour = new Vector3(0.14f, 0.11f, 0.05f);
        public PlantJoint Joint = SeaJoints.Ribbon;
        public int Links = 2;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("ribbons");
            int shoots = genes.Count("shoots", Shoots.Min, Shoots.Max);
            float drift = genes.Range("drift", -1f, 1f) * Colours.Drift;
            int material = FloraMaterials.Bark(BarkKind.Stem);

            // Each runner wanders across the clump, and the shoots come up along it.
            int runners = Math.Max(Runners, 1);
            var starts = new Vector3[runners];
            var headings = new float[runners];
            for (int r = 0; r < runners; r++)
            {
                float angle = random.Float() * MathF.Tau;
                starts[r] = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (Spread * random.Range(0.55f, 0.95f));
                headings[r] = angle + MathF.PI + random.Range(-0.6f, 0.6f);
            }

            for (int s = 0; s < shoots; s++)
            {
                int runner = s % runners;
                float along = (s / runners + random.Range(0.1f, 0.9f)) / MathF.Max(MathF.Ceiling((float)shoots / runners), 1f);
                float bend = headings[runner] + MathF.Sin(along * 4.1f + runner) * 0.35f;
                Vector3 foot = starts[runner] + new Vector3(MathF.Cos(bend), 0f, MathF.Sin(bend)) * (along * Spread * 1.8f);
                foot += new Vector3(random.Range(-0.06f, 0.06f), -0.03f, random.Range(-0.06f, 0.06f));
                if (foot.Xz.Length > Spread * 1.05f) foot = new Vector3(foot.X, foot.Y, foot.Z) * (Spread * 1.05f / foot.Xz.Length);

                int blades = random.Int(BladesPerShoot);
                float turn = random.Float() * MathF.Tau;
                for (int b = 0; b < blades; b++)
                {
                    float age = blades > 1 ? (float)b / (blades - 1) : 0.5f;
                    float angle = turn + b * 2.39996f;
                    Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                    float length = random.Range(BladeLength) * MathHelper.Lerp(0.72f, 1.1f, age);
                    float lean = MathHelper.Lerp(Lean.Min, Lean.Max, age) + random.Range(-0.05f, 0.05f);
                    float arch = random.Range(Arch) * MathHelper.Lerp(0.8f, 1.15f, age);
                    List<Vector3> spine = Strip.Arc(foot + outward * 0.01f, outward, length, lean, arch, Segments, Curl);

                    bool dead = random.Float() < Dead;
                    float tone = random.Range(0.35f, 0.85f);
                    float tipped = MathF.Max(TipShare * age * random.Range(0.4f, 1.3f), 0.02f);
                    float half = BladeWidth * random.Range(0.8f, 1.15f);
                    var style = new StripStyle
                    {
                        Material = material, Across = 1, Fold = 0f, Centre = foot - Vector3.UnitY * 0.5f, Shell = 0.3f, Joint = Joint, Links = Links, ParentJoint = -1,
                    };
                    Vector3 side = new Vector3(-outward.Z, 0f, outward.X);
                    Strip.Add(mesh, spine, t => half * (1f - Taper * t) * (1f - 0.75f * SeaShape.Smooth(0.86f, 1f, t)), side, style, (t, x) =>
                    {
                        if (dead) return DeadColour * (0.8f + 0.35f * t) * (1f + drift);
                        // Darker where it comes out of the bed and in the shade of the rest, fresher along it, and what's grown on its end over the last of it.
                        Vector3 colour = Colours.At(MathHelper.Lerp(tone * 0.45f, tone, MathF.Sqrt(t)));
                        colour = Vector3.Lerp(colour, Tips, SeaShape.Smooth(1f - tipped, 1f - tipped * 0.4f, t) * 0.85f);
                        return colour * (1f + drift);
                    });
                }
            }
        }
    }

    /// <summary>
    /// A kelp: a holdfast gripping the rock, a stalk standing up from it, and leathery fronds from its top. Oarweed's frond is split into straps that fan out and stream; sugar kelp's is one
    /// long strap crinkled down its middle; a giant kelp is a rope up to the surface on its floats, with fronds all up its top and lying out on the water, which is what a kelp forest is
    /// from above. Every one gives to the surge, the fronds most (SEA.md).
    /// </summary>
    public sealed class KelpForm : PlantForm, IUnderWater
    {
        // The stalk's foot, its arc at the least lean, and a frond's arc on top of that at the most upright, with its crinkles: a giant kelp's crown lies on the surface by design.
        public float Tallest => 0.04f + SeaShape.ArcHeight(StipeLength.Max, StipeLean.Min, StipeLean.Min * 0.6f, Floats ? 6 : 3, 1.3f)
            + (Floats ? FrondLength.Max * MathF.Cos(1.55f) + 0.01f : SeaShape.ArcHeight(FrondLength.Max, FrondLean.Min, FrondArch.Min, Segments, 1.35f) + Ruffle);

        public bool Rigid => false;

        public IntRange Stipes = new IntRange(1, 3);
        public FloatRange StipeLength = new FloatRange(0.4f, 1.1f);   // Metres. For a giant kelp, to the surface of water as deep as it's made for: the world scales it to the depth.
        public float StipeRadius = 0.03f;
        public FloatRange StipeLean = new FloatRange(0.05f, 0.35f);
        public IntRange Fronds = new IntRange(5, 9);                  // Straps from the top of each stalk.
        public FloatRange FrondLength = new FloatRange(0.7f, 1.4f);
        public float FrondWidth = 0.06f;                              // Half a strap's width.
        public float Splay = 0.9f;                                    // Radians a frond's straps fan over to each side.
        public FloatRange FrondLean = new FloatRange(0.6f, 1.2f);     // Radians from upright as they leave the stalk.
        public FloatRange FrondArch = new FloatRange(0.3f, 0.9f);
        public float Ruffle;                                          // Metres sugar kelp's crinkles stand out of its middle.
        public bool Floats;                                           // A giant kelp: a rope to the surface, fronds up its top, and its crown lying out on the water.
        public IntRange SideFronds = new IntRange(6, 10);             // For a giant kelp: fronds up the top of its rope.
        public Vector3 Holdfast = new Vector3(0.06f, 0.045f, 0.03f);
        public Vector3 Stalk = new Vector3(0.08f, 0.06f, 0.03f);
        public Palette Colours = new Palette { Shade = new Vector3(0.07f, 0.05f, 0.02f), Body = new Vector3(0.15f, 0.11f, 0.04f), Sun = new Vector3(0.26f, 0.19f, 0.06f), Drift = 0.1f };
        public int Segments = 4;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("kelp");
            int stipes = genes.Count("stipes", Stipes.Min, Stipes.Max);
            float drift = genes.Range("drift", -1f, 1f) * Colours.Drift;
            int material = FloraMaterials.Flesh;

            // The holdfast: a knot of claws on the rock, under all of it.
            var knot = new List<(float r, float y, Vector3 colour)>
            {
                (StipeRadius * 3.2f, -0.03f, Holdfast * 0.8f),
                (StipeRadius * 2.6f, 0.03f, Holdfast),
                (StipeRadius * 1.2f, 0.08f, Holdfast * 1.1f),
            };
            SeaShape.Lathe(mesh, Vector3.Zero, knot, 6, material, (angle, row) => row == 0 ? 1f + 0.35f * MathF.Sin(angle * 3f + drift * 9f) : 1f);

            for (int s = 0; s < stipes; s++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 foot = outward * (StipeRadius * random.Range(0f, 1.5f)) + Vector3.UnitY * 0.04f;
                float height = random.Range(StipeLength) * (s == 0 ? 1f : random.Range(0.6f, 0.95f));
                float lean = random.Range(StipeLean);
                List<Vector3> stalk = Strip.Arc(foot, outward, height, lean, lean * 0.6f, Floats ? 6 : 3, 1.3f);

                PlantJoint kind = Floats ? SeaJoints.Rope : SeaJoints.Stipe;
                int links = Floats ? 3 : 1;
                var points = new Vector3[links + 1];
                for (int i = 0; i <= links; i++) points[i] = stalk[(int)MathF.Round((float)i / links * (stalk.Count - 1))];
                PlantJoint[] chain = kind.Chain(points);
                int parent = -1;
                int firstJoint = mesh.Joints.Count;
                for (int i = 0; i < chain.Length; i++)
                {
                    chain[i].Parent = parent;
                    parent = mesh.AddJoint(chain[i]);
                }
                // Each ring of the stalk hangs from the link it's on.
                for (int i = 0; i + 1 < stalk.Count; i++)
                {
                    int link = firstJoint + Math.Min((int)((float)i / (stalk.Count - 1) * links), links - 1);
                    var piece = new List<Vector3> { stalk[i], stalk[i + 1] };
                    float from = (float)i / (stalk.Count - 1), to = (float)(i + 1) / (stalk.Count - 1);
                    SeaShape.Limb(mesh, piece, t => StipeRadius * MathHelper.Lerp(1f - 0.4f * from, 1f - 0.4f * to, t), 4, t => Stalk * (0.85f + 0.3f * MathHelper.Lerp(from, to, t)), LimbTip.Open, material, link);
                }
                Vector3 top = stalk[^1];
                Vector3 heading = (stalk[^1] - stalk[^2]).Normalized();

                if (Floats)
                {
                    int sides = random.Int(SideFronds);
                    for (int f = 0; f < sides; f++)
                    {
                        // Fronds alternate up the top of the rope, each on its little float, pointing up and out and streaming.
                        float at = 0.55f + 0.43f * (f + random.Range(0f, 0.7f)) / sides;
                        int index = Math.Clamp((int)(at * (stalk.Count - 1)), 0, stalk.Count - 2);
                        Vector3 from = Vector3.Lerp(stalk[index], stalk[index + 1], at * (stalk.Count - 1) - index);
                        int link = firstJoint + Math.Min((int)(at * links), links - 1);
                        float turn = angle + f * 2.39996f;
                        Vector3 away = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
                        float length = random.Range(FrondLength) * 0.55f;
                        float tilt = random.Range(0.45f, 0.85f);
                        float arch = random.Range(0.2f, 0.6f);
                        // The higher it leaves the rope the further it leans out, never rising past where the rope ends: the rope ends at the surface, and a frond standing over it
                        // stood up out of the water.
                        float room = (1f - at) * height;
                        while (tilt < 1.55f && SeaShape.ArcHeight(length, tilt, arch, Segments, 1.35f) > room) tilt += 0.05f;
                        AddFrond(mesh, random, from, away, length, tilt, arch, FrondWidth * 0.8f, link, drift, material);
                    }
                    // The crown lies out flat on the surface, radiating from the top of the rope.
                    int crown = random.Int(Fronds);
                    for (int f = 0; f < crown; f++)
                    {
                        float turn = random.Float() * MathF.Tau;
                        Vector3 away = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
                        AddFrond(mesh, random, top, away, random.Range(FrondLength), random.Range(1.55f, 1.62f), random.Range(0f, 0.06f), FrondWidth, parent, drift, material);
                    }
                    continue;
                }

                int fronds = random.Int(Fronds);
                float spin = random.Float() * MathF.Tau;
                for (int f = 0; f < fronds; f++)
                {
                    // Oarweed's straps fan from the top of the stalk across one side, as the blade they split from did; sugar kelp's one or two go every which way.
                    float spread = fronds > 1 ? (f / (float)(fronds - 1) - 0.5f) * 2f * Splay : 0f;
                    float turn = spin + spread + random.Range(-0.15f, 0.15f);
                    Vector3 away = (new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)) + heading * 0.2f).Normalized();
                    away = new Vector3(away.X, 0f, away.Z).Normalized();
                    AddFrond(mesh, random, top, away, random.Range(FrondLength), random.Range(FrondLean), random.Range(FrondArch), FrondWidth, parent, drift, material);
                }
            }
        }

        private void AddFrond(FloraMesh mesh, WorldRandom random, Vector3 from, Vector3 away, float length, float lean, float arch, float width, int parent, float drift, int material)
        {
            List<Vector3> spine = Strip.Arc(from, away, length, lean, arch, Segments, 1.35f);
            float tone = random.Range(0.35f, 0.85f);
            float half = width * random.Range(0.8f, 1.2f);
            int across = Ruffle > 0f ? 4 : 1;
            var style = new StripStyle
            {
                Material = material, Across = across, Fold = Ruffle > 0f ? 0.04f : 0f, Centre = from - Vector3.UnitY * 0.6f, Shell = 0.25f,
                Joint = SeaJoints.Frond, Links = 2, ParentJoint = parent,
            };
            int first = mesh.VertexCount;
            Vector3 side = new Vector3(-away.Z, 0f, away.X);
            // A strap widens out of the stalk and rounds off at its end, darker and thicker at its foot and golden where it's thin.
            Strip.Add(mesh, spine, t => half * MathF.Min(1f, 0.35f + t * 5f) * (1f - 0.7f * SeaShape.Smooth(0.82f, 1f, t)), side, style, (t, x) =>
                Colours.At(MathHelper.Lerp(tone * 0.35f, tone, MathF.Pow(t, 0.7f))) * (1f + drift) * (1f - 0.12f * x * x));
            if (Ruffle <= 0f) return;

            // Sugar kelp crinkles down its middle, where it grows faster than its edges can keep up with: its inner columns go in and out along it, and their normals with them.
            int row = across + 1;
            int count = spine.Count;
            float phase = random.Float() * MathF.Tau;
            for (int i = 1; i < count; i++)
            {
                float t = (float)i / (count - 1);
                for (int c = 1; c < across; c++)
                {
                    int at = first + i * row + c;
                    FloraVertex vertex = mesh.Vertices[at];
                    float x = (float)c / across * 2f - 1f;
                    float wave = MathF.Sin(t * 13f + phase + c * 1.7f);
                    float lift = Ruffle * wave * (1f - x * x) * MathF.Min(1f, t * 3f);
                    vertex.Position += vertex.Normal * lift;
                    vertex.Normal = (vertex.Normal + vertex.Tangent * (MathF.Cos(t * 13f + phase + c * 1.7f) * 0.45f)).Normalized();
                    mesh.Vertices[at] = vertex;
                }
            }
        }
    }

    /// <summary>How a coral grows, which is all its shape is.</summary>
    public enum CoralGrowth
    {
        Branching,  // Antlers that fork as they go: staghorn.
        Table,      // A plate on a stalk, flat as a table: what reef fish shelter under.
        Massive,    // A dome, round and grooved as a brain or lumpy as a boulder.
        Fingers,    // A soft coral: fat fingers from a mound, that give a little to the water.
        Tubes,      // Sponges: a barrel, or a stand of tubes, open at the top.
        Fan,        // A sea fan: a lattice on a stalk, broadside to the waves.
    }

    /// <summary>
    /// A coral, a sponge or a sea fan. A colony is one colour of the few its kind comes in, drawn by its seed, and the growing tips of a branch or the rim of a plate are paler, as the
    /// polyps there are young. Everything here is solid and small, so it gets real geometry, coloured by its vertices; only a fan, whose lattice is finer than anything could be built,
    /// is a card. What's alive and soft gives to the water (<see cref="SeaJoints"/>), and the rest stands still.
    /// </summary>
    public sealed class CoralForm : PlantForm, IUnderWater
    {
        public float Tallest => Growth switch
        {
            CoralGrowth.Branching => BranchesClimb(),
            CoralGrowth.Table => Height.Max + 0.1f,
            CoralGrowth.Massive => Size.Max * 0.5f * Squat.Max * (1f + Lumps),
            CoralGrowth.Fingers => Height.Max + 0.1f,
            CoralGrowth.Fan => Height.Max * 0.25f + Size.Max,
            _ => Height.Max,
        };

        public bool Rigid => true;

        // A staghorn's first branches run at most 0.8 of its height and each generation at most 0.85 of the one before, from where that one ends: the most they climb, all straight up,
        // and a tip. Its height alone, as it was once taken to be, let it stand half a metre out of the troughs.
        private float BranchesClimb()
        {
            float most = 0f, length = 0.8f * Height.Max;
            for (int generation = 0; generation <= Generations; generation++)
            {
                most += length;
                length *= 0.85f;
            }
            return most + BranchRadius * 2f;
        }

        public CoralGrowth Growth;
        public FloatRange Size = new FloatRange(0.6f, 1.2f);          // Metres across.
        public FloatRange Height = new FloatRange(0.3f, 0.6f);        // Metres tall: a table's stalk, a dome, a thicket, a finger, a tube.
        public Vector3[] Colours = { new Vector3(0.34f, 0.24f, 0.11f) };
        public Vector3 Tips = new Vector3(0.5f, 0.46f, 0.38f);        // What its youngest parts tend to.
        public float TipShare = 0.45f;
        public float Mottle = 0.12f;
        // Branching.
        public IntRange Bases = new IntRange(3, 5);
        public int Generations = 2;
        public float BranchRadius = 0.05f;
        public float BranchAngle = 0.55f;
        // Massive.
        public float Lumps = 0.06f;                                   // How far its surface heaves, of its size.
        public float Grooves;                                         // How much a brain coral's meanders show.
        public FloatRange Squat = new FloatRange(0.55f, 0.9f);        // How tall a dome stands, of how far it reaches.
        // Fingers and tubes.
        public IntRange Count = new IntRange(8, 14);
        public float Thickness = 0.035f;                              // A finger's radius, or a tube's.
        // Fan.
        public CardForm? Card;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("coral");
            Vector3 body = Colours[genes.Count("colour", 0, Colours.Length - 1)] * genes.Range("shade", 0.88f, 1.12f);
            uint noise = (uint)random.NextULong();
            switch (Growth)
            {
                case CoralGrowth.Branching: Branching(mesh, random, body, noise); break;
                case CoralGrowth.Table: Table(mesh, random, body, noise); break;
                case CoralGrowth.Massive: Massive(mesh, random, body, noise); break;
                case CoralGrowth.Fingers: Fingers(mesh, random, body, noise); break;
                case CoralGrowth.Tubes: Tubes(mesh, random, body); break;
                case CoralGrowth.Fan: Fan(mesh, random, body); break;
            }
        }

        private Vector3 Mottled(Vector3 colour, Vector3 at, uint noise)
        {
            float n = FloraNoise.Fbm(at * 7.3f, noise, 2) * 2f - 1f;
            return colour * (1f + Mottle * n);
        }

        // Staghorn: a few bases, each an antler that forks as it climbs, up and out, its tips pale where it's growing.
        private void Branching(FloraMesh mesh, WorldRandom random, Vector3 body, uint noise)
        {
            int bases = random.Int(Bases);
            float reach = random.Range(Size) * 0.5f;
            float height = random.Range(Height);
            for (int b = 0; b < bases; b++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 foot = outward * (MathF.Sqrt(random.Float()) * reach * 0.4f) - Vector3.UnitY * 0.04f;
                float tilt = random.Range(0.2f, 0.75f);
                Vector3 heading = (Vector3.UnitY * MathF.Cos(tilt) + outward * MathF.Sin(tilt)).Normalized();
                Branch(mesh, random, foot, heading, height * random.Range(0.55f, 0.8f), BranchRadius * random.Range(0.85f, 1.15f), 0, body, noise);
            }
        }

        private void Branch(FloraMesh mesh, WorldRandom random, Vector3 from, Vector3 heading, float length, float radius, int generation, Vector3 body, uint noise)
        {
            Vector3 bend = Scaffold.Perpendicular(heading) * random.Range(-0.18f, 0.18f) + Vector3.Cross(heading, Scaffold.Perpendicular(heading)) * random.Range(-0.18f, 0.18f);
            Vector3 mid = from + (heading + bend * 0.5f).Normalized() * (length * 0.5f);
            Vector3 end = mid + (heading + bend).Normalized() * (length * 0.5f);
            bool last = generation >= Generations;
            float young = (float)generation / (Generations + 1), older = (generation + 1f) / (Generations + 1);
            Vector3 foot = Mottled(body * 0.78f, from, noise);
            // Four sides at the foot and three after: a branch is three pixels thick from up here at the most, and a side more of it is a facet under a pixel.
            SeaShape.Limb(mesh, new[] { from, mid, end }, t => radius * (1f - 0.28f * t), generation == 0 ? 4 : 3, t =>
            {
                float age = MathHelper.Lerp(young, older, t);
                Vector3 colour = Vector3.Lerp(foot, body, MathF.Min(age * 1.6f, 1f));
                return last ? Vector3.Lerp(colour, Tips, SeaShape.Smooth(1f - TipShare, 1f, t) * 0.8f) : colour;
            }, last ? LimbTip.Point : LimbTip.Open, FloraMaterials.Flesh, -1);
            if (last) return;

            int children = random.Float() < 0.25f ? 3 : 2;
            float roll = random.Float() * MathF.Tau;
            for (int c = 0; c < children; c++)
            {
                // One carries on much as it was going, and the rest leave it; all bend up a little, towards the light.
                float angle = c == 0 ? random.Range(0.05f, 0.25f) : BranchAngle * random.Range(0.7f, 1.25f);
                Vector3 onward = SeaShape.Diverge((heading + (end - mid).Normalized()).Normalized(), angle, roll + c * (MathF.Tau / children));
                onward = (onward + Vector3.UnitY * 0.22f).Normalized();
                Branch(mesh, random, end - heading * (radius * 0.6f), onward, length * random.Range(0.6f, 0.85f), radius * 0.74f, generation + 1, body, noise);
            }
        }

        // A table: a flat plate on a stalk, its rim lobed and pale where it grows, darker beneath, where it's in its own shade.
        private void Table(FloraMesh mesh, WorldRandom random, Vector3 body, uint noise)
        {
            float radius = random.Range(Size) * 0.5f;
            float stalk = random.Range(Height);
            float lean = 0.12f * stalk;
            Vector3 top = new Vector3(random.Range(-lean, lean), stalk, random.Range(-lean, lean));
            int material = FloraMaterials.Flesh;
            // Leaning low down and rising straight into the plate, its flared end inside the plate's thickness: ended on a slant, one side of it came up through the top.
            var spine = new[] { new Vector3(0f, -0.05f, 0f), new Vector3(top.X, stalk * 0.55f, top.Z), new Vector3(top.X, stalk - 0.035f, top.Z) };
            SeaShape.Limb(mesh, spine, t => MathHelper.Lerp(0.09f, 0.06f, t) + SeaShape.Smooth(0.7f, 1f, t) * 0.07f, 6,
                t => body * MathHelper.Lerp(0.55f, 0.7f, t), LimbTip.Open, material, -1);

            int lobes = random.Int(4, 7);
            float phase = random.Float() * MathF.Tau;
            float rise = random.Range(-0.04f, 0.06f);
            float thick = random.Range(0.03f, 0.05f);
            // One rim for both faces, lobed out and in, and less of it nearer the middle: lobed on its top alone, its underside showed past it at every lobe.
            // Its noise is read round a circle, so the rim comes back to where it started and the lathe's seam closes.
            int scallops = random.Int(11, 17);
            float Lobe(float angle) => 0.07f * MathF.Sin(angle * lobes + phase) + 0.035f * MathF.Sin(angle * (lobes * 2 + 1) + phase * 1.7f)
                + 0.025f * MathF.Sin(angle * scallops + phase * 2.3f)
                + 0.03f * (FloraNoise.Value(new Vector3(MathF.Cos(angle) * 1.6f + 3f, MathF.Sin(angle) * 1.6f + 5f, 0.5f), noise) * 2f - 1f);
            Vector3 rim = Vector3.Lerp(body, Tips, TipShare);
            // Its top, from the middle out to the rim, rising or falling a little as it goes.
            var plate = new List<(float r, float y, Vector3 colour)>
            {
                (radius, rise, rim),
                (radius * 0.68f, rise * 0.6f + 0.012f, Mottled(body * 1.04f, top, noise)),
                (radius * 0.34f, rise * 0.2f + 0.02f, Mottled(body, top + Vector3.UnitX, noise)),
                (0f, 0.026f, body * 0.95f),   // Closed over the stalk, or its open end shows through as a hole.
            };
            SeaShape.Lathe(mesh, top, plate, 24, material, (angle, row) => 1f + Lobe(angle) * plate[row].r / radius);
            // Its underside, in its own shade, from the rim back in to the stalk.
            var under = new List<(float r, float y, Vector3 colour)>
            {
                (0.08f, -thick * 1.5f, body * 0.45f),
                (radius * 0.4f, rise * 0.25f - thick, body * 0.5f),
                (radius, rise - thick * 0.3f, rim * 0.7f),
            };
            SeaShape.Lathe(mesh, top, under, 24, material, (angle, row) => 1f + Lobe(angle) * under[row].r / radius);
        }

        // A dome: a brain coral round and grooved in meanders, a boulder coral lumpy and broad. Sunk a little into the bed, so it never sits on it.
        private void Massive(FloraMesh mesh, WorldRandom random, Vector3 body, uint noise)
        {
            float radius = random.Range(Size) * 0.5f;
            // A dome is broad, whatever its size: as tall as it was wide, a boulder coral was a cone.
            float height = radius * random.Range(Squat);
            const int Sides = 16;
            const int Rings = 6;
            var phases = new Vector3(random.Float(), random.Float(), random.Float()) * MathF.Tau;
            int material = FloraMaterials.Flesh;
            int first = mesh.VertexCount;
            for (int ring = 0; ring <= Rings; ring++)
            {
                // From a little below the bed at its skirt up to its crown.
                float phi = MathHelper.Lerp(-0.18f, MathF.PI * 0.5f, (float)ring / Rings);
                for (int s = 0; s <= Sides; s++)
                {
                    float theta = s * (MathF.Tau / Sides);
                    Vector3 round = new Vector3(MathF.Cos(phi) * MathF.Cos(theta), MathF.Sin(phi), MathF.Cos(phi) * MathF.Sin(theta));
                    float heave = MathF.Sin(theta * 2f + phases.X) * MathF.Cos(phi * 1.5f + phases.Y) * 0.6f + MathF.Sin(theta * 3f + phi * 2f + phases.Z) * 0.4f;
                    float push = 1f + Lumps * heave * (ring == Rings ? 0f : 1f);
                    Vector3 at = new Vector3(round.X * radius, round.Y * height, round.Z * radius) * push;
                    Vector3 normal = new Vector3(round.X / radius, round.Y / height, round.Z / radius).Normalized();
                    Vector3 colour = Mottled(body, at, noise) * (0.72f + 0.28f * MathF.Max(normal.Y, 0f));
                    if (Grooves > 0f)
                    {
                        // A brain's ridges run in meanders, pale on top and dark in the valleys between.
                        float meander = MathF.Abs(MathF.Sin(FloraNoise.Fbm(at * 4.1f, noise + 7u, 2) * 11f));
                        colour *= 1f - Grooves * 0.35f * (1f - meander);
                    }
                    if (ring == 0) colour *= 0.6f;
                    mesh.Add(new FloraVertex { Position = at, Normal = normal, Tangent = new Vector3(-MathF.Sin(theta), 0f, MathF.Cos(theta)), Colour = colour, UV = new Vector2((float)s / Sides, (float)ring / Rings), Material = material, Joint = -1 });
                }
            }
            const int Row = Sides + 1;
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = first + ring * Row + s;
                    mesh.Quad(a, a + Row, a + Row + 1, a + 1);
                }
            }
        }

        // A soft coral: fat fingers from a low mound, leaning out the further from its middle they rise, each giving a little to the water.
        private void Fingers(FloraMesh mesh, WorldRandom random, Vector3 body, uint noise)
        {
            float radius = random.Range(Size) * 0.5f;
            int material = FloraMaterials.Flesh;
            // A low lumpy foot, hidden by the fingers crowding up out of it.
            var mound = new List<(float r, float y, Vector3 colour)>
            {
                (radius * 0.72f, -0.04f, body * 0.55f),
                (radius * 0.62f, 0.05f, body * 0.72f),
                (radius * 0.3f, 0.1f, body * 0.82f),
                (0f, 0.115f, body * 0.82f),   // Closed at its crown, or a hole shows down into it from above.
            };
            SeaShape.Lathe(mesh, Vector3.Zero, mound, 8, material, (angle, row) => row == 3 ? 1f : 1f + 0.12f * MathF.Sin(angle * 3f + radius * 17f));

            int count = random.Int(Count);
            for (int f = 0; f < count; f++)
            {
                // Fat fingers crowd up out of the middle and lean further out the further out they rise, as a colony spreads.
                float angle = random.Float() * MathF.Tau;
                float out_ = MathF.Sqrt(random.Float());
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 foot = outward * (out_ * radius * 0.62f) + Vector3.UnitY * 0.06f;
                float length = random.Range(Height) * MathHelper.Lerp(1f, 0.7f, out_);
                float lean = 0.08f + 0.6f * out_ + random.Range(-0.1f, 0.1f);
                float fat = Thickness * random.Range(0.85f, 1.3f);
                Vector3 tint = Mottled(body, foot, noise);
                AddFinger(mesh, random, foot, outward, length, lean, fat, tint, material, -1, true);
            }
        }

        private void AddFinger(FloraMesh mesh, WorldRandom random, Vector3 foot, Vector3 outward, float length, float lean, float fat, Vector3 tint, int material, int parent, bool mayFork)
        {
            List<Vector3> spine = Strip.Arc(foot, outward, length, lean, random.Range(-0.05f, 0.35f), 1, 1.2f);
            int joint = mesh.AddJoint(SeaJoints.Finger.Between(spine[0], spine[^1], parent));
            Vector3 tip = Vector3.Lerp(tint, Tips, TipShare * 0.6f);
            SeaShape.Limb(mesh, spine, t => fat * (1f - 0.15f * t), 4, t => Vector3.Lerp(tint * 0.8f, tip, t), LimbTip.Round, material, joint);
            // Now and then a finger forks halfway up, its branch thinner and shorter.
            if (!mayFork || random.Float() > 0.3f) return;
            Vector3 from = Vector3.Lerp(spine[0], spine[^1], random.Range(0.4f, 0.6f));
            float turn = random.Range(0.6f, 1.2f) * (random.Float() < 0.5f ? -1f : 1f);
            Vector3 away = new Vector3(outward.X * MathF.Cos(turn) - outward.Z * MathF.Sin(turn), 0f, outward.X * MathF.Sin(turn) + outward.Z * MathF.Cos(turn));
            AddFinger(mesh, random, from, away, length * random.Range(0.4f, 0.6f), lean + 0.35f, fat * 0.78f, tint, material, joint, false);
        }

        // Sponges: one barrel, or a stand of tubes, each open at its top onto a hollow that's dark from above.
        private void Tubes(FloraMesh mesh, WorldRandom random, Vector3 body)
        {
            int count = random.Int(Count);
            int material = FloraMaterials.Flesh;
            if (count <= 1)
            {
                float rim = random.Range(Size) * 0.5f;
                float tall = random.Range(Height);
                var barrel = new List<(float r, float y, Vector3 colour)>
                {
                    (rim * 0.55f, -0.04f, body * 0.6f),
                    (rim * 0.7f, tall * 0.2f, body * 0.85f),
                    (rim * 0.98f, tall * 0.62f, body),
                    (rim, tall, body * 1.08f),
                    (rim * 0.8f, tall - 0.03f, body * 0.6f),
                    (rim * 0.62f, tall * 0.55f, body * 0.18f),
                };
                SeaShape.Lathe(mesh, Vector3.Zero, barrel, 14, material, (angle, row) => row is >= 1 and <= 4 ? 1f + 0.07f * MathF.Sin(angle * 7f + row * 0.6f) + 0.04f * MathF.Sin(angle * 3f + rim * 23f) : 1f);
                return;
            }
            float spread = random.Range(Size) * 0.5f;
            for (int t = 0; t < count; t++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 foot = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (MathF.Sqrt(random.Float()) * spread * 0.6f);
                float tall = random.Range(Height) * random.Range(0.6f, 1f);
                float r = Thickness * random.Range(0.85f, 1.25f);
                Vector3 tint = body * random.Range(0.9f, 1.1f);
                var tube = new List<(float r, float y, Vector3 colour)>
                {
                    (r * 1.15f, -0.03f, tint * 0.6f),
                    (r * 1.05f, tall * 0.5f, tint * 0.9f),
                    (r, tall, tint * 1.05f),
                    (r * 0.72f, tall - 0.012f, tint * 0.5f),
                    (r * 0.6f, tall * 0.7f, tint * 0.15f),
                };
                SeaShape.Lathe(mesh, foot, tube, 7, material);
            }
        }

        // A sea fan: a lattice on a short stalk, one or two of them, bowed a little, broadside to the waves the way the world turns it (WorldSeaFloor), rocking with them.
        private void Fan(FloraMesh mesh, WorldRandom random, Vector3 body)
        {
            if (Card == null) return;
            int fans = random.Float() < 0.3f ? 2 : 1;
            int material = CardLibrary.Material(Card);
            for (int f = 0; f < fans; f++)
            {
                float size = random.Range(Size) * (f == 0 ? 1f : 0.7f);
                float turn = random.Range(-0.3f, 0.3f) + (f == 0 ? 0f : random.Range(-0.6f, 0.6f));
                Vector3 across = new Vector3(MathF.Cos(turn), 0f, -MathF.Sin(turn));
                Vector3 facing = new Vector3(MathF.Sin(turn), 0f, MathF.Cos(turn));
                Vector3 foot = across * (f == 0 ? 0f : random.Range(-0.12f, 0.12f));
                float stalk = random.Range(Height) * 0.25f;
                SeaShape.Limb(mesh, new[] { foot - Vector3.UnitY * 0.03f, foot + Vector3.UnitY * stalk }, t => MathHelper.Lerp(0.025f, 0.015f, t), 4, _ => body * 0.6f, LimbTip.Open, FloraMaterials.Flesh, -1);
                Vector3 bottom = foot + Vector3.UnitY * (stalk * 0.8f);
                int joint = mesh.AddJoint(SeaJoints.Fan.Between(bottom, bottom + Vector3.UnitY * size));
                CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
                float bow = size * random.Range(0.04f, 0.1f);
                Vector3 tint = body * random.Range(0.92f, 1.08f);

                const int Columns = 2, Rows = 3;
                int first = mesh.VertexCount;
                for (int y = 0; y <= Rows; y++)
                {
                    for (int x = 0; x <= Columns; x++)
                    {
                        float u = (float)x / Columns, v = (float)y / Rows;
                        float sideways = (u - 0.5f) * 2f;
                        Vector3 at = bottom + across * (sideways * size * 0.5f) + Vector3.UnitY * (v * size) + facing * (bow * sideways * sideways);
                        // A card's normals are the mass it's part of, the same from either side: a fan's face, tipped up to the sky it's mostly lit from.
                        Vector3 normal = (facing * (1f - 0.5f * sideways * sideways) + Vector3.UnitY * 0.45f).Normalized();
                        mesh.Add(new FloraVertex { Position = at, Normal = normal, Tangent = across, Colour = tint * (0.85f + 0.2f * v), UV = new Vector2(MathHelper.Lerp(min.X, max.X, u), MathHelper.Lerp(min.Y, max.Y, v)), Material = material, Joint = joint });
                    }
                }
                const int Row = Columns + 1;
                for (int y = 0; y < Rows; y++)
                {
                    for (int x = 0; x < Columns; x++)
                    {
                        int a = first + y * Row + x;
                        mesh.Quad(a, a + 1, a + Row + 1, a + Row);
                    }
                }
            }
        }
    }

    /// <summary>A starfish: five arms, sometimes four or six, plump at the middle and thin at the tips, curled a little, lying on whatever it's crawled over.</summary>
    public sealed class StarForm : PlantForm, IUnderWater
    {
        // The crest of an arm's root over the bed, or the hub, or a tip curled up off it.
        public float Tallest => MathF.Max(Thick * 1.1f, MathF.Max(0.006f + Thick, 0.024f + 0.25f * Thick));

        public bool Rigid => false;

        public IntRange Arms = new IntRange(5, 5);
        public FloatRange Radius = new FloatRange(0.13f, 0.19f);      // Metres from its middle to a tip: larger than life, or it's a speck from up here.
        public float Plump = 0.36f;                                   // How wide an arm is at its root, of its length.
        public float Thick = 0.028f;
        public Vector3[] Colours = { new Vector3(0.5f, 0.16f, 0.05f) };
        public float Mottle = 0.18f;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("star");
            int arms = genes.Chance("odd", 0.08f) ? random.Int(4, 6) : random.Int(Arms);
            float radius = random.Range(Radius);
            Vector3 body = Colours[genes.Count("colour", 0, Colours.Length - 1)] * genes.Range("shade", 0.85f, 1.12f);
            uint noise = (uint)random.NextULong();
            int material = FloraMaterials.Flesh;
            float start = random.Float() * MathF.Tau;

            int hub = mesh.Add(new FloraVertex { Position = new Vector3(0f, Thick * 1.1f, 0f), Normal = Vector3.UnitY, Tangent = Vector3.UnitX, Colour = body * 1.05f, UV = new Vector2(0.5f), Material = material, Joint = -1 });
            var roots = new int[arms * 3];
            const int Rows = 4;
            for (int a = 0; a < arms; a++)
            {
                float angle = start + a * (MathF.Tau / arms) + random.Range(-0.12f, 0.12f);
                float curl = random.Range(-0.5f, 0.5f);
                float length = radius * random.Range(0.85f, 1.1f);
                int first = mesh.VertexCount;
                for (int r = 0; r < Rows; r++)
                {
                    float t = (float)r / (Rows - 1);
                    float heading = angle + curl * t * t;
                    Vector3 along = new Vector3(MathF.Cos(heading), 0f, MathF.Sin(heading));
                    Vector3 across = new Vector3(-along.Z, 0f, along.X);
                    Vector3 centre = along * (length * MathHelper.Lerp(0.18f, 1f, t)) + Vector3.UnitY * (0.006f + 0.018f * t * t * random.Range(0f, 1f));
                    float half = length * Plump * 0.5f * (1f - 0.82f * t);
                    float crest = Thick * (1f - 0.75f * t);
                    for (int c = 0; c < 3; c++)
                    {
                        float x = c - 1f;
                        Vector3 at = centre + across * (x * half) + Vector3.UnitY * (c == 1 ? crest : 0f);
                        Vector3 normal = (Vector3.UnitY + across * (x * 0.9f)).Normalized();
                        Vector3 colour = body * (1f + Mottle * (FloraNoise.Fbm(at * 31f, noise, 2) * 2f - 1f)) * (c == 1 ? 1.08f : 0.86f);
                        mesh.Add(new FloraVertex { Position = at, Normal = normal, Tangent = along, Colour = colour, UV = new Vector2(c * 0.5f, t), Material = material, Joint = -1 });
                    }
                }
                for (int r = 0; r + 1 < Rows; r++)
                {
                    int a0 = first + r * 3, a1 = a0 + 3;
                    mesh.Quad(a0, a0 + 1, a1 + 1, a1);
                    mesh.Quad(a0 + 1, a0 + 2, a1 + 2, a1 + 1);
                }
                // Its last row closes to the point.
                roots[a * 3] = first;
                roots[a * 3 + 1] = first + 1;
                roots[a * 3 + 2] = first + 2;
            }
            // The middle: from the crest of the hub out to each arm's root and across the gaps between them.
            for (int a = 0; a < arms; a++)
            {
                int left = roots[a * 3], crest = roots[a * 3 + 1], right = roots[a * 3 + 2];
                int nextLeft = roots[((a + 1) % arms) * 3];
                mesh.Triangle(hub, crest, left);
                mesh.Triangle(hub, right, crest);
                mesh.Triangle(hub, right, nextLeft);
            }
        }
    }

    /// <summary>
    /// A sea urchin: a dark test bristling with spines, lying on rock. Its spines are each a thin blade to a point, as they're far finer than a pixel from up here, so a few stand for many
    /// and read as what an urchin is from a distance: a dark burst.
    /// </summary>
    public sealed class UrchinForm : PlantForm, IUnderWater
    {
        // The crown of its test, or a spine at the steepest it rises, from the highest root.
        public float Tallest => MathF.Max(Radius.Max * 0.74f, Radius.Max * (0.25f + 0.45f * MathF.Sin(1.1f)) + Spine.Max * MathF.Sin(1.1f));

        public bool Rigid => false;

        public FloatRange Radius = new FloatRange(0.06f, 0.09f);
        public FloatRange Spine = new FloatRange(0.08f, 0.15f);
        public IntRange Spines = new IntRange(10, 14);
        public Vector3 Body = new Vector3(0.12f, 0.04f, 0.13f);
        public Vector3 SpineColour = new Vector3(0.1f, 0.03f, 0.11f);

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("urchin");
            float radius = random.Range(Radius);
            float shade = genes.Range("shade", 0.8f, 1.15f);
            int material = FloraMaterials.Flesh;
            var test = new List<(float r, float y, Vector3 colour)>
            {
                (radius * 0.9f, -0.01f, Body * shade * 0.7f),
                (radius, radius * 0.25f, Body * shade),
                (radius * 0.75f, radius * 0.6f, Body * shade * 1.1f),
                (0f, radius * 0.74f, Body * shade * 1.15f),
            };
            SeaShape.Lathe(mesh, Vector3.Zero, test, 7, material);

            int spines = random.Int(Spines);
            for (int s = 0; s < spines; s++)
            {
                float angle = random.Float() * MathF.Tau;
                float rise = random.Range(0.05f, 1.1f);
                Vector3 out_ = new Vector3(MathF.Cos(angle) * MathF.Cos(rise), MathF.Sin(rise), MathF.Sin(angle) * MathF.Cos(rise));
                Vector3 root = new Vector3(out_.X * radius * 0.85f, radius * 0.25f + out_.Y * radius * 0.45f, out_.Z * radius * 0.85f);
                Vector3 tip = root + out_ * random.Range(Spine);
                Vector3 side = Vector3.Cross(out_, Vector3.UnitY);
                side = side.LengthSquared > 1e-6f ? side.Normalized() * 0.008f : Vector3.UnitX * 0.008f;
                Vector3 normal = (Vector3.UnitY + out_ * 0.3f).Normalized();
                int a = mesh.Add(new FloraVertex { Position = root - side, Normal = normal, Tangent = out_, Colour = SpineColour * shade, UV = Vector2.Zero, Material = material, Joint = -1 });
                int b = mesh.Add(new FloraVertex { Position = root + side, Normal = normal, Tangent = out_, Colour = SpineColour * shade, UV = Vector2.UnitX, Material = material, Joint = -1 });
                int c = mesh.Add(new FloraVertex { Position = tip, Normal = normal, Tangent = out_, Colour = SpineColour * shade * 1.2f, UV = Vector2.UnitY, Material = material, Joint = -1 });
                mesh.Triangle(a, b, c);
            }
        }
    }

    /// <summary>
    /// Water lilies: round pads floating on still water, each cleft to its middle where its stalk comes up, crowding over each other at the edges, a young one bronze; and here and there
    /// a flower lying open on the water, or a bud standing out of it. They float, so they lie at the world's water level (WorldSeaFloor), and the wind slides over them: only a body
    /// wading through pushes them aside, or under (<see cref="SeaJoints.Pad"/>).
    /// </summary>
    public sealed class LilyForm : PlantForm
    {
        public CardForm Pad = null!;
        public CardForm Flower = null!;
        public IntRange Pads = new IntRange(6, 12);
        public FloatRange PadSize = new FloatRange(0.26f, 0.42f);     // Metres across: larger than life, or a pad is a dot from up here.
        public float Spread = 0.68f;
        public float FlowerChance = 0.55f;
        public FloatRange FlowerSize = new FloatRange(0.2f, 0.3f);
        public float BudChance = 0.4f;
        public Palette Leaves = new Palette { Shade = new Vector3(0.03f, 0.07f, 0.025f), Body = new Vector3(0.07f, 0.15f, 0.04f), Sun = new Vector3(0.14f, 0.24f, 0.06f), Drift = 0.1f, Patches = 0.12f };
        public Vector3 Bronze = new Vector3(0.17f, 0.075f, 0.04f);
        public float Young = 0.15f;                                   // Share of pads still bronze.
        public Vector3[] Blooms = { new Vector3(0.52f, 0.5f, 0.46f), new Vector3(0.5f, 0.27f, 0.33f) };

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("lilies");
            int pads = genes.Count("pads", Pads.Min, Pads.Max);
            float drift = genes.Range("drift", -1f, 1f) * Leaves.Drift;
            Vector3 bloom = Blooms[genes.Count("bloom", 0, Blooms.Length - 1)] * genes.Range("bloom shade", 0.9f, 1.08f);
            int padMaterial = CardLibrary.Material(Pad);

            var placed = new List<(Vector2 at, float size)>();
            for (int p = 0; p < pads; p++)
            {
                float size = random.Range(PadSize);
                // Pads crowd but don't pile: try a few places, and keep the one least on top of the others.
                Vector2 best = Vector2.Zero;
                float bestRoom = float.MinValue;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float angle = random.Float() * MathF.Tau;
                    Vector2 at = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (MathF.Sqrt(random.Float()) * Spread);
                    float room = float.MaxValue;
                    foreach ((Vector2 other, float otherSize) in placed) room = MathF.Min(room, (at - other).Length - (size + otherSize) * 0.36f);
                    if (room > bestRoom)
                    {
                        bestRoom = room;
                        best = at;
                    }
                }
                placed.Add((best, size));
                AddPad(mesh, random, new Vector3(best.X, 0.012f + p * 0.0028f, best.Y), size, padMaterial, drift);
            }

            if (random.Float() < FlowerChance) AddFlower(mesh, random, placed, bloom);
            if (random.Float() < BudChance) AddBud(mesh, random, placed, bloom);
        }

        private void AddPad(FloraMesh mesh, WorldRandom random, Vector3 centre, float size, int material, float drift)
        {
            float turn = random.Float() * MathF.Tau;
            // The pad's cleft, where its stalk comes up, is at the foot of its card: its joint runs from there across it, so it swings aside about its stalk and dips if it's trodden.
            Vector3 forward = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            Vector3 right = new Vector3(-forward.Z, 0f, forward.X);
            Vector3 foot = centre - forward * (size * 0.5f);
            int joint = mesh.AddJoint(SeaJoints.Pad.Between(foot, foot + forward * size));
            CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
            bool young = random.Float() < Young;
            Vector3 colour = young ? Bronze * random.Range(0.85f, 1.15f) : Leaves.At(random.Range(0.3f, 0.85f)) * (1f + drift + random.Range(-1f, 1f) * Leaves.Patches);
            // Most lie flat, and an old one turns its rim up.
            float curl = random.Float() < 0.3f ? random.Range(0.01f, 0.03f) : 0.003f;

            const int Grid = 2;
            int first = mesh.VertexCount;
            for (int y = 0; y <= Grid; y++)
            {
                for (int x = 0; x <= Grid; x++)
                {
                    float u = (float)x / Grid, v = (float)y / Grid;
                    float edge = MathF.Max(MathF.Abs(u - 0.5f), MathF.Abs(v - 0.5f)) * 2f;
                    Vector3 at = centre + right * ((u - 0.5f) * size) + forward * ((v - 0.5f) * size) + Vector3.UnitY * (curl * edge * edge);
                    Vector3 normal = (Vector3.UnitY - (right * (u - 0.5f) + forward * (v - 0.5f)) * (curl * 10f)).Normalized();
                    mesh.Add(new FloraVertex
                    {
                        Position = at, Normal = normal, Tangent = right, Colour = colour,
                        UV = new Vector2(MathHelper.Lerp(min.X, max.X, u), MathHelper.Lerp(min.Y, max.Y, v)), Material = material, Joint = joint,
                    });
                }
            }
            const int Row = Grid + 1;
            for (int y = 0; y < Grid; y++)
            {
                for (int x = 0; x < Grid; x++)
                {
                    int a = first + y * Row + x;
                    mesh.Quad(a, a + Row, a + Row + 1, a + 1);
                }
            }
        }

        // A flower lying open on the water among the pads, held a hand's width up, a shallow cup of four triangles as anything a few pixels across is.
        private void AddFlower(FloraMesh mesh, WorldRandom random, List<(Vector2 at, float size)> pads, Vector3 bloom)
        {
            (Vector2 near, float padSize) = pads[random.Int(pads.Count)];
            float angle = random.Float() * MathF.Tau;
            Vector2 spot = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (padSize * 0.45f);
            float size = random.Range(FlowerSize);
            Vector3 at = new Vector3(spot.X, random.Range(0.04f, 0.07f), spot.Y);
            int joint = mesh.AddJoint(SeaJoints.Pad.Between(at - Vector3.UnitY * 0.04f, at + Vector3.UnitY * size * 0.3f));
            int material = CardLibrary.Material(Flower);
            CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
            float turn = random.Float() * MathF.Tau;
            Vector3 right = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            // Turned so that the four triangles of the cup go counter-clockwise seen from the sky.
            Vector3 up = Vector3.Cross(Vector3.UnitY, right);

            int centre = mesh.Add(new FloraVertex { Position = at, Normal = Vector3.UnitY, Tangent = right, Colour = bloom, UV = (min + max) * 0.5f, Material = material, Joint = joint });
            int first = mesh.VertexCount;
            for (int i = 0; i < 4; i++)
            {
                float x = i == 0 || i == 3 ? -1f : 1f;
                float y = i < 2 ? -1f : 1f;
                Vector3 corner = at + (right * x + up * y) * (size * 0.5f) + Vector3.UnitY * (size * 0.22f);
                mesh.Add(new FloraVertex
                {
                    Position = corner, Normal = (Vector3.UnitY + (right * x + up * y) * 0.3f).Normalized(), Tangent = right, Colour = bloom,
                    UV = new Vector2(x < 0f ? min.X : max.X, y < 0f ? min.Y : max.Y), Material = material, Joint = joint,
                });
            }
            for (int i = 0; i < 4; i++) mesh.Triangle(centre, first + i, first + (i + 1) % 4);
        }

        // A bud on its stalk, standing out of the water: a pointed spindle, green where its sepals wrap it and the bloom's colour at its tip.
        private void AddBud(FloraMesh mesh, WorldRandom random, List<(Vector2 at, float size)> pads, Vector3 bloom)
        {
            (Vector2 near, float padSize) = pads[random.Int(pads.Count)];
            float angle = random.Float() * MathF.Tau;
            Vector2 spot = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (padSize * 0.6f);
            Vector3 foot = new Vector3(spot.X, -0.02f, spot.Y);
            Vector3 top = foot + new Vector3(random.Range(-0.03f, 0.03f), random.Range(0.1f, 0.16f), random.Range(-0.03f, 0.03f));
            int joint = mesh.AddJoint(SeaJoints.Pad.Between(foot, top));
            Vector3 sepal = Leaves.Sun * 0.95f;
            SeaShape.Limb(mesh, new[] { foot, Vector3.Lerp(foot, top, 0.55f), top }, t => 0.012f + 0.028f * SeaShape.Smooth(0.35f, 0.75f, t) * (1f - SeaShape.Smooth(0.75f, 1f, t)), 5,
                t => Vector3.Lerp(sepal, bloom, SeaShape.Smooth(0.6f, 0.95f, t)), LimbTip.Round, FloraMaterials.Flesh, joint);
        }
    }
}
