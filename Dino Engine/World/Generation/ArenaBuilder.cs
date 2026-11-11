using Dino_Engine.World.Generation.Arenas;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds an arena: a boss map's whole world every tenth level, or the sealed one in a land's keep where its tyrant waits. Each kind has a layout of its own (<see cref="ArenaLayout"/>), and every one
    /// is sealed round its floor, so that nothing that walks is ever outside it and nothing outside can walk in, and says what the game needs of it in an <see cref="ArenaPlan"/>. Read ARENAS.md.
    /// </summary>
    public static class ArenaBuilder
    {
        /// <summary>A boss map's arena, the whole of its world.</summary>
        internal static void Build(SiteBuilder b)
        {
            ArenaKind kind = b.World.Plan.Archetype.ArenaKind ?? ArenaKind.Pit;
            ArenaLayout layout = Layout(kind, b, keep: false, b.Centre);
            layout.Build();
            b.Site.Arena = layout.Plan;
        }

        /// <summary>
        /// The arena in a land's keep, where its tyrant waits: one of the six kinds, drawn from a fork of the keep's own, at a keep's size about <paramref name="centre"/>, walled round at its rim and
        /// sealed, with the way down by the gate in its wall and the way up from its floor (<see cref="ArenaPlan.Door"/>, <see cref="ArenaPlan.Exit"/>).
        /// </summary>
        internal static ArenaPlan Keep(SiteBuilder b, Vector2 centre)
        {
            ArenaKind kind = KeepKind(b.Random.Fork("keep arena"));
            ArenaLayout layout = Layout(kind, b, keep: true, centre);
            layout.Build();
            b.Site.Arena = layout.Plan;
            return layout.Plan;
        }

        /// <summary>Which kind a keep's arena is, each as likely as the next.</summary>
        internal static ArenaKind KeepKind(WorldRandom random) => (ArenaKind)random.Int(Enum.GetValues<ArenaKind>().Length);

        internal static ArenaLayout Layout(ArenaKind kind, SiteBuilder b, bool keep, Vector2 centre) => kind switch
        {
            ArenaKind.Colosseum => new ColosseumArena(b, keep, centre),
            ArenaKind.Cage => new CageArena(b, keep, centre),
            ArenaKind.SunTemple => new SunTempleArena(b, keep, centre),
            ArenaKind.Onslaught => new OnslaughtArena(b, keep, centre),
            ArenaKind.Cathedral => new CathedralArena(b, keep, centre),
            _ => new PitArena(b, keep, centre),
        };
    }
}
