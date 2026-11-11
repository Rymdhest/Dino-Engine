using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Cathedral of the Iron Apex, the Reich's to its creed that the strongest blood is owed the world, and to the tyrant it holds up as proof. Its nave is sunk below the land, paved in flags, between
    /// walls of dark granite with clustered piers standing out of them; iron screens stand on the walls between the piers, and behind them the faithful in their pews look down on the floor. Arches of
    /// riveted black iron spring from the piers across the nave, which is open to the night, and from their crowns hang iron wheels of candles, which come down. To the north, where the camera looks, the
    /// apse rises with tall windows of oxblood and amber glass round the throne-cage where the tyrant is anointed, and over it the Iron Apex, the first tyrant's skull in black iron with amber in its eye.
    /// Gates under the screens let the brood in. The south end stays low, so that the camera sees the whole nave, and the waygate the Reich built its cathedral at stands there. It's always night here.
    /// A keep's is the same, shorter, with two arches and a lesser apse. Read ARENAS.md.
    /// </summary>
    internal sealed class CathedralArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.DarkGranite;

        // The nave, sunk at least four metres below the aisles and the land round it, so that no grid node on its edge splits the drop into steps that can be walked (ARENAS.md).
        private const float Sunk = 4.6f;
        private const float WallTop = 5.4f;
        private const float WallThick = 1.8f;
        private const float PierAt = 9.5f;              // From the nave's middle line, where the piers stand, half in the walls.
        private const float PierHeight = 14f;
        private const float ArchRise = 7f;
        private const float AisleWidth = 6.4f;

        private const float GateWidth = 3.6f;
        private const float GateSpring = 3f;
        private const float GateDepth = 3.4f;
        private const float GrilleRise = 4.6f;

        private readonly float _half;                    // Half the nave's width.
        private readonly float _south, _apse;            // How far south the nave goes, and where the apse begins.
        private readonly float _apseRadius;
        private readonly float[] _piers;
        private readonly float[] _arches;
        private readonly float[] _gates;

        public CathedralArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.Cathedral, keep, centre, keep ? "keep cathedral" : "cathedral")
        {
            _half = 10f;
            _south = keep ? -15f : -22f;
            _apse = keep ? 10f : 16f;
            _apseRadius = 10f;
            float step = 5.6f;
            var piers = new List<float>();
            for (float z = _south + 2.8f; z < _apse; z += step) piers.Add(z);
            _piers = piers.ToArray();
            _arches = _piers.Where((_, i) => i % 2 == 0).ToArray();
            // A gate stands in the wall where a pier would, an odd one, so that no arch loses its footing and the gate has the whole bay either side.
            _gates = keep ? new[] { _piers[1] } : new[] { _piers[3], _piers[5] };
        }

        /// <summary>How far from the middle the whole of it reaches.</summary>
        public float Reach => MathF.Max(MathF.Sqrt((_half + AisleWidth + 2f) * (_half + AisleWidth + 2f) + _south * _south), _apse + _apseRadius + 6f);

        public override void Build()
        {
            float ground = AverageGround(Reach + 6f);
            F = MathF.Max(ground - Sunk, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            Level(Footprint.Circle(C, Reach + 6f), Sunk, 9f);
            Raise(Footprint.Box(C + North * ((_south + _apse) * 0.5f), new Vector2(_half + 0.9f, (_apse - _south) * 0.5f + 0.9f), 0f), 0f);
            Raise(Footprint.Circle(C + North * _apse, _apseRadius + 0.8f), 0f);
            Vector2 gate = Keep ? KeepGate(North * (_south + KeepGateDeep), North, Stone, ArenaGrille.Screen) : Waygate(North * (_south + 4.2f), North);
            Solid(Footprint.Box(C + gate - North * 1.2f, new Vector2(4.4f, 1.2f), 0f));

            Walls();
            Gates();
            Piers();
            Apse();
            Aisles();
            Seal();
            Nave(gate);
            People();

            Keepout(Footprint.Circle(C, Reach + 2f));
            Tyrants(C + North * (_apse - 4f), C + North * (_apse - 10f) + East * 5f);
            if (Keep) KeepRim(Reach + 1f, Sunk, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // The nave's walls: east and west from the south end to the apse, broken by the gates, and the low wall across the south end.
        private void Walls()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float from = _south - WallThick;
                foreach (float g in _gates.OrderBy(z => z))
                {
                    float to = g - (GateWidth * 0.5f + 1.1f) + 0.3f;
                    RunWall(ArenaWallForm.Podium, Stone, new Vector2(side * _half, from), new Vector2(side * _half, to), -East * side, WallTop, WallThick, Sunk, ArenaTrim.None);
                    from = g + (GateWidth * 0.5f + 1.1f) - 0.3f;
                }
                RunWall(ArenaWallForm.Podium, Stone, new Vector2(side * _half, from), new Vector2(side * _half, _apse), -East * side, WallTop, WallThick, Sunk, ArenaTrim.None);
            }
            RunWall(ArenaWallForm.Podium, Stone, new Vector2(-_half, _south), new Vector2(_half, _south), North, WallTop, WallThick, Sunk, ArenaTrim.Pilasters | ArenaTrim.Banners);
        }

        // A gate under the screens in each wall, its grille down, and the nave in front of it where whoever's let through is first seen.
        private void Gates()
        {
            foreach (float z in _gates)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 outward = East * side;
                    Vector2 at = new Vector2(side * (_half - 0.22f), z);
                    float yaw = YawFacing(-outward);
                    Piece(new ArenaSpec(ArenaShape.ScreenGate, NextSeed(), (int)Stone, Cm(GateWidth), Cm(GateSpring), Cm(GateDepth)), at, yaw, 0f);
                    float half = GateWidth * 0.5f + 1.1f;
                    Solid(Footprint.Box(C + at + outward * (GateDepth * 0.5f), new Vector2(half, GateDepth * 0.5f + 0.22f), yaw));
                    Raise(Footprint.Box(C + at + outward * (GateDepth * 0.5f), new Vector2(GateWidth * 0.5f + 0.3f, GateDepth * 0.5f), yaw), 0f);
                    string grille = new ArenaSpec(ArenaShape.Portcullis, NextSeed(), (int)ArenaGrille.Screen, Cm(GateWidth + 0.1f), Cm(GateSpring + 2.2f)).Id;
                    Vector2 door = at + outward * 0.1f;
                    Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Screen, C + at - outward * 2.6f, new Vector3(C.X + door.X, F, C.Y + door.Y), yaw, grille, GrilleRise));
                }
            }
        }

        // The piers, half in the walls, the screens on the walls between them, a torch on the face of every other one, and the iron arches springing from every other pair across the nave with a
        // chandelier hung from each crown on its chain.
        private void Piers()
        {
            for (int i = 0; i < _piers.Length; i++)
            {
                float z = _piers[i];
                if (_gates.Contains(z)) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 at = new Vector2(side * PierAt, z);
                    Piece(new ArenaSpec(ArenaShape.GothicPier, NextSeed(), (int)Stone, Cm(PierHeight)), at, 0f, 0f);
                    Solid(Footprint.Circle(C + at, 1.15f));
                    if (i % 2 == 1) Lamp(1, at - East * side * 1.2f, YawFacing(-East * side), 6.4f);
                }
            }
            // The screens on the walls between the piers, but beside the gates, whose blocks stand in their place.
            for (int i = 0; i + 1 < _piers.Length; i++)
            {
                if (_gates.Contains(_piers[i]) || _gates.Contains(_piers[i + 1])) continue;
                float z0 = _piers[i] + 1.15f, z1 = _piers[i + 1] - 1.15f;
                foreach (float side in new[] { -1f, 1f })
                {
                    // A screen's own x runs along the wall: from the south on the west side and from the north on the east, so that its front faces the nave.
                    Vector2 a = new Vector2(side * (_half + 0.6f), side > 0f ? z1 : z0);
                    float yaw = YawFacing(-East * side);
                    Piece(new ArenaSpec(ArenaShape.Screen, NextSeed(), Cm(z1 - z0), Cm(3.6f)), a, yaw, WallTop);
                }
            }
            for (int i = 0; i < _arches.Length; i++)
            {
                float z = _arches[i];
                Piece(new ArenaSpec(ArenaShape.IronArch, NextSeed(), Cm(PierAt * 2f), Cm(ArchRise)), North * z, 0f, PierHeight + 0.25f);
                if (i == 0) continue;
                // The chandelier hangs from the arch's crown on its chain; the chain's a part of the building and the wheel a thing of its own, as it comes down.
                float crown = PierHeight + 0.25f + ArchRise - CathedralPieces.RingBelow;
                float hook = PierHeight - 3f;
                Piece(new ArenaSpec(ArenaShape.Chain, NextSeed(), Cm(crown - hook)), North * z, 0f, crown);
                string chandelier = new ArenaSpec(ArenaShape.Chandelier, NextSeed(), Cm(Keep ? 2.2f : 2.7f)).Id;
                Plan.Pieces.Add(new ArenaPiece(ArenaPieceKind.Chandelier, new Vector3(C.X, F + hook, C.Y + z), 0f, chandelier));
            }
        }

        // The apse to the north: its wall of windows round the throne-cage, the Iron Apex high on its middle, and the light of its windows thrown into the nave.
        private void Apse()
        {
            Vector2 centre = North * _apse;
            float height = Keep ? 13f : 17f;
            int windows = 6;
            Piece(new ArenaSpec(ArenaShape.Apse, NextSeed(), (int)Stone, Cm(_apseRadius), -90, 180, Cm(height), windows), centre, 0f, 0f);
            foreach ((float bearing, float y) in CathedralPieces.ApseWindows(-90f, 180f, height, windows))
            {
                Vector2 inward = -Toward(bearing);
                Lamp(5, centre - inward * (_apseRadius - 1.6f), YawFacing(inward), y);
            }
            Piece(new ArenaSpec(ArenaShape.Throne, NextSeed(), (int)Stone, Cm(7f)), centre + North * 2f, YawFacing(-North), 0f);
            Solid(Footprint.Box(C + centre + North * 2f, new Vector2(3.5f, 2.7f), 0f), 6f);
            Piece(new ArenaSpec(ArenaShape.IronSkull, NextSeed(), Cm(Keep ? 5f : 7f)), centre + North * (_apseRadius - 0.1f), YawFacing(-North), height * 0.62f);
        }

        // The aisles beyond the screens, on the land's own level: the outer walls, pews facing the nave, candle stands between them, and the land's people and the faithful standing in them.
        private void Aisles()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float outer = side * (_half + AisleWidth + WallThick);
                RunWall(ArenaWallForm.Parapet, Stone, new Vector2(outer, _south - 1f), new Vector2(outer, _apse), East * side, 3.2f, 1.2f, -1f, ArenaTrim.None, Sunk);
                for (float z = _south + 2f; z < _apse - 3f; z += 9f)
                {
                    // Pews face the nave: their own x runs along the aisle and their own z towards the nave.
                    float yaw = YawFacing(-East * side);
                    Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
                    Vector2 at = new Vector2(side * (_half + WallThick + 1.4f), z) - along * 2.6f;
                    Piece(new ArenaSpec(ArenaShape.Pews, NextSeed(), Cm(5.2f), 2), at, yaw, Sunk);
                    Lamp(3, new Vector2(side * (_half + AisleWidth - 0.4f), z + 5f), 0f, Sunk);
                }
            }
        }

        // The seal round the nave's walls and the apse.
        private void Seal()
        {
            var line = new List<Vector2> { new(-_half, _apse), new(-_half, _south), new(_half, _south), new(_half, _apse) };
            foreach (Vector2 p in ArcPoints(_apseRadius, 90f, -180f, 6f)) line.Add(North * _apse + p);
            SealLine(line, WallThick, closed: true);
            foreach (Vector2 p in line) Plan.Bounds.Add(C + p);
        }

        // The nave's floor, flagged, and what's on it: old blood before the altar, and bones by its walls.
        private void Nave(Vector2 gate)
        {
            Paint(GroundPaint.Paving, Footprint.Box(C + North * ((_south + _apse) * 0.5f), new Vector2(_half + 0.6f, (_apse - _south) * 0.5f + 0.6f), 0f), 0.8f);
            Paint(GroundPaint.Paving, Footprint.Circle(C + North * _apse, _apseRadius + 0.6f), 0.8f);
            Paint(GroundPaint.Paving, Footprint.Circle(C, Reach), 3f);
            var clear = new List<(Vector2, float)> { (C + gate, 8f), (C + North * (_apse + 2f), 5f) };
            foreach (ArenaEntry entry in Plan.Entries) clear.Add((entry.Out, 4f));
            foreach (float z in _piers) foreach (float side in new[] { -1f, 1f }) clear.Add((C + new Vector2(side * PierAt, z), 2.2f));
            Remains(Keep ? 2 : 4, Keep ? 1 : 3, clear);
        }

        // The faithful in the aisles, between and behind the pews, looking down on the nave, and along the south end.
        private void People()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float x in new[] { _half + WallThick + 0.8f, _half + WallThick + 2.8f, _half + WallThick + 4.8f })
                {
                    for (float z = _south + 1f; z < _apse - 1f; z += 2.4f)
                    {
                        // Not inside a pew: the two rows nearest the nave stand between them, and the last behind them.
                        bool inPews = x < _half + WallThick + 4f && Enumerable.Range(0, 8).Any(k => MathF.Abs(z - (_south + 2f + k * 9f)) < 2.9f);
                        // Nor in a gate's passage, which runs back under the aisle at the nave's level.
                        bool inGate = x < _half + GateDepth + 1.2f && _gates.Any(g => MathF.Abs(z - g) < GateWidth * 0.5f + 1.6f);
                        if (inPews || inGate || !Random.Chance(0.6f)) continue;
                        B.Spawn(SpawnRole.Spectator, C + new Vector2(side * x + Random.Range(-0.3f, 0.3f), z + Random.Range(-0.4f, 0.4f)));
                    }
                }
            }
            for (float x = -_half + 1f; x < _half - 1f; x += 2.6f)
            {
                if (!Random.Chance(0.6f)) continue;
                B.Spawn(SpawnRole.Spectator, C + new Vector2(x, _south - WallThick - 2f));
            }
        }
    }
}
