using Dino_Defenders.Gameplay;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using OpenTK.Mathematics;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// What each kind of damage looks like as particles (read PARTICLES.md). Fire, cold, lightning, venom and magic each have a burst, a patch of ground, a bolt's trail, a blow
    /// landing and what someone suffering from them gives off, and physical blows have blood, bone and sparks. It's all for show, on this machine alone.
    /// </summary>
    public static class ParticleShow
    {
        public static ParticleEffect BurstOf(DamageType type) => type switch
        {
            DamageType.Fire => Fire.Burst,
            DamageType.Cold => Frost.Burst,
            DamageType.Lightning => Lightning.Burst,
            DamageType.Poison => Poison.Burst,
            DamageType.Magic => Arcane.Burst,
            _ => Impact.Shockwave,
        };

        public static ParticleEffect GroundOf(DamageType type) => type switch
        {
            DamageType.Fire => Fire.Ground,
            DamageType.Cold => Frost.Ground,
            DamageType.Lightning => Lightning.Ground,
            DamageType.Poison => Poison.Ground,
            DamageType.Magic => Arcane.Ground,
            _ => Impact.Churn,
        };

        public static ParticleEffect? BoltOf(DamageType type) => type switch
        {
            DamageType.Fire => Fire.Bolt,
            DamageType.Cold => Frost.Bolt,
            DamageType.Lightning => Lightning.Bolt,
            DamageType.Poison => Poison.Bolt,
            DamageType.Magic => Arcane.Bolt,
            _ => null,
        };

        /// <summary>A blow of this kind landing, or the same in a caster's hand. Physical blows land as blood, bone or sparks: see <see cref="Wound"/>.</summary>
        public static ParticleEffect? HitOf(DamageType type) => type switch
        {
            DamageType.Fire => Fire.Hit,
            DamageType.Cold => Frost.Hit,
            DamageType.Lightning => Lightning.Hit,
            DamageType.Poison => Poison.Hit,
            DamageType.Magic => Arcane.Hit,
            _ => null,
        };

        /// <summary>What a body is made of, for what comes off it when it's struck: blood, or bone, or the dark of something rotting.</summary>
        public enum Flesh
        {
            Blood,
            Bone,
            Rot,
        }

        public static Flesh FleshOf(string model)
        {
            if (model.StartsWith("skeleton") || model.Contains(".bones")) return Flesh.Bone;
            if (model == "revenant" || model.Contains(".rot")) return Flesh.Rot;
            return Flesh.Blood;
        }

        private static readonly Vector3 Ichor = new Vector3(0.28f, 0.42f, 0.18f);

        /// <summary>A blow landing on a body, going <paramref name="way"/>: blood thrown along it, or bone chipped, or sparks off armour. A heavy one throws more, and a killing one pools.</summary>
        public static void Wound(Vector3 at, Vector2 way, Flesh flesh, bool armour, float weight, bool critical)
        {
            Vector3 forward = new Vector3(way.X, 0f, way.Y);
            if (forward.LengthSquared < 1e-6f) forward = Vector3.UnitZ;
            float scale = Math.Clamp(0.8f + weight * 1.5f + (critical ? 0.3f : 0f), 0.8f, 1.8f);

            if (armour) Particles.Play(Impact.Sparks, at, 1f, forward: forward);
            if (flesh == Flesh.Bone)
            {
                Particles.Play(Impact.Bone, at, scale, forward: forward);
                return;
            }
            bool gush = weight >= 0.45f || (critical && weight >= 0.2f);
            Vector3 tint = flesh == Flesh.Rot ? Ichor : Vector3.One;
            Particles.Play(gush ? Blood.Gush : Blood.Hit, at, scale, forward: forward, tint: tint);
        }

        /// <summary>How big a body is for what's shown happening all over it, from its model's own size.</summary>
        public static EffectShape BodyOf(in EntityView entity)
        {
            float scale = entity.Has<ScaleComponent>() ? entity.Get<ScaleComponent>().value.Y : 1f;
            Vector3 size = new Vector3(2.2f, 4.4f, 1.6f);
            if (entity.Has<CharacterModelComponent>() && entity.Get<CharacterModelComponent>().model is Dino_Engine.Modelling.Model.glModel model && model.Size.X < 1e6f) size = model.Size;
            float radius = Math.Clamp(0.28f * MathF.Max(size.X, size.Z), 0.35f, 4f) * scale;
            float height = Math.Clamp(size.Y * 0.92f, 0.5f, 20f) * scale;
            return EffectShape.Body(radius, height);
        }
    }

    /// <summary>A piece of ground, as an effect covers it.</summary>
    public sealed class GroundArea : IEffectArea
    {
        public GroundShape Shape;

        public GroundArea(GroundShape shape)
        {
            Shape = shape;
        }

        public Vector2 Anywhere(float u, float v) => Shape.Anywhere(u, v);

        public float SquareMetres => Shape.Kind switch
        {
            ShapeKind.Circle => MathF.PI * Shape.Length * Shape.Length,
            ShapeKind.Sector => 0.5f * Shape.Width * Shape.Length * Shape.Length,
            _ => Shape.Length * Shape.Width * 2f,
        };

        public Vector2 Middle => Shape.Kind switch
        {
            ShapeKind.Circle => Shape.Origin,
            ShapeKind.Sector => Shape.Origin + Shape.Facing * (Shape.Length * 0.6f),
            _ => Shape.Origin + Shape.Facing * (Shape.Length * 0.5f),
        };

        public float Reach => Shape.Kind == ShapeKind.Circle ? Shape.Length : Shape.Extent;
    }

    /// <summary>
    /// What everyone suffering from something gives off: fire on someone burning, cold mist off someone chilled, arcs over someone shocked, venom dripping, blood falling. Each is an
    /// effect that follows them for as long as it lasts, sized to their body, and stops when it's over or they're gone.
    /// </summary>
    public sealed class Ailments
    {
        private static readonly (Condition Condition, ParticleEffect Effect)[] Shown =
        {
            (Condition.Burning, Fire.Burning),
            (Condition.Frozen, Frost.Frozen),
            (Condition.Chilled, Frost.Chilled),
            (Condition.Shocked, Lightning.Shocked),
            (Condition.Envenomed, Poison.Envenomed),
            (Condition.Hexed, Arcane.Hexed),
            (Condition.Bleeding, Blood.Bleeding),
        };

        private sealed class Sufferer
        {
            public readonly EffectRun?[] Runs = new EffectRun?[Shown.Length];
            public int Seen;
        }

        private readonly Dictionary<uint, Sufferer> _sufferers = new();
        private readonly List<uint> _gone = new();
        private int _frame;

        /// <summary>Someone who's afflicted with <paramref name="afflicted"/> this frame.</summary>
        public void Show(in EntityView entity, uint id, in AfflictedComponent afflicted)
        {
            if (!_sufferers.TryGetValue(id, out Sufferer? sufferer))
            {
                if (afflicted.Flags == 0) return;
                sufferer = new Sufferer();
                _sufferers.Add(id, sufferer);
            }
            sufferer.Seen = _frame;

            Vector3 position = entity.Get<PositionComponent>().value;
            Vector3 velocity = entity.Has<VelocityComponent>() ? entity.Get<VelocityComponent>().value : Vector3.Zero;
            EffectShape body = ParticleShow.BodyOf(entity);
            for (int i = 0; i < Shown.Length; i++)
            {
                // Frozen solid is more than chilled, and shows instead.
                bool on = afflicted.Has(Shown[i].Condition) && !(Shown[i].Condition == Condition.Chilled && afflicted.Has(Condition.Frozen));
                EffectRun? run = sufferer.Runs[i];
                if (on && run == null) run = sufferer.Runs[i] = Particles.Begin(Shown[i].Effect, position, body);
                else if (!on && run != null)
                {
                    Particles.Stop(run);
                    sufferer.Runs[i] = run = null;
                }
                if (run == null) continue;
                run.Position = position;
                run.Velocity = velocity;
                run.Shape = body;
            }
        }

        /// <summary>Everyone has been shown for this frame: whoever wasn't is gone, and so is what they gave off.</summary>
        public void EndFrame()
        {
            _gone.Clear();
            foreach (var (id, sufferer) in _sufferers)
            {
                bool any = false;
                foreach (EffectRun? run in sufferer.Runs) any |= run != null;
                if (sufferer.Seen == _frame && any) continue;
                foreach (EffectRun? run in sufferer.Runs) if (run != null) Particles.Stop(run);
                _gone.Add(id);
            }
            foreach (uint id in _gone) _sufferers.Remove(id);
            _frame++;
        }
    }
}
