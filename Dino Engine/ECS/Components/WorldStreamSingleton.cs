using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// The generated world being played, and which of its objects exist as entities right now. Only what is near the camera
    /// does: <see cref="Systems.TerrainChunkSystem"/> brings sectors of placed objects and the ground cover of terrain chunks
    /// in and out as the camera moves.
    /// </summary>
    public struct WorldStreamSingleton : IComponent
    {
        public WorldData World;
        public WorldAssetLibrary Assets;
        public WorldGroundCover GroundCover;
        public glModel[] ObjectModels;      // By index in World.Objects.AssetIds.
        public IReadOnlyList<AssetLight>[] ObjectLights;    // Likewise.

        /// <summary>The live entities of each sector of placed objects.</summary>
        public Dictionary<Vector2i, List<Entity>> Sectors = new();

        /// <summary>The live ground cover entities of each terrain chunk, keyed like <see cref="TerrainQuadTreeSingleton.Chunks"/>.</summary>
        public Dictionary<Vector2i, List<Entity>> ChunkPlants = new();

        /// <summary>What each asset is, by index like <see cref="ObjectModels"/>, if it's loose clutter that breaks (<see cref="StructureDefinition.Loose"/>), or null.</summary>
        public StructureDefinition?[] ObjectLoose;

        /// <summary>Loose clutter standing as an entity now, by <see cref="Key"/>.</summary>
        public Dictionary<long, LooseObject> Loose = new();

        /// <summary>Loose clutter that's been broken, by <see cref="Key"/>. It stays broken for as long as this world does: walking away and back doesn't mend it.</summary>
        public HashSet<long> Broken = new();

        public WorldStreamSingleton(WorldData world, WorldAssetLibrary assets)
        {
            World = world;
            Assets = assets;
            GroundCover = new WorldGroundCover(world);
            ObjectModels = new glModel[world.Objects.AssetIds.Count];
            ObjectLights = new IReadOnlyList<AssetLight>[ObjectModels.Length];
            ObjectLoose = new StructureDefinition?[ObjectModels.Length];
            for (int i = 0; i < ObjectModels.Length; i++)
            {
                ObjectModels[i] = assets.Get(world.Objects.AssetIds[i]);
                ObjectLights[i] = assets.LightsOf(world.Objects.AssetIds[i]);
                if (StructureCatalog.TryGetPlain(world.Objects.AssetIds[i], out StructureDefinition definition) && definition.Loose) ObjectLoose[i] = definition;
            }
        }

        /// <summary>Which placed object this is, for as long as the world is: the sector it's in and where in that sector's list.</summary>
        public static long Key(Vector2i sector, int index) => ((long)sector.Y << 42) | ((long)sector.X << 21) | (uint)index;
    }

    /// <summary>A piece of loose clutter standing in the world, as <see cref="Systems.BreakageSystem"/> needs it.</summary>
    public readonly record struct LooseObject(Entity Entity, Vector2i Sector, Vector3 Position, Quaternion Rotation, Vector3 Scale, StructureDefinition Definition);
}
