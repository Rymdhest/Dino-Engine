using Dino_Engine.World.Generation.Arenas;
using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Builds an arena, a boss map's whole world every tenth level. Each kind has a layout of its own (<see cref="ArenaLayout"/>), and every one is sealed round its floor, so that nothing that walks is
    /// ever outside it and nothing outside can walk in, and says what the game needs of it in an <see cref="ArenaPlan"/>. A land's keep is open ground instead, where its tyrant can be led anywhere. Read ARENAS.md.
    /// </summary>
    public static class ArenaBuilder
    {
        /// <summary>A boss map's arena, the whole of its world.</summary>
        internal static void Build(SiteBuilder b)
        {
            ArenaKind kind = b.World.Plan.Archetype.ArenaKind ?? ArenaKind.Pit;
            ArenaLayout layout = Layout(kind, b, b.Centre);
            layout.Build();
            b.Site.Arena = layout.Plan;
        }

        internal static ArenaLayout Layout(ArenaKind kind, SiteBuilder b, Vector2 centre) => kind switch
        {
            ArenaKind.Colosseum => new ColosseumArena(b, centre),
            ArenaKind.Cage => new CageArena(b, centre),
            ArenaKind.SunTemple => new SunTempleArena(b, centre),
            ArenaKind.Onslaught => new OnslaughtArena(b, centre),
            ArenaKind.Cathedral => new CathedralArena(b, centre),
            _ => new PitArena(b, centre),
        };
    }
}
