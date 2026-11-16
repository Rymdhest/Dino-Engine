using Dino_Defenders.Client;
using Dino_Defenders.Gameplay;
using Dino_Defenders.Progression;
using Dino_Defenders.Server;
using Dino_Defenders.Stats;
using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_NetLab
{
    internal static partial class LandTests
    {
        // A client grows only the creatures a land can send while it loads (EncounterSystem.Foresee, CharacterFactory.Grow), and anything it didn't grow is built on the frame it's first
        // sent, which is a stall in the middle of a fight. So every place on a land is woken here, on a real server, and every body that comes in has to be one the client grew, on lands
        // with nothing given for them, with two tyrants and the dead rising, in an arena, in the snow and on someone's first steps.
        public static int RunCreatures(Options options)
        {
            var report = new Report();
            WhatAClientGrows(report, new WorldCache());
            return report.Finish();
        }

        private static void WhatAClientGrows(Report report, WorldCache worlds)
        {
            report.Section("What a client grows for a land");

            HeroState climber = Sturdy(10);
            var twinsAndWrit = new LandTerms { Orders = { OrdersCatalog.TwinPens.Id, OrdersCatalog.Writ.Id } };
            var cases = new (string what, WorldRequest request, LandTerms terms, int level)[]
            {
                ("a land with nothing given for it", Home, new LandTerms(), 6),
                ("two tyrants and the dead rising", Home, twinsAndWrit, 9),
                ("an arena", new WorldRequest(Lands.Offer(climber, 10)[0].Request.Seed, ArenaCatalog.Pit.Id, 10), new LandTerms(), 10),
                ("a land in the snow", new WorldRequest(5, "highlands", 3), new LandTerms(), 3),
                ("someone's first steps", Lands.Offer(Sturdy(1), 1)[0].Request, new LandTerms(), 1),
            };
            int everything = new HashSet<string>(CharacterFactory.Everything()).Count;
            foreach ((string what, WorldRequest request, LandTerms terms, int level) in cases)
            {
                worlds.Get(terms.Shape(request)).Wait();
                using var scene = new Scene(worlds, request, terms, Sturdy(level));
                ServerSession session = scene.Session;
                HashSet<string> grown = EncounterSystem.Foresee(scene.Seen.World, scene.Seen.Terms);
                report.Check($"For {what}, the client foresees what the server does", grown.SetEquals(EncounterSystem.Foresee(session.World, session.Terms)), $"{grown.Count} models");

                var sent = new HashSet<string>();
                void Look()
                {
                    foreach (Archetype archetype in scene.Lab.Server.Ecs.QueryArchetypes(new BitMask(typeof(ActorComponent), typeof(EnemyComponent)), BitMask.Empty))
                    {
                        foreach (EntityView entity in new ComponentAccessor(archetype))
                        {
                            Actor actor = entity.Get<ActorComponent>().Actor;
                            if (actor.Team == Team.Enemies) sent.Add(actor.Body);
                        }
                    }
                }

                int woken = 0;
                foreach (SiteState state in session.Sites.Where(s => s.HostilesTotal > 0).ToList())
                {
                    scene.Go(state.Site.Position);
                    Look();
                    woken++;
                }

                // Under the Writ what falls gets up again as bones, which comes of a fight and not of where anyone stands.
                if (session.Terms.Has(OrdersCatalog.Writ))
                {
                    SiteState pack = session.Sites.Where(s => s.Wild && s.HostilesTotal > 0).OrderBy(s => s.Site.Remoteness).First();
                    scene.Go(pack.Site.Position);
                    foreach (Actor victim in scene.Members(pack).Where(a => !a.Dead).Take(3)) session.Combat.Harm(scene.Hero, victim, 1e12f, DamageType.Magic);
                    scene.Lab.Run(Rulebook.RiseDelay + 0.6);
                    report.Check("And the dead really rose", RisenActors(scene.Lab).Any());
                    Look();
                }

                var missed = sent.Where(model => !grown.Contains(model)).OrderBy(model => model).ToList();
                report.Check($"And every one of the {sent.Count} looks that came in at its {woken} places was grown for it", woken > 0 && sent.Count > 0 && missed.Count == 0,
                    missed.Count > 0 ? $"not grown: {string.Join(", ", missed)}" : $"{grown.Count} grown of {everything} there are");
            }
        }
    }
}
