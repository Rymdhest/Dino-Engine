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
                cascadesTemp.Add(new Shadow(resolution, projeciton, size, size * 0.001f+6f) );
                size /= 6.0f;
            }
            cascadesTemp.Sort((p1, p2) => p1.projectionSize.CompareTo(p2.projectionSize));

            cascades = cascadesTemp.ToArray();
        }

        public void Cleanup()
        {
            foreach (var cascade in cascades)
            {
                cascade.shadowFrameBuffer.cleanUp();
            }
        }
    }
}
