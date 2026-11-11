using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Sun-Temple: a temple the land raised to its sun long before the Reich came, which the Reich made its hall of assembly. Its court is sunk into the land, paved, with rings inlaid at its heart and
    /// sand blown into its corners, and walled in golden stone; behind its north wall a platform climbs in three tiers, where the Reich's commanders stand, to the land's own great disc of gold in its frame,
    /// with fires either side. Colonnades run along the east and west of the rim, the Reich's oxblood banners hung between their columns where the land's own once were, and the land's people stand in them
    /// to watch. Bronze doors in the east and west walls let the brood in, and on the rim stand the brass mirrors the Reich's priests turn on the floor below. The waygate the temple was built round stands
    /// in a bay in the south wall. A keep's temple is the same, smaller: no colonnades, one tier and a lesser disc. Read ARENAS.md.
    /// </summary>
    internal sealed class SunTempleArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.TempleGold;

        private const float WallTop = 5.6f;
        private const float WallThick = 1.8f;
        private const float Parapet = 0.95f;
        private const float TierRise = 3.6f;
        private const float TierDepth = 4f;

        private const float PortalWidth = 5.2f;
        private const float PortalHeight = 6.2f;
        private const float PortalDepth = 3.6f;
        private const float DoorSwing = 100f;

        private const float BayWidth = 11f;
        private const float BayDepth = 5.6f;
        private const float BayPylon = 2.2f;

        private readonly float _hx, _hz;                  // Half the court's length east and west, and its depth north and south.
        private readonly int _tiers;
        private readonly float _disc;

        public SunTempleArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.SunTemple, keep, centre, keep ? "keep temple" : "sun temple")
        {
            _hx = keep ? 16f : 22f;
            _hz = keep ? 12.5f : 16f;
            _tiers = keep ? 1 : 3;
            _disc = keep ? 2.8f : 4.2f;
        }

        private float RimGround => WallTop - Parapet;
        private float TierTop(int tier) => WallTop + (tier - 1) * TierRise;       // A tier's front's top: the first is the court's north wall.
        private float TierGround(int tier) => TierTop(tier) - Parapet;
        private float TierFront(int tier) => _hz + (tier - 1) * TierDepth;
        private float TierHalf(int tier) => _hx - (tier - 1) * 6f;

        private float PlatformBack => TierFront(_tiers) + TierDepth * 2.2f;

        /// <summary>How far from the middle the whole of it reaches.</summary>
        public float Reach => MathF.Max(MathF.Sqrt(_hx * _hx + _hz * _hz) + 10f, PlatformBack + 3f);

        public override void Build()
        {
            float ground = AverageGround(Reach + 8f);
            F = MathF.Max(ground - RimGround, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            Level(Footprint.Circle(C, Reach + 6f), RimGround, 9f);
            Vector2 gate = Keep ? KeepGate(new Vector2(0f, -(_hz + BayDepth - KeepGateDeep)), North, Stone, ArenaGrille.Pit) : Waygate(new Vector2(0f, -(_hz + BayDepth - 2.4f)), North);

            Ground();
            Walls();
            Portals();
            Bay();
            Platform();
            if (!Keep) Colonnades();
            Mirrors();
            Rim();
            Seal();
            Court(gate);
            People();

            Keepout(Footprint.Circle(C, Reach + 2f));
            Tyrants(C + North * 4f, C - North * 3f + East * 8f);
            if (Keep) KeepRim(Reach + 1f, RimGround, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // The court sunk into the rim, its edge inside the walls; the bay; the platform's terraces stepping up behind the north wall; and each portal's passage at the court's level.
        private void Ground()
        {
            // Lowest first, as each cuts into the one before: each from its front wall back to the platform's back.
            float back = PlatformBack;
            for (int tier = 2; tier <= _tiers; tier++)
            {
                float front = TierFront(tier) + 0.78f;
                Raise(Footprint.Box(C + North * ((front + back) * 0.5f), new Vector2(TierHalf(tier) - 0.7f, (back - front) * 0.5f), 0f), TierGround(tier));
            }
            Raise(Footprint.Box(C, new Vector2(_hx + 0.88f, _hz + 0.88f), 0f), 0f);
            Raise(Footprint.Box(C - North * (_hz + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f + 0.7f, BayDepth * 0.5f + 0.7f), 0f), 0f);
        }

        // The court's walls, east, west and south, broken by the portals and the bay; the north wall is the platform's first tier.
        private void Walls()
        {
            float bayHalf = BayWidth * 0.5f + BayPylon - 0.4f;
            RunWall(ArenaWallForm.Podium, Stone, new Vector2(-_hx, -_hz), new Vector2(-bayHalf, -_hz), North, WallTop, WallThick, RimGround, ArenaTrim.Pilasters);
            RunWall(ArenaWallForm.Podium, Stone, new Vector2(bayHalf, -_hz), new Vector2(_hx, -_hz), North, WallTop, WallThick, RimGround, ArenaTrim.Pilasters);
            float portalHalf = PortalWidth * 0.5f + 1.4f - 0.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 face = -East * side;
                RunWall(ArenaWallForm.Podium, Stone, new Vector2(side * _hx, -_hz - WallThick), new Vector2(side * _hx, -portalHalf), face, WallTop, WallThick, RimGround, ArenaTrim.Pilasters | ArenaTrim.Banners);
                RunWall(ArenaWallForm.Podium, Stone, new Vector2(side * _hx, portalHalf), new Vector2(side * _hx, _hz + WallThick), face, WallTop, WallThick, RimGround, ArenaTrim.Pilasters | ArenaTrim.Banners);
            }
        }

        // A portal with bronze doors in the middle of the east and west walls, its doors shut, and the court in front of it where whoever's let through is first seen.
        private void Portals()
        {
            string leaf = new ArenaSpec(ArenaShape.DoorLeaf, NextSeed(), Cm(PortalWidth * 0.5f), Cm(PortalHeight)).Id;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 outward = East * side;
                Vector2 at = outward * (_hx - 0.25f);
                float yaw = YawFacing(-outward);
                Piece(new ArenaSpec(ArenaShape.Portal, NextSeed(), (int)Stone, Cm(PortalWidth), Cm(PortalHeight), Cm(PortalDepth)), at, yaw, 0f);
                float half = PortalWidth * 0.5f + 1.4f;
                Solid(Footprint.Box(C + at + outward * (PortalDepth * 0.5f), new Vector2(half, PortalDepth * 0.5f + 0.25f), yaw));
                Raise(Footprint.Box(C + at + outward * (PortalDepth * 0.5f), new Vector2(PortalWidth * 0.5f + 0.3f, PortalDepth * 0.5f), yaw), 0f);
                Vector2 hinge = at + outward * 0.3f;
                Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Doors, C + at - outward * 3f, new Vector3(C.X + hinge.X, F, C.Y + hinge.Y), yaw, leaf, DoorSwing, PortalWidth));
                // Torches either side of it, on its face.
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 right = new Vector2(outward.Y, -outward.X);
                    Lamp(1, at + right * s * (half - 0.7f) - outward * 0.25f, yaw, 3.6f);
                }
            }
        }

        // The bay the waygate stands in, in the middle of the south wall, with pylons and braziers at its mouth.
        private void Bay()
        {
            // As high as holds the rim behind it, its own bank at the rim's height.
            float height = RimGround + 0.9f;
            Vector2 mouth = new Vector2(0f, -_hz);
            Piece(new ArenaSpec(ArenaShape.Bay, NextSeed(), (int)Stone, Cm(BayWidth), Cm(BayDepth), Cm(height)), mouth, 0f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                Pylon(Stone, mouth + East * side * (BayWidth * 0.5f + BayPylon * 0.5f) + North * 0.5f, BayPylon, height + 1.6f, 1, 0f, 0f);
            }
            foreach (Vector3 brazier in ArenaDressing.BayBraziers(BayWidth)) Brazier(mouth + new Vector2(brazier.X, brazier.Z) - North * BayDepth * 0.55f, 1, 0.85f, 0f);
            Solid(Footprint.Box(C - North * (_hz + BayDepth) + North * 1.2f, new Vector2(BayWidth * 0.5f, 1.2f), 0f));
        }

        // The platform behind the north wall, climbing in tiers to the land's disc, each tier's front hung with the Reich's banners, and its ends walled.
        private void Platform()
        {
            for (int tier = 1; tier <= _tiers; tier++)
            {
                float front = TierFront(tier), half = TierHalf(tier);
                float below = tier == 1 ? 0f : TierGround(tier - 1);
                float top = TierTop(tier) - below, soil = TierGround(tier) - below;
                RunWall(ArenaWallForm.Podium, Stone, new Vector2(-half, front), new Vector2(half, front), -North, top, WallThick, soil, ArenaTrim.Pilasters | ArenaTrim.Banners, below);
                if (tier > 1)
                {
                    // Its ends, facing out east and west, from its front back to the platform's back.
                    foreach (float side in new[] { -1f, 1f })
                    {
                        RunWall(ArenaWallForm.Parapet, Stone, new Vector2(side * half, front), new Vector2(side * half, PlatformBack + 0.6f), East * side, top + 0.4f, 1.2f, -1f, ArenaTrim.None, below);
                    }
                }
            }
            // The disc on the top tier, facing the court, with the Reich's banners hung either side of it on their own masts.
            float discAt = TierFront(_tiers) + TierDepth * 0.9f;
            Piece(new ArenaSpec(ArenaShape.SunDisc, NextSeed(), (int)Stone, Cm(_disc)), North * discAt, YawFacing(-North), TierGround(_tiers));
            if (!Keep)
            {
                float apart = _disc + 0.9f + 1.6f + 3f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 mast = North * (discAt - 0.6f) + East * side * apart;
                    Piece(new ArenaSpec(ArenaShape.Banner, NextSeed(), Cm(14f), Cm(8f), Cm(2.6f), 0), mast, YawFacing(-North), TierGround(_tiers));
                }
            }
        }

        // Colonnades along the east and west of the rim, their fronts to the court, set back from its walls.
        private void Colonnades()
        {
            float length = _hz * 2f - 4f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Each faces the court across the wall: on the east its own x runs south, and on the west north.
                Vector2 face = -East * side;
                float yaw = YawFacing(face);
                Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
                Vector2 start = new Vector2(side * (_hx + WallThick + 2.4f), 0f) - along * (length * 0.5f);
                Piece(new ArenaSpec(ArenaShape.Colonnade, NextSeed(), (int)Stone, Cm(length), Cm(7.2f), Cm(3.4f)), start, yaw, RimGround);
            }
        }

        // The mirrors: at the court's corners and on the south rim, each on its pedestal, aimed into the court, which the game turns and looses (ArenaSystem).
        private void Mirrors()
        {
            string mirror = new ArenaSpec(ArenaShape.Mirror, NextSeed(), Cm(Keep ? 1.6f : 2f)).Id;
            var spots = new List<Vector2>();
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    if (Keep && sz > 0f) continue;
                    spots.Add(new Vector2(sx * (_hx + WallThick + 2.2f), sz * (_hz + WallThick + 2.2f)));
                }
                if (!Keep) spots.Add(new Vector2(sx * _hx * 0.55f, -(_hz + WallThick + 2.4f)));
            }
            foreach (Vector2 at in spots)
            {
                float yaw = YawFacing(-at.Normalized());
                Piece(new ArenaSpec(ArenaShape.Pedestal, NextSeed(), (int)Stone), at, yaw, RimGround);
                Solid(Footprint.Circle(C + at, 1.3f));
                Plan.Pieces.Add(new ArenaPiece(ArenaPieceKind.Mirror, new Vector3(C.X + at.X, F + RimGround + TemplePieces.MirrorPivot, C.Y + at.Y), yaw, mirror));
            }
        }

        // Braziers along the rim between the mirrors, on the south and at the court's corners.
        private void Rim()
        {
            foreach (float x in new[] { -0.82f, 0.82f })
            {
                Brazier(new Vector2(x * _hx, -(_hz + WallThick + 2.4f)), 0, 1.05f, RimGround);
            }
        }

        // The seal round the court's walls, broken only by the bay; the portals are solid of themselves.
        private void Seal()
        {
            var line = new List<Vector2>
            {
                new Vector2(-BayWidth * 0.5f, -_hz), new Vector2(-_hx, -_hz), new Vector2(-_hx, _hz), new Vector2(_hx, _hz), new Vector2(_hx, -_hz), new Vector2(BayWidth * 0.5f, -_hz),
            };
            SealLine(line, WallThick, closed: false);
            var bay = new List<Vector2> { new Vector2(-BayWidth * 0.5f, -_hz), new Vector2(-BayWidth * 0.5f, -_hz - BayDepth), new Vector2(BayWidth * 0.5f, -_hz - BayDepth), new Vector2(BayWidth * 0.5f, -_hz) };
            SealLine(bay, 1.4f, closed: false);
            foreach (Vector2 p in line) Plan.Bounds.Add(C + p);
            Plan.Bounds.Add(C + new Vector2(BayWidth * 0.5f, -_hz - BayDepth));
            Plan.Bounds.Add(C + new Vector2(-BayWidth * 0.5f, -_hz - BayDepth));
        }

        // The court: paving laid in the gate's stone, rings inlaid at its heart, sand blown into its corners and along its walls, and what's left of the fights before.
        private void Court(Vector2 gate)
        {
            Paint(GroundPaint.Paving, Footprint.Box(C, new Vector2(_hx + 0.6f, _hz + 0.6f), 0f), 0.8f);
            Paint(GroundPaint.Paving, Footprint.Box(C - North * (_hz + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f, BayDepth * 0.5f + 0.6f), 0f), 0.6f);
            Piece(new ArenaSpec(ArenaShape.Inlay, NextSeed(), (int)Stone, Cm(Keep ? 6f : 8.5f)), Vector2.Zero, 0f, 0f);
            for (int i = 0; i < (Keep ? 5 : 9); i++)
            {
                // Drifts against the walls, deepest in the corners, where the wind leaves what it carries.
                float t = Random.Float();
                Vector2 at = Random.Int(4) switch
                {
                    0 => new Vector2(-_hx + 1.5f, (t * 2f - 1f) * _hz),
                    1 => new Vector2(_hx - 1.5f, (t * 2f - 1f) * _hz),
                    2 => new Vector2((t * 2f - 1f) * _hx, _hz - 1.5f),
                    _ => new Vector2((t * 2f - 1f) * _hx, -_hz + 1.5f),
                };
                Paint(GroundPaint.Sand, Footprint.Circle(C + at, Random.Range(2.5f, 5f)), 2.5f);
            }
            foreach (Vector2 corner in new[] { new Vector2(-_hx, -_hz), new Vector2(_hx, -_hz), new Vector2(-_hx, _hz), new Vector2(_hx, _hz) }) Paint(GroundPaint.Sand, Footprint.Circle(C + corner * 0.94f, 5.5f), 3f);
            Paint(GroundPaint.Plaza, Footprint.Circle(C, Reach + 1f), 2.5f);
            var clear = new List<(Vector2, float)> { (C + gate, 8f), (C, 9f) };
            foreach (ArenaEntry entry in Plan.Entries) clear.Add((entry.Out, 5f));
            Remains(Keep ? 2 : 4, Keep ? 1 : 3, clear);
        }

        // The land's people, in the colonnades and along the south rim, and the Reich's assembly on the platform's tiers.
        private void People()
        {
            if (!Keep)
            {
                // Two rows of them down each colonnade's walk, between its columns.
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float back in new[] { 1.1f, 2.4f })
                    {
                        for (float z = -_hz + 2.2f + back * 0.5f; z < _hz - 2.2f; z += 2.6f)
                        {
                            if (!Random.Chance(0.7f)) continue;
                            B.Spawn(SpawnRole.Spectator, C + new Vector2(side * (_hx + WallThick + 2.4f + back), z + Random.Range(-0.4f, 0.4f)));
                        }
                    }
                }
                for (int tier = 2; tier <= _tiers; tier++)
                {
                    float half = TierHalf(tier) - 2f;
                    for (float x = -half; x <= half; x += 2.8f)
                    {
                        if (MathF.Abs(x) < _disc + 3f && tier == _tiers) continue;
                        if (!Random.Chance(0.65f)) continue;
                        B.Spawn(SpawnRole.Spectator, C + new Vector2(x + Random.Range(-0.4f, 0.4f), TierFront(tier) + WallThick + 1.6f));
                    }
                }
            }
            foreach (float south in new[] { _hz + WallThick + 4.6f, _hz + WallThick + 7.2f })
            for (float x = -_hx; x <= _hx; x += 2.7f)
            {
                if (MathF.Abs(x) < BayWidth * 0.5f + BayPylon + 1f) continue;
                // Not on a mirror's pedestal or a brazier.
                if (MathF.Abs(MathF.Abs(x) - _hx * 0.55f) < 2.2f || MathF.Abs(MathF.Abs(x) - _hx * 0.82f) < 1.8f) continue;
                if (!Random.Chance(0.72f)) continue;
                B.Spawn(SpawnRole.Spectator, C + new Vector2(x + Random.Range(-0.5f, 0.5f), -south + Random.Range(-0.6f, 0.6f)));
            }
            float north = TierFront(1) + WallThick + 1.4f;
            for (float x = -_hx + 2f; x <= _hx - 2f; x += 2.8f)
            {
                if (!Keep && MathF.Abs(x) < TierHalf(2) + 0.8f) continue;
                if (!Random.Chance(0.7f)) continue;
                B.Spawn(SpawnRole.Spectator, C + new Vector2(x + Random.Range(-0.4f, 0.4f), north));
            }
        }
    }
}
