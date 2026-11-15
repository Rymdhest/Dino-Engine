using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World
{
    /// <summary>
    /// What grows under the water and on it, put down as the rest of the ground cover is (<see cref="WorldGroundCover"/>): on the client, as chunks stream in, one thing to a cell of the
    /// ground cover's grid by one draw, so nothing collides whichever order chunks load in and a cell always grows the same thing when it comes back. Read SEA.md.
    ///
    /// What a cell grows goes by the water over it (how deep, the sea's or a pond's, salt or fresh), the life of the coast (<see cref="SeaLife"/>, by its biome's warmth), and the
    /// bottom: hard where the land's rock runs into the water, where the bed is steep, on the shingle of a windward coast, in patches on the sand, and wherever the hot seas have built
    /// reef; soft in the sand and the mud between. Meadows of seagrass lie on soft bottoms in drifts, kelp and coral on hard ones, wrack where the waves wash, and lilies on the
    /// shallows of still fresh water. Rock stands up off a rocky bottom here and there (<see cref="FormationCell"/>), always under water at the troughs of the waves and only where nobody
    /// can wade, as nothing here is solid to anyone, and nothing else grows where it stands.
    /// </summary>
    public sealed class WorldSeaFloor
    {
        /// <summary>Metres between the places rock might stand up off the bed, each the middle of a square of this size; a terrain chunk (WorldScene.ChunkSize) is one.</summary>
        public const float FormationCell = 8f;

        private const float NearBody = 1.1f;                 // A body's radius: rock standing up off the bed keeps this far from anywhere anyone stands, as nothing of it is solid.
        private const float FormationReach = 9f;             // Metres from a square that a formation of the next one can reach into it from.
        private const float OutOfReach = WorldData.WadingDepth + 0.25f;  // Water deeper than anyone wades, under everything that stands up off the bed.

        private readonly WorldData _world;
        private readonly WorldNoise _noise;
        private readonly uint _seed;
        private readonly bool _none;
        private readonly bool _stillWater;
        private readonly List<(Vector2 at, float radius)> _near = new();
        private readonly Dictionary<(int x, int z, int which), Formation?> _candidates = new();

        private readonly struct Formation
        {
            public readonly WorldAsset Asset;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly float Scale;
            public readonly float Radius;
            public readonly ulong Rank;

            public Formation(WorldAsset asset, Vector3 position, float yaw, float scale, float radius, ulong rank)
            {
                Asset = asset;
                Position = position;
                Yaw = yaw;
                Scale = scale;
                Radius = radius;
                Rank = rank;
            }
        }

        public WorldSeaFloor(WorldData world)
        {
            _world = world;
            _seed = (uint)world.Plan.Seed * 0x9E3779B1u + 0x7F4A7C15u;
            _noise = new WorldNoise(world.Random.Fork("sea floor"));
            // The picture behind the main menu is left as it was composed (MENU.md).
            _none = world.Plan.Archetype.Vista;
            _stillWater = world.Plan.Archetype.StillWater;
        }

        /// <summary>
        /// Puts down the rock formations whose places lie in the square from <paramref name="origin"/> with sides of <paramref name="size"/>, and remembers those near enough to keep
        /// anything else off it. Call it before the square's cells.
        /// </summary>
        public void BeginSquare(Vector2 origin, float size, List<GroundPlant> results)
        {
            _near.Clear();
            _candidates.Clear();
            if (_none) return;
            int firstX = (int)MathF.Floor((origin.X - FormationReach) / FormationCell);
            int lastX = (int)MathF.Floor((origin.X + size + FormationReach) / FormationCell);
            int firstZ = (int)MathF.Floor((origin.Y - FormationReach) / FormationCell);
            int lastZ = (int)MathF.Floor((origin.Y + size + FormationReach) / FormationCell);
            for (int z = firstZ; z <= lastZ; z++)
            {
                for (int x = firstX; x <= lastX; x++)
                {
                    for (int which = 0; which < 2; which++)
                    {
                        if (Placed(x, z, which) is not Formation formation) continue;
                        _near.Add((formation.Position.Xz, formation.Radius));
                        // A formation belongs to the square its place's middle is in, so that each is put down once, whichever squares load.
                        Vector2 middle = (new Vector2(x, z) + new Vector2(0.5f)) * FormationCell;
                        if (middle.X < origin.X || middle.Y < origin.Y || middle.X >= origin.X + size || middle.Y >= origin.Y + size) continue;
                        results.Add(new GroundPlant
                        {
                            Asset = formation.Asset,
                            Model = NatureCatalog.ModelAt(formation.Asset, _world.Plan.Seed, formation.Position.Xz),
                            Position = formation.Position,
                            Yaw = formation.Yaw,
                            Scale = formation.Scale,
                        });
                    }
                }
            }
        }

        /// <summary>Grows whatever a cell under water grows, at its jittered <paramref name="point"/> on ground <paramref name="height"/> high, by the cell's own draw.</summary>
        public void TryGrow(Vector2 point, float height, ref uint state, List<GroundPlant> results) => Grow(point, height, ref state, results, true);

        private void Grow(Vector2 point, float height, ref uint state, List<GroundPlant> results, bool crowding)
        {
            if (_none) return;
            float level = _world.WaterLevelAt(point);
            float depth = level - height;
            if (depth <= 0.03f) return;
            bool pond = level > WorldPlan.SeaLevel + 0.01f;
            BiomeDefinition biome = _world.BiomeAt(point);
            SeaLife life = pond || _stillWater ? SeaLife.FreshWater : SeaLife.Of(biome);
            if (depth > life.Deepest) return;
            foreach ((Vector2 at, float radius) in _near)
            {
                if ((point - at).LengthSquared < radius * radius * 0.7f) return;
            }

            float hard = Hardness(point, biome, life, out float halo);
            float soft = 1f - hard;
            float x = point.X, z = point.Y;

            // What grows on hard bottoms grows in masses, as reef heads, kelp beds and the barrens between them do: crowding and overgrowing each other in each, older and larger
            // towards its heart, with only a few young colonies between them. Strewn evenly, one to a cell, a reef read as polka dots.
            float massAt = Lerp(0.66f, 0.34f, life.ReefCover);
            float mass = _noise.Fbm01(x - 257f, z + 613f, life.ReefPatch, 3);
            float massed = Lerp(life.ReefScatter, 1f, Smoothstep(massAt - 0.025f, massAt + 0.025f, mass));
            float heart = Smoothstep(massAt, massAt + 0.12f, mass);

            // Each band is how likely this cell is to grow from it; one draw falls in at most one.
            float meadowAt = Lerp(0.72f, 0.32f, life.MeadowCover);
            // A meadow is a carpet with an edge: thin at its edges for a stride or two and whole inside, never strewn about.
            float meadow = soft * (1f - halo) * life.MeadowDensity * Smoothstep(meadowAt - 0.03f, meadowAt + 0.03f, _noise.Fbm01(x + 191f, z - 77f, 23f, 3))
                * Smoothstep(0.22f, 0.5f, depth) * (1f - Smoothstep(3.2f, 4.5f, depth)) * Has(life.Meadow);
            float washed = hard * life.WashedDensity * Smoothstep(0.03f, 0.12f, depth) * (1f - Smoothstep(0.7f, 1.1f, depth)) * Has(life.Washed);
            float reef = hard * massed * life.ReefDensity * Smoothstep(0.5f, 1f, depth) * (1f - Smoothstep(life.Deepest - 1.5f, life.Deepest, depth)) * Has(life.Reef);
            float deep = hard * massed * life.DeepDensity * Smoothstep(2.6f, 3.4f, depth) * (1f - Smoothstep(life.Deepest - 0.5f, life.Deepest, depth)) * Has(life.Deep);
            // Urchins and starfish keep to the bare hard ground between the masses and round their edges, which they graze.
            float critters = life.CritterDensity * (1f + 2.5f * hard * (1f - heart)) * Smoothstep(0.15f, 0.4f, depth) * (1f - Smoothstep(4f, 5.5f, depth)) * Has(life.Critters);
            float floatAt = Lerp(0.72f, 0.32f, life.FloatingCover);
            float floating = life.FloatingDensity * Smoothstep(floatAt - 0.05f, floatAt + 0.05f, _noise.Fbm01(x - 613f, z + 389f, 13f, 3))
                * Smoothstep(0.2f, 0.45f, depth) * (1f - Smoothstep(1.8f, 2.6f, depth)) * Has(life.Floating);
            float total = meadow + washed + reef + deep + critters + floating;
            if (total <= 0f) return;
            float pick = WorldGroundCover.Random01(ref state) * MathF.Max(total, 1f);

            Species[] list;
            if ((pick -= floating) < 0f) list = life.Floating;
            else if ((pick -= critters) < 0f) list = life.Critters;
            else if ((pick -= washed) < 0f) list = life.Washed;
            else if ((pick -= deep) < 0f) list = life.Deep;
            else if ((pick -= reef) < 0f) list = life.Reef;
            else if ((pick -= meadow) < 0f) list = life.Meadow;
            else return;

            // A mass is mostly of one kind, as thickets of staghorn, fields of table coral and stands of kelp are, with a few others among it: a random mix of everything read as litter.
            bool massing = list == life.Reef || list == life.Deep;
            WorldAsset chosen = massing && WorldGroundCover.Random01(ref state) < StandShare ? StandOf(list, x, z) : PickSpecies(list, ref state);
            WorldAsset asset = NatureCatalog.Resolve(chosen, _world.Geology.Kind);
            float scale = asset.Scale.Lerp(WorldGroundCover.Random01(ref state));
            if (massing) scale *= Lerp(0.6f, 1.3f, heart);
            float yaw = WorldGroundCover.Random01(ref state) * MathF.Tau;
            float ground = height + TerrainRenderer.SurfaceOffset;
            float y = ground - 0.03f;

            if (list == life.Floating)
            {
                // A lily floats, on the water and not the bed.
                y = level;
            }
            else if (asset.Id == Seas.GiantKelpPlant.Id)
            {
                // A giant kelp reaches the surface, however deep it stands: it's made for one depth and scaled to the rest.
                scale = (level - ground + 0.05f) / Seas.GiantKelpReach;
                if (scale < asset.Scale.Min || scale > asset.Scale.Max) return;
            }
            else if (FloraCatalog.TryGet(asset.Id, out PlantSpecies species) && species.Form is IUnderWater under)
            {
                // Nothing stands out of the water. What's rigid, a coral or a sponge, keeps under the troughs of the waves, as what's out of the water at low tide doesn't live; what
                // sways keeps under the still water, as a meadow's blades and a kelp's straps lie out along the surface rather than stand up out of it, and a trough may pass over
                // them. Grown smaller to fit, as in the shallows they are, or not at all.
                if (under.Rigid) y = ground - 0.04f;
                float keep = under.Rigid ? Trough(depth) + 0.04f : 0.05f;
                float most = (level - keep - y) / under.Tallest;
                if (most < asset.Scale.Min * (under.Rigid ? 0.6f : 0.4f)) return;
                scale = MathF.Min(scale, most);
            }
            if (asset.Id == Seas.SeaFanPlant.Id)
            {
                // A fan grows broadside to the waves, which come in up the slope of the bed.
                Vector2 seaward = Downhill(point);
                if (seaward != Vector2.Zero) yaw = MathF.Atan2(seaward.X, seaward.Y) + (WorldGroundCover.Random01(ref state) - 0.5f) * 0.5f;
            }

            results.Add(new GroundPlant
            {
                Asset = asset,
                Model = NatureCatalog.ModelAt(asset, _world.Plan.Seed, point),
                Position = new Vector3(point.X, y, point.Y),
                Yaw = yaw,
                Scale = scale,
            });

            // In the heart of a mass the colonies crowd two to a cell and overgrow each other, as a reef's and a kelp bed's do: one to a cell, even a heart showed sand between every
            // one. The second is a draw of its own, half a cell off, held to everything the first was where it stands.
            if (crowding && massing && WorldGroundCover.Random01(ref state) < heart * 0.85f)
            {
                float turn = WorldGroundCover.Random01(ref state) * MathF.Tau;
                Vector2 beside = point + new Vector2(MathF.Cos(turn), MathF.Sin(turn)) * (WorldGroundCover.Cell * 0.5f);
                if (_world.InBounds(beside)) Grow(beside, _world.Height.Sample(beside), ref state, results, false);
            }
        }

        /// <summary>Every model what lives in a world's water can ask for, for <see cref="WorldAssetLibrary.AssetsOf"/>: the life of each of its coasts, fresh water's, and its formations.</summary>
        public static IEnumerable<WorldAsset> AssetsOf(WorldData world)
        {
            if (world.Plan.Archetype.Vista) yield break;
            var lives = new HashSet<SeaLife>();
            if (world.Plan.Archetype.StillWater || world.Ponds.Count > 0) lives.Add(SeaLife.FreshWater);
            if (!world.Plan.Archetype.StillWater)
            {
                foreach (BiomeDefinition biome in world.Plan.Biomes) lives.Add(SeaLife.Of(biome));
            }
            foreach (SeaLife life in lives)
            {
                foreach (Species species in life.Everything) yield return NatureCatalog.Resolve(species.Asset, world.Geology.Kind);
            }
            foreach (RockForm form in FormsOf(world.Geology.Kind)) yield return NatureCatalog.RockOf(world.Geology.Kind, form);
        }

        // ---------------------------------------------------------------- the bottom

        // How hard the bottom is here, 0 sand or mud to 1 rock: where the land's rock runs into the water, where the bed is steep, on a stony shore in patches, and where a hot sea has
        // built reef on the sand.
        private float Hardness(Vector2 point, BiomeDefinition biome, SeaLife life, out float halo)
        {
            float rock = _world.RockNear != null ? 1f - Smoothstep(2f, 14f, _world.RockNear.Sample(point)) : 0f;
            float steep = Smoothstep(0.35f, 0.9f, _world.Height.SlopeAt(point));
            float stony = biome.Shore switch
            {
                SurfaceMaterial.Gravel or SurfaceMaterial.Shingle or SurfaceMaterial.Ice or SurfaceMaterial.Rock => 0.85f,
                SurfaceMaterial.Mud => 0f,
                _ => 0.22f,
            };
            float patches = Smoothstep(0.5f, 0.62f, _noise.Fbm01(point.X - 433f, point.Y + 259f, 27f, 3));
            float hard = MathF.Max(MathF.Max(rock, steep), stony * patches);
            halo = 0f;
            if (life.Patches > 0f && biome.Shore != SurfaceMaterial.Mud)
            {
                float reefAt = Lerp(0.74f, 0.4f, life.Patches);
                float patch = _noise.Fbm01(point.X + 877f, point.Y + 41f, 31f, 3);
                hard = MathF.Max(hard, Smoothstep(reefAt - 0.04f, reefAt + 0.04f, patch));
                // A patch reef in a meadow is ringed with bare sand, grazed by what shelters on the reef and goes out from it by night: the halos reefs show from the air.
                halo = Smoothstep(reefAt - 0.16f, reefAt - 0.08f, patch);
            }
            return Saturate(hard);
        }

        // Which way the bed falls away here, towards the open water, or nothing where it's flat.
        private Vector2 Downhill(Vector2 point)
        {
            const float Step = 1.5f;
            float dx = _world.Height.Sample(point + new Vector2(Step, 0f)) - _world.Height.Sample(point - new Vector2(Step, 0f));
            float dz = _world.Height.Sample(point + new Vector2(0f, Step)) - _world.Height.Sample(point - new Vector2(0f, Step));
            Vector2 down = new Vector2(-dx, -dz);
            return down.LengthSquared > 1e-6f ? down.Normalized() : Vector2.Zero;
        }

        private static float Has(Species[] list) => list.Length > 0 ? 1f : 0f;

        private const float StandSize = 7f;     // Metres across a stand of one kind.
        private const float StandShare = 0.7f;  // Of what grows in a stand, how much is its own kind.

        // The kind a stand is of: a cell of a grid warped so its edges wander, and the same kind drawn in all of it, by weight.
        private WorldAsset StandOf(Species[] list, float x, float z)
        {
            Vector2 warp = _noise.Warp(x, z, 12f, 3.5f, 2);
            uint state = Hash((int)MathF.Floor((x + warp.X) / StandSize) + 40503, (int)MathF.Floor((z + warp.Y) / StandSize) - 19937);
            return PickSpecies(list, ref state);
        }

        private static WorldAsset PickSpecies(Species[] list, ref uint state)
        {
            float total = 0f;
            foreach (Species species in list) total += species.Weight;
            float pick = WorldGroundCover.Random01(ref state) * total;
            foreach (Species species in list)
            {
                pick -= species.Weight;
                if (pick <= 0f) return species.Asset;
            }
            return list[^1].Asset;
        }

        // How far a solid thing stands up off the bed at a scale of one, from the ranges it's designed in, or null for what bends out of the way.
        // What a formation really stands and reaches at a scale of one. RockForge.TopOf and a rock's footprint are what its forms are designed to, and their meshes go past both: a
        // schist slab stands 2.55 times its TopOf and an outcrop reaches 1.85 times its footprint. These are the most of 200 of each (forty seeds of each kind of rock) with a
        // tenth more, and Dino WorldLab sea holds every one placed to them on its real mesh.
        private static float TallestOf(RockForm form) => RockForge.TopOf(form) * form switch
        {
            RockForm.Boulder or RockForm.Block => 1.55f,
            RockForm.Slab => 2.8f,
            RockForm.Outcrop => 1.27f,
            _ => 1f,
        };

        private static float ReachOf(RockForm form, WorldAsset asset) => asset.Footprint * form switch
        {
            RockForm.Outcrop => 2.05f,
            RockForm.Tor => 1.45f,
            RockForm.Columns => 1.2f,
            _ => 1.45f,
        };

        /// <summary>
        /// The deepest a trough of an ordinary day's waves goes under the still water where it's <paramref name="depth"/> metres deep: a wave breaks at about 0.78 of the depth
        /// (WaterWaves.BreakingRatio), its trough is at most 0.41 of it under the mean of its profile (Water/ShoreData.glsl's shoreWaveProfile), and no ordinary breaker on these
        /// coasts is much over a metre.
        /// </summary>
        public static float Trough(float depth) => 0.41f * MathF.Min(WaterWaves.BreakingRatio * depth, 1f);

        // ---------------------------------------------------------------- formations

        // The forms rock stands up off the bed in: what it breaks into anywhere, and what this rock breaks into best, in deeper water.
        private static IEnumerable<RockForm> FormsOf(RockKind kind)
        {
            yield return RockForm.Boulder;
            yield return RockForm.Block;
            yield return RockForm.Slab;
            yield return RockForm.Outcrop;
            if (kind == RockKind.Granite) yield return RockForm.Tor;
            if (kind == RockKind.Basalt) yield return RockForm.Columns;
        }

        // A formation at its place, unless one nearby with a higher rank stands where it would: each place decides from its neighbours' own draws alone, so the answer is the same
        // whichever squares are asked first.
        private Formation? Placed(int x, int z, int which)
        {
            if (Candidate(x, z, which) is not Formation formation) return null;
            for (int nz = z - 1; nz <= z + 1; nz++)
            {
                for (int nx = x - 1; nx <= x + 1; nx++)
                {
                    for (int other = 0; other < 2; other++)
                    {
                        if (nx == x && nz == z && other == which) continue;
                        if (Candidate(nx, nz, other) is not Formation rival || rival.Rank <= formation.Rank) continue;
                        float apart = (rival.Position.Xz - formation.Position.Xz).Length;
                        if (apart < (rival.Radius + formation.Radius) * 0.8f) return null;
                    }
                }
            }
            return formation;
        }

        private Formation? Candidate(int x, int z, int which)
        {
            if (_candidates.TryGetValue((x, z, which), out Formation? known)) return known;
            Formation? made = Make(x, z, which);
            _candidates[(x, z, which)] = made;
            return made;
        }

        private Formation? Make(int x, int z, int which)
        {
            uint state = Hash(x * 2 + which, z);
            Vector2 jitter = new Vector2(WorldGroundCover.Random01(ref state), WorldGroundCover.Random01(ref state)) - new Vector2(0.5f);
            Vector2 point = (new Vector2(x, z) + new Vector2(0.5f) + jitter * (which == 0 ? 0.55f : 0.85f)) * FormationCell;
            if (!_world.InBounds(point)) return null;

            float height = _world.Height.Sample(point);
            float level = _world.WaterLevelAt(point);
            float depth = level - height;
            // Not in a pond: rock in one would be the pond's floor.
            if (depth < OutOfReach + 0.3f || level > WorldPlan.SeaLevel + 0.01f) return null;
            BiomeDefinition biome = _world.BiomeAt(point);
            SeaLife life = _stillWater ? SeaLife.FreshWater : SeaLife.Of(biome);
            if (depth > life.Deepest + 1f) return null;
            float rocky = Hardness(point, biome, life, out _);
            float chance = life.Formations * (which == 0 ? 1f : 0.55f) * Smoothstep(0.25f, 0.6f, rocky);
            if (WorldGroundCover.Random01(ref state) >= chance) return null;

            // The forms that fit under this much water, as many times as they're likely: what's big only where it's deep. Its top keeps under the deepest trough, over the drawn ground.
            float room = depth - TerrainRenderer.SurfaceOffset - Trough(depth) - 0.05f;
            RockKind kind = _world.Geology.Kind;
            Span<(RockForm form, float weight)> forms = stackalloc (RockForm, float)[6];
            int count = 0;
            if (which == 0)
            {
                bool tabular = kind is RockKind.Limestone or RockKind.Sandstone;
                forms[count++] = (RockForm.Boulder, 3f);
                forms[count++] = (RockForm.Block, 2f);
                forms[count++] = (RockForm.Slab, tabular ? 3f : 1.5f);
                forms[count++] = (RockForm.Outcrop, 1.5f);
                if (kind == RockKind.Granite) forms[count++] = (RockForm.Tor, 1f);
                if (kind == RockKind.Basalt) forms[count++] = (RockForm.Columns, 1.5f);
            }
            else
            {
                forms[count++] = (RockForm.Boulder, 2f);
                forms[count++] = (RockForm.Block, 1f);
            }
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                WorldAsset candidate = NatureCatalog.RockOf(kind, forms[i].form);
                if (TallestOf(forms[i].form) * candidate.Scale.Min > room) forms[i].weight = 0f;
                total += forms[i].weight;
            }
            if (total <= 0f) return null;
            float pick = WorldGroundCover.Random01(ref state) * total;
            RockForm chosen = forms[0].form;
            for (int i = 0; i < count; i++)
            {
                if (forms[i].weight <= 0f) continue;
                chosen = forms[i].form;
                if ((pick -= forms[i].weight) <= 0f) break;
            }

            WorldAsset asset = NatureCatalog.RockOf(kind, chosen);
            float most = MathF.Min(asset.Scale.Max * (which == 0 ? 1f : 0.7f), room / TallestOf(chosen));
            float scale = Lerp(asset.Scale.Min, MathF.Max(most, asset.Scale.Min), WorldGroundCover.Random01(ref state));
            float radius = asset.Footprint * scale;
            float reach = ReachOf(chosen, asset) * scale;

            // All of it out of anyone's reach: deeper than a body can wade under all of it, which reaches further than its footprint, and nobody standing within a body of it.
            for (int i = 0; i < 16; i++)
            {
                float angle = i * (MathF.Tau / 16f);
                var round = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector2 inner = point + round * (reach * 0.5f), edge = point + round * reach, near = point + round * (reach + NearBody);
                if (!_world.InBounds(near) || _world.WaterDepthAt(inner) < OutOfReach || _world.WaterDepthAt(edge) < OutOfReach) return null;
                if (_world.Walkable.Sample(inner) > 0.25f || _world.Walkable.Sample(edge) > 0.25f || _world.Walkable.Sample(near) > 0.25f) return null;
            }

            float yaw = WorldGroundCover.Random01(ref state) * MathF.Tau;
            // Sunk a little into the bed, as rock that's lain under the sea a long time is.
            float sunk = 0.12f * RockForge.TopOf(chosen) * scale;
            Vector3 position = new Vector3(point.X, height + TerrainRenderer.SurfaceOffset - sunk, point.Y);
            ulong rank = ((ulong)(which == 0 ? 1u : 0u) << 32) | state;
            return new Formation(asset, position, yaw, scale, radius, rank);
        }

        private uint Hash(int x, int z)
        {
            unchecked
            {
                uint h = _seed ^ (uint)x * 0x85EBCA6Bu ^ (uint)z * 0xC2B2AE35u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
