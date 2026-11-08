using Dino_Engine.Animation;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Sound;
using Dino_Engine.Sound.Content;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Breaks loose clutter (<see cref="StructureDefinition.Loose"/>): barrels and crates go to pieces when a blast that bends the grass is strong enough where they stand,
    /// or when a body walks into them, and the pieces fly, tumble and lie on the ground a while. It's only something to see. It runs on each client by itself,
    /// reads nothing but what's drawn, and nothing in the rules knows of it: clutter is solid to nothing, so a fight never waits on what's broken.
    /// </summary>
    public class BreakageSystem : SystemBase
    {
        private const float BreakingWind = 10f;     // Metres a second of blast where something stands that breaks it. A dash's puff doesn't; a slam does, over most of where it struck.
        private const float BodyShare = 0.5f;       // Of how far a body parts plants, how far it reaches itself.
        private const float Reach = 0.75f;          // Of a piece of clutter's footprint, how much of it a body has to walk into.
        private const int PiecesEach = 14;
        private const int MostPieces = 420;         // However much is broken, past this the newest breaks throw fewer pieces.
        private const float LieTime = 24f;          // Seconds a piece lies there before it sinks away.
        private const float SinkTime = 1.5f;
        private const float SinkSpeed = 0.35f;      // Metres a second, as it goes.
        private const float Bounce = 0.3f;
        private const float BounceSpeed = 3f;       // Metres a second down, under which a piece stops bouncing and settles.
        private const float Friction = 6f;          // Per second, of what's left of how it slides and spins on the ground.
        private const float Settle = 6f;            // Per second, of how far it is from lying flat.

        private static readonly BitMask Bodies = new BitMask(typeof(PlantContactComponent), typeof(PositionComponent));

        private readonly Dictionary<StructureDefinition, (glModel Model, Vector3 Half)[]> _pieces = new();
        private readonly List<(long Key, Vector2 Push)> _breaking = new();
        private readonly Random _random = new();
        private int _live;

        public BreakageSystem()
            : base(new BitMask(typeof(DebrisComponent), typeof(PositionComponent), typeof(RotationComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            if (!world.TryGetSingleton<WorldStreamSingleton>(out Entity streamEntity)) return;
            WorldStreamSingleton stream = world.GetComponent<WorldStreamSingleton>(streamEntity);

            if (stream.Loose.Count > 0) FindBreaks(world, stream);
            foreach ((long key, Vector2 push) in _breaking)
            {
                if (stream.Loose.TryGetValue(key, out LooseObject loose)) Break(world, stream, key, loose, push);
            }
            _breaking.Clear();

            if (deltaTime <= 0f) return;
            int live = 0;
            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Fall(entity, world, stream.World, deltaTime);
                    live++;
                }
            }
            _live = live;
        }

        private void FindBreaks(ECSWorld world, WorldStreamSingleton stream)
        {
            if (Wind.TryGet(world, out WindSingleton wind) && wind.Blasts != null)
            {
                foreach (WindEmitter blast in wind.Blasts)
                {
                    foreach ((long key, LooseObject loose) in stream.Loose)
                    {
                        Vector2 gust = WindField.EmitterVelocity(blast, loose.Position.Xz).Xz;
                        if (gust.LengthSquared >= BreakingWind * BreakingWind) _breaking.Add((key, gust * 0.5f));
                    }
                }
            }

            foreach (Archetype archetype in world.QueryArchetypes(Bodies, BitMask.Empty))
            {
                foreach (EntityView body in new ComponentAccessor(archetype))
                {
                    Vector2 at = body.Get<PositionComponent>().value.Xz;
                    float radius = body.Get<PlantContactComponent>().radius * BodyShare;
                    Vector2 walking = body.Has<VelocityComponent>() ? body.Get<VelocityComponent>().value.Xz : Vector2.Zero;
                    foreach ((long key, LooseObject loose) in stream.Loose)
                    {
                        float reach = radius + loose.Definition.FootprintAt(Vector2.Zero, 0f).BoundingRadius * loose.Scale.X * Reach;
                        Vector2 away = loose.Position.Xz - at;
                        if (away.LengthSquared >= reach * reach) continue;
                        Vector2 push = away.LengthSquared > 1e-4f ? away.Normalized() * 3f : Vector2.Zero;
                        _breaking.Add((key, push + walking * 0.6f));
                    }
                }
            }
        }

        // Gone from where it stood, for as long as this world is, and in its place what it was made of, thrown the way it was struck.
        private void Break(ECSWorld world, WorldStreamSingleton stream, long key, LooseObject loose, Vector2 push)
        {
            if (world.TryGetSingleton<RenderSpatialGridSingleton>(out Entity gridEntity)) world.GetComponent<RenderSpatialGridSingleton>(gridEntity).Grid.Remove(loose.Entity);
            world.DestroyEntity(loose.Entity);
            if (stream.Sectors.TryGetValue(loose.Sector, out List<Entity>? entities)) entities.RemoveAll(entity => entity.Id == loose.Entity.Id);
            stream.Loose.Remove(key);
            stream.Broken.Add(key);

            Audio.Play(Impacts.Wood, loose.Position + Vector3.UnitY, 2f, Range(0.8f, 1.1f));
            Audio.Play(Impacts.Debris, loose.Position + Vector3.UnitY, 0f, Range(0.9f, 1.15f));

            if (!_pieces.TryGetValue(loose.Definition, out var kinds))
            {
                Mesh[] meshes = PropGenerator.Pieces(loose.Definition);
                kinds = Array.ConvertAll(meshes, mesh => (glLoader.loadToVAO(mesh), Extent(mesh)));
                _pieces.Add(loose.Definition, kinds);
            }

            int count = Math.Min(PiecesEach, Math.Max(MostPieces - _live, 0));
            _live += count;
            float wide = loose.Definition.FootprintAt(Vector2.Zero, 0f).BoundingRadius * loose.Scale.X;
            float tall = loose.Definition.Height * loose.Scale.Y;
            for (int i = 0; i < count; i++)
            {
                (glModel model, Vector3 half) = kinds[_random.Next(kinds.Length)];
                float size = Range(0.8f, 1.1f) * loose.Scale.X;
                float angle = Range(0f, MathF.Tau);
                Vector2 out_ = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                Vector3 position = loose.Position + new Vector3(out_.X * wide * Range(0f, 0.7f), tall * Range(0.15f, 0.9f), out_.Y * wide * Range(0f, 0.7f));
                Vector2 flung = push + out_ * Range(1f, 4.5f);
                var rotation = Quaternion.FromEulerAngles(Range(0f, MathF.Tau), Range(0f, MathF.Tau), Range(0f, MathF.Tau));
                Vector3 axis = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                if (axis.LengthSquared < 1e-3f) axis = Vector3.UnitX;

                world.CreateEntity(
                    new PositionComponent(position),
                    new RotationComponent(rotation),
                    new ScaleComponent(new Vector3(size)),
                    new ModelComponent(model),
                    new ModelRenderTag(),
                    new LocalToWorldMatrixComponent { value = MyMath.createTransformationMatrix(position, rotation, new Vector3(size)) },
                    new DebrisComponent { Velocity = new Vector3(flung.X, Range(3f, 9f), flung.Y), Spin = axis.Normalized() * Range(4f, 14f), Half = half * size });
            }
        }

        // Flies, lands, bounces a little and slides to a stop lying flat, as a board does. A while later it sinks into the ground and is gone.
        private static void Fall(EntityView entity, ECSWorld world, WorldData land, float deltaTime)
        {
            var debris = entity.Get<DebrisComponent>();
            Vector3 position = entity.Get<PositionComponent>().value;
            Quaternion rotation = entity.Get<RotationComponent>().quaternion;
            debris.Age += deltaTime;

            if (debris.Age > LieTime)
            {
                if (debris.Age > LieTime + SinkTime) world.DestroyEntity(entity.Entity);
                entity.Set(new PositionComponent(position - Vector3.UnitY * (SinkSpeed * deltaTime)));
                entity.Set(debris);
                return;
            }

            debris.Velocity.Y -= Rig.Gravity * deltaTime;
            position += debris.Velocity * deltaTime;
            float spin = debris.Spin.Length;
            if (spin > 1e-4f) rotation = Quaternion.FromAxisAngle(debris.Spin / spin, spin * deltaTime) * rotation;

            // How far down it reaches from its middle, turned as it is.
            Vector3 x = Vector3.Transform(Vector3.UnitX, rotation), y = Vector3.Transform(Vector3.UnitY, rotation), z = Vector3.Transform(Vector3.UnitZ, rotation);
            float below = MathF.Abs(x.Y) * debris.Half.X + MathF.Abs(y.Y) * debris.Half.Y + MathF.Abs(z.Y) * debris.Half.Z;
            float ground = land.Height.SampleCubic(position.X, position.Z);
            if (position.Y - below <= ground)
            {
                position.Y = ground + below;
                if (debris.Velocity.Y < -BounceSpeed)
                {
                    debris.Velocity = new Vector3(debris.Velocity.X * 0.65f, -debris.Velocity.Y * Bounce, debris.Velocity.Z * 0.65f);
                    debris.Spin *= 0.55f;
                }
                else
                {
                    float keep = MathF.Exp(-Friction * deltaTime);
                    debris.Velocity = new Vector3(debris.Velocity.X * keep, 0f, debris.Velocity.Z * keep);
                    debris.Spin *= keep;

                    // Down onto its broad side, whichever is nearer.
                    Vector3 flat = y.Y >= 0f ? Vector3.UnitY : -Vector3.UnitY;
                    Vector3 axis = Vector3.Cross(y, flat);
                    float off = MathF.Acos(Math.Clamp(Vector3.Dot(y, flat), -1f, 1f));
                    if (axis.LengthSquared > 1e-8f && off > 1e-3f) rotation = Quaternion.FromAxisAngle(axis.Normalized(), off * (1f - MathF.Exp(-Settle * deltaTime))) * rotation;
                }
            }

            entity.Set(new PositionComponent(position));
            entity.Set(new RotationComponent(Quaternion.Normalize(rotation)));
            entity.Set(debris);
        }

        // Half a piece's size along each of its own axes.
        private static Vector3 Extent(Mesh mesh)
        {
            Vector3 most = Vector3.Zero;
            foreach (MeshVertex vertex in mesh.meshVertices) most = Vector3.ComponentMax(most, Vector3.ComponentMax(vertex.position, -vertex.position));
            return most;
        }

        private float Range(float from, float to) => from + (float)_random.NextDouble() * (to - from);
    }
}
