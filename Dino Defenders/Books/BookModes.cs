using Dino_Defenders.Progression;
using Dino_Engine.World;

namespace Dino_Defenders.Books
{
    /// <summary>How a book goes off. It's what the book is, found that way, and never changes. See BOOKS.md.</summary>
    public enum BookMode : byte
    {
        Active,     // When it's pressed.
        Trigger,    // By itself, whenever what it's waiting for has happened enough.
        Toggle,     // Turned on and left on, for as long as there's Mana Regeneration to give up for it.
    }

    /// <summary>What a Trigger Book can be waiting for. The first of these line up with <see cref="Stats.TriggerEvent"/>, which <see cref="Server.Combat.SetOff"/> relies on, so anything new goes on the end.</summary>
    public enum BookWhen : byte
    {
        Hit,
        Critical,
        Kill,
        Struck,
        Block,
        Dodge,
        Flask,
        Damaged,    // By a share of Maximum Health, in percent.
        Moved,      // By metres.
        Used,       // By Books used by hand.
    }

    /// <summary>What a Trigger Book is waiting for, and how much of it.</summary>
    public sealed class BookTrigger
    {
        public BookWhen When { get; set; }
        public float Amount { get; set; } = 1f;

        public BookTrigger Copy() => (BookTrigger)MemberwiseClone();
    }

    /// <summary>How Trigger Books are rolled: anything they can wait for, and the rarer the book the less it has to wait.</summary>
    public static class BookTriggers
    {
        private const float GradeSteps = 5f;

        /// <param name="Most">What the plainest asks for.</param>
        /// <param name="Least">What the best asks for.</param>
        private readonly record struct Line(BookWhen When, float Most, float Least, bool Whole = true);

        private static readonly Line[] Lines =
        {
            new(BookWhen.Hit, 30f, 8f),
            new(BookWhen.Critical, 12f, 3f),
            new(BookWhen.Kill, 12f, 3f),
            new(BookWhen.Struck, 10f, 3f),
            new(BookWhen.Block, 8f, 2f),
            new(BookWhen.Dodge, 8f, 2f),
            new(BookWhen.Flask, 2f, 1f),
            new(BookWhen.Damaged, 60f, 15f),
            new(BookWhen.Moved, 80f, 20f),
            new(BookWhen.Used, 12f, 3f),
        };

        /// <summary>Everything a book can wait for.</summary>
        public static IEnumerable<BookWhen> All => Lines.Select(line => line.When);

        public static BookTrigger Roll(LootGrade grade, WorldRandom random)
        {
            Line line = Lines[random.Int(Lines.Length)];
            float share = ((int)grade + random.Float()) / GradeSteps;
            return new BookTrigger { When = line.When, Amount = MathF.Round(line.Most + (line.Least - line.Most) * share) };
        }

        /// <summary>What's said to be waited for, brought back to something that could have been rolled, for a save that's been edited.</summary>
        public static BookTrigger Fix(BookTrigger? trigger)
        {
            trigger ??= new BookTrigger { When = BookWhen.Hit, Amount = Lines[0].Most };
            if (!Enum.IsDefined(trigger.When)) trigger.When = BookWhen.Hit;
            Line line = Array.Find(Lines, each => each.When == trigger.When);
            trigger.Amount = Math.Clamp(float.IsFinite(trigger.Amount) ? trigger.Amount : line.Most, line.Least, line.Most);
            return trigger;
        }
    }
}
