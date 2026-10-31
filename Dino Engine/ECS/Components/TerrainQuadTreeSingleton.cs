using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Holds the fixed-size terrain chunk configuration and the set of chunks that are currently
    /// alive around the camera. Terrain is no longer LOD'd via a quadtree: every chunk is exactly
    /// <see cref="ChunkSize"/> in size, so quality and grass density are uniform everywhere (no
    /// popping). Only a square ring of <see cref="Radius"/> chunks on each side of the camera
    /// exists at any time; everything else is destroyed.
    /// </summary>
    public struct TerrainQuadTreeSingleton : IComponent
    {
        // --- Fixed chunk configuration (set once at world spawn) ---
        public float ChunkSize;   // World size of one square chunk (X == Z). All chunks are identical.
        public int Radius;        // How many chunks to keep alive on each side of the camera.

        /// <summary>
        /// The live chunks, keyed by their integer grid coordinate. A key of (cx, cz) maps to a
        /// chunk whose bottom-left corner is at (cx * ChunkSize, cz * ChunkSize).
        /// </summary>
        public Dictionary<Vector2i, Entity> Chunks = new();

        public TerrainQuadTreeSingleton(float chunkSize, int radius)
        {
            this.ChunkSize = chunkSize;
            this.Radius = radius;
        }
    }
}
