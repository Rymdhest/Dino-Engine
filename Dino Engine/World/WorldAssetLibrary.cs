using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Nature;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// Turns the asset ids of a generated world into models. A model is only built the first time a world asks for it and is
    /// kept for the worlds after, so the first worlds pay for what later ones reuse. Must be used on the render thread.
    /// </summary>
    public sealed class WorldAssetLibrary
    {
        // Off while the camera stays top down: it never sees far enough to need them, and at its height the distances in the catalogs
        // turned plants into imposters just past the hero. A model that isn't given one is always drawn, and casts its shadow, as a mesh.
        private static readonly bool UseImposters = false;

        private readonly Dictionary<string, glModel> _models = new();
        private readonly Dictionary<string, List<Vector3>> _chimneys = new();

        public int Count => _models.Count;

        /// <summary>Where smoke leaves a building, in model space. Known once its model has been built.</summary>
        public IReadOnlyList<Vector3> ChimneysOf(string id)
        {
            return _chimneys.TryGetValue(id, out List<Vector3>? tops) ? tops : Array.Empty<Vector3>();
        }

        public glModel Get(string id)
        {
            if (_models.TryGetValue(id, out glModel? model)) return model;

            float imposterDistance;
            float sway = 0f;
            Mesh mesh;
            if (NatureCatalog.TryGet(id, out WorldAsset asset))
            {
                mesh = BuildNature(id);
                imposterDistance = asset.ImposterDistance;
                sway = asset.Sway;
            }
            else
            {
                var chimneys = new List<Vector3>();
                mesh = BuildStructure(id, chimneys, out imposterDistance);
                if (chimneys.Count > 0) _chimneys.Add(id, chimneys);
            }

            model = glLoader.loadToVAO(mesh);
            model.swayAmount = sway;
            if (UseImposters && imposterDistance > 0f) Engine.RenderEngine.textureGenerator.AddImposterToModel(model, imposterDistance);
            _models.Add(id, model);
            return model;
        }

        /// <summary>Every asset a world can use: its placed objects, and whatever ground cover its biomes can grow.</summary>
        public static List<string> AssetsOf(WorldData world)
        {
            var ids = new List<string>(world.Objects.AssetIds);
            foreach (BiomeDefinition biome in world.Plan.Biomes)
            {
                AddSpecies(ids, biome.ForestFloor);
                AddSpecies(ids, biome.OpenGround);
                AddSpecies(ids, biome.Waterside);
            }
            return ids;
        }

        private static void AddSpecies(List<string> ids, Species[] list)
        {
            foreach (Species species in list)
            {
                if (!ids.Contains(species.Asset.Id)) ids.Add(species.Asset.Id);
            }
        }

        private static Mesh BuildNature(string id)
        {
            Colour crimson = new Colour(196, 44, 30);
            Colour gold = new Colour(222, 164, 54);
            Colour blossom = new Colour(255, 168, 172);
            Colour frost = new Colour(176, 200, 196);

            switch (id)
            {
                case "pine.tall": return TreeGenerator.generatePineTree(45, 0.35f);
                case "pine.mid": return TreeGenerator.generatePineTree(25, 0.5f);
                case "pine.sparse": return TreeGenerator.generatePineTree(10, 0.7f);
                case "pine.frosted": return TreeGenerator.generatePineTree(30, 0.42f, branchTexture: TextureGenerator.palePineBranch, branchTint: frost);
                case "pine.dead": return TreeGenerator.generatePineTree(15, 0.35f, alive: false);
                case "pine.charred":
                    Mesh charred = TreeGenerator.generatePineTree(15, 0.35f, alive: false);
                    charred.setColour(new Colour(40, 36, 34));
                    return charred;
                case "birch": return TreeGenerator.GenerateBirchTree();
                case "birch.gold": return TreeGenerator.GenerateBirchTree(TextureGenerator.paleOakBranch, gold);
                case "oak": return TreeGenerator.GenerateOakTree();
                case "maple.crimson": return TreeGenerator.GenerateOakTree(TextureGenerator.paleOakBranch, crimson);
                case "blossom": return TreeGenerator.GenerateOakTree(TextureGenerator.paleOakBranch, blossom);
                case "bamboo": return PlantGenerator.GenerateBamboo();
                case "bush.bigleaf": return TreeGenerator.GenerateBigLeafBush();
                case "fern": return TreeGenerator.GenerateFern();
                case "flower.yellow": return TreeGenerator.GenerateFlowerBush(new OpenTK.Mathematics.Vector3(0.8f, 0.8f, 0.01f));
                case "flower.red": return TreeGenerator.GenerateFlowerBush(new OpenTK.Mathematics.Vector3(0.8f, 0.2f, 0.1f));
                case "flower.purple": return TreeGenerator.GenerateFlowerBush(new OpenTK.Mathematics.Vector3(0.55f, 0.3f, 0.7f));
                case "flower.white": return TreeGenerator.GenerateFlowerBush(new OpenTK.Mathematics.Vector3(0.8f, 0.8f, 0.8f));
                case "reeds": return PlantGenerator.GenerateReeds();
                case "pampas": return PlantGenerator.GeneratePampas();
                case "heather": return PlantGenerator.GenerateHeather();
                case "scrub": return PlantGenerator.GenerateScrub();
                case "mushrooms": return PlantGenerator.GenerateMushrooms();
                case "rock.boulder": return RockGenerator.GenerateRock(0.75f);
                case "rock.brown": return RockGenerator.GenerateRockBrown();
                case "rock.small": return RockGenerator.GenerateRock(0f).scaled(new OpenTK.Mathematics.Vector3(0.55f));
                case "log.pine": return TreeGenerator.GenerateFallenPineTree();
                case "stump.pine": return TreeGenerator.GeneratePineTrunk();
                default: throw new ArgumentException($"No model for nature asset '{id}'.");
            }
        }

        private static Mesh BuildStructure(string id, List<Vector3> chimneys, out float imposterDistance)
        {
            StructureDefinition definition = StructureCatalog.Parse(id, out BuildingStyle style, out int variant);
            imposterDistance = definition.ImposterDistance;

            if (definition == StructureCatalog.Fence) return PropGenerator.GenerateFence();
            if (definition == StructureCatalog.GrainCrop) return PlantGenerator.GenerateGrainCrop();
            if (definition == StructureCatalog.VegetableCrop) return PlantGenerator.GenerateVegetableCrop();
            if (definition.Styled && definition != StructureCatalog.WatchTower) return BuildingGenerator.Generate(definition, style, variant, chimneys);
            return PropGenerator.Generate(definition, style);
        }
    }
}
