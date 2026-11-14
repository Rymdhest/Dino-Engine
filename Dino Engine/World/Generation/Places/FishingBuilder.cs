using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Places
{
    /// <summary>
    /// Lays out a fishing village (PLACES.md) along its shore, facing the water: the jetty run out from the beach to where a boat floats, its boats moored at it and drawn up on the shingle
    /// with the capstan above them, a boat on the stocks, the nets and the fish hung to dry between the water and the houses, the smokehouse smoking, the pots by the jetty, and the houses in
    /// the land's own way of building, back from the water's edge. Raiders hold it for the catch, which goes to the Reich's broods, and its fishers come back to it once it's freed.
    /// </summary>
    internal sealed class FishingBuilder : PlaceBuilder
    {
        private float _shore;   // Where the water's edge is along the place's own z, which runs out to the water.

        private FishingBuilder(SiteBuilder b) : base(b, "fishing") { }

        public static void Build(SiteBuilder b) => new FishingBuilder(b).Lay();

        private void Lay()
        {
            Vector2 toWater = B.FindWater(95f, out Vector2 water, out _) ? water : -Site.RoadDirection;
            Plan.Yaw = SiteBuilder.YawFacing(toWater);
            Plan.Half = new Vector2(Site.Radius);
            _shore = ShoreAlong(0f) ?? Site.Radius * 0.5f;
            Paint(GroundPaint.Yard, Footprint.Box(W(new Vector2(0f, _shore - 14f)), new Vector2(Site.Radius * 0.75f, 13f), Plan.Yaw), 8f);
            Waterside(Random.Fork("waterside"));
            Houses(Random.Fork("houses"));
            Drying(Random.Fork("drying"));
            Hold(Random.Fork("holders"));
        }

        // How far out along the place's z, at a point along its x, the land meets the sea, if it does within reach.
        private float? ShoreAlong(float x)
        {
            for (float z = -10f; z < Site.Radius * 2.2f; z += 1f)
            {
                Vector2 at = W(new Vector2(x, z));
                if (!World.InBounds(at)) return null;
                if (World.Elevation.Sample(at) < WorldPlan.SeaLevel + 0.15f) return z;
            }
            return null;
        }

        private float DepthAt(Vector2 local) => WorldPlan.SeaLevel - World.Elevation.Sample(W(local));

        // The jetty, out from the beach to where a boat floats, and the boats at it and on the shingle, the capstan above them, a boat on the stocks and the pots.
        private void Waterside(WorldRandom r)
        {
            float width = r.Range(1.9f, 2.6f);
            float? root = null, rootX = 0f, length = 0f, depth = 0f;
            foreach (float x in new[] { 0f, 6f, -6f, 12f, -12f, 18f, -18f })
            {
                if (ShoreAlong(x) is not float shore) continue;
                // Out until a boat floats, or as far as a jetty goes, and stopping short of land again: the water's edge itself may stand a hair over the sea.
                float end = shore;
                for (float z = shore + 1f; z < shore + 28f; z += 1f)
                {
                    float d = DepthAt(new Vector2(x, z));
                    if (d < -0.3f || !World.InBounds(W(new Vector2(x, z)))) break;
                    end = z;
                    if (d >= 1.4f) break;
                }
                float run = end - (shore - 2f);
                if (run < 8f) continue;
                Footprint f = Footprint.Box(W(new Vector2(x, shore - 2f + run * 0.5f)), new Vector2(width * 0.5f + 0.6f, run * 0.5f), Plan.Yaw);
                if (!Clear(f, PlacementLayer.Built | PlacementLayer.Road, 0.4f)) continue;
                World.Placement.Add(f, PlacementLayer.Structure, solid: true, height: 0.95f);
                root = shore - 2f;
                rootX = x;
                length = run;
                depth = Math.Clamp(DepthAt(new Vector2(x, end)), 0.3f, 3f);
                break;
            }
            if (root is float rootZ)
            {
                var jetty = new PlaceSpec(PlacePiece.Jetty, NextSeed(), PlaceSpec.Cm(length!.Value), PlaceSpec.Cm(width), PlaceSpec.Cm(depth!.Value));
                World.Pending.Add(new PendingObject(jetty.Id, W(new Vector2(rootX!.Value, rootZ)), Plan.Yaw, Vector3.One, WorldPlan.SeaLevel + 0.9f));
                // Boats moored along it where they float.
                int moored = r.Int(1, 2);
                for (int k = 0; k < moored; k++)
                {
                    float boatLength = r.Range(4.5f, 7f);
                    float side = (k == 0) == r.Chance(0.5f) ? 1f : -1f;
                    Vector2 local = new Vector2(rootX.Value + side * (width * 0.5f + boatLength * 0.18f + 0.45f), rootZ + length.Value * r.Range(0.5f, 0.85f));
                    if (DepthAt(local) < 0.5f) continue;
                    var boat = new PlaceSpec(PlacePiece.Boat, NextSeed(), PlaceSpec.Cm(boatLength), 0);
                    World.Pending.Add(new PendingObject(boat.Id, W(local), Plan.Yaw + (r.Chance(0.5f) ? 0f : MathF.PI) + r.Range(-0.08f, 0.08f), Vector3.One, WorldPlan.SeaLevel));
                }
                Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Pots, NextSeed()), W(new Vector2(rootX.Value + (r.Chance(0.5f) ? 1f : -1f) * r.Range(3f, 6f), rootZ - r.Range(1f, 4f))), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(1.6f, 1.2f), 1.3f, 0.3f, 0.5f));
            }
            // Boats drawn up on the shingle, bows to the water, one maybe over on its side, and the capstan above the first.
            int drawn = r.Int(1, 3);
            for (int k = 0; k < drawn; k++)
            {
                float boatLength = r.Range(4f, 6.5f), beam = boatLength * 0.36f;
                Tries(8, () =>
                {
                    float x = (r.Chance(0.5f) ? 1f : -1f) * r.Range(7f, Site.Radius * 0.8f);
                    if (ShoreAlong(x) is not float shore) return false;
                    Vector2 local = new Vector2(x, shore - boatLength * 0.5f - r.Range(0.5f, 3.5f));
                    float yaw = YawOf(Vector2.UnitY) + r.Range(-0.45f, 0.45f);
                    // Its bow may be in the shallows: the shingle at the water's edge is what it's drawn up on.
                    var boat = new PlaceSpec(PlacePiece.Boat, NextSeed(), PlaceSpec.Cm(boatLength), k == 2 || r.Chance(0.2f) ? 2 : 1);
                    if (!Put(boat, W(local), yaw, Footprint.Box(W(local), new Vector2(beam * 0.5f + 0.5f, boatLength * 0.5f + 0.3f), yaw), 1.1f, PlacementLayer.Prop, 0.5f, 0.3f, wet: 0.3f)) return false;
                    if (k == 0) Tries(4, () => PutBox(new PlaceSpec(PlacePiece.Capstan, NextSeed()), W(local - new Vector2(r.Range(-1f, 1f), boatLength * 0.5f + r.Range(3.5f, 5f))), yaw, new Vector2(1.9f, 1.6f), 1.2f, 0.3f, 0.5f));
                    return true;
                });
            }
            if (r.Chance(0.55f))
            {
                float boatLength = r.Range(5f, 7.5f);
                Tries(8, () => PutBox(new PlaceSpec(PlacePiece.BoatStocks, NextSeed(), PlaceSpec.Cm(boatLength)), W(new Vector2(r.Range(-Site.Radius * 0.6f, Site.Radius * 0.6f), _shore - r.Range(5f, 10f))), YawOf(Vector2.UnitY) + r.Range(-0.3f, 0.3f), new Vector2(boatLength * 0.36f + 1.2f, boatLength * 0.5f + 0.4f), 1.4f, 0.4f, 0.4f));
            }
        }

        // The houses back from the water in a loose row along it, each its door to the shore, in the land's own way of building, and the smokehouse among them.
        private void Houses(WorldRandom r)
        {
            int houses = r.Int(3, 5);
            float spacing = Site.Radius * 1.5f / houses;
            for (int k = 0; k < houses; k++)
            {
                float x = (k - (houses - 1) * 0.5f) * spacing;
                Tries(6, () => Structure(StructureCatalog.Hut, W(new Vector2(x + r.Range(-2.5f, 2.5f), _shore - r.Range(16f, 24f))), YawOf(Vector2.UnitY) + r.Range(-0.25f, 0.25f), 0.8f, 0.4f));
            }
            Tries(10, () => PutBox(new PlaceSpec(PlacePiece.Smokehouse, NextSeed()), W(new Vector2(r.Range(-Site.Radius * 0.7f, Site.Radius * 0.7f), _shore - r.Range(9f, 15f))), YawOf(Vector2.UnitY) + r.Range(-0.4f, 0.4f), new Vector2(2.4f, 2f), 3.6f, 0.5f, 0.4f));
        }

        // Between the houses and the water, the nets and the fish hung to dry along the shore, the barrels the catch is salted in, the crates the Reich's share goes away in, and the fire.
        private void Drying(WorldRandom r)
        {
            int nets = r.Int(1, 2), fish = r.Int(1, 2);
            for (int k = 0; k < nets + fish; k++)
            {
                bool net = k < nets;
                float length = net ? r.Range(5f, 8f) : r.Range(3.5f, 6f);
                PlacePiece kind = net ? PlacePiece.NetRack : PlacePiece.FishRack;
                Tries(10, () => PutBox(new PlaceSpec(kind, NextSeed(), PlaceSpec.Cm(length)), W(new Vector2(r.Range(-Site.Radius * 0.75f, Site.Radius * 0.75f), _shore - r.Range(4f, 12f))), YawOf(Vector2.UnitY) + r.Range(-0.2f, 0.2f), new Vector2(length * 0.5f + 0.3f, 1f), 2.4f, 0.4f, 0.45f));
            }
            for (int k = 0; k < 2; k++) Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Barrels, NextSeed(), r.Int(2, 5)), W(new Vector2(r.Range(-Site.Radius * 0.6f, Site.Radius * 0.6f), _shore - r.Range(5f, 16f))), r.Float() * MathF.Tau, new Vector2(1.6f), 1.4f, 0.4f, 0.45f));
            Tries(8, () => PutBox(new PlaceSpec(PlacePiece.Crates, NextSeed(), r.Int(3, 5), 1), W(new Vector2(r.Range(-Site.Radius * 0.5f, Site.Radius * 0.5f), _shore - r.Range(6f, 14f))), r.Float() * MathF.Tau, new Vector2(2.2f, 1.3f), 1.8f, 0.4f, 0.45f));
            Tries(8, () => PutRound(new PlaceSpec(PlacePiece.Campfire, NextSeed()), W(new Vector2(r.Range(-8f, 8f), _shore - r.Range(8f, 14f))), 0f, 1.4f, 0.8f, 0.5f, 0.45f));
        }

        // The raiders about the houses and the shore, the fishers who'll come back once it's free, and what's been taken in a chest by the fire.
        private void Hold(WorldRandom r)
        {
            Vector2 middle = W(new Vector2(0f, _shore - 12f));
            Stand(SpawnRole.Raider, Around(middle, Site.Radius * 0.65f, r.Fork("raiders"), 60), r.Int(4, 6), 4.5f, r.Fork("raider stand"));
            Stand(SpawnRole.Returning, Around(W(new Vector2(0f, _shore - 7f)), Site.Radius * 0.6f, r.Fork("fishers"), 40), r.Int(2, 3), 4f, r.Fork("fisher stand"));
            Chest(W(new Vector2(r.Range(-5f, 5f), _shore - 14f)), Site.Radius * 0.5f);
        }
    }
}
