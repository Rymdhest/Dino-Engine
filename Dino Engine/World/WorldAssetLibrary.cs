using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

namespace Dino_Engine.World
{
    /// <summary>
    /// Turns the asset ids of a generated world into models. Plants are grown on worker threads while a world loads (<see cref="Grow"/>) and only reach the GPU here, and a model is
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
            var wanted = new List<(PlantSpecies species, string id, ulong seed)>();
            foreach (string id in ids)
            {
                if (_models.ContainsKey(id) || _grown.ContainsKey(id)) continue;
                if (FloraCatalog.TryGet(NatureCatalog.SpeciesOf(id, out ulong seed), out PlantSpecies species)) wanted.Add((species, id, seed));
            }
            Parallel.ForEach(wanted, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, plant =>
            {
                _grown[plant.id] = FloraForge.Grow(plant.species, plant.seed);
            });
        }

        /// <summary>Lets go of what neither this world nor the one before it uses. By the time a world is two worlds old, nothing can still be drawing its plants.</summary>
        public void Retain(IEnumerable<string> ids)
        {
            var thisWorld = new HashSet<string>(ids);
            foreach (string id in _models.Keys)
            {
                if (thisWorld.Contains(id) || _lastWorld.Contains(id) || !id.Contains('#')) continue;
                if (_models.TryRemove(id, out glModel? model)) model.cleanUp();
            }
            _lastWorld = thisWorld;
        }

        public glModel Get(string id)
        {
            if (_models.TryGetValue(id, out glModel? model)) return model;

            float imposterDistance = 0f;
            if (FloraCatalog.TryGet(NatureCatalog.SpeciesOf(id, out ulong seed), out PlantSpecies species))
            {
                if (!_grown.TryRemove(id, out FloraMesh? plant)) plant = FloraForge.Grow(species, seed);
                model = glLoader.loadToVAO(plant);
                imposterDistance = species.Asset.ImposterDistance;
            }
            else
            {
                Mesh mesh;
                if (NatureCatalog.TryGet(id, out WorldAsset asset))
                {
                    mesh = BuildRock(id);
                    imposterDistance = asset.ImposterDistance;
                    mesh.Still();
                }
                else
                {
                    var chimneys = new List<Vector3>();
                    var lights = new List<AssetLight>();
                    mesh = BuildStructure(id, chimneys, lights, out imposterDistance);
                    if (chimneys.Count > 0) _chimneys.Add(id, chimneys);
                    if (lights.Count > 0) _lights.Add(id, lights);
                }
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
            foreach (BiomeDefinition biome in world.Plan.Biomes)
            {
                AddSpecies(ids, seen, biome.ForestFloor, world.Plan.Seed);
                AddSpecies(ids, seen, biome.OpenGround, world.Plan.Seed);
                AddSpecies(ids, seen, biome.Waterside, world.Plan.Seed);
            }
            return ids;
        }

        private static void AddSpecies(List<string> ids, HashSet<string> seen, Species[] list, int worldSeed)
        {
            foreach (Species species in list)
            {
                foreach (string model in NatureCatalog.ModelsOf(species.Asset, worldSeed))
                {
                    if (seen.Add(model)) ids.Add(model);
                }
            }
        }

        private static Mesh BuildRock(string id)
        {
            switch (id)
            {
                case "rock.boulder": return RockGenerator.GenerateRock(0.75f);
                case "rock.brown": return RockGenerator.GenerateRockBrown();
                case "rock.small": return RockGenerator.GenerateRock(0f, detail: 3).scaled(new Vector3(0.55f));
                default: throw new ArgumentException($"No model for nature asset '{id}'.");
            }
        }

        private static Mesh BuildStructure(string id, List<Vector3> chimneys, List<AssetLight> lights, out float imposterDistance)
        {
            StructureDefinition definition = StructureCatalog.Parse(id, out BuildingStyle style, out int variant);
            imposterDistance = definition.ImposterDistance;

            if (definition == StructureCatalog.Fence) return PropGenerator.GenerateFence();
            if (definition == StructureCatalog.GrainCrop) return FloraForge.Grow(Undergrowth.Grain, (ulong)variant).ToMesh();
            if (definition == StructureCatalog.VegetableCrop) return FloraForge.Grow(Undergrowth.Cabbage, (ulong)variant).ToMesh();
            if (definition.Styled && definition != StructureCatalog.WatchTower) return BuildingGenerator.Generate(definition, style, variant, chimneys, lights);
            return PropGenerator.Generate(definition, style, lights);
        }
    }
}
