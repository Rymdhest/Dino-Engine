using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.Modelling.Procedural.Architecture.Halls;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Rock;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

namespace Dino_Engine.World
{
    /// <summary>
    /// Turns the asset ids of a generated world into models. Plants, stones and fences are made on worker threads while a world loads (<see cref="Grow"/>) and only reach the GPU here, and a model is
    /// kept for the world after, in case it's wanted again. Every world has plants of its own, so what no world has used for two worlds is let go. Must be used on the render thread,
    /// but for <see cref="Grow"/>.
    /// </summary>
    public sealed class WorldAssetLibrary
    {
        // Off while the camera stays top down: it never sees far enough to need them, and at its height the distances in the catalogs
        // turned plants into imposters just past the hero. A model that isn't given one is always drawn, and casts its shadow, as a mesh.
        private static readonly bool UseImposters = false;

        private readonly ConcurrentDictionary<string, glModel> _models = new();
        private readonly ConcurrentDictionary<string, FloraMesh> _grown = new();
        private readonly ConcurrentDictionary<string, (Mesh Mesh, List<AssetLight> Lights)> _forged = new();
        private readonly Dictionary<string, List<Vector3>> _chimneys = new();
        private readonly Dictionary<string, List<AssetLight>> _lights = new();
        private HashSet<string> _lastWorld = new();

        public int Count => _models.Count;

        /// <summary>Where smoke leaves a building, in model space. Known once its model has been built.</summary>
        public IReadOnlyList<Vector3> ChimneysOf(string id)
        {
            return _chimneys.TryGetValue(id, out List<Vector3>? tops) ? tops : Array.Empty<Vector3>();
        }

        /// <summary>The lights an object carries, in model space. Known once its model has been built.</summary>
        public IReadOnlyList<AssetLight> LightsOf(string id)
        {
            return _lights.TryGetValue(id, out List<AssetLight>? lights) ? lights : Array.Empty<AssetLight>();
        }

        /// <summary>Grows every plant on the list that isn't there yet, on as many threads as there are. Safe from any thread, and it touches no GPU.</summary>
        public void Grow(IEnumerable<string> ids)
        {
            var wanted = new List<(string id, int slowness, Func<FloraMesh> make)>();
            var forging = new List<(string id, PlazaSpec spec)>();
            var raising = new List<(string id, ArenaSpec spec)>();
            foreach (string id in ids)
            {
                if (_models.ContainsKey(id) || _grown.ContainsKey(id) || _forged.ContainsKey(id)) continue;
                // A square's pieces are headless too, so they're built here rather than on the frame that first draws them (SQUARE.md).
                if (PlazaSpec.TryParse(id, out PlazaSpec piece))
                {
                    forging.Add((id, piece));
                    continue;
                }
                // And an arena's (ARENAS.md).
                if (ArenaSpec.TryParse(id, out ArenaSpec arena))
                {
                    raising.Add((id, arena));
                    continue;
                }
                if (FenceSpec.TryParse(id, out FenceSpec fence))
                {
                    wanted.Add((id, 0, () => FenceForge.Make(fence)));
                    continue;
                }
                string name = NatureCatalog.SpeciesOf(id, out ulong seed);
                if (FloraCatalog.TryGet(name, out PlantSpecies species)) wanted.Add((id, 1, () => FloraForge.Grow(species, seed)));
                else if (NatureCatalog.TryGetRock(name, out RockKind kind, out RockForm form)) wanted.Add((id, 2, () => RockForge.Forge(form, kind, seed)));
            }

            // One at a time, and stones first, as a stone can take a hundred times as long as a fern. Handed out in fixed runs, as a list otherwise is, a world's stones,
            // which come one after another, all fell to one thread, and everything waited seconds on it with the other cores idle.
            var queue = Partitioner.Create(wanted.OrderByDescending(item => item.slowness), EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(queue, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, item =>
            {
                _grown[item.id] = item.make();
            });
            Parallel.ForEach(Partitioner.Create(forging, EnumerablePartitionerOptions.NoBuffering), new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, item =>
            {
                var lights = new List<AssetLight>();
                Mesh mesh = PlazaForge.Make(item.spec, lights);
                _forged[item.id] = (mesh, lights);
            });
            Parallel.ForEach(Partitioner.Create(raising, EnumerablePartitionerOptions.NoBuffering), new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, item =>
            {
                var lights = new List<AssetLight>();
                Mesh mesh = ArenaForge.Make(item.spec, lights);
                _forged[item.id] = (mesh, lights);
            });
        }

        /// <summary>Lets go of what neither this world nor the one before it uses. By the time a world is two worlds old, nothing can still be drawing its plants.</summary>
        public void Retain(IEnumerable<string> ids)
        {
            var thisWorld = new HashSet<string>(ids);
            foreach (string id in _models.Keys)
            {
                // Every arena and keep has pieces of its own, so they go too once no world uses them.
                if (thisWorld.Contains(id) || _lastWorld.Contains(id) || !(id.Contains('#') || FenceSpec.IsFence(id) || ArenaSpec.IsArena(id))) continue;
                if (_models.TryRemove(id, out glModel? model)) model.cleanUp();
                _lights.Remove(id);
                _chimneys.Remove(id);
            }
            _lastWorld = thisWorld;
        }

        public glModel Get(string id)
        {
            if (_models.TryGetValue(id, out glModel? model)) return model;

            float imposterDistance = 0f;
            string name = NatureCatalog.SpeciesOf(id, out ulong seed);
            if (FenceSpec.TryParse(id, out FenceSpec fence))
            {
                if (!_grown.TryRemove(id, out FloraMesh? built)) built = FenceForge.Make(fence);
                model = glLoader.loadToVAO(built);
            }
            else if (FloraCatalog.TryGet(name, out PlantSpecies species))
            {
                if (!_grown.TryRemove(id, out FloraMesh? plant)) plant = FloraForge.Grow(species, seed);
                model = glLoader.loadToVAO(plant);
                imposterDistance = species.Asset.ImposterDistance;
                // Trees, and nothing else that isn't asked for, thin away for the player behind them (glModel.OpensForFocus).
                model.OpensForFocus = species.Asset.Kind == WorldAssetKind.Tree;
            }
            else if (NatureCatalog.TryGetRock(name, out RockKind kind, out RockForm form))
            {
                if (!_grown.TryRemove(id, out FloraMesh? stone)) stone = RockForge.Forge(form, kind, seed);
                model = glLoader.loadToVAO(stone);
                imposterDistance = NatureCatalog.RockOf(kind, form).ImposterDistance;
            }
            else
            {
                var chimneys = new List<Vector3>();
                var lights = new List<AssetLight>();
                Mesh mesh = BuildStructure(id, chimneys, lights, out imposterDistance);
                if (chimneys.Count > 0) _chimneys[id] = chimneys;
                if (lights.Count > 0) _lights[id] = lights;
                model = glLoader.loadToVAO(mesh);
            }

            if (UseImposters && imposterDistance > 0f) Engine.RenderEngine.textureGenerator.AddImposterToModel(model, imposterDistance);
            _models[id] = model;
            return model;
        }

        /// <summary>Every model a world can use: its placed objects, and whatever ground cover its biomes can grow.</summary>
        public static List<string> AssetsOf(WorldData world)
        {
            var ids = new List<string>(world.Objects.AssetIds);
            var seen = new HashSet<string>(ids);
            RockKind rock = world.Geology.Kind;
            foreach (BiomeDefinition biome in world.Plan.Biomes)
            {
                AddSpecies(ids, seen, biome.ForestFloor, world.Plan.Seed, rock);
                AddSpecies(ids, seen, biome.OpenGround, world.Plan.Seed, rock);
                AddSpecies(ids, seen, biome.Waterside, world.Plan.Seed, rock);
            }
            AddAsset(ids, seen, NatureCatalog.RockOf(rock, RockForm.Chip), world.Plan.Seed);
            return ids;
        }

        private static void AddSpecies(List<string> ids, HashSet<string> seen, Species[] list, int worldSeed, RockKind rock)
        {
            foreach (Species species in list) AddAsset(ids, seen, NatureCatalog.Resolve(species.Asset, rock), worldSeed);
        }

        private static void AddAsset(List<string> ids, HashSet<string> seen, WorldAsset asset, int worldSeed)
        {
            foreach (string model in NatureCatalog.ModelsOf(asset, worldSeed))
            {
                if (seen.Add(model)) ids.Add(model);
            }
        }

        private Mesh BuildStructure(string id, List<Vector3> chimneys, List<AssetLight> lights, out float imposterDistance)
        {
            if (HallDesigns.TryParseAsset(id, out HallDesign hall, out bool inside))
            {
                imposterDistance = 0f;
                return HallForge.Build(hall, inside, chimneys, lights);
            }
            if (PlazaSpec.TryParse(id, out PlazaSpec piece))
            {
                imposterDistance = 0f;
                if (_forged.TryRemove(id, out var forged))
                {
                    lights.AddRange(forged.Lights);
                    return forged.Mesh;
                }
                return PlazaForge.Make(piece, lights);
            }
            if (ArenaSpec.TryParse(id, out ArenaSpec arena))
            {
                imposterDistance = 0f;
                if (_forged.TryRemove(id, out var raised))
                {
                    lights.AddRange(raised.Lights);
                    return raised.Mesh;
                }
                return ArenaForge.Make(arena, lights);
            }
            StructureDefinition definition = StructureCatalog.Parse(id, out BuildingStyle style, out int variant);
            imposterDistance = definition.ImposterDistance;

            if (definition == StructureCatalog.GrainCrop) return FloraForge.Grow(Undergrowth.Grain, (ulong)variant).ToMesh();
            if (definition == StructureCatalog.VegetableCrop) return FloraForge.Grow(Undergrowth.Cabbage, (ulong)variant).ToMesh();
            if (definition.Styled && definition != StructureCatalog.WatchTower) return BuildingGenerator.Generate(definition, style, variant, chimneys, lights);
            return PropGenerator.Generate(definition, style, lights);
        }
    }
}
