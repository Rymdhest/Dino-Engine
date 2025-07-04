using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct SpotLightComponent : IComponent
    {
        public float softness;
        private float _halfAngleRad;
        private float _cutoffCosine;

        public float CutoffCosine
        {
            get => _cutoffCosine;
        }
        public float HalfAngleRad
        {
            get => _halfAngleRad;
        }

        public SpotLightComponent(float softness, float angleRadians)
        {
            this.softness = softness;

            _halfAngleRad = angleRadians / 2f;
            _cutoffCosine = MathF.Cos(_halfAngleRad);
        }

        public void setAngleRadians(float radians)
        {
            _halfAngleRad = radians / 2f;
            _cutoffCosine = MathF.Cos(_halfAngleRad);
        }
    }
}
