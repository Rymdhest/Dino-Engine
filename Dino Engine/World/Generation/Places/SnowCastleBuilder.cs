using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a snow castle (PLACES.md) in the snow, as the north's children build one each winter, older now than this winter and kept by the frost's cult: walls of packed snow round a
    /// court with a tower at each corner and an arch where the way in is, towards the camera; inside, the cult's altar of ice burning cold at its heart, snowmen standing about, lanterns of
    /// snowballs lit round it, pennants on its towers, and the children's sledges and spades left where they dropped them. The cult and the wolves it runs with hold it.
    /// </summary>
    internal sealed class SnowCastleBuilder : PlaceBuilder
    {
        private float _hx, _hz;
        private Vector2 _heart;

        private SnowCastleBuilder(SiteBuilder b) : base(b, "snow castle") { }

        public static void Build(SiteBuilder b) => new SnowCastleBuilder(b).Lay();

        private void Lay()
        {
            Plan.Yaw = MathF.PI + Random.Range(-0.25f, 0.25f);
            _hx = Site.Radius * Random.Range(0.42f, 0.52f);
            _hz = Site.Radius * Random.Range(0.36f, 0.46f);
            Plan.Half = new Vector2(_hx + 3f, _hz + 3f);
            Plan.Gates.Add((W(new Vector2(0f, _hz)), Plan.Dir(Vector2.UnitY)));
            Walls(Random.Fork("walls"));
            Court(Random.Fork("court"));
            Frost(Random.Fork("frost"));
            Hold(Random.Fork("holders"));
        }

        // The walls round the court in stretches, broken for the arch in the front, and a tower at each corner, its pennant on top.
        private void Walls(WorldRandom r)
        {
            float wallHigh = r.Range(1.5f, 2.2f);
            Vector2[] corners = { new(-_hx, -_hz), new(_hx, -_hz), new(_hx, _hz), new(-_hx, _hz) };
            float gateHalf = r.Range(1.4f, 1.9f);
            for (int side = 0; side < 4; side++)
            {
                Vector2 a = corners[side], c = corners[(side + 1) % 4];
                float length = (c - a).Length;
                Vector2 along = (c - a) / length;
                // Stretches between the towers, leaving the towers' room at each end and the gate's in the front.
                var spans = new List<(float From, float To)>();
                const float TowerRoom = 2.2f;
                if (side == 2)
                {
                    float middle = length * 0.5f;
                    spans.Add((TowerRoom, middle - gateHalf - 1.7f));
                    spans.Add((middle + gateHalf + 1.7f, length - TowerRoom));
                }
                else spans.Add((TowerRoom, length - TowerRoom));
                foreach ((float from, float to) in spans)
                {
                    if (to - from < 1.2f) continue;
                    int pieces = Math.Max(1, (int)MathF.Round((to - from) / 5f));
                    float each = (to - from) / pieces;
                    for (int k = 0; k < pieces; k++)
                    {
                        Vector2 mid = a + along * (from + each * (k + 0.5f));
                        float yaw = YawOf(new Vector2(along.Y, -along.X));
                        float high = wallHigh * r.Range(0.85f, 1.1f);
                        Footprint f = Footprint.Box(W(mid), new Vector2(each * 0.5f, 0.55f), yaw);
                        if (!Ground(f, 0.45f) || !Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.1f)) continue;
                        // Its solid stops short of its ends, so that the stretches beside it never touch it, with no gap anyone gets through.
                        World.Placement.Add(Footprint.Segment(W(mid - along * (each * 0.5f - 0.6f)), W(mid + along * (each * 0.5f - 0.6f)), 0.5f), PlacementLayer.Structure, solid: true, height: high);
                        World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.SnowWall, NextSeed(), PlaceSpec.Cm(each + 0.02f), PlaceSpec.Cm(high)).Id, W(mid), yaw, Vector3.One, null));
                    }
                }
            }
            foreach (Vector2 corner in corners)
            {
                float radius = r.Range(1.5f, 2.1f), height = r.Range(3.6f, 5.2f);
                int tiers = r.Int(2, 3);
                if (!PutRound(new PlaceSpec(PlacePiece.SnowTower, NextSeed(), PlaceSpec.Cm(radius), PlaceSpec.Cm(height), tiers), W(corner), r.Float() * MathF.Tau, radius + 0.3f, height, 0.1f, 0.45f)) continue;
                // Stuck in the tip of its cone, where it flies clear of it: each tier is 0.78 of the one under it, and the cone 1.4 times the last's radius high (SnowWorks.Tower).
                float tip = height + radius * MathF.Pow(0.78f, tiers - 1) * 1.4f;
                // A pennant on top, in one of the land's colours the children flew; on the back towers the cult's violet, hung where its watch stands.
                float size = r.Range(1.4f, 2f);
                int paint = r.Int(0, 5);
                World.Pending.Add(new PendingObject(new PlaceSpec(PlacePiece.Pennant, NextSeed(), PlaceSpec.Cm(size), corner.Y < 0f ? SnowWorks.CultPennant : paint).Id, W(corner), r.Float() * MathF.Tau, Vector3.One, World.Elevation.Sample(W(corner)) + tip - 0.45f));
            }
            // The gatehouse over the way in, its sides reaching out to where the front wall's stretches begin: solid all that way, but short of their solids.
            var gate = new PlaceSpec(PlacePiece.SnowGate, NextSeed(), PlaceSpec.Cm(gateHalf * 2f), PlaceSpec.Cm(r.Range(2.8f, 3.6f)));
            World.Pending.Add(new PendingObject(gate.Id, W(new Vector2(0f, _hz)), YawOf(Vector2.UnitY), Vector3.One, null));
            float jamb = SnowWorks.GateSide - 0.15f, gateTop = SnowWorks.GateTop(gate.Metres(0), gate.Metres(1));
            foreach (float s in new[] { -1f, 1f }) World.Placement.Add(Footprint.Box(W(new Vector2(s * (gateHalf + jamb * 0.5f), _hz)), new Vector2(jamb * 0.5f, 0.55f), Plan.Yaw), PlacementLayer.Structure, solid: true, height: gateTop);
            Keep(Footprint.Box(W(new Vector2(0f, _hz)), new Vector2(gateHalf, 3f), Plan.Yaw));
        }

        // The court: the altar of ice at its heart, lanterns of snowballs round it, snowmen about, sledges dropped, and a pennant or two stuck in the snow.
        private void Court(WorldRandom r)
        {
            Vector2 heart = _heart = new Vector2(r.Range(-2f, 2f), r.Range(-_hz * 0.35f, -_hz * 0.1f));
            PutBox(new PlaceSpec(PlacePiece.IceAltar, NextSeed()), W(heart), YawOf(Vector2.UnitY), new Vector2(2.2f, 2.2f), 1.4f, 0.2f, 0.45f);
            int lanterns = r.Int(3, 5);
            for (int k = 0; k < lanterns; k++)
            {
                float a = k * MathF.Tau / lanterns + r.Range(-0.3f, 0.3f);
                Tries(4, () => PutRound(new PlaceSpec(PlacePiece.SnowLantern, NextSeed(), PlaceSpec.Cm(r.Range(0.8f, 1.2f))), W(heart + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r.Range(3.6f, 4.6f)), r.Float() * MathF.Tau, 0.6f, 0.9f, 0.1f, 0.45f));
            }
            int snowmen = r.Int(2, 4);
            for (int k = 0; k < snowmen; k++) Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Snowman, NextSeed(), PlaceSpec.Cm(r.Range(1.4f, 2.1f)), r.Chance(0.5f) ? 1 : 0), W(new Vector2(r.Range(-_hx + 2f, _hx - 2f), r.Range(-_hz + 2f, _hz - 2f))), r.Float() * MathF.Tau, 0.7f, 1.6f, 0.2f, 0.45f));
            // Outside it as well: a snowman by the gate, and the children's sledges.
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Snowman, NextSeed(), PlaceSpec.Cm(r.Range(1.2f, 1.8f)), r.Chance(0.5f) ? 1 : 0), W(new Vector2((r.Chance(0.5f) ? 1f : -1f) * r.Range(3f, 6f), _hz + r.Range(2.5f, 5f))), r.Float() * MathF.Tau, 0.7f, 1.4f, 0.2f, 0.45f));
            for (int k = 0; k < 2; k++) Tries(6, () => Lay(new PlaceSpec(PlacePiece.Sledges, NextSeed(), r.Int(1, 3)), W(new Vector2(r.Range(-_hx - 4f, _hx + 4f), r.Chance(0.5f) ? r.Range(-_hz + 2f, _hz - 2f) : _hz + r.Range(3f, 7f))), r.Float() * MathF.Tau, 2f));
            Tries(6, () => PutRound(new PlaceSpec(PlacePiece.Pennant, NextSeed(), PlaceSpec.Cm(r.Range(1.6f, 2.4f)), r.Int(0, 5)), W(new Vector2(r.Range(-_hx + 1.5f, _hx - 1.5f), r.Range(-_hz + 1.5f, _hz - 1.5f))), r.Float() * MathF.Tau, 0.2f, 0f, 0.1f, 0.5f));
        }

        // The cult round its altar, the wolves about the walls, and the chest: what the cult takes from those who come to it, by the altar.
        // The frost's cult's (PLACES.md): its spire of ice grown up in the court behind the altar, its totems either side of the gate, its torches round the court burning cold, and the
        // children's snowballs still heaped along the walls where they'd last played.
        private void Frost(WorldRandom r)
        {
            Tries(10, () => PutRound(new PlaceSpec(PlacePiece.CultHeart, NextSeed(), (int)Cult.Frost, PlaceSpec.Cm(r.Range(1.4f, 1.9f))), W(new Vector2(r.Range(-3f, 3f), -_hz * r.Range(0.35f, 0.55f))), r.Float() * MathF.Tau, 1.9f, Tall, 0.3f, 0.4f));
            foreach (float s in new[] { -1f, 1f }) PutRound(new PlaceSpec(PlacePiece.Totem, NextSeed(), PlaceSpec.Cm(r.Range(3f, 3.8f)), (int)Cult.Frost), W(new Vector2(s * r.Range(4.5f, 6f), _hz + r.Range(2.5f, 4f))), r.Float() * MathF.Tau, 0.6f, Tall, 0.2f, 0.45f);
            for (int k = 0; k < 4; k++) PutRound(new PlaceSpec(PlacePiece.Torch, NextSeed(), PlaceSpec.Cm(r.Range(2.2f, 2.6f)), 3), W(new Vector2(r.Range(-_hx * 0.7f, _hx * 0.7f), r.Range(-_hz * 0.6f, _hz * 0.6f))), 0f, 0.35f, 0f, 0.2f, 0.45f);
            for (int k = 0; k < 6; k++) Strew(LitterKind.Snow, new Vector2(r.Range(-_hx + 1.5f, _hx - 1.5f), (r.Chance(0.5f) ? 1f : -1f) * (_hz - r.Range(1.2f, 2.2f))), r.Range(1.2f, 2f), r.Int(8, 14));
            Strew(LitterKind.Bones, new Vector2(r.Range(-2f, 2f), -_hz * 0.3f), 1.8f, r.Int(5, 9));
            Trodden(r.Fork("trodden"));
            Kill(r.Fork("kill"));
        }

        // Where feet have gone through the snow to the earth under it: in at the gate and up to the altar, round and round the altar, and out to the back towers where the cult keeps
        // its watch. White on white says nothing from up there, and this says where everyone goes.
        private void Trodden(WorldRandom r)
        {
            Trail(new Vector2(0f, _hz + 5f), _heart + new Vector2(0f, 1.5f), r.Range(1.6f, 2.1f));
            Paint(GroundPaint.Yard, Footprint.Circle(W(_heart), r.Range(3.4f, 4.2f)), 1.8f);
            foreach (float s in new[] { -1f, 1f }) Trail(_heart, new Vector2(s * (_hx - 2.2f), -_hz + 2.2f), r.Range(0.7f, 1f));
        }

        // What the wolves brought down, outside the walls where the cult lets them feed, red in the snow round it, its bones dragged off and their track to it from the gate; and before
        // the altar, what the cult has given its cold.
        private void Kill(WorldRandom r)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float side = r.Chance(0.5f) ? 1f : -1f;
                Vector2 local = new Vector2(side * (_hx + r.Range(3f, 6.5f)), r.Range(-_hz * 0.8f, _hz * 0.8f));
                float length = r.Range(4.5f, 6.5f);
                if (!PutBox(new PlaceSpec(PlacePiece.Carcass, NextSeed(), r.Chance(0.6f) ? 0 : 1, PlaceSpec.Cm(length)), W(local), r.Float() * MathF.Tau, new Vector2(length * 0.25f, length * 0.5f), 0.9f, 0.4f, 0.45f)) continue;
                Strew(LitterKind.Blood, local, r.Range(2.6f, 3.4f), r.Int(16, 24));
                Strew(LitterKind.Bones, local + new Vector2(r.Range(-3f, 3f), r.Range(-3f, 3f)), 2.6f, r.Int(8, 12));
                Trail(local, new Vector2(side * 2.5f, _hz + 4f), 0.8f);
                break;
            }
            Strew(LitterKind.Blood, _heart + new Vector2(0f, 1.9f), 1.1f, r.Int(5, 8));
        }

        private void Hold(WorldRandom r)
        {
            Stand(SpawnRole.Cultist, Around(Plan.Centre, MathF.Min(_hx, _hz) * 0.8f, r.Fork("court"), 40), r.Int(3, 5), 3.5f, r.Fork("cult"));
            Stand(SpawnRole.Beast, Around(Plan.Centre, Plan.Half.Length * 1.1f, r.Fork("about"), 40), r.Int(2, 3), 4.5f, r.Fork("wolves"));
            Chest(W(new Vector2(r.Range(-3f, 3f), -_hz * 0.55f)), MathF.Min(_hx, _hz));
        }
    }
}
