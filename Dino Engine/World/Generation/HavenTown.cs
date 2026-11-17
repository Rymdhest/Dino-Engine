using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Architecture.Town;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Everything of the Haven's town that isn't its square, its halls or its wall (HAVEN.md): the market along the foot of the Forge's terrace, what each hall's work puts
    /// out before it, the homes of the people who got out, each on a plot of its own with a way to its door and something about it that says who lives there, the walk along
    /// the wall's top, the green, and the harbour below, where everyone who's here first came ashore. Laid out to the metre, east and north of the town's middle, on the levels
    /// <see cref="HavenBuilder"/> has laid. Nothing here is the Reich's colours (LORE.md).
    /// </summary>
    internal sealed class HavenTown
    {
        private static readonly Vector2 North = new Vector2(0f, 1f);
        private static readonly Vector2 East = new Vector2(1f, 0f);
        private static readonly float FacingSouth = YawFacing(-North);
        private const float StreetHalf = 3f;
        private const float GateHalf = 1.25f;       // Half the width of a garden's gate.

        private readonly TownMason _m;
        private readonly float _upper, _lower;
        private readonly WorldRandom _random;
        private readonly List<HitPart> _parts = new();

        public HavenTown(TownMason m, float upper, float lower)
        {
            _m = m;
            _upper = upper;
            _lower = lower;
            _random = m.Random.Fork("homes");
        }

        private WorldData World => _m.World;

        public void Build(WorldHall? forge, WorldHall? gaming, WorldHall? archive)
        {
            Turf();
            Market();
            Walk();
            Row();
            Outliers();
            Green();
            if (forge != null) ForgeYard(forge);
            if (gaming != null) BarnYard(gaming);
            if (archive != null) ArchiveCourt(archive);
            WallFoot();
            Harbour();
            People();
        }

        // ---------------------------------------------------------------- Ground

        // The whole town on turf kept short, and the harbour with it: meadow only begins where the town ends.
        private void Turf()
        {
            _m.Paint(GroundPaint.Green, _m.Box(new Vector2(-88f, -50f), new Vector2(88f, 80f)), 14f);
            _m.Paint(GroundPaint.Green, _m.Box(new Vector2(-70f, -94f), new Vector2(86f, -50f)), 10f);
        }

        // A way worn in the turf from one place to another, as feet wear one: what says how a green is used.
        private void Worn(Vector2 from, Vector2 to, float width = 1.3f)
        {
            _m.Paint(GroundPaint.Yard, Footprint.Segment(_m.C + from, _m.C + to, width * 0.5f), 1.1f);
        }

        // ---------------------------------------------------------------- The market

        /// <summary>
        /// The market, along the foot of the Forge's terrace, on the north side of the street out of the square, so that every stall's counter faces south: the merchant's and the
        /// beta vendor's nearest the square, then the Forge's steps, then what the town grows and bakes. Across the street a row of limes, and a board of notices at its corner.
        /// </summary>
        private void Market()
        {
            float kerb = HavenBuilder.MarketZ + StreetHalf;
            const float Wall = 12.6f;
            // Cobbles from the kerb to the wall, all the way along.
            _m.Paint(GroundPaint.Plaza, _m.Box(new Vector2(-62f, kerb - 0.3f), new Vector2(-20.5f, Wall)), 0.7f);
            float z = Wall - 2.35f;
            (float X, StallTrade Trade, int Colour, SpawnRole? Keeper)[] stalls =
            {
                (-25.4f, StallTrade.Wares, 5, SpawnRole.Merchant), (-31.6f, StallTrade.Gifts, 0, SpawnRole.BetaVendor),
                (-53.9f, StallTrade.Produce, 3, null), (-59.2f, StallTrade.Bread, 1, null),
            };
            foreach ((float x, StallTrade trade, int colour, SpawnRole? keeper) in stalls)
            {
                const float Width = 4.6f;
                Vector2 at = new Vector2(x, z);
                _m.Town(new TownSpec(TownKind.Stall, _m.NextSeed(), TownSpec.Cm(Width), (int)trade, colour), at, FacingSouth, _upper);
                // Its counter is in the way, and what's stacked behind it; its poles are walked round.
                _m.Block(Footprint.Box(_m.C + at - North * 1.2f, new Vector2(Width * 0.5f - 0.15f, 0.62f), 0f), 1.9f);
                _m.Block(Footprint.Box(_m.C + at + North * 0.85f, new Vector2(Width * 0.5f - 0.3f, 0.55f), 0f), 1.3f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f }) _m.Block(Footprint.Circle(_m.C + at + new Vector2(sx * Width * 0.5f, sz * 1.45f), 0.14f), 0f);
                }
                // Whoever keeps it stands out in front of it, at its corner, where they're seen.
                if (keeper is SpawnRole role) _m.Stand(role, at + new Vector2(Width * 0.5f - 0.6f, -3.1f));
                else _m.Stand(SpawnRole.Villager, at + new Vector2(-Width * 0.5f + 0.8f, -3.2f));
            }

            // Across the street, limes in a row with a lantern between each two, and the board the town's notices are pinned to where the street leaves the square.
            float south = HavenBuilder.MarketZ - StreetHalf;
            for (int i = 0; i < 3; i++)
            {
                float x = -33f - i * 13f;
                _m.Grow(Broadleaves.WhiteCherry, new Vector2(x, south - 4.6f), 0.5f, _upper);
                Lamp(new Vector2(x + 6.5f, south - 1.2f), North, _upper);
            }
            Vector2 board = new Vector2(-20.6f, south - 2.7f);
            _m.Place(new PlaceSpec(PlacePiece.NoticeBoard, _m.NextSeed()), board, YawFacing(East), new Vector2(1.5f, 0.7f), 2.8f, _upper);
            _m.Strew(LitterKind.Papers, board + East * 1.4f, 1.6f, 6, _upper);
            // Where the street ends and the back lane goes on down to the Row, a post that says which way is which.
            Signpost(new Vector2(-69.8f, -0.8f), 3);
        }

        // A signpost of the land's own, where ways part.
        private void Signpost(Vector2 at, int arms)
        {
            _m.Place(new PlaceSpec(PlacePiece.Signpost, _m.NextSeed(), arms, 0), at, _random.Range(0f, MathF.Tau), new Vector2(0.35f, 0f), 0f, null);
        }

        // ---------------------------------------------------------------- What each hall's work puts out before it

        /// <summary>
        /// The Forge's forecourt, up its steps: what's too big or too hot for indoors is done out here. The anvil on its stump east of the door with coal and bar stock by it,
        /// the dyer's lines west of it with the cloth still dripping its colour onto the cobbles, the Haven's banner either side of the door, and what's come and what's going
        /// stacked by the lean-to.
        /// </summary>
        private void ForgeYard(WorldHall hall)
        {
            Vector2 at = hall.Centre - _m.C;
            float front = at.Y - hall.Design.Half.Y, level = hall.Floor;
            Vector2 smith = new Vector2(at.X + 10.2f, front - 3f);
            Yardwork(YardTrade.Smithing, smith, FacingSouth, level, new Vector2(2.4f, 1.3f), 1.6f);
            _m.Strew(LitterKind.Embers, smith + new Vector2(-0.6f, -0.3f), 2.6f, 18, level);
            Vector2 lines = new Vector2(at.X - 11f, front - 2.7f);
            Yardwork(YardTrade.Dyeing, lines, 0f, level, Vector2.Zero, 0f);
            foreach (float x in new[] { -3.1f, 0f, 3.1f }) _m.Block(Footprint.Circle(_m.C + lines + East * x, 0.16f), 0f);
            _m.Block(Footprint.Circle(_m.C + lines + new Vector2(4.4f, 0.2f), 0.7f), 0.9f);
            Banner(new Vector2(at.X - 6.7f, front - 0.9f), 8.2f, 1, level);
            Banner(new Vector2(at.X + 6.7f, front - 0.9f), 8.2f, 5, level);
            _m.Place(new PlaceSpec(PlacePiece.Barrels, _m.NextSeed(), 4), new Vector2(at.X + 19.2f, front - 3.6f), 0.4f, new Vector2(1.6f, 0f), 1.4f, level);
            _m.Place(new PlaceSpec(PlacePiece.Crates, _m.NextSeed(), 4, 0), new Vector2(at.X + 19.6f, front - 0.2f), 1.5f, new Vector2(2.2f, 1.3f), 1.8f, level);
            _m.Stand(SpawnRole.Villager, new Vector2(at.X - 6f, front - 3.4f));
        }

        /// <summary>
        /// The Gaming House's forecourt and the yard beside it: it's a barn, and still half kept as one. Tables out in front where the house's ale is drunk in the sun, straw
        /// trodden about its doors, the hay cart in the yard between it and the gate's terrace with its casks, and a stack on the bank.
        /// </summary>
        private void BarnYard(WorldHall hall)
        {
            Vector2 at = hall.Centre - _m.C;
            float front = at.Y - hall.Design.Half.Y, level = hall.Floor;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 table = new Vector2(at.X + s * 10.6f, front - 2.9f);
                _m.Place(new PlaceSpec(PlacePiece.MessTable, _m.NextSeed(), PlaceSpec.Cm(3f)), table, MathF.PI * 0.5f, new Vector2(2.2f, 1.3f), 1.2f, level);
            }
            _m.Strew(LitterKind.Straw, new Vector2(at.X - 2.2f, front - 2.6f), 3.4f, 34, level);
            _m.Strew(LitterKind.Straw, new Vector2(at.X + 3.4f, front - 1.4f), 2.6f, 22, level);
            // The yard on the gate's side.
            float yardX = at.X - hall.Design.Half.X - 4.6f;
            _m.Paint(GroundPaint.Yard, _m.Box(new Vector2(yardX - 3.4f, front + 1f), new Vector2(yardX + 3.4f, at.Y + hall.Design.Half.Y)), 1.2f);
            _m.Place(new PlaceSpec(PlacePiece.Wagon, _m.NextSeed(), 0, 0, PlaceSpec.Cm(4.2f)), new Vector2(yardX + 0.2f, at.Y - 3f), 0.12f, new Vector2(1.6f, 4f), 2.6f, level);
            _m.Place(new PlaceSpec(PlacePiece.Barrels, _m.NextSeed(), 5), new Vector2(yardX - 0.6f, at.Y + 6.6f), 1.1f, new Vector2(1.6f, 0f), 1.4f, level);
            _m.Strew(LitterKind.Straw, new Vector2(yardX, at.Y + 1.5f), 3.6f, 30, level);
            // And on the bank beyond it, the stack.
            Vector2 stack = new Vector2(at.X + hall.Design.Half.X + 5.4f, at.Y + 1.5f);
            _m.Place(new PlaceSpec(PlacePiece.Haystack, _m.NextSeed(), PlaceSpec.Cm(1.6f)), stack, 0f, new Vector2(2.1f, 0f), PlacementIndex.Tall);
            _m.Stand(SpawnRole.Villager, new Vector2(at.X + 7f, front - 3.2f));
            // Under its terrace's wall, along the lane, the rail whatever drew the cart is tied to, and its trough.
            Vector2 rail = new Vector2(29.4f, HavenBuilder.BarnLaneZ + StreetHalf + 1.95f);
            _m.Place(new PlaceSpec(PlacePiece.HitchingRail, _m.NextSeed(), PlaceSpec.Cm(5f)), rail, FacingSouth, new Vector2(3f, 0.5f), 1.2f, _upper);
            _m.Paint(GroundPaint.Yard, _m.Box(rail - new Vector2(4.4f, 0.75f), rail + new Vector2(4.4f, 0.95f)), 0.7f);
            _m.Strew(LitterKind.Straw, rail + new Vector2(0.6f, -0.1f), 2.6f, 16, _upper);
            // And where the lane ends and the way down past the joiner's begins, a post that says so.
            Signpost(new Vector2(65.2f, 2f), 2);
        }

        /// <summary>The Archive's court: the Haven's banner either side of its door, and the Row crossed in flags before its steps, from the podium to the wall's walk.</summary>
        private void ArchiveCourt(WorldHall hall)
        {
            Vector2 at = hall.Centre - _m.C;
            float front = at.Y - hall.Design.Half.Y, level = hall.Floor;
            Banner(new Vector2(at.X - 11.4f, front - 1.3f), 8.8f, 2, level);
            Banner(new Vector2(at.X + 11.4f, front - 1.3f), 8.8f, 2, level);
            float kerbNorth = HavenBuilder.RowZ + StreetHalf, kerbSouth = HavenBuilder.RowZ - StreetHalf;
            _m.Paint(GroundPaint.Paving, _m.Box(new Vector2(at.X - 6.4f, kerbSouth - 0.4f), new Vector2(at.X + 6.4f, kerbNorth + 0.9f)), 0.3f);
            foreach (float s in new[] { -1f, 1f }) _m.Band(new Vector2(at.X + s * 6.6f, kerbSouth), new Vector2(at.X + s * 6.6f, kerbNorth), 0.45f, _upper);
        }

        // ---------------------------------------------------------------- The wall's walk, and its foot

        // The walk along the top of the town's wall, between the Row and the parapet: flagged, with benches that look out over the harbour, and flowers on the parapet.
        private void Walk()
        {
            float back = HavenBuilder.WallZ + 1.3f, kerb = HavenBuilder.RowZ - StreetHalf;
            _m.Paint(GroundPaint.Paving, _m.Box(new Vector2(-48.4f, back - 0.2f), new Vector2(48.4f, kerb - 0.35f)), 0.3f);
            // A band of kerbs where the flags meet the street's own.
            _m.Band(new Vector2(-48.4f, kerb - 0.2f), new Vector2(48.4f, kerb - 0.2f), 0.5f, _upper);
            float cope = _lower + HavenBuilder.Lift + PlazaStone.Ground + TownMason.Parapet;
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = s * (10.5f + i * 13f);
                    Bench(new Vector2(x, kerb - 1.5f), -North, 3.2f, _upper);
                    _m.PotOn(new Vector2(x + s * 4.4f, HavenBuilder.WallZ + 0.62f), cope, i % 2, 0.95f, i % 2 == 0 ? Undergrowth.Lavender : Undergrowth.Azalea);
                }
                Lamp(new Vector2(s * 30.2f, back + 0.7f), North, _upper);
            }
        }

        // Below the wall: the town's water, out of a spout in the wall into a trough, and what's waiting there to go up or to go to sea.
        private void WallFoot()
        {
            _m.Paint(GroundPaint.Plaza, _m.Box(new Vector2(-44f, HavenBuilder.WallZ - 5.5f), new Vector2(46f, HavenBuilder.WallZ - 0.1f)), 0.6f);
            Vector2 trough = new Vector2(15.2f, HavenBuilder.WallZ - 0.8f);
            _m.Town(new TownSpec(TownKind.Trough, _m.NextSeed(), TownSpec.Cm(3.2f)), trough, FacingSouth, _lower);
            _m.Block(Footprint.Box(_m.C + trough, new Vector2(1.7f, 0.75f), 0f), 1.25f);
            _m.Place(new PlaceSpec(PlacePiece.Barrels, _m.NextSeed(), 3), new Vector2(-15.6f, HavenBuilder.WallZ - 1.5f), 0.9f, new Vector2(1.5f, 0f), 1.4f, _lower);
            _m.Place(new PlaceSpec(PlacePiece.Crates, _m.NextSeed(), 3, 0), new Vector2(-27.2f, HavenBuilder.WallZ - 1.4f), 0.2f, new Vector2(1.8f, 1.1f), 1.6f, _lower);
            _m.Place(new PlaceSpec(PlacePiece.Barrels, _m.NextSeed(), 2), new Vector2(27.4f, HavenBuilder.WallZ - 1.5f), 2.2f, new Vector2(1.5f, 0f), 1.4f, _lower);
            Lamp(new Vector2(-38.4f, HavenBuilder.WallZ - 1.1f), -North, _lower);
            Lamp(new Vector2(39.6f, HavenBuilder.WallZ - 1.1f), -North, _lower);
        }

        // ---------------------------------------------------------------- Homes

        /// <summary>
        /// A home on its plot: the house itself, as solid as it stands, the ground under it level, and its chimney's foot, its wing and whatever leans on it in the way of
        /// whoever walks round it. It faces south, as every door in the Haven does that can; its spec says the rest.
        /// </summary>
        private void House(in TownSpec spec, Vector2 at, float level)
        {
            Vector2 half = new Vector2(spec.Metres(0), spec.Metres(1)) * 0.5f;
            Footprint body = Footprint.Box(_m.C + at, half, FacingSouth);
            World.Placement.Add(body, PlacementLayer.Structure, solid: true);
            World.Pads.Add(new GroundPad(body.Grown(1.2f), level, 3.5f));
            World.Clearings.Add(new Clearing(_m.C + at, half.Length + 3f, 8f));
            TownHouse.Solids(spec, _parts);
            // The first of its parts is the house itself, which is in already.
            for (int i = 1; i < _parts.Count; i++) _m.Block(_parts[i].At(_m.C + at, FacingSouth), _parts[i].Height);
            if (TownHouse.WingOf(spec, out Vector2 wing, out Vector2 wingHalf)) World.Placement.Add(new HitPart(wing, wingHalf, PlacementIndex.Tall).At(_m.C + at, FacingSouth), PlacementLayer.Structure, solid: true);
            _m.Town(spec, at, FacingSouth, level);
        }

        // Where a house's door is, on the ground, for a house standing at `at` and facing south.
        private static Vector2 DoorOf(in TownSpec spec, Vector2 at) => at + new Vector2(-TownHouse.DoorAt(spec), -spec.Metres(1) * 0.5f);

        /// <summary>
        /// The homes along the north side of the Row, each facing south over the wall to the sea behind a garden of its own: a fence along the street with a gate, stones trodden
        /// in from the gate to the door, flowers along the fence, and what each household does out where it's done.
        /// </summary>
        private void Row()
        {
            float fence = HavenBuilder.RowZ + StreetHalf + 0.35f;
            const float Front = -34.9f;      // Where every house's front wall stands.

            // The bakehouse, west of the lane: two storeys under tiles, its upper floor out over its shop, its oven's stack up the gable.
            TownSpec bakery = TownSpec.House(0xBA4E01u, 13f, 9.6f, HouseDesign.Townhouse, Roofing.Tile, Walling.StoneFoot, HouseFeature.Jetty | HouseFeature.Awning | HouseFeature.EndStack | HouseFeature.Boxes, 2);
            Plot(bakery, new Vector2(-13.6f, Front + 4.8f), -22.4f, -5.2f, fence, FenceCatalog.Paling, YardTrade.Baking, SignEmblem.Loaf, arbour: false, bloom: 2);

            // East of the lane, the cottage everyone passes: thatch with brows over its loft, a wing to the street, roses up its front.
            TownSpec cottage = TownSpec.House(0xC077A6u, 13f, 9.6f, HouseDesign.Cottage, Roofing.Thatch, Walling.Framed,
                HouseFeature.Wing | HouseFeature.Dormers | HouseFeature.Boxes | HouseFeature.Bench | HouseFeature.Climber | HouseFeature.EndStack, 0);
            Plot(cottage, new Vector2(13.6f, Front + 4.8f), 5.2f, 22.4f, fence, FenceCatalog.Wattle, YardTrade.Washing, null, arbour: true, bloom: 0);

            // The gardener's: thatch, a hood over the door, wood stacked under a lean-to, and more garden than house.
            TownSpec gardener = TownSpec.House(0x6A4DE2u, 12f, 9.2f, HouseDesign.Cottage, Roofing.Thatch, Walling.Framed,
                HouseFeature.Dormers | HouseFeature.Porch | HouseFeature.Boxes | HouseFeature.Climber | HouseFeature.LeanRight, 4);
            Vector2 gardenerAt = new Vector2(32.4f, Front + 4.6f);
            Plot(gardener, gardenerAt, 24.6f, 42.2f, fence, FenceCatalog.Wattle, YardTrade.Gardening, SignEmblem.Blossom, arbour: true, bloom: 4);
            KitchenGarden(24.8f, 42f, gardenerAt.Y + 4.6f + 0.9f, -16.6f);

            // The joiner's: shingles, and its near end open as a shop to work in, with what's being made stood out in front and what it's made of seasoning behind.
            TownSpec joiner = TownSpec.House(0x101AE4u, 14f, 9.6f, HouseDesign.Workshop, Roofing.Shingle, Walling.Framed, HouseFeature.LeanRight | HouseFeature.Bench | HouseFeature.Dormers, 3);
            Vector2 joinerAt = new Vector2(51.6f, Front + 4.8f);
            Plot(joiner, joinerAt, 44.4f, 61.6f, fence, FenceCatalog.Paling, YardTrade.Joinery, SignEmblem.Hammer, arbour: false, bloom: 1);
            TimberYard(44.6f, 61.4f, joinerAt.Y + 4.8f + 0.9f, -16.6f);
        }

        /// <summary>
        /// One home's plot on the Row, between <paramref name="west"/> and <paramref name="east"/> along the street: the house, a fence along the street with a gate before its
        /// door, the path in, a border of flowers inside the fence, and its household's work in the corner of the garden nearest the street.
        /// </summary>
        private void Plot(in TownSpec spec, Vector2 at, float west, float east, float fenceZ, FenceKind fence, YardTrade trade, SignEmblem? sign, bool arbour, int bloom)
        {
            House(spec, at, _upper);
            Vector2 door = DoorOf(spec, at);
            float front = at.Y - spec.Metres(1) * 0.5f;

            // The fence: along the street either side of the gate, and back to the house's front at each end.
            Vector2 inside = new Vector2((west + east) * 0.5f, (fenceZ + front) * 0.5f);
            _m.Fence(fence, new Vector2(west, fenceZ), new Vector2(door.X - GateHalf, fenceZ), inside);
            _m.Fence(fence, new Vector2(door.X + GateHalf, fenceZ), new Vector2(east, fenceZ), inside);
            _m.Fence(fence, new Vector2(west, fenceZ), new Vector2(west, front + 0.4f), inside);
            _m.Fence(fence, new Vector2(east, fenceZ), new Vector2(east, front + 0.4f), inside);
            _m.Keep(_m.Box(new Vector2(door.X - GateHalf, fenceZ - 0.6f), new Vector2(door.X + GateHalf, front)));

            // The way in: stones trodden into the turf from the gate to the step, and the turf worn round them.
            float length = front - 1.5f - fenceZ;
            _m.Town(new TownSpec(TownKind.Stepping, _m.NextSeed(), TownSpec.Cm(length), TownSpec.Cm(1.5f)), new Vector2(door.X, fenceZ - 0.3f), -MathF.PI * 0.5f, _upper);
            _m.Paint(GroundPaint.Yard, _m.Box(new Vector2(door.X - 0.7f, fenceZ - 0.3f), new Vector2(door.X + 0.7f, front - 0.6f)), 0.9f);
            if (arbour) _m.Town(new TownSpec(TownKind.Trellis, _m.NextSeed(), TownSpec.Cm(GateHalf * 2f), TownSpec.Cm(3.7f), bloom), new Vector2(door.X, fenceZ), 0f, _upper);

            // Flowers in a border along the inside of the fence, either side of the gate, wherever the house's wing leaves room for one.
            float wingFrom = float.MaxValue, wingTo = float.MinValue;
            if (TownHouse.WingOf(spec, out Vector2 wing, out Vector2 wingHalf))
            {
                wingFrom = at.X - wing.X - wingHalf.X - 0.6f;
                wingTo = at.X - wing.X + wingHalf.X + 0.6f;
            }
            foreach ((float from, float to) in new[] { (west + 0.8f, door.X - GateHalf - 0.5f), (door.X + GateHalf + 0.5f, east - 0.8f) })
            {
                if (to - from < 1.4f) continue;
                Flowers(from, to, fenceZ + 0.95f, x => x > wingFrom && x < wingTo && front - 2.6f < fenceZ + 2.2f);
            }

            // What the household does, out where it does it: in the wider side of the garden.
            bool wideEast = east - door.X > door.X - west;
            float yardX = wideEast ? Math.Min(door.X + GateHalf + 4.2f, east - 3.2f) : Math.Max(door.X - GateHalf - 4.2f, west + 3.2f);
            if (yardX > wingFrom - 2f && yardX < wingTo + 2f) yardX = wideEast ? wingTo + 2.4f : wingFrom - 2.4f;
            Vector2 yard = new Vector2(yardX, (fenceZ + front) * 0.5f + 0.55f);
            Yardwork(trade, yard, FacingSouth + _random.Range(-0.15f, 0.15f), _upper, new Vector2(1.6f, 0.6f), 1.5f);
            if (sign is SignEmblem emblem)
            {
                Vector2 post = new Vector2(wideEast ? west + 0.9f : east - 0.9f, fenceZ - 0.75f);
                _m.Town(new TownSpec(TownKind.Sign, _m.NextSeed(), (int)emblem, TownSpec.Cm(5.2f)), post, wideEast ? 0f : MathF.PI, _upper);
                _m.Block(Footprint.Circle(_m.C + post, 0.2f), 0f);
            }
        }

        // A border of flowers along a line at `z`, from one x to another, but where `skip` says something else stands.
        private void Flowers(float from, float to, float z, Func<float, bool>? skip = null, float level = float.NaN)
        {
            if (float.IsNaN(level)) level = _upper;
            Footprint border = _m.Box(new Vector2(from, z - 0.55f), new Vector2(to, z + 0.55f));
            _m.Paint(GroundPaint.Garden, border, 0.5f);
            _m.Garden(border);
            for (float x = from + 0.6f; x < to - 0.3f; x += _random.Range(1.2f, 1.8f))
            {
                WorldAsset plant = _random.Pick(Border);
                if (skip != null && skip(x)) continue;
                _m.Flower(plant, new Vector2(x, z + _random.Range(-0.15f, 0.2f)), level, _random.Range(0.75f, 1f));
            }
        }

        // What grows in a garden's border.
        private static readonly WorldAsset[] Border =
        {
            Undergrowth.Lavender, Undergrowth.PinkLupine, Undergrowth.BlueLupine, Undergrowth.RedPoppy, Undergrowth.WhiteDaisy, Undergrowth.Yarrow, Undergrowth.PurpleAster, Undergrowth.Azalea,
            Undergrowth.Lavender, Undergrowth.WhiteAzalea,
        };

        /// <summary>
        /// The gardener's ground behind the house, fenced in wattle with a gate onto the green: beds between boards, each with its own crop, a path of trodden earth down the
        /// middle, hives at the far end and a barrow of what's just been pulled.
        /// </summary>
        private void KitchenGarden(float west, float east, float south, float north)
        {
            Vector2 inside = new Vector2((west + east) * 0.5f, (south + north) * 0.5f);
            float gate = (west + east) * 0.5f;
            _m.Fence(FenceCatalog.Wattle, new Vector2(west, south - 0.9f), new Vector2(west, north), inside);
            _m.Fence(FenceCatalog.Wattle, new Vector2(east, south - 0.9f), new Vector2(east, north), inside);
            _m.Fence(FenceCatalog.Wattle, new Vector2(west, north), new Vector2(gate - GateHalf, north), inside);
            _m.Fence(FenceCatalog.Wattle, new Vector2(gate + GateHalf, north), new Vector2(east, north), inside);
            Worn(new Vector2(gate, south), new Vector2(gate, north + 1.5f), 1.5f);
            _m.Town(new TownSpec(TownKind.Trellis, _m.NextSeed(), TownSpec.Cm(GateHalf * 2f), TownSpec.Cm(3.7f), 1), new Vector2(gate, north), 0f, _upper);

            BedCrop[] crops = { BedCrop.Cabbages, BedCrop.Beans, BedCrop.Leeks, BedCrop.Pumpkins, BedCrop.Herbs, BedCrop.Flowers };
            int next = 0;
            float bedLength = (east - west) * 0.5f - 2.4f;
            for (float z = south + 1.4f; z < north - 1.5f; z += 2.5f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    BedCrop crop = crops[next++ % crops.Length];
                    Vector2 at = new Vector2(gate + s * (bedLength * 0.5f + 1.2f), z);
                    _m.Town(new TownSpec(TownKind.Bed, _m.NextSeed(), TownSpec.Cm(bedLength), TownSpec.Cm(1.5f), (int)crop), at, 0f, _upper);
                    // What grows by itself is planted in it, in a row.
                    WorldAsset? row = crop == BedCrop.Cabbages ? (WorldAsset)Undergrowth.Cabbage : crop == BedCrop.Herbs ? (WorldAsset)Undergrowth.Lavender : crop == BedCrop.Flowers ? (WorldAsset)Undergrowth.RedPoppy : null;
                    if (row == null) continue;
                    for (float x = -bedLength * 0.5f + 0.5f; x <= bedLength * 0.5f - 0.4f; x += crop == BedCrop.Cabbages ? 0.95f : 1.2f)
                    {
                        WorldAsset plant = crop == BedCrop.Flowers ? _random.Pick(Border) : row;
                        _m.Flower(plant, at + new Vector2(x, _random.Range(-0.2f, 0.2f)), _upper + 0.3f, crop == BedCrop.Cabbages ? 0.62f : 0.8f);
                    }
                }
            }
            Vector2 hives = new Vector2(east - 2.7f, north - 1.3f);
            _m.Thing(StructureCatalog.Hives, hives, FacingSouth, _upper);
        }

        /// <summary>The joiner's yard behind the house: a trunk on trestles over the sawpit, boards stacked to season with sticks between, and chips over all of it.</summary>
        private void TimberYard(float west, float east, float south, float north)
        {
            Vector2 inside = new Vector2((west + east) * 0.5f, (south + north) * 0.5f);
            float gate = west + 4f;
            _m.Fence(FenceCatalog.Paling, new Vector2(west, south - 0.9f), new Vector2(west, north), inside);
            _m.Fence(FenceCatalog.Paling, new Vector2(east, south - 0.9f), new Vector2(east, north), inside);
            _m.Fence(FenceCatalog.Paling, new Vector2(west, north), new Vector2(gate - GateHalf * 1.4f, north), inside);
            _m.Fence(FenceCatalog.Paling, new Vector2(gate + GateHalf * 1.4f, north), new Vector2(east, north), inside);
            _m.Paint(GroundPaint.Yard, _m.Box(new Vector2(west + 1f, south + 0.4f), new Vector2(east - 1f, north - 0.8f)), 1.2f);
            Vector2 pit = new Vector2((west + east) * 0.5f + 1.2f, south + 3f);
            _m.Place(new PlaceSpec(PlacePiece.Sawpit, _m.NextSeed()), pit, 0f, new Vector2(4.4f, 1.5f), 1.2f, _upper);
            _m.Place(new PlaceSpec(PlacePiece.PlankStack, _m.NextSeed(), PlaceSpec.Cm(4.6f)), new Vector2(east - 4f, north - 2.4f), 0f, new Vector2(2.7f, 1.3f), 1.6f, _upper);
            _m.Place(new PlaceSpec(PlacePiece.PlankStack, _m.NextSeed(), PlaceSpec.Cm(4.2f)), new Vector2(west + 2.4f, south + 4.4f), MathF.PI * 0.5f, new Vector2(2.7f, 1.3f), 1.6f, _upper);
            _m.Strew(LitterKind.Chips, pit + new Vector2(-1f, -0.4f), 3.6f, 36, _upper);
            _m.Strew(LitterKind.Chips, pit + new Vector2(3f, 2.4f), 2.6f, 20, _upper);
        }

        // The two homes off the ends of the streets out of the square's sides, each facing south onto its street behind a paling.
        private void Outliers()
        {
            // West of the Forge, the thatcher's own, half of its roof new and the ladder still up.
            TownSpec thatcher = TownSpec.House(0x7AA7C4u, 12.6f, 9.2f, HouseDesign.Cottage, Roofing.Thatch, Walling.Framed,
                HouseFeature.Mended | HouseFeature.Boxes | HouseFeature.EndStack | HouseFeature.Bench | HouseFeature.LeanLeft, 1);
            float kerb = HavenBuilder.MarketZ + StreetHalf;
            Vector2 at = new Vector2(-72.6f, 17.4f);
            House(thatcher, at, _upper);
            Front(thatcher, at, -79.6f, -66.6f, kerb + 0.5f, FenceCatalog.Paling, null, Vector2.Zero, 0f, null);
            // The reed for the other half of it, stooked beside the house where the cart tipped it.
            Vector2 reed = new Vector2(-82.4f, 12.2f);
            float turned = FacingSouth + 0.25f;
            _m.Town(new TownSpec(TownKind.Yardwork, _m.NextSeed(), (int)YardTrade.Thatching), reed, turned, null);
            _m.Block(Footprint.Box(_m.C + reed + new Vector2(-0.4f, -0.2f), new Vector2(2.5f, 1.2f), turned), 1.4f);
            _m.Paint(GroundPaint.Yard, _m.Circle(reed, 2.5f), 1.4f);
            _m.Strew(LitterKind.Straw, reed + new Vector2(0.4f, -0.6f), 3f, 30);

            // East of the Gaming House, the brewer's: stone below, shingles, a hood over its door, its casks on a stillage in the garden and a table where its ale is drunk.
            TownSpec brewer = TownSpec.House(0xB4E3E4u, 12.6f, 9.4f, HouseDesign.Cottage, Roofing.Shingle, Walling.StoneFoot,
                HouseFeature.EndStack | HouseFeature.Porch | HouseFeature.Bench | HouseFeature.Dormers | HouseFeature.Boxes, 5);
            Vector2 east = new Vector2(72.4f, 19.6f);
            House(brewer, east, _upper);
            float lane = HavenBuilder.BarnLaneZ + StreetHalf;
            Front(brewer, east, 65.2f, 79.6f, lane + 0.5f, FenceCatalog.Wattle, YardTrade.Brewing, new Vector2(2.9f, 1.2f), 1.8f, SignEmblem.Mug);
        }

        // A front garden for a house off the Row: a fence along its street with a gate and back to the house at either end, the stones in to its door, what the household
        // does out on the wider side of it, and flowers along the fence on the other.
        private void Front(in TownSpec spec, Vector2 at, float west, float east, float fenceZ, FenceKind fence, YardTrade? trade, Vector2 solid, float tall, SignEmblem? sign)
        {
            Vector2 door = DoorOf(spec, at);
            float front = at.Y - spec.Metres(1) * 0.5f;
            Vector2 inside = new Vector2((west + east) * 0.5f, (fenceZ + front) * 0.5f);
            _m.Fence(fence, new Vector2(west, fenceZ), new Vector2(door.X - GateHalf, fenceZ), inside);
            _m.Fence(fence, new Vector2(door.X + GateHalf, fenceZ), new Vector2(east, fenceZ), inside);
            _m.Fence(fence, new Vector2(west, fenceZ), new Vector2(west, front - 0.25f), inside);
            _m.Fence(fence, new Vector2(east, fenceZ), new Vector2(east, front - 0.25f), inside);
            float length = front - 1.5f - fenceZ;
            if (length > 1f)
            {
                _m.Town(new TownSpec(TownKind.Stepping, _m.NextSeed(), TownSpec.Cm(length + 0.6f), TownSpec.Cm(1.5f)), new Vector2(door.X, fenceZ - 0.6f), -MathF.PI * 0.5f, _upper);
                _m.Paint(GroundPaint.Yard, _m.Box(new Vector2(door.X - 0.7f, fenceZ - 0.6f), new Vector2(door.X + 0.7f, front - 0.6f)), 0.9f);
            }
            _m.Keep(_m.Box(new Vector2(door.X - GateHalf, fenceZ - 0.6f), new Vector2(door.X + GateHalf, front)));
            bool wideEast = east - door.X > door.X - west;
            foreach ((float from, float to, bool wide) in new[] { (west + 0.8f, door.X - GateHalf - 0.5f, !wideEast), (door.X + GateHalf + 0.5f, east - 0.8f, wideEast) })
            {
                if (trade != null && wide) continue;
                if (to - from >= 1.4f) Flowers(from, to, fenceZ + 0.95f);
            }
            if (trade is YardTrade work)
            {
                float yardX = wideEast ? (door.X + GateHalf + east) * 0.5f + 0.2f : (west + door.X - GateHalf) * 0.5f - 0.2f;
                Yardwork(work, new Vector2(yardX, (fenceZ + front) * 0.5f + 0.15f), FacingSouth, _upper, solid, tall);
            }
            if (sign is SignEmblem emblem)
            {
                Vector2 post = new Vector2(wideEast ? west + 0.9f : east - 0.9f, fenceZ - 0.75f);
                _m.Town(new TownSpec(TownKind.Sign, _m.NextSeed(), (int)emblem, TownSpec.Cm(5.2f)), post, wideEast ? 0f : MathF.PI, _upper);
                _m.Block(Footprint.Circle(_m.C + post, 0.2f), 0f);
            }
        }

        // ---------------------------------------------------------------- The green

        /// <summary>
        /// The green, east of the square behind the loggia: turf, an old oak with a seat round it, a household's linen out on the lines, a sail to sit under, and the ways
        /// feet have worn across it from the square to the oak and to the gardens' gates.
        /// </summary>
        private void Green()
        {
            Vector2 tree = new Vector2(46.5f, -8.2f);
            _m.Grow(Broadleaves.Oak, tree, 0.8f, _upper);
            foreach (float turn in new[] { 0f, MathF.PI * 0.5f, MathF.PI, -MathF.PI * 0.5f })
            {
                Vector2 out_ = new Vector2(MathF.Sin(turn), MathF.Cos(turn));
                Bench(tree + out_ * 2.6f, out_, 2.6f, _upper);
            }
            Vector2 lines = new Vector2(33.6f, -11.6f);
            Yardwork(YardTrade.Drying, lines, 0f, _upper, Vector2.Zero, 0f);
            foreach (float x in new[] { -3.1f, 0f, 3.1f }) _m.Block(Footprint.Circle(_m.C + lines + East * x, 0.16f), 0f);
            Vector2 sail = new Vector2(58.4f, -4.6f);
            _m.Town(new TownSpec(TownKind.Canopy, _m.NextSeed(), TownSpec.Cm(5.6f), TownSpec.Cm(4.6f), TownSpec.Cm(3.9f), 3), sail, FacingSouth, _upper);
            foreach (float sx in new[] { -2.8f, 2.8f })
            {
                foreach (float sz in new[] { -2.3f, 0f, 2.3f }) _m.Block(Footprint.Circle(_m.C + sail + new Vector2(sx, sz), 0.14f), 0f);
            }
            // The table under it stands out of its south side, where it's seen.
            _m.Place(new PlaceSpec(PlacePiece.MessTable, _m.NextSeed(), PlaceSpec.Cm(2.8f)), sail - North * 1.3f, 0f, new Vector2(2.2f, 1.3f), 1.2f, _upper);
            Worn(new Vector2(28.4f, -9.2f), tree + new Vector2(-4.2f, -0.4f));
            Worn(tree + new Vector2(-1.4f, -4f), new Vector2(33.4f, -15.4f));
            Worn(tree + new Vector2(2f, 4.2f), new Vector2(52f, HavenBuilder.BarnLaneZ - StreetHalf));
            Worn(tree + new Vector2(4.2f, 1f), sail + new Vector2(-3.6f, 0f));
        }

        // ---------------------------------------------------------------- The harbour

        /// <summary>
        /// The harbour, on the shore south-east of the wall, where everyone in the Haven first came ashore. A path down to a jetty with boats on it and boats drawn up beside
        /// it; nets and the catch drying; a fisher's cottage boarded against the weather; the house where whoever comes next is taken in, its fire lit and its table laid;
        /// and at the head of the jetty the lanterns kept burning for those who haven't come yet.
        /// </summary>
        private void Harbour()
        {
            // The landing house first, as the way to the water goes round it: one long roof and two fires, where whoever's just come is given a bed.
            TownSpec landing = TownSpec.House(0x1A9D19u, 21f, 9.4f, HouseDesign.Longhouse, Roofing.Thatch, Walling.Framed, HouseFeature.TwoStacks | HouseFeature.Bench | HouseFeature.Boxes | HouseFeature.Dormers, 0);
            Vector2 house = new Vector2(60f, -73f);
            float floor = MathF.Max(World.Elevation.Sample(_m.C + house), WorldPlan.SeaLevel + 1.2f);
            House(landing, house, floor);
            Vector2 landingDoor = DoorOf(landing, house);
            float yardZ = landingDoor.Y - 7.4f;

            Vector2 from = new Vector2(64f, yardZ);
            if (!FindShore(from, 70f, 130f, out Vector2 shore, out Vector2 sea)) return;
            Vector2 side = new Vector2(-sea.Y, sea.X);
            WorldRoad? path = RoadStage.BuildRoad(World, new List<Vector2>
            {
                _m.C + HarbourWay, _m.C + new Vector2(44.6f, -65.5f), _m.C + new Vector2(43.4f, yardZ + 4f), _m.C + new Vector2(49f, yardZ), _m.C + from, _m.C + shore - sea * 4f,
            }, RoadClass.Footpath);
            if (path != null)
            {
                World.Roads.Add(path);
                SettlementStage.ReserveRoad(World, path);
            }

            // The jetty, out over the water from the path's end, a boat moored either side of it, and two more drawn up the beach.
            Vector2 root = shore - sea * 2.5f;
            float yaw = YawFacing(sea);
            World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Jetty, _m.NextSeed(), PlaceSpec.Cm(17f), PlaceSpec.Cm(2.8f), PlaceSpec.Cm(1.4f)).Id, _m.C + root, yaw, Vector3.One, WorldPlan.SeaLevel + 0.9f));
            World.Placement.Add(Footprint.Box(_m.C + root + sea * 8.5f, new Vector2(1.6f, 8.5f), yaw), PlacementLayer.Keepout);
            foreach (float s in new[] { -1f, 1f })
            {
                float length = s < 0f ? 5.6f : 6.4f;
                Vector2 moored = root + sea * (s < 0f ? 11f : 14f) + side * (s * (1.4f + length * 0.18f + 0.5f));
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Boat, _m.NextSeed(), PlaceSpec.Cm(length), 0).Id, _m.C + moored, yaw + (s < 0f ? 0f : MathF.PI) + 0.06f * s, Vector3.One, WorldPlan.SeaLevel));
                float drawn = s < 0f ? 5.2f : 6f;
                Vector2 beached = shore - sea * (drawn * 0.5f + 1.3f) + side * (s < 0f ? 7f : 13.5f);
                _m.Place(new PlaceSpec(PlacePiece.Boat, _m.NextSeed(), PlaceSpec.Cm(drawn), s < 0f ? 1 : 2), beached, yaw + s * 0.3f, new Vector2(drawn * 0.18f + 0.5f, drawn * 0.5f + 0.3f), 1.1f);
            }
            _m.Place(new PlaceSpec(PlacePiece.Capstan, _m.NextSeed()), shore - sea * 10.5f + side * 10f, yaw, new Vector2(1.9f, 1.6f), 1.2f);

            // Nets and the catch out to dry above the beach, creels by the jetty and a net spread to be mended.
            _m.Place(new PlaceSpec(PlacePiece.NetRack, _m.NextSeed(), PlaceSpec.Cm(7f)), shore - sea * 13f - side * 15f, yaw + MathF.PI * 0.5f, new Vector2(3.8f, 1f), 2.4f);
            _m.Place(new PlaceSpec(PlacePiece.FishRack, _m.NextSeed(), PlaceSpec.Cm(5f)), shore - sea * 21f - side * 16f, yaw + MathF.PI * 0.5f, new Vector2(2.8f, 1f), 2.4f);
            _m.Place(new PlaceSpec(PlacePiece.Pots, _m.NextSeed()), shore - sea * 3f - side * 4.2f, yaw + 0.5f, new Vector2(1.6f, 1.2f), 1.3f);
            _m.Lay(new PlaceSpec(PlacePiece.MendingNet, _m.NextSeed(), PlaceSpec.Cm(4.4f), PlaceSpec.Cm(2.4f)), shore - sea * 12f - side * 9.5f, yaw + 0.4f);
            _m.Place(new PlaceSpec(PlacePiece.Barrels, _m.NextSeed(), 3), shore - sea * 5f - side * 7.8f, 0.7f, new Vector2(1.5f, 0f), 1.4f);
            _m.Strew(LitterKind.Shells, shore - sea * 9f - side * 11f, 3.4f, 26);

            // The lanterns hung for those who haven't come yet, beside the jetty's head, looking out to sea.
            Vector2 lanterns = shore - sea * 9.4f - side * 5.4f;
            _m.Town(new TownSpec(TownKind.Memorial, _m.NextSeed(), TownSpec.Cm(5.6f), 11), lanterns, yaw, null);
            foreach (float x in new[] { -2.8f, 0f, 2.8f }) _m.Block(Footprint.Circle(_m.C + lanterns + side * x, 0.2f), 0f);
            _m.Stand(SpawnRole.Villager, lanterns - sea * 2.6f);

            // The fisher's cottage, above the beach north of the jetty, boarded against the weather, its creels by its door and the table the catch is gutted on.
            TownSpec fisher = TownSpec.House(0xF15E42u, 11f, 8.4f, HouseDesign.Fisher, Roofing.Shingle, Walling.Boarded, HouseFeature.LeanRight | HouseFeature.Bench | HouseFeature.EndStack, 3);
            Vector2 cottage = new Vector2(76.4f, -64.6f);
            float ground = MathF.Max(World.Elevation.Sample(_m.C + cottage), WorldPlan.SeaLevel + 1.3f);
            House(fisher, cottage, ground);
            Vector2 door = DoorOf(fisher, cottage);
            _m.Town(new TownSpec(TownKind.Yardwork, _m.NextSeed(), (int)YardTrade.Fishing), door + new Vector2(4.4f, -2.6f), FacingSouth - 0.2f, ground);
            _m.Block(Footprint.Circle(_m.C + door + new Vector2(4.4f, -2.6f), 1.2f), 1.4f);
            _m.Place(new PlaceSpec(PlacePiece.GuttingTable, _m.NextSeed(), PlaceSpec.Cm(2.4f)), door + new Vector2(-4.2f, -2.4f), 0.2f, new Vector2(1.6f, 1f), 1.2f);
            Worn(door + new Vector2(0f, -1f), shore - sea * 6f + side * 2f);

            _m.Town(new TownSpec(TownKind.Sign, _m.NextSeed(), (int)SignEmblem.Fish, TownSpec.Cm(4.8f)), door + new Vector2(-2.4f, -1.5f), 0f, ground);
            _m.Block(Footprint.Circle(_m.C + door + new Vector2(-2.4f, -1.5f), 0.2f), 0f);

            // South of the landing house, on the shore of the bay, the boat the town is building: bigger than any that brought them, to go back for whoever's still over
            // the water. Its timber's stacked beside it, its tools are on their rack and the chips lie where they fell.
            if (FindShore(new Vector2(47f, -93f), 150f, 215f, out Vector2 slip, out Vector2 down))
            {
                const float Keel = 9.5f;
                Vector2 stocks = slip - down * (Keel * 0.5f + 2.4f), across = new Vector2(-down.Y, down.X);
                float way = YawFacing(down);
                _m.Paint(GroundPaint.Yard, Footprint.Box(_m.C + stocks, new Vector2(Keel * 0.36f + 4.4f, Keel * 0.5f + 2.6f), way), 2f);
                _m.Place(new PlaceSpec(PlacePiece.BoatStocks, _m.NextSeed(), PlaceSpec.Cm(Keel)), stocks, way, new Vector2(Keel * 0.36f + 1.2f, Keel * 0.5f + 0.4f), 1.4f);
                _m.Place(new PlaceSpec(PlacePiece.PlankStack, _m.NextSeed(), PlaceSpec.Cm(4.6f)), stocks + across * 7.4f - down * 1.2f, way + MathF.PI * 0.5f, new Vector2(2.7f, 1.3f), 1.6f);
                _m.Place(new PlaceSpec(PlacePiece.ToolRack, _m.NextSeed()), stocks - across * 5.4f - down * 1.6f, way + MathF.PI * 0.5f, new Vector2(3.6f, 0.8f), 2f);
                _m.Strew(LitterKind.Chips, stocks + across * 3f, 4.2f, 40);
                _m.Strew(LitterKind.Chips, stocks - across * 2.8f - down * 2f, 3.6f, 28);
                _m.Stand(SpawnRole.Villager, stocks - across * 4.6f + down * 1.2f);
            }
            // Where the ways down off the rise meet and go on to the water.
            Signpost(HarbourWay + new Vector2(-1.4f, -4.6f), 3);

            // Before the landing house's door, the bundles of whoever came last, a fire with the pot on and a table laid.
            _m.Town(new TownSpec(TownKind.Yardwork, _m.NextSeed(), (int)YardTrade.Arriving), landingDoor + new Vector2(5.2f, -3.2f), FacingSouth + 0.3f, floor);
            _m.Block(Footprint.Circle(_m.C + landingDoor + new Vector2(5.2f, -3.2f), 1.3f), 1.5f);
            Vector2 fire = landingDoor + new Vector2(-4.6f, -3.4f);
            _m.Place(new PlaceSpec(PlacePiece.Campfire, _m.NextSeed()), fire, 0f, new Vector2(1.4f, 0f), 0.8f, floor);
            _m.Place(new PlaceSpec(PlacePiece.MessTable, _m.NextSeed(), PlaceSpec.Cm(3f)), fire + new Vector2(-4.4f, 0.6f), 0.2f, new Vector2(2.2f, 1.3f), 1.2f, floor);
            Banner(landingDoor + new Vector2(-2.6f, -0.9f), 7.4f, 0, floor);
            // Its yard is trodden bare from the fire to the bundles, as a place a whole town comes to is.
            _m.Paint(GroundPaint.Yard, _m.Box(landingDoor + new Vector2(-11.4f, -5.6f), landingDoor + new Vector2(7.6f, -0.4f)), 1.8f);
            _m.Stand(SpawnRole.Villager, landingDoor + new Vector2(1.6f, -3f));
            _m.Stand(SpawnRole.Villager, fire + new Vector2(1.9f, 1.4f));
        }

        /// <summary>Where the ways down off the rise meet, east of the tiltyard, and the way on to the water begins.</summary>
        public static readonly Vector2 HarbourWay = new Vector2(51f, -63.5f);

        // Where the land meets the sea nearest a point, looking out between two bearings: the place, and the way out to sea from it.
        private bool FindShore(Vector2 from, float bearingFrom, float bearingTo, out Vector2 shore, out Vector2 sea)
        {
            float best = float.MaxValue;
            shore = from;
            sea = -North;
            for (float bearing = bearingFrom; bearing <= bearingTo; bearing += 5f)
            {
                Vector2 d = TownMason.Toward(bearing);
                for (float t = 4f; t < 90f; t += 1f)
                {
                    if (World.Elevation.Sample(_m.C + from + d * t) > WorldPlan.SeaLevel + 0.2f) continue;
                    if (t < best)
                    {
                        best = t;
                        shore = from + d * t;
                        sea = d;
                    }
                    break;
                }
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------------- People, and small things

        private void People()
        {
            // On the wall's walk, on the green, by the lane and below the wall.
            _m.Stand(SpawnRole.Villager, new Vector2(-16f, HavenBuilder.WallZ + 3.4f));
            _m.Stand(SpawnRole.Villager, new Vector2(22f, HavenBuilder.WallZ + 3.2f));
            _m.Stand(SpawnRole.Villager, new Vector2(42.6f, -11.6f));
            _m.Stand(SpawnRole.Villager, new Vector2(3.6f, -34f));
            _m.Stand(SpawnRole.Villager, new Vector2(13f, HavenBuilder.WallZ - 3.2f));
        }

        // What a household's or a hall's work leaves standing: its piece, and what of it is in the way.
        private void Yardwork(YardTrade trade, Vector2 at, float yaw, float level, Vector2 solid, float tall)
        {
            _m.Town(new TownSpec(TownKind.Yardwork, _m.NextSeed(), (int)trade), at, yaw, level);
            if (solid.X > 0f) _m.Block(Footprint.Box(_m.C + at, solid, yaw), tall);
            _m.Paint(GroundPaint.Yard, _m.Circle(at, 1.9f), 1.4f);
        }

        private void Bench(Vector2 at, Vector2 facing, float length, float level)
        {
            float yaw = YawFacing(facing);
            _m.Town(new TownSpec(TownKind.Bench, _m.NextSeed(), TownSpec.Cm(length)), at, yaw, level);
            _m.Block(Footprint.Box(_m.C + at, new Vector2(length * 0.5f, 0.42f), yaw), 1.2f);
        }

        private void Lamp(Vector2 at, Vector2 facing, float level)
        {
            _m.Town(new TownSpec(TownKind.Lamp, _m.NextSeed(), TownSpec.Cm(5.4f)), at, YawFacing(facing), level);
            _m.Block(Footprint.Circle(_m.C + at, 0.22f), 0f);
        }

        private void Banner(Vector2 at, float height, int colour, float level)
        {
            _m.Town(new TownSpec(TownKind.Banner, _m.NextSeed(), TownSpec.Cm(height), colour), at, FacingSouth, level);
            _m.Block(Footprint.Circle(_m.C + at, 0.36f), 0f);
        }
    }
}
