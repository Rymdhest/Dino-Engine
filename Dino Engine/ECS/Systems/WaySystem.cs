using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.Particles;
using Dino_Engine.Particles.Content;
using Dino_Engine.Physics;
using Dino_Engine.Rendering.Renderers.Geometry;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Runs every open way (<see cref="WayComponent"/>, WAYGATES.md). The first time it sees one it opens it: its runes light up the gate, its edge kindles, its light floods in and meets in the
    /// middle with a flash, a gust goes out through the grass and its throat opens up behind it. From then on it breathes, and its lights, which are its own (a shadowed one in front and one
    /// behind, both given to global illumination as every light is), keep time with it. Motes drift out of it, with whatever drifts in the air of the land it goes to. And it's drawn
    /// whenever it can be seen (<see cref="WayRenderer"/>). Whatever a way leaves behind it, lights and all, goes the frame after it does.
    /// </summary>
    public class WaySystem : SystemBase
    {
        /// <summary>
        /// How much of a way's throat goes the camera's way rather than towards the way itself: 1 is all parallax, 0 a throat that always faces the camera. The camera sees sixty degrees
        /// top to bottom, so a way at the top of the screen is looked into seventeen degrees shallower than one in the middle, and with all of that its far end slid off the bottom of
        /// the opening; this much keeps it in view anywhere on screen and still swings it a metre as a hero walks past.
        /// </summary>
        public const float Parallax = 0.3f;

        private const float LeastAxis = 0.3f;           // The throat never lies flatter to the face than this, whatever the camera does.
        private const float LandDepth = 11f;            // Metres down a land's way to the far end,
        private const float GreatDepth = 14f;           // and the Haven's.
        private const float KindleAt = 1.3f;            // Seconds from opening that its light meets in the middle, a little before Way/WayShape.glsl's WAY_FILL_TO, so the burst is under way as it does.
        private const float LightsFrom = 0.25f;         // Its lights come up from when its edge kindles,
        private const float LightsTo = 1.5f;            // to full when it's open.
        private const float Flash = 1.4f;               // How much brighter its lights are for a moment as it opens.
        private const float ThroughAfter = 1.8f;        // Seconds before what comes through from the other side starts to.

        private readonly Dictionary<WaygateDesign, glModel> _meshes = new();
        private HashSet<WayState> _shown = new();
        private HashSet<WayState> _seen = new();
        private readonly Dictionary<WayState, Vector4[]> _packed = new();
        private Frustum _view = new Frustum(Matrix4.Identity);

        public WaySystem()
            : base(new BitMask(typeof(WayComponent), typeof(PositionComponent), typeof(RotationComponent)))
        {
        }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            WayRenderer renderer = Engine.RenderEngine._wayRenderer;
            renderer.Clear();
            _view = new Frustum(Engine.RenderEngine.context.viewMatrix * Engine.RenderEngine.context.projectionMatrix);
            _seen.Clear();

            base.UpdateFrame(world, deltaTime);

            foreach (WayState gone in _shown)
            {
                if (!_seen.Contains(gone)) Retire(world, gone);
            }
            (_shown, _seen) = (_seen, _shown);
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime)
        {
            WayComponent way = entity.Get<WayComponent>();
            if (way.State == null || way.Design == null || way.Look == null) return;
            WayState state = way.State;
            WaygateDesign design = way.Design;
            WayLook look = way.Look;
            _seen.Add(state);

            Vector3 scale = entity.Has<ScaleComponent>() ? entity.Get<ScaleComponent>().value : Vector3.One;
            Matrix4 model = MyMath.createTransformationMatrix(entity.Get<PositionComponent>().value, entity.Get<RotationComponent>().quaternion, scale);
            Vector3 origin = model.ExtractTranslation();
            Vector3 forward = Vector3.TransformVector(Vector3.UnitZ, model).Normalized();
            Vector3 heart = Vector3.TransformPosition(new Vector3(design.Heart.X, design.Heart.Y, 0f), model);

            if (!state.Begun) Begin(world, state, design, look, model, origin, forward);
            float age = Engine.Time - state.Born;
            float breath = look.Breathing(age);

            // Kindled once, as its light meets in the middle: the burst, and a breath of air out through the grass, too weak to break anything.
            if (!state.Kindled && age >= KindleAt)
            {
                state.Kindled = true;
                if (age < KindleAt + 2f)
                {
                    Particles.Particles.Play(Ways.Kindle, origin, 1f, EffectShape.Across(design.Face), forward, look.Rim);
                    bool great = design.Kind == WaygateKind.Great;
                    Wind.AddBlast(world, origin, great ? 22f : 14f, great ? 8f : 6.5f, 1.3f);
                }
            }

            float lit = Smooth((age - LightsFrom) / (LightsTo - LightsFrom));
            float flash = MathF.Exp(-MathF.Max(age - KindleAt, 0f) * 2.2f) * Smooth((age - (KindleAt - 0.35f)) / 0.35f);
            float shine = lit * breath * (1f + Flash * flash);
            SetLight(world, state.Front, state, look.Light * (design.LightIntensity * shine));
            SetLight(world, state.Back, state, look.Light * (design.BackIntensity * shine));

            if (state.Shimmer != null) state.Shimmer.Strength = lit;
            if (state.Through != null) state.Through.Strength = Smooth((age - ThroughAfter) / 1.5f);

            float reach = design.Reach * MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
            if (!_view.IntersectsSphere(heart, reach)) return;

            Matrix4 toLocal = Matrix4.Invert(model);
            Vector3 eye = Engine.RenderEngine.context.viewPos;
            Vector3 eyeLocal = Vector3.TransformPosition(eye, toLocal);
            Vector3 cameraForward = Vector3.TransformVector(-Vector3.UnitZ, Engine.RenderEngine.context.invViewMatrix).Normalized();
            Vector3 toHeart = (heart - eye).LengthSquared > 1e-6f ? (heart - eye).Normalized() : cameraForward;
            Vector3 axis = Vector3.TransformVector(Vector3.Lerp(toHeart, cameraForward, Parallax).Normalized(), toLocal).Normalized();

            // Into whichever face the camera is on, and never lying along it.
            float side = eyeLocal.Z >= 0f ? 1f : -1f;
            if (axis.Z * side > -LeastAxis)
            {
                Vector2 across = axis.Xy.LengthSquared > 1e-8f ? axis.Xy.Normalized() : Vector2.Zero;
                float flat = MathF.Sqrt(1f - LeastAxis * LeastAxis);
                axis = new Vector3(across.X * flat, across.Y * flat, -LeastAxis * side);
            }

            Engine.RenderEngine._wayRenderer.Submit(new WayRenderCommand
            {
                Mesh = MeshOf(design),
                Model = model,
                Shape = new Vector4(design.HalfWidth, design.Sill, design.Spring, design.Radius),
                Heart = new Vector4(design.Heart.X, design.Heart.Y, design.Kind == WaygateKind.Great ? GreatDepth : LandDepth, design.Kind == WaygateKind.Great ? 1f : 0f),
                Eye = eyeLocal,
                Axis = axis,
                Age = age,
                Strength = 1f,
                Breath = breath,
                Look = Packed(state, look),
            });
        }

        // The first time a way is seen: when it opened, its lights, and what it breathes out.
        private static void Begin(ECSWorld world, WayState state, WaygateDesign design, WayLook look, Matrix4 model, Vector3 origin, Vector3 forward)
        {
            state.Begun = true;
            state.Born = Engine.Time;
            Vector3 back = new Vector3(design.Light.X, design.Light.Y, -design.Light.Z);
            state.Front = Light(world, state, Vector3.TransformPosition(design.Light, model), design.LightRadius, shadowed: true);
            state.Back = Light(world, state, Vector3.TransformPosition(back, model), design.BackRadius, shadowed: false);

            EffectShape face = EffectShape.Across(design.Face);
            state.Shimmer = Particles.Particles.Begin(Ways.Shimmer, origin, face);
            state.Shimmer.Forward = forward;
            state.Shimmer.Tint = look.Rim;
            state.Shimmer.Strength = 0f;
            if (Ways.From(look.Motes) is (ParticleEffect through, bool tinted))
            {
                state.Through = Particles.Particles.Begin(through, origin, face);
                state.Through.Forward = forward;
                state.Through.Tint = tinted ? look.MoteColour : Vector3.One;
                state.Through.Strength = 0f;
            }
        }

        private static Entity Light(ECSWorld world, WayState owner, Vector3 at, float radius, bool shadowed)
        {
            var components = new List<IComponent>
            {
                new PositionComponent(at),
                new LocalToWorldMatrixComponent { value = Matrix4.CreateTranslation(at) },
                new LightRangeComponent(radius, 0.25f, lightsFog: true),
                new ColorComponent(new Colour(0f, 0f, 0f, 0f)),
                new PointLightTag(),
                new WayLightComponent { Owner = owner },
            };
            if (shadowed) components.Add(new PointLightShadowComponent());
            return world.CreateEntity("Way light", components.ToArray());
        }

        // A light is only touched if it's there and still this way's: its id could be anyone's once it's gone.
        private static bool Owned(ECSWorld world, Entity light, WayState owner, out EntityView view)
        {
            view = default;
            if (!light.IsValid() || !world.TryGetEntityLocation(light, out Archetype archetype, out int index)) return false;
            view = new EntityView(archetype, index);
            return view.Has<WayLightComponent>() && view.Get<WayLightComponent>().Owner == owner;
        }

        private static void SetLight(ECSWorld world, Entity light, WayState owner, Vector3 radiance)
        {
            if (!Owned(world, light, owner, out EntityView view)) return;
            float strength = MathF.Max(radiance.X, MathF.Max(radiance.Y, radiance.Z));
            Vector3 hue = strength > 1e-5f ? radiance / strength : Vector3.Zero;
            view.Set(new ColorComponent(new Colour(hue.X, hue.Y, hue.Z, strength)));
        }

        private void Retire(ECSWorld world, WayState state)
        {
            if (Owned(world, state.Front, state, out _)) world.DestroyEntity(state.Front);
            if (Owned(world, state.Back, state, out _)) world.DestroyEntity(state.Back);
            if (state.Shimmer != null) Particles.Particles.Stop(state.Shimmer);
            if (state.Through != null) Particles.Particles.Stop(state.Through);
            state.Front = state.Back = Entity.Invalid;
            state.Shimmer = state.Through = null;
            _packed.Remove(state);
        }

        private glModel MeshOf(WaygateDesign design)
        {
            if (_meshes.TryGetValue(design, out glModel? model)) return model;
            WayMesh mesh = WaygateForge.Way(design);
            int count = mesh.Positions.Count;
            float[] positions = new float[count * 3], colours = new float[count * 3], normals = new float[count * 3], tangents = new float[count * 3];
            float[] uvs = new float[count * 2], materials = new float[count];
            for (int i = 0; i < count; i++)
            {
                positions[i * 3] = mesh.Positions[i].X; positions[i * 3 + 1] = mesh.Positions[i].Y; positions[i * 3 + 2] = mesh.Positions[i].Z;
                colours[i * 3] = mesh.Colours[i].X; colours[i * 3 + 1] = mesh.Colours[i].Y; colours[i * 3 + 2] = mesh.Colours[i].Z;
                normals[i * 3] = mesh.Normals[i].X; normals[i * 3 + 1] = mesh.Normals[i].Y; normals[i * 3 + 2] = mesh.Normals[i].Z;
                tangents[i * 3] = 1f;
                uvs[i * 2] = mesh.UVs[i].X; uvs[i * 2 + 1] = mesh.UVs[i].Y;
            }
            model = glLoader.loadToVAO(positions, colours, normals, tangents, uvs, materials, mesh.Indices.ToArray());
            _meshes.Add(design, model);
            return model;
        }

        // WayLook as Way/Way.frag reads it.
        private Vector4[] Packed(WayState state, WayLook look)
        {
            if (_packed.TryGetValue(state, out Vector4[]? packed)) return packed;
            packed = new Vector4[WayRenderer.LookLength];
            packed[0] = new Vector4(look.Rim, look.Arms);
            packed[1] = new Vector4(look.Deep, look.Twist);
            packed[2] = new Vector4(look.Core, look.Spin);
            packed[3] = new Vector4(look.Accent, look.Flow);
            packed[4] = new Vector4(look.SkyHigh, look.Sharpness);
            packed[5] = new Vector4(look.SkyLow, look.Mist);
            packed[6] = new Vector4(look.Sun, look.Unrest);
            packed[7] = new Vector4(look.Haze, look.Seed);
            packed[8] = new Vector4(look.Near, look.SunHeight);
            packed[9] = new Vector4(look.Middle, look.SunAcross);
            packed[10] = new Vector4(look.Far, look.Sea);
            packed[11] = new Vector4(look.Rugged, look.Height, look.Terraces, look.Snow);
            _packed[state] = packed;
            return packed;
        }

        private static float Smooth(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
