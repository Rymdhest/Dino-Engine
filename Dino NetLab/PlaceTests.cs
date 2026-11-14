using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Net;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>
    /// What a place with a story does once it's freed (PLACES.md), on a running server with a client on the other end of a bad line: a prison's beasts stand in their cages while it's held and
    /// its doors are shut, and once its holders are down every door swings open and every beast walks out and is gone; a rearing farm's grazers wander their paddocks, never out of them, and go
    /// the same way once it's freed; the land's people come back to both; and a trading post's trader opens up. Each land is found by growing lands until one has the place, so no seed is kept.
    /// </summary>
    internal static class PlaceTests
    {
        private static readonly NetworkConditions Line = new NetworkConditions { Latency = 40, Jitter = 8, LossChance = 0.01 };

        public static int Run(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            Caged(report, worlds, SiteKind.Prison, "A prison");
            Caged(report, worlds, SiteKind.Farm, "A rearing farm");
            Trading(report, worlds);
            return report.Finish();
        }

        // The first land from seed 1 on that has a place of the kind.
        private static WorldRequest? Find(SiteKind kind)
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                var request = new WorldRequest(seed, null, 1);
                WorldData world = new WorldGenerator().Generate(request);
                if (world.Sites.Any(s => s.Kind == kind)) return request;
            }
            return null;
        }

        private sealed class Scene : IDisposable
        {
            public readonly GameLab Lab;
            public readonly LabPlayer Player;

            public Scene(WorldCache worlds, WorldRequest request, SiteKind kind)
            {
                Lab = new GameLab(worlds, request, kind.ToString());
                HeroState hero = GameLab.StrongHero();
                hero.Equipped[WornSlot.Neck].Lines[0].Value = 200000f;
                Player = Lab.AddPlayer("Hero", Line, 1, hero: hero);
                Player.Fights = false;
                Lab.Run(5);
            }

            public ServerSession Session => Lab.Server.Session!;
            public Actor Hero => Lab.Server.Heroes[0].Actor;

            public List<(Entity Entity, CaptiveComponent Captive, Vector2 At)> Captives()
            {
                var found = new List<(Entity, CaptiveComponent, Vector2)>();
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(CaptiveComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype)) found.Add((entity.Entity, entity.Get<CaptiveComponent>(), entity.Get<PositionComponent>().value.Xz));
                }
                return found;
            }

            public List<CageDoorComponent> Doors()
            {
                var found = new List<CageDoorComponent>();
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(CageDoorComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype)) found.Add(entity.Get<CageDoorComponent>());
                }
                return found;
            }

            public int Friendly()
            {
                int count = 0;
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(VillagerComponent)), BitMask.Empty)) count += archetype.EntityCount;
                return count;
            }

            // Whether anything of a kind to use stands within reach of a point: a land has merchants of its own elsewhere.
            public bool AnyNear(InteractKind kind, Vector2 near, float reach)
            {
                foreach (Archetype archetype in Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(InteractableComponent), typeof(PositionComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (entity.Get<InteractableComponent>().Kind == kind && (entity.Get<PositionComponent>().value.Xz - near).Length < reach) return true;
                    }
                }
                return false;
            }

            // How many ghosts this client has of a captive's look.
            public int ClientSees(IEnumerable<string> looks)
            {
                var wanted = new HashSet<string>(looks);
                int count = 0;
                foreach (Archetype archetype in Player.Ecs.QueryArchetypes(new BitMask(typeof(VisualComponent)), BitMask.Empty))
                {
                    foreach (EntityView entity in new ComponentAccessor(archetype))
                    {
                        if (wanted.Contains(entity.Get<VisualComponent>().Model)) count++;
                    }
                }
                return count;
            }

            public List<Actor> Members(SiteState state)
            {
                var found = new List<Actor>();
                foreach (Entity entity in state.Live)
                {
                    if (!Lab.Server.Ecs.TryGetEntityLocation(entity, out _, out _)) continue;
                    EntityView view = Lab.Server.Ecs.GetEntityView(entity);
                    if (view.Has<ActorComponent>()) found.Add(view.Get<ActorComponent>().Actor);
                }
                return found;
            }

            public void Clear(SiteState state)
            {
                for (int attempt = 0; attempt < 80 && !state.Cleared; attempt++)
                {
                    foreach (Actor actor in Members(state))
                    {
                        if (!actor.Dead) Session.Combat.Harm(Hero, actor, 1e12f, DamageType.Magic);
                    }
                    Lab.Run(0.3);
                }
            }

            public void Dispose() => Lab.Dispose();
        }

        private static void Caged(Report report, WorldCache worlds, SiteKind kind, string name)
        {
            report.Section(name + " freed");
            if (Find(kind) is not WorldRequest request)
            {
                report.Check($"There's {name.ToLowerInvariant()} in the first lands grown", false);
                return;
            }
            using var scene = new Scene(worlds, request, kind);
            SiteState state = scene.Session.Sites.First(s => s.Site.Kind == kind);
            PlacePlan plan = state.Site.Place!;
            int held = state.Site.Spawns.Count(s => s.Role == SpawnRole.Captive);
            scene.Player.Client.TravelTo(state.Site.Position);
            scene.Lab.Run(4);
            var before = scene.Captives().Where(c => c.Captive.SiteIndex == scene.Session.Sites.IndexOf(state)).ToList();
            report.Check("Every beast it holds stands there while it's held", held > 0 && before.Count == held, $"{before.Count} of {held}");
            report.Check("The client sees them", scene.ClientSees(plan.Cages.Select(c => c.Look)) >= held, $"{scene.ClientSees(plan.Cages.Select(c => c.Look))} of {held}");
            int doors = plan.Cages.Count(c => c.Door.Length > 0);
            report.Check("Every door and gate hangs, shut", doors > 0 && scene.Doors().Count(d => d.Open <= 0f) == doors, $"{scene.Doors().Count} of {doors}");
            if (kind == SiteKind.Farm)
            {
                scene.Lab.Run(25);
                var later = scene.Captives();
                bool moved = later.Any(l => before.Any(b => b.Entity.Equals(l.Entity) && (b.At - l.At).Length > 1f));
                bool inside = later.All(l => l.Captive.Paddock >= 0 && Inside(plan.Paddocks[l.Captive.Paddock], l.At, 0.5f));
                report.Check("Penned, they wander their paddocks", moved);
                report.Check("And never out of them", inside, string.Join(", ", later.Select(l => l.At)));
            }
            int people = scene.Friendly();
            scene.Clear(state);
            report.Check("Its holders can be cut down", state.Cleared);
            scene.Lab.Run(3);
            report.Check("Then every door and gate swings open", scene.Doors().All(d => d.Open >= 1f), string.Join(", ", scene.Doors().Select(d => d.Open.ToString("0.00"))));
            if (state.Site.Spawns.Any(s => s.Role == SpawnRole.Returning)) report.Check("And the land's people come back to it", scene.Friendly() > people, $"{scene.Friendly()}, from {people}");
            for (int i = 0; i < 70 && scene.Captives().Any(); i++) scene.Lab.Run(1);
            report.Check("Every beast walks out and is gone into the wild", !scene.Captives().Any(), $"{scene.Captives().Count} still there");
        }

        private static bool Inside(PlacePaddock pen, Vector2 at, float slack)
        {
            Vector2 offset = at - pen.Centre;
            Vector2 ax = new Vector2(MathF.Cos(pen.Yaw), -MathF.Sin(pen.Yaw)), az = new Vector2(MathF.Sin(pen.Yaw), MathF.Cos(pen.Yaw));
            return MathF.Abs(Vector2.Dot(offset, ax)) <= pen.Half.X + slack && MathF.Abs(Vector2.Dot(offset, az)) <= pen.Half.Y + slack;
        }

        private static void Trading(Report report, WorldCache worlds)
        {
            report.Section("A trading post freed");
            if (Find(SiteKind.Tradepost) is not WorldRequest request)
            {
                report.Check("There's a trading post in the first lands grown", false);
                return;
            }
            using var scene = new Scene(worlds, request, SiteKind.Tradepost);
            SiteState state = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Tradepost);
            scene.Player.Client.TravelTo(state.Site.Position);
            scene.Lab.Run(4);
            report.Check("It has a trader to come back", state.Site.Spawns.Any(s => s.Role == SpawnRole.Trader));
            report.Check("Nobody trades there while the turncoats hold it", !scene.AnyNear(InteractKind.Merchant, state.Site.Position, state.Site.Radius * 1.2f));
            scene.Clear(state);
            scene.Lab.Run(3);
            report.Check("Once it's freed its trader opens up", scene.AnyNear(InteractKind.Merchant, state.Site.Position, state.Site.Radius * 1.2f));
        }
    }
}
