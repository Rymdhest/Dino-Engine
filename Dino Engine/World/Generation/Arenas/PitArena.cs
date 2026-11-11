using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Pit: the Reich's oldest kind of arena, a round pit dug into a hill. Its floor is sand, sunk nine metres below the land, and a sheer podium of sandstone stands round it with the Reich's iron along
    /// the top, broken on the north by four gatehouses that tower over it, their grilles shut on the dark, and on the south by the bay the land's waygate stands in, which the pit was dug round. Above the
    /// podium three tiers of stone stands climb to a rim of pylons with braziers, the land's people packed onto them, and the Reich's banners hang down the podium's north face and fly from the rim. A keep's
    /// pit is the same, smaller, with one tier and two gates. Read ARENAS.md.
    /// </summary>
    internal sealed class PitArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.Sandstone;

        // The podium, and the tiers above it.
        private const float PodiumTop = 6f;             // Above the floor: a person and a third, sheer.
        private const float PodiumThick = 1.8f;
        private const float Parapet = 0.95f;            // How far it stands above the first tier, behind its railing.
        private const float TierRise = 1.4f;
        private const float TierDepth = 2.6f;
        private const float RiserThick = 1f;
        private const float RiserCurb = 0.3f;           // How far a riser's bench stands above the tread behind it.

        // The gates: an opening a tyrant's kin could come through, and a block that towers over the podium so the grille has somewhere to go.
        private const float Opening = 5f;
        private const float Crown = 5.8f;
        private const float GateDepth = 3.6f;
        private const float GateWidth = 9f;
        private const float GateHeight = 10.8f;
        private const float GrilleRise = 4.6f;

        // The bay the waygate stands in, and the pylons either side of its mouth.
        private const float BayWidth = 11f;
        private const float BayDepth = 5.6f;
        private const float BayPylon = 2.2f;

        private const float RimWalk = 3f;               // Of the rim beyond the last riser, before the pylons.

        private readonly float _radius;                  // Of the floor, to the podium's face.
        private readonly int _tiers;
        private readonly float[] _gates;                 // Bearings of the gates, round the north half, where the camera sees them face on.

        public PitArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.Pit, keep, centre, keep ? "keep pit" : "pit")
        {
            _radius = keep ? 15f : 18f;
            _tiers = keep ? 1 : 3;
            _gates = keep ? new[] { -55f, 55f } : new[] { -100f, -40f, 40f, 100f };
        }

        private float TierGround(int tier) => PodiumTop - Parapet + (tier - 1) * TierRise;   // Tier 1 is the first above the podium.
        private float RimGround => TierGround(_tiers + 1);
        private float TierFace(int tier) => tier == 1 ? _radius : _radius + PodiumThick + (tier - 2) * (TierDepth + RiserThick) + TierDepth;
        private float TierBack(int tier) => (tier == 1 ? _radius + PodiumThick : TierFace(tier) + RiserThick) + TierDepth;
        private float RimFace => TierFace(_tiers + 1);
        private float RimPylons => RimFace + RiserThick + RimWalk * 0.6f;

        /// <summary>How far from the middle the whole of a pit stands.</summary>
        public float Reach => RimPylons + 2.5f;

        // How far the bay's mouth is from the middle, and the half-angle of a gate's block on the podium's circle.
        private float BayMouth => MathF.Sqrt(_radius * _radius - BayWidth * BayWidth * 0.25f);
        private float GateHalfAngle => MathHelper.RadiansToDegrees(MathF.Asin(GateWidth * 0.5f / _radius));
        // Where a gatehouse's own middle stands from the arena's, so that the corners of its front stand in front of the podium's curve.
        private float GateAt => MathF.Sqrt(_radius * _radius - GateWidth * GateWidth * 0.25f) - 0.2f + ArenaGates.Proud;

        public override void Build()
        {
            float ground = AverageGround(Reach + 12f);
            F = MathF.Max(ground - RimGround, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            // The hill it's dug into, levelled to the rim, and the waygate before anything else can take its ground.
            Level(Footprint.Circle(C, Reach + 6f), RimGround, 10f);
            Vector2 bayBack = -North * (BayMouth + BayDepth);
            Vector2 gate = Keep ? KeepGate(bayBack + North * KeepGateDeep, North, Stone, ArenaGrille.Pit) : Waygate(-North * (BayMouth + BayDepth - 2.4f), North);

            Ground();
            Podium();
            Gates();
            Bay(bayBack);
            Stands();
            Rim();
            Seal();
            People();

            Paint(GroundPaint.Sand, Footprint.Circle(C, _radius + 0.6f), 1.2f);
            Paint(GroundPaint.Sand, Footprint.Box(C - North * (BayMouth + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f, BayDepth * 0.5f + 0.6f), 0f), 0.8f);
            // The stands' treads are laid in flags, and the rim beyond them is cobbled out to where the hill begins.
            Paint(GroundPaint.Paving, Footprint.Circle(C, RimFace + 0.6f), 1f);
            Paint(GroundPaint.Plaza, Footprint.Circle(C, Reach + 1f), 2.5f);
            var clear = new List<(Vector2, float)> { (C - North * (BayMouth + BayDepth * 0.5f), 8f) };
            foreach (ArenaEntry entry in Plan.Entries) clear.Add((entry.Out, 4.5f));
            Remains(Keep ? 3 : 5, Keep ? 2 : 4, clear);
            Keepout(Footprint.Circle(C, Reach + 2f));

            Tyrants(C + North * 4.5f, C - North * 3f + East * 7f);
            if (Keep) KeepRim(Reach + 1f, RimGround, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // The floor, sunk into everything round it, and the tiers stepping up from the podium to the rim: outermost first, as each cuts into the one before.
        private void Ground()
        {
            for (int tier = _tiers; tier >= 1; tier--)
            {
                // Its edge in the middle of the riser and the bench behind it, so that the slope a metre's grid lays a step as falls inside them.
                Raise(Footprint.Circle(C, TierBack(tier) + 0.78f), TierGround(tier));
            }
            Raise(Footprint.Circle(C, _radius + Inset * 0.6f), 0f);
            Raise(Footprint.Box(C - North * (BayMouth + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f + 0.7f, BayDepth * 0.5f + 0.7f), 0f), 0f);
        }

        // The podium, round the floor between the gates and the bay, its north face hung with the Reich's banners and lit by torches, in arcs of no more than 45 degrees so that each is culled
        // on its own.
        private void Podium()
        {
            float bayHalf = MathHelper.RadiansToDegrees(MathF.Asin((BayWidth * 0.5f + BayPylon) / _radius)) - 1f;
            var stops = new List<(float From, float To)>();
            var breaks = new List<(float Middle, float Half)> { (180f, bayHalf) };
            foreach (float g in _gates) breaks.Add(((g + 360f) % 360f, GateHalfAngle - 0.6f));
            breaks.Sort((a, c) => a.Middle.CompareTo(c.Middle));
            for (int i = 0; i < breaks.Count; i++)
            {
                (float m0, float h0) = breaks[i];
                (float m1, float h1) = breaks[(i + 1) % breaks.Count];
                float from = m0 + h0, to = m1 - h1 + (i + 1 == breaks.Count ? 360f : 0f);
                if (to > from) stops.Add((from, to));
            }
            foreach ((float from, float to) in stops)
            {
                int pieces = Math.Max(1, (int)MathF.Ceiling((to - from) / 45f));
                for (int p = 0; p < pieces; p++)
                {
                    float a = from + (to - from) * p / pieces, c = from + (to - from) * (p + 1) / pieces;
                    float middle = ((a + c) * 0.5f) % 360f;
                    bool shown = middle > 270f || middle < 90f;      // The north half faces the camera.
                    // The gates' torches light the floor, so the podium's own pilasters carry banners and no fire.
                    ArenaTrim trim = ArenaTrim.Railing | ArenaTrim.Pilasters | (shown ? ArenaTrim.Banners : ArenaTrim.None);
                    ArcWall(ArenaWallForm.Podium, Stone, Vector2.Zero, _radius, a, c - a, PodiumTop, PodiumThick, TierGround(1), -1f, trim);
                }
            }
        }

        // The gatehouses, each a block of stone in the podium with its grille shut, the place in front of it where whoever's let through is first seen, and its torches.
        private void Gates()
        {
            foreach (float bearing in _gates)
            {
                Vector2 outward = Toward(bearing);
                Vector2 at = outward * GateAt;
                float yaw = YawFacing(-outward);
                Piece(new ArenaSpec(ArenaShape.Gatehouse, NextSeed(), (int)Stone, Cm(Opening), Cm(Crown), Cm(GateDepth), Cm(GateWidth), Cm(GateHeight)), at, yaw, 0f);
                foreach (Vector3 torch in ArenaGates.TorchSpots(Opening, GateWidth))
                {
                    Vector2 local = new Vector2(torch.X, torch.Z);
                    Vector2 right = new Vector2(outward.Y, -outward.X);
                    Lamp(1, at + right * -local.X - outward * local.Y, yaw, torch.Y);
                }

                // Solid all of it, so that nobody stands inside its front, and the ground under it at the floor's height to its back.
                float depth = GateDepth + ArenaGates.Proud + 0.3f;
                Solid(Footprint.Box(C + at + outward * (depth * 0.5f - ArenaGates.Proud), new Vector2(GateWidth * 0.5f, depth * 0.5f), yaw));
                Raise(Footprint.Box(C + at + outward * (GateDepth * 0.5f), new Vector2(Opening * 0.5f + 0.4f, GateDepth * 0.5f), yaw), 0f);

                // The Reich's banner flies from its top.
                Piece(new ArenaSpec(ArenaShape.Banner, NextSeed(), Cm(6.2f), Cm(4.2f), Cm(1.7f), 0), at + outward * (GateDepth * 0.45f), yaw, GateHeight);

                string grille = new ArenaSpec(ArenaShape.Portcullis, NextSeed(), (int)ArenaGrille.Pit, Cm(Opening + 0.24f), Cm(Crown + 0.15f)).Id;
                Vector2 door = at + outward * (ArenaGates.GrilleBack - ArenaGates.Proud);
                Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Portcullis, C + outward * (_radius - 2.8f), new Vector3(C.X + door.X, F, C.Y + door.Y), yaw, grille, GrilleRise));
            }
        }

        // The bay the waygate stands in, deep in the south podium, its walls high enough to hold back the second tier behind it, and pylons with braziers either side of its mouth.
        private void Bay(Vector2 back)
        {
            float height = TierGround(Math.Min(2, _tiers + 1)) + Parapet;
            Vector2 mouth = -North * BayMouth;
            Piece(new ArenaSpec(ArenaShape.Bay, NextSeed(), (int)Stone, Cm(BayWidth), Cm(BayDepth), Cm(height)), mouth, 0f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 pylon = mouth + East * side * (BayWidth * 0.5f + BayPylon * 0.5f) + North * 0.5f;
                Pylon(Stone, pylon, BayPylon, height + 1.6f, 1, 0f, 0f);
            }
            foreach (Vector3 brazier in ArenaDressing.BayBraziers(BayWidth)) Brazier(mouth + new Vector2(brazier.X, brazier.Z) - North * BayDepth * 0.55f, 0, 0.9f, 0f);

            // The waygate stands against the bay's back, and nothing walks behind it: anyone there would be hidden from the camera by the wall that holds the stands up.
            Solid(Footprint.Box(C + back + North * 1.2f, new Vector2(BayWidth * 0.5f, 1.2f), 0f));
        }

        // The tiers' risers, each holding up the tier behind it, broken where a gatehouse or the bay stands in them.
        private void Stands()
        {
            for (int tier = 2; tier <= _tiers + 1; tier++)
            {
                float face = TierFace(tier);
                var breaks = new List<(float Middle, float Half)>();
                // Only a riser the bay's walls reach is broken by it: one behind the bay runs on over it.
                if (face < BayMouth + BayDepth + 1.7f) breaks.Add((180f, MathHelper.RadiansToDegrees(MathF.Atan2(BayWidth * 0.5f + 1.2f, face)) + 1f));
                foreach (float g in _gates)
                {
                    // A gatehouse reaches back past the podium into the first tier; whatever's behind it is clear.
                    float back = GateAt + GateDepth + 0.3f;
                    if (back > face - 0.3f) breaks.Add(((g + 360f) % 360f, MathHelper.RadiansToDegrees(MathF.Atan2(GateWidth * 0.5f + 0.4f, face))));
                }
                breaks.Sort((a, c) => a.Middle.CompareTo(c.Middle));
                for (int i = 0; i < breaks.Count; i++)
                {
                    (float m0, float h0) = breaks[i];
                    (float m1, float h1) = breaks[(i + 1) % breaks.Count];
                    float from = m0 + h0, to = m1 - h1 + (i + 1 == breaks.Count ? 360f : 0f);
                    if (to - from < 2f) continue;
                    int pieces = Math.Max(1, (int)MathF.Ceiling((to - from) / 50f));
                    for (int p = 0; p < pieces; p++)
                    {
                        float a = from + (to - from) * p / pieces, c = from + (to - from) * (p + 1) / pieces;
                        ArcWall(ArenaWallForm.Riser, Stone, Vector2.Zero, face, a, c - a, TierGround(tier) + RiserCurb - TierGround(tier - 1), RiserThick, TierGround(tier) - TierGround(tier - 1), -1f, ArenaTrim.None, TierGround(tier - 1));
                    }
                }
            }
        }

        // The rim: pylons all the way round but over the bay, a brazier on every other one, and between them on the north, where the camera sees them face on, poles flying the Reich's banner.
        // Every pylon can't burn: a light's cost is every pixel it reaches, and a ring of them was more than the rest of the pit together.
        private void Rim()
        {
            const float Step = 15f;
            for (int k = 0; k < 24; k++)
            {
                float bearing = k * Step;
                if (Near(bearing, 180f, 20f)) continue;
                Vector2 at = Toward(bearing) * RimPylons;
                int crown = k % 2 == 0 ? 1 : Near(bearing, 0f, 75f) ? 2 : 0;
                Pylon(Stone, at, 1.5f, 4.6f, crown, YawFacing(-at), RimGround);
            }
        }

        // The seal: the inside face of the podium all round, broken only by the bay, whose walls carry it round the waygate. Gatehouses are solid of themselves.
        private void Seal()
        {
            float bayHalf = MathHelper.RadiansToDegrees(MathF.Asin(BayWidth * 0.5f / _radius));
            SealArc(_radius, 180f + bayHalf, 360f - 2f * bayHalf, PodiumThick);
            float mouth = BayMouth;
            var bay = new List<Vector2>
            {
                new Vector2(-BayWidth * 0.5f, -mouth), new Vector2(-BayWidth * 0.5f, -mouth - BayDepth), new Vector2(BayWidth * 0.5f, -mouth - BayDepth), new Vector2(BayWidth * 0.5f, -mouth),
            };
            SealLine(bay, 1.4f, closed: false);

            // The bounds: round the circle from the bay's east corner, then into the bay and out.
            Plan.Bounds.AddRange(ArcPoints(_radius, 180f - bayHalf, -(360f - 2f * bayHalf)));
            Plan.Bounds.Add(new Vector2(-BayWidth * 0.5f, -mouth - BayDepth));
            Plan.Bounds.Add(new Vector2(BayWidth * 0.5f, -mouth - BayDepth));
            for (int i = 0; i < Plan.Bounds.Count; i++) Plan.Bounds[i] += C;
        }

        // The land's people on every tier, but where a gatehouse or the bay stands.
        private void People()
        {
            for (int tier = 1; tier <= _tiers; tier++)
            {
                float radius = (tier == 1 ? _radius + PodiumThick : TierFace(tier) + RiserThick) + TierDepth * 0.55f;
                float step = 360f / (MathF.Tau * radius / 3.4f);
                float gateClear = MathHelper.RadiansToDegrees(MathF.Atan2(GateWidth * 0.5f + 1.2f, radius));
                bool Clear(float bearing) => !Near(bearing, 180f, 26f) && (tier > 1 || !_gates.Any(g => Near(bearing, (g + 360f) % 360f, gateClear)));
                Watchers(radius, step, tier * step / 3f, Clear, share: 0.64f);
            }
        }
    }
}
