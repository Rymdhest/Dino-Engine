using Dino_Defenders.Gameplay;
using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// The Haven's halls as the game sees them: whether a hero may use one, and what each is for (HALLS.md). The buildings themselves are the world's
    /// (<see cref="HallDesigns"/>); this is their rules, which both ends run, as a hero's step and what they may use have to agree.
    /// </summary>
    public static class Halls
    {
        /// <summary>
        /// Whether every hall stands open to every hero, which is how they are while the game's being tested. Halls are meant to be earned, one at a time, before a hero can go in
        /// (HALLS.md says how that's to go); until that's decided, nothing is locked.
        /// </summary>
        public const bool AllOpen = true;

        /// <summary>Whether a hero may go into a hall and use what's in it.</summary>
        public static bool IsOpen(HeroState hero, HallKind kind) => AllOpen || hero.HallsOpen.Contains(kind.ToString());

        /// <summary>The hall what's used here belongs to, if it's in one.</summary>
        public static HallKind? Of(InteractKind kind) => kind switch
        {
            InteractKind.Workbench => HallKind.Forge,
            InteractKind.GamingTable => HallKind.GamingHouse,
            _ => null,
        };

        /// <summary>What's said to a hero who tries a hall they haven't opened.</summary>
        public static string Shut(HallKind kind) => $"{HallDesigns.Of(kind).Name} isn't open to you yet.";
    }
}
