using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// The Jurassic Cage: the Amber Institute's breeding cage, where the tyrant it grew is kept between hunts. A round pen of mud and straw sunk below the land, ringed by a footing of grey stone with
    /// black iron bars leaded into it that lean in overhead so that nothing climbs out, riveted posts with brass caps at even steps. The brood's pens open onto it through iron doors round its north half,
    /// and the Institute's eggs lie in nests in the straw: left alone, they hatch. Troughs along its edge and gibbets with what's being fed hung from them. Outside, the land's people stand round the bars,
    /// and on the north the Institute's gallery stands over the ground where the blood-doctors watch, its lamps and its jars of amber glowing. The waygate the cage was built round stands inside it, by
    /// its south bars. A keep's cage is the same, smaller, with two pens and three nests. Read ARENAS.md.
    /// </summary>
    internal sealed class CageArena : ArenaLayout
    {
        private const ArenaStoneKind Stone = ArenaStoneKind.Granite;

        // How far the pen is below the land round it: at least four metres, or a grid node falling on its edge splits the drop into two half-steps that can each be walked (ARENAS.md).
        private const float Sunk = 4f;
        private const float FootingTop = 4.4f;          // Above the pen's floor, where the bars are leaded in.
        private const float FootingThick = 1.4f;
        private const float BarHeight = 9f;             // Above the footing, before they lean in.
        private const float BarLean = 2.6f;
        private const float BarSpacing = 1f;

        // A pen's gate: its opening, and the block it's in, which its door rises into.
        private const float PenWidth = 3.8f;
        private const float PenHeight = 4.4f;
        private const float PenDepth = 3.4f;
        private const float PenBlock = 9.2f;
        private const float DoorRise = 4.2f;

        private readonly float _radius;
        private readonly float[] _pens;
        private readonly int _nests;

        public CageArena(SiteBuilder b, bool keep, Vector2 centre)
            : base(b, ArenaKind.Cage, keep, centre, keep ? "keep cage" : "cage")
        {
            _radius = keep ? 15f : 19f;
            _pens = keep ? new[] { -50f, 50f } : new[] { -100f, -40f, 40f, 100f };
            _nests = keep ? 3 : 5;
        }

        private float BarRing => _radius + FootingThick * 0.5f;
        private float PenHalfAngle => MathHelper.RadiansToDegrees(MathF.Asin((PenWidth * 0.5f + 1.3f) / _radius));

        /// <summary>How far from the middle the whole of it reaches.</summary>
        public float Reach => _radius + (Keep ? 9f : 13f);

        public override void Build()
        {
            float ground = AverageGround(Reach + 8f);
            F = MathF.Max(ground - Sunk, WorldPlan.SeaLevel + 3f);
            Plan.Floor = F;
            Plan.Reach = Reach;

            // The land round the cage, level with the top of its pen, and the pen sunk into it, its edge inside the footing.
            Level(Footprint.Circle(C, Reach + 4f), Sunk, 8f);
            Raise(Footprint.Circle(C, _radius + FootingThick * 0.5f), 0f);
            Vector2 gate = Keep ? KeepGate(-North * (_radius - KeepGateDeep), North, Stone, ArenaGrille.Pen) : Waygate(-North * (_radius - 4.2f), North);

            Footing();
            Bars();
            Pens();
            Seal();
            Floor(gate);
            if (!Keep) Gallery();
            Round();
            People();

            Paint(GroundPaint.Mud, Footprint.Circle(C, _radius + 0.6f), 1.4f);
            Paint(GroundPaint.Yard, Footprint.Circle(C, _radius + 7f), 3f);
            Keepout(Footprint.Circle(C, Reach + 2f));

            Tyrants(C + North * 4.5f, C - North * 2f + East * 7f);
            if (Keep) KeepRim(Reach + 1f, Sunk, Stone);
            else Plan.Arrival = C + gate + North * 2.5f;
        }

        // The footing all round, in pieces of 30 degrees, broken where a pen's gate stands in it.
        private void Footing()
        {
            foreach ((float from, float to) in Around(_pens.Select(p => ((p + 360f) % 360f, PenHalfAngle - 0.5f)).ToList()))
            {
                int pieces = Math.Max(1, (int)MathF.Ceiling((to - from) / 30f));
                for (int p = 0; p < pieces; p++)
                {
                    float a = from + (to - from) * p / pieces, c = from + (to - from) * (p + 1) / pieces;
                    ArcWall(ArenaWallForm.Footing, Stone, Vector2.Zero, _radius, a, c - a, FootingTop, FootingThick, Sunk, -1f, ArenaTrim.None);
                }
            }
        }

        // The stretches of a circle left between breaks in it, from one break round to the next.
        private static IEnumerable<(float From, float To)> Around(List<(float Middle, float Half)> breaks)
        {
            if (breaks.Count == 0)
            {
                yield return (0f, 360f);
                yield break;
            }
            breaks.Sort((a, c) => a.Middle.CompareTo(c.Middle));
            for (int i = 0; i < breaks.Count; i++)
            {
                (float m0, float h0) = breaks[i];
                (float m1, float h1) = breaks[(i + 1) % breaks.Count];
                float from = m0 + h0, to = m1 - h1 + (i + 1 == breaks.Count ? 360f : 0f);
                if (to - from > 0.5f) yield return (from, to);
            }
        }

        // The bars on the footing all round, and over each pen's gate shorter bars from its top, so that the cage leans in overhead in one line all the way round.
        private void Bars()
        {
            float halfBlock = MathHelper.RadiansToDegrees(MathF.Asin((PenWidth * 0.5f + 1.3f) / BarRing));
            foreach ((float from, float to) in Around(_pens.Select(p => ((p + 360f) % 360f, halfBlock)).ToList()))
            {
                int pieces = Math.Max(1, (int)MathF.Ceiling((to - from) / 28f));
                for (int p = 0; p < pieces; p++)
                {
                    float a = from + (to - from) * p / pieces, c = from + (to - from) * (p + 1) / pieces;
                    Piece(new ArenaSpec(ArenaShape.Bars, NextSeed(), Cm(BarRing), (int)MathF.Round(a), (int)MathF.Round(c - a), Cm(BarHeight), Cm(BarLean), Cm(BarSpacing)), Vector2.Zero, 0f, FootingTop);
                }
            }
            foreach (float pen in _pens)
            {
                float bearing = (pen + 360f) % 360f;
                Piece(new ArenaSpec(ArenaShape.Bars, NextSeed(), Cm(BarRing), (int)MathF.Round(bearing - halfBlock), (int)MathF.Round(2f * halfBlock), Cm(FootingTop + BarHeight - PenBlock), Cm(BarLean), Cm(BarSpacing)), Vector2.Zero, 0f, PenBlock);
            }
        }

        // Each pen's gate in the footing, its door shut, and the floor in front of it where what's let out is first seen.
        private void Pens()
        {
            foreach (float bearing in _pens)
            {
                Vector2 outward = Toward(bearing);
                Vector2 at = outward * (_radius - 0.2f);
                float yaw = YawFacing(-outward);
                Piece(new ArenaSpec(ArenaShape.PenGate, NextSeed(), (int)Stone, Cm(PenWidth), Cm(PenHeight), Cm(PenDepth), Cm(PenBlock)), at, yaw, 0f);
                float half = PenWidth * 0.5f + 1.3f;
                Solid(Footprint.Box(C + at + outward * (PenDepth * 0.5f), new Vector2(half, PenDepth * 0.5f + 0.2f), yaw));
                Raise(Footprint.Box(C + at + outward * (PenDepth * 0.5f), new Vector2(PenWidth * 0.5f + 0.3f, PenDepth * 0.5f), yaw), 0f);
                string door = new ArenaSpec(ArenaShape.Portcullis, NextSeed(), (int)ArenaGrille.Pen, Cm(PenWidth + 0.1f), Cm(PenHeight + 0.25f)).Id;
                Vector2 hangs = at + outward * 0.15f;
                Plan.Entries.Add(new ArenaEntry(ArenaEntryKind.PenDoor, C + outward * (_radius - 2.8f), new Vector3(C.X + hangs.X, F, C.Y + hangs.Y), yaw, door, DoorRise));
                // An amber lamp on the gate's face, to see what's coming out by.
                Lamp(4, at - outward * 0.25f, yaw, PenHeight + 2.2f);
            }
        }

        // The seal, round the footing's face, all the way round: the waygate stands inside the cage, and the pens' gates are solid of themselves.
        private void Seal()
        {
            SealArc(_radius, 0f, 360f, FootingThick);
            foreach (Vector2 p in ArcPoints(_radius, 0f, 360f)) Plan.Bounds.Add(C + p);
            Plan.Bounds.RemoveAt(Plan.Bounds.Count - 1);
        }

        // What's in the pen: the Institute's nests, which the game hatches (ArenaSystem), straw trodden into the mud, troughs along the edge, a couple of gibbets with what's being fed on them, and the
        // remains of what's been fed already, all kept clear of the waygate and of where the pens let out. Nothing behind the waygate, where the footing would hide anyone.
        private void Floor(Vector2 gate)
        {
            var clear = new List<(Vector2, float)> { (C + gate, 7f), (C + North * 4.5f, 5f) };
            foreach (ArenaEntry entry in Plan.Entries) clear.Add((entry.Out, 4.5f));
            bool Clear(Vector2 at, float radius) => clear.All(c => (C + at - c.Item1).Length > c.Item2 + radius);

            for (int i = 0, placed = 0; i < _nests * 12 && placed < _nests; i++)
            {
                float bearing = (placed * 360f / _nests + Random.Range(-18f, 18f) + 20f) % 360f;
                Vector2 at = Toward(bearing) * Random.Range(_radius * 0.38f, _radius * 0.7f);
                if (!Clear(at, 2f) || Plan.Pieces.Any(p => (new Vector2(p.At.X, p.At.Z) - (C + at)).Length < 6f)) continue;
                Piece(new ArenaSpec(ArenaShape.Nest, NextSeed(), Cm(3.4f)), at, Random.Float() * MathF.Tau, 0f);
                // The eggs are a body of their own, which the arena puts in the nest and takes away when they hatch or are smashed.
                string clutch = new ArenaSpec(ArenaShape.Clutch, NextSeed(), Cm(3.4f)).Id;
                Plan.Pieces.Add(new ArenaPiece(ArenaPieceKind.Clutch, new Vector3(C.X + at.X, F, C.Y + at.Y), Random.Float() * MathF.Tau, clutch));
                clear.Add((C + at, 2.2f));
                placed++;
            }

            for (int i = 0; i < (Keep ? 4 : 7); i++)
            {
                Vector2 at = Toward(Random.Range(0f, 360f)) * MathF.Sqrt(Random.Float()) * (_radius - 3f);
                if (!Clear(at, 1.5f)) continue;
                Piece(new ArenaSpec(ArenaShape.Remains, NextSeed(), 2, (int)MathF.Round(Random.Range(0.7f, 1.2f) * 100f)), at, Random.Float() * MathF.Tau, 0f);
            }
            int troughs = Keep ? 2 : 3;
            for (int i = 0, placed = 0; i < troughs * 8 && placed < troughs; i++)
            {
                float bearing = Random.Range(0f, 360f);
                Vector2 at = Toward(bearing) * (_radius - 1.9f);
                if (!Clear(at, 2.6f)) continue;
                Piece(new ArenaSpec(ArenaShape.Trough, NextSeed(), (int)Stone, Cm(4.2f)), at, YawFacing(Toward(bearing)) + MathF.PI * 0.5f, 0f);
                Solid(Footprint.Box(C + at, new Vector2(2.2f, 0.65f), YawFacing(Toward(bearing)) + MathF.PI * 0.5f), 1.1f);
                clear.Add((C + at, 2.4f));
                placed++;
            }
            int gibbets = Keep ? 1 : 2;
            for (int i = 0, placed = 0; i < gibbets * 24 && placed < gibbets; i++)
            {
                float bearing = Random.Range(0f, 360f);
                Vector2 at = Toward(bearing) * (_radius - 2.4f);
                if (!Clear(at, 1.6f)) continue;
                Piece(new ArenaSpec(ArenaShape.Gibbet, NextSeed(), Cm(7.5f)), at, YawFacing(-Toward(bearing)), 0f);
                Solid(Footprint.Circle(C + at, 0.7f));
                clear.Add((C + at, 3f));
                placed++;
            }
            Remains(Keep ? 2 : 3, Keep ? 1 : 3, clear);
            // Nothing walks behind the waygate.
            Solid(Footprint.Box(C + gate - North * 1.2f, new Vector2(4.4f, 1.2f), 0f));
        }

        // The Institute's gallery over the ground outside the bars on the north, where the blood-doctors watch.
        private void Gallery()
        {
            Piece(new ArenaSpec(ArenaShape.Gallery, NextSeed(), Cm(_radius + 4.6f), -48, 96, Cm(3.4f), Cm(3.2f)), Vector2.Zero, 0f, Sunk);
        }

        // Round the cage outside: amber lamps on poles, braziers, and the Reich's banners on masts.
        private void Round()
        {
            float ring = _radius + (Keep ? 4.5f : 8.6f);
            int count = Keep ? 8 : 14;
            for (int k = 0; k < count; k++)
            {
                float bearing = (k + 0.5f) * 360f / count;
                if (!Keep && Near(bearing, 0f, 55f)) continue;
                Vector2 at = Toward(bearing) * ring;
                if (k % 2 == 0) Brazier(at, 0, 1f, Sunk);
                else
                {
                    Piece(new ArenaSpec(ArenaShape.Banner, NextSeed(), Cm(10f), Cm(5f), Cm(1.8f), 0), at, YawFacing(-at), Sunk);
                    Solid(Footprint.Circle(C + at, 0.6f));
                }
            }
        }

        // The land's people, made to stand round the bars, though not under the gallery.
        private void People()
        {
            foreach (float radius in Keep ? new[] { _radius + 3.2f, _radius + 6f } : new[] { _radius + 3.4f, _radius + 6.4f, _radius + 9.6f })
            {
                float step = 360f / (MathF.Tau * radius / 3.3f);
                Watchers(radius, step, radius, bearing => (Keep || !Near(bearing, 0f, 52f)) && !_pens.Any(p => Near(bearing, (p + 360f) % 360f, PenHalfAngle + 3f)) && !Near(bearing, 180f, 8f), share: 0.7f);
            }
        }
    }
}
