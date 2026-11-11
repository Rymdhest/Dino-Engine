using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Colosseum: the Reich's great amphitheatre, built to hold a whole land. Its floor is an oval of sand laid out on four centres, as amphitheatres always were, ringed by a podium of pale limestone
    /// with the Reich's iron along it; great gates at either end of its long axis, and lifts under the floor whose hatches drop open when the rest of the brood is brought up. Four tiers of seats climb
    /// from the podium, packed with the land's people, and round the top of the north side, where the camera looks, an arcade of two storeys stands against the sky with the Reich's banners in its attic.
    /// The commander's box stands over the podium on the north, under an oxblood canopy, and its bolt-throwers cover the floor. The waygate it was built round stands in a bay in the south podium. A
    /// keep's colosseum is the same, smaller, with two tiers, no arcade and no box. Read ARENAS.md.
    /// </summary>
    internal sealed class ColosseumArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.Limestone;

        private const float PodiumTop = 5.6f;
        private const float PodiumThick = 1.8f;
        private const float Parapet = 0.95f;
        private const float TierRise = 1.3f;
        private const float TierDepth = 2.4f;
        private const float RiserThick = 1f;
        private const float RiserCurb = 0.3f;
        private const float RimWalk = 2.6f;

        // The great gates at the ends of the long axis.
        private const float Opening = 6.4f;
        private const float Crown = 7.6f;
        private const float GateDepth = 4.4f;
        private const float GateWidth = 12f;
        private const float GateHeight = 13.6f;
        private const float GrilleRise = 5.4f;

        private const float HatchSize = 3.6f;
        private const float TribuneWidth = 12.6f;
        private const float TribuneDepth = 3.4f;
        private const float BayWidth = 11f;
        private const float BayDepth = 5.6f;
        private const float BayPylon = 2.2f;

        private readonly Oval _oval;
        private readonly int _tiers;
        private readonly bool _grand;                    // The arcade and the commander's box, which only a boss map's has.
        private readonly Vector2[] _hatches;

        public ColosseumArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.Colosseum, keep, centre, keep ? "keep colosseum" : "colosseum")
        {
            _oval = keep ? new Oval(19f, 14f, 10f) : new Oval(27f, 19f, 14f);
            _tiers = keep ? 2 : 4;
            _grand = !keep;
            _hatches = keep ? new[] { new Vector2(7f, 4f), new Vector2(-7f, 4f) } : new[] { new Vector2(10f, 7f), new Vector2(-10f, 7f), new Vector2(10.5f, -6.5f), new Vector2(-10.5f, -6.5f) };
        }

        private float TierGround(int tier) => PodiumTop - Parapet + (tier - 1) * TierRise;
        private float RimGround => TierGround(_tiers + 1);
        private float TierFace(int tier) => tier == 1 ? 0f : PodiumThick + (tier - 2) * (TierDepth + RiserThick) + TierDepth;
        private float TierBack(int tier) => (tier == 1 ? PodiumThick : TierFace(tier) + RiserThick) + TierDepth;
        private float RimFace => TierFace(_tiers + 1);
        private float ArcadeAt => RimFace + RiserThick + 1.2f;

        /// <summary>How far from the middle the whole of it reaches.</summary>
        public float Reach => _oval.A + ArcadeAt + 3f;

        private float BaySide => _oval.Side;                 // The south podium's radius, about its centre.
        private Vector2 BayCentre => _oval.SouthCentre;
        private float BayMouth => -(BayCentre.Y - MathF.Sqrt(BaySide * BaySide - BayWidth * BayWidth * 0.25f));   // How far south of the middle the bay's mouth is.

        public override void Build()
        {
            float ground = AverageGround(Reach + 10f);
            F = MathF.Max(ground - RimGround, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            Level(Footprint.Circle(C, Reach + 6f), RimGround, 10f);
            Vector2 gate = Keep ? KeepGate(-North * (BayMouth + BayDepth - KeepGateDeep), North, Stone, ArenaGrille.Pit) : Waygate(-North * (BayMouth + BayDepth - 2.4f), North);

            Ground();
            Podium();
            Gates();
            Hatches();
            Bay();
            Stands();
            if (_grand)
            {
                Tribune();
                Arcade();
            }
            Rim();
            Seal();
            People();

            // Sand on the floor, flags on the tiers, and cobbles on the rim out to where the hill begins.
            OvalPaint(GroundPaint.Sand, 0.6f, 1.2f);
            Paint(GroundPaint.Sand, Footprint.Box(C - North * (BayMouth + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f, BayDepth * 0.5f + 0.6f), 0f), 0.8f);
            OvalPaint(GroundPaint.Paving, RimFace + 0.6f, 1f);
            Paint(GroundPaint.Plaza, Footprint.Circle(C, Reach + 1f), 2.5f);
            var clear = new List<(Vector2, float)> { (C - North * (BayMouth + BayDepth * 0.5f), 8f) };
            foreach (ArenaEntry entry in Plan.Entries) clear.Add((entry.Out, entry.Kind == ArenaEntryKind.Hatch ? 3.6f : 5f));
            Remains(Keep ? 3 : 6, Keep ? 2 : 5, clear);
            Keepout(Footprint.Circle(C, Reach + 2f));

            Tyrants(C + North * 4f, C - North * 2f + East * 8f);
            if (Keep) KeepRim(Reach + 1f, RimGround, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // ---------------------------------------------------------------- Ground

        // The oval at an offset, as strips a metre deep across it, which is what a pad of its shape is made of.
        private IEnumerable<Footprint> OvalStrips(float offset)
        {
            float half = _oval.B + offset;
            for (float z = -half; z < half; z += 1f)
            {
                float w = MathF.Max(_oval.HalfWidthAt(z, offset), _oval.HalfWidthAt(MathF.Min(z + 1f, half), offset));
                if (w <= 0.05f) continue;
                yield return Footprint.Box(C + new Vector2(0f, z + 0.5f), new Vector2(w, 0.52f), 0f);
            }
        }

        private void OvalPaint(GroundPaint paint, float offset, float softness)
        {
            foreach (Footprint strip in OvalStrips(offset)) Paint(paint, strip, softness);
        }

        // The floor sunk into the tiers stepping up from the podium to the rim, outermost first, each edge in the middle of the riser and bench that hide it.
        private void Ground()
        {
            for (int tier = _tiers; tier >= 1; tier--)
            {
                foreach (Footprint strip in OvalStrips(TierBack(tier) + 0.78f)) Raise(strip, TierGround(tier));
            }
            foreach (Footprint strip in OvalStrips(Inset * 0.6f)) Raise(strip, 0f);
            Raise(Footprint.Box(C - North * (BayMouth + BayDepth * 0.5f), new Vector2(BayWidth * 0.5f + 0.7f, BayDepth * 0.5f + 0.7f), 0f), 0f);
        }

        // ---------------------------------------------------------------- The podium and its gates

        // Where each gate stands on an end's arc, and the half-angle its block takes of it.
        private float GateHalfAngle => MathHelper.RadiansToDegrees(MathF.Asin(GateWidth * 0.5f / _oval.End));
        private float GateFromEnd => MathF.Sqrt(_oval.End * _oval.End - GateWidth * GateWidth * 0.25f) - 0.2f + ArenaGates.Proud;

        // The podium all round, in pieces of no more than 40 degrees of an arc, broken where the great gates and the bay stand, carrying banners and pilasters on its north half and the railing
        // all round but under the commander's box, which stands on it.
        private void Podium()
        {
            (Vector2 Centre, float Radius, float From, float Sweep)[] arcs = _oval.Arcs(0f);
            float bayHalf = MathHelper.RadiansToDegrees(MathF.Asin((BayWidth * 0.5f + BayPylon) / _oval.Side)) - 0.8f;
            float boxHalf = MathHelper.RadiansToDegrees(MathF.Asin((TribuneWidth * 0.5f + 0.3f) / _oval.Side));
            for (int a = 0; a < 4; a++)
            {
                (Vector2 centre, float radius, float from, float sweep) = arcs[a];
                var breaks = new List<(float Middle, float Half)>();
                if (a == 1) breaks.Add((90f, GateHalfAngle - 0.6f));
                if (a == 3) breaks.Add((270f, GateHalfAngle - 0.6f));
                if (a == 2) breaks.Add((180f, bayHalf));
                foreach ((float s, float e) in Spans(from, sweep, breaks))
                {
                    int pieces = Math.Max(1, (int)MathF.Ceiling((e - s) / 40f));
                    for (int p = 0; p < pieces; p++)
                    {
                        float p0 = s + (e - s) * p / pieces, p1 = s + (e - s) * (p + 1) / pieces;
                        bool north = a == 0 || (a == 1 && p1 < 90f) || (a == 3 && p0 > 270f);
                        bool underBox = _grand && a == 0 && p0 < boxHalf && p1 > -boxHalf;
                        if (underBox)
                        {
                            // Under the box the railing gives way to it, and either side of it carries on.
                            foreach ((float q0, float q1, bool box) in new[] { (p0, MathF.Max(p0, -boxHalf), false), (MathF.Max(p0, -boxHalf), MathF.Min(p1, boxHalf), true), (MathF.Min(p1, boxHalf), p1, false) })
                            {
                                if (q1 - q0 < 0.5f) continue;
                                ArenaTrim t = box ? ArenaTrim.Pilasters : ArenaTrim.Railing | ArenaTrim.Pilasters | ArenaTrim.Banners;
                                ArcWall(ArenaWallForm.Podium, Stone, centre, radius, q0, q1 - q0, PodiumTop, PodiumThick, TierGround(1), -1f, t);
                            }
                            continue;
                        }
                        ArenaTrim trim = ArenaTrim.Railing | ArenaTrim.Pilasters | (north ? ArenaTrim.Banners : ArenaTrim.None);
                        ArcWall(ArenaWallForm.Podium, Stone, centre, radius, p0, p1 - p0, PodiumTop, PodiumThick, TierGround(1), -1f, trim);
                    }
                }
            }
        }

        // The stretches of an arc left between the breaks in it.
        private static IEnumerable<(float From, float To)> Spans(float from, float sweep, List<(float Middle, float Half)> breaks)
        {
            float at = from, end = from + sweep;
            foreach ((float middle, float half) in breaks.OrderBy(b => b.Middle))
            {
                if (middle - half > at) yield return (at, middle - half);
                at = MathF.Max(at, middle + half);
            }
            if (end > at) yield return (at, end);
        }

        // The great gates at the ends of the long axis, each a gatehouse with its grille shut and the Reich's banner on its top, and the floor in front of it where whoever's let through is first seen.
        private void Gates()
        {
            foreach (float side in new[] { 1f, -1f })
            {
                Vector2 outward = East * side;
                Vector2 endCentre = side > 0f ? _oval.EastCentre : _oval.WestCentre;
                Vector2 at = endCentre + outward * GateFromEnd;
                float yaw = YawFacing(-outward);
                Piece(new ArenaSpec(ArenaShape.Gatehouse, NextSeed(), (int)Stone, Cm(Opening), Cm(Crown), Cm(GateDepth), Cm(GateWidth), Cm(GateHeight)), at, yaw, 0f);
                foreach (Vector3 torch in ArenaGates.TorchSpots(Opening, GateWidth))
                {
                    Vector2 right = new Vector2(outward.Y, -outward.X);
                    Lamp(1, at + right * -torch.X - outward * torch.Z, yaw, torch.Y);
                }
                float depth = GateDepth + ArenaGates.Proud + 0.3f;
                Solid(Footprint.Box(C + at + outward * (depth * 0.5f - ArenaGates.Proud), new Vector2(GateWidth * 0.5f, depth * 0.5f), yaw));
                Raise(Footprint.Box(C + at + outward * (GateDepth * 0.5f), new Vector2(Opening * 0.5f + 0.4f, GateDepth * 0.5f), yaw), 0f);
                Piece(new ArenaSpec(ArenaShape.Banner, NextSeed(), Cm(7f), Cm(5f), Cm(2.1f), 0), at + outward * (GateDepth * 0.45f), yaw, GateHeight);

                string grille = new ArenaSpec(ArenaShape.Portcullis, NextSeed(), (int)ArenaGrille.Pit, Cm(Opening + 0.24f), Cm(Crown + 0.15f)).Id;
                Vector2 door = at + outward * (ArenaGates.GrilleBack - ArenaGates.Proud);
                Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Portcullis, C + at - outward * (ArenaGates.Proud + 2.6f), new Vector3(C.X + door.X, F, C.Y + door.Y), yaw, grille, GrilleRise));
            }
        }

        // The lifts under the floor: a frame round each shaft, flush with the sand, and its leaves, which the game drops open when the brood is brought up.
        private void Hatches()
        {
            string leaf = new ArenaSpec(ArenaShape.Leaf, NextSeed(), Cm(HatchSize)).Id;
            foreach (Vector2 at in _hatches)
            {
                Piece(new ArenaSpec(ArenaShape.Hatch, NextSeed(), Cm(HatchSize)), at, 0f, 0f);
                Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Hatch, C + at, new Vector3(C.X + at.X, F, C.Y + at.Y), 0f, leaf, 90f, HatchSize));
            }
        }

        // ---------------------------------------------------------------- The waygate's bay

        private void Bay()
        {
            float height = TierGround(2) + Parapet;
            Vector2 mouth = -North * BayMouth;
            Piece(new ArenaSpec(ArenaShape.Bay, NextSeed(), (int)Stone, Cm(BayWidth), Cm(BayDepth), Cm(height)), mouth, 0f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector2 pylon = mouth + East * side * (BayWidth * 0.5f + BayPylon * 0.5f) + North * 0.5f;
                Pylon(Stone, pylon, BayPylon, height + 1.6f, 1, 0f, 0f);
            }
            foreach (Vector3 brazier in ArenaDressing.BayBraziers(BayWidth)) Brazier(mouth + new Vector2(brazier.X, brazier.Z) - North * BayDepth * 0.55f, 1, 0.85f, 0f);
            // Nothing walks behind the waygate, where the wall holding the stands up would hide anyone from the camera.
            Solid(Footprint.Box(C - North * (BayMouth + BayDepth) + North * 1.2f, new Vector2(BayWidth * 0.5f, 1.2f), 0f));
        }

        // ---------------------------------------------------------------- The stands

        // Each tier's riser, round all four arcs, broken where the bay reaches into it and, for the first tiers on the north, where the commander's box stands.
        private void Stands()
        {
            float boxBack = TribuneDepth + 0.6f;
            for (int tier = 2; tier <= _tiers + 1; tier++)
            {
                float face = TierFace(tier);
                (Vector2 Centre, float Radius, float From, float Sweep)[] arcs = _oval.Arcs(face);
                for (int a = 0; a < 4; a++)
                {
                    (Vector2 centre, float radius, float from, float sweep) = arcs[a];
                    var breaks = new List<(float Middle, float Half)>();
                    if (a == 2 && face < BayDepth + 1.7f) breaks.Add((180f, MathHelper.RadiansToDegrees(MathF.Atan2(BayWidth * 0.5f + 1.2f, radius)) + 0.8f));
                    if (_grand && a == 0 && face < boxBack + 0.2f) breaks.Add((0f, MathHelper.RadiansToDegrees(MathF.Atan2(TribuneWidth * 0.5f + 0.5f, radius))));
                    foreach ((float s, float e) in Spans(from, sweep, breaks))
                    {
                        if (e - s < 1f) continue;
                        int pieces = Math.Max(1, (int)MathF.Ceiling((e - s) / 35f));
                        for (int p = 0; p < pieces; p++)
                        {
                            float p0 = s + (e - s) * p / pieces, p1 = s + (e - s) * (p + 1) / pieces;
                            ArcWall(ArenaWallForm.Riser, Stone, centre, radius, p0, p1 - p0, TierGround(tier) + RiserCurb - TierGround(tier - 1), RiserThick, TierGround(tier) - TierGround(tier - 1), -1f, ArenaTrim.None, TierGround(tier - 1));
                        }
                    }
                }
            }
        }

        // The commander's box on the north, its front over the podium's face, and the bolt-throwers in it, which the game turns and looses (ArenaSystem).
        private void Tribune()
        {
            Vector2 front = North * _oval.B;
            Vector2 at = front + North * TribuneDepth;
            float yaw = YawFacing(-North);
            float floor = PodiumTop + 0.5f;
            Piece(new ArenaSpec(ArenaShape.Tribune, NextSeed(), (int)Stone, Cm(TribuneWidth), Cm(TribuneDepth), Cm(floor)), at, yaw, 0f);
            string ballista = new ArenaSpec(ArenaShape.Ballista, NextSeed()).Id;
            foreach (Vector3 thrower in ColosseumPieces.TribuneThrowers(TribuneWidth, TribuneDepth, floor))
            {
                // The box faces south, so its own x runs west and its own z south.
                Vector2 where = at + new Vector2(-thrower.X, -thrower.Z);
                Plan.Pieces.Add(new ArenaPiece(ArenaPieceKind.Ballista, new Vector3(C.X + where.X, F + thrower.Y, C.Y + where.Y), yaw, ballista));
            }
        }

        // The arcade round the top of the north side, standing on the rim behind the last tier, its arches open to the sky: what the camera sees across the floor.
        private void Arcade()
        {
            (Vector2 centre, float radius, float from, float sweep) = _oval.Arcs(ArcadeAt)[0];
            int pieces = 3;
            for (int p = 0; p < pieces; p++)
            {
                float p0 = from + sweep * p / pieces, p1 = from + sweep * (p + 1) / pieces;
                Piece(new ArenaSpec(ArenaShape.Arcade, NextSeed(), (int)Stone, Cm(radius), (int)MathF.Round(p0), (int)MathF.Round(p1 - p0), -1, 2, Cm(6.4f), Cm(1.7f), Cm(2.2f)), centre, 0f, RimGround);
            }
        }

        // ---------------------------------------------------------------- The rim

        // Pylons with braziers along the rim where the arcade isn't, each other one burning, and the rest flying the Reich's banner.
        private void Rim()
        {
            float perimeter = _oval.Perimeter(ArcadeAt - 0.4f);
            int count = Math.Max(12, (int)MathF.Round(perimeter / 11f));
            for (int i = 0; i < count; i++)
            {
                (Vector2 point, Vector2 outward) = _oval.Along(perimeter * (i + 0.5f) / count, ArcadeAt - 0.4f);
                float bearing = Bearing(point);
                if (Near(bearing, 180f, 16f)) continue;
                if (_grand && point.Y > 0f && MathF.Abs(point.X) < _oval.A - _oval.End + 6f) continue;
                Pylon(Stone, point, 1.5f, 4.4f, i % 2 == 0 ? 1 : 2, YawFacing(-outward), RimGround);
            }
        }

        // ---------------------------------------------------------------- The seal and the people

        // The seal round the podium's face, broken only by the bay, whose walls carry it round the waygate; the gatehouses are solid of themselves.
        private void Seal()
        {
            float mouth = BayMouth;
            var ring = new List<Vector2>();
            foreach (Vector2 point in _oval.Points(0f, 1.2f))
            {
                // Leave out the bay's mouth.
                if (point.Y < 0f && MathF.Abs(point.X) < BayWidth * 0.5f) continue;
                ring.Add(point);
            }
            // The ring's points run clockwise from due north; the bay's mouth lies between the last point east of it and the first west of it.
            int split = ring.FindIndex(p => p.Y < 0f && p.X < 0f);
            var ordered = new List<Vector2>();
            ordered.AddRange(ring.Skip(split));
            ordered.AddRange(ring.Take(split));
            ordered.Insert(0, new Vector2(-BayWidth * 0.5f, -mouth));
            ordered.Add(new Vector2(BayWidth * 0.5f, -mouth));
            SealLine(ordered, PodiumThick, closed: false);
            var bay = new List<Vector2> { new Vector2(-BayWidth * 0.5f, -mouth), new Vector2(-BayWidth * 0.5f, -mouth - BayDepth), new Vector2(BayWidth * 0.5f, -mouth - BayDepth), new Vector2(BayWidth * 0.5f, -mouth) };
            SealLine(bay, 1.4f, closed: false);
            foreach (Vector2 p in ordered) Plan.Bounds.Add(C + p);
            Plan.Bounds.Add(C + new Vector2(BayWidth * 0.5f, -mouth - BayDepth));
            Plan.Bounds.Add(C + new Vector2(-BayWidth * 0.5f, -mouth - BayDepth));
        }

        // The land's people on every tier, but where a gate, the bay or the commander's box stands.
        private void People()
        {
            for (int tier = 1; tier <= _tiers; tier++)
            {
                float offset = (tier == 1 ? PodiumThick : TierFace(tier) + RiserThick) + TierDepth * 0.55f;
                float perimeter = _oval.Perimeter(offset);
                int count = (int)(perimeter / 3.4f);
                for (int i = 0; i < count; i++)
                {
                    (Vector2 point, _) = _oval.Along(perimeter * (i + tier * 0.33f) / count, offset);
                    if (point.Y < 0f && MathF.Abs(point.X) < BayWidth * 0.5f + 2.5f && tier <= 2) continue;
                    if (tier == 1 && MathF.Abs(point.Y) < GateWidth * 0.5f + 1.2f && MathF.Abs(point.X) > _oval.A - 2f) continue;
                    if (_grand && tier <= 2 && point.Y > 0f && MathF.Abs(point.X) < TribuneWidth * 0.5f + 1.2f) continue;
                    if (!Random.Chance(0.56f)) continue;
                    B.Spawn(SpawnRole.Spectator, C + point + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.3f, 0.3f)));
                }
            }
        }
    }
}
