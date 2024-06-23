

using Dino_Engine.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Dino_Engine.ECS
{
    internal class ProjectionComponent : Component
    {
        private float _fieldOfView;
        private Matrix4 _projectionMatrix = Matrix4.Identity;
        private float _near = 0.1f;
        private float _far = 1000f;

        public Matrix4 ProjectionMatrix { get => _projectionMatrix; }

        public ProjectionComponent(float fieldOfView) {
            _fieldOfView = fieldOfView;
            updateProjectionMatrix(Engine.Resolution);
        }
        private void updateProjectionMatrix(Vector2i resolution)
        {

            float aspect = (float)resolution.X / (float)resolution.Y;
            float y_scale = (float)((1f / Math.Tan((_fieldOfView / 2f))));
            float x_scale = y_scale / aspect;
            float frustum_length = _far - _near;

            _projectionMatrix = Matrix4.Identity;
            _projectionMatrix.M11 = x_scale;
            _projectionMatrix.M22 = y_scale;
            _projectionMatrix.M33 = -((_far + _near) / frustum_length);
            _projectionMatrix.M34 = -1f;
            _projectionMatrix.M43 = -((2 * _near * _far) / frustum_length);

            //_projectionMatrix = Matrix4.CreatePerspectiveFieldOfView(_fieldOfView, aspect, _near, _far);
        }

        public override void OnResize(ResizeEventArgs eventArgs)
        {
            updateProjectionMatrix(eventArgs.Size);
        }
    }
}
