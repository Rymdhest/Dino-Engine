using Dino_Engine.GUI;

namespace Dino_Defenders.UI
{
    /// <summary>
    /// Boxes on screen that push each other up rather than overlap. Whoever goes round placing them starts over each time one moves, so a move has to be real: near the top of
    /// the screen rounding can leave a box that's just been lifted touching what it was lifted over, and lifting it again puts it exactly where it is. That froze the game.
    /// </summary>
    public static class LabelStack
    {
        /// <summary>Lifts a box above what's been placed if it's in the way. True only if it went up, which can't go on forever, so it's safe to start over on.</summary>
        public static bool Lift(ref GuiRect box, in GuiRect placed, float gap)
        {
            bool overlaps = box.X < placed.Right + gap && box.Right + gap > placed.X && box.Y < placed.Bottom + gap && box.Bottom + gap > placed.Y;
            if (!overlaps) return false;

            float above = placed.Y - box.Height - gap;
            if (above >= box.Y) return false;

            box.Y = above;
            return true;
        }
    }
}
