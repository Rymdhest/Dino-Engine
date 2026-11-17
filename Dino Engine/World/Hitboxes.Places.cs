using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Seats;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    public static partial class Hitboxes
    {
        /// <summary>
        /// What's solid about a piece of a place with a story (PLACES.md), in the frame its model is built in, from the numbers in its id: <paramref name="parts"/> is cleared and filled.
        /// Every number here was measured off the piece's model where it stands above a knee and below a person's waist (`Dino WorldLab hitboxes --mode measure`), and is a little
        /// inside it, never round it.
        /// </summary>
        /// <summary>
        /// The pieces their builders lay out themselves, piece by piece, and make as solid as they are where they stand: a ship's hull, the walls of a ruin or a castle of snow, a shrine
        /// gate's posts, the Institute's house and what stands inside it, a fossil in its pit, a fallen mast, a jetty, what a weaver strings between trunks. They never come through
        /// <see cref="Of(in PlaceSpec, List{HitPart})"/>.
        /// </summary>
        public static readonly IReadOnlySet<PlacePiece> ByBuilder = new HashSet<PlacePiece>
        {
            PlacePiece.Hull, PlacePiece.LabHall, PlacePiece.Hatchery, PlacePiece.Face, PlacePiece.RuinWall, PlacePiece.SnowWall, PlacePiece.SnowGate, PlacePiece.Pipes, PlacePiece.ShrineGate,
            PlacePiece.Specimens, PlacePiece.AmberBench, PlacePiece.Fossil, PlacePiece.Mast, PlacePiece.Jetty, PlacePiece.Web, PlacePiece.Wrap, PlacePiece.EggSac, PlacePiece.Cocoon,
            PlacePiece.CageDoor, PlacePiece.FarmGate, PlacePiece.PrayerLine,
        };

        public static HitKind Of(in PlaceSpec spec, List<HitPart> parts)
        {
            parts.Clear();
            switch (spec.Kind)
            {
                // ---- Wood, wicker and cloth that's knocked about: a body walking into it breaks it.
                case PlacePiece.Crates:
                case PlacePiece.Barrels:
                case PlacePiece.BoneCrates:
                case PlacePiece.Cargo:
                case PlacePiece.Bales:
                case PlacePiece.Pots:
                    return HitKind.Loose;

                // ---- Lower than a knee, lying flat, strewn, hung up out of the way, or nothing a body would mind: stepped over or walked through.
                case PlacePiece.Litter:
                case PlacePiece.Puddle:
                case PlacePiece.Duckboards:
                case PlacePiece.MendingNet:
                case PlacePiece.GuttingTable:
                case PlacePiece.Shells:
                case PlacePiece.SilkFloor:
                case PlacePiece.Web:
                case PlacePiece.EggSac:
                case PlacePiece.Cocoon:
                case PlacePiece.Wrap:
                case PlacePiece.Ribbons:
                case PlacePiece.Cairns:
                case PlacePiece.Offerings:
                case PlacePiece.Basin:
                case PlacePiece.DigGrid:
                case PlacePiece.Revetment:
                case PlacePiece.Sieve:
                case PlacePiece.Jackets:
                case PlacePiece.Fossil:
                case PlacePiece.ChainStake:
                case PlacePiece.NestMound:
                case PlacePiece.Carcass:
                case PlacePiece.Stump:
                case PlacePiece.Log:
                case PlacePiece.Slash:
                case PlacePiece.Driftwood:
                case PlacePiece.Wreckage:
                case PlacePiece.Mast:
                case PlacePiece.ShipBoat:
                case PlacePiece.Rubble:
                case PlacePiece.RuneStone:
                case PlacePiece.FallenColumn:
                case PlacePiece.RuinSteps:
                case PlacePiece.HitchingRail:
                case PlacePiece.Midden:
                case PlacePiece.BiteDummy:
                case PlacePiece.Sledges:
                case PlacePiece.SnowLantern:
                case PlacePiece.Jetty:
                case PlacePiece.CageDoor:
                case PlacePiece.FarmGate:
                case PlacePiece.Specimens:
                case PlacePiece.AmberBench:
                case PlacePiece.PrayerLine:
                    return HitKind.Open;

                // ---- Posts and poles.
                case PlacePiece.Lantern:
                case PlacePiece.Banner:
                case PlacePiece.Torch:
                case PlacePiece.Signpost:
                case PlacePiece.Pennant:
                    return Post(parts);
                case PlacePiece.Totem: return Post(parts, 0.35f);
                case PlacePiece.Brazier: return Post(parts, 0.45f);
                case PlacePiece.Capstan: return Post(parts, 0.45f);
                case PlacePiece.BreakingPost: return Post(parts, 0.4f);
                case PlacePiece.Anchor: return Post(parts, 0.3f, 0f, 1.55f);
                case PlacePiece.Gibbet:
                    Post(parts, 0.35f);
                    return Post(parts, 0.45f, 1.9f, 0.05f);

                // ---- A fire, walked round and blown across.
                case PlacePiece.Campfire: return Round(parts, 1f, 0f);

                // ---- Racks, frames, wheels and bars: walked round, seen and struck through.
                case PlacePiece.WeaponRack: return Rack(parts, Math.Clamp(spec.Metres(0), 1.6f, 5f) * 0.5f + 0.15f, 0.5f);
                case PlacePiece.Barricade: return Rack(parts, Math.Clamp(spec.Metres(0), 2f, 6f) * 0.5f, 0.5f);
                case PlacePiece.Skidder: return Rack(parts, 1.55f, 1.55f);     // Its two high wheels: the log slung under it and the tongue are stepped over.
                case PlacePiece.NetRack:
                case PlacePiece.FishRack:
                    return Rack(parts, spec.Metres(0) * 0.5f, 0.3f);
                case PlacePiece.BoatStocks: return Rack(parts, 1f, spec.Metres(0) * 0.5f);
                case PlacePiece.Scales: return Rack(parts, 1.45f, 0.35f);
                case PlacePiece.ButcherBlock: return Rack(parts, 1.9f, 0.25f, 0f, -0.8f);
                case PlacePiece.Horn: return Rack(parts, 0.3f, 1.3f, 0f, 0.25f);
                case PlacePiece.BurstCage: return Rack(parts, Math.Clamp(spec.Metres(0), 2.5f, 5f) * 0.5f, Math.Clamp(spec.Metres(0), 2.5f, 5f) * 0.35f);
                case PlacePiece.Bell: return spec.Int(0) == 1 ? HitKind.Open : Rack(parts, 1.15f, 0.4f);
                case PlacePiece.HarnessRack:
                    Rack(parts, 2f, 0.4f, 0.1f, -0.2f);
                    return Post(parts, 0.4f, 1.4f, 0.75f);
                case PlacePiece.ToolRack:
                    Rack(parts, 1.8f, 0.36f, 0.2f, 0.14f);
                    return Round(parts, 0.55f, 1.6f, 2.6f, 0.3f);
                case PlacePiece.Crane:
                    // Its treadwheel's two frames on the beam they stand on, and the block slung from its jib.
                    Rack(parts, 0.5f, 1.8f, 0f, -0.25f);
                    Rack(parts, 0.75f, 1.8f, 1.75f, -0.25f);
                    Rack(parts, 2.75f, 0.5f, 0.75f, -0.25f);
                    return Box(parts, 1f, 0.5f, 1.6f, 0f, 5f);
                case PlacePiece.Salvage:
                    // Its sheerlegs' feet and the crate swinging under their head: the ropes are walked through.
                    Post(parts, 0.3f, -2.2f, -0.6f);
                    Post(parts, 0.3f, 2.2f, -0.6f);
                    return Box(parts, 0.75f, 0.65f, 2.4f, 0f, 0.9f);

                // ---- Tents and shelters: the canvas where it's above a knee, its guys and pegs stepped over.
                case PlacePiece.Tent: return Box(parts, 1.75f, Math.Clamp(spec.Metres(0), 3f, 9f) * 0.5f - 0.05f, 3.3f);
                case PlacePiece.CommandTent:
                {
                    // Its fly out over the table hangs lower than a person's chest, so it's in the way as the tent is, out to the poles it's on.
                    float hz = Math.Clamp(spec.Metres(0), 4.5f, 9f) * 0.5f;
                    return Box(parts, 2.45f, hz + 1.2f, 3.6f, 0f, 1.2f);
                }
                case PlacePiece.Shelter:
                {
                    // Boarded at its back and down its sides, open at its front, so it can be walked into.
                    float hx = Math.Clamp(spec.Metres(0), 3f, 12f) * 0.5f, hz = Math.Clamp(spec.Metres(1), 2.5f, 7f) * 0.5f;
                    Box(parts, hx + 0.15f, 0.2f, 2.2f, 0f, -hz + 0.1f);
                    Box(parts, 0.2f, hz, 2.2f, -hx, 0f);
                    return Box(parts, 0.2f, hz, 2.2f, hx, 0f);
                }

                // ---- What stands about a camp or a yard.
                case PlacePiece.Wagon:
                {
                    float hz = Math.Clamp(spec.Metres(2), 3f, 5.5f) * 0.5f;
                    return Box(parts, 1.05f, hz, spec.Int(0) == 1 ? 1.6f : spec.Int(1) == 1 ? 2.6f : 1.7f);
                }
                case PlacePiece.Firewood: return Box(parts, spec.Metres(0) * 0.5f + 0.1f, 0.55f, spec.Metres(1), 0f, 0.03f);
                case PlacePiece.MessTable:
                {
                    // The table and its benches are knee high: the lantern's pole at its end, and its arm out over it, are what's in the way.
                    float hx = Math.Clamp(spec.Metres(0), 1.8f, 4f) * 0.5f;
                    Post(parts, 0.2f, -hx - 0.25f, 0f);
                    return Rack(parts, 0.4f, 0.12f, -hx + 0.15f, 0f);
                }
                case PlacePiece.Spoil:
                {
                    // A heap of earth, solid where it stands over a knee.
                    float length = Math.Clamp(spec.Metres(0), 2f, 20f), high = length * 0.12f + 0.6f;
                    if (high < StepOver + 0.2f) return HitKind.Open;
                    float over = MathF.Sqrt(1f - StepOver / high);
                    return Box(parts, length * 0.5f * over, (length * 0.22f + 0.8f) * over, high);
                }
                case PlacePiece.DrawingTable:
                    Box(parts, 1.5f, 0.75f, 1.8f, 0f, -0.25f);
                    foreach (float x in new[] { -1.9f, 1.9f })
                    {
                        Post(parts, 0.2f, x, 1.1f);
                        Post(parts, 0.2f, x, -1.4f);
                    }
                    return HitKind.Solid;
                case PlacePiece.Ledger: return Box(parts, 1.4f, 1.15f, 2.6f, 0f, -0.12f);   // Its awning comes down lower than a person's chest.
                case PlacePiece.NoticeBoard: return Box(parts, 1.35f, 0.3f, 2.9f);
                case PlacePiece.Haystack: return Round(parts, spec.Metres(0) * 0.95f, 3f);
                case PlacePiece.Hoist: return Box(parts, 1.4f, 0.4f, 1.6f, 0f, -0.25f);     // The jacketed bone coming up on its chain: the tripod's legs are poles.
                case PlacePiece.CollarForge: return Box(parts, 1.3f, 0.35f, 1.7f, 0f, -1.65f);
                case PlacePiece.Statue: return Round(parts, 0.8f, 1.6f);

                // ---- A logging camp's.
                case PlacePiece.Sawpit: return Box(parts, 3.6f, 0.8f, 1.6f, 0f, -0.3f);
                case PlacePiece.PlankStack: return Box(parts, spec.Metres(0) * 0.5f + 0.1f, 1.05f, 1.7f);
                case PlacePiece.LogDeck: return Box(parts, 1.85f, spec.Metres(0) * 0.5f + 0.2f, 3f);
                case PlacePiece.CharcoalClamp:
                {
                    float radius = Logging.ClampRadius(spec);
                    return Round(parts, radius * 0.72f, radius * 0.68f + 0.3f);
                }

                // ---- A quarry's.
                case PlacePiece.Block: return Box(parts, spec.Metres(0) * 0.47f, spec.Metres(2) * 0.47f, spec.Metres(1));
                case PlacePiece.BlockStack:
                {
                    // Only its second row stands above a knee.
                    int bottom = (Math.Clamp(spec.Int(0), 2, 9) + 1) / 2;
                    return bottom < 2 ? HitKind.Open : Box(parts, (bottom - 1) * 0.72f, 0.5f, 1.7f);
                }
                case PlacePiece.Sledge: return Box(parts, 0.8f, 1.2f, 1.9f);
                case PlacePiece.Banker: return Box(parts, 0.75f, 0.4f, 1.4f);
                case PlacePiece.SplitLine: return Box(parts, 1.5f, 0.8f, 1.5f);

                // ---- A fishing village's.
                case PlacePiece.Boat:
                {
                    // Afloat or drawn up it's knee high; on its side, the side it fell to.
                    if (spec.Int(1) != 2) return HitKind.Open;
                    (float side, float beam) = Fishing.Lying(spec);
                    return Box(parts, beam * 0.24f, Math.Clamp(spec.Metres(0), 3.5f, 9f) * 0.47f, beam * 0.9f, side * beam * 0.23f, 0f);
                }
                case PlacePiece.Smokehouse: return Box(parts, 1.85f, 1.55f, 4f);

                // ---- Ruins.
                case PlacePiece.RuinColumn: return Round(parts, 0.45f, spec.Metres(0));
                case PlacePiece.RuinArch: return Pair(parts, 0.4f, 0.45f, 5f, spec.Metres(0) * 0.5f + 0.3f);

                // ---- A farm's.
                case PlacePiece.FeedRack: return Box(parts, spec.Metres(0) * 0.5f + 0.25f, 0.85f, 1.9f);
                case PlacePiece.HatchingShed: return Box(parts, spec.Metres(0) * 0.5f + 0.9f, 2.55f, 2.6f, 0.6f, 0f);
                case PlacePiece.WaterTank: return Box(parts, 1.3f, 1.3f, 3.2f);

                // ---- A castle of snow.
                case PlacePiece.SnowTower: return Round(parts, Math.Clamp(spec.Metres(0), 0.8f, 3f) * 0.95f, Math.Clamp(spec.Metres(1), 1.5f, 7f));
                case PlacePiece.Snowman:
                {
                    float high = Math.Clamp(spec.Metres(0), 0.8f, 3f);
                    return Round(parts, high * 0.25f, high);
                }
                case PlacePiece.IceAltar: return Box(parts, 0.8f, 0.55f, 1.3f);

                // ---- A shrine's.
                case PlacePiece.ShrineHall: return Box(parts, spec.Metres(0) * 0.5f, spec.Metres(1) * 0.5f, PlacementIndex.Tall);
                case PlacePiece.ShrineLantern: return spec.Int(1) == 1 ? HitKind.Open : Round(parts, 0.45f, spec.Int(1) == 2 ? spec.Metres(0) * 0.65f : spec.Metres(0));

                // ---- A ring of stones.
                case PlacePiece.Menhir:
                {
                    float high = spec.Metres(0), wide = Math.Clamp(spec.Metres(1), 0.6f, 3.4f);
                    int form = spec.Int(2);
                    return form == 3 ? HitKind.Open : Box(parts, wide * 0.45f, wide * 0.3f, form == 2 ? high * 0.45f : high);
                }
                case PlacePiece.Trilithon: return Pair(parts, 0.6f, 0.45f, Math.Clamp(spec.Metres(0), 3f, 8f), Math.Clamp(spec.Metres(1), 1.4f, 3.6f) * 0.5f + 0.65f);
                case PlacePiece.Barrow:
                {
                    float hz = Math.Clamp(spec.Metres(0), 6f, 18f) * 0.5f, hx = Math.Clamp(spec.Metres(1), 4f, 9f) * 0.5f, high = Math.Clamp(spec.Metres(2), 1.2f, 3.2f);
                    Box(parts, hx * 0.68f, hz * 0.72f, high, 0f, -0.4f);
                    return Box(parts, 1.4f, 0.7f, high, 0f, hz - 0.6f);
                }
                case PlacePiece.CultHeart:
                {
                    float radius = Math.Clamp(spec.Metres(1), 1.2f, 3.5f);
                    // The frost's heart is spikes of ice standing up out of a frozen disc, thickest at its middle; the fire's and the storm's are a fire in its ring and struck stone:
                    // all three walked round and blown across. The mire's is a black pool among reeds, waded through.
                    return (Cult)spec.Int(0) switch
                    {
                        Cult.Frost => Round(parts, radius * 0.35f, 0f),
                        Cult.Mire => HitKind.Open,
                        _ => Round(parts, radius * 0.8f, 0f),
                    };
                }

                // ---- A watchtower's.
                case PlacePiece.BeaconTower:
                {
                    float radius = spec.Metres(1);
                    if (spec.Int(2) == 1) Box(parts, radius * 1.05f, radius * 1.05f, PlacementIndex.Tall);
                    else Round(parts, radius * 1.1f + 0.1f, PlacementIndex.Tall);
                    Vector3 hoist = Beacon.HoistFoot(radius);
                    return Post(parts, 0.35f, hoist.X, hoist.Z);
                }
                case PlacePiece.BeaconPyre: return Box(parts, spec.Metres(0) * 0.5f + 0.13f, 0.95f, 1.8f);
                case PlacePiece.SightStone: return Round(parts, 0.32f, 1.9f);

                // ---- A brood's pen and a prison's.
                case PlacePiece.Kennel: return Box(parts, Math.Clamp(spec.Int(0), 1, 7) * Math.Clamp(spec.Metres(1), 2f, 3.4f) * 0.5f, Math.Clamp(spec.Metres(2), 2.6f, 4.4f) * 0.5f, 3.1f);
                case PlacePiece.Cage: return Box(parts, spec.Metres(0) * 0.5f, spec.Metres(1) * 0.5f, spec.Metres(2));

                // ---- The Amber Institute's.
                case PlacePiece.Vat: return Round(parts, Laboratory.VatRadius(spec) * 1.02f, PlacementIndex.Tall);
                case PlacePiece.Boiler: return Box(parts, 2.1f, 0.8f, 7.5f, 0.15f, -0.25f);

                // ---- A commander's seat (SEATS.md).
                case PlacePiece.SeatWall:
                {
                    float hl = Math.Clamp(spec.Metres(0), 3f, 30f) * 0.5f, high = Math.Clamp(spec.Metres(1), 2f, 16f), ht = Math.Clamp(spec.Metres(2), 1f, 4f) * 0.5f;
                    bool low = spec.Int(3) == Fortress.WallLow;
                    // The wall and the foot of its batter, and the buttress or the pier at its end.
                    Box(parts, hl, ht + 0.2f, high, 0f, -0.15f);
                    return low ? Box(parts, 0.62f, ht + 0.28f, high, -hl, 0f) : Box(parts, 0.75f, 0.6f, high * 0.8f, -hl, -ht - 0.6f);
                }
                case PlacePiece.SeatTower: return Round(parts, Math.Clamp(spec.Metres(0), 2.2f, 8f) + 0.4f, PlacementIndex.Tall);
                case PlacePiece.SeatGate:
                {
                    float way = Math.Clamp(spec.Metres(0), 4f, 10f), radius = Math.Clamp(spec.Metres(1), 2.4f, 6f);
                    float offset = Fortress.GateTowerOffset(way, radius);
                    if (spec.Int(3) == Fortress.GateOpen)
                    {
                        // An open gate's two towers, and its leaves of bars standing open, which are walked round and seen and struck through.
                        for (int k = 0; k < 2; k++)
                        {
                            float side = k == 0 ? -1f : 1f, length = way * 0.5f - 0.2f;
                            Round(parts, radius + 0.4f, PlacementIndex.Tall, side * offset, 0f);
                            Vector2 swing = new Vector2(-side * MathF.Cos(Fortress.GateLeaves[k]), MathF.Sin(Fortress.GateLeaves[k]));
                            parts.Add(new HitPart(new Vector2(side * way * 0.5f, Fortress.GateHinge) + swing * (length * 0.5f), new Vector2(length * 0.5f, 0.12f), 0f, -MathF.Atan2(swing.Y, swing.X)));
                        }
                        return HitKind.Solid;
                    }
                    // Its two towers, and the ends of the block between them: the way through is open.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Round(parts, radius + 0.4f, PlacementIndex.Tall, side * offset, -0.6f);
                        Box(parts, 0.45f, Fortress.GateHalfDepth, PlacementIndex.Tall, side * (way * 0.5f + 0.45f), 0f);
                    }
                    return HitKind.Solid;
                }
                case PlacePiece.SeatSteps:
                {
                    // Its cheeks and the piers they end in: the flight between them is the way up.
                    float half = Math.Clamp(spec.Metres(0), 3f, 26f) * 0.5f, cheek = Math.Clamp(spec.Metres(4), 0.6f, 3f);
                    int count = Math.Clamp(spec.Int(1), 1, 14);
                    float run = Fortress.StepsRun(count, Math.Clamp(spec.Metres(3), 0.5f, 2f)), high = count * Math.Clamp(spec.Metres(2), 0.15f, 0.6f) + 1.1f;
                    foreach (float side in new[] { -1f, 1f }) Box(parts, cheek * 0.5f + 0.2f, (run + 2.2f) * 0.5f, high, side * (half + cheek * 0.5f), (2.2f - run) * 0.5f);
                    return HitKind.Solid;
                }
                case PlacePiece.SeatRevet: return Box(parts, Math.Clamp(spec.Metres(0), 2f, 30f) * 0.5f, 0.6f, Math.Clamp(spec.Metres(1), 0.6f, 6f) + 1.1f, 0f, -0.12f);
                case PlacePiece.GreatHall:
                {
                    float hx = Math.Clamp(spec.Metres(0), 18f, 34f) * 0.5f, hz = Math.Clamp(spec.Metres(1), 10f, 18f) * 0.5f;
                    Box(parts, hx + 0.45f, hz + 0.45f, PlacementIndex.Tall);
                    Box(parts, Fortress.PorchHalf, Fortress.PorchDepth * 0.5f, PlacementIndex.Tall, 0f, hz + Fortress.PorchDepth * 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(parts, Fortress.WingWidth * 0.5f, hz - 1.8f, 6.2f, side * (hx + Fortress.WingWidth * 0.5f), -0.8f);
                        Round(parts, Fortress.TurretRadius + 0.3f, PlacementIndex.Tall, side * hx, hz);
                    }
                    return HitKind.Solid;
                }
                case PlacePiece.TyrantStall:
                {
                    float hx = Math.Clamp(spec.Metres(0), 10f, 26f) * 0.5f, hz = Math.Clamp(spec.Metres(1), 10f, 24f) * 0.5f, high = Math.Clamp(spec.Metres(2), 5f, 12f);
                    float gate = hx * Fortress.StallGateShare;
                    // Boarded at its back and down the back half of its sides; the rest is bars, which are walked round and seen and struck through, the open leaves of its gate too.
                    Box(parts, hx + 0.3f, 0.3f, high, 0f, -hz);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(parts, 0.3f, hz * 0.5f, high, side * hx, -hz * 0.5f);
                        Rack(parts, 0.3f, hz * 0.5f, side * hx, hz * 0.5f);
                        Rack(parts, (hx - gate) * 0.5f + 0.3f, 0.3f, side * (hx + gate) * 0.5f, hz);
                        float open = Fortress.StallLeaves[side < 0f ? 0 : 1];
                        Vector2 swing = new Vector2(-side * MathF.Cos(open), MathF.Sin(open));
                        Vector2 middle = new Vector2(side * gate, hz + 0.35f) + swing * (gate * 0.5f);
                        parts.Add(new HitPart(middle, new Vector2(gate * 0.5f, 0.15f), 0f, -MathF.Atan2(swing.Y, swing.X)));
                    }
                    return HitKind.Solid;
                }
                case PlacePiece.FallenLord: return Box(parts, SeatDressing.PlinthHalf, SeatDressing.PlinthHalf, SeatDressing.PlinthHigh);
                case PlacePiece.GreatBrazier: return Round(parts, 1.25f, Math.Clamp(spec.Metres(0), 1.6f, 5f));
                case PlacePiece.LongBanner: return spec.Int(3) == 1 ? HitKind.Open : Post(parts, 0.5f);
                case PlacePiece.GatePier: return Box(parts, 0.95f, 0.95f, Math.Clamp(spec.Metres(0), 2.5f, 9f));
                case PlacePiece.FeedTrough:
                case PlacePiece.YardRing:
                case PlacePiece.Hoard:
                case PlacePiece.BilePool:
                    return HitKind.Open;

                // ---- The Ossuary's seats.
                case PlacePiece.BoneMatrix:
                {
                    // The slab, all of it, as its lip stands over a knee, and the feet of the sheerlegs over it.
                    float hw = Math.Clamp(spec.Metres(0), 14f, 40f) * 0.5f, hd = Math.Clamp(spec.Metres(1), 8f, 22f) * 0.5f, span = hw * 0.68f;
                    Box(parts, hw, hd, Math.Clamp(spec.Metres(2), 4f, 12f));
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Post(parts, 0.3f, side * (span + 1.6f), hd + 2.2f);
                        Post(parts, 0.3f, side * (span + 4.5f), 0f);
                    }
                    return HitKind.Solid;
                }
                // ---- The Reich's other seats.
                case PlacePiece.BroodDome: return Round(parts, Math.Clamp(spec.Metres(0), 8f, 18f) + 0.3f, PlacementIndex.Tall);
                case PlacePiece.BroodTanks: return Box(parts, Math.Clamp(spec.Int(0), 1, 6) * ReichWorks.TankSpacing * 0.5f + 0.4f, 1.6f, PlacementIndex.Tall);
                case PlacePiece.GreatShell: return Round(parts, Math.Clamp(spec.Metres(0), 2.2f, 5f) * 0.5f + 1.5f, 1.6f);
                case PlacePiece.BlastFurnace:
                {
                    Box(parts, ReichWorks.FurnaceFoot + 0.1f, ReichWorks.FurnaceFoot + 0.1f, PlacementIndex.Tall);
                    Box(parts, ReichWorks.StoveHalf, ReichWorks.StoveHalf, PlacementIndex.Tall, ReichWorks.StoveAt.X, ReichWorks.StoveAt.Y);
                    Box(parts, ReichWorks.StackHalf, ReichWorks.StackHalf, PlacementIndex.Tall, ReichWorks.StackAt.X, ReichWorks.StackAt.Y);
                    Box(parts, ReichWorks.HoistHalf, ReichWorks.HoistHalf, PlacementIndex.Tall, ReichWorks.HoistAt.X, ReichWorks.HoistAt.Y);
                    // Its casting bed, which is iron still bright: nothing walks over it.
                    return Rack(parts, ReichWorks.BedHalf, (ReichWorks.FurnaceBed - 1.2f) * 0.5f, 0f, ReichWorks.FurnaceFoot + (ReichWorks.FurnaceBed + 1.2f) * 0.5f);
                }
                case PlacePiece.Armature:
                {
                    float length = Math.Clamp(spec.Metres(1), 8f, 28f);
                    return Box(parts, length * 0.24f, length * 0.1f, PlacementIndex.Tall);
                }
                case PlacePiece.ScrapHeap:
                {
                    float size = Math.Clamp(spec.Metres(1), 3f, 9f);
                    // A heap that stands over a knee is walked round, and none stands over a hip, so a blow goes over it: the land's metal by its bells, coal and slag by how
                    // far across they are.
                    float tall = spec.Int(0) == 0 ? 1.6f : size * 0.25f - 0.05f;
                    return tall > StepOver ? Round(parts, size * 0.36f, MathF.Min(tall, 1.9f)) : HitKind.Open;
                }
                case PlacePiece.Colossus:
                {
                    Vector2 half = ReichField.PlinthOf(Math.Clamp(spec.Metres(1), 14f, 40f));
                    return Box(parts, half.X, half.Y, PlacementIndex.Tall);
                }
                case PlacePiece.TrophyPylon: return Box(parts, 1.05f, 1.05f, Math.Clamp(spec.Metres(0), 4f, 12f));
                case PlacePiece.Dais: return Box(parts, Math.Clamp(spec.Metres(0), 7f, 16f) * 0.5f, ReichField.DaisDepth * 0.5f, ReichField.DaisHigh);
                case PlacePiece.Pavilion:
                {
                    float w = Math.Clamp(spec.Metres(0), 14f, 26f), hz = Math.Clamp(spec.Metres(1), 8f, 14f) * 0.5f;
                    (float middle, float wing, float at) = ReichField.PavilionBays(w);
                    Box(parts, middle + 0.2f, hz + 0.2f, PlacementIndex.Tall);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(parts, wing + 0.2f, hz * 0.8f + 0.2f, PlacementIndex.Tall, side * at, -hz * 0.15f);
                        Post(parts, 0.3f, side * middle * 0.9f, hz + ReichField.PavilionFly);
                        Post(parts, 0.45f, side * (middle + 1.4f), hz + ReichField.PavilionFly + 0.3f);
                    }
                    return HitKind.Solid;
                }
                case PlacePiece.WarWagon: return Box(parts, Math.Clamp(spec.Metres(0), 5f, 9f) * 0.5f, ReichField.WagonHalf + 0.3f, 3.6f);
                case PlacePiece.CageWagon: return Rack(parts, ReichField.CageHalf + 0.4f, Math.Clamp(spec.Metres(0), 8f, 16f) * 0.5f);

                case PlacePiece.BonePale: return Rack(parts, Math.Clamp(spec.Metres(0), 3f, 16f) * 0.5f, 0.3f);
                case PlacePiece.RibArch:
                {
                    float hs = Math.Clamp(spec.Metres(0), 5f, 14f) * 0.5f;
                    Post(parts, 0.7f, -hs, 0f);
                    return Post(parts, 0.7f, hs, 0f);
                }
                case PlacePiece.GreatBarrow: return Box(parts, Math.Clamp(spec.Metres(0), 16f, 52f) * 0.4f, Math.Clamp(spec.Metres(1), 10f, 26f) * 0.33f, Math.Clamp(spec.Metres(2), 3f, 10f));
                case PlacePiece.BileVats: return Box(parts, Math.Clamp(spec.Int(0), 1, 5) * OssuaryWorks.VatSpacing * 0.5f + 0.25f, 4.85f, PlacementIndex.Tall);
                case PlacePiece.BileCradle:
                {
                    float hw = Math.Clamp(spec.Metres(0), 8f, 18f) * 0.5f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Post(parts, 0.6f, side * hw, -OssuaryWorks.CradleSpread);
                        Post(parts, 0.6f, side * hw, OssuaryWorks.CradleSpread);
                    }
                    return HitKind.Solid;
                }

                // ---- Whatever has no rule of its own is laid out as it stands, until it's given one: `Dino WorldLab hitboxes` fails on it. What its builder makes solid itself is
                // laid out as it stands too, as its builder says (ByBuilder).
                default: return HitKind.Layout;
            }
        }
    }
}
