using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Churchyard;
using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Holds what's solid about everything built to what's really there (Hitboxes): grows every piece a run of lands puts down from its id, as the game does, finds where its model stands
    /// above a knee, and lays its hitboxes over that. A hitbox is the part of a thing a body walks into and a blow is stopped by, and it has to be inside what's drawn: a person walking
    /// round something that isn't there, or shut out of a gap they can see, is what this is for. Fails on a hitbox with too much air in it, on one that stands taller than what it's of,
    /// and on something tall left open, so anything new that stands in the way gets a look.
    ///   measure: says where each kind's model stands above a knee, in its own frame, to write its hitbox from.
    /// </summary>
    internal static class HitboxCheck
    {
        private const float Cell = 0.1f;            // Metres a side of the grid a model is drawn into.
        private const float OffGround = 0.3f;       // What lies lower than this is ground, a cloth, a puddle: nothing a hitbox stands on.
        private const int MostEach = 10;            // Models of a kind looked at, the first seen.
        private const float Head = 2.8f;            // Above a person's waist: a lintel, an awning, a roof's eaves, a lantern's arm, which a body walks under or ducks.
        private static bool Maps;

        /// <summary>One kind of thing: what's grown of it, from which ids.</summary>
        private sealed class Kind
        {
            public readonly List<string> Ids = new();
            public int Placed;
        }

        public static int Run(string mode, int first, int count, string? archetype, string? only) => mode switch
        {
            "measure" => Measure(first, count, archetype, only),
            "map" => Map(first, count, archetype, only),
            "catalog" => Catalog(only),
            _ => Check(first, count, archetype, only),
        };

        private const float MostAir = 0.3f;         // Of a hitbox, how much may have nothing of its model over it, near enough to brush.
        private const float Brush = 0.3f;           // Metres off a model that a hitbox's edge may stand, as a body brushes what it walks past.
        private const float MostOpenArea = 3f;      // Square metres clearly above a knee that something walked through may have before it's looked at again.
        private const float ClearlyAbove = 0.15f;   // Metres over a knee that counts as clearly above it, so a bench a hair over isn't.

        /// <summary>What's solid where nothing of it stands up, and why: a hole, which is walked round.</summary>
        private static readonly Dictionary<string, string> Holes = new() { ["yard.dug"] = "an opened grave, a hole walked round" };

        /// <summary>
        /// Holds every kind's hitboxes to its model: the share of each hitbox with nothing of the model near it (air a body would walk into), the ground the model stands on above
        /// a knee and below a waist that no hitbox covers (what a body walks into the model through), and a hitbox standing taller than its model. Fails on air, on a kind with no
        /// rule of its own, and on anything open that stands in the way over too much ground.
        /// </summary>
        public static int Check(int first, int count, string? archetype, string? only)
        {
            Dictionary<string, Kind> kinds = Gather(first, count, archetype, only);
            int problems = 0;
            var parts = new List<HitPart>();
            foreach ((string name, Kind kind) in kinds.OrderBy(k => k.Key))
            {
                float worstAir = 0f, worstPoke = 0f, openArea = 0f;
                string? worstId = null;
                HitKind rule = HitKind.Layout;
                var notes = new List<string>();
                foreach (string id in kind.Ids)
                {
                    if (Grow(id) is not Mesh mesh) continue;
                    rule = RuleOf(id, parts, out string? unruled);
                    if (unruled != null) { notes.Add(unruled); continue; }
                    if (rule == HitKind.Layout) continue;
                    Shape shape = Shape.Of(mesh);
                    if (rule is HitKind.Open or HitKind.Loose)
                    {
                        openArea = MathF.Max(openArea, shape.ClearCells * Cell * Cell);
                        continue;
                    }
                    if (Holes.ContainsKey(name)) continue;
                    if (rule != HitKind.Solid) continue;
                    foreach (HitPart part in parts)
                    {
                        float air = AirIn(part, shape);
                        if (air > worstAir) { worstAir = air; worstId = id; }
                        if (part.Height > shape.Top + 0.5f && part.Height < PlacementIndex.Tall) notes.Add(F($"a hitbox {part.Height:0.0} m tall over a model {shape.Top:0.0} m tall"));
                    }
                    worstPoke = MathF.Max(worstPoke, Poking(parts, shape));
                }
                bool bad = worstAir > MostAir || notes.Count > 0 || (rule == HitKind.Open && openArea > MostOpenArea);
                if (Holes.TryGetValue(name, out string? hole)) notes.Clear();
                if (bad) problems++;
                string line = rule switch
                {
                    HitKind.Solid when hole != null => hole,
                    HitKind.Solid => F($"air {worstAir,4:0%}  poking out {worstPoke,5:0.0} m2"),
                    HitKind.Open or HitKind.Loose => F($"stands {openArea,5:0.0} m2 above a knee"),
                    _ => "laid out as it stands",
                };
                Console.WriteLine($"{(bad ? "PROBLEM " : "        ")}{name,-26} {kind.Placed,5} placed  {rule,-6}  {line}{(notes.Count > 0 ? "  " + notes[0] : "")}{(bad && worstAir > MostAir ? "  worst " + worstId : "")}");
            }
            Console.WriteLine(problems == 0 ? $"{kinds.Count} kinds hold up." : $"{problems} kinds to look at.");
            return problems == 0 ? 0 : 1;
        }

        /// <summary>The rule an id is made solid by, and why not if it has none of its own.</summary>
        private static HitKind RuleOf(string id, List<HitPart> parts, out string? unruled)
        {
            unruled = null;
            if (PlaceSpec.TryParse(id, out PlaceSpec place))
            {
                if (Hitboxes.ByBuilder.Contains(place.Kind)) return HitKind.Layout;
                HitKind kind = Hitboxes.Of(place, parts);
                if (kind == HitKind.Layout) unruled = "no rule of its own";
                return kind;
            }
            if (YardSpec.TryParse(id, out YardSpec yard)) return Hitboxes.Of(yard, parts);
            StructureDefinition definition = StructureCatalog.Parse(id, out _, out _);
            // What the catalog leaves open on purpose, a gate's way through, a scarecrow, a crop, is its own.
            if (!definition.Solid && !definition.Loose) return HitKind.Layout;
            return Hitboxes.Of(definition, parts);
        }

        /// <summary>The share of a hitbox with nothing of its model off the ground within <see cref="Brush"/> of it.</summary>
        private static float AirIn(HitPart part, Shape shape)
        {
            int inside = 0, empty = 0;
            int reach = (int)MathF.Ceiling(Brush / Cell);
            Footprint f = part.At(Vector2.Zero, 0f);
            float r = f.BoundingRadius;
            for (float x = -r; x <= r; x += Cell)
            {
                for (float z = -r; z <= r; z += Cell)
                {
                    Vector2 p = f.Centre + new Vector2(x, z);
                    if (!Sight.Contains(f, p)) continue;
                    inside++;
                    if (!Near(shape.OffCells, p, reach)) empty++;
                }
            }
            return inside == 0 ? 0f : empty / (float)inside;
        }

        /// <summary>Square metres of a model above a knee and below a waist that no hitbox comes within <see cref="Brush"/> of.</summary>
        private static float Poking(List<HitPart> parts, Shape shape)
        {
            int poking = 0;
            foreach ((int cx, int cz) in shape.KneeCells)
            {
                Vector2 p = new Vector2((cx + 0.5f) * Cell, (cz + 0.5f) * Cell);
                bool covered = false;
                foreach (HitPart part in parts)
                {
                    if (Sight.Contains(part.At(Vector2.Zero, 0f).Grown(Brush), p)) { covered = true; break; }
                }
                if (!covered) poking++;
            }
            return poking * Cell * Cell;
        }

        private static bool Near(HashSet<(int, int)> cells, Vector2 p, int reach)
        {
            int x0 = (int)MathF.Floor(p.X / Cell), z0 = (int)MathF.Floor(p.Y / Cell);
            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dz = -reach; dz <= reach; dz++)
                {
                    if (dx * dx + dz * dz <= reach * reach && cells.Contains((x0 + dx, z0 + dz))) return true;
                }
            }
            return false;
        }

        // Every structure of the catalog, grown straight from its id whether a land put one down or not, with its picture.
        private static int Catalog(string? only)
        {
            foreach (var field in typeof(StructureCatalog).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.GetValue(null) is not StructureDefinition definition || (only != null && !definition.Id.Contains(only, StringComparison.OrdinalIgnoreCase))) continue;
                string id = StructureCatalog.AssetId(definition, BuildingStyle.Timber, 0);
                if (Grow(id) is not Mesh mesh) continue;
                Shape shape = Shape.Of(mesh);
                Console.WriteLine(F($"structure.{definition.Id,-18} half {definition.HalfExtents.X:0.00},{definition.HalfExtents.Y:0.00} height {definition.Height,5:0.0} solid {definition.Solid} loose {definition.Loose}  top {shape.Top,5:0.00}") + $"  body x {Range(shape.KneeUp, 0)} z {Range(shape.KneeUp, 1)} fill {shape.Fill,4:0%}  off-ground x {Range(shape.Off, 0)} z {Range(shape.Off, 1)}");
                if (only != null) shape.Draw();
            }
            return 0;
        }

        private static int Map(int first, int count, string? archetype, string? only)
        {
            Maps = true;
            return Measure(first, count, archetype, only);
        }

        public static int Measure(int first, int count, string? archetype, string? only)
        {
            Dictionary<string, Kind> kinds = Gather(first, count, archetype, only);
            foreach ((string name, Kind kind) in kinds.OrderBy(k => k.Key))
            {
                Console.WriteLine($"{name}  ({kind.Placed} placed, {kind.Ids.Count} looked at)");
                foreach (string id in kind.Ids)
                {
                    if (Grow(id) is not Mesh mesh) continue;
                    Shape shape = Shape.Of(mesh);
                    Console.WriteLine(F($"    {Short(id),-58} top {shape.Top,5:0.00}") + $"  body x {Range(shape.KneeUp, 0)} z {Range(shape.KneeUp, 1)} fill {shape.Fill,4:0%}  off-ground x {Range(shape.Off, 0)} z {Range(shape.Off, 1)}");
                    if (Maps && id == kind.Ids[0]) shape.Draw();
                }
            }
            return 0;
        }

        private static string Range(Bounds box, int axis) => box.Empty ? "     none     " : axis == 0 ? F($"{box.Min.X,6:0.00}..{box.Max.X,-6:0.00}") : F($"{box.Min.Y,6:0.00}..{box.Max.Y,-6:0.00}");

        private static string F(FormattableString text) => FormattableString.Invariant(text);

        /// <summary>A box on the ground round what's been seen, empty until something has.</summary>
        internal sealed class Bounds
        {
            public Vector2 Min = new Vector2(float.MaxValue), Max = new Vector2(float.MinValue);
            public bool Empty => Max.X < Min.X;
            public void Add(Vector2 p)
            {
                Min = Vector2.ComponentMin(Min, p);
                Max = Vector2.ComponentMax(Max, p);
            }
        }

        private static string Short(string id) => id.Length > 58 ? id[..58] : id;

        /// <summary>Every kind of built thing a run of lands puts down, by a name of its own, with up to <see cref="MostEach"/> of its ids.</summary>
        private static Dictionary<string, Kind> Gather(int first, int count, string? archetype, string? only)
        {
            var kinds = new Dictionary<string, Kind>();
            for (int seed = first; seed < first + count; seed++)
            {
                WorldData world = new Dino_Engine.World.Generation.WorldGenerator().Generate(new WorldRequest(seed, archetype));
                var seen = new HashSet<string>();
                foreach (WorldObject item in world.Objects.All())
                {
                    string id = world.Objects.AssetIds[item.Asset];
                    string? name = KindOf(id);
                    if (name == null || (only != null && !name.Contains(only, StringComparison.OrdinalIgnoreCase))) continue;
                    if (!kinds.TryGetValue(name, out Kind? kind)) kinds[name] = kind = new Kind();
                    kind.Placed++;
                    if (kind.Ids.Count < MostEach && seen.Add(id) && !kind.Ids.Contains(id)) kind.Ids.Add(id);
                }
            }
            return kinds;
        }

        /// <summary>What kind of built thing an id is, by a name, or null for what grows, a stone of the land's, a fence or a hall.</summary>
        internal static string? KindOf(string id)
        {
            if (PlaceSpec.TryParse(id, out PlaceSpec place)) return "place." + place.Kind.ToString().ToLowerInvariant();
            if (YardSpec.TryParse(id, out YardSpec yard))
            {
                string name = "yard." + yard.Kind.ToString().ToLowerInvariant();
                return yard.Kind == YardKind.Stone ? name + "." + ((StoneForm)yard.Int(0)).ToString().ToLowerInvariant() : name;
            }
            if (StructureCatalog.TryGetPlain(id, out StructureDefinition plain)) return "structure." + plain.Id;
            int dot = id.LastIndexOf('.');
            if (dot > 0 && int.TryParse(id[(dot + 1)..], out _))
            {
                try { return "structure." + StructureCatalog.Parse(id, out _, out _).Id; }
                catch { return null; }
            }
            return null;
        }

        private static readonly Dictionary<string, Mesh?> Grown = new();

        internal static Mesh? Grow(string id)
        {
            if (Grown.TryGetValue(id, out Mesh? known)) return known;
            var lights = new List<AssetLight>();
            Mesh? mesh = null;
            if (PlaceSpec.TryParse(id, out PlaceSpec place)) mesh = PlaceForge.Make(place, lights);
            else if (YardSpec.TryParse(id, out YardSpec yard)) mesh = YardForge.Make(yard, lights);
            else
            {
                StructureDefinition definition = StructureCatalog.Parse(id, out BuildingStyle style, out int variant);
                if (definition == StructureCatalog.GrainCrop || definition == StructureCatalog.VegetableCrop) mesh = null;
                else if (definition.Styled && definition != StructureCatalog.WatchTower) mesh = BuildingGenerator.Generate(definition, style, variant, new List<Vector3>(), lights);
                else
                {
                    // What a structure's id names but a plant grows (a dead pine) is the plant's, with its own trunk.
                    try { mesh = PropGenerator.Generate(definition, style, lights); }
                    catch (ArgumentException) { mesh = null; }
                }
            }
            Grown[id] = mesh;
            return mesh;
        }

        /// <summary>Where a model stands, in its own frame: drawn into a grid from above, what's above a knee and what's off the ground at all.</summary>
        internal sealed class Shape
        {
            public float Top;
            public readonly Bounds KneeUp = new(), Off = new();
            public readonly HashSet<(int, int)> KneeCells = new(), OffCells = new(), ClearSeen = new();

            public static Shape Of(Mesh mesh)
            {
                var shape = new Shape();
                float[] p = mesh.getAllPositionsArray();
                vIndex[] indices = mesh.getAllIndicesArray();
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    Vector3 a = At(p, indices[t].index), b = At(p, indices[t + 1].index), c = At(p, indices[t + 2].index);
                    float longest = MathF.Max((b - a).Length, MathF.Max((c - a).Length, (c - b).Length));
                    int n = Math.Clamp((int)MathF.Ceiling(longest / (Cell * 0.7f)), 1, 400);
                    for (int i = 0; i <= n; i++)
                    {
                        for (int j = 0; i + j <= n; j++)
                        {
                            Vector3 q = a + (b - a) * (i / (float)n) + (c - a) * (j / (float)n);
                            shape.Mark(q);
                        }
                    }
                }
                return shape;
            }

            private void Mark(Vector3 q)
            {
                if (!float.IsFinite(q.X) || !float.IsFinite(q.Y) || !float.IsFinite(q.Z)) return;
                Top = MathF.Max(Top, q.Y);
                if (q.Y < OffGround) return;
                (int, int) cell = ((int)MathF.Floor(q.X / Cell), (int)MathF.Floor(q.Z / Cell));
                Off.Add(q.Xz);
                OffCells.Add(cell);
                if (q.Y < Hitboxes.StepOver || q.Y > Head) return;
                KneeUp.Add(q.Xz);
                if (KneeCells.Add(cell)) { }
                if (q.Y >= Hitboxes.StepOver + ClearlyAbove && ClearSeen.Add(cell)) ClearCells++;
            }

            /// <summary>Cells of what stands clearly above a knee and below a waist.</summary>
            public int ClearCells;

            /// <summary>How much of the box round what's above a knee is filled by it.</summary>
            public float Fill => KneeUp.Empty ? 0f : KneeCells.Count * Cell * Cell / MathF.Max((KneeUp.Max.X - KneeUp.Min.X + Cell) * (KneeUp.Max.Y - KneeUp.Min.Y + Cell), 1e-4f);

            /// <summary>A picture from above, half a metre to a letter, +z up the page: # what's above a knee, . what's only off the ground.</summary>
            public void Draw()
            {
                if (Off.Empty) return;
                const float Step = 0.5f;
                int x0 = (int)MathF.Floor(Off.Min.X / Step), x1 = (int)MathF.Floor(Off.Max.X / Step), z0 = (int)MathF.Floor(Off.Min.Y / Step), z1 = (int)MathF.Floor(Off.Max.Y / Step);
                int per = (int)MathF.Round(Step / Cell);
                for (int z = z1; z >= z0; z--)
                {
                    var line = new System.Text.StringBuilder(FormattableString.Invariant($"      {z * Step,6:0.0} "));
                    for (int x = x0; x <= x1 && x - x0 < 110; x++)
                    {
                        bool knee = false, off = false;
                        for (int i = 0; i < per && !knee; i++)
                        {
                            for (int j = 0; j < per; j++)
                            {
                                (int, int) c = (x * per + i, z * per + j);
                                knee |= KneeCells.Contains(c);
                                off |= OffCells.Contains(c);
                            }
                        }
                        line.Append(knee ? '#' : off ? '.' : x == 0 || z == 0 ? '+' : ' ');
                    }
                    Console.WriteLine(line.ToString());
                }
                Console.WriteLine(FormattableString.Invariant($"             x from {x0 * Step:0.0}, +z up, half a metre a letter"));
            }

            private static Vector3 At(float[] p, int i) => new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);
        }
    }
}
