using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Sound;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>Keeps the sound of each <see cref="SoundEmitterComponent"/> where its entity is, and ends it when the entity goes.</summary>
    public class SoundEmitterSystem : SystemBase
    {
        private const float FadeOut = 0.6f;

        private sealed class Emitter
        {
            public SoundHandle Handle;
            public bool Seen;
        }

        private readonly Dictionary<Entity, Emitter> _emitters = new();
        private readonly List<Entity> _gone = new();

        public SoundEmitterSystem()
            : base(new BitMask(typeof(SoundEmitterComponent), typeof(PositionComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Vector3 position = entity.Get<PositionComponent>().value;
                    if (!_emitters.TryGetValue(entity.Entity, out Emitter? emitter))
                    {
                        var sound = entity.Get<SoundEmitterComponent>();
                        emitter = new Emitter { Handle = Audio.Start(sound.Definition, position, sound.Gain) };
                        emitter.Handle.Set(0, sound.Parameter);
                        _emitters.Add(entity.Entity, emitter);
                    }
                    emitter.Handle.Move(position);
                    emitter.Seen = true;
                }
            }

            _gone.Clear();
            foreach ((Entity entity, Emitter emitter) in _emitters)
            {
                if (!emitter.Seen)
                {
                    emitter.Handle.Stop(FadeOut);
                    _gone.Add(entity);
                }
                emitter.Seen = false;
            }
            foreach (Entity entity in _gone) _emitters.Remove(entity);
        }
    }
}
