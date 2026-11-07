using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Leaves from one crown: a fern, an agave, a banana, the head of a palm. Each leaf is a bent strip with a card on it, arching out on a golden angle, older and lower on the outside
    /// and younger and more upright in the middle, which is the shuttlecock a fern makes.
    /// </summary>
    public sealed class RosetteForm : PlantForm
    {
        public CardForm Card = null!;
        public IntRange Leaves = new IntRange(9, 14);
        public FloatRange Length = new FloatRange(1.3f, 2.2f);      // Metres, for the outermost.
        public float Inner = 0.62f;                                 // How long the innermost is, against that.
        public float Aspect = 0.34f;                                // Width of a leaf for its length.
        public FloatRange Lean = new FloatRange(0.3f, 0.95f);       // Radians from upright, from the innermost to the outermost.
        public FloatRange Arch = new FloatRange(0.9f, 1.5f);        // Radians it bends over by its tip.
        public float Curl = 1.5f;                                   // Above 1 a leaf goes straight and then falls away.
        public float FootSpread = 0.1f;                             // Metres of crown they come from.
        public int Segments = 5;
        public float Fold = 0.22f;
        public float Shell = 0.55f;
        public Palette Colours = new Palette();
        public PlantJoint Joint = PlantJoint.Frond;
        public int Links = 3;
        public int Crozier;                                         // Young fronds still curled up in the middle.

        public override void Grow(FloraMesh mesh, Genes genes) => Grow(mesh, genes, Vector3.Zero, -1, 1f);

        /// <summary>Grows from <paramref name="at"/> and hangs from <paramref name="parentJoint"/>, for the head of a palm.</summary>
        public void Grow(FloraMesh mesh, Genes genes, Vector3 at, int parentJoint, float scale)
        {
            WorldRandom random = genes.Stream("rosette");
            int leaves = genes.Count("leaves", Leaves.Min, Leaves.Max);
            float length = genes.Range("length", Length) * scale;
            float start = genes.Range("start", 0f, MathF.Tau);
            float drift = genes.Range("drift", -1f, 1f) * Colours.Drift;
            float warmth = genes.Range("warmth", -1f, 1f) * Colours.Drift;
            int material = CardLibrary.Material(Card);
            const float Golden = 2.39996f;

            for (int i = 0; i < leaves; i++)
            {
                // Innermost first, so that the outer leaves are what's on top from up here only where they lie.
                float age = leaves > 1 ? (float)i / (leaves - 1) : 1f;
                float angle = start + i * Golden + random.Range(-0.2f, 0.2f);
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 across = new Vector3(-outward.Z, 0f, outward.X);

                float own = length * MathHelper.Lerp(Inner, 1f, age) * random.Range(0.86f, 1.12f);
                float lean = Lean.Lerp(age) + random.Range(-0.1f, 0.1f);
                float arch = Arch.Lerp(age) * random.Range(0.85f, 1.15f);
                Vector3 foot = at + outward * (FootSpread * scale * (0.4f + 0.6f * age)) - Vector3.UnitY * 0.03f;
                List<Vector3> spine = Strip.Arc(foot, outward, own, lean, arch, Segments, Curl);

                // Fronds lean away from the sky a little to each side, or they'd all be edge on from somewhere.
                Vector3 side = (across + Vector3.UnitY * random.Range(-0.18f, 0.18f)).Normalized();
                CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 uvMin, out Vector2 uvMax);
                float tint = random.Range(-1f, 1f) * Colours.Patches;
                float lightness = MathHelper.Lerp(0.72f, 0.38f, age);

                var style = new StripStyle
                {
                    Material = material, UvMin = uvMin, UvMax = uvMax, Fold = Fold, Across = 2, Centre = at - Vector3.UnitY * own * 0.3f, Shell = Shell,
                    Joint = Joint, Links = Links, ParentJoint = parentJoint,
                };
                float half = own * Aspect * 0.5f;
                Strip.Add(mesh, spine, _ => half, side, style, (t, x) =>
                {
                    Vector3 colour = Colours.At(lightness + 0.32f * t);
                    colour *= 1f + tint + drift;
                    colour.X *= 1f + warmth * 0.7f;
                    return colour;
                });
            }

            for (int i = 0; i < Crozier; i++) AddCrozier(mesh, random, at, length, parentJoint);
        }

        // A fiddlehead: a young frond before it's opened, a stalk rolled up at the top.
        private void AddCrozier(FloraMesh mesh, WorldRandom random, Vector3 at, float length, int parent)
        {
            float angle = random.Float() * MathF.Tau;
            Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            float tall = length * random.Range(0.28f, 0.42f);
            var spine = new List<Vector3> { at + outward * 0.05f };
            Vector3 point = spine[0];
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                float turn = t < 0.55f ? 0.2f * t : MathF.Pow((t - 0.55f) / 0.45f, 1.3f) * 4.6f;
                point += (Vector3.UnitY * MathF.Cos(turn) + outward * MathF.Sin(turn)) * (tall / 8f * (t < 0.55f ? 1.3f : 0.55f));
                spine.Add(point);
            }
            int joint = mesh.AddJoint(PlantJoint.Stem.Between(spine[0], spine[^1], parent));
            Strip.Stalk(mesh, spine, 0.028f, 0.014f, FloraMaterials.Bark(BarkKind.Stem), Colours.Sun * 1.2f, joint);
        }
    }

    /// <summary>What stands up out of a tuft.</summary>
    public enum SeedHead
    {
        None,
        Plume,      // A card of soft strands: pampas, reed.
        Catkin,     // A solid sausage: bulrush.
        Ear,        // A bent ear of grain.
    }

    /// <summary>Grass that's a plant and not a field: blades in a tuft, and stalks with something on top of them. Reeds, pampas, sedge, grain.</summary>
    public sealed class TuftForm : PlantForm
    {
        public IntRange Blades = new IntRange(14, 22);
        public FloatRange BladeLength = new FloatRange(1.6f, 2.6f);
        public float BladeWidth = 0.05f;
        public FloatRange BladeArch = new FloatRange(0.7f, 1.7f);
        public float Spread = 0.28f;                    // Metres the tuft stands on.
        public IntRange Stalks = new IntRange(3, 6);
        public FloatRange StalkHeight = new FloatRange(2.6f, 3.6f);
        public SeedHead Head = SeedHead.Plume;
        public CardForm? HeadCard;
        public float HeadLength = 0.9f;
        public float HeadWidth = 0.34f;
        public Vector3 HeadColour = new Vector3(0.5f, 0.42f, 0.3f);
        public Palette Colours = new Palette();
        public int Segments = 4;

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("tuft");
            int blades = genes.Count("blades", Blades.Min, Blades.Max);
            int stalks = genes.Count("stalks", Stalks.Min, Stalks.Max);
            float drift = genes.Range("drift", -1f, 1f) * Colours.Drift;
            int stem = FloraMaterials.Bark(BarkKind.Stem);

            for (int i = 0; i < blades; i++)
            {
                float angle = random.Float() * MathF.Tau;
                float distance = MathF.Sqrt(random.Float()) * Spread;
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 foot = outward * distance - Vector3.UnitY * 0.04f;
                float length = random.Range(BladeLength);
                float lean = 0.12f + 0.3f * (distance / MathF.Max(Spread, 0.01f));
                List<Vector3> spine = Strip.Arc(foot, outward, length, lean, random.Range(BladeArch), Segments, 1.7f);
                float tone = random.Range(0.3f, 0.75f);
                float half = BladeWidth * random.Range(0.75f, 1.2f);
                var style = new StripStyle { Material = stem, Across = 2, Fold = 0.3f, Centre = -Vector3.UnitY * 0.5f, Shell = 0.45f, Joint = PlantJoint.Blade, Links = 3, ParentJoint = -1 };
                Strip.Add(mesh, spine, t => half * (1f - t * t * 0.92f), new Vector3(-outward.Z, 0f, outward.X), style, (t, x) => Colours.At(tone + 0.3f * t) * (1f + drift));
            }

            for (int i = 0; i < stalks; i++) AddStalk(mesh, random, genes, drift, stem);
        }

        private void AddStalk(FloraMesh mesh, WorldRandom random, Genes genes, float drift, int stem)
        {
            float angle = random.Float() * MathF.Tau;
            Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            Vector3 foot = outward * random.Range(0f, Spread * 0.7f);
            float height = random.Range(StalkHeight);
            List<Vector3> spine = Strip.Arc(foot, outward, height, random.Range(0.02f, 0.14f), random.Range(0.05f, 0.4f), 4, 1.5f);
            int joint = mesh.AddJoint(PlantJoint.Stem.Between(spine[0], spine[^1]));
            Strip.Stalk(mesh, spine, 0.03f, 0.012f, stem, Colours.At(0.5f) * (1f + drift), joint);
            Vector3 top = spine[^1];
            Vector3 heading = (spine[^1] - spine[^2]).Normalized();

            switch (Head)
            {
                case SeedHead.Plume when HeadCard != null:
                    int material = CardLibrary.Material(HeadCard);
                    CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 uvMin, out Vector2 uvMax);
                    Vector3 lean = (heading + outward * 0.5f).Normalized();
                    List<Vector3> plume = Strip.Arc(top - heading * (HeadLength * 0.25f), outward, HeadLength, MathF.Acos(Math.Clamp(lean.Y, -1f, 1f)), random.Range(0.3f, 0.9f), 3, 1.3f);
                    foreach (Vector3 side in new[] { new Vector3(-outward.Z, 0f, outward.X), outward })
                    {
                        var style = new StripStyle { Material = material, UvMin = uvMin, UvMax = uvMax, Across = 1, Centre = top - Vector3.UnitY, Shell = 0.4f, Joint = PlantJoint.Head, Links = 1, ParentJoint = joint };
                        Vector3 colour = HeadColour * random.Range(0.88f, 1.1f);
                        float half = HeadWidth * 0.5f;
                        Strip.Add(mesh, plume, t => half, side, style, (t, x) => colour);
                    }
                    break;

                case SeedHead.Catkin:
                    var spike = new List<Vector3> { top - heading * HeadLength * 0.8f, top - heading * HeadLength * 0.4f, top };
                    Strip.Stalk(mesh, spike, HeadWidth * 0.5f, HeadWidth * 0.4f, FloraMaterials.Flesh, HeadColour, joint);
                    break;

                case SeedHead.Ear:
                    List<Vector3> ear = Strip.Arc(top, outward, HeadLength, 0.4f, 1.1f, 3, 1.2f);
                    int head = mesh.AddJoint(PlantJoint.Head.Between(ear[0], ear[^1], joint));
                    Strip.Stalk(mesh, ear, HeadWidth * 0.5f, HeadWidth * 0.2f, FloraMaterials.Bark(BarkKind.Stem), HeadColour, head);
                    break;
            }
        }
    }

    /// <summary>How flowers are carried.</summary>
    public enum Inflorescence
    {
        Solitary,   // One flower to a stem, looking at the sky: daisy, poppy, lily.
        Spike,      // Many up a stem: lupine, foxglove, lavender.
    }

    /// <summary>A flowering plant: leafy stems from a clump. A flower is a few pixels from up here, so it's a card lying open to the sky, and that's all it is.</summary>
    public sealed class HerbForm : PlantForm
    {
        public IntRange Stems = new IntRange(5, 9);
        public FloatRange Height = new FloatRange(0.6f, 1.1f);
        public float Spread = 0.4f;
        public CardForm LeafCard = null!;
        public int LeavesPerStem = 3;
        public float LeafLength = 0.42f;
        public float LeafAspect = 0.5f;
        public CardForm Flower = null!;
        public Inflorescence Carried = Inflorescence.Solitary;
        public FloatRange FlowerSize = new FloatRange(0.34f, 0.5f);
        public float SpikeLength = 0.55f;
        public Palette Foliage = new Palette();
        public Palette Bloom = new Palette { Shade = new Vector3(0.3f, 0.05f, 0.05f), Body = new Vector3(0.6f, 0.1f, 0.07f), Sun = new Vector3(0.8f, 0.2f, 0.1f) };

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("herb");
            int stems = genes.Count("stems", Stems.Min, Stems.Max);
            float drift = genes.Range("drift", -1f, 1f) * Bloom.Drift;
            int stemMaterial = FloraMaterials.Bark(BarkKind.Stem);
            int leafMaterial = CardLibrary.Material(LeafCard);
            int flowerMaterial = CardLibrary.Material(Flower);

            for (int s = 0; s < stems; s++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 foot = outward * (MathF.Sqrt(random.Float()) * Spread);
                float height = random.Range(Height);
                List<Vector3> spine = Strip.Arc(foot, outward, height, random.Range(0.02f, 0.28f), random.Range(-0.12f, 0.3f), 3, 1.4f);
                int joint = mesh.AddJoint(PlantJoint.Stem.Between(spine[0], spine[^1]));
                Strip.Stalk(mesh, spine, 0.018f, 0.009f, stemMaterial, Foliage.At(0.5f), joint);

                for (int l = 0; l < LeavesPerStem; l++)
                {
                    float t = (l + 0.4f) / LeavesPerStem * 0.72f;
                    Vector3 at = Vector3.Lerp(spine[0], spine[^1], t);
                    float leafAngle = angle + l * 2.4f + random.Range(-0.4f, 0.4f);
                    Vector3 away = new Vector3(MathF.Cos(leafAngle), 0f, MathF.Sin(leafAngle));
                    float length = LeafLength * random.Range(0.75f, 1.15f) * (1f - 0.45f * t);
                    List<Vector3> leaf = Strip.Arc(at, away, length, random.Range(0.75f, 1.15f), random.Range(0.4f, 0.9f), 2, 1.3f);
                    CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
                    var style = new StripStyle { Material = leafMaterial, UvMin = min, UvMax = max, Across = 1, Centre = foot - Vector3.UnitY * 0.2f, Shell = 0.5f, Joint = PlantJoint.Leaf, Links = 1, ParentJoint = joint };
                    float tone = random.Range(0.32f, 0.7f);
                    float half = length * LeafAspect * 0.5f;
                    Strip.Add(mesh, leaf, _ => half, new Vector3(-away.Z, 0f, away.X), style, (u, x) => Foliage.At(tone + 0.15f * u));
                }

                if (Carried == Inflorescence.Spike) AddSpike(mesh, random, spine, joint, flowerMaterial, outward, drift);
                else AddFlower(mesh, random, spine[^1], joint, flowerMaterial, (spine[^1] - spine[^2]).Normalized(), drift);
            }
        }

        private void AddFlower(FloraMesh mesh, WorldRandom random, Vector3 at, int parent, int material, Vector3 heading, float drift)
        {
            float size = random.Range(FlowerSize);
            Vector3 face = (Vector3.UnitY * 0.8f + heading * 0.5f + new Vector3(random.Range(-0.25f, 0.25f), 0f, random.Range(-0.25f, 0.25f))).Normalized();
            Vector3 right = Scaffold.Perpendicular(face);
            Vector3 up = Vector3.Cross(face, right);
            int joint = mesh.AddJoint(PlantJoint.Head.Between(at, at + face * size * 0.5f, parent));
            CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
            float tone = random.Range(0.45f, 0.95f);
            Vector3 colour = Bloom.At(tone) * (1f + drift);

            // A shallow bowl, in one fan of four triangles.
            int centre = mesh.Add(new FloraVertex { Position = at, Normal = face, Tangent = right, Colour = colour, UV = (min + max) * 0.5f, Material = material, Joint = joint });
            int first = mesh.VertexCount;
            for (int i = 0; i < 4; i++)
            {
                float x = i == 0 || i == 3 ? -1f : 1f;
                float y = i < 2 ? -1f : 1f;
                Vector3 corner = at + (right * x + up * y) * (size * 0.5f) + face * (size * 0.16f);
                mesh.Add(new FloraVertex
                {
                    Position = corner,
                    Normal = (face + (right * x + up * y) * 0.25f).Normalized(),
                    Tangent = right,
                    Colour = colour,
                    UV = new Vector2(x < 0f ? min.X : max.X, y < 0f ? min.Y : max.Y),
                    Material = material,
                    Joint = joint,
                });
            }
            for (int i = 0; i < 4; i++) mesh.Triangle(centre, first + i, first + (i + 1) % 4);
        }

        private void AddSpike(FloraMesh mesh, WorldRandom random, List<Vector3> stem, int parent, int material, Vector3 outward, float drift)
        {
            Vector3 top = stem[^1];
            Vector3 heading = (stem[^1] - stem[^2]).Normalized();
            float length = SpikeLength * random.Range(0.8f, 1.2f);
            float size = random.Range(FlowerSize);
            var spine = new List<Vector3> { top - heading * (length * 0.45f), top, top + heading * (length * 0.55f) };
            CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
            Vector3 colour = Bloom.At(random.Range(0.5f, 0.95f)) * (1f + drift);
            foreach (Vector3 side in new[] { new Vector3(-outward.Z, 0f, outward.X), outward })
            {
                var style = new StripStyle { Material = material, UvMin = min, UvMax = max, Across = 1, Centre = top - Vector3.UnitY * 0.5f, Shell = 0.3f, Joint = PlantJoint.Head, Links = 1, ParentJoint = parent };
                Strip.Add(mesh, spine, _ => size * 0.5f, side, style, (t, x) => colour);
            }
        }
    }

    /// <summary>Mushrooms in a group, as lathed shapes. They're the only thing in a wood that's solid and small, so they get real geometry.</summary>
    public sealed class FungusForm : PlantForm
    {
        public IntRange Count = new IntRange(4, 8);
        public float Spread = 0.42f;
        public FloatRange Height = new FloatRange(0.22f, 0.5f);
        public FloatRange CapRadius = new FloatRange(0.12f, 0.26f);
        public float Dome = 0.7f;           // How tall a cap is for its width.
        public float Flare = 0.1f;          // How far a cap's edge turns up, as an old one's does.
        public float StemWidth = 0.22f;     // As a share of the cap.
        public Vector3 Cap = new Vector3(0.6f, 0.14f, 0.08f);
        public Vector3 CapTop = new Vector3(0.45f, 0.08f, 0.05f);
        public Vector3 Gills = new Vector3(0.36f, 0.3f, 0.23f);
        public Vector3 Stem = new Vector3(0.42f, 0.37f, 0.3f);
        public float Spots;                 // 0 to 1, how much of a cap is pale flecks.
        public bool Glows;                  // Some give their own light, for what's dark.

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("mushrooms");
            int count = genes.Count("count", Count.Min, Count.Max);
            uint seed = (uint)random.NextULong();
            int material = Glows ? FloraMaterials.Glow : FloraMaterials.Flesh;
            for (int i = 0; i < count; i++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 foot = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (MathF.Sqrt(random.Float()) * Spread);
                float height = random.Range(Height);
                float radius = random.Range(CapRadius) * (0.7f + height);
                Vector3 lean = new Vector3(random.Range(-0.2f, 0.2f), 1f, random.Range(-0.2f, 0.2f)).Normalized();
                Lathe(mesh, foot - Vector3.UnitY * 0.04f, lean, height, radius, material, seed + (uint)i * 31u);
            }
        }

        // A section down the middle, turned about the stem: up the stem, out under the gills, over the top.
        private void Lathe(FloraMesh mesh, Vector3 foot, Vector3 axis, float height, float radius, int material, uint seed)
        {
            float stem = radius * StemWidth;
            float dome = radius * Dome;
            var section = new (float r, float y, Vector3 colour)[]
            {
                (stem * 1.25f, 0f, Stem * 0.8f),
                (stem, height * 0.5f, Stem),
                (stem * 0.9f, height, Stem),
                (radius * 0.62f, height + dome * 0.05f, Gills),
                (radius, height + dome * (0.1f + Flare), Gills * 0.8f),
                (radius * 1.02f, height + dome * (0.16f + Flare), Cap),
                (radius * 0.9f, height + dome * 0.52f, Cap),
                (radius * 0.58f, height + dome * 0.86f, CapTop),
                (radius * 0.04f, height + dome, CapTop),
            };

            const int Sides = 9;
            Vector3 right = Scaffold.Perpendicular(axis);
            Vector3 other = Vector3.Cross(axis, right);
            int first = mesh.VertexCount;
            for (int row = 0; row < section.Length; row++)
            {
                var (r, y, colour) = section[row];
                float rise = row + 1 < section.Length ? section[row + 1].y - y : section[row].y - section[row - 1].y;
                float wide = row + 1 < section.Length ? section[row + 1].r - r : section[row].r - section[row - 1].r;
                for (int s = 0; s <= Sides; s++)
                {
                    float angle = s * (MathF.Tau / Sides);
                    Vector3 outward = right * MathF.Cos(angle) + other * MathF.Sin(angle);
                    Vector3 normal = (outward * rise - axis * wide).Normalized();
                    Vector3 at = foot + outward * r + axis * y;
                    Vector3 tint = colour;
                    if (Spots > 0f && row >= 6 && FloraNoise.Hash((uint)(s * 31 + row * 7) + seed) % 100u < (uint)(Spots * 26f)) tint = new Vector3(0.58f, 0.54f, 0.46f);
                    mesh.Add(new FloraVertex { Position = at, Normal = normal, Tangent = -right * MathF.Sin(angle) + other * MathF.Cos(angle), Colour = tint, UV = new Vector2((float)s / Sides, (float)row / section.Length), Material = material, Joint = -1 });
                }
            }
            const int Row = Sides + 1;
            for (int row = 0; row + 1 < section.Length; row++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = first + row * Row + s;
                    mesh.Quad(a, a + 1, a + Row + 1, a + Row);
                }
            }
        }
    }

    /// <summary>
    /// What lies on the ground: leaves in a drift, a cushion of moss. One card laid over the ground is two triangles, which is why a maple wood can have a carpet. It's bent a little
    /// so that it doesn't read as flat, and it doesn't move, as only a falling leaf does that.
    /// </summary>
    public sealed class GroundForm : PlantForm
    {
        public CardForm Card = null!;
        public FloatRange Size = new FloatRange(1.8f, 2.8f);
        public int Layers = 1;
        public float Lift = 0.04f;
        public Palette Colours = new Palette();

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("ground");
            int material = CardLibrary.Material(Card);
            for (int layer = 0; layer < Layers; layer++)
            {
                float size = random.Range(Size);
                float angle = random.Float() * MathF.Tau;
                Vector3 right = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                Vector3 forward = new Vector3(-right.Z, 0f, right.X);
                Vector3 centre = (right * random.Range(-0.3f, 0.3f) + forward * random.Range(-0.3f, 0.3f)) * size;
                CardLibrary.Cell(random.Int(CardLibrary.CellsPerLayer), out Vector2 min, out Vector2 max);
                Vector3 colour = Colours.At(random.Range(0.4f, 0.8f));

                const int Grid = 2;
                int first = mesh.VertexCount;
                for (int y = 0; y <= Grid; y++)
                {
                    for (int x = 0; x <= Grid; x++)
                    {
                        float u = (float)x / Grid, v = (float)y / Grid;
                        float edge = MathF.Max(MathF.Abs(u - 0.5f), MathF.Abs(v - 0.5f)) * 2f;
                        Vector3 at = centre + right * ((u - 0.5f) * size) + forward * ((v - 0.5f) * size) + Vector3.UnitY * (Lift + layer * 0.03f - edge * edge * Lift * 2f);
                        mesh.Add(new FloraVertex { Position = at, Normal = Vector3.UnitY, Tangent = right, Colour = colour, UV = new Vector2(MathHelper.Lerp(min.X, max.X, u), MathHelper.Lerp(min.Y, max.Y, v)), Material = material, Joint = -1 });
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
        }
    }

    /// <summary>A prickly pear: flat pads, each growing from the edge of the last.</summary>
    public sealed class PadForm : PlantForm
    {
        public IntRange Generations = new IntRange(3, 4);
        public IntRange Roots = new IntRange(2, 3);
        public FloatRange PadSize = new FloatRange(0.55f, 0.85f);
        public Vector3 Colour = new Vector3(0.2f, 0.32f, 0.14f);
        public Vector3 Fruit = new Vector3(0.6f, 0.1f, 0.2f);

        public override void Grow(FloraMesh mesh, Genes genes)
        {
            WorldRandom random = genes.Stream("pads");
            int generations = genes.Count("generations", Generations.Min, Generations.Max);
            int roots = genes.Count("roots", Roots.Min, Roots.Max);
            for (int i = 0; i < roots; i++)
            {
                float angle = random.Float() * MathF.Tau;
                Vector3 up = new Vector3(MathF.Cos(angle) * 0.35f, 1f, MathF.Sin(angle) * 0.35f).Normalized();
                AddPad(mesh, random, new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * 0.2f, up, random.Range(PadSize), generations);
            }
        }

        private void AddPad(FloraMesh mesh, WorldRandom random, Vector3 foot, Vector3 heading, float size, int left)
        {
            Vector3 facing = Scaffold.Perpendicular(heading);
            facing = (facing + new Vector3(random.Range(-0.3f, 0.3f), random.Range(-0.1f, 0.1f), random.Range(-0.3f, 0.3f))).Normalized();
            Vector3 side = Vector3.Cross(heading, facing).Normalized();
            facing = Vector3.Cross(side, heading).Normalized();

            // An oval, thick in the middle and thin at its rim.
            const int Rim = 9;
            float tall = size * 1.15f, wide = size * 0.42f, thick = size * 0.09f;
            Vector3 centre = foot + heading * (tall * 0.5f);
            int material = FloraMaterials.Bark(BarkKind.Stem);
            foreach (float sign in new[] { 1f, -1f })
            {
                int hub = mesh.Add(new FloraVertex { Position = centre + facing * (thick * sign), Normal = facing * sign, Tangent = side, Colour = Colour * 1.08f, UV = new Vector2(0.5f), Material = material, Joint = -1 });
                int first = mesh.VertexCount;
                for (int i = 0; i <= Rim; i++)
                {
                    float angle = i * (MathF.Tau / Rim);
                    float wobble = 1f + 0.06f * MathF.Sin(angle * 3f + size * 9f);
                    Vector3 at = centre + (side * (MathF.Cos(angle) * wide) + heading * (MathF.Sin(angle) * tall * 0.5f)) * wobble;
                    Vector3 normal = (facing * sign + (at - centre).Normalized() * 0.9f).Normalized();
                    mesh.Add(new FloraVertex { Position = at, Normal = normal, Tangent = side, Colour = Colour * 0.8f, UV = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 0.5f + new Vector2(0.5f), Material = material, Joint = -1 });
                }
                for (int i = 0; i < Rim; i++)
                {
                    if (sign > 0f) mesh.Triangle(hub, first + i, first + i + 1);
                    else mesh.Triangle(hub, first + i + 1, first + i);
                }
            }

            if (left <= 0) return;
            int children = random.Float() < 0.55f ? 2 : 1;
            for (int i = 0; i < children; i++)
            {
                float angle = random.Range(-0.95f, 0.95f);
                Vector3 onward = (heading * MathF.Cos(angle) + side * MathF.Sin(angle)).Normalized();
                Vector3 from = centre + (side * (MathF.Sin(angle) * wide) + heading * (MathF.Cos(angle) * tall * 0.5f)) * 0.92f;
                AddPad(mesh, random, from, (onward + Vector3.UnitY * 0.25f).Normalized(), size * random.Range(0.72f, 0.95f), left - 1);
            }
        }
    }
}
