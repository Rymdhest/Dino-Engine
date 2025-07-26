using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct AttunuationComponent : IComponent
    {
        public Vector3 Attunuation;
        public float AttunuationRadius;
        public AttunuationComponent(float constant, float linear, float quadratic)
        {
            Attunuation = new Vector3(constant, linear, quadratic);
            AttunuationRadius = AttunuationComponent.CalculateAttunRadius(Attunuation);
            Console.WriteLine(AttunuationRadius);
        }

        public static float CalculateAttunRadius(Vector3 attun)
        {
            float sqrt = MathF.Sqrt(attun.Y * attun.Y - 4f * attun.Z * (attun.X - 1f));
            return (-attun.Y + sqrt) / (2f * attun.Z);
        }
    }
}
