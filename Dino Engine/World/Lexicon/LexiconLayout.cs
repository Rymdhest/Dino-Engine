using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Lexicon
{
    /// <summary>An upright rectangle on the ground, in world metres.</summary>
    public readonly record struct GroundBox(Vector2 Min, Vector2 Max)
    {
        public Vector2 Centre => (Min + Max) * 0.5f;
        public Vector2 Half => (Max - Min) * 0.5f;
        public Vector2 Size => Max - Min;
        public bool Contains(Vector2 point, float grow = 0f) => point.X >= Min.X - grow && point.X <= Max.X + grow && point.Y >= Min.Y - grow && point.Y <= Max.Y + grow;

        /// <summary>Metres from a point to the box, nothing inside it.</summary>
        public float Outside(Vector2 point)
        {
            float dx = MathF.Max(MathF.Max(Min.X - point.X, point.X - Max.X), 0f);
            float dz = MathF.Max(MathF.Max(Min.Y - point.Y, point.Y - Max.Y), 0f);
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        public GroundBox Grow(float by) => new GroundBox(Min - new Vector2(by), Max + new Vector2(by));
    }

    /// <summary>What a place needs of the land round it to be what it is, which the lexicon lays out for it on its plot (<see cref="LexiconDioramas"/>).</summary>
    public enum Diorama
    {
        None,
        /// <summary>A beach and the sea beyond it, to the north: a wreck lies on one, and a fishing village has its jetties out over it.</summary>
        Coast,
        /// <summary>A hillside rising to the north, as a quarry is cut into one that the camera looks at.</summary>
        Hill,
        /// <summary>The land it stands in is under snow.</summary>
        Snow,
        /// <summary>A wood behind it, to the north, as a logging camp stands at the edge of what it fells.</summary>
        WoodsBehind,
        /// <summary>A wood all round it, as a nesting ground is deep in one.</summary>
        WoodsAround,
    }

    /// <summary>A section of the lexicon, where it stands.</summary>
    public sealed class PlacedSection
    {
        public ExhibitSection Section = null!;
        public int Index;

        /// <summary>Everything in it, its sign included.</summary>
        public GroundBox Bounds;

        /// <summary>Where its sign is read, at its south-west corner, on the way in.</summary>
        public Vector2 Sign;
    }

    /// <summary>A family's run of plots in one row, where its sign stands at the west end. A family too long for one row goes on in the next, and says so.</summary>
    public sealed class PlacedFamily
    {
        public ExhibitFamily Family = null!;
        public PlacedSection Section = null!;
        public Vector2 Sign;
        public bool Continued;
        public GroundBox Bounds;
    }

    /// <summary>An exhibit, where it stands.</summary>
    public sealed class PlacedExhibit
    {
        public Exhibit Exhibit = null!;
        public PlacedFamily Family = null!;
        public PlacedSection Section = null!;
        public int Index;

        /// <summary>Its whole plot, whatever it needs of the land round it included.</summary>
        public GroundBox Plot;

        /// <summary>Where what's on show stands: a creature's feet, a tree's trunk, a place's middle.</summary>
        public Vector2 Centre;

        /// <summary>Where its plaque is read, on the walk along the front of its row.</summary>
        public Vector2 Plaque;

        /// <summary>For a place: how far it reaches from its middle, which its builder lays it out in.</summary>
        public float Radius;

        public Diorama Diorama;

        /// <summary>Where someone stands to look at it, in front of its plaque, with it in the middle of the screen.</summary>
        public Vector2 Viewpoint;
    }

    /// <summary>
    /// Where everything in the asset lexicon stands (LEXICON.md): a flat isle with the gate home at its south end, and north of it two columns, the small things to the west (left on the
    /// screen) and the places to the east, with an avenue between them. Each section is a run of rows, its families side by side from west to east and each family's plots side by side,
    /// with every plaque along the front of its row, which is where the walk goes. A place that needs the sea stands on the north shore, and whatever else a place needs of the land, a
    /// hillside or a wood, is laid out on its own plot. A pure function of the catalogue, so a client and the server, which grow the world apart, agree on where everything is.
    /// </summary>
    public sealed class LexiconPlan
    {
        /// <summary>How high the floor of the isle stands above the sea, in metres.</summary>
        public const float Ground = 6f;

        /// <summary>Metres from the water's edge to where the floor is level, which is the beach.</summary>
        public const float Beach = 64f;

        /// <summary>Metres from the edge of the world to the water's edge, which is the sea.</summary>
        public const float Sea = 96f;

        /// <summary>Metres a place that needs the sea stands from the water's edge: on the beach, as a wreck and a fishing village are, with the water near enough to find.</summary>
        public const float CoastInland = 24f;

        public float Size;

        /// <summary>The water's edge, all round.</summary>
        public GroundBox Island;

        /// <summary>Where heroes arrive, the landing with its gate home.</summary>
        public Vector2 Arrival;
        public float ArrivalRadius;

        /// <summary>Where the room's own sign is read, by the landing.</summary>
        public Vector2 Sign;

        public readonly List<PlacedSection> Sections = new();
        public readonly List<PlacedFamily> Families = new();
        public readonly List<PlacedExhibit> Exhibits = new();

        /// <summary>The paved walk along the front of each row, where its plaques are.</summary>
        public readonly List<GroundBox> Walks = new();

        /// <summary>The paved ground each section's sign stands on.</summary>
        public readonly List<GroundBox> Forecourts = new();

        private Dictionary<string, PlacedExhibit>? _byKey;

        public PlacedExhibit? Find(string key)
        {
            _byKey ??= Exhibits.ToDictionary(exhibit => exhibit.Exhibit.Key, StringComparer.Ordinal);
            return _byKey.TryGetValue(key, out PlacedExhibit? found) ? found : null;
        }

        /// <summary>The section a point is in, if it's in one, or the nearest within a little of one.</summary>
        public PlacedSection? SectionAt(Vector2 point, float grow = 12f)
        {
            PlacedSection? best = null;
            float nearest = grow;
            foreach (PlacedSection section in Sections)
            {
                float away = section.Bounds.Outside(point);
                if (away > nearest) continue;
                nearest = away;
                best = section;
            }
            return best;
        }

        /// <summary>The family whose row a point is on, if any.</summary>
        public PlacedFamily? FamilyAt(Vector2 point, float grow = 4f)
        {
            PlacedFamily? best = null;
            float nearest = grow;
            foreach (PlacedFamily family in Families)
            {
                float away = family.Bounds.Outside(point);
                if (away > nearest) continue;
                nearest = away;
                best = family;
            }
            return best;
        }
    }

    /// <summary>Works out a <see cref="LexiconPlan"/> from the catalogue: which column each section goes in, how wide each column is so that the isle is as small as it can be, and where every plot is.</summary>
    public static class LexiconLayout
    {
        // Metres. The game's camera is some 33 m off and 67 degrees up, where a metre is about thirty pixels, so a plaque's two lines take a metre and a half of ground.
        private const float SignBand = 10f;       // South of a section's first row: its sign.
        private const float PlaqueBand = 4f;      // The front of a row: the walk its plaques are on.
        private const float RowGap = 5f;          // Between the back of one row and the walk of the next.
        private const float FamilySign = 16f;     // At the west end of a family's run: its sign.
        private const float FamilyGap = 8f;
        private const float PlotGap = 1.5f;
        private const float SectionGap = 26f;     // Between sections in a column.
        private const float ColumnGap = 44f;      // The avenue between the two columns.
        private const float Edge = 16f;           // From the last plot to where the beach begins.
        private const float ArrivalDepth = 64f;   // The landing's ground, south of everything.
        private const float CoastGap = 18f;       // Between plots on the north shore.

        // Where someone sent to look at something stands: due south of the middle of its plot, near enough that the middle of it is a fifth of the screen above the hero, clear of the
        // minimap and the corner box. The game's camera is 30 m up and 13 m back at 67° with a 60° view (ArpgGame), so that's where (30 - h) / (13 + d) is 1.31, h up and d ahead:
        // flat ground is looked at from 10 m off, a person from 9 m, a tall tree from 5 m. Never from in what's solid of it (Exhibit.Clear).
        private const float CameraUp = 30f, CameraBack = 12.94f, ViewSlope = 1.31f, NearestView = 5f, ClearView = 1.5f;
        private const float PlaqueClear = 3.5f;   // Metres south of a plaque that someone standing is clear of its words on the screen.
        private const float RoomSignBack = 5f;    // Metres behind where heroes arrive that the room's sign is read: on the screen between them and the bar along its foot.

        /// <summary>Metres a hillside behind a place takes, a wood behind it and a wood round it, past its own plot.</summary>
        public const float HillDepth = 64f;
        public const float WoodsDepth = 44f;
        public const float WoodsRing = 28f;

        private static readonly Lazy<LexiconPlan> _current = new(() => Compose(LexiconCatalog.Sections), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>Where everything in the catalogue stands, worked out once.</summary>
        public static LexiconPlan Current => _current.Value;

        private readonly record struct Item(Exhibit Exhibit, ExhibitFamily Family, float U, float V, float Width, float Depth, Vector2 Stand, Diorama Diorama, float Radius);
        private readonly record struct Sign(ExhibitFamily Family, float U, float V, bool Continued, float Width);
        private readonly record struct Walk(float U0, float U1, float V);

        // One section laid out at a width, in its own frame: u to the east (right on the screen) from its west edge, v to the north from its south edge.
        private sealed class Block
        {
            public ExhibitSection Section = null!;
            public float Width, Depth;
            public readonly List<Item> Items = new();
            public readonly List<Sign> Signs = new();
            public readonly List<Walk> Walks = new();
        }

        public static LexiconPlan Compose(IReadOnlyList<ExhibitSection> sections)
        {
            List<ExhibitSection> small = sections.Where(section => !section.Large).ToList();
            List<ExhibitSection> large = sections.Where(section => section.Large).ToList();

            // What stands by the sea goes on the north shore, whatever section says it.
            var coast = new List<(Exhibit Exhibit, ExhibitFamily Family, ExhibitSection Section)>();
            foreach (ExhibitSection section in large)
            {
                foreach (ExhibitFamily family in section.Families)
                {
                    foreach (Exhibit exhibit in family.Exhibits)
                    {
                        if (DioramaOf(exhibit) == Diorama.Coast) coast.Add((exhibit, family, section));
                    }
                }
            }

            // The narrowest each column can be is its widest plot and a sign: try every pair of widths from there, and keep whichever makes the smallest square. Between two about as
            // small, the one with less of a walk from south to north, which is the way everything is walked.
            float smallLeast = MathF.Max(144f, WidestRun(small));
            float largeLeast = MathF.Max(MathF.Max(240f, WidestRun(large)), CoastWidth(coast));
            float bestCost = float.MaxValue, bestSmall = smallLeast, bestLarge = largeLeast;
            for (float ws = smallLeast; ws <= smallLeast + 320f; ws += 16f)
            {
                float smallDepth = ColumnDepth(small, ws);
                for (float wl = largeLeast; wl <= largeLeast + 960f; wl += 16f)
                {
                    float largeDepth = ColumnDepth(large, wl) + CoastDepth(coast);
                    float width = (small.Count > 0 ? ws : 0f) + (large.Count > 0 ? wl : 0f) + (small.Count > 0 && large.Count > 0 ? ColumnGap : 0f);
                    float side = MathF.Max(width, MathF.Max(smallDepth, largeDepth) + ArrivalDepth);
                    float cost = side + 0.08f * smallDepth + 0.05f * largeDepth;
                    if (cost >= bestCost - 0.01f) continue;
                    bestCost = cost;
                    bestSmall = ws;
                    bestLarge = wl;
                }
            }
            return Place(small, large, coast, bestSmall, bestLarge);
        }

        // What a place needs of the land, and how much more ground that takes past its own plot.
        private static Diorama DioramaOf(Exhibit exhibit) => exhibit.Kind == LexiconKind.Place ? LexiconDioramas.Of(exhibit.Place) : Diorama.None;

        // A wood round a place is open to the south, where the camera and its plaque are, or the trees in front would hide it.
        private static Vector2 Extra(Diorama diorama) => diorama switch
        {
            Diorama.Hill => new Vector2(0f, HillDepth),
            Diorama.WoodsBehind => new Vector2(0f, WoodsDepth),
            Diorama.WoodsAround => new Vector2(WoodsRing * 2f, WoodsRing),
            _ => Vector2.Zero,
        };

        /// <summary>Metres from a place's own edge to the front of its plot, which is its plaque's walk: near enough that standing at its plaque shows its way in, 10 m ahead.</summary>
        public const float PlaceFront = 8f;

        // A plot's whole size, and where in it (from its south-west corner) what's on show stands: a place near the front of its plot, with whatever room it needs past its edge to the
        // north and either side, and anything else in the middle, as far off it as it says.
        private static (Vector2 Size, Vector2 Stand) Fit(Exhibit exhibit)
        {
            Diorama diorama = DioramaOf(exhibit);
            Vector2 size = exhibit.Size + Extra(diorama);
            if (exhibit.Kind == LexiconKind.Place && diorama != Diorama.Coast) return (size, new Vector2(size.X * 0.5f, LexiconDioramas.RadiusOf(exhibit.Place) + PlaceFront));
            // The layout's frame runs east, which is the world's -x, and north, which is its z.
            return (size, size * 0.5f + new Vector2(-exhibit.Offset.X, exhibit.Offset.Y));
        }

        private static float WidestRun(List<ExhibitSection> sections)
        {
            float widest = 0f;
            foreach (Exhibit exhibit in sections.SelectMany(section => section.Exhibits))
            {
                if (DioramaOf(exhibit) == Diorama.Coast) continue;
                widest = MathF.Max(widest, FamilySign + Fit(exhibit).Size.X);
            }
            return widest;
        }

        private static float CoastWidth(List<(Exhibit Exhibit, ExhibitFamily Family, ExhibitSection Section)> coast) =>
            coast.Count == 0 ? 0f : FamilySign + coast.Sum(item => item.Exhibit.Size.X) + CoastGap * (coast.Count - 1);

        // The coast row and the plateau's edge before it, as far as the column of places goes.
        private static float CoastDepth(List<(Exhibit Exhibit, ExhibitFamily Family, ExhibitSection Section)> coast) =>
            coast.Count == 0 ? 0f : SectionGap + coast.Max(item => item.Exhibit.Size.Y * 0.5f) + CoastInland();

        private static float CoastInland() => LexiconPlan.CoastInland;

        private static float ColumnDepth(List<ExhibitSection> sections, float width)
        {
            float depth = 0f;
            foreach (ExhibitSection section in sections)
            {
                Block block = Lay(section, width);
                if (block.Items.Count == 0) continue;
                depth += (depth > 0f ? SectionGap : 0f) + block.Depth;
            }
            return depth;
        }

        // A section's rows at a width: families side by side, west to east, each kept whole on a row where it fits on one, and the rows south to north.
        private static Block Lay(ExhibitSection section, float width)
        {
            var block = new Block { Section = section, Width = width };
            float u = 0f, v = SignBand, rowDepth = 0f, rowEnd = 0f;
            bool rowUsed = false;

            void NewRow()
            {
                if (!rowUsed) return;
                block.Walks.Add(new Walk(0f, rowEnd, v));
                v += PlaqueBand + rowDepth + RowGap;
                u = 0f;
                rowDepth = 0f;
                rowEnd = 0f;
                rowUsed = false;
            }

            foreach (ExhibitFamily family in section.Families)
            {
                var plots = new List<(Exhibit Exhibit, Vector2 Size, Vector2 Stand, Diorama Diorama)>();
                foreach (Exhibit exhibit in family.Exhibits)
                {
                    Diorama diorama = DioramaOf(exhibit);
                    if (diorama == Diorama.Coast) continue;
                    (Vector2 size, Vector2 stand) = Fit(exhibit);
                    plots.Add((exhibit, size, stand, diorama));
                }
                if (plots.Count == 0) continue;

                float run = FamilySign + plots.Sum(plot => plot.Size.X + PlotGap);
                if (rowUsed && u + run > width) NewRow();

                block.Signs.Add(new Sign(family, u, v, false, FamilySign));
                u += FamilySign;
                rowUsed = true;
                foreach ((Exhibit exhibit, Vector2 size, Vector2 stand, Diorama diorama) in plots)
                {
                    if (u + size.X > width && u > FamilySign + 0.5f)
                    {
                        NewRow();
                        block.Signs.Add(new Sign(family, u, v, true, FamilySign));
                        u += FamilySign;
                        rowUsed = true;
                    }
                    block.Items.Add(new Item(exhibit, family, u, v + PlaqueBand, size.X, size.Y, stand, diorama, 0f));
                    rowDepth = MathF.Max(rowDepth, size.Y);
                    u += size.X + PlotGap;
                    rowEnd = u;
                }
                u += FamilyGap - PlotGap;
            }
            if (rowUsed) block.Walks.Add(new Walk(0f, rowEnd, v));
            block.Depth = rowUsed || block.Items.Count > 0 ? v + PlaqueBand + rowDepth : 0f;
            return block;
        }

        // Everything where it goes, in the frame of the whole layout first (u east from the west edge of the column of small things, v north from the front of the first rows), and then
        // turned into the world's, where x is west and z north, the isle in the middle of a square of sea.
        private static LexiconPlan Place(List<ExhibitSection> small, List<ExhibitSection> large, List<(Exhibit Exhibit, ExhibitFamily Family, ExhibitSection Section)> coast, float smallWidth, float largeWidth)
        {
            var blocks = new List<(Block Block, float U, float V)>();
            float smallTop = Stack(small, smallWidth, 0f, blocks);
            float largeU = small.Count > 0 ? smallWidth + ColumnGap : 0f;
            float largeTop = Stack(large, large.Count > 0 ? largeWidth : 0f, largeU, blocks);

            float contentEast = large.Count > 0 ? largeU + largeWidth : smallWidth;
            float shore = MathF.Max(smallTop, largeTop) + Edge + LexiconPlan.Beach;

            // The north shore's places stand side by side on one line, as far in from the water's edge as such a place would be in a land, each as wide as it needs.
            var coastItems = new List<Item>();
            if (coast.Count > 0)
            {
                float deepest = coast.Max(item => item.Exhibit.Size.Y * 0.5f);
                float line = MathF.Max(largeTop + SectionGap + deepest, shore - LexiconPlan.CoastInland);
                shore = line + LexiconPlan.CoastInland;
                float width = CoastWidth(coast);
                float u = largeU + MathF.Max((largeWidth - width) * 0.5f, 0f) + FamilySign;
                contentEast = MathF.Max(contentEast, largeU + width);
                foreach ((Exhibit exhibit, ExhibitFamily family, ExhibitSection _) in coast)
                {
                    Vector2 plot = exhibit.Size;
                    coastItems.Add(new Item(exhibit, family, u, line - plot.Y * 0.5f, plot.X, plot.Y, plot * 0.5f, Diorama.Coast, 0f));
                    u += plot.X + CoastGap;
                }
            }

            // The landing at the south end of the avenue between the columns, or under the only column there is.
            float arrivalU = small.Count > 0 && large.Count > 0 ? smallWidth + ColumnGap * 0.5f : contentEast * 0.5f;
            float arrivalV = -ArrivalDepth * 0.5f;

            float west = -Edge - LexiconPlan.Beach, east = contentEast + Edge + LexiconPlan.Beach;
            float south = -ArrivalDepth - Edge - LexiconPlan.Beach, north = shore;
            float islandWidth = east - west, islandDepth = north - south;
            float size = MathF.Ceiling((MathF.Max(islandWidth, islandDepth) + LexiconPlan.Sea * 2f) / 8f) * 8f;

            // From the layout's frame to the world's: x = originX - u, z = originZ + v, with the isle centred.
            float originX = size * 0.5f + (west + east) * 0.5f;
            float originZ = size * 0.5f - (south + north) * 0.5f;
            Vector2 World(float u, float v) => new Vector2(originX - u, originZ + v);
            GroundBox Box(float u0, float v0, float u1, float v1) => new GroundBox(new Vector2(originX - u1, originZ + v0), new Vector2(originX - u0, originZ + v1));

            var plan = new LexiconPlan
            {
                Size = size,
                Island = Box(west, south, east, north),
                Arrival = World(arrivalU, arrivalV),
                ArrivalRadius = SiteCatalog.Landing.Radius.Min,
                Sign = World(arrivalU, arrivalV - RoomSignBack),
            };

            foreach ((Block block, float bu, float bv) in blocks)
            {
                var section = new PlacedSection
                {
                    Section = block.Section,
                    Index = plan.Sections.Count,
                    Bounds = Box(bu, bv, bu + block.Width, bv + block.Depth),
                    Sign = World(bu, bv + SignBand * 0.62f),
                };
                plan.Sections.Add(section);
                plan.Forecourts.Add(Box(bu - 2f, bv + 1.5f, bu + MathF.Min(block.Width, 42f), bv + SignBand - 1.5f));
                foreach (Walk walk in block.Walks) plan.Walks.Add(Box(bu + walk.U0 - 1f, bv + walk.V + 0.4f, bu + walk.U1, bv + walk.V + PlaqueBand - 0.4f));

                PlacedFamily? family = null;
                int sign = 0;
                foreach (Item item in block.Items)
                {
                    // Each run of a family starts at its sign, which is just west of its first plot on the same row.
                    while (sign < block.Signs.Count && block.Signs[sign].Family == item.Family && block.Signs[sign].U <= item.U && Math.Abs(block.Signs[sign].V + PlaqueBand - item.V) < 0.01f)
                    {
                        Sign at = block.Signs[sign++];
                        family = new PlacedFamily
                        {
                            Family = at.Family,
                            Section = section,
                            Continued = at.Continued,
                            Sign = World(bu + at.U + 1f, bv + at.V + PlaqueBand * 0.5f),
                            Bounds = Box(bu + at.U, bv + at.V, bu + at.U + at.Width, bv + at.V + PlaqueBand),
                        };
                        plan.Families.Add(family);
                    }
                    AddExhibit(plan, item, family!, section, bu, bv, World, Box);
                }
            }

            if (coastItems.Count > 0)
            {
                // The coast is a family of its own, whatever section its places came from, with its sign at the west end of the row.
                PlacedSection? home = plan.Sections.FirstOrDefault(section => section.Section == coast[0].Section);
                var section = home ?? new PlacedSection { Section = coast[0].Section, Index = plan.Sections.Count };
                float first = coastItems[0].U - FamilySign;
                float front = coastItems.Min(item => item.V);
                var shoreFamily = new ExhibitFamily { Name = "By the sea", Detail = "Where the land meets the water" };
                foreach (Item item in coastItems) shoreFamily.Exhibits.Add(item.Exhibit);
                var family = new PlacedFamily
                {
                    Family = shoreFamily,
                    Section = section,
                    Sign = World(first + 1f, front - PlaqueBand * 0.5f),
                    Bounds = Box(first, front - PlaqueBand, first + FamilySign, front),
                };
                plan.Families.Add(family);
                foreach (Item item in coastItems) AddExhibit(plan, item, family, section, 0f, 0f, World, Box);
                if (home != null) home.Bounds = Union(home.Bounds, Box(first, front - PlaqueBand, coastItems[^1].U + coastItems[^1].Width, coastItems.Max(item => item.V + item.Depth)));
            }
            return plan;
        }

        private static float ViewFrom(Exhibit exhibit) => MathF.Max(MathF.Max((CameraUp - exhibit.Height * 0.5f) / ViewSlope - CameraBack, NearestView), exhibit.Clear + ClearView);

        private static GroundBox Union(GroundBox a, GroundBox b) => new GroundBox(Vector2.ComponentMin(a.Min, b.Min), Vector2.ComponentMax(a.Max, b.Max));

        private static void AddExhibit(LexiconPlan plan, Item item, PlacedFamily family, PlacedSection section, float bu, float bv, Func<float, float, Vector2> world, Func<float, float, float, float, GroundBox> box)
        {
            Exhibit exhibit = item.Exhibit;
            float u = bu + item.U, v = bv + item.V;
            Vector2 centre = world(u + item.Stand.X, v + item.Stand.Y);
            // A place's plaque is read at the south edge of its own plot, which is where its way in faces; anything else's at the front of its row.
            float front = item.Diorama == Diorama.Coast ? v - 1.2f : v - PlaqueBand * 0.5f;
            var placed = new PlacedExhibit
            {
                Exhibit = exhibit,
                Family = family,
                Section = section,
                Index = plan.Exhibits.Count,
                Plot = box(u, v, u + item.Width, v + item.Depth),
                Centre = centre,
                Plaque = world(u + item.Width * 0.5f, front),
                Radius = exhibit.Kind == LexiconKind.Place ? LexiconDioramas.RadiusOf(exhibit.Place) : 0f,
                Diorama = item.Diorama,
            };
            // A place is looked at from a little south of its middle, as the camera sees further ahead than behind, which has most of it in view: the game moves that to the middle of what
            // it's built, which only the grown world knows (LexiconWorld.StandFor). Anything else from in front of the middle of its plot, as near as its height wants.
            placed.Viewpoint = exhibit.Kind == LexiconKind.Place ? placed.Centre - new Vector2(0f, LexiconWorld.PlaceView) : new Vector2(placed.Plaque.X, placed.Plot.Centre.Y - ViewFrom(exhibit));
            // Never on the walk by its plaque, which is drawn over whoever stands there: a step south of it instead.
            if (exhibit.Kind != LexiconKind.Place && placed.Viewpoint.Y > placed.Plaque.Y - PlaqueClear && placed.Viewpoint.Y < placed.Plot.Min.Y) placed.Viewpoint.Y = placed.Plaque.Y - PlaqueClear;
            plan.Exhibits.Add(placed);
            family.Bounds = Union(family.Bounds, placed.Plot);
        }

        // Sections one above another, south to north, from a point in the layout's frame; says where the last one ends.
        private static float Stack(List<ExhibitSection> sections, float width, float u, List<(Block Block, float U, float V)> blocks)
        {
            float v = 0f;
            foreach (ExhibitSection section in sections)
            {
                Block block = Lay(section, width);
                if (block.Items.Count == 0) continue;
                if (v > 0f) v += SectionGap;
                blocks.Add((block, u, v));
                v += block.Depth;
            }
            return v;
        }
    }

    /// <summary>
    /// What each kind of place needs of the land round it to be what it is, read from what its <see cref="SiteDefinition"/> asks of the ground when a land chooses where it goes, so that a new
    /// kind of place gets what it needs in the lexicon with nothing added here.
    /// </summary>
    public static class LexiconDioramas
    {
        public static Diorama Of(SiteKind kind)
        {
            SiteDefinition definition = SiteCatalog.Get(kind);
            if (definition.MaxInland < float.PositiveInfinity || definition.MostAboveSea < float.PositiveInfinity) return Diorama.Coast;
            if (definition.LeastRise > 0f) return Diorama.Hill;
            if (definition.Climate != null) return Diorama.Snow;
            if (definition.LeastWoods > 0f) return definition.Fells ? Diorama.WoodsBehind : Diorama.WoodsAround;
            return Diorama.None;
        }

        /// <summary>How far a place reaches from its middle in the lexicon: the least its kind is ever laid out in, which is a real size for it and keeps the room small.</summary>
        public static float RadiusOf(SiteKind kind) => SiteCatalog.Get(kind).Radius.Min;

        /// <summary>The biome a place whose kind asks for a climate is laid out in: the first in the catalogue its kind will have, which for a snow castle is the snow.</summary>
        public static BiomeDefinition? ClimateOf(SiteKind kind)
        {
            SiteDefinition definition = SiteCatalog.Get(kind);
            if (definition.Climate == null) return null;
            return BiomeCatalog.All.FirstOrDefault(biome => definition.Climate(biome));
        }
    }
}
