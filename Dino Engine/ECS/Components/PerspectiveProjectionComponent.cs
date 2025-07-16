using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct PerspectiveProjectionComponent : IComponent
    {
        public Matrix4 ProjectionMatrix;
        public float FieldOfView;
        public float AspectRatio;
        public float Near;
        public float Far;

        public PerspectiveProjectionComponent(float fieldOfView, Vector2i resolution, float near, float far) {
            this.FieldOfView = fieldOfView;
            CalcAspect(resolution);
            this.Near = near;
            this.Far = far;
            RebuildMatrix();
        }
        public void CalcAspect(Vector2i resolution)
        {
            AspectRatio = ((float)resolution.X)/((float)resolution.Y);
        }
        public void RebuildMatrix()
        {
            float y_scale = (float)(1f / Math.Tan(FieldOfView / 2f));
            float x_scale = y_scale / AspectRatio;
            float frustum_length = Far - Near;

            ProjectionMatrix = Matrix4.Identity;
            ProjectionMatrix.M11 = x_scale;
            ProjectionMatrix.M22 = y_scale;
            ProjectionMatrix.M33 = -((Far + Near) / frustum_length);
            ProjectionMatrix.M34 = -1f;
            ProjectionMatrix.M43 = -(2 * Near * Far / frustum_length);
            ProjectionMatrix.M44 = 0f;

            //ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, Near, Far);
        }
    }
}
