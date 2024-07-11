

using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.Modelling;
using OpenTK.Mathematics;
using System.Numerics;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace Dino_Engine.ECS
{
    public class DirectionalLightSystem : ComponentSystem
    {
        public DirectionalLightSystem()
        {
            addRequiredComponent<DirectionComponent>();
            addRequiredComponent<ColourComponent>();

            addOptionalComponent<AmbientLightComponent>();
            addOptionalComponent<CascadingShadowComponent>();
        }

        internal override void UpdateEntity(Entity entity)
        {
            if (entity.TryGetComponent<CascadingShadowComponent>(out CascadingShadowComponent shadow))
            {

                Vector3 lightDirection = entity.getComponent<DirectionComponent>().Direction;
                Vector3 cameraPosition = Engine.Instance.ECSEngine.Camera.getComponent<TransformationComponent>().Transformation.position;
                shadow.LightViewMatrix = CreateLightViewMatrix(-lightDirection, cameraPosition);

            }
        }


        private static Matrix4 CreateLightViewMatrix(Vector3 direction, Vector3 center)
        {
            direction.Normalize();
            center *= -1f;
            Matrix4 lightViewMatrix = Matrix4.Identity;

            float rotX = MathF.Acos((direction.Xz).Length);

            //float rotY = MathF.Atan(direction.X / direction.Z);
            //rotY = direction.Z > 0 ? rotY - MathF.PI : rotY;

            float rotY = MathF.Atan2(direction.X, direction.Z) + MathF.PI;

            lightViewMatrix *= Matrix4.CreateTranslation(new Vector3(center.X, center.Y, center.Z));
            lightViewMatrix *= Matrix4.CreateRotationY(-rotY);
            lightViewMatrix *= Matrix4.CreateRotationX(rotX);
            return lightViewMatrix;
        }
    }
}
