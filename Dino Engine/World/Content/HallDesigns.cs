using OpenTK.Mathematics;

namespace Dino_Engine.World.Content
{
    /// <summary>
    /// The plan of every kind of hall: where its doors and windows are, what stands in it and where, all in its own frame, whose front (+z) is where its door is and whose +x is on the right
    /// of whoever faces it from outside. The Haven turns them to face the camera, so +x is the west, where the evening sun comes in, and so is the back. Read HALLS.md.
    /// A person is about 4.7 m tall and 1.1 m from their middle to their side, so a door is 6 m high and about 5 wide, and nothing leaves less than 2.5 m to walk between.
    /// Nothing tall stands within <see cref="HallDesign.Wall"/> and 0.6 m of a wall facing the camera, as that wall comes down to <see cref="HallDesign.FrontCut"/> when someone's inside.
    /// </summary>
    public static class HallDesigns
    {
        private static readonly Dictionary<HallKind, HallDesign> ByKind = new()
        {
            [HallKind.Forge] = Forge(),
            [HallKind.GamingHouse] = GamingHouse(),
            [HallKind.Archive] = Archive(),
        };

        public static HallDesign Of(HallKind kind) => ByKind[kind];

        public static IEnumerable<HallDesign> All => ByKind.Values;

        /// <summary>The hall an asset id is the shell or the inside of, if it's one.</summary>
        public static bool TryParseAsset(string id, out HallDesign design, out bool inside)
        {
            foreach (HallDesign each in ByKind.Values)
            {
                if (id == each.ShellAsset || id == each.InsideAsset)
                {
                    design = each;
                    inside = id == each.InsideAsset;
                    return true;
                }
            }
            design = null!;
            inside = false;
            return false;
        }

        private const float Quarter = MathF.PI * 0.5f;

        // A smithy, all stone: the forge at the back under its hood, the anvil before it, the crafting bench along the east wall under a window, and what's being worked on round them.
        private static HallDesign Forge()
        {
            var d = new HallDesign
            {
                Kind = HallKind.Forge,
                Name = "The Forge",
                Half = new Vector2(15f, 11f),
                Wall = 0.9f,
                WallTop = 9f,
                Top = 24f,
                FrontCut = 1.8f,
                Eaves = 2.6f,
                FrontReach = 1.4f,
                Floor = SurfaceMaterial.Cobble,
            };
            d.Door(HallSide.Front, 0f, 2.5f, 7.4f, arched: true)
             .Window(HallSide.Front, -8.5f, 1.3f, 2.6f, 6.2f).Window(HallSide.Front, 8.5f, 1.3f, 2.6f, 6.2f)
             .Window(HallSide.Back, -12f, 1.3f, 2.8f, 6.4f).Window(HallSide.Back, 9.5f, 1.3f, 2.8f, 6.4f)
             .Window(HallSide.PlusX, -4.5f, 1.4f, 2.6f, 6.6f).Window(HallSide.PlusX, 4f, 1.4f, 2.6f, 6.6f)
             .Window(HallSide.MinusX, 0.6f, 1.5f, 3.1f, 6.6f).Window(HallSide.MinusX, -7.4f, 1.1f, 3f, 6.2f);

            // The back wall: coals, bellows, and what's used at the fire.
            d.Piece(PieceKind.Hearth, 0f, -8.2f, 3.6f, 1.9f, 2.4f)
             .Piece(PieceKind.Bellows, 5.3f, -8.6f, 1.1f, 1.5f, 2.2f)
             .Piece(PieceKind.CoalBin, -4.9f, -9f, 1.1f, 1.1f, 1.3f)
             .Piece(PieceKind.ToolRack, -8.6f, -9.75f, 2f, 0.35f, 5.2f)
             .Piece(PieceKind.Anvil, 0.4f, -3.4f, 1.1f, 0.55f, 1.9f)
             .Piece(PieceKind.Trough, -4.4f, -4.4f, 1.7f, 0.75f, 1.3f);

            // The east wall: arms on the rack, the crafting bench under its window, stock on shelves.
            d.Piece(PieceKind.WeaponRack, -13.55f, -6.6f, 1.9f, 0.45f, 4.4f, yaw: Quarter)
             .Piece(PieceKind.Workbench, -12.5f, 0.6f, 3.8f, 1.4f, 1.95f, yaw: Quarter)
             .Piece(PieceKind.Shelves, -13.55f, 7f, 1.8f, 0.45f, 4.6f, yaw: Quarter)
             .Round(PieceKind.ArmourStand, -9.8f, 7.4f, 0.9f, 4.7f);

            // The west wall, in the sun: the grindstone, the leather table, wood and stock.
            d.Piece(PieceKind.Grindstone, 10.2f, -3.6f, 1.5f, 0.9f, 2.6f)
             .Piece(PieceKind.LeatherTable, 11.6f, 3.8f, 2.8f, 1.3f, 1.95f, yaw: -Quarter)
             .Piece(PieceKind.Firewood, 13.4f, -8f, 2f, 0.7f, 2.4f, yaw: -Quarter)
             .Round(PieceKind.Casks, 9.6f, -8.8f, 1.2f, 2.1f)
             .Piece(PieceKind.Crates, 12.6f, 8f, 1.3f, 1.2f, 2.4f);

            // A lean-to against the east wall, with the wood for the forge stacked dry under it.
            d.Against(PieceKind.LeanTo, -15.75f, -1.5f, 0.75f, 6f, 3f);
            foreach (float z in new[] { -7.6f, -1.5f, 4.6f }) d.Against(PieceKind.Post, -18.6f, z, 0.25f, 0.25f, 0f);

            d.Spot(SpawnRole.Workbench, -9.3f, 0.6f)
             .Spot(SpawnRole.Keeper, 3f, -3f);
            d.Visit(-6.5f, 2.5f).Visit(6.5f, 1f).Visit(2f, 6.5f).Visit(-3f, 7f).Visit(7f, -6f);
            return d;
        }

        // A gaming house, warm wood and plaster: the great table in the middle with its dealer, the bar along the back, the fire on the east wall with chairs before it, dice and cards on the
        // sunny west side, and a little stage for music by the door.
        private static HallDesign GamingHouse()
        {
            var d = new HallDesign
            {
                Kind = HallKind.GamingHouse,
                Name = "The Gaming House",
                Half = new Vector2(15f, 11f),
                Wall = 0.9f,
                WallTop = 9f,
                Top = 23f,
                FrontCut = 1.8f,
                Eaves = 2.8f,
                FrontReach = 3.6f,
                Floor = SurfaceMaterial.Planks,
            };
            d.Door(HallSide.Front, 0f, 2.4f, 6.4f)
             .Window(HallSide.Front, -8f, 1.4f, 2.4f, 6.4f).Window(HallSide.Front, 8f, 1.4f, 2.4f, 6.4f)
             .Window(HallSide.Back, -10.5f, 1.2f, 2.6f, 6.6f).Window(HallSide.Back, 10.5f, 1.2f, 2.6f, 6.6f)
             .Window(HallSide.PlusX, -5.5f, 1.5f, 2.4f, 6.8f).Window(HallSide.PlusX, 2.5f, 1.5f, 2.4f, 6.8f)
             .Window(HallSide.MinusX, -7.6f, 1.2f, 2.6f, 6.4f).Window(HallSide.MinusX, 5.8f, 1.2f, 2.6f, 6.4f);

            d.Piece(PieceKind.GamingTable, 0f, 1.6f, 3.9f, 2.4f, 1.95f)
             .Round(PieceKind.Chair, -5f, 1.6f, 0.65f, 2.4f, variant: 0, yaw: Quarter)
             .Round(PieceKind.Chair, 5f, 1.6f, 0.65f, 2.4f, variant: 1, yaw: -Quarter)
             .Round(PieceKind.Chair, -2.4f, 4.9f, 0.65f, 2.4f, variant: 2, yaw: MathF.PI)
             .Round(PieceKind.Chair, 2.4f, 4.9f, 0.65f, 2.4f, variant: 3, yaw: MathF.PI);

            d.Piece(PieceKind.Bar, 0f, -5f, 5.2f, 0.75f, 2.3f)
             .Piece(PieceKind.BarShelves, 0f, -9.7f, 5.6f, 0.4f, 5.2f)
             .Round(PieceKind.Casks, -10.2f, -8.6f, 1.2f, 2.1f)
             .Round(PieceKind.Casks, 10.2f, -8.6f, 1.2f, 2.1f, variant: 1);

            d.Piece(PieceKind.Fireplace, -13.3f, -1f, 2.6f, 0.8f, 3.4f, yaw: Quarter)
             .Piece(PieceKind.Rug, -9.8f, -1f, 3.6f, 2.6f, 0.02f, solid: false, yaw: Quarter)
             .Round(PieceKind.Armchair, -10.2f, -4f, 0.95f, 2.6f, variant: 0, yaw: -Quarter - 0.5f)
             .Round(PieceKind.Armchair, -10.2f, 2f, 0.95f, 2.6f, variant: 1, yaw: -Quarter + 0.5f)
             .Round(PieceKind.RoundTable, -10.6f, -1f, 0.6f, 1.6f, variant: 2);

            // Dice and cards in the sun along the west wall, each table with its stools.
            foreach ((float z, int variant) in new[] { (-4.6f, 0), (2.8f, 1) })
            {
                d.Round(PieceKind.RoundTable, 9.6f, z, 1.4f, 1.95f, variant: variant);
                foreach (float angle in new[] { 3.3f, 1.9f, 0.7f })
                {
                    d.Round(PieceKind.Stool, 9.6f + MathF.Cos(angle) * 2.5f, z + MathF.Sin(angle) * 2.5f, 0.5f, 1.25f, variant: variant);
                }
            }

            d.Piece(PieceKind.Stage, -11.3f, 7.4f, 2.6f, 1.8f, 0.5f)
             .Round(PieceKind.Planter, -13f, -9f, 0.8f, 3f)
             .Round(PieceKind.Planter, 13f, -9f, 0.8f, 3f, variant: 1)
             .Round(PieceKind.Planter, 13.2f, 8.2f, 0.7f, 2.6f, variant: 2);

            // The porch over the door on its two posts, and the chimney of the fire built out from the east wall.
            d.Against(PieceKind.Post, -3.1f, 14.1f, 0.3f, 0.3f, 0f).Against(PieceKind.Post, 3.1f, 14.1f, 0.3f, 0.3f, 0f)
             .Against(PieceKind.ChimneyBreast, -15.6f, -1f, 0.6f, 1.9f, PlacementIndex.Tall);

            d.Spot(SpawnRole.GamingTable, 0f, 5.3f)
             .Spot(SpawnRole.Keeper, 0f, -2.2f)
             .Spot(SpawnRole.Keeper, 0f, -7.6f);
            d.Visit(7.4f, -0.8f).Visit(-7.6f, -1f).Visit(3.5f, 7.6f).Visit(-6f, 5.6f).Visit(6.8f, -7f).Visit(12.4f, 6f);
            return d;
        }

        // The Archive, the grandest of them: a long hall of stone and timber with tall windows, a carpet up the middle to the tyrant's skull at the far end, the records of every hero along
        // either side, each a display of its own, and the archive itself on shelves along the east wall, with whoever keeps it at a desk among them.
        private static HallDesign Archive()
        {
            var d = new HallDesign
            {
                Kind = HallKind.Archive,
                Name = "The Archive",
                Half = new Vector2(22f, 14f),
                Wall = 1f,
                WallTop = 10.5f,
                Top = 32.5f,
                FrontCut = 1.8f,
                Eaves = 2.8f,
                FrontReach = 2.2f,
                Floor = SurfaceMaterial.Planks,
            };
            d.Door(HallSide.Front, 0f, 2.8f, 8.2f, arched: true)
             .Window(HallSide.Front, -9.5f, 1.4f, 2.8f, 7.6f, arched: true).Window(HallSide.Front, 9.5f, 1.4f, 2.8f, 7.6f, arched: true)
             .Window(HallSide.Front, -16.5f, 1.4f, 2.8f, 7.6f, arched: true).Window(HallSide.Front, 16.5f, 1.4f, 2.8f, 7.6f, arched: true)
             .Window(HallSide.Back, -6.5f, 1.5f, 2.8f, 8.6f, arched: true).Window(HallSide.Back, 6.5f, 1.5f, 2.8f, 8.6f, arched: true)
             .Window(HallSide.Back, -15.5f, 1.5f, 2.8f, 8.6f, arched: true).Window(HallSide.Back, 15.5f, 1.5f, 2.8f, 8.6f, arched: true)
             .Window(HallSide.PlusX, -7.5f, 1.6f, 2.6f, 8.6f, arched: true).Window(HallSide.PlusX, 0f, 1.6f, 2.6f, 8.6f, arched: true).Window(HallSide.PlusX, 7.5f, 1.6f, 2.6f, 8.6f, arched: true)
             .Window(HallSide.MinusX, -6f, 1.6f, 6.6f, 9.4f, arched: true).Window(HallSide.MinusX, 6f, 1.6f, 6.6f, 9.4f, arched: true);

            // The records, each on a display of its own, facing the carpet up the middle, and the greatest of them at the far end.
            d.Exhibit(ExhibitKind.TyrantsFelled, 0f, -8.8f, 0f)
             .Exhibit(ExhibitKind.FoesSlain, -9.5f, 5.5f, Quarter)
             .Exhibit(ExhibitKind.HardestBlow, 9.5f, 5.5f, -Quarter)
             .Exhibit(ExhibitKind.UniquesFound, -9.5f, -1.5f, Quarter)
             .Exhibit(ExhibitKind.SwiftestLand, 9.5f, -1.5f, -Quarter)
             .Exhibit(ExhibitKind.Falls, -9.5f, -8.5f, Quarter)
             .Exhibit(ExhibitKind.LandsFreed, 9.5f, -8.5f, -Quarter)
             .Exhibit(ExhibitKind.GoldGathered, 18.7f, -3.75f, -Quarter)
             .Exhibit(ExhibitKind.DeepestLand, 18.7f, 3.75f, -Quarter);
            foreach (HallExhibit exhibit in d.Exhibits)
            {
                (Vector2 half, float height) = exhibit.Kind switch
                {
                    ExhibitKind.TyrantsFelled => (new Vector2(3.4f, 2.4f), 5.4f),
                    ExhibitKind.FoesSlain => (new Vector2(2f, 2f), 6f),
                    ExhibitKind.HardestBlow => (new Vector2(2f, 1.6f), 4.4f),
                    ExhibitKind.UniquesFound => (new Vector2(2.4f, 1f), 4.8f),
                    ExhibitKind.SwiftestLand => (new Vector2(1.6f, 1.6f), 5.2f),
                    ExhibitKind.Falls => (new Vector2(2f, 1.4f), 3.4f),
                    ExhibitKind.LandsFreed => (new Vector2(2.2f, 1.6f), 3.4f),
                    ExhibitKind.GoldGathered => (new Vector2(1.7f, 1f), 2.2f),
                    _ => (new Vector2(1.7f, 1f), 4.8f),
                };
                d.Piece(PieceKind.Plinth, exhibit.At.X, exhibit.At.Y, half.X, half.Y, height, yaw: exhibit.Yaw, variant: (int)exhibit.Kind);
            }

            // The archive: shelves along the east wall under its high windows, and two more on the back wall between the windows.
            foreach (float z in new[] { -10.6f, -5f, 0.6f, 6.2f }) d.Piece(PieceKind.Bookcase, -20.4f, z, 2f, 0.55f, 5.8f, yaw: Quarter);
            d.Piece(PieceKind.Bookcase, -11f, -12.45f, 2.6f, 0.55f, 5.8f, variant: 1)
             .Piece(PieceKind.Bookcase, 11f, -12.45f, 2.6f, 0.55f, 5.8f, variant: 2)
             .Piece(PieceKind.ReadingDesk, -15.6f, -9.4f, 1.6f, 1f, 2f)
             .Piece(PieceKind.Lectern, -15f, 6f, 0.7f, 0.6f, 2.6f, yaw: Quarter)
             .Round(PieceKind.Globe, -14.6f, 0.6f, 0.9f, 3.2f);

            // Candles up either side of the carpet, and benches by the door to sit and look.
            foreach (float z in new[] { 9f, 2f, -5f })
            {
                d.Round(PieceKind.CandleStand, -4.2f, z, 0.45f, 4.6f).Round(PieceKind.CandleStand, 4.2f, z, 0.45f, 4.6f);
            }
            d.Piece(PieceKind.Bench, -6f, 10.4f, 1.9f, 0.5f, 1.2f, yaw: MathF.PI).Piece(PieceKind.Bench, 6f, 10.4f, 1.9f, 0.5f, 1.2f, yaw: MathF.PI)
             .Piece(PieceKind.Rug, 0f, 3.2f, 2.4f, 9.4f, 0.02f, solid: false);

            // Buttresses between the windows, all round.
            foreach (float x in new[] { -11f, 0f, 11f }) d.Against(PieceKind.Buttress, x, -14.45f, 0.65f, 0.45f, PlacementIndex.Tall);
            foreach (float x in new[] { -13f, -5.6f, 5.6f, 13f }) d.Against(PieceKind.Buttress, x, 14.45f, 0.65f, 0.45f, PlacementIndex.Tall);
            foreach (float z in new[] { -11.3f, -3.75f, 3.75f, 11.3f }) d.Against(PieceKind.Buttress, 22.45f, z, 0.45f, 0.65f, PlacementIndex.Tall);
            foreach (float z in new[] { -11f, 0f, 11f }) d.Against(PieceKind.Buttress, -22.45f, z, 0.45f, 0.65f, PlacementIndex.Tall);

            d.Spot(SpawnRole.Keeper, -15.6f, -7f);
            d.Visit(6.5f, 2f).Visit(-6.5f, 2f).Visit(6.6f, -5f).Visit(-6.6f, -5f).Visit(0f, 6f).Visit(14f, 0f).Visit(-14f, -3f).Visit(15f, 9f);
            return d;
        }
    }
}
