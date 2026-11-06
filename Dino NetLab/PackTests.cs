using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Networking;
using Dino_Engine.World;
using Dino_Engine.World.Generation;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    /// <summary>What makes a band of enemies a pack rather than a number of enemies standing near each other, done for real on a running server.</summary>
    internal static class PackTests
    {
        private const float Distant = 34f;      // Further than anyone notices a hero by themselves.

        public static void Run(Report report, WorldCache worlds)
        {
            var request = new WorldRequest(303, "isle", 6);
            OneBody(report, worlds, request);
            Crowding(report, worlds, request);
            Waking(report, worlds, request);
            Keeping(report, worlds, request);
            Growing(report);
        }

        private static List<Actor> Members(GameLab lab, SiteState state)
        {
            var found = new List<Actor>();
            foreach (Entity entity in state.Live)
            {
                EntityView view = lab.Server.Ecs.GetEntityView(entity);
                if (view.Has<ActorComponent>()) found.Add(view.Get<ActorComponent>().Actor);
            }
            return found;
        }

        private static EnemyState StateOf(GameLab lab, Actor actor) => lab.Server.Ecs.GetEntityView(actor.Entity).Get<EnemyComponent>().State;

        // A pack of a good size well out in the open, and somewhere to stand that's out of every creature's sight.
        private static (SiteState pack, Vector2 spot) Lookout(ServerSession session, float away)
        {
            var hostiles = session.Sites.SelectMany(s => s.Site.Spawns.Where(p => p.Role.IsHostile()).Select(p => p.Position)).ToList();
            foreach (SiteState state in session.Sites.Where(s => s.Wild && s.HostilesTotal >= 5 && s.Site.Haunt != HauntKind.Camp).OrderBy(s => s.Site.Remoteness))
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector2 spot = state.Site.Position + new Vector2(MathF.Cos(i * 0.39f), MathF.Sin(i * 0.39f)) * away;
                    bool clear = away < Distant ? hostiles.All(h => (h - state.Site.Position).Length < 16f || (h - spot).Length > Distant) : hostiles.All(h => (h - spot).Length > Distant);
                    if (session.World.Passable.IsSet(spot) && clear) return (state, spot);
                }
            }
            throw new InvalidOperationException("Nowhere to watch a pack from.");
        }

        private static void OneBody(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, GameLab.StrongHero());
            ServerSession session = arena.Lab.Server.Session!;
            (SiteState pack, Vector2 spot) = Lookout(session, 42f);
            arena.Player.Client.TravelTo(spot);
            arena.Lab.Run(2);

            List<Actor> members = Members(arena.Lab, pack);
            report.Check("A pack stands waiting for someone out of its sight", members.Count >= 5 && members.All(m => StateOf(arena.Lab, m) == EnemyState.Idle), $"{members.Count} there");

            SiteState neighbour = session.Sites.Where(s => s.Wild && s != pack && s.Active && s.HostilesTotal > 0).OrderBy(s => (s.Site.Position - pack.Site.Position).LengthSquared).First();
            float gap = (neighbour.Site.Position - pack.Site.Position).Length;

            // One is shot, wherever it stands. It's the only one the blow reached.
            members[^1].Alarm = EnemyBrainSystem.StruckAlarm;
            arena.Lab.Run(0.4);

            int chasing = members.Count(m => StateOf(arena.Lab, m) != EnemyState.Idle);
            report.Check("Struck, one of a pack brings all of it", chasing == members.Count, $"{chasing} of {members.Count}");
            report.Check("And the next pack over hears nothing", Members(arena.Lab, neighbour).All(m => StateOf(arena.Lab, m) == EnemyState.Idle && m.Alarm == 0), $"{gap:0} m away");
            report.Note($"The next pack stood {gap:0} m away");
        }

        private static void Crowding(Report report, WorldCache worlds, WorldRequest request)
        {
            HeroState hero = GameLab.StrongHero();
            hero.Equipped[WornSlot.Neck].Lines.Add(new ItemLine("health", 60000f));
            using var arena = new Arena(worlds, request, hero);
            ServerSession session = arena.Lab.Server.Session!;
            (SiteState pack, Vector2 spot) = Lookout(session, 12f);
            arena.Player.Client.TravelTo(spot);
            arena.Lab.Run(9);

            List<Actor> near = Members(arena.Lab, pack).Where(m => (m.Position - arena.Truth.Sim.Position).Length < 12f).ToList();
            float total = 0f;
            float closest = float.PositiveInfinity;
            foreach (Actor one in near)
            {
                float nearest = near.Where(other => other != one).Min(other => (other.Position - one.Position).Length);
                total += nearest;
                closest = MathF.Min(closest, nearest);
            }
            float average = near.Count > 0 ? total / near.Count : 0f;
            report.Check("A pack on a hero stands around them, not in each other", near.Count >= 4 && average > 1.6f && closest > 0.7f, $"{near.Count} near, {average:0.0} m apart on average, closest {closest:0.0}");
            report.Note($"{near.Count} around the hero, {average:0.0} m apart on average, the closest two {closest:0.0} m");
        }

        private static void Waking(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, GameLab.StrongHero());
            ServerSession session = arena.Lab.Server.Session!;

            // Where there's the most to wake, or a limit has nothing to do.
            SiteState far = session.Sites.Where(s => s.Wild && s.Site.Remoteness > 0.4f).OrderByDescending(s => session.Sites.Count(o => (o.Site.Position - s.Site.Position).Length < 95f)).First();
            int waiting = session.Sites.Count(s => (s.Site.Position - far.Site.Position).Length < 95f);

            bool[] was = session.Sites.Select(s => s.Active).ToArray();
            int mostAtOnce = 0;
            arena.Player.Client.TravelTo(far.Site.Position);
            arena.Lab.Run(4, () =>
            {
                int woken = 0;
                for (int i = 0; i < was.Length; i++)
                {
                    if (session.Sites[i].Active && !was[i]) woken++;
                    was[i] = session.Sites[i].Active;
                }
                mostAtOnce = Math.Max(mostAtOnce, woken);
            });
            report.Check("Arriving somewhere wakes it a few packs at a time", waiting > 4 && mostAtOnce is > 0 and <= 4, $"{mostAtOnce} of {waiting} in one go");
            report.Check("And soon enough it's all there", session.Sites.Where(s => (s.Site.Position - far.Site.Position).Length < 90f).All(s => s.Active));

            arena.Player.Client.TravelTo(session.World.Sites[0].Position);
            arena.Lab.Run(3);
            report.Check("What's left behind goes back to sleep", !far.Active && far.Live.Count == 0);
        }

        private static void Keeping(Report report, WorldCache worlds, WorldRequest request)
        {
            using var arena = new Arena(worlds, request, GameLab.StrongHero());
            ServerSession session = arena.Lab.Server.Session!;
            SiteState pack = session.Sites.Where(s => s.Wild && !s.Active && s.HostilesTotal >= 3).OrderByDescending(s => s.Site.Remoteness).First();
            int index = session.Sites.IndexOf(pack);

            pack.Dead.Add(1);
            arena.Player.Client.TravelTo(pack.Site.Position);
            arena.Lab.Run(2.5);
            report.Check("The dead of a pack stay dead when it wakes again", pack.Active && Members(arena.Lab, pack).Count == pack.HostilesTotal - 1, $"{Members(arena.Lab, pack).Count} of {pack.HostilesTotal}");

            var save = new SaveGame();
            session.Capture(save);
            SiteRecord kept = save.Sites.First(s => s.Index == index);

            using var again = new Arena(worlds, request, GameLab.StrongHero());
            ServerSession restored = again.Lab.Server.Session!;
            restored.Restore(save);
            report.Check("A pack's dead are saved with the world", restored.Sites[index].Dead.SetEquals(kept.Dead) && kept.Dead.Contains(1));

            using var older = new Arena(worlds, request, GameLab.StrongHero());
            ServerSession old = older.Lab.Server.Session!;
            save.Version = 2;
            old.Restore(save);
            report.Check("A save from before packs doesn't mark the wrong ones", old.Sites[index].Dead.Count == 0);

            SiteState gone = session.Sites.Where(s => s.Wild && !s.Active && s != pack).OrderByDescending(s => s.Site.Remoteness).First();
            gone.Cleared = true;
            arena.Player.Client.TravelTo(gone.Site.Position);
            arena.Lab.Run(2.5);
            report.Check("A place set free stays free, whatever a newer build would put there", gone.Active && Members(arena.Lab, gone).Count == 0);

            // Everything anyone has done in a world goes to someone joining it, and a world now has a great many places to have done it in.
            int dead = 0;
            foreach (SiteState state in session.Sites.Where(s => s.Wild && !s.Active))
            {
                state.Dead.Add(0);
                dead++;
            }
            LabPlayer late = arena.Lab.AddPlayer("Late", new NetworkConditions { Latency = 60 }, 9);
            late.Fights = false;
            arena.Lab.Run(4);
            int heard = late.Client.Session?.Sites.Count(s => s.Wild && s.Dead.Contains(0)) ?? 0;
            report.Check("Someone joining late hears about all of it", heard >= dead && dead > 100, $"{heard} of {dead}");

            var flasks = HeroState.CreateNew();
            flasks.Flasks = 0;
            int filled = 0;
            for (int i = 0; i < HeroState.KillsPerFlask * 2; i++) filled += flasks.ChargeFlask(1f) ? 1 : 0;
            report.Check("Kills fill flasks", filled == 2 && flasks.Flasks == 2);
            flasks.Flasks = HeroState.MaxFlasks;
            report.Check("But no further than a hero can carry", !flasks.ChargeFlask(100f) && flasks.Flasks == HeroState.MaxFlasks);
        }

        private static void Growing(Report report)
        {
            report.Check("Packs are small at first", PackStage.UsualSize(1, 0f) <= 3f);
            report.Check("And are clearly bigger a few worlds on", PackStage.UsualSize(4, 0.5f) >= PackStage.UsualSize(1, 0.5f) + 2.5f);
            report.Check("They keep growing", PackStage.UsualSize(30, 0.5f) > PackStage.UsualSize(10, 0.5f) + 1.5f);
            report.Check("And the far end of a world is worse than the near", PackStage.UsualSize(5, 1f) > PackStage.UsualSize(5, 0f));
            report.Check("With more of them about", PackStage.Spacing(30) < PackStage.Spacing(1));
        }
    }
}
