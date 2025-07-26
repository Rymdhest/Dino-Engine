using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct SpotlightShadowComponent : IComponent, ICleanupComponent
    {
        public Shadow shadow;
        public SpotlightShadowComponent(Vector2i resolution, float angleRadians)
        {
            var projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(angleRadians, 1f, 0.15f, 10f);
            shadow = new Shadow(resolution, projectionMatrix, 0, 0.0f);
        }

        public void Cleanup()
        {
            shadow.shadowFrameBuffer.cleanUp();
        }
    }
}
