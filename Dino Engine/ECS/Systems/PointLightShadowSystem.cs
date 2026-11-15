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
    /// Shares out the cube shadow maps. Every light that can be seen holds one, up to <see cref="LightingSettings.StillShadowedPointLights"/>, the nearest to the hero first:
    /// lending them only to the few nearest made the shadows of a square full of lanterns come and go as the hero walked, each lantern lighting straight through the stone
    /// whenever it wasn't one of them. What's drawn into a map is what stands, which doesn't move, so a map is drawn once and kept until what's round its light changes;
    /// only the nearest (<see cref="LightingSettings.ShadowedPointLights"/>) are drawn every frame, with whoever's walking about in them. A light gaining or giving up its map
    /// fades, so nothing pops. Maps are made as they're first needed and kept.
    /// </summary>
    public class PointLightShadowSystem : SystemBase
    {
        private const int Resolution = 512;
        private const float FadePerSecond = 3f;

        // Maps beyond the budget, for lights still fading out while the ones taking over fade in.
        private const int SpareMaps = 4;

        // Kept maps drawn in a frame at most, and kept maps whose casters are looked over each frame for a change: entering the Haven
        // would otherwise draw two dozen cube maps in one frame.
        private const int StillDrawsPerFrame = 3;
        private const int ChecksPerFrame = 2;

        /// <summary>A map on loan, and what's in it.</summary>
        private sealed class Loan
        {
            public Shadow Map;
            public bool Drawn;          // Whether anything has been drawn into it yet: until then the light has no shadow to fade in.
            public bool Moving;         // Whether it was last drawn with the characters in it, which a kept map mustn't keep.
            public bool Stale;          // Whether what stands round its light has changed since it was drawn.
            public int Signature;       // What stood round its light when it was drawn (LocalShadowCasters.Signature).
            public readonly bool[] FaceHadCharacters = new bool[6];  // Which faces were last drawn with someone in them.
        }

        private static readonly Vector3[] Directions = { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ };
        private static readonly Vector3[] Ups = { -Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitY, -Vector3.UnitY };

        private readonly CubeShadowArray _maps = new(Resolution);
        private readonly LocalShadowCasters _casters = new();
        private readonly List<Shadow> _freeMaps = new();
        private readonly Dictionary<Entity, Loan> _lentMaps = new();
        private readonly List<Entity> _gone = new();
        private readonly List<(float distance, Entity light)> _candidates = new();
        private readonly HashSet<Entity> _chosen = new();
        private readonly HashSet<Entity> _moving = new();
        private readonly Queue<Entity> _toCheck = new();
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
            CheckKeptMaps(world);

            int stillDraws = StillDrawsPerFrame;
            foreach (Archetype archetype in world.QueryArchetypes(WithMask, WithoutMask))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var shadow = entity.Get<PointLightShadowComponent>();
                    bool chosen = _chosen.Contains(entity.Entity);

                    if (chosen && shadow.Shadow == null) shadow.Shadow = BorrowMap(entity.Entity)?.Map;
                    Loan? loan = shadow.Shadow != null ? _lentMaps[entity.Entity] : null;

                    if (loan != null && chosen)
                    {
                        // The nearest are drawn every frame with whoever's about; the rest are drawn once, without them, and kept.
                        bool moving = _moving.Contains(entity.Entity);
                        if (moving || ((!loan.Drawn || loan.Moving || loan.Stale) && stillDraws > 0))
                        {
                            if (!moving) stillDraws--;
                            Vector3 position = entity.Get<LocalToWorldMatrixComponent>().value.ExtractTranslation();
                            // Drawn as it was and with what stands unchanged, a face nobody is in, or was in, is already right.
                            bool whole = !loan.Drawn || loan.Stale;
                            loan.Signature = RenderMap(loan, position, entity.Get<LightRangeComponent>().Radius, moving, whole, world);
                            loan.Drawn = true;
                            loan.Moving = moving;
                            loan.Stale = false;
                        }
                    }

                    bool shown = chosen && loan != null && loan.Drawn;
                    shadow.Strength = loan == null || !loan.Drawn ? 0f : Math.Clamp(shadow.Strength + (shown ? FadePerSecond : -FadePerSecond) * deltaTime, 0f, 1f);
                    if (!chosen && shadow.Strength <= 0f && loan != null)
                    {
                        _lentMaps.Remove(entity.Entity);
                        _freeMaps.Add(loan.Map);
                        shadow.Shadow = null;
                    }
                    entity.Set(shadow);
                }
            }
        }

        // A kept map is right until what stands round its light changes, which is rare, so a couple of them are looked over each frame in turn.
        private void CheckKeptMaps(ECSWorld world)
        {
            if (_toCheck.Count == 0) foreach (Entity light in _lentMaps.Keys) _toCheck.Enqueue(light);
            for (int i = 0; i < ChecksPerFrame && _toCheck.Count > 0; i++)
            {
                Entity light = _toCheck.Dequeue();
                if (!_lentMaps.TryGetValue(light, out Loan? loan) || !loan.Drawn || loan.Moving || loan.Stale) continue;
                Vector3 position = world.GetComponent<LocalToWorldMatrixComponent>(light).value.ExtractTranslation();
                _casters.Gather(world, position, MathF.Min(world.GetComponent<LightRangeComponent>(light).Radius, PointLightRenderer.ShadowFarPlane), characters: false);
                if (_casters.Signature != loan.Signature) loan.Stale = true;
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
                _freeMaps.Add(_lentMaps[light].Map);
                _lentMaps.Remove(light);
            }
        }

        private void ChooseLights(ECSWorld world)
        {
            _chosen.Clear();
            _moving.Clear();
            LightingSettings lighting = Engine.RenderEngine.Lighting;
            if (!lighting.LocalLights || Budget(lighting) <= 0 || !world.Camera.IsValid()) return;

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
            for (int i = 0; i < _candidates.Count && i < Budget(lighting); i++) _chosen.Add(_candidates[i].light);
            for (int i = 0; i < _candidates.Count && i < lighting.ShadowedPointLights; i++) _moving.Add(_candidates[i].light);
            Engine.PerformanceMonitor.Count("Point lights that could cast shadows", _candidates.Count);
        }

        private static int Budget(LightingSettings lighting) => Math.Max(lighting.ShadowedPointLights, lighting.StillShadowedPointLights);

        private Loan? BorrowMap(Entity light)
        {
            Shadow map;
            if (_freeMaps.Count > 0)
            {
                map = _freeMaps[^1];
                _freeMaps.RemoveAt(_freeMaps.Count - 1);
            }
            else if (_mapsMade < Budget(Engine.RenderEngine.Lighting) + SpareMaps)
            {
                map = MakeMap();
                _mapsMade++;
            }
            else
            {
                return null;
            }
            var loan = new Loan { Map = map };
            _lentMaps[light] = loan;
            return loan;
        }

        // Every map is a cube of one array, so that the lights are all lit in one pass (PointLightRenderer). It grows by doubling, up to what the budget allows.
        private Shadow MakeMap()
        {
            int wanted = _mapsMade + 1;
            if (wanted > _maps.Capacity) _maps.EnsureCapacity(Math.Max(wanted, Math.Min(Math.Max(_maps.Capacity * 2, 8), Budget(Engine.RenderEngine.Lighting) + SpareMaps)));

            // A cube map's six faces each see 90 degrees.
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(90f), 1f, PointLightRenderer.ShadowNearPlane, PointLightRenderer.ShadowFarPlane);
            return new Shadow
            {
                shadowFrameBuffer = FrameBuffer.ForCube(_maps, _mapsMade),
                shadowProjectionMatrix = projection,
                lightViewMatrix = Matrix4.Identity,
                polygonOffsetModel = 2f,
                polygonOffsetTerrain = 2f,
                isCubeMap = true,
                cubemapFaceIndex = 0,
            };
        }

        /// <summary>
        /// Draws what stands round a light into its map, and whoever's about if <paramref name="characters"/>. Returns what stood there. Unless <paramref name="whole"/>,
        /// only the faces someone is in now or was in when it was last drawn are drawn again, as the rest still hold just what stands, unless that's changed.
        /// </summary>
        private int RenderMap(Loan loan, Vector3 position, float radius, bool characters, bool whole, ECSWorld world)
        {
            Shadow map = loan.Map;
            _casters.Gather(world, position, MathF.Min(radius, PointLightRenderer.ShadowFarPlane), characters);
            if (_casters.Signature != loan.Signature) whole = true;

            for (int face = 0; face < 6; face++)
            {
                // Every face, up included: a light under an arch, an eave or a porch roof would otherwise light the wall and roof over it straight through the stone, in
                // a wedge as wide as the face, and the wall and roof over a light are exactly what the camera looks down on.
                Shadow view = map;
                view.cubemapFaceIndex = face;
                view.lightViewMatrix = Matrix4.LookAt(position, position + Directions[face], Ups[face]);
                var frustum = new Frustum(view.lightViewMatrix * view.shadowProjectionMatrix);

                bool someone = characters && _casters.CharactersIn(frustum);
                if (!whole && !someone && !loan.FaceHadCharacters[face]) continue;
                loan.FaceHadCharacters[face] = someone;

                GL.DepthMask(true);
                map.shadowFrameBuffer.bindFace(TextureTarget.TextureCubeMapPositiveX + face);
                GL.Clear(ClearBufferMask.DepthBufferBit);
                _casters.Submit(frustum, view, characters);
            }
            return _casters.Signature;
        }
    }
}
