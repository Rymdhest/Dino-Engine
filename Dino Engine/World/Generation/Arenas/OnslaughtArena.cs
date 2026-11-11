using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Fossilized Onslaught: the field where the Reich's first charge on the land was buried when the mountain fell on it, which the Ossuary has dug out into a quarry. Its floor is ash, sunk in two
    /// benches of cut basalt, and on it stands the charge itself, war-beasts in harness turned to stone mid-stride, heading south-west where the ash caught them, with their wagons on their sides and
    /// their standards still up, all of it solid: what's in the way of a blow stops it. The cracks in the ash breathe fire, and the dead come up through it. In the north faces, where the camera sees them,
    /// are the bones of beasts far older than the Reich, half cut free, with the Ossuary's scaffolds against them and its crane on the rim, and its cold lamps all round. The waygate the Reich built its
    /// road to stands on the ash by the south face. A keep's is the same, smaller, with fewer of the charge. Read ARENAS.md.
    /// </summary>
    internal sealed class OnslaughtArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.Basalt;

        // Two benches: the first at least four metres over the floor, so that no grid node on its edge splits the drop into steps that can be walked (ARENAS.md), and the rim over that.
        private const float BenchOne = 4.4f;
        private const float BenchTwo = 3.4f;
        private const float WallThick = 1.6f;
        private const float BenchDepth = 2.6f;

        // The way the charge was going when the ash caught it.
        private static readonly Vector2 Charge = new Vector2(-0.7071f, -0.7071f);

        private readonly float _radius;
        private readonly int _statues;

        public OnslaughtArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.Onslaught, keep, centre, keep ? "keep onslaught" : "onslaught")
        {
            _radius = keep ? 15.5f : 21f;
            _statues = keep ? 3 : 7;
        }

        private float RimGround => BenchOne + BenchTwo;
        private float BenchFace => _radius + WallThick + BenchDepth;

        /// <summary>How far from the middle the whole of it reaches.</summary>
        public float Reach => BenchFace + WallThick + 9f;

        public override void Build()
        {
            float ground = AverageGround(Reach + 8f);
            F = MathF.Max(ground - RimGround, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            Level(Footprint.Circle(C, Reach + 5f), RimGround, 9f);
            Raise(Footprint.Circle(C, BenchFace + 0.78f), BenchOne);
            Raise(Footprint.Circle(C, _radius + 0.8f), 0f);
            Vector2 gate = Keep ? KeepGate(-North * (_radius - KeepGateDeep), North, Stone, ArenaGrille.Pit) : Waygate(-North * (_radius - 4.2f), North);
            // Nothing walks behind the waygate, where the face would hide anyone from the camera.
            Solid(Footprint.Box(C + gate - North * 1.2f, new Vector2(4.4f, 1.2f), 0f));

            Faces();
            Seal();
            Dig();
            Charging(gate);
            Rim();
            People();

            Paint(GroundPaint.Ash, Footprint.Circle(C, _radius + 0.6f), 1.4f);
            Paint(GroundPaint.Yard, Footprint.Circle(C, Reach), 4f);
            Keepout(Footprint.Circle(C, Reach + 2f));

            Tyrants(C + North * 5f + East * 2f, C - North * 2f - East * 8f);
            if (Keep) KeepRim(Reach + 1f, RimGround, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // The cut faces: the first bench and the second behind it, all the way round, in pieces of 40 degrees.
        private void Faces()
        {
            for (float from = 0f; from < 360f; from += 40f)
            {
                ArcWall(ArenaWallForm.Quarry, Stone, Vector2.Zero, _radius, from, 40f, BenchOne + 0.4f, WallThick, BenchOne, -1f, ArenaTrim.None);
                ArcWall(ArenaWallForm.Quarry, Stone, Vector2.Zero, BenchFace, from, 40f, BenchTwo + 0.35f, WallThick, BenchTwo, -1f, ArenaTrim.None, BenchOne);
            }
            // The waygate's lamps, on the floor either side of it.
            foreach (float side in new[] { -1f, 1f }) Lamp(2, -North * (_radius - 3.2f) + East * side * 6.2f, 0f, 0f);
        }

        // The seal round the first face, all the way round: the waygate stands inside it.
        private void Seal()
        {
            SealArc(_radius, 0f, 360f, WallThick);
            foreach (Vector2 p in ArcPoints(_radius, 0f, 360f)) Plan.Bounds.Add(C + p);
            Plan.Bounds.RemoveAt(Plan.Bounds.Count - 1);
        }

        // The Ossuary's dig in the north faces, where the camera sees it: bones of old beasts half cut free of both benches, scaffolds against them, cold lamps, and the crane on the rim.
        private void Dig()
        {
            float[] bearings = Keep ? new[] { -25f, 30f } : new[] { -55f, -18f, 22f, 58f };
            for (int i = 0; i < bearings.Length; i++)
            {
                float bearing = bearings[i];
                bool upper = i % 2 == 1;
                float radius = upper ? BenchFace : _radius;
                Vector2 outward = Toward(bearing);
                float lean = upper ? 0.12f * (BenchTwo - 0.6f) : 0.12f * (BenchOne - 0.6f);
                Vector2 at = outward * (radius + lean * 0.5f - 0.15f);
                Piece(new ArenaSpec(ArenaShape.Fossil, NextSeed(), Random.Int(70, 100)), at, YawFacing(-outward), upper ? BenchOne + 0.2f : 0.3f);
                if (!upper)
                {
                    // The scaffold the bones are being cut from stands on the floor before them, so it's solid.
                    Vector2 scaffold = outward * (radius - 1.2f) + new Vector2(outward.Y, -outward.X) * 3.2f;
                    Piece(new ArenaSpec(ArenaShape.Scaffold, NextSeed(), Cm(4.6f), Cm(BenchOne + 0.6f)), scaffold + outward * 1.2f, YawFacing(-outward), 0f);
                    Solid(Footprint.Box(C + scaffold, new Vector2(2.5f, 1.2f), YawFacing(-outward)));
                }
                Lamp(2, outward * (radius + WallThick + 1.2f) + new Vector2(-outward.Y, outward.X) * 2.4f, YawFacing(-outward), upper ? RimGround : BenchOne);
            }
            if (!Keep)
            {
                Vector2 crane = Toward(38f) * (BenchFace + WallThick + 3.4f);
                Piece(new ArenaSpec(ArenaShape.Crane, NextSeed(), Cm(6.2f)), crane, YawFacing(-crane), RimGround);
                Solid(Footprint.Circle(C + crane, 2.4f));
            }
        }

        // The charge, turned to stone across the floor, every one of them heading the same way, and what went down with them: wagons on their sides and standards still up. All of it solid, kept
        // apart enough to fight between, clear of the waygate, the middle, and the places where the vents breathe and the dead rise.
        private void Charging(Vector2 gate)
        {
            // Where the dead come up out of the ash, and where it breathes fire: these first, as the charge keeps clear of them.
            var risings = new List<Vector2>();
            float[] risingBearings = Keep ? new[] { -60f, 60f, 150f } : new[] { -110f, -45f, 15f, 75f, 140f };
            foreach (float bearing in risingBearings) risings.Add(Toward(bearing) * _radius * 0.62f);
            foreach (Vector2 at in risings) Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.Rising, C + at, new Vector3(C.X + at.X, F, C.Y + at.Y), 0f, "", 0f));
            var vents = new List<Vector2>();
            float[] ventBearings = Keep ? new[] { 0f, 120f, 240f } : new[] { -150f, -80f, -10f, 45f, 110f };
            foreach (float bearing in ventBearings) vents.Add(Toward(bearing) * _radius * (Keep ? 0.42f : 0.36f));
            foreach (Vector2 at in vents)
            {
                float yaw = Random.Float() * MathF.Tau;
                Piece(new ArenaSpec(ArenaShape.Vent, NextSeed(), Cm(Random.Range(3.2f, 4.4f))), at, yaw, 0f);
                Plan.Pieces.Add(new ArenaPiece(ArenaPieceKind.Vent, new Vector3(C.X + at.X, F, C.Y + at.Y), yaw, ""));
            }

            var keepClear = new List<(Vector2 At, float Radius)> { (gate, 6.5f), (North * 5f + East * 2f, 4.5f) };
            foreach (Vector2 r in risings) keepClear.Add((r, 2.6f));
            foreach (Vector2 v in vents) keepClear.Add((v, 2.2f));
            bool Clear(Vector2 at, float radius) => keepClear.All(c => (at - c.At).Length > c.Radius + radius) && at.Length < _radius - radius - 2.2f;

            float yawCharge = YawFacing(Charge);
            for (int i = 0, placed = 0; i < _statues * 80 && placed < _statues; i++)
            {
                Vector2 at = Toward(Random.Range(0f, 360f)) * MathF.Sqrt(Random.Float()) * (_radius - 4f);
                int kind = placed % 4 == 1 ? 1 : placed % 4 == 3 ? 2 : placed == 4 ? 3 : 0;
                float size = kind is 1 or 2 ? 3.1f : 2.3f;
                if (!Clear(at, size)) continue;
                float lean = Random.Chance(0.25f) ? Random.Range(-28f, 28f) : Random.Range(-6f, 6f);
                Piece(new ArenaSpec(ArenaShape.Statue, NextSeed(), kind, Random.Int(0, 7), (int)MathF.Round(Random.Range(0.05f, 0.6f) * 100f), (int)MathF.Round(lean), Cm(Random.Range(0.25f, 0.6f))),
                    at, yawCharge + Random.Range(-0.25f, 0.25f), 0f);
                Solid(Footprint.Circle(C + at, size), kind is 1 or 2 ? 4.5f : 4f);
                keepClear.Add((at, size + 2.2f));
                placed++;
            }
            for (int i = 0, placed = 0; i < 120 && placed < (Keep ? 1 : 2); i++)
            {
                Vector2 at = Toward(Random.Range(0f, 360f)) * MathF.Sqrt(Random.Float()) * (_radius - 5f);
                if (!Clear(at, 3f)) continue;
                float yaw = yawCharge + MathF.PI * 0.5f + Random.Range(-0.4f, 0.4f);
                Piece(new ArenaSpec(ArenaShape.Wagon, NextSeed()), at, yaw, 0f);
                Solid(Footprint.Box(C + at, new Vector2(3.4f, 1.6f), yaw), 3f);
                keepClear.Add((at, 6f));
                placed++;
            }
            for (int i = 0, placed = 0; i < 40 && placed < (Keep ? 2 : 4); i++)
            {
                Vector2 at = Toward(Random.Range(0f, 360f)) * MathF.Sqrt(Random.Float()) * (_radius - 3f);
                if (!Clear(at, 1.2f)) continue;
                Piece(new ArenaSpec(ArenaShape.Standard, NextSeed(), Random.Int(-24, 24)), at, yawCharge + Random.Range(-0.6f, 0.6f), 0f);
                Solid(Footprint.Circle(C + at, 0.45f), 8f);
                keepClear.Add((at, 2.5f));
                placed++;
            }
            var clear = new List<(Vector2, float)> { (C + gate, 8f) };
            foreach (Vector2 r in risings) clear.Add((C + r, 3f));
            Remains(0, Keep ? 2 : 5, clear);
        }

        // Cold lamps round the rim, and the Reich's old banners on its masts, which the Ossuary left up.
        private void Rim()
        {
            float ring = BenchFace + WallThick + 3.6f;
            int count = Keep ? 8 : 12;
            for (int k = 0; k < count; k++)
            {
                float bearing = (k + 0.5f) * 360f / count;
                if (Near(bearing, 38f, 12f) && !Keep) continue;
                Vector2 at = Toward(bearing) * ring;
                if (k % 3 == 1)
                {
                    Piece(new ArenaSpec(ArenaShape.Banner, NextSeed(), Cm(9f), Cm(4.4f), Cm(1.6f), 0), at, YawFacing(-at), RimGround);
                    Solid(Footprint.Circle(C + at, 0.6f));
                }
                else Lamp(2, at, YawFacing(-at), RimGround);
            }
        }

        // The land's people, made to dig, standing on the bench and the rim to watch.
        private void People()
        {
            foreach (float radius in new[] { _radius + WallThick + BenchDepth * 0.5f, BenchFace + WallThick + 1.8f })
            {
                float step = 360f / (MathF.Tau * radius / 3.3f);
                Watchers(radius, step, radius * 3f, bearing => Keep || !Near(bearing, 38f, 10f), share: 0.6f);
            }
        }
    }
}
