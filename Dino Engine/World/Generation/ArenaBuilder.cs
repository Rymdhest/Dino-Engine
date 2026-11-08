using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds a colosseum, where a land's boss is fought every tenth level. It's a bowl dug into level ground: a floor of sand sunk below the land, tiers of stone stands stepping up from it all
    /// the way round to a colonnade at the rim, gates at the foot of the stands that the brood's handlers open as the fight goes against their tyrant, and a ramp cut through the stands on the
    /// south side from the waygate down to the floor. The stands are the ground itself, in terraces, so that the people made to watch stand on them as anyone stands anywhere. The camera looks
    /// north from the south, and the stands rise more gently than it looks down, so nothing on the floor is ever hidden. Read LANDS.md.
    /// </summary>
    public static class ArenaBuilder
    {
        public const float FloorRadius = 18f;
        public const float TierDepth = 2.4f;
        public const float TierRise = 1.6f;
        public const int Tiers = 3;

        /// <summary>Where the stands end and the land around them begins, from the middle.</summary>
        public static readonly float RimRadius = FloorRadius + Tiers * TierDepth;

        private const float FloorAboveWater = 3f;       // Metres the floor stands above the sea at the least, as it's dug down into the land, and the land is raised for it where it's lower.
        private const float StepEdge = 0.35f;           // Metres a tier's riser is spread over: steep enough that nobody climbs it, soft enough to draw.
        private const float LevelRadius = 40f;          // Of the level ground the bowl is dug into.
        private const float RampHalfWidth = 7f;
        private const float RampLength = 18f;
        private const int RampSteps = 9;
        private const float WaygateBack = 36f;          // From the middle, south, at the top of the ramp.
        private const float GateOut = 2.6f;             // How far in front of a gate whatever comes through it is first seen.

        /// <summary>How far from the middle a gate's face stands, at the foot of the stands.</summary>
        public const float GateFront = 18.2f;

        /// <summary>A gate's depth, from its face to the back of its passage, and its width: its middle is half its depth behind its face, and the stands are cut back to floor level under all of it.</summary>
        public const float GateDepth = 3.4f;
        public const float GateWidth = 6.8f;

        /// <summary>How far behind its face a gate's portcullis hangs.</summary>
        public const float PortcullisBack = 0.9f;
        private const float ColumnRadius = 26.6f;
        private const float ColumnStep = 15f;           // Degrees round the rim between columns.
        private const float EntranceClear = 27f;        // Degrees either side of south that nobody watches from, as that's the way in cut through the stands.
        private const float ColumnEntranceClear = 20f;  // And that no column stands in, further out, where the cut is narrower against the rim.
        private const float GateClear = 13f;            // Degrees either side of a gate that nobody watches from, as the stands are cut back round it.
        private const float SpectatorStep = 7.5f;       // Degrees round a tier between those watching.
        private const float SpectatorShare = 0.7f;      // Of the places they could stand, how many someone does.

        /// <summary>The bearings of the gates, in degrees from north, round the north half of the bowl where the camera sees them face on.</summary>
        public static readonly float[] GateBearings = { -95f, -35f, 35f, 95f };

        public static readonly Vector2 North = new Vector2(0f, 1f);

        internal static void Build(SiteBuilder b)
        {
            Vector2 c = b.Centre;
            float floor = MathF.Max(b.World.Elevation.Sample(c) - (Tiers + 1) * TierRise, WorldPlan.SeaLevel + FloorAboveWater);
            float ground = floor + (Tiers + 1) * TierRise;

            // The land it's dug into, then each tier from the top down, then the floor, so that each cuts into the one before.
            b.World.Pads.Add(new GroundPad(Footprint.Circle(c, LevelRadius), ground, 9f));
            for (int tier = Tiers; tier >= 1; tier--)
            {
                b.World.Pads.Add(new GroundPad(Footprint.Circle(c, FloorRadius + tier * TierDepth), floor + tier * TierRise, StepEdge));
            }
            b.World.Pads.Add(new GroundPad(Footprint.Circle(c, FloorRadius), floor, StepEdge));

            // The way in: a ramp cut through the south stands, rising from the floor to the land above in even steps that its edges soften into a slope.
            float stepLength = RampLength / RampSteps;
            for (int step = 0; step < RampSteps; step++)
            {
                float along = FloorRadius - 1.5f + (step + 0.5f) * stepLength;
                float height = floor + (Tiers + 1) * TierRise * (step + 0.5f) / RampSteps;
                b.World.Pads.Add(new GroundPad(Footprint.Box(c - North * along, new Vector2(RampHalfWidth, stepLength * 0.5f + 0.1f), 0f), height, 1.2f));
            }

            b.Paint(GroundPaint.Plaza, Footprint.Circle(c, RimRadius + 0.6f), 1.2f);
            b.Paint(GroundPaint.Plaza, Footprint.Box(c - North * (FloorRadius + RampLength * 0.5f), new Vector2(RampHalfWidth + 1f, RampLength * 0.5f + 2f), 0f), 2f);
            b.Paint(GroundPaint.Yard, Footprint.Circle(c, RimRadius + 6f), 5f);

            PlaceGates(b, c, floor);
            PlaceColonnade(b, c);
            PlaceWaygate(b, c);

            b.Spawn(SpawnRole.Boss, c + North * 4f);
            b.Spawn(SpawnRole.Rival, c - North * 5f);
            PlaceSpectators(b, c);

            // Nothing grows in it, stands on it or fills it with water.
            b.World.Placement.Add(Footprint.Circle(c, RimRadius + 1f), PlacementLayer.Keepout);
        }

        private static Vector2 Toward(float degrees) => new Vector2(MathF.Sin(degrees * MathF.PI / 180f), MathF.Cos(degrees * MathF.PI / 180f));

        // A gate at the foot of the stands at each bearing, facing the middle, with a passage cut back into the stands behind it, and the spot in front where what's let through is first seen.
        private static void PlaceGates(SiteBuilder b, Vector2 c, float floor)
        {
            foreach (float bearing in GateBearings)
            {
                Vector2 outward = Toward(bearing);
                Vector2 middle = c + outward * (GateFront + GateDepth * 0.5f);
                b.World.Pads.Add(new GroundPad(Footprint.Box(middle, new Vector2(GateWidth * 0.5f, GateDepth * 0.5f), YawFacing(outward)), floor, StepEdge));
                b.Place(StructureCatalog.ArenaGate, middle, YawFacing(-outward), padding: 0.1f, maxSlope: 100f);
                b.Spawn(SpawnRole.ArenaGate, c + outward * (GateFront - GateOut));
            }
        }

        // Columns round the rim, each with a brazier, except over the way in.
        private static void PlaceColonnade(SiteBuilder b, Vector2 c)
        {
            for (float bearing = 0f; bearing < 360f; bearing += ColumnStep)
            {
                if (MathF.Abs(bearing - 180f) < ColumnEntranceClear) continue;
                b.Place(StructureCatalog.ArenaColumn, c + Toward(bearing) * ColumnRadius, 0f, padding: 0.1f, maxSlope: 0.6f);
            }
        }

        // The waygate at the top of the ramp, facing down it, with lanterns either side.
        private static void PlaceWaygate(SiteBuilder b, Vector2 c)
        {
            Vector2 at = c - North * WaygateBack;
            if (b.PlaceNear(StructureCatalog.Waygate, at, YawFacing(North), 8f, out Vector2 gate, padding: 0.5f)) b.Spawn(SpawnRole.Waygate, gate + North * 2.5f);
            foreach (float side in new[] { -1f, 1f })
            {
                b.Place(StructureCatalog.StoneLantern, at + new Vector2(side * 6f, 1.5f), 0f, padding: 0.2f);
            }
        }

        // The people of the land, made to watch from the tiers, spread round them, though nobody stands over the way in or beside a gate.
        private static void PlaceSpectators(SiteBuilder b, Vector2 c)
        {
            for (int tier = 1; tier <= Tiers; tier++)
            {
                float radius = FloorRadius + (tier - 0.5f) * TierDepth;
                float offset = tier * SpectatorStep / 3f;
                for (float bearing = offset; bearing < 360f; bearing += SpectatorStep)
                {
                    if (MathF.Abs(bearing - 180f) < EntranceClear) continue;
                    float signed = bearing > 180f ? bearing - 360f : bearing;
                    if (GateBearings.Any(gate => MathF.Abs(signed - gate) < GateClear)) continue;
                    if (!b.Random.Chance(SpectatorShare)) continue;
                    float jitter = b.Random.Range(-1.5f, 1.5f);
                    b.Spawn(SpawnRole.Spectator, c + Toward(bearing + jitter) * (radius + b.Random.Range(-0.35f, 0.35f)));
                }
            }
        }
    }
}
