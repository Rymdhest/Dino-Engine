using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using Dino_Engine.World.Lexicon;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_WorldLab
{
    /// <summary>
    /// Grows the asset lexicon (LEXICON.md) with no window and holds it to what it promises: no plot on another, every place built where its plot is and every plant and stone planted on
    /// its own, what lies on the ground laid on it as a land lays it, the floor level with no water standing on it, every plaque on ground that can be walked to from the landing, and nothing
    /// on the plateau that isn't on show. Draws its plan as the game's camera sees it, +x to the left and north up, every plot outlined in its section's colour. Exit code 1 on any problem.
    /// The game's own shelf, its creatures and people, is in the game's assembly, which is loaded from beside this one's if it's been built (or from --game), so that the whole room is laid
    /// out as the game lays it out; otherwise it's said that only the engine's sections were checked. --find words lists every exhibit those words are in, with where to stand to see it, for
    /// --at in the game.
    /// </summary>
    internal static class LexiconCheck
    {
        private const float LevelTolerance = 0.08f;

        public static int Run(string? path, int resolution, string? game = null, string? find = null)
        {
            bool withGame = LoadGame(game);
            var clock = Stopwatch.StartNew();
            LexiconPlan plan = LexiconLayout.Current;
            if (!withGame) Console.WriteLine("The game hasn't been built, so only the engine's sections are laid out and checked: build Dino Defenders, or pass --game.");
            if (find != null)
            {
                string[] words = find.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (PlacedExhibit exhibit in plan.Exhibits)
                {
                    string about = $"{exhibit.Exhibit.Name} {exhibit.Exhibit.Detail} {exhibit.Exhibit.Key} {exhibit.Exhibit.Look} {exhibit.Exhibit.Keywords} {exhibit.Family.Family.Name} {exhibit.Section.Section.Name}".ToLowerInvariant();
                    if (words.All(about.Contains)) Console.WriteLine(FormattableString.Invariant($"  {exhibit.Exhibit.Key,-34} {exhibit.Exhibit.Name,-30} --at {exhibit.Viewpoint.X:0},{exhibit.Viewpoint.Y:0}  (it stands at {exhibit.Centre.X:0},{exhibit.Centre.Y:0})"));
                }
                return 0;
            }
            double laidOut = clock.Elapsed.TotalSeconds;
            WorldData world = new WorldGenerator().Generate(LexiconWorld.Request);
            Console.WriteLine($"Laid out in {laidOut:0.000} s and grown in {clock.Elapsed.TotalSeconds - laidOut:0.00} s: {world.Plan.Size:0} m a side, the isle {plan.Island.Size.X:0} by {plan.Island.Size.Y:0} m, "
                + $"{plan.Sections.Count} sections, {plan.Families.Count} family runs, {plan.Exhibits.Count} exhibits, {world.Objects.Count} objects.");
            foreach (PlacedSection section in plan.Sections)
            {
                int count = plan.Exhibits.Count(exhibit => exhibit.Section == section);
                Console.WriteLine($"  {section.Section.Name,-14} {count,4} exhibits in {section.Section.Families.Count,3} families, {section.Bounds.Size.X:0} by {section.Bounds.Size.Y:0} m, sign at {section.Sign.X:0},{section.Sign.Y:0}");
            }

            var problems = new List<string>();
            CheckPlots(plan, problems);
            CheckPlaces(world, plan, problems);
            CheckSpecimens(world, plan, problems);
            CheckFloor(world, plan, problems);
            CheckWalks(world, plan, problems);
            CheckClear(world, plan, problems);
            if (world.Ponds.Count > 0) problems.Add($"{world.Ponds.Count} ponds stand on the floor, the first at {world.Ponds[0].Centre.X:0},{world.Ponds[0].Centre.Y:0}.");
            if (world.Haunts.Count > 0) problems.Add($"{world.Haunts.Count} packs have a haunt in the room.");
            int hostile = world.Sites.Sum(site => site.Spawns.Count(spawn => spawn.Role.IsHostile()));
            if (hostile > 0) problems.Add($"{hostile} hostile spawns are left in the room's places.");

            Draw(world, plan, path ?? "lexicon.png", resolution);
            foreach (string problem in problems) Console.WriteLine("FAIL " + problem);
            Console.WriteLine(problems.Count == 0 ? "The lexicon holds up." : $"{problems.Count} problems.");
            return problems.Count == 0 ? 0 : 1;
        }

        // The game's assembly, so that its shelves are found as they are in the game: from where it's asked, or where a release build of it would be beside this one. Whatever it needs that
        // isn't loaded already is looked for beside it.
        private static bool LoadGame(string? path)
        {
            string here = AppContext.BaseDirectory;
            path ??= Path.GetFullPath(Path.Combine(here, "..", "..", "..", "..", "Dino Defenders", "bin", "Release", "net8.0", "Dino Defenders.dll"));
            if (!File.Exists(path)) return false;
            string folder = Path.GetDirectoryName(path)!;
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                string wanted = Path.Combine(folder, new System.Reflection.AssemblyName(args.Name).Name + ".dll");
                return File.Exists(wanted) ? System.Reflection.Assembly.LoadFrom(wanted) : null;
            };
            System.Reflection.Assembly.LoadFrom(path);
            Console.WriteLine($"With the game's shelves, from {path}.");
            return true;
        }

        private static void CheckPlots(LexiconPlan plan, List<string> problems)
        {
            for (int i = 0; i < plan.Exhibits.Count; i++)
            {
                for (int j = i + 1; j < plan.Exhibits.Count; j++)
                {
                    GroundBox a = plan.Exhibits[i].Plot, b = plan.Exhibits[j].Plot;
                    float overlapX = MathF.Min(a.Max.X, b.Max.X) - MathF.Max(a.Min.X, b.Min.X);
                    float overlapZ = MathF.Min(a.Max.Y, b.Max.Y) - MathF.Max(a.Min.Y, b.Min.Y);
                    if (overlapX > 0.01f && overlapZ > 0.01f) problems.Add($"{plan.Exhibits[i].Exhibit.Key}'s plot is on {plan.Exhibits[j].Exhibit.Key}'s.");
                }
                if (!plan.Island.Contains(plan.Exhibits[i].Centre)) problems.Add($"{plan.Exhibits[i].Exhibit.Key} stands off the isle.");
            }
        }

        // Every place is a site of its kind where its plot says, with its builder's work standing in it.
        private static void CheckPlaces(WorldData world, LexiconPlan plan, List<string> problems)
        {
            foreach (PlacedExhibit exhibit in plan.Exhibits.Where(exhibit => exhibit.Exhibit.Kind == LexiconKind.Place))
            {
                WorldSite? site = world.Sites.FirstOrDefault(site => site.Kind == exhibit.Exhibit.Place && (site.Position - exhibit.Centre).Length < 1f);
                if (site == null)
                {
                    problems.Add($"{exhibit.Exhibit.Key} has no site.");
                    continue;
                }
                int built = world.Objects.All().Count(item => (item.Position.Xz - site.Position).Length < site.Radius * 1.4f && !world.Objects.AssetIds[item.Asset].Contains('#'));
                Console.WriteLine($"  {exhibit.Exhibit.Name,-16} {built,4} pieces, {site.Spawns.Count,3} spawns, at {site.Position.X:0},{site.Position.Y:0}, {site.Height:0.0} m up, in {world.Plan.Biomes[site.Biome].Name} {(exhibit.Diorama == Diorama.None ? "" : exhibit.Diorama.ToString())}");
                if (built < 3) problems.Add($"{exhibit.Exhibit.Key} has only {built} pieces standing.");

                // What makes a place with a story what it is, held to the same as in any land (PlaceCheck.Signatures).
                if (!PlaceCheck.Signatures.TryGetValue(site.Kind, out var signature)) continue;
                float reach = (site.Place?.Reach ?? site.Radius) + 12f;
                Vector2 middle = site.Place?.Centre ?? site.Position;
                var counted = new Dictionary<Dino_Engine.Modelling.Procedural.Architecture.Places.PlacePiece, int>();
                foreach (WorldObject item in world.Objects.All())
                {
                    string id = world.Objects.AssetIds[item.Asset];
                    if ((item.Position.Xz - middle).Length > reach || !Dino_Engine.Modelling.Procedural.Architecture.Places.PlaceSpec.TryParse(id, out var spec)) continue;
                    counted[spec.Kind] = counted.GetValueOrDefault(spec.Kind) + 1;
                }
                int has = signature.Pieces.Sum(piece => counted.GetValueOrDefault(piece));
                if (has < signature.Least) problems.Add($"{exhibit.Exhibit.Key} has {has} of {string.Join(" or ", signature.Pieces)}, and wants {signature.Least}.");
            }
        }

        private static void CheckSpecimens(WorldData world, LexiconPlan plan, List<string> problems)
        {
            foreach (PlacedExhibit exhibit in plan.Exhibits.Where(exhibit => exhibit.Exhibit.Kind == LexiconKind.Asset))
            {
                if (!NatureCatalog.TryGet(exhibit.Exhibit.Look, out WorldAsset asset))
                {
                    problems.Add($"{exhibit.Exhibit.Key} names no asset.");
                    continue;
                }
                string model = LexiconAssets.ModelOf(asset);
                GroundCards.Shape? card = GroundCards.ShapeOf(model);
                if (!world.Objects.TryNearest(model, exhibit.Centre, 0.5f, out WorldObject planted))
                    problems.Add(card != null ? $"{exhibit.Exhibit.Key} isn't laid: the ground under it is too uneven for a card to lie on." : $"{exhibit.Exhibit.Key} isn't planted.");
                // What lies on the ground lies on it as a land lays it, on the ground as it's drawn, rather than buried in it or standing off it.
                else if (card != null && (!GroundCards.TryLay(world, exhibit.Centre, exhibit.Exhibit.Yaw, exhibit.Exhibit.Scale, card, out Vector3 laid, out _) || (planted.Position - laid).Length > 0.01f))
                    problems.Add($"{exhibit.Exhibit.Key} doesn't lie on the ground as a land lays it.");
            }
        }

        // Level wherever anything small stands, and wherever a plaque is read.
        private static void CheckFloor(WorldData world, LexiconPlan plan, List<string> problems)
        {
            int off = 0;
            string? first = null;
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                if (exhibit.Exhibit.Kind == LexiconKind.Place) continue;
                foreach (Vector2 at in new[] { exhibit.Centre, exhibit.Plaque })
                {
                    float height = world.Height.Sample(at);
                    if (MathF.Abs(height - LexiconPlan.Ground) <= LevelTolerance) continue;
                    off++;
                    first ??= $"{exhibit.Exhibit.Key} at {at.X:0},{at.Y:0} is {height:0.00} m up";
                }
            }
            if (off > 0) problems.Add($"{off} spots that should be level aren't, the first {first}.");
        }

        // Every plaque can be walked to from the landing, which is where the index sends someone.
        // Whoever the index sends to an exhibit is stood where the game stands them (LexiconWorld.StandFor): for anything but a place, at its viewpoint itself, which has to be open
        // ground; for a place, near the middle of what it's built, on the nearest open ground. Never on another exhibit's plot. The landing has only to be open ground, as heroes arrive
        // there as anywhere.
        private static void CheckWalks(WorldData world, LexiconPlan plan, List<string> problems)
        {
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                Vector2 at = exhibit.Viewpoint;
                string key = exhibit.Exhibit.Key;
                if (LexiconWorld.StandFor(world, at) is not Vector2 stood)
                {
                    problems.Add($"{key}'s viewpoint at {at.X:0},{at.Y:0} has no open ground near it.");
                    continue;
                }
                if (exhibit.Exhibit.Kind == LexiconKind.Place) Console.WriteLine($"  {exhibit.Exhibit.Name,-16} is looked at from {stood.X:0},{stood.Y:0}");
                else if ((stood - at).Length > 0.01f) problems.Add($"{key}'s viewpoint at {at.X:0},{at.Y:0} isn't open ground: whoever's sent there is stood {(stood - at).Length:0.0} m off.");
                PlacedExhibit? on = plan.Exhibits.FirstOrDefault(other => other != exhibit && other.Plot.Contains(stood));
                if (on != null) problems.Add($"Whoever's sent to {key} is stood at {stood.X:0},{stood.Y:0}, on {on.Exhibit.Key}'s plot.");
            }
            if (!GameReach(world, plan.Arrival, near => true, out _)) problems.Add("The landing can't be walked from.");
        }

        // As the game looks for somewhere to put someone near a point (GameServer.TryFindGround): there, or the first allowed open ground on rings round it.
        private static bool GameReach(WorldData world, Vector2 near, Func<Vector2, bool> allowed, out Vector2 stood)
        {
            stood = near;
            if (world.Passable.IsSet(near) && allowed(near)) return true;
            for (float radius = 1.5f; radius <= 24f; radius += 1.5f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * (MathF.Tau / 16f);
                    stood = near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                    if (world.Passable.IsSet(stood) && allowed(stood)) return true;
                }
            }
            return false;
        }

        // Nothing stands on a walk or a forecourt, and nothing a place builds stands outside its plot: a field or a fence across a walk hides the plaques and bars the way, and what's
        // over a plot's edge is on the lawn between, or its neighbour's. Whatever's outside every plot is the nearest site's (the landing's own are left be).
        private static void CheckClear(WorldData world, LexiconPlan plan, List<string> problems)
        {
            var places = new Dictionary<WorldSite, PlacedExhibit>();
            foreach (PlacedExhibit exhibit in plan.Exhibits.Where(exhibit => exhibit.Exhibit.Kind == LexiconKind.Place))
            {
                WorldSite? site = world.Sites.FirstOrDefault(site => site.Kind == exhibit.Exhibit.Place && (site.Position - exhibit.Centre).Length < 1f);
                if (site != null) places[site] = exhibit;
            }
            var onWalks = new SortedDictionary<string, (int Count, Vector2 First)>(StringComparer.Ordinal);
            var astray = new Dictionary<PlacedExhibit, (int Count, float Farthest, string Id, Vector2 At)>();
            foreach (WorldObject item in world.Objects.All())
            {
                Vector2 at = item.Position.Xz;
                string id = world.Objects.AssetIds[item.Asset];
                if (plan.Walks.Any(box => box.Contains(at)) || plan.Forecourts.Any(box => box.Contains(at)))
                {
                    onWalks.TryGetValue(id, out var seen);
                    onWalks[id] = (seen.Count + 1, seen.Count == 0 ? at : seen.First);
                }
                if (plan.Exhibits.Any(exhibit => exhibit.Plot.Contains(at))) continue;
                WorldSite? nearest = world.Sites.MinBy(site => (site.Position - at).LengthSquared);
                if (nearest == null || !places.TryGetValue(nearest, out PlacedExhibit? home)) continue;
                float outside = home.Plot.Outside(at);
                astray.TryGetValue(home, out var far);
                astray[home] = outside > far.Farthest ? (far.Count + 1, outside, id, at) : (far.Count + 1, far.Farthest, far.Id, far.At);
            }
            foreach ((string id, var seen) in onWalks) problems.Add($"{seen.Count} {id} stand on a walk, the first at {seen.First.X:0},{seen.First.Y:0}.");
            foreach ((PlacedExhibit place, var far) in astray.OrderBy(pair => pair.Key.Index))
            {
                problems.Add($"{far.Count} things of {place.Exhibit.Key} stand outside its plot, as far as {far.Farthest:0.0} m out ({far.Id} at {far.At.X:0},{far.At.Y:0}).");
            }
        }

        // The plan as the game's camera sees it: +x to the left, north up.
        private static void Draw(WorldData world, LexiconPlan plan, string path, int resolution)
        {
            byte[] pixels = new byte[resolution * resolution * 4];
            float size = world.Size, scale = resolution / size;
            Vector2 ToPixel(Vector2 at) => new Vector2((size - at.X) * scale, (size - at.Y) * scale);

            Parallel.For(0, resolution, row =>
            {
                float z = size - (row + 0.5f) / scale;
                for (int column = 0; column < resolution; column++)
                {
                    float x = size - (column + 0.5f) / scale;
                    float h = world.Height.Sample(x, z);
                    Vector3 colour;
                    if (h <= world.WaterLevelAt(new Vector2(x, z))) colour = new Vector3(0.16f, 0.36f, 0.5f);
                    else
                    {
                        colour = Vector3.Zero;
                        for (int b = 0; b < world.Plan.Biomes.Count; b++) colour += world.Plan.Biomes[b].MapColour * world.BiomeWeights[b].Sample(x, z);
                        if (world.Paving.Sample(x, z) > 0.5f) colour = new Vector3(0.72f, 0.7f, 0.64f);
                        else if (world.Plaza.Sample(x, z) > 0.5f) colour = new Vector3(0.6f, 0.55f, 0.45f);
                        else if (world.Garden.Sample(x, z) > 0.5f) colour = Vector3.Lerp(colour, new Vector3(0.3f, 0.55f, 0.25f), 0.5f);
                        if (world.Rock.Sample(x, z) > 0.5f) colour = new Vector3(0.45f, 0.44f, 0.43f);
                        colour *= 0.82f + 0.025f * (h - LexiconPlan.Ground);
                    }
                    Set(pixels, resolution, column, row, colour);
                }
            });

            foreach (WorldObject item in world.Objects.All())
            {
                Vector2 at = ToPixel(item.Position.Xz);
                bool plant = world.Objects.AssetIds[item.Asset].Contains('#');
                Dot(pixels, resolution, at, plant ? new Vector3(0.1f, 0.3f, 0.08f) : new Vector3(0.3f, 0.2f, 0.16f), plant ? 1 : 0);
            }

            Vector3[] colours = { new(0.95f, 0.75f, 0.2f), new(0.9f, 0.35f, 0.3f), new(0.35f, 0.65f, 0.95f), new(0.75f, 0.45f, 0.95f), new(0.3f, 0.85f, 0.6f), new(0.95f, 0.55f, 0.75f), new(0.6f, 0.9f, 0.3f) };
            foreach (PlacedExhibit exhibit in plan.Exhibits)
            {
                Vector3 colour = colours[exhibit.Section.Index % colours.Length];
                if (exhibit.Exhibit.Kind == LexiconKind.Missing) colour *= 0.5f;
                Outline(pixels, resolution, ToPixel(exhibit.Plot.Max), ToPixel(exhibit.Plot.Min), colour);
                Dot(pixels, resolution, ToPixel(exhibit.Plaque), new Vector3(1f), 0);
            }
            foreach (PlacedFamily family in plan.Families) Dot(pixels, resolution, ToPixel(family.Sign), new Vector3(1f, 0.85f, 0.3f), 2);
            foreach (PlacedSection section in plan.Sections) Dot(pixels, resolution, ToPixel(section.Sign), new Vector3(1f, 0.3f, 0.2f), 4);
            Dot(pixels, resolution, ToPixel(plan.Arrival), new Vector3(0.3f, 0.9f, 1f), 5);

            PngWriter.Write(path, pixels, resolution, resolution);
            Console.WriteLine($"Wrote {path}");
        }

        private static void Set(byte[] pixels, int resolution, int column, int row, Vector3 colour)
        {
            if (column < 0 || row < 0 || column >= resolution || row >= resolution) return;
            int i = (row * resolution + column) * 4;
            pixels[i] = (byte)Math.Clamp(colour.X * 255f, 0f, 255f);
            pixels[i + 1] = (byte)Math.Clamp(colour.Y * 255f, 0f, 255f);
            pixels[i + 2] = (byte)Math.Clamp(colour.Z * 255f, 0f, 255f);
            pixels[i + 3] = 255;
        }

        private static void Dot(byte[] pixels, int resolution, Vector2 at, Vector3 colour, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++) Set(pixels, resolution, (int)at.X + dx, (int)at.Y + dy, colour);
            }
        }

        private static void Outline(byte[] pixels, int resolution, Vector2 a, Vector2 b, Vector3 colour)
        {
            int x0 = (int)MathF.Min(a.X, b.X), x1 = (int)MathF.Max(a.X, b.X), y0 = (int)MathF.Min(a.Y, b.Y), y1 = (int)MathF.Max(a.Y, b.Y);
            for (int x = x0; x <= x1; x++)
            {
                Set(pixels, resolution, x, y0, colour);
                Set(pixels, resolution, x, y1, colour);
            }
            for (int y = y0; y <= y1; y++)
            {
                Set(pixels, resolution, x0, y, colour);
                Set(pixels, resolution, x1, y, colour);
            }
        }
    }
}
