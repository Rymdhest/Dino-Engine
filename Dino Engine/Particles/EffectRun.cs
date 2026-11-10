using Dino_Engine.Physics;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>
    /// An effect happening somewhere: where it is, how big, and how far through it is. Whoever owns it moves it about; <see cref="Step"/> emits what each layer is due. An effect
    /// that goes on and can't be seen emits nothing, and when it comes back into view it fills in what would be there, so walking up to a chimney doesn't show its smoke starting.
    /// </summary>
    public sealed class EffectRun
    {
        public readonly ParticleEffect Effect;
        public float Age;

        public Vector3 Position;
        public Vector3 Velocity;                // Of whatever it's on.
        public Vector3 Forward = Vector3.UnitZ; // The way an emitter's +z points; +y stays up.
        public EffectShape Shape = EffectShape.Point;
        public float Scale = 1f;                // Of its particles' sizes and offsets.
        public float Strength = 1f;             // Of how much it emits and how bright its light is: what's dying down emits less.
        public Vector3 Tint = Vector3.One;
        public bool Prewarm;                    // Starts as though it had been going for a while.

        private readonly float[] _debt;
        private readonly bool[] _burst;
        private bool _started;
        private float _hiddenFor;
        private Vector3 _previous;      // Where it was a step ago.
        private Vector3 _origin;        // Where the particle being made starts from: along the way it came this step, for what's moving fast.
        private readonly float _lightPhase;

        public EffectRun(ParticleEffect effect)
        {
            Effect = effect;
            _debt = new float[effect.Emitters.Length];
            _burst = new bool[effect.Emitters.Length];
            _lightPhase = Particles.Random.Range(0f, 100f);
        }

        public bool Finished => !Effect.Lasts && Age > Effect.Length;

        /// <summary>The middle of what it covers, and how far its particles get from there.</summary>
        public Vector3 Middle => Shape.Kind switch
        {
            EffectShapeKind.Area => new Vector3(Shape.Area!.Middle.X, Position.Y, Shape.Area.Middle.Y),
            EffectShapeKind.Face => Position + Turn(Shape.Face!.Middle),
            _ => Position + Vector3.UnitY * (Shape.Height * 0.5f),
        };
        public float Reach => Shape.Extent + Effect.Reach * Scale;

        /// <summary>Emits what's due over the next <paramref name="seconds"/>. <paramref name="air"/> is the wind here, for filling in a lasting effect.</summary>
        public void Step(float seconds, bool visible, Vector3 air)
        {
            if (Effect.Lasts && !visible)
            {
                _hiddenFor += seconds;
                Age += seconds;
                return;
            }
            // Something that's jumped somewhere hasn't flown there, and leaves no trail along the way.
            if (!_started || (Position - _previous).LengthSquared > MostTrail * MostTrail) _previous = Position;
            _origin = Position;
            if (Effect.Lasts && (!_started ? Prewarm : _hiddenFor > 0.5f)) FillIn(air);
            _started = true;
            _hiddenFor = 0f;

            float density = Particles.Density;
            ParticleEmitter[] emitters = Effect.Emitters;
            for (int i = 0; i < emitters.Length; i++)
            {
                ParticleEmitter emitter = emitters[i];
                float start = emitter.Delay;
                if (Age + seconds < start) continue;

                if (emitter.Burst > 0 && !_burst[i])
                {
                    _burst[i] = true;
                    // Smoke over a wide blast is a few more puffs, wider, and not more of the same: as many puffs as there was ground was a field of balls.
                    float spread = Spread(emitter);
                    bool smoke = emitter.Style.Sprite == ParticleSprite.Smoke;
                    float grow = smoke ? MathF.Pow(spread, 0.25f) : 1f;
                    if (smoke) spread = MathF.Sqrt(spread);
                    int count = Math.Max(1, (int)MathF.Round(emitter.Burst * density * MathF.Min(Strength, 1f) * spread));
                    for (int n = 0; n < count; n++) Emit(emitter, 0f, default, 1f, grow);
                }

                if (emitter.Rate <= 0f) continue;
                float end = start + emitter.Duration;
                float active = MathF.Min(Age + seconds, end) - MathF.Max(Age, start);
                if (active <= 0f) continue;

                float rate = emitter.Rate * Basis(emitter) * Strength * density;
                float drawnOut = DrawnOut(emitter, rate, air);
                _debt[i] += rate * drawnOut * active;
                int due = (int)_debt[i];
                _debt[i] -= due;
                for (int n = 0; n < due; n++)
                {
                    _origin = Vector3.Lerp(_previous, Position, (n + 1f) / due);
                    Emit(emitter, 0f, default, 1f / MathF.Sqrt(drawnOut));
                }
                _origin = Position;
            }
            Age += seconds;
            _previous = Position;
        }

        /// <summary>What its light gives now, at one metre, and where; nothing if it has none.</summary>
        public bool LightNow(float time, out Vector3 position, out Vector3 colour, out float radius)
        {
            position = Position;
            colour = Vector3.Zero;
            radius = 0f;
            if (Effect.Light is not EffectLight light) return false;

            if (!Effect.Lasts && Age > light.Length) return false;
            float over = Effect.Lasts ? 1f : light.Over[Math.Clamp(Age / MathF.Max(light.Length, 1e-3f), 0f, 1f)];
            float t = time * light.FlickerSpeed + _lightPhase;
            float wave = MathF.Sin(t) * 0.5f + MathF.Sin(t * 2.3f + _lightPhase * 1.7f) * 0.3f + MathF.Sin(t * 5.1f + _lightPhase * 3.1f) * 0.2f;
            float flicker = 1f - light.Flicker * (wave * 0.5f + 0.5f);

            colour = light.Colour * (light.Intensity * over * flicker * MathF.Min(Strength, 1.5f));
            position = Middle + Vector3.UnitY * (light.Height * Scale);
            radius = light.Radius * MathF.Sqrt(Scale) + (Shape.Kind == EffectShapeKind.Area ? Shape.Extent : Shape.Radius);
            return colour != Vector3.Zero;
        }

        /// <summary>The radius of ground an effect is made to fill by itself. A Spreads emitter fills anything bigger with more of the same.</summary>
        public const float SpreadRadius = 3.3f;
        private const float MostSpread = 16f;
        private const float MostTrail = 8f;     // Metres a step past which it's taken to have jumped.
        private const float PlumeStep = 0.35f;  // How far the wind carries a plume between one puff and the next, in the puff's width.
        private const float MostDrawnOut = 6f;

        // Smoke the wind carries off has to come out often enough that each puff overlaps the last, or a plume is a row of balls: as often as the air carries it
        // PlumeStep of a puff's width. Each puff is fainter by the square root of that: by all of it, a plume would be as much smoke in any wind, but a real one
        // narrows as it's drawn out and a puff can't, and a fire's smoke all but vanished in a breeze. The air carries it as fast as the wind's patterns go, as
        // Particle_Simulate.comp has it.
        private float DrawnOut(ParticleEmitter emitter, float rate, Vector3 air)
        {
            ParticleStyle style = emitter.Style;
            if (style.Sprite != ParticleSprite.Smoke || style.Wind <= 0f || rate <= 0f) return 1f;
            float wind = new Vector2(air.X, air.Z).Length;
            float carried = WindField.PatternSpeed(wind) * style.Wind;
            float width = (emitter.Size.Min + emitter.Size.Max) * 0.5f * Scale * style.Size[0f];
            if (width <= 0f) return 1f;
            return Math.Clamp(carried / (width * PlumeStep) / rate, 1f, MostDrawnOut);
        }

        private float Spread(ParticleEmitter emitter)
        {
            if (!emitter.Spreads) return 1f;
            float own = MathF.PI * SpreadRadius * SpreadRadius;
            return Shape.Kind switch
            {
                EffectShapeKind.Ball or EffectShapeKind.Area => Math.Clamp(Shape.SquareMetres / own, 1f, MostSpread),
                _ => 1f,
            };
        }

        // A second of particles for each unit of whatever the rate is of.
        private float Basis(ParticleEmitter emitter) => emitter.Per switch
        {
            RateBasis.SquareMetre => Shape.SquareMetres,
            RateBasis.Metre => MathF.Max(Shape.Height, 0.5f),
            _ => 1f,
        };

        // What a lasting effect would have in the air by now, each particle as far through its life as it would be. The air carries it at the speed the wind's
        // patterns go, as Particle_Simulate.comp has it, or a plume coming into view in a gale would reach twice as far as the one it joins.
        private void FillIn(Vector3 air)
        {
            float speed = air.Length;
            Vector3 carried = speed > 1e-3f ? air * (WindField.PatternSpeed(speed) / speed) : Vector3.Zero;
            float density = Particles.Density;
            foreach (ParticleEmitter emitter in Effect.Emitters)
            {
                if (emitter.Rate <= 0f || !float.IsPositiveInfinity(emitter.Duration) || emitter.ArcReach != null) continue;
                float meanLife = (emitter.Life.Min + emitter.Life.Max) * 0.5f;
                float rate = emitter.Rate * Basis(emitter) * Strength * density;
                float drawnOut = DrawnOut(emitter, rate, air);
                float count = rate * drawnOut * meanLife;
                int whole = (int)count + (Particles.Random.Float() < count - (int)count ? 1 : 0);
                for (int n = 0; n < whole; n++) Emit(emitter, -1f, carried, 1f / MathF.Sqrt(drawnOut));
            }
        }

        // One particle, or one arc. An age below 0 means somewhere through its life, where it would have got to. A faint one is part of a plume drawn out by the wind.
        private void Emit(ParticleEmitter emitter, float age, Vector3 air = default, float faint = 1f, float grow = 1f)
        {
            WorldRandom random = Particles.Random;
            ParticleStyle style = emitter.Style;
            if (emitter.Fills && Shape.Kind == EffectShapeKind.Area) return;
            Vector3 at = emitter.Spreads && Shape.Kind is EffectShapeKind.Ball or EffectShapeKind.Area ? Spot(emitter, random) : Place(emitter, random);
            if (emitter.Jitter > 0f)
            {
                Vector2 aside = Disc(random) * (emitter.Jitter * Scale);
                at += new Vector3(aside.X, 0f, aside.Y);
                if (emitter.Place == EmitterPlace.Ground) at.Y = Particles.GroundAt(at.X, at.Z) + Turn(emitter.Offset * Scale).Y;
            }

            if (emitter.ArcReach is FloatRange reach)
            {
                Vector3 to = Toward(emitter, at, random.Range(reach) * Scale, random);
                float width = random.Range(emitter.Size) * Scale;
                Arcs.Strike(style, at, to, width, random.Range(emitter.Life), emitter.Tint * Tint, emitter.Strength, forks: width > 0.15f ? 2 : 1, priority: Effect.Priority);
                return;
            }

            Vector3 velocity = Turn(Scatter(emitter.Direction, emitter.Spread, random)) * random.Range(emitter.Speed) + Velocity * emitter.Inherit;
            if (emitter.Outward != 0f)
            {
                Vector3 out_ = at - Middle;
                if (Shape.Kind is EffectShapeKind.Body or EffectShapeKind.Area) out_.Y = 0f;
                velocity += (out_.LengthSquared > 1e-6f ? out_.Normalized() : Scatter(Vector3.UnitY, MathF.PI, random)) * emitter.Outward;
            }

            float life = random.Range(emitter.Life);
            float size = random.Range(emitter.Size) * (emitter.Fills && Shape.Radius > 0f ? 2f * Shape.Radius : Scale) * grow;
            var spawn = new ParticleSpawn(at, size, life)
            {
                Velocity = velocity,
                Angle = emitter.Turned ? random.Range(0f, MathF.Tau) : 0f,
                Spin = random.Range(emitter.Spin) * (random.Chance(0.5f) ? 1f : -1f),
                Tint = emitter.Tint * Tint,
                Strength = emitter.Strength * faint,
            };
            if (age < 0f)
            {
                spawn.Age = random.Range(0f, life * 0.98f);
                spawn.Position += Drift(style, velocity, air, spawn.Age);
                spawn.Angle += spawn.Spin * spawn.Age;
            }
            Particles.Spawn(style, spawn, Effect.Priority);
        }

        private Vector3 Place(ParticleEmitter emitter, WorldRandom random)
        {
            Vector3 offset = Turn(emitter.Offset * Scale);
            float radius = Shape.Radius;
            switch (Shape.Kind)
            {
                case EffectShapeKind.Area:
                {
                    Vector2 xz = Shape.Area!.Anywhere(random.Float(), random.Float());
                    float ground = Particles.GroundAt(xz.X, xz.Y);
                    float up = emitter.Place == EmitterPlace.Ground ? 0f : random.Float();
                    return new Vector3(xz.X, ground, xz.Y) + new Vector3(offset.X, offset.Y * (emitter.Place == EmitterPlace.Within ? up : 1f), offset.Z);
                }
                case EffectShapeKind.Body:
                {
                    float height = Shape.Height;
                    Vector2 disc = Disc(random) * radius;
                    return emitter.Place switch
                    {
                        EmitterPlace.Centre => _origin + Vector3.UnitY * (height * 0.5f) + offset,
                        EmitterPlace.Top => _origin + new Vector3(disc.X * 0.4f, height * random.Range(0.82f, 1f), disc.Y * 0.4f) + offset,
                        EmitterPlace.Ground => OnGround(_origin.Xz + Disc(random) * radius) + offset,
                        EmitterPlace.Surface => _origin + Around(Ring(random, radius), height * random.Float()) + offset,
                        _ => _origin + new Vector3(disc.X, height * random.Float(), disc.Y) + offset,
                    };
                }
                case EffectShapeKind.Face:
                {
                    IEffectFace face = Shape.Face!;
                    return emitter.Place switch
                    {
                        EmitterPlace.Centre => _origin + Turn(face.Middle) + offset,
                        EmitterPlace.Surface => _origin + Turn(face.Edge(random.Float())) + offset,
                        EmitterPlace.Ground => OnGround(_origin.Xz + Turn(face.Anywhere(random.Float(), random.Float())).Xz) + offset,
                        EmitterPlace.Top => _origin + Turn(face.Middle + Vector3.UnitY * face.Reach) + offset,
                        _ => _origin + Turn(face.Anywhere(random.Float(), random.Float())) + offset,
                    };
                }
                case EffectShapeKind.Ball:
                    return emitter.Place switch
                    {
                        EmitterPlace.Centre => _origin + offset,
                        EmitterPlace.Surface => _origin + Scatter(Vector3.UnitY, MathF.PI, random) * radius + offset,
                        EmitterPlace.Ground => OnGround(_origin.Xz + Disc(random) * radius) + offset,
                        EmitterPlace.Top => _origin + Vector3.UnitY * radius + offset,
                        _ => _origin + Scatter(Vector3.UnitY, MathF.PI, random) * (radius * MathF.Cbrt(random.Float())) + offset,
                    };
                default:
                    return emitter.Place == EmitterPlace.Ground ? OnGround(_origin.Xz) + offset : _origin + offset;
            }
        }

        // Somewhere on the ground of a wide ball or an area, for what spreads over it.
        private Vector3 Spot(ParticleEmitter emitter, WorldRandom random)
        {
            Vector3 offset = Turn(emitter.Offset * Scale);
            Vector2 xz = Shape.Kind == EffectShapeKind.Area ? Shape.Area!.Anywhere(random.Float(), random.Float()) : _origin.Xz + Disc(random) * Shape.Radius;
            return OnGround(xz) + offset;
        }

        // Where an arc from here goes: another point in the shape about this far off, or a point this far away if the shape's too small for that.
        private Vector3 Toward(ParticleEmitter emitter, Vector3 from, float distance, WorldRandom random)
        {
            Vector2 way = Disc(random);
            way = way.LengthSquared > 1e-6f ? way.Normalized() : Vector2.UnitX;
            Vector3 to = from + new Vector3(way.X, 0f, way.Y) * distance;
            switch (emitter.Place)
            {
                case EmitterPlace.Ground:
                    return OnGround(to.Xz) + Vector3.UnitY * (from.Y - Particles.GroundAt(from.X, from.Z));
                case EmitterPlace.Surface when Shape.Kind == EffectShapeKind.Body:
                    // Round the body and up or down it, as a crackle crawls over someone.
                    Vector2 side = Ring(random, Shape.Radius);
                    return new Vector3(_origin.X + side.X, Math.Clamp(from.Y + random.Range(-distance, distance), _origin.Y, _origin.Y + Shape.Height), _origin.Z + side.Y);
                default:
                    return to + Vector3.UnitY * random.Range(-0.4f, 0.4f) * distance;
            }
        }

        private static Vector3 OnGround(Vector2 xz) => new Vector3(xz.X, Particles.GroundAt(xz.X, xz.Y), xz.Y);

        private static Vector3 Around(Vector2 side, float up) => new Vector3(side.X, up, side.Y);

        private static Vector2 Disc(WorldRandom random)
        {
            float angle = random.Range(0f, MathF.Tau);
            float r = MathF.Sqrt(random.Float());
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
        }

        private static Vector2 Ring(WorldRandom random, float radius)
        {
            float angle = random.Range(0f, MathF.Tau);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        // A unit vector within `spread` radians of `direction`, evenly over that cap of the sphere.
        private static Vector3 Scatter(Vector3 direction, float spread, WorldRandom random)
        {
            if (direction.LengthSquared < 1e-8f) return Vector3.Zero;
            direction.Normalize();
            if (spread <= 0f) return direction;
            float cosMax = MathF.Cos(MathF.Min(spread, MathF.PI));
            float cos = 1f - random.Float() * (1f - cosMax);
            float sin = MathF.Sqrt(MathF.Max(1f - cos * cos, 0f));
            float turn = random.Range(0f, MathF.Tau);
            Vector3 a = MathF.Abs(direction.Y) < 0.99f ? Vector3.Cross(direction, Vector3.UnitY).Normalized() : Vector3.UnitX;
            Vector3 b = Vector3.Cross(direction, a);
            return direction * cos + (a * MathF.Cos(turn) + b * MathF.Sin(turn)) * sin;
        }

        // From the effect's own frame, where +z is Forward, into the world. Only its heading counts: up stays up.
        private Vector3 Turn(Vector3 local)
        {
            Vector2 heading = Forward.Xz;
            if (heading.LengthSquared < 1e-8f) return local;
            heading.Normalize();
            return new Vector3(local.X * heading.Y + local.Z * heading.X, local.Y, -local.X * heading.X + local.Z * heading.Y);
        }

        // About where a particle would have got to in this long, for filling in: drag towards the air it's in, lift and weight. Swirls and gusts are left out.
        private static Vector3 Drift(ParticleStyle style, Vector3 velocity, Vector3 air, float age)
        {
            float lift = style.Buoyancy;
            if (float.IsFinite(style.BuoyancyFade) && style.BuoyancyFade > 0f && age > 1e-3f) lift *= style.BuoyancyFade / age * (1f - MathF.Exp(-age / style.BuoyancyFade));
            Vector3 push = new Vector3(0f, lift - style.Gravity, 0f);
            if (style.Drag < 1e-3f) return velocity * age + push * (0.5f * age * age);

            Vector3 settled = air * style.Wind + push / style.Drag;
            return settled * age + (velocity - settled) * ((1f - MathF.Exp(-style.Drag * age)) / style.Drag);
        }
    }
}
