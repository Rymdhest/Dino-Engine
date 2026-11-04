using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Physics;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers.Lighting;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Shares out the cube shadow maps. A shadow map is what makes a light expensive, and a village has dozens of lights that
    /// could cast one, so only the nearest to the hero that can be seen hold a map (<see cref="LightingSettings.ShadowedPointLights"/>).
    /// A light gaining or giving up its map fades, so nothing pops. Maps are made as they're first needed and kept.
    /// </summary>
    public class PointLightShadowSystem : SystemBase
    {
        private const int Resolution = 512;
        private const float FadePerSecond = 3f;
        private const int FaceUp = 2;

        // Maps beyond the budget, for lights still fading out while the ones taking over fade in.
        private const int SpareMaps = 2;

        private static readonly Vector3[] Directions = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
        private static readonly Vector3[] Ups = { -Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitY, -Vector3.UnitY };

        private readonly LocalShadowCasters _casters = new();
        private readonly List<Shadow> _freeMaps = new();
        private readonly Dictionary<Entity, Shadow> _lentMaps = new();
        private readonly List<Entity> _gone = new();
        private readonly List<(float distance, Entity light)> _candidates = new();
        private readonly HashSet<Entity> _chosen = new();
        private int _mapsMade;

        public PointLightShadowSystem()
            : base(new BitMask(
                typeof(PointLightTag),
                typeof(LocalToWorldMatrixComponent),
                typeof(LightRangeComponent),
                typeof(PointLightShadowComponent)))
        {
        }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            ReclaimMaps(world);
            ChooseLights(world);

            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var shadow = entity.Get<PointLightShadowComponent>();
                    bool chosen = _chosen.Contains(entity.Entity);

                    if (chosen && shadow.Shadow == null) shadow.Shadow = BorrowMap(entity.Entity);
                    shadow.Strength = shadow.Shadow == null ? 0f : Math.Clamp(shadow.Strength + (chosen ? FadePerSecond : -FadePerSecond) * deltaTime, 0f, 1f);
                    if (!chosen && shadow.Strength <= 0f && shadow.Shadow != null)
                    {
                        _lentMaps.Remove(entity.Entity);
                        _freeMaps.Add(shadow.Shadow.Value);
                        shadow.Shadow = null;
                    }
                    entity.Set(shadow);

                    if (shadow.Shadow != null)
                    {
                        Vector3 position = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
                        RenderMap(shadow.Shadow.Value, position, entity.Get<LightRangeComponent>().Radius, world);
                    }
                }
            }
        }

        // A light can be destroyed holding a map, when its sector streams out. Its id may already belong to something else by
        // then, so what counts is whether whatever has that id now still says it has a map.
        private void ReclaimMaps(ECSWorld world)
        {
            _gone.Clear();
            foreach (Entity light in _lentMaps.Keys)
            {
                bool holding = world.TryGetEntityLocation(light, out _, out _)
                    && world.HasComponent<PointLightShadowComponent>(light)
                    && world.GetComponent<PointLightShadowComponent>(light).Shadow != null;
                if (!holding) _gone.Add(light);
            }
            foreach (Entity light in _gone)
            {
                _freeMaps.Add(_lentMaps[light]);
                _lentMaps.Remove(light);
            }
        }

        private void ChooseLights(ECSWorld world)
        {
            _chosen.Clear();
            LightingSettings lighting = Engine.RenderEngine.Lighting;
            if (!lighting.LocalLights || lighting.ShadowedPointLights <= 0 || !world.Camera.IsValid()) return;

            ref ShaderGlobals context = ref Engine.RenderEngine.context;
            Vector3 focus = context.focusPoint.W > 0f ? context.focusPoint.Xyz : context.viewPos;
            var frustum = new Frustum(context.viewMatrix * context.projectionMatrix);

            _candidates.Clear();
            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    Vector3 position = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
                    float distance = (position - focus).Length;
                    if (distance > lighting.ShadowDistance) continue;

                    Vector3 reach = new Vector3(entity.Get<LightRangeComponent>().Radius);
                    if (frustum.IntersectsAABB(new AABB(position - reach, position + reach)) == IntersectionResult.Outside) continue;
                    _candidates.Add((distance, entity.Entity));
                }
            }

            _candidates.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < _candidates.Count && i < lighting.ShadowedPointLights; i++) _chosen.Add(_candidates[i].light);
        }

        private Shadow? BorrowMap(Entity light)
        {
            Shadow map;
            if (_freeMaps.Count > 0)
            {
                map = _freeMaps[^1];
                _freeMaps.RemoveAt(_freeMaps.Count - 1);
            }
            else if (_mapsMade < Engine.RenderEngine.Lighting.ShadowedPointLights + SpareMaps)
            {
                map = MakeMap();
                _mapsMade++;
            }
            else
            {
                return null;
            }
            _lentMaps[light] = map;
            return map;
        }

        private static Shadow MakeMap()
        {
            // A cube map's six faces each see 90 degrees.
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90f), 1f, PointLightRenderer.ShadowNearPlane, PointLightRenderer.ShadowFarPlane);
            return new Shadow(Resolution, projection, 0, 2f, 2f, true);
        }

        private void RenderMap(Shadow map, Vector3 position, float radius, ECSWorld world)
        {
            _casters.Gather(world, position, MathF.Min(radius, PointLightRenderer.ShadowFarPlane));

            for (int face = 0; face < 6; face++)
            {
                GL.DepthMask(true);
                map.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + face);
                GL.Clear(ClearBufferMask.DepthBufferBit);

                // What's above a light only shadows what's above that again, and nobody sees the underside of a roof from here.
                if (face == FaceUp) continue;

                Shadow view = map;
                view.cubemapFaceIndex = face;
                view.lightViewMatrix = Matrix4.LookAt(position, position + Directions[face], Ups[face]);
                _casters.Submit(new Frustum(view.lightViewMatrix * view.shadowProjectionMatrix), view);
            }
        }
    }
}
