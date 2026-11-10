using Dino_Engine.Core;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.World;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Systems
{
    /// <summary>
    /// Opens the hall the focus is standing in, for the camera alone: tells every shader where the halls near it are and which of them is open (<see cref="HallShaderData"/>,
    /// Halls.glsl), fading it open as the focus steps in through a door and shut as it steps out, and draws what's inside a hall only while it can be seen. The focus is the point
    /// the game keeps on its hero (<see cref="ShaderGlobals.focusPoint"/>). The sun's shadows and global illumination always see every hall whole. Read HALLS.md.
    /// </summary>
    public class HallCutawaySystem : SystemBase
    {
        private const float OpenSeconds = 0.35f;
        private const float StepIn = -0.4f;     // Metres inside a hall's walls the focus has to be to open it,
        private const float StepOut = 1.1f;     // and how far outside them, through the doorway and out, before it shuts.
        private const float Near = 9f;          // Metres from a hall's walls that what's inside it is drawn, for what its doors and windows show.
        private const float InsideAdaptation = 1.7f;    // How much the eye opens up for a hall's room: a room is far darker than the day outside it.

        private WorldData? _world;
        private WorldHall? _open;
        private float _opened;
        private readonly List<(WorldHall Hall, glModel? Inside)> _halls = new();
        private readonly List<int> _nearest = new();

        public HallCutawaySystem() : base(new BitMask(typeof(WorldStreamSingleton))) { }

        protected override void UpdateEntity(EntityView entity, ECSWorld world, float deltaTime) { }

        /// <summary>The hall that's open, or opening, or nothing.</summary>
        public static WorldHall? Open { get; private set; }

        protected override void UpdateFrame(ECSWorld world, float deltaTime)
        {
            RenderEngine render = Engine.RenderEngine;
            if (!world.TryGetSingleton<WorldStreamSingleton>(out Entity streamEntity))
            {
                Forget();
                render.context.halls = HallShaderData.None;
                return;
            }
            WorldStreamSingleton stream = world.GetComponent<WorldStreamSingleton>(streamEntity);
            if (!ReferenceEquals(stream.World, _world)) Enter(stream);
            if (_halls.Count == 0)
            {
                render.context.halls = HallShaderData.None;
                return;
            }

            Vector4 focusPoint = render.context.focusPoint;
            bool focused = focusPoint.W > 0f;
            Vector2 focus = focused ? focusPoint.Xz : render.context.viewPos.Xz;
            WorldHall? standing = focused ? Standing(focus) : null;

            // One hall open at a time: the one being left shuts before the next opens.
            float step = deltaTime / OpenSeconds;
            if (_open != null && standing == _open) _opened = MathF.Min(_opened + step, 1f);
            else _opened = MathF.Max(_opened - step, 0f);
            if (_opened <= 0f) _open = standing;
            Open = _opened > 0f ? _open : null;

            // The nearest of them go to the shaders, which is all of them anywhere but somewhere very large.
            _nearest.Clear();
            for (int i = 0; i < _halls.Count; i++) _nearest.Add(i);
            if (_nearest.Count > HallShaderData.Most) _nearest.Sort((a, b) => Distance(_halls[a].Hall, focus).CompareTo(Distance(_halls[b].Hall, focus)));
            var data = new HallShaderData();
            int count = Math.Min(_nearest.Count, HallShaderData.Most);
            int openIndex = -1;
            for (int n = 0; n < count; n++)
            {
                WorldHall hall = _halls[_nearest[n]].Hall;
                HallDesign d = hall.Design;
                data.Set(n,
                    new Vector4(hall.Centre.X, hall.Centre.Y, d.Half.X, d.Half.Y),
                    new Vector4(MathF.Cos(hall.Yaw), MathF.Sin(hall.Yaw), hall.Floor, d.WallTop),
                    new Vector4(d.Wall, d.FrontCut, d.Eaves, d.FrontReach));
                if (hall == _open) openIndex = n;
            }
            data.Cutaway = new Vector4(openIndex, openIndex >= 0 ? Smooth(_opened) : 0f, count, 0f);
            render.context.halls = data;
            render.Adaptation = MathHelper.Lerp(1f, InsideAdaptation, Smooth(_opened));

            // What's inside a hall costs as much to draw under its roof as with the roof off, so it's drawn only while it can be seen.
            foreach ((WorldHall hall, glModel? inside) in _halls)
            {
                if (inside == null) continue;
                bool seen = (hall == _open && _opened > 0f) || (focused && Distance(hall, focus) < Near);
                inside.Hidden = !seen;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // The hall the focus stands in: the one already open for as long as it's anywhere near inside it, or any other it's well inside.
        private WorldHall? Standing(Vector2 focus)
        {
            if (_open != null && _open.Contains(focus, StepOut)) return _open;
            foreach ((WorldHall hall, _) in _halls)
            {
                if (hall.Contains(focus, StepIn)) return hall;
            }
            return null;
        }

        // Metres from a point to the outside of a hall's walls, 0 inside.
        private static float Distance(WorldHall hall, Vector2 point)
        {
            Vector2 local = hall.ToLocal(point);
            Vector2 outside = new Vector2(MathF.Max(MathF.Abs(local.X) - hall.Design.Half.X, 0f), MathF.Max(MathF.Abs(local.Y) - hall.Design.Half.Y, 0f));
            return outside.Length;
        }

        private void Enter(WorldStreamSingleton stream)
        {
            Forget();
            _world = stream.World;
            List<string> ids = stream.World.Objects.AssetIds;
            foreach (WorldHall hall in stream.World.Halls)
            {
                int index = ids.IndexOf(hall.Design.InsideAsset);
                _halls.Add((hall, index >= 0 ? stream.ObjectModels[index] : null));
            }
        }

        // A world left behind leaves nothing hidden: its models are kept for the next time.
        private void Forget()
        {
            foreach ((_, glModel? inside) in _halls)
            {
                if (inside != null) inside.Hidden = false;
            }
            _halls.Clear();
            _world = null;
            _open = null;
            _opened = 0f;
            Open = null;
            Engine.RenderEngine.Adaptation = 1f;
        }
    }
}
