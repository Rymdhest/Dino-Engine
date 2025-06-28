using OpenTK.Mathematics;

namespace Dino_Engine.ECS.ComponentsOLD
{
    public class AttunuationComponent : ComponentOLD
    {
        public Vector3 Attunuation { get; }
        public float AttunuationRadius { get; set; }
        public AttunuationComponent(float constant, float linear, float quadratic)
        {
            Attunuation = new Vector3(constant, linear, quadratic);
            AttunuationRadius = CalculateAttunRadius(Attunuation);
        }

        public static float CalculateAttunRadius(Vector3 attun)
        {
            float sqrt = MathF.Sqrt(attun.Y * attun.Y - 4f * attun.Z * (attun.X - 1f));
            return (-attun.Y + sqrt) / (2f * attun.Z);
        }
    }
}
