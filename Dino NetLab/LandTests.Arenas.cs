using Dino_Defenders.Books;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.Components;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_NetLab
{
    internal static partial class LandTests
    {
        /// <summary>The arenas alone: the Pit's gates and waves, and every kind of arena (<c>--kind id</c> for one of them).</summary>
        public static int RunArenas(Options options)
        {
            var report = new Report();
            var worlds = new WorldCache();
            string? only = options.Text("kind");
            if (only == null) Arenas(report, worlds);
            if (only != "keeps") ArenaKinds(report, worlds, only);
            if (only == null || only == "keeps") KeepArenas(report, worlds);
            return report.Finish();
        }

        // The arena in a land's keep (ARENAS.md), one of each kind: its tyrant isn't there until someone goes down by the gate in the wall round it, and is once they do; down there nothing takes them
        // out, not calling the roads, a page that carries them or walking at its walls, and nobody's carried in from the keep's ground either; the way up stays shut while the tyrant lives, and
        // once it's down the grille in its gate rises and it takes them back up to the keep.
        private static void KeepArenas(Report report, WorldCache worlds)
        {
            report.Section("Arenas in keeps");
            var found = new Dictionary<ArenaKind, int>();
            for (int seed = 1; seed <= 60 && found.Count < Enum.GetValues<ArenaKind>().Length; seed++)
            {
                WorldData world = worlds.Get(new WorldRequest(seed, null, 1)).Result;
                if (world.Sites.FirstOrDefault(s => s.Kind == SiteKind.Keep)?.Arena is ArenaPlan plan && !found.ContainsKey(plan.Kind)) found[plan.Kind] = seed;
            }
            report.Check("Every kind of arena is a keep's somewhere", found.Count == Enum.GetValues<ArenaKind>().Length, string.Join(", ", found.Select(f => $"{f.Key} in {f.Value}")));

            foreach ((ArenaKind kind, int seed) in found.OrderBy(f => f.Key))
            {
                using var scene = new Scene(worlds, new WorldRequest(seed, null, 1), hero: Sturdy(1));
                SiteState keep = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Keep);
                ArenaPlan plan = keep.Site.Arena!;
                string name = $"A keep's {kind}";
                GameServer server = scene.Lab.Server;

                scene.Go(plan.Door);
                scene.Lab.Run(1.5);
                report.Check($"{name}: its tyrant isn't there while nobody's come down", keep.Active && !scene.Members(keep).Any(a => a.Tyrant), keep.Active ? "awake" : "asleep");

                // Carried at the floor from the keep's ground, nobody lands on it.
                scene.Session.Carry(scene.Hero, plan.Centre);
                scene.Lab.Run(0.4);
                report.Check($"{name}: nobody's carried onto its floor from the keep", !plan.Contains(scene.Truth.Sim.Position, 1.5f), $"at {(scene.Truth.Sim.Position - plan.Centre).Length:0.0} m from the middle");

                scene.Go(plan.Door);
                scene.Player.PressUse = true;
                scene.Lab.Run(2);
                bool down = plan.Contains(scene.Truth.Sim.Position, 1.5f);
                report.Check($"{name}: the gate in its wall takes whoever uses it down onto its floor", down, $"{(scene.Truth.Sim.Position - plan.Arrival).Length:0.0} m from where they're meant to arrive");
                if (!down) continue;
                scene.Lab.Run(1);
                Actor? tyrant = scene.Members(keep).FirstOrDefault(a => a.Tyrant);
                report.Check($"{name}: and its tyrant's there once someone is", tyrant != null && plan.Contains(tyrant.Position, 2f));
                if (tyrant == null) continue;
                Hold(scene, tyrant);

                Vector2 arrived = scene.Truth.Sim.Position;
                scene.Player.Client.CallRoads();
                scene.Lab.Run(Rulebook.CallRoads + 1.5f);
                report.Check($"{name}: calling the roads down there opens nothing", scene.Truth.RoadsOpenAt == null && scene.Truth.CallingSince == null && plan.Contains(scene.Truth.Sim.Position, 1.5f));

                // The way up isn't there to use while the tyrant stands: nothing in reach of where it would be.
                scene.Go(plan.Exit);
                scene.Player.PressUse = true;
                scene.Lab.Run(1.5);
                report.Check($"{name}: the way up stays shut while the tyrant lives", plan.Contains(scene.Truth.Sim.Position, 1.5f) && scene.Find(InteractKind.ArenaUp) == null);

                Sealed(report, scene, plan, name);

                scene.Session.Combat.Harm(scene.Hero, tyrant, 1e12f, DamageType.Magic);
                scene.Lab.Run(3);
                Vector2? up = scene.Find(InteractKind.ArenaUp);
                bool risen = plan.Way is ArenaEntry way && PiecesOf(server.Ecs, ArenaShape.Portcullis).Any(p => (p.at.Xz - way.Door.Xz).Length < 0.1f && p.at.Y > way.Door.Y + way.Lift * 0.9f);
                report.Check($"{name}: once the tyrant's down the grille in its gate rises, and the way up is there", tyrant.Dead && up != null && risen, $"{(tyrant.Dead ? "down" : "standing")}, {(up != null ? "a way up" : "no way up")}, {(risen ? "risen" : "not risen")}");
                scene.Go(plan.Exit);
                scene.Player.PressUse = true;
                scene.Lab.Run(2);
                report.Check($"{name}: and takes whoever uses it back up to the keep", !plan.Contains(scene.Truth.Sim.Position, 1.5f) && (scene.Truth.Sim.Position - plan.Door).Length < 6f, $"{(scene.Truth.Sim.Position - plan.Door).Length:0.0} m from the gate");
            }
        }

        // Every kind of arena, each on its own isle (ARENAS.md): its entries open as its tyrant weakens and let the brood in, whatever kind they are; what it does on its own is marked, lands on the tick
        // its mark says, on whoever's standing in it and on nobody who's stepped out of it, and does what it says; and nobody walks out of it, whichever way they go at its walls, nor is carried out.
        private static void ArenaKinds(Report report, WorldCache worlds, string? only = null)
        {
            report.Section("Every kind of arena");
            foreach (WorldArchetype archetype in ArenaCatalog.All)
            {
                if (only != null && archetype.Id != only) continue;
                var request = new WorldRequest(1, archetype.Id, 10);
                worlds.Get(request).Wait();
                using var scene = new Scene(worlds, request, hero: Sturdy(10));
                SiteState arena = scene.Session.Sites.First(s => s.Site.Kind == SiteKind.Arena);
                ArenaPlan plan = arena.Site.Arena!;
                string name = archetype.Name;
                Actor? tyrant = scene.Members(arena).FirstOrDefault(a => a.Tyrant);
                report.Check($"{name}: the hero arrives on its floor, and its tyrant is there", tyrant != null && plan.Contains(scene.Truth.Sim.Position), $"at {scene.Truth.Sim.Position.X:0},{scene.Truth.Sim.Position.Y:0}");
                if (tyrant == null) continue;

                // It stands and takes it, so that its own blows don't get in the way of what the arena does.
                Hold(scene, tyrant);
                ArenaPieces(report, scene, plan, name);
                ArenaWaves(report, scene, plan, tyrant, name);
                switch (plan.Kind)
                {
                    case ArenaKind.Pit:
                        NothingOfItsOwn(report, scene, name);
                        break;
                    case ArenaKind.Colosseum:
                        BoltThrowers(report, scene, plan, name);
                        break;
                    case ArenaKind.Cage:
                        Clutches(report, scene, plan, name);
                        break;
                    case ArenaKind.SunTemple:
                        Mirrors(report, scene, plan, tyrant, name);
                        break;
                    case ArenaKind.Onslaught:
                        Vents(report, scene, plan, name);
                        break;
                }
                Sealed(report, scene, plan, name);
            }
        }

        // ---------------------------------------------------------------- Helpers

        // A mark on the server that's the arena's own, of a shape and length, newer than one already seen.
        private static bool ArenaMark(ECSWorld ecs, ShapeKind kind, float length, uint after, out TelegraphComponent found, out Vector3 at)
        {
            foreach (Archetype archetype in ecs.QueryArchetypes(new BitMask(typeof(TelegraphComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    var mark = entity.Get<TelegraphComponent>();
                    if (mark.Kind != kind || !mark.Hostile || mark.Serial <= after || MathF.Abs(mark.Length - length) > 0.3f) continue;
                    found = mark;
                    at = entity.Get<PositionComponent>().value;
                    return true;
                }
            }
            found = default;
            at = default;
            return false;
        }

        private static float LengthOf(BookPlan book) => book.Pages[0].Length;

        // Whether any ground is doing something on the server.
        private static bool Burning(ECSWorld ecs) => ecs.QueryArchetypes(new BitMask(typeof(ZoneComponent)), BitMask.Empty).Any(archetype => archetype.EntityCount > 0);

        private static GroundShape ShapeOf(in TelegraphComponent mark, Vector3 at) => new GroundShape(mark.Kind, at.Xz, mark.Facing, mark.Length, mark.Width);

        // Stands something still where it is: still, it does nothing of its own, and with nothing going it doesn't slide on with whatever it was last doing.
        private static void Hold(Scene scene, Actor actor)
        {
            actor.Still = true;
            if (scene.Lab.Server.Ecs.TryGetEntityLocation(actor.Entity, out _, out _)) scene.Lab.Server.Ecs.GetEntityView(actor.Entity).Set(new VelocityComponent());
        }

        // What of a blow of some kind gets through to someone, before armour: what they resist and what they take more or less of.
        private static float Through(Actor actor, DamageType type) => (1f - MathF.Max(actor.Sheet.Resistance[(int)type], Hits.LeastResistance) / 100f) * actor.Sheet.Taken[(int)type] * (1f + actor.TakenIncrease);

        // Whether what something took was a share of the tyrant's health, as what's added is a range either side of what it says, and fire goes through as the tyrant takes it.
        private static bool AShare(float took, float share, Actor tyrant) => took >= share * Through(tyrant, DamageType.Fire) * (1f - Rulebook.AddedSpread) * 0.99f && took <= share * Through(tyrant, DamageType.Fire) * (1f + Rulebook.AddedSpread) * 1.01f;

        // All that stands between someone and falling: a tyrant can wear a Barrier, which takes a blow before its Health does.
        private static float Standing(Actor actor) => actor.Vitals.Health + actor.Vitals.Barrier;

        // Whether someone has blocked a blow on this very tick, which a tyrant whose gear says it can does now and then, whatever it was: a tick later it's been a sixtieth of a second.
        private static bool JustBlocked(Actor actor) => actor.SinceBlock < 0.01f;

        // Moves something of the server's to a place, as nothing walks a standing tyrant anywhere.
        private static void Put(Scene scene, Actor actor, Vector2 at)
        {
            float height = scene.Session.World.Height.Sample(at);
            actor.Position = at;
            actor.Height = height;
            if (scene.Lab.Server.Ecs.TryGetEntityLocation(actor.Entity, out _, out _)) scene.Lab.Server.Ecs.GetEntityView(actor.Entity).Set(new PositionComponent(new Vector3(at.X, height, at.Y)));
        }

        // The arena's own pieces on the server, by what they're grown from.
        private static List<(Entity entity, string model, Vector3 at)> PiecesOf(ECSWorld ecs, ArenaShape shape)
        {
            var found = new List<(Entity, string, Vector3)>();
            foreach (Archetype archetype in ecs.QueryArchetypes(new BitMask(typeof(VisualComponent), typeof(PositionComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    string model = entity.Get<VisualComponent>().Model;
                    if (ArenaSpec.TryParse(model, out ArenaSpec spec) && spec.Shape == shape) found.Add((entity.Entity, model, entity.Get<PositionComponent>().value));
                }
            }
            return found;
        }

        // Somewhere on the floor a few metres off to the side of a line, out of it.
        private static Vector2 Aside(Scene scene, ArenaPlan plan, Vector2 at, Vector2 way, float by)
        {
            Vector2 across = new Vector2(-way.Y, way.X);
            foreach (float side in new[] { 1f, -1f })
            {
                Vector2 spot = at + across * (by * side);
                if (plan.Contains(spot, -1.5f) && GameServer.TryFindGround(scene.Session.World, spot, out Vector2 ground) && plan.Contains(ground, -1f)) return ground;
            }
            return at + across * by;
        }

        // ---------------------------------------------------------------- What every kind is held to

        private static void ArenaPieces(Report report, Scene scene, ArenaPlan plan, string name)
        {
            ECSWorld ecs = scene.Lab.Server.Ecs;
            int throwers = plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Ballista), mirrors = plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Mirror);
            int chandeliers = plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Chandelier), clutches = plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Clutch);
            bool all = PiecesOf(ecs, ArenaShape.Ballista).Count == throwers && PiecesOf(ecs, ArenaShape.Mirror).Count == mirrors
                && PiecesOf(ecs, ArenaShape.Chandelier).Count == chandeliers && PiecesOf(ecs, ArenaShape.Clutch).Count == clutches;
            bool wanted = plan.Kind switch
            {
                ArenaKind.Colosseum => throwers > 0,
                ArenaKind.Cage => clutches > 0,
                ArenaKind.SunTemple => mirrors >= 2,
                ArenaKind.Onslaught => plan.Pieces.Any(p => p.Kind == ArenaPieceKind.Vent),
                ArenaKind.Cathedral => chandeliers > 0,
                _ => plan.Pieces.Count == 0,
            };
            report.Check($"{name}: everything of its own that moves is up, and it has what makes it what it is", all && wanted,
                $"{throwers} throwers, {mirrors} mirrors, {chandeliers} chandeliers, {clutches} clutches, {plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Vent)} vents");
        }

        // As the tyrant weakens the entries open, whatever they are, and the brood come through them. Where the ash heaves, it's marked first and they come up on its tick. In the Cathedral a
        // chandelier gives at the same time, and here it's let fall on the tyrant and the hero both.
        private static void ArenaWaves(Report report, Scene scene, ArenaPlan plan, Actor tyrant, string name)
        {
            GameServer server = scene.Lab.Server;
            Actor hero = scene.Hero;
            ArenaPiece? chandelier = null;
            if (plan.Kind == ArenaKind.Cathedral)
            {
                // Under the chandelier nearest the hero, which is the one that gives, with the tyrant beside them.
                Vector2 near = scene.Truth.Sim.Position;
                chandelier = plan.Pieces.Where(p => p.Kind == ArenaPieceKind.Chandelier).OrderBy(p => (p.At.Xz - near).LengthSquared).First();
                GameServer.TryFindGround(scene.Session.World, chandelier.Value.At.Xz, out Vector2 under);
                scene.Go(under);
                Put(scene, tyrant, under + new Vector2(1.2f, 0f));
                scene.Lab.Run(0.5);
            }

            hero.Vitals = Vitals.Full(hero.Sheet);
            float tyrantBefore = tyrant.Sheet.MaxHealth * (1f - Rulebook.ArenaWaveEvery - 0.03f);
            float heroBefore = hero.Vitals.Health;
            tyrant.Vitals.Health = MathF.Min(tyrant.Vitals.Health, tyrantBefore);
            tyrantBefore = Standing(tyrant);

            float tyrantAfter = tyrantBefore;
            bool burning = false, blocked = false;
            var heaves = new Dictionary<uint, uint>();
            var heroHurtOn = new List<uint>();
            var trace = new List<string>();
            float lastHero = heroBefore;
            Vector3 crashAt = default;
            uint cameAt = 0, crashMarked = 0, crashLands = 0, tyrantHurt = 0;
            bool rising = plan.Entries.Any(e => e.Kind == ArenaEntryKind.Rising);
            float seconds = (float)plan.Entries.Max(e => ArenaSystem.OpeningTime(e.Kind)) + 1.5f;
            if (chandelier != null) seconds = MathF.Max(seconds, Rulebook.ChandelierWindUp + 1f);
            scene.Lab.Run(seconds, () =>
            {
                uint tick = server.Tick - 1;
                if (rising && ArenaMark(server.Ecs, ShapeKind.Circle, LengthOf(Shelf.Heave), 0, out TelegraphComponent heave, out _)) heaves[heave.Serial] = heave.Lands;
                if (cameAt == 0 && scene.Session.Arrivals.Count > 0) cameAt = tick;
                if (chandelier != null && crashMarked == 0 && ArenaMark(server.Ecs, ShapeKind.Circle, LengthOf(Shelf.Crash), 0, out TelegraphComponent crash, out crashAt))
                {
                    crashMarked = crash.Start;
                    crashLands = crash.Lands;
                }
                if (hero.Vitals.Health < lastHero - 0.5f) heroHurtOn.Add(tick);
                lastHero = hero.Vitals.Health;
                if (tyrantHurt == 0 && Standing(tyrant) < tyrantBefore - 0.5f) tyrantHurt = tick;
                if (crashLands != 0 && tick + 2 >= crashLands && tick <= crashLands + 1) trace.Add($"{tick}: {tyrant.Vitals.Health:0} at {(tyrant.Position - crashAt.Xz).Length:0.0} m{(tyrant.Dead ? " dead" : "")}");
                if (crashLands != 0 && tick == crashLands)
                {
                    tyrantAfter = Standing(tyrant);
                    blocked = JustBlocked(tyrant);
                }
                if (crashLands != 0 && tick == crashLands + 2) burning = Burning(server.Ecs);
            });

            int expected = Math.Min(Rulebook.ArenaWaveGates, plan.Entries.Count) * Rulebook.ArenaWaveSize;
            string ways = string.Join(" and ", plan.Entries.Select(e => e.Kind).Distinct());
            report.Check($"{name}: as its tyrant weakens, its entries ({ways}) let more of the brood in", scene.Session.Arrivals.Count == expected, $"{scene.Session.Arrivals.Count} of {expected}");
            if (rising)
            {
                report.Check($"{name}: where the dead come up the ash is marked first, and they come on its tick", heaves.Count > 0 && heaves.Values.Min() == cameAt, $"marked to land on {string.Join(",", heaves.Values)}, came on {cameAt}");
            }
            if (chandelier != null)
            {
                float took = tyrantBefore - tyrantAfter;
                float share = Rulebook.ChandelierTyrantShare / 100f * tyrant.Sheet.MaxHealth;
                report.Check($"{name}: a chandelier gives as the tyrant weakens, marked under it for as long as it falls", crashMarked > 0 && crashLands - crashMarked == (uint)MathF.Round(Rulebook.ChandelierWindUp * Dino_Engine.Networking.NetTime.TicksPerSecond));
                report.Check($"{name}: and comes down on the tick its mark said, on the hero under it", heroHurtOn.Contains(crashLands), $"marked for {crashLands}, the hero hurt on {string.Join(",", heroHurtOn)}");
                report.Check($"{name}: and on the tyrant, taking its share of the tyrant's health, or blocked", blocked || AShare(took, share, tyrant), $"took {took:0} of {share:0} with {Through(tyrant, DamageType.Fire):0.00} of fire getting through, first hurt on {tyrantHurt}; {string.Join("; ", trace)}");
                Vector3 lying = PiecesOf(server.Ecs, ArenaShape.Chandelier).Select(p => p.at).OrderBy(p => (p.Xz - chandelier.Value.At.Xz).LengthSquared).First();
                float floor = scene.Session.World.Height.Sample(lying.Xz);
                report.Check($"{name}: where it lies on the floor after", lying.Y - floor < ArenaForge.ChandelierDrop + 0.5f && lying.Y > floor, $"its hook {lying.Y - floor:0.0} m up");
                report.Check($"{name}: burning", burning);
            }
            foreach (Actor brood in scene.Session.Arrivals.ToList()) scene.Session.Combat.Harm(hero, brood, 1e12f, DamageType.Magic);
            hero.Vitals = Vitals.Full(hero.Sheet);
            scene.Lab.Run(1);
        }

        // The plain fight: nothing marks the floor but the tyrant and its brood.
        private static void NothingOfItsOwn(Report report, Scene scene, string name)
        {
            bool any = false;
            scene.Lab.Run(Rulebook.HazardGrace + 6, () =>
            {
                foreach (Archetype archetype in scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(TelegraphComponent)), BitMask.Empty)) any |= archetype.EntityCount > 0;
            });
            report.Check($"{name}: does nothing of its own, being the plain fight", !any);
        }

        // ---------------------------------------------------------------- Each kind's own

        // A thrower is aimed at whoever's on the floor, and its bolt goes down the line marked on the tick it said: into a hero still standing in it, and past one who stepped out of it.
        private static void BoltThrowers(Report report, Scene scene, ArenaPlan plan, string name)
        {
            GameServer server = scene.Lab.Server;
            Actor hero = scene.Hero;
            float bolt = LengthOf(Shelf.BallistaBolt);
            uint seen = 0;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool step = attempt == 1;
                hero.Vitals = Vitals.Full(hero.Sheet);
                float last = hero.Vitals.Health;
                TelegraphComponent mark = default;
                Vector3 at = default;
                var hurt = new List<uint>();
                bool covered = false, moved = false, outOf = false;
                scene.Lab.Run(Rulebook.BallistaEvery + Rulebook.BallistaWindUp + 2f, () =>
                {
                    uint tick = server.Tick - 1;
                    if (mark.Serial == 0 && ArenaMark(server.Ecs, ShapeKind.Line, bolt, seen, out mark, out at))
                    {
                        covered = ShapeOf(mark, at).Covers(scene.Truth.Sim.Position, 0f);
                        if (step)
                        {
                            scene.Player.Client.TravelTo(Aside(scene, plan, scene.Truth.Sim.Position, mark.Facing, 6f));
                            moved = true;
                        }
                    }
                    if (mark.Serial != 0 && tick == mark.Lands) outOf = !ShapeOf(mark, at).Covers(scene.Truth.Sim.Position, GroundShape.HeroBodyShare * hero.Radius);
                    if (hero.Vitals.Health < last - 0.5f) hurt.Add(tick);
                    last = hero.Vitals.Health;
                });
                seen = mark.Serial;
                bool struck = mark.Serial != 0 && hurt.Contains(mark.Lands);
                if (!step)
                {
                    report.Check($"{name}: a bolt-thrower is aimed at whoever's on the floor, down a line from the commander's box", mark.Serial != 0 && covered, mark.Serial == 0 ? "no mark" : $"{(at.Xz - plan.Centre).Length:0} m from the middle");
                    report.Check($"{name}: its bolt goes down the line on the tick its mark said, into whoever's still in it", struck && hurt.Min() == mark.Lands, $"marked for {mark.Lands}, hurt on {string.Join(",", hurt)}");
                }
                else
                {
                    report.Check($"{name}: and past whoever stepped out of it", mark.Serial != 0 && moved && outOf && !struck, $"{(outOf ? "out of it" : "still in it")} on {mark.Lands}, hurt on {string.Join(",", hurt)}");
                }
            }
        }

        // Each clutch is something that can be struck, and breaking it leaves its shells and nothing else; one left alone hatches into the brood when its time comes.
        private static void Clutches(Report report, Scene scene, ArenaPlan plan, string name)
        {
            GameServer server = scene.Lab.Server;
            var clutches = new List<Actor>();
            foreach (Archetype archetype in server.Ecs.QueryArchetypes(new BitMask(typeof(ActorComponent), typeof(VisualComponent), typeof(EnemyComponent)), BitMask.Empty))
            {
                foreach (EntityView entity in new ComponentAccessor(archetype))
                {
                    if (ArenaSpec.TryParse(entity.Get<VisualComponent>().Model, out ArenaSpec spec) && spec.Shape == ArenaShape.Clutch) clutches.Add(entity.Get<ActorComponent>().Actor);
                }
            }
            report.Check($"{name}: its clutches are in their nests, each something that can be struck", clutches.Count == plan.Pieces.Count(p => p.Kind == ArenaPieceKind.Clutch) && clutches.All(c => !c.Dead && c.Sheet.MaxHealth > 0f));
            if (clutches.Count < 2) return;

            // The one due first is smashed, and the next is left to hatch.
            Actor first = clutches.OrderBy(c => plan.Pieces.FindIndex(p => p.Kind == ArenaPieceKind.Clutch && (p.At.Xz - c.Position).LengthSquared < 0.01f)).First();
            int before = scene.Session.Arrivals.Count;
            scene.Session.Combat.Harm(scene.Hero, first, 1e12f, DamageType.Physical);
            scene.Lab.Run(0.5);
            bool gone = !server.Ecs.TryGetEntityLocation(first.Entity, out _, out _) || !server.Ecs.GetEntityView(first.Entity).Has<ActorComponent>();
            bool shells = PiecesOf(server.Ecs, ArenaShape.Shells).Any(p => (p.at.Xz - first.Position).Length < 0.1f);
            report.Check($"{name}: smashing one leaves its shells, and nothing comes out", gone && shells && scene.Session.Arrivals.Count == before, $"{(gone ? "gone" : "still there")}, {(shells ? "shells" : "no shells")}, {scene.Session.Arrivals.Count - before} came");

            int hatched = 0;
            scene.Lab.Run(Rulebook.ClutchHatchFirst + Rulebook.ClutchHatchApart + 2f, () =>
            {
                if (hatched == 0 && scene.Session.Arrivals.Count > before) hatched = scene.Session.Arrivals.Count - before;
            });
            scene.Lab.Run(0.3);
            hatched = scene.Session.Arrivals.Count - before;
            report.Check($"{name}: one left alone hatches into the brood when its time comes", hatched >= Rulebook.ClutchHatchlings && hatched % Rulebook.ClutchHatchlings == 0, $"{hatched} came");
            foreach (Actor brood in scene.Session.Arrivals.ToList()) scene.Session.Combat.Harm(scene.Hero, brood, 1e12f, DamageType.Magic);
            scene.Lab.Run(0.5);
        }

        // The mirrors gather the sun onto whoever's on the floor, marked where they stand, and it lands on its tick and burns the ground, and takes its share of the tyrant if it's caught there too.
        private static void Mirrors(Report report, Scene scene, ArenaPlan plan, Actor tyrant, string name)
        {
            GameServer server = scene.Lab.Server;
            Actor hero = scene.Hero;
            hero.Vitals = Vitals.Full(hero.Sheet);
            float heroBefore = hero.Vitals.Health, tyrantBefore = Standing(tyrant);
            TelegraphComponent mark = default;
            Vector3 at = default;
            uint heroHurt = 0, tyrantHurt = 0;
            float tyrantAfter = tyrantBefore;
            bool onHero = false, burning = false, blocked = false;
            scene.Lab.Run(Rulebook.SunfireEvery + Rulebook.SunfireWindUp + 2f, () =>
            {
                uint tick = server.Tick - 1;
                if (mark.Serial == 0 && ArenaMark(server.Ecs, ShapeKind.Circle, LengthOf(Shelf.Sunfire), 0, out mark, out at))
                {
                    onHero = ShapeOf(mark, at).Covers(scene.Truth.Sim.Position, 0f);
                    // The tyrant's lured into it.
                    Put(scene, tyrant, at.Xz + new Vector2(0.8f, 0.4f));
                    heroBefore = hero.Vitals.Health;
                    tyrantBefore = Standing(tyrant);
                }
                if (mark.Serial != 0 && heroHurt == 0 && hero.Vitals.Health < heroBefore - 0.5f) heroHurt = tick;
                if (mark.Serial != 0 && tyrantHurt == 0 && Standing(tyrant) < tyrantBefore - 0.5f) tyrantHurt = tick;
                if (mark.Serial != 0 && tick == mark.Lands)
                {
                    tyrantAfter = Standing(tyrant);
                    blocked = JustBlocked(tyrant);
                }
                if (mark.Serial != 0 && tick == mark.Lands + 2) burning = Burning(server.Ecs);
            });
            float took = tyrantBefore - tyrantAfter;
            float share = Rulebook.SunfireTyrantShare / 100f * tyrant.Sheet.MaxHealth;
            report.Check($"{name}: its mirrors gather the sun onto whoever's on the floor, marked where they stand", mark.Serial != 0 && onHero);
            report.Check($"{name}: and it lands on its tick, burning them", mark.Serial != 0 && heroHurt == mark.Lands, $"marked for {mark.Lands}, hurt on {heroHurt}");
            report.Check($"{name}: and the tyrant caught in it, taking its share of the tyrant's health, or blocked", blocked || (tyrantHurt == mark.Lands && AShare(took, share, tyrant)), $"took {took:0} of {share:0} with {Through(tyrant, DamageType.Fire):0.00} of fire getting through, on {tyrantHurt}, marked for {mark.Lands}{(blocked ? ", blocked" : "")}");
            report.Check($"{name}: and leaves the ground burning", burning);
        }

        // A vent near whoever's on the floor wakes, marked along its crack, and breathes fire along it on its tick, leaving it smouldering.
        private static void Vents(Report report, Scene scene, ArenaPlan plan, string name)
        {
            GameServer server = scene.Lab.Server;
            Actor hero = scene.Hero;
            ArenaPiece vent = plan.Pieces.Where(p => p.Kind == ArenaPieceKind.Vent).OrderBy(p => (p.At.Xz - plan.Centre).LengthSquared).First();
            GameServer.TryFindGround(scene.Session.World, vent.At.Xz, out Vector2 on);
            scene.Go(on);
            hero.Vitals = Vitals.Full(hero.Sheet);
            float last = hero.Vitals.Health;
            TelegraphComponent mark = default;
            Vector3 at = default;
            var hurt = new List<uint>();
            bool onIt = false, smouldering = false;
            // Whatever an earlier one left smouldering hurts too, so what counts is whether it's on the tick this one's mark said.
            scene.Lab.Run(Rulebook.VentEvery * 2f + Rulebook.VentWindUp + 2f, () =>
            {
                uint tick = server.Tick - 1;
                if (mark.Serial == 0 && ArenaMark(server.Ecs, ShapeKind.Line, LengthOf(Shelf.Eruption), 0, out mark, out at)) onIt = ShapeOf(mark, at).Covers(scene.Truth.Sim.Position, 0f);
                if (hero.Vitals.Health < last - 0.5f) hurt.Add(tick);
                last = hero.Vitals.Health;
                if (mark.Serial != 0 && tick == mark.Lands + 2) smouldering = Burning(server.Ecs);
            });
            report.Check($"{name}: a vent near whoever's on the floor wakes, marked along its crack", mark.Serial != 0 && onIt);
            report.Check($"{name}: and breathes fire along it on its tick", mark.Serial != 0 && hurt.Contains(mark.Lands), $"marked for {mark.Lands}, hurt on {string.Join(",", hurt)}");
            report.Check($"{name}: and leaves it smouldering", smouldering);
        }

        // Nobody walks out of an arena, whichever way they go at its walls, nor is carried out by a page that moves them.
        private static void Sealed(Report report, Scene scene, ArenaPlan plan, string name)
        {
            GameServer.TryFindGround(scene.Session.World, plan.Centre, out Vector2 middle);
            float worst = 0f;
            int reached = 0;
            for (int i = 0; i < 8; i++)
            {
                float bearing = i * MathF.Tau / 8f;
                Vector2 way = new Vector2(MathF.Sin(bearing), MathF.Cos(bearing));
                // How far the floor goes that way, which is how far they ought to get before the wall stops them.
                float edge = 0f;
                while (edge < plan.Reach + 10f && plan.Contains(middle + way * (edge + 0.5f))) edge += 0.5f;
                scene.Go(middle);
                scene.Player.Heading = middle + way * (plan.Reach + 25f);
                float furthest = 0f;
                scene.Lab.Run(Math.Max(3.0, plan.Reach / 5.0), () =>
                {
                    Vector2 at = scene.Truth.Sim.Position;
                    furthest = MathF.Max(furthest, Vector2.Dot(at - middle, way));
                    if (!plan.Contains(at, 1.2f)) worst = MathF.Max(worst, (at - plan.Centre).Length);
                });
                scene.Player.Heading = null;
                if (furthest >= edge * 0.7f) reached++;
            }
            report.Check($"{name}: nobody walks out of it, whichever way they go at its walls", worst == 0f && reached >= 6, worst == 0f ? $"reached the walls {reached} ways of 8" : $"got {worst:0} m from the middle");

            scene.Go(middle);
            bool carriedOut = false;
            for (int i = 0; i < 8; i++)
            {
                float bearing = i * MathF.Tau / 8f + 0.3f;
                Vector2 way = new Vector2(MathF.Sin(bearing), MathF.Cos(bearing));
                scene.Session.Carry(scene.Hero, plan.Centre + way * (plan.Reach + 6f));
                scene.Lab.Run(0.3);
                carriedOut |= !plan.Contains(scene.Truth.Sim.Position, 1.2f);
                scene.Go(middle);
            }
            report.Check($"{name}: nor is carried out of it by a page that moves them", !carriedOut);
        }
    }
}
