using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>
    /// A recipe for something that happens: layers of particles (<see cref="ParticleEmitter"/>s) and the light it gives. A campfire is flames, tongues that break off them, embers,
    /// smoke and its glow; a fireball's burst is a ball of fire turning to smoke, sparks, a scorch and a flash. The same recipe fills whatever <see cref="EffectShape"/> it's given,
    /// at whatever scale, so one burning recipe sets a torch, a body or a field on fire. Recipes are in Particles/Content/.
    /// </summary>
    public sealed class ParticleEffect
    {
        public string Name = "";
        public ParticleEmitter[] Emitters = Array.Empty<ParticleEmitter>();
        public EffectLight? Light;
        public ParticlePriority Priority = ParticlePriority.Normal;

        /// <summary>Metres its particles get from where it is, at a scale of 1: how far out of view it can be and still be seen, smoke rising off the top of the screen included.</summary>
        public float Reach = 4f;

        /// <summary>Seconds it emits for, or forever.</summary>
        public float Duration => Emitters.Length == 0 ? 0f : Emitters.Max(e => e.Delay + (e.Rate > 0f ? e.Duration : 0f));

        public bool Lasts => float.IsPositiveInfinity(Duration);

        /// <summary>Seconds until something that happens once is over: its emitting, and its light.</summary>
        public float Length => MathF.Max(Duration, Light?.Length ?? 0f);
    }

    /// <summary>Where in its effect's shape an emitter puts what it makes.</summary>
    public enum EmitterPlace
    {
        Centre,     // At the effect's position, plus the offset.
        Within,     // Anywhere inside the shape.
        Surface,    // On the outside of the shape.
        Ground,     // On the land inside the shape, wherever its edge is.
        Top,        // About the top of the shape, as a head is of a body.
    }

    /// <summary>What an emitter's rate is a rate of.</summary>
    public enum RateBasis
    {
        Effect,         // Particles a second, however big the effect.
        SquareMetre,    // A second for each square metre of ground the shape covers, so a wide fire has more flames and not bigger ones.
        Metre,          // A second for each metre of the shape's height.
    }

    /// <summary>One layer of an effect: a kind of particle, how many, where they start, how they set off.</summary>
    public sealed class ParticleEmitter
    {
        public ParticleStyle Style = null!;

        public float Rate;                      // Particles a second, of RateBasis.
        public RateBasis Per = RateBasis.Effect;
        public int Burst;                       // Particles all at once, when it starts.
        public float Delay;                     // Seconds from the effect starting.
        public float Duration = float.PositiveInfinity;    // Seconds it goes on emitting at its rate.

        public EmitterPlace Place = EmitterPlace.Centre;
        public Vector3 Offset;                  // Metres, times the effect's scale.
        public float Jitter;                    // Metres about where it's put, on the level, times the effect's scale.
        public Vector3 Direction = Vector3.UnitY;
        public float Spread;                    // Radians either side of Direction, out to a whole sphere at pi.
        public FloatRange Speed = new FloatRange(0f, 0f);
        public float Outward;                   // Metres a second away from the effect's middle.
        public float Inherit;                   // Share of the speed of whatever the effect is on.
        public FloatRange Size = new FloatRange(1f, 1f);   // Metres, times the effect's scale.
        public FloatRange Life = new FloatRange(1f, 1f);
        public FloatRange Spin = new FloatRange(0f, 0f);   // Radians a second, either way.
        public bool Turned = true;              // Born at any angle. Otherwise upright.
        public Vector3 Tint = Vector3.One;
        public float Strength = 1f;

        /// <summary>Arcs of lightning between two points in the shape this far apart, instead of particles. See <see cref="Arcs"/>.</summary>
        public FloatRange? ArcReach;

        /// <summary>
        /// Laid over the ground of a shape bigger than an effect is made for (a ball wider than <see cref="EffectRun.SpreadRadius"/>, or an area), with as many more as it takes to
        /// cover it. A burst made for a blow a couple of metres across then fills a blast ten metres across with more of itself, not bigger.
        /// </summary>
        public bool Spreads;

        /// <summary>As big as the ball it's in, Size times its width, whatever the scale: a ring that has to reach the edge of what was hit. It has no place in an area.</summary>
        public bool Fills;
    }

    /// <summary>The light an effect gives, which follows it about and flickers.</summary>
    public sealed class EffectLight
    {
        public Vector3 Colour = Vector3.One;
        public float Intensity = 40f;           // What it gives at one metre, as a light's ColorComponent does.
        public float Radius = 12f;              // Metres, times the effect's scale.
        public float Height = 1f;               // Metres above the effect's position, times its scale.
        public float Fill = 0.2f;
        public float Flicker = 0.3f;            // See LightFlickerComponent.
        public float FlickerSpeed = 9f;
        public bool LightsFog;
        public float Length = 0.4f;             // Seconds it shines for, for something that happens once.
        public Curve Over = Curve.Line(1f, 0f); // Over its Length, for something that happens once.
    }

    /// <summary>The ground an effect is laid over, for one that covers an area: anywhere in it, and how big it is.</summary>
    public interface IEffectArea
    {
        /// <summary>A place in it, on the xz plane, from two numbers 0 to 1, evenly over its area.</summary>
        Vector2 Anywhere(float u, float v);

        float SquareMetres { get; }

        /// <summary>Its middle, on the xz plane, and how far from it anything in it is.</summary>
        Vector2 Middle { get; }
        float Reach { get; }
    }

    /// <summary>
    /// An upright face an effect fills, such as a waygate's way: in the effect's own frame, x across, y up from its position and z out of it along the effect's Forward, so it turns with
    /// whatever it's on. Anything in it lies at z = 0.
    /// </summary>
    public interface IEffectFace
    {
        /// <summary>A place on it, from two numbers 0 to 1, about evenly over it.</summary>
        Vector3 Anywhere(float u, float v);

        /// <summary>A place on its edge, from a number 0 to 1 the way round it.</summary>
        Vector3 Edge(float u);

        float SquareMetres { get; }

        /// <summary>Its middle, and how far from it anything in it is.</summary>
        Vector3 Middle { get; }
        float Reach { get; }
    }

    public enum EffectShapeKind
    {
        Point,
        Ball,       // A sphere of Radius about the position.
        Body,       // An upright cylinder of Radius from the position (its feet) up Height, as someone standing.
        Area,       // The ground an IEffectArea covers.
        Face,       // An upright IEffectFace, square to the effect's Forward.
    }

    /// <summary>What an effect fills. Its size is already scaled: the effect's scale only scales its particles' sizes and offsets.</summary>
    public readonly record struct EffectShape(EffectShapeKind Kind, float Radius = 0f, float Height = 0f, IEffectArea? Area = null, IEffectFace? Face = null)
    {
        public static EffectShape Point => new EffectShape(EffectShapeKind.Point);
        public static EffectShape Ball(float radius) => new EffectShape(EffectShapeKind.Ball, radius);
        public static EffectShape Body(float radius, float height) => new EffectShape(EffectShapeKind.Body, radius, height);
        public static EffectShape Over(IEffectArea area) => new EffectShape(EffectShapeKind.Area, Area: area);
        public static EffectShape Across(IEffectFace face) => new EffectShape(EffectShapeKind.Face, Face: face);

        public float SquareMetres => Kind switch
        {
            EffectShapeKind.Area => Area!.SquareMetres,
            EffectShapeKind.Face => Face!.SquareMetres,
            EffectShapeKind.Point => 1f,
            _ => MathF.PI * Radius * Radius,
        };

        /// <summary>How far from its middle anything in it is.</summary>
        public float Extent => Kind switch
        {
            EffectShapeKind.Area => Area!.Reach,
            EffectShapeKind.Face => Face!.Reach,
            _ => MathF.Max(Radius, Height),
        };
    }
}
