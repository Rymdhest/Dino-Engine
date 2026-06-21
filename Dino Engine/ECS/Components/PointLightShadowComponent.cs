using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Lighting;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct PointLightShadowComponent : IComponent, ICleanupComponent
    {
        public Shadow shadow;
        // Point lights require a 90-degree FOV for each face of the cube
        public PointLightShadowComponent(int resolution)
        {
            // The projection far plane MUST match the light radius to sync correctly with shader depth reconstruction
            var projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90f), 1f, 0.15f, 30f);
            shadow = new Shadow(resolution, projectionMatrix, 0, 2.0f, 2.0f, true);
        }

        public void Cleanup()
        {
            shadow.shadowFrameBuffer.cleanUp();
        }
    }
}