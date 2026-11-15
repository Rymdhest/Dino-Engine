using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct DirectionalCascadingShadowComponent : IComponent, ICleanupComponent
    {
        public Shadow[] cascades;
        public DirectionalCascadingShadowComponent(Vector2i resolution, int numCascades, float size)
        {
            //cascades = new ShadowCascade[numCascades];

            List<Shadow> cascadesTemp= new List<Shadow>();
            for (int i = 0; i < numCascades; i++)
            {
                var projeciton = Matrix4.CreateOrthographic(size, size, -size, size);
                float polygonOffsetTerrain = -size * 0.13f + 60f;
                float polygonOffsetModel = size * 0.001f + 6f;
                cascadesTemp.Add(new Shadow(resolution, projeciton, size, polygonOffsetModel, polygonOffsetTerrain) );
                size /= 6.0f;
            }
            cascadesTemp.Sort((p1, p2) => p1.projectionSize.CompareTo(p2.projectionSize));

            cascades = cascadesTemp.ToArray();
        }

        /// <summary>
        /// A map for each of these widths in metres, finest first: the game's own, and for a view that looks out over a land rather than down on it, one or more wide enough to hold what's
        /// far off (the picture behind the main menu, whose giants are 250 m away). A wider map's offsets are less by as much as its texels are wider, so that every map holds its casters
        /// off by the same distance in metres as the game's does: a slope's depth changes by the same share of a map's range per texel whatever its width, so an offset kept the same
        /// would push a wide map's shadows off by many metres.
        /// </summary>
        public DirectionalCascadingShadowComponent(Vector2i resolution, params float[] sizes)
        {
            float[] ordered = sizes.OrderBy(size => size).ToArray();
            float finest = ordered[0];
            cascades = new Shadow[ordered.Length];
            for (int i = 0; i < ordered.Length; i++)
            {
                float size = ordered[i];
                float narrower = finest / size;
                cascades[i] = new Shadow(resolution, Matrix4.CreateOrthographic(size, size, -size, size), size, (finest * 0.001f + 6f) * narrower, (-finest * 0.13f + 60f) * narrower);
                // What a wide map holds is hundreds of metres off, where a tree's sway is under a texel and a giant's step a few: it's drawn every third frame.
                if (i > 0) cascades[i].interval = WideInterval;
            }
        }

        private const int WideInterval = 3;

        public void Cleanup()
        {
            foreach (var cascade in cascades)
            {
                cascade.shadowFrameBuffer.cleanUp();
            }
        }
    }
}
