using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Fills the land with what there is to fight. What a player feels is the time between fights, so packs stand a short run apart, yet far enough that
    /// each can be taken on alone: the gap has to clear two creatures' range of notice. Every pack has a reason to be where it is: it holds a camp, keeps watch
    /// outside a stronghold, lies in wait by a road, or has made the wild its home, and who it is depends on whose country it stands in. Quiet noise opens the
    /// spacing up here and there, as an even carpet of packs reads as wallpaper. This runs last, on the finished <see cref="WorldData.Passable"/>, so there's
    /// nothing to repair: a spot is either good now or it's never used.
    /// </summary>
    public sealed class PackStage : IWorldStage
    {
        private const float FormationStep = 2.7f;
        private const float StandingDepth = 0.3f;       // Metres of water a pack is willing to stand about in.
        private const float QuietSize = 420f;
        private const float LeastSpacing = 40f;         // However many are asked for. Noise closes this to 34 m, still clear of how far a cry carries, so a fight can't wake the whole land.

        // A settlement keeps the land around it. The landing most of all: it's where people get their bearings, though the first fight should be in sight of it.
        private const float LandingMargin = 46f;
        private const float SettlementMargin = 34f;
        private const float DangerMargin = 28f;
        private const float WaysideMargin = 14f;

        public string Description => "Stirring the wilds";
        public float Cost => 0.2f;

        /// <summary>Metres between packs in this world.</summary>
        public static float Spacing(int level, float morePacks = 0f) => MathF.Max(Lerp(66f, 56f, Saturate(MathF.Log(MathF.Max(level, 1)) / MathF.Log(30f))) / MathF.Sqrt(1f + MathF.Max(morePacks, 0f)), LeastSpacing);

        /// <summary>
        /// How many stand together, before anything is drawn. This climbs quickly and levels off, as worlds go up one at a time, and a player should
        /// feel the difference in the second and third rather than the thirtieth.
        /// </summary>
        public static float UsualSize(int level, float remoteness) => 3f + 2.2f * MathF.Log(MathF.Max(level, 1)) + 2f * remoteness;

        /// <summary>A pack's size: most are about the usual, some are stragglers and a few are out in force.</summary>
        /// <param name="crowd">How many times the usual number have been asked for.</param>
        public static int DrawSize(int level, float remoteness, WorldRandom random, float crowd = 1f)
        {
            float size = UsualSize(level, remoteness) * crowd * random.Range(0.85f, 1.15f);
            float roll = random.Float();
            if (roll < 0.14f) size *= 0.55f;
            else if (roll > 0.9f) size *= 1.6f;
            return Math.Clamp((int)MathF.Round(size), 2, (int)(18f * crowd));
        }

        public void Run(WorldData world)
        {
            // Nothing hostile ever stands in the Haven, and in an arena only what the arena holds, which the game brings in.
            if (world.Plan.Archetype.Enclosed) return;

            WorldRandom random = world.Random.Fork("packs");
            var pass = new Pass(world, random);

            foreach (WorldSite site in world.Sites) pass.Reinforce(site);

            // A land asked to be held only at its places has nobody between them: its camps stand empty, and its roads and wilds too.
            if (!world.Plan.Request.Emptied)
            {
                // Camps are already there, so they go first, and the rest find room around them.
                for (int i = 0; i < world.Haunts.Count; i++) pass.FillCamp(world.Haunts[i], i);

                foreach (WorldSite site in world.Sites)
                {
                    if (site.Definition.IsDanger) pass.PostPickets(site);
                }
                foreach (WorldRoad road in world.Roads) pass.LineRoad(road);
                pass.FillWild();
            }

            // A place that has lost everyone to the land around it, as a camp on a cliff can, is no use to anybody, but its props can stay.
            world.Haunts.RemoveAll(haunt => haunt.Spawns.Count == 0);
        }

        // One run over one world.
        private sealed class Pass
        {
            private readonly WorldData _world;
            private readonly WorldRandom _random;
            private readonly Territory _territory;
            private readonly WorldNoise _quiet;
            private readonly float _spacing;
            private readonly float _crowd;
            private readonly SpotIndex _taken;
            private int _drawn;

            public Pass(WorldData world, WorldRandom random)
            {
                _world = world;
                _random = random;
                _territory = new Territory(world);
                _quiet = new WorldNoise(random.Fork("quiet"));
                _spacing = Spacing(world.Plan.Level, world.Plan.Request.MorePacks);
                _crowd = 1f + world.Plan.Request.MorePeople;
                _taken = new SpotIndex(world.Size, _spacing);
            }

            private WorldRandom Next() => _random.Fork(++_drawn);

            private float SpacingAt(Vector2 at) => _spacing * (0.85f + 0.65f * _quiet.Fbm01(at.X, at.Y, QuietSize, 2));

            /// <summary>More of whoever already holds a place. They go on the end, as what has happened to the first ones is kept by number.</summary>
            public void Reinforce(WorldSite site)
            {
                // Every place a land asks to be freed is held by a group worth stopping for, and the keep by its whole garrison.
                float share = site.Kind switch
                {
                    SiteKind.Keep => 3f,
                    SiteKind.BanditCamp => 1.3f,
                    SiteKind.Graveyard or SiteKind.Ruins or SiteKind.Hamlet => 1f,
                    SiteKind.FishingHamlet or SiteKind.LumberCamp or SiteKind.StandingStones => 0.9f,
                    SiteKind.Watchtower or SiteKind.Shrine or SiteKind.Farmstead => 0.8f,
                    _ => 0f,
                };
                if (share <= 0f) return;

                SpawnRole? held = null;
                foreach (SiteSpawn spawn in site.Spawns)
                {
                    if (spawn.Role is SpawnRole.Raider or SpawnRole.Undead or SpawnRole.Beast or SpawnRole.Cultist) held = spawn.Role;
                    else if (spawn.Role == SpawnRole.Guard) held ??= SpawnRole.Raider;
                }
                if (held is not SpawnRole role) return;

                WorldRandom random = _random.Fork("sites").Fork(site.Id);
                int wanted = (int)MathF.Round(UsualSize(_world.Plan.Level, site.Remoteness) * share * _crowd);
                float reach = site.Radius * 0.72f;
                int placed = 0;
                for (int attempt = 0; attempt < wanted * 8 && placed < wanted; attempt++)
                {
                    Vector2 spot = site.Position + SiteBuilder.Direction(random.Float() * MathF.Tau) * (reach * MathF.Sqrt(random.Float()));
                    if (!CanStand(spot)) continue;
                    // A keep's garrison camps round the arena its tyrant is kept in, and nobody of it is on the arena's floor or behind its wall (ARENAS.md).
                    if (site.Arena is { Keep: true } arena && (spot - arena.Centre).Length < arena.Rim + 1.5f) continue;
                    site.Spawns.Add(new SiteSpawn(role, spot));
                    placed++;
                }
            }

            public void FillCamp(WorldSite camp, int index)
            {
                WorldRandom random = _random.Fork("camp").Fork(index);
                if (TryFindOpen(camp.Position, camp.Radius, random, out Vector2 centre))
                {
                    int size = (int)MathF.Round(DrawSize(_world.Plan.Level, camp.Remoteness, random, _crowd) * 1.6f);
                    Muster(camp, camp.Folk, centre, Math.Clamp(size, 4, (int)(20f * _crowd)), camp.Radius, random);

                    // A den holds what its hunters dragged home. Everyone else keeps theirs in a box.
                    if (camp.Spawns.Count > 0 && camp.Folk != SpawnRole.Beast && random.Chance(0.7f) && TryFindOpen(camp.Position, camp.Radius * 0.8f, random, out Vector2 chest))
                    {
                        camp.Spawns.Add(new SiteSpawn(SpawnRole.Chest, chest));
                    }
                }
                _taken.Add(camp.Position);
            }

            /// <summary>Those set to watch the ways in. The road gets the first of them, as that's the way most people come.</summary>
            public void PostPickets(WorldSite site)
            {
                WorldRandom random = _random.Fork("pickets").Fork(site.Id);
                int pickets = site.Kind == SiteKind.Keep ? 4 : random.Int(2, 3);
                float first = MathF.Atan2(site.RoadDirection.Y, site.RoadDirection.X);
                for (int i = 0; i < pickets; i++)
                {
                    float angle = first + i * (MathF.Tau / pickets) + random.Range(-0.3f, 0.3f);
                    Vector2 spot = site.Position + SiteBuilder.Direction(angle) * (site.Radius + DangerMargin + random.Range(4f, 18f));
                    SpawnRole folk = site.Kind == SiteKind.Graveyard ? SpawnRole.Undead : site.Kind == SiteKind.Ruins ? _territory.FolkAt(spot, random) : SpawnRole.Raider;
                    TryPack(HauntKind.Picket, spot, folk, Next());
                }
            }

            /// <summary>Those who wait beside a road. It's where most walking is done, so this is what decides how long a journey goes without a fight.</summary>
            public void LineRoad(WorldRoad road)
            {
                WorldRandom random = Next();
                float walked = 0f;
                float next = random.Range(0.3f, 1f) * _spacing;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 point = road.Points[i].Xz;
                    Vector2 from = road.Points[i - 1].Xz;
                    walked += (point - from).Length;
                    if (walked < next) continue;

                    Vector2 along = (point - from).LengthSquared > 1e-4f ? (point - from).Normalized() : Vector2.UnitX;
                    Vector2 side = new Vector2(-along.Y, along.X) * (random.Chance(0.5f) ? 1f : -1f);
                    Vector2 spot = point + side * (road.HalfWidth + random.Range(7f, 20f));

                    // Not here means a few metres on, so that something a pack can't stand on doesn't leave a hole in the road.
                    if (!TryPack(HauntKind.Ambush, spot, _territory.FolkAt(spot, random), Next())) continue;
                    next = walked + _spacing * random.Range(0.75f, 1.1f);
                }
            }

            /// <summary>Everywhere else, so that leaving the road is never the way to find nothing.</summary>
            public void FillWild()
            {
                WorldRandom random = Next();
                float cell = _spacing * 0.7f;
                int columns = (int)(_world.Size / cell) + 1;
                var cells = new List<int>(columns * columns);
                for (int i = 0; i < columns * columns; i++) cells.Add(i);
                random.Shuffle(cells);

                foreach (int index in cells)
                {
                    var corner = new Vector2(index % columns, index / columns) * cell;
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        Vector2 spot = corner + new Vector2(random.Float(), random.Float()) * cell;
                        if (TryPack(HauntKind.Den, spot, _territory.FolkAt(spot, random), Next())) break;
                    }
                }
            }

            private bool TryPack(HauntKind kind, Vector2 at, SpawnRole folk, WorldRandom random)
            {
                if (!CanStand(at) || !Clear(at) || _taken.AnyWithin(at, SpacingAt(at))) return false;

                float remoteness = _world.Remoteness.Sample(at);
                var haunt = new WorldSite
                {
                    Id = -1,
                    Definition = SiteCatalog.Haunt,
                    Haunt = kind,
                    Folk = folk,
                    Name = kind.ToString(),
                    Position = at,
                    Remoteness = remoteness,
                    Biome = _world.DominantBiome(at),
                };

                // A crowd needs the room of one: the same ground would only pack them into a single body.
                Muster(haunt, folk, at, DrawSize(_world.Plan.Level, remoteness, random, _crowd), FormationReach * MathF.Sqrt(_crowd), random);
                if (haunt.Spawns.Count < 2) return false;

                _taken.Add(at);
                _world.Haunts.Add(haunt);
                return true;
            }

            private const float FormationReach = 14f;

            // Stands them about the middle in a loose spiral, the first at the centre: that one leads, if anyone does.
            private void Muster(WorldSite haunt, SpawnRole folk, Vector2 centre, int size, float reach, WorldRandom random)
            {
                float farthest = 0f;
                float turn = random.Float() * MathF.Tau;
                bool camp = haunt.Haunt == HauntKind.Camp;
                for (int step = 0; step < size * 6 && haunt.Spawns.Count < size; step++)
                {
                    float out_ = FormationStep * MathF.Sqrt(step);
                    if (out_ > reach * 2f) break;
                    Vector2 spot = centre + SiteBuilder.Direction(turn + step * 2.39996f) * out_ + new Vector2(random.Range(-0.5f, 0.5f), random.Range(-0.5f, 0.5f));

                    // A camp's own tents are in the way of everything. Anywhere in it is with the rest.
                    if ((spot - haunt.Position).Length > reach || !CanStand(spot) || (!camp && !InSight(centre, spot))) continue;
                    haunt.Spawns.Add(new SiteSpawn(folk, spot));
                    farthest = MathF.Max(farthest, (spot - haunt.Position).Length);
                }

                if (haunt.Radius <= 0f) haunt.Radius = farthest + 1f;
            }

            private bool TryFindOpen(Vector2 around, float reach, WorldRandom random, out Vector2 found)
            {
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    Vector2 spot = around + SiteBuilder.Direction(random.Float() * MathF.Tau) * (reach * 0.8f * MathF.Sqrt(random.Float()));
                    if (!CanStand(spot)) continue;
                    found = spot;
                    return true;
                }
                found = around;
                return false;
            }

            private bool CanStand(Vector2 at) => _world.InBounds(at) && _world.Passable.IsSet(at) && _world.WaterDepthAt(at) < StandingDepth;

            // A pack is one group: anyone cut off from the rest by a wall or a stream isn't with them.
            private bool InSight(Vector2 from, Vector2 to)
            {
                int steps = Math.Max((int)((to - from).Length / 1.5f), 1);
                for (int i = 1; i < steps; i++)
                {
                    if (!_world.Passable.IsSet(Vector2.Lerp(from, to, (float)i / steps))) return false;
                }
                return true;
            }

            private bool Clear(Vector2 at)
            {
                foreach (WorldSite site in _world.Sites)
                {
                    float margin = site.Kind == SiteKind.Landing ? LandingMargin : site.Definition.IsSettlement ? SettlementMargin : site.Definition.IsDanger ? DangerMargin : WaysideMargin;
                    float reach = site.Radius + margin;
                    if ((site.Position - at).LengthSquared < reach * reach) return false;
                }
                return true;
            }
        }

        // Where packs already are, to ask quickly whether there's one near.
        private sealed class SpotIndex
        {
            private readonly float _cell;
            private readonly int _columns;
            private readonly List<Vector2>?[] _cells;

            public SpotIndex(float size, float cell)
            {
                _cell = cell;
                _columns = (int)(size / cell) + 2;
                _cells = new List<Vector2>?[_columns * _columns];
            }

            public void Add(Vector2 spot) => (_cells[Clamp((int)(spot.Y / _cell)) * _columns + Clamp((int)(spot.X / _cell))] ??= new List<Vector2>()).Add(spot);

            private int Clamp(int cell) => Math.Clamp(cell, 0, _columns - 1);

            public bool AnyWithin(Vector2 at, float reach)
            {
                int spread = (int)(reach / _cell) + 1;
                int centreX = (int)(at.X / _cell);
                int centreZ = (int)(at.Y / _cell);
                for (int z = Math.Max(centreZ - spread, 0); z <= Math.Min(centreZ + spread, _columns - 1); z++)
                {
                    for (int x = Math.Max(centreX - spread, 0); x <= Math.Min(centreX + spread, _columns - 1); x++)
                    {
                        if (_cells[z * _columns + x] is not List<Vector2> cell) continue;
                        foreach (Vector2 spot in cell)
                        {
                            if ((spot - at).LengthSquared < reach * reach) return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}
