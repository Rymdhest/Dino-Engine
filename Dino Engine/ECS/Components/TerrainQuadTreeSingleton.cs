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
        /// Metres from the camera to the middle of the square, in the world's frame. Nothing for the game, whose camera looks down on the middle of what it sees; the picture behind the menu
        /// looks out level, and what's behind its camera is never seen, so its square lies ahead of it.
        /// </summary>
        public Vector2 Ahead;

        /// <summary>
        /// The most chunks on each side there can be, as every chunk alive has a layer of the terrain's arrays (<see cref="Rendering.Renderers.Geometry.TerrainRenderer.MaxChunks"/>), and a
        /// square that needs more hands layers out twice: the menu's once did, and the land entered after it was drawn with its chunks' textures crossed, in squares.
        /// </summary>
        public static int MostRadius => ((int)MathF.Floor(MathF.Sqrt(Rendering.Renderers.Geometry.TerrainRenderer.MaxChunks)) - 1) / 2;

        /// <summary>
        /// The live chunks, keyed by their integer grid coordinate. A key of (cx, cz) maps to a
        /// chunk whose bottom-left corner is at (cx * ChunkSize, cz * ChunkSize).
        /// </summary>
        public Dictionary<Vector2i, Entity> Chunks = new();

        public TerrainQuadTreeSingleton(float chunkSize, int radius)
        {
            this.ChunkSize = chunkSize;
            if (radius > MostRadius) Console.WriteLine($"A square of {radius} chunks a side needs more than the terrain's {Rendering.Renderers.Geometry.TerrainRenderer.MaxChunks} layers: drawing {MostRadius}.");
            this.Radius = Math.Min(radius, MostRadius);
            this.Ahead = Vector2.Zero;
        }
    }
}
