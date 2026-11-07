using OpenTK.Mathematics;

namespace Dino_Engine.Physics
{
    /// <summary>
    /// What a body does to one patch of plants, on the CPU: a copy of the touch half of Shaders/Wind_Vegetation.frag, so that it can be checked with no GPU.
    /// Change them together. Nothing in the game reads this.
    /// </summary>
    public struct PlantTouch
    {
        public const float SoftMiddle = 0.3f;       // SOFT_MIDDLE
        public const float SettledShare = 0.8f;     // SETTLED_SHARE
        public const float ContactRate = 30f;       // CONTACT_RATE

        public Vector2 Lean;
        public float Press;
        public Vector2 Velocity;
        public Vector2 Settled;

        public readonly float Stir => Velocity.Length;

        /// <summary>A body going by, between where it was a step ago and where it is.</summary>
        public struct Body
        {
            public Vector2 Position;
            public Vector2 Previous;
            public float Radius;
            public float Lean;
            public float Press;
            public float Drag;
        }

        /// <summary>One step of the patch at <paramref name="at"/>. Any number of bodies can be going through it, or none.</summary>
        public void Step(Vector2 at, ReadOnlySpan<Body> bodies, float frequency, float damping, float trailTime, float pressTime, float dt)
        {
            Settled *= MathF.Exp(-dt / MathF.Max(trailTime, 1e-3f));
            Press *= MathF.Exp(-dt / MathF.Max(pressTime, 1e-3f));

            float pressed = 1f - MathF.Exp(-dt * ContactRate);
            Vector2 held = Vector2.Zero;
            foreach (Body body in bodies)
            {
                Vector2 travel = body.Position - body.Previous;
                float travelled = travel.LengthSquared;
                bool moved = travelled > 1e-8f;
                float t = moved ? Math.Clamp(Vector2.Dot(at - body.Previous, travel) / travelled, 0f, 1f) : 1f;
                Vector2 offset = at - (body.Previous + travel * t);
                float distance = offset.Length;
                if (body.Radius <= 0f || distance >= body.Radius) continue;

                float near = 1f - distance / body.Radius;
                float weight = Smooth(0f, 0.55f, near);
                float soft = SoftMiddle * body.Radius;
                Vector2 aside = offset / MathF.Sqrt(distance * distance + soft * soft);
                Vector2 along = moved ? travel / MathF.Sqrt(travelled) : Vector2.Zero;

                Vector2 wanted = Limit((aside * (1f - 0.45f * body.Drag) + along * (0.85f * body.Drag)) * (body.Lean * weight), WindField.MaxVegetationBend);
                if (wanted.LengthSquared > held.LengthSquared) held = wanted;

                Vector2 left = (aside * (1f - 0.75f * body.Drag) + along * body.Drag) * (body.Lean * weight * SettledShare);
                if (left.LengthSquared > Settled.LengthSquared) Settled = left;

                float flat = body.Press * Smooth(0.3f, 0.9f, near);
                Press = MathF.Max(Press, Press + (flat - Press) * pressed);
            }

            Vector2 centre = held.LengthSquared > Settled.LengthSquared ? held : Settled;
            Vector2 swing = Lean - centre;
            Ring(ref swing, ref Velocity, MathF.Tau * frequency, damping, dt);
            Lean = centre + swing;
            if (held.LengthSquared > Lean.LengthSquared)
            {
                Lean += (held - Lean) * pressed;
                Velocity *= 1f - pressed;
            }
        }

        private static void Ring(ref Vector2 x, ref Vector2 v, float omega, float zeta, float dt)
        {
            float decay = zeta * omega;
            float rate = omega * MathF.Sqrt(1f - zeta * zeta);
            float fade = MathF.Exp(-decay * dt);
            float c = MathF.Cos(rate * dt);
            float s = MathF.Sin(rate * dt);
            Vector2 nextX = fade * (x * c + (v + decay * x) * (s / rate));
            Vector2 nextV = fade * (v * c - (decay * v + omega * omega * x) * (s / rate));
            x = nextX;
            v = nextV;
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = Math.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static Vector2 Limit(Vector2 v, float most)
        {
            float length = v.Length;
            return length > most ? v * (most / length) : v;
        }
    }
}
