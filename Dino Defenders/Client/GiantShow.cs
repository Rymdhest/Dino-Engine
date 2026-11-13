using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.ECS.Systems;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
using Dino_Engine.Wildlife;
using Dino_Engine.World;
using OpenTK.Mathematics;
using GiantSounds = Dino_Engine.Sound.Content.Giants;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// The Haven's giants, which belong to nobody, and the herd behind the main menu: read LORE.md, and the giants in WILDLIFE.md. They're only ever looked at, so like what's alive they're this
    /// client's alone: nothing about them is sent, saved or read by the rules, and each machine has its own. Each is one body, drawn as any character is and told by a <see cref="Herd"/> where to stand
    /// and what to do with its head, and everything heard or felt of it is read off that body: a foot coming down presses the grass under it, sends a ring through the grass round it and is heard
    /// where it lands, a call is heard from its throat, and a pull at a tree shakes the crown towards it.
    /// </summary>
    public sealed class GiantShow
    {
        private const float FootMark = 2.6f;        // Metres from the middle of a foot that it presses the grass, at a scale of one: well past the foot itself, as a weight that size flattens what's round it.
        private const float TreadReach = 6.5f;      // Metres the ring a foot sends through the grass goes out, at a scale of one.
        private const float TugGust = 4.5f;         // A pull at a tree, as a gust drawing its crown towards the mouth.
        private const float TugFor = 0.6f;
        private const float NearCamera = 7f;        // Metres from the camera inside which any of a giant thins away, as nothing should ever be seen from inside.
        private const float FadeIn = 2.5f;          // Seconds a giant takes to thin in, if it grew after the world was on screen.
        private const int MostPeople = 16;

        private static readonly BitMask Walkers = new BitMask(typeof(PlantContactComponent), typeof(PositionComponent));

        private readonly ECSWorld _ecs;
        private readonly WorldData _world;
        private readonly CharacterFactory _characters;
        private readonly Herd _herd;
        private readonly Entity[] _bodies;
        private readonly Vector3[] _was;
        private readonly Vector2[] _people = new Vector2[MostPeople];
        private bool _standing;
        private float _shown;

        private GiantShow(ECSWorld ecs, WorldData world, CharacterFactory characters, Herd herd, bool atOnce)
        {
            _ecs = ecs;
            _world = world;
            _characters = characters;
            _herd = herd;
            _bodies = new Entity[herd.Members.Count];
            _was = new Vector3[herd.Members.Count];

            // Grown already, they're there from the first frame, as they should be. Grown since the world was put on screen, they thin in.
            _shown = atOnce && characters.TryGiants(out _) ? 1f : 0f;
        }

        /// <summary>The Haven's herd, somewhere on the isle that isn't the village, different each time anyone comes home. Nothing in any other world.</summary>
        public static GiantShow? InHaven(ECSWorld ecs, WorldData world, CharacterFactory characters, uint seed)
        {
            Herd? herd = Herds.InHaven(world, Shapes(), seed);
            return herd == null ? null : new GiantShow(ecs, world, characters, herd, atOnce: true);
        }

        /// <summary>A herd kept to somewhere: the one in the picture behind the main menu.</summary>
        public static GiantShow? Around(ECSWorld ecs, WorldData world, CharacterFactory characters, Vector2 home, float radius, uint seed, Func<Vector2, bool>? allowed = null)
        {
            Herd? herd = Herds.Around(world, home, radius, Shapes(), seed, allowed);
            return herd == null ? null : new GiantShow(ecs, world, characters, herd, atOnce: true);
        }

        private static List<GiantShape> Shapes()
        {
            var shapes = new List<GiantShape>();
            foreach ((string model, float scale) in Herds.Haven) shapes.Add(GiantShape.Of(CharacterFactory.RigOf(model), scale));
            return shapes;
        }

        /// <summary>Every frame, before the bodies are moved: the herd decides, and each body is told where it is and what it's doing, and what it did since the last is heard and felt.</summary>
        public void Update(float deltaTime)
        {
            if (!_standing && !Stand()) return;

            if (!Nature.Paused && deltaTime > 0f)
            {
                _herd.Step(deltaTime, ReadPeople());
                _shown = MathF.Min(_shown + deltaTime / FadeIn, 1f);
            }

            Vector3 camera = _ecs.Camera.IsValid() && _ecs.TryGetEntityLocation(_ecs.Camera, out _, out _) ? _ecs.GetComponent<LocalToWorldMatrixComponent>(_ecs.Camera).value.ExtractTranslation() : new Vector3(float.MaxValue);
            for (int i = 0; i < _herd.Members.Count; i++)
            {
                Entity entity = _bodies[i];
                if (!_ecs.TryGetEntityLocation(entity, out _, out _)) continue;

                Giant giant = _herd.Members[i];
                EntityView body = _ecs.GetEntityView(entity);
                CharacterAnimator animator = body.Get<AnimatorComponent>().Animator;

                // What it did since the last frame, read off the body that did it.
                Felt(giant, animator);

                Vector2 root = giant.Root;
                var position = new Vector3(root.X, _world.Height.Sample(root), root.Y);
                body.Set(new PositionComponent(position));
                body.Set(new RotationComponent(Frames.Yaw(giant.Yaw)));
                body.Set(new VelocityComponent { value = deltaTime > 0f ? (position - _was[i]) / deltaTime : Vector3.Zero });
                _was[i] = position;

                animator.Orders.ReachFor = giant.ReachFor;
                animator.Orders.Reaching = giant.Reaching;
                animator.Orders.LookAt = giant.LookAt;
                animator.Orders.Looking = giant.Looking;
                animator.Orders.Chewing = giant.Chewing;
                animator.Orders.KeepOff = giant.KeepOff;
                animator.Orders.KeepOffRadius = giant.KeepOffRadius;

                var look = body.Get<CharacterModelComponent>();
                look.Faded = MathF.Max(1f - _shown, NearlyInside(giant, animator, position, camera));
                body.Set(look);
            }

            foreach (Giant giant in _herd.Members) Heard(giant);
        }

        // Once their bodies have grown: one each, standing where the herd has them.
        private bool Stand()
        {
            if (!_characters.TryGiants(out glModel[] models)) return false;
            for (int i = 0; i < _herd.Members.Count; i++)
            {
                Giant giant = _herd.Members[i];
                string key = Herds.Haven[giant.Index].Model;
                CharacterFactory.Bearing bearing = CharacterFactory.BearingOf(key);
                var animator = new CharacterAnimator(bearing.Rig, bearing.Style, 911 + giant.Index * 37);
                Vector2 root = giant.Root;
                var position = new Vector3(root.X, _world.Height.Sample(root), root.Y);
                float scale = giant.Shape.Scale;
                _was[i] = position;
                _bodies[i] = _ecs.CreateEntity("Giant",
                    new PositionComponent(position),
                    new RotationComponent(Frames.Yaw(giant.Yaw)),
                    new ScaleComponent(new Vector3(scale)),
                    new LocalToWorldMatrixComponent(),
                    new CharacterModelComponent(models[giant.Index]) { Faded = 1f - _shown },
                    new AnimatorComponent(animator),
                    new SkeletonComponent(bearing.Rig, animator.Skinning),
                    new VelocityComponent(),
                    new PlantContactComponent(FootMark * scale / Treads.FootShare) { feetOnly = true });
            }
            _ecs.ApplyDeferredCommands();
            _standing = true;
            return true;
        }

        // Everyone who walks through the plants, which is everyone, but the giants themselves.
        private ReadOnlySpan<Vector2> ReadPeople()
        {
            int count = 0;
            foreach (Archetype archetype in _ecs.QueryArchetypes(Walkers, BitMask.Empty))
            {
                var contacts = archetype.GetComponentArray<PlantContactComponent>()!;
                var positions = archetype.GetComponentArray<PositionComponent>()!;
                for (int i = 0; i < archetype.EntityCount && count < MostPeople; i++)
                {
                    if (contacts[i].feetOnly) continue;
                    _people[count++] = positions[i].value.Xz;
                }
            }
            return new ReadOnlySpan<Vector2>(_people, 0, count);
        }

        // A foot that came down: the grass under it pressed flat by the body's own contact, the thud of it where it landed, and what any heavy foot does (Treads.Felt), a ring of wind through
        // the grass round it and a little of the ground thrown up, or a splash in water.
        private void Felt(Giant giant, CharacterAnimator animator)
        {
            int landed = animator.Footfalls;
            if (landed == 0) return;
            float scale = giant.Shape.Scale;
            Foot[] feet = animator.Gait.Feet;
            for (int f = 0; f < feet.Length; f++)
            {
                if ((landed & (1 << f)) == 0) continue;
                Vector3 at = feet[f].At;
                Treads.Felt(_ecs, _world, at, TreadReach * scale, scale);
                Audio.Play(GiantSounds.Step, at + Vector3.UnitY * 0.4f);
            }
        }

        // A call is heard from its throat, and a pull at a tree shakes the crown, drawn towards the mouth by a gust from beyond the trunk.
        private void Heard(Giant giant)
        {
            if (giant.Called) Audio.Play(GiantSounds.Call, giant.CalledFrom);
            if (!giant.Tugged) return;
            Audio.Play(GiantSounds.Crop, giant.TuggedAt);
            if (!giant.EatingFrom(out Vector2 trunk)) return;
            Vector2 toMouth = giant.TuggedAt.Xz - trunk;
            float reach = MathF.Max(toMouth.Length, 1f);
            Vector2 beyond = trunk - toMouth / reach * reach;
            Wind.AddBlast(_ecs, new Vector3(beyond.X, giant.TuggedAt.Y, beyond.Y), reach * 2.6f, TugGust, TugFor);
        }

        // How far it should have thinned away for a camera that's nearly inside it: by how near the camera is to its body or its head.
        private static float NearlyInside(Giant giant, CharacterAnimator animator, Vector3 position, Vector3 camera)
        {
            Vector3 head = position + Vector3.Transform(animator.JointOf(BoneType.Head), Frames.Yaw(giant.Yaw)) * giant.Shape.Scale;
            Vector3 middle = new Vector3(giant.Middle.X, position.Y + giant.Shape.NeckRoot.Y * 0.8f, giant.Middle.Y);
            float body = (camera - middle).Length - giant.Shape.Back - giant.Shape.HalfWidth;
            float nearest = MathF.Min((camera - head).Length - 1.5f * giant.Shape.Scale, body);
            return 1f - Math.Clamp(nearest / (NearCamera * giant.Shape.Scale), 0f, 1f);
        }
    }
}
