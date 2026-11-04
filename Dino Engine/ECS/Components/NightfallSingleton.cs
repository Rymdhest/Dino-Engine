using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// How far day has given way to night, wherever the game wants it to. It is applied as light is read
    /// (<see cref="Systems.SkySYstem"/>) and never written to the sun or sky, so whatever else changes those carries on working.
    /// The moon shines from where the sun is, so that shadows hold still as the light goes.
    /// </summary>
    public struct NightfallSingleton : IComponent
    {
        public float Amount;    // 0 day, 1 full night.
        public Vector3 Moon;    // What the sun becomes.
        public Vector3 Sky;     // What the sky becomes.
        public float FogGlow;   // How much more light fog gives back by night. With so little to scatter it would only darken what's behind it.

        public NightfallSingleton()
        {
            Amount = 0f;
            Moon = new Vector3(0.6f, 0.72f, 1f) * 2.6f;
            Sky = new Vector3(0.13f, 0.18f, 0.34f);
            FogGlow = 2.4f;
        }

        /// <summary>Light on its way from day to night. Evenly through the stops, as the eye sees it: a straight line stays bright till the end.</summary>
        public static Vector3 Blend(Vector3 day, Vector3 night, float amount)
        {
            if (amount <= 0f) return day;
            return new Vector3(Blend(day.X, night.X, amount), Blend(day.Y, night.Y, amount), Blend(day.Z, night.Z, amount));
        }

        private static float Blend(float day, float night, float amount)
        {
            return day <= 0f ? 0f : MathF.Pow(day, 1f - amount) * MathF.Pow(night, amount);
        }
    }
}
