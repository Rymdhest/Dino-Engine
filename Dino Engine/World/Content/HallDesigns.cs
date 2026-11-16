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
        /// <summary>Metres from the Archive's middle to its two rows of columns, either side of its nave.</summary>
        public const float ArchiveArcade = 7f;

        /// <summary>Where its columns stand along the nave, from the door towards the dais. Before the designs, which are made as the class is and read it.</summary>
        public static readonly float[] ArchiveBays = { 8.4f, 3f, -2.4f };

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

        // The Forge: everything a Defender wears or carries is made here, in metal, leather and cloth, under one roof, and every corner of it is in the middle of something.
        // The fire is at the back in the middle, open to the room under a high copper hood, with its great bellows beside it, and straight down the room from it are the anvil,
        // whoever keeps the Forge, and the master's bench, which is the crafting bench and faces the door. The side on the left of whoever comes in (-x) is the hot side: the
        // smelting furnace in its far corner with charcoal and ore beside it, metal cooling in its moulds along the wall, the quench trough, the grindstone, and by the door
        // what's been mended, the shelf of what nobody came back for, and what's waiting to be. The side on the right is the soft side: dye and tanning vats along the back
        // under the cloth hung to dry, a hide in its frame and one on the beam, the leather table under its window, and by the door the loom, the tailor's table and the cloth.
        // The floor is flagged where it's hot, boarded where cloth is cut, and cobbled between. Everything stands against a wall or on the line from the door to the fire, so
        // there's a way round the room on either side. How it all looks is Smithy.Metal.cs, Smithy.Soft.cs and Smithy.Works.cs.
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
            // A window to each bench that needs the light: the moulds and the grindstone on the left, the hide's frame, the leather table and the loom on the right, and one
            // either side of the fire at the back, over the stock and behind the cloth that's drying.
            d.Door(HallSide.Front, 0f, 2.5f, 7.4f, arched: true)
             .Window(HallSide.Front, -8.5f, 1.3f, 2.6f, 6.2f).Window(HallSide.Front, 8.5f, 1.3f, 2.6f, 6.2f)
             .Window(HallSide.Back, -6.9f, 1.3f, 2.8f, 6.4f).Window(HallSide.Back, 10.9f, 1.5f, 2.8f, 6.4f)
             .Window(HallSide.PlusX, -5.1f, 1.3f, 1.4f, 6.6f).Window(HallSide.PlusX, -0.3f, 1.3f, 1.4f, 6.6f).Window(HallSide.PlusX, 7.2f, 1.3f, 1.4f, 6.6f)
             .Window(HallSide.MinusX, -2.2f, 1.5f, 1.5f, 6.6f).Window(HallSide.MinusX, 2.4f, 1f, 1.5f, 6.2f);

            // The line from the door to the fire: the master's bench, the anvil, and the hearth, which is part of the building, with its bellows.
            d.Piece(PieceKind.Hearth, 0f, -7.7f, 3.6f, 2.4f, 2.4f)
             .Piece(PieceKind.Bellows, 5.7f, -8.5f, 1.5f, 1.4f, 2.6f)
             .Round(PieceKind.Anvil, 0f, -3.3f, 0.95f, 1.95f)
             .Piece(PieceKind.Workbench, 0f, 2.3f, 2.9f, 1f, 1.95f);

            // The hot side: winning iron, pouring it, quenching it, grinding it, and what's finished.
            d.Round(PieceKind.Smelter, -11.5f, -7.6f, 1.5f, 4.6f)
             .Piece(PieceKind.StockBins, -6.5f, -9.2f, 1.9f, 0.85f, 1.6f)
             .Piece(PieceKind.CastingBed, -12.5f, -2.2f, 1.5f, 2f, 0.9f)
             .Piece(PieceKind.Trough, -5f, -4.4f, 1.6f, 0.7f, 1.3f)
             .Piece(PieceKind.Grindstone, -13.1f, 2.4f, 1.5f, 0.9f, 2.6f, yaw: Quarter)
             .Piece(PieceKind.Uncollected, -13.6f, 6.9f, 2.1f, 0.45f, 4.6f, yaw: Quarter)
             .Round(PieceKind.ArmourStand, -10.6f, 8.5f, 0.9f, 4.7f, variant: 0)
             .Round(PieceKind.ArmourStand, -8.3f, 8.8f, 0.9f, 4.7f, variant: 1)
             .Piece(PieceKind.Crates, -5.2f, 8.9f, 1.2f, 0.8f, 1.7f);

            // And in the middle of the floor on that side, where it has to be walked round, what the whole Forge is building: an engine for the Haven's wall.
            d.Piece(PieceKind.Ballista, -7.4f, 4.5f, 3.05f, 1.9f, 2.9f, yaw: Quarter);

            // The soft side: dyeing and tanning along the back, leather under the windows, cloth by the door.
            d.Round(PieceKind.Cauldron, 8.3f, -8.8f, 0.75f, 1.9f)
             .Round(PieceKind.Vat, 10.3f, -8.5f, 1f, 1.5f, variant: 0)
             .Round(PieceKind.Vat, 12.8f, -8.4f, 1f, 1.5f, variant: 1)
             .Piece(PieceKind.HideFrame, 13.6f, -4.9f, 1.8f, 0.35f, 4.6f, yaw: -Quarter)
             .Piece(PieceKind.FleshingBeam, 10.9f, -5.2f, 0.55f, 1.3f, 1.7f)
             .Piece(PieceKind.LeatherTable, 12.7f, -0.2f, 2.5f, 1.1f, 1.95f, yaw: -Quarter)
             .Round(PieceKind.SaddleStand, 12.9f, 3.7f, 0.8f, 2.4f)
             .Piece(PieceKind.Loom, 12.9f, 7.3f, 1.9f, 1.15f, 4.4f, yaw: -Quarter)
             .Piece(PieceKind.TailorTable, 8f, 7f, 1.9f, 1f, 1.95f)
             .Round(PieceKind.DressForm, 5.2f, 8.7f, 0.6f, 4.4f)
             .Piece(PieceKind.ClothRack, 8f, 9.45f, 1.9f, 0.45f, 1.7f)
             .Round(PieceKind.SpinningWheel, 10.6f, 4.6f, 0.7f, 2.6f);

            // A lean-to against the east wall, with the wood for the forge stacked dry under it.
            d.Against(PieceKind.LeanTo, -15.75f, -1.5f, 0.75f, 6f, 3f);
            foreach (float z in new[] { -7.6f, -1.5f, 4.6f }) d.Against(PieceKind.Post, -18.6f, z, 0.25f, 0.25f, 0f);

            // The bench is used from the door's side of it. Whoever keeps the Forge stands behind it, between it and the anvil, and two more of the Haven work here: at the
            // leather and at the cloth. A keeper stands facing the middle of the room, so each is put where that's towards whoever comes in.
            d.Spot(SpawnRole.Workbench, 0f, 4.6f)
             .Spot(SpawnRole.Keeper, 0f, -0.5f)
             .Spot(SpawnRole.Keeper, 9.9f, -0.2f)
             .Spot(SpawnRole.Keeper, 6.4f, 4.4f);
            d.Visit(-3.4f, 5.4f).Visit(3.6f, 5.6f).Visit(-7.6f, -0.8f).Visit(-8.6f, -4.6f).Visit(6.4f, -3.4f).Visit(4.6f, 1.2f);
            return d;
        }

        // The Gaming House, an old barn turned to games of chance, and laid out as a barn is: the great doors in the middle of its long side open onto the threshing floor, flagged in
        // stone, which runs straight through between four great posts, and either side of it is a bay floored in boards. The great table stands in the middle of the threshing floor
        // under a cart wheel of candles, its dealer behind it and the bar behind them both, against the back wall. The bay on the left of whoever comes in (-x) is for sitting: the
        // hearth, a settle and a chair before it, the hayloft in its far corner over the tuns, with a ladder up, and a stage for music by the door. The bay on the right keeps its
        // stalls, which are booths now, each with a table under its window, and the wheel of fortune hangs at its far end. Everything stands at the edges or between the posts, so the
        // threshing floor and both aisles beside it are clear to walk: nothing leaves a way that looks open and isn't. How it all looks is GamingHouse.cs.
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
            // A window to each booth, one by the stage, one behind the far booth, and two small ones high up that light the hayloft.
            d.Door(HallSide.Front, 0f, 2.4f, 6.4f)
             .Window(HallSide.Front, -8f, 1.4f, 2.4f, 6.4f).Window(HallSide.Front, 8f, 1.4f, 2.4f, 6.4f)
             .Window(HallSide.Back, -10f, 1.3f, 6.2f, 8.1f).Window(HallSide.Back, 11.3f, 1.2f, 2.6f, 6.6f)
             .Window(HallSide.PlusX, -6.7f, 1.5f, 1.5f, 6.8f).Window(HallSide.PlusX, 0f, 1.5f, 1.5f, 6.8f).Window(HallSide.PlusX, 6.7f, 1.5f, 1.5f, 6.8f)
             .Window(HallSide.MinusX, -7.6f, 1.1f, 6.2f, 8.1f).Window(HallSide.MinusX, 6.4f, 1.4f, 1.2f, 6.4f);

            // The frame: four posts, which the tie beams cross the barn on. The bays are theirs.
            int post = 0;
            foreach (float x in new[] { -GamingBay, GamingBay })
            {
                foreach (float z in new[] { -GamingPostDepth, GamingPostDepth }) d.Round(PieceKind.BarnPost, x, z, 0.4f, 8.4f, variant: post++);
            }

            // The threshing floor: the great table, a seat either side of where a hero stands at it, and the bar across the back.
            d.Piece(PieceKind.GamingTable, 0f, 0.6f, 3.1f, 1.9f, 1.95f)
             .Round(PieceKind.Chair, -2.7f, 2.75f, 0.6f, 2.5f, variant: 0, yaw: 2.53f)
             .Round(PieceKind.Chair, 2.7f, 2.75f, 0.6f, 2.5f, variant: 1, yaw: -2.53f)
             .Piece(PieceKind.Rug, 0f, 0.6f, 4.7f, 3.5f, 0.02f, solid: false, variant: 2)
             .Piece(PieceKind.Rug, 0f, 7f, 1.5f, 2.5f, 0.02f, solid: false, variant: 3)
             .Piece(PieceKind.Bar, 0f, -5.5f, 4.6f, 0.5f, 2.3f)
             .Piece(PieceKind.BarShelves, 0f, -9.72f, 4.9f, 0.38f, 5.6f);

            // The left bay: the hearth on its wall, a settle with its back to the tuns under the loft, a chair and a barrel to play draughts on, the ladder, and the stage.
            d.Piece(PieceKind.Fireplace, -13.35f, -0.2f, 2.7f, 0.75f, 4f, yaw: Quarter)
             .Piece(PieceKind.HideRug, -10.4f, -0.9f, 1.8f, 1.4f, 0.02f, solid: false, yaw: 0.35f)
             .Piece(PieceKind.Settle, -10.7f, -4.6f, 1.9f, 0.5f, 3.3f)
             .Round(PieceKind.Armchair, -9.3f, 2.2f, 0.9f, 2.6f, yaw: -2.18f)
             .Round(PieceKind.RoundTable, -11f, 3.4f, 0.65f, 1.5f, variant: 2)
             .Piece(PieceKind.LoftStore, -10.05f, -7.7f, 4.05f, 2.4f, 5f)
             .Piece(PieceKind.Ladder, -7.3f, -4.1f, 0.6f, 0.8f, 5.2f)
             .Piece(PieceKind.Stage, -11.3f, 7.3f, 2.5f, 2.1f, 0.5f)
             .Round(PieceKind.Casks, -7f, 8.6f, 1.15f, 2.1f);

            // The right bay: two stall walls make three booths, each a bench along its far side and one under its window, round a table.
            foreach ((float z, int variant) in new[] { (-3.35f, 0), (3.35f, 1) }) d.Piece(PieceKind.StallWall, 12f, z, 2.1f, 0.17f, 3.3f, variant: variant);
            int booth = 0;
            foreach ((float far, float middle) in new[] { (-9.6f, -7f), (-2.68f, -0.1f), (4.02f, 6.6f) })
            {
                d.Piece(PieceKind.BoothBench, 11.9f, far, 1.8f, 0.5f, 1.2f, variant: booth)
                 .Piece(PieceKind.BoothBench, 13.55f, middle, 1.9f, 0.5f, 1.2f, yaw: -Quarter, variant: booth + 3)
                 .Round(PieceKind.RoundTable, 11.3f, middle, 1.15f, 1.95f, variant: booth == 2 ? 3 : booth);
                booth++;
            }

            // By the door: the house's rules, where blades are left, and the hay that's still kept here.
            d.Round(PieceKind.Easel, -4.3f, 8.5f, 0.6f, 3.2f)
             .Round(PieceKind.BladeBarrel, 4.3f, 8.6f, 0.6f, 1.7f)
             .Piece(PieceKind.HayBales, 7.9f, 8.4f, 1.4f, 1.1f, 2.3f);

            // The porch over the door on its two posts, and the chimney of the fire built out from the east wall.
            d.Against(PieceKind.Post, -3.1f, 14.1f, 0.3f, 0.3f, 0f).Against(PieceKind.Post, 3.1f, 14.1f, 0.3f, 0.3f, 0f)
             .Against(PieceKind.ChimneyBreast, -15.6f, -0.2f, 0.6f, 1.9f, PlacementIndex.Tall);

            // The table is used from in front of it, its dealer stands behind it, and whoever pours stands between the bar and the casks.
            d.Spot(SpawnRole.GamingTable, 0f, 3.8f)
             .Spot(SpawnRole.Keeper, 0f, -2.75f)
             .Spot(SpawnRole.Keeper, 0f, -7.7f);
            d.Visit(-4.6f, 3.4f).Visit(4.6f, 3.4f).Visit(8f, -7f).Visit(-8f, 0.4f).Visit(3.4f, -3.6f).Visit(8.2f, 2f);
            return d;
        }

        /// <summary>Metres from the Gaming House's middle to the lines its posts and tie beams stand on, either side of its threshing floor.</summary>
        public const float GamingBay = 6f;

        /// <summary>And from its middle to its posts, front and back.</summary>
        public const float GamingPostDepth = 5f;

        // The Archive, the grandest of them, and laid out as a great church is: a nave from the door between two rows of marble columns to a dais under the end wall, where the
        // tyrant's skull stands as an altar would, and an aisle either side. Every other record has a place of its own against a wall with the floor open before it: two in
        // each aisle against the end wall, facing down the hall, and two against each side wall, between its windows, facing in. So nothing stands in front of anything, as the
        // camera looks down from the south, and the two sides are a pair. What faces the camera is against the end wall, where it's seen whole, and what's the same from every
        // side, or is best seen from above, is on the side walls. The books are in the end wall either side of the dais and in the corners by the door, with whoever keeps the
        // Archive at a desk among them. How it looks is Basilica.*.cs and Exhibits.cs.
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
                Floor = SurfaceMaterial.Flagstones,
            };
            // The same windows down either side, and in the end wall one to each aisle, between its two records. The end of the nave has none: it's the skull's.
            d.Door(HallSide.Front, 0f, 2.8f, 8.2f, arched: true)
             .Window(HallSide.Front, -9.5f, 1.4f, 2.8f, 7.6f, arched: true).Window(HallSide.Front, 9.5f, 1.4f, 2.8f, 7.6f, arched: true)
             .Window(HallSide.Front, -16.5f, 1.4f, 2.8f, 7.6f, arched: true).Window(HallSide.Front, 16.5f, 1.4f, 2.8f, 7.6f, arched: true)
             .Window(HallSide.Back, -14.05f, 1.45f, 3f, 9f, arched: true).Window(HallSide.Back, 14.05f, 1.45f, 3f, 9f, arched: true);
            foreach (HallSide side in new[] { HallSide.PlusX, HallSide.MinusX })
            {
                foreach (float z in new[] { -7.5f, 0f, 7.5f }) d.Window(side, z, 1.6f, 2.6f, 8.6f, arched: true);
            }

            // The columns of the nave.
            foreach (float z in ArchiveBays)
            {
                d.Round(PieceKind.Column, -ArchiveArcade, z, 1f, 10.3f).Round(PieceKind.Column, ArchiveArcade, z, 1f, 10.3f);
            }

            // The records. The skull on its dais at the end of the nave; in each aisle two against the end wall and two against the side wall.
            d.Exhibit(ExhibitKind.TyrantsFelled, 0f, -9.95f, 0f)
             .Exhibit(ExhibitKind.FoesSlain, -10.5f, -11.55f, 0f)
             .Exhibit(ExhibitKind.HardestBlow, -17.6f, -11.55f, 0f)
             .Exhibit(ExhibitKind.UniquesFound, 10.5f, -11.55f, 0f)
             .Exhibit(ExhibitKind.Falls, 17.6f, -11.55f, 0f)
             .Exhibit(ExhibitKind.SwiftestLand, -19.25f, -3.75f, Quarter)
             .Exhibit(ExhibitKind.LandsFreed, -19.25f, 3.75f, Quarter)
             .Exhibit(ExhibitKind.DeepestLand, 19.25f, -3.75f, -Quarter)
             .Exhibit(ExhibitKind.GoldGathered, 19.25f, 3.75f, -Quarter);
            foreach (HallExhibit exhibit in d.Exhibits)
            {
                (Vector2 half, float height) = exhibit.Kind switch
                {
                    ExhibitKind.TyrantsFelled => (new Vector2(4.2f, 3f), 7f),
                    ExhibitKind.FoesSlain => (new Vector2(1.9f, 1.4f), 6f),
                    ExhibitKind.HardestBlow => (new Vector2(1.9f, 1.4f), 5.4f),
                    ExhibitKind.UniquesFound => (new Vector2(1.9f, 1.4f), 5.6f),
                    ExhibitKind.Falls => (new Vector2(1.9f, 1.4f), 5f),
                    ExhibitKind.SwiftestLand => (new Vector2(1.7f, 1.7f), 5f),
                    ExhibitKind.LandsFreed => (new Vector2(1.8f, 1.7f), 3.4f),
                    ExhibitKind.DeepestLand => (new Vector2(1.7f, 1.7f), 4.8f),
                    _ => (new Vector2(1.8f, 1.7f), 3.2f),
                };
                d.Piece(PieceKind.Plinth, exhibit.At.X, exhibit.At.Y, half.X, half.Y, height, yaw: exhibit.Yaw, variant: (int)exhibit.Kind);
            }

            // The archive itself: two tall cases in the end wall, either side of the dais, and one in each corner by the door, short of the wall that comes down for the camera.
            d.Piece(PieceKind.Bookcase, -5.2f, -12.45f, 1.15f, 0.55f, 7.4f, variant: 1)
             .Piece(PieceKind.Bookcase, 5.2f, -12.45f, 1.15f, 0.55f, 7.4f, variant: 2)
             .Piece(PieceKind.Bookcase, -20.4f, 10.8f, 1.5f, 0.55f, 6.2f, yaw: Quarter, variant: 3)
             .Piece(PieceKind.Bookcase, 20.4f, 10.8f, 1.5f, 0.55f, 6.2f, yaw: -Quarter, variant: 4)
             .Piece(PieceKind.ReadingDesk, -13.2f, 10.2f, 1.9f, 1f, 2f)
             .Round(PieceKind.Globe, 13.2f, 9.6f, 1.1f, 3.8f);

            // Candles on tall stands at the corners of the dais and inside the door, and a bench either side of the nave between its first columns.
            d.Round(PieceKind.CandleStand, -5.3f, -6.1f, 0.5f, 5.2f).Round(PieceKind.CandleStand, 5.3f, -6.1f, 0.5f, 5.2f)
             .Round(PieceKind.CandleStand, -4.7f, 11.3f, 0.5f, 5.2f).Round(PieceKind.CandleStand, 4.7f, 11.3f, 0.5f, 5.2f)
             .Piece(PieceKind.Bench, -ArchiveArcade, 5.7f, 1.5f, 0.5f, 1.2f, yaw: -Quarter).Piece(PieceKind.Bench, ArchiveArcade, 5.7f, 1.5f, 0.5f, 1.2f, yaw: Quarter);

            // Buttresses between the windows, all round, the same either side.
            foreach (float x in new[] { -18.5f, -9.5f, 0f, 9.5f, 18.5f }) d.Against(PieceKind.Buttress, x, -14.45f, 0.65f, 0.45f, PlacementIndex.Tall);
            foreach (float x in new[] { -13f, -5.6f, 5.6f, 13f }) d.Against(PieceKind.Buttress, x, 14.45f, 0.65f, 0.45f, PlacementIndex.Tall);
            foreach (float z in new[] { -11.3f, -3.75f, 3.75f, 11.3f })
            {
                d.Against(PieceKind.Buttress, 22.45f, z, 0.45f, 0.65f, PlacementIndex.Tall).Against(PieceKind.Buttress, -22.45f, z, 0.45f, 0.65f, PlacementIndex.Tall);
            }

            // Whoever keeps the Archive has stepped out from the desk, towards whoever comes in. The village comes to stand before the records.
            d.Spot(SpawnRole.Keeper, -13.2f, 7.3f);
            d.Visit(0f, 6.5f).Visit(0f, -4.4f).Visit(-10.5f, -6.6f).Visit(10.5f, -6.6f).Visit(-14.6f, -3.75f).Visit(14.6f, 3.75f).Visit(12.6f, 5.2f).Visit(-17.6f, -6.8f);
            return d;
        }
    }
}