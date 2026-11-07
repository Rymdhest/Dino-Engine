using Dino_Defenders.Books;
using Dino_Defenders.Stats;
using Dino_Engine.Networking;
using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Defenders.Gameplay
{
    public enum ShapeKind : byte
    {
        Circle,
        Sector,     // A slice of a circle, from a point.
        Line,       // A strip, from a point.
    }

    /// <summary>How much of a player's attention something deserves. There are a lot of these in a fight, and only so much attention.</summary>
    public enum MarkTier : byte
    {
        Slight,     // Something small, from one of many.
        Plain,      // An attack worth stepping out of.
        Grave,      // Stay in this and it will hurt.
    }

    /// <summary>
    /// A piece of ground. This is the one description of what an attack covers: the server tests blows against it and a client draws it, so what's shown can't be anything but what lands. The shape of
    /// a hit and the mark on the ground are the same object, and nothing else may decide whether something is in the way of an area.
    /// </summary>
    public readonly record struct GroundShape(ShapeKind Kind, Vector2 Origin, Vector2 Facing, float Length, float Width)
    {
        private const float BeyondTarget = 7f;      // Metres what flies is shown past whoever it's aimed at. It goes on further, but that's nobody's concern yet.

        /// <summary>A body is this near the middle of an area before it's over what's going to hit it. Heroes get the benefit of the doubt: if a foot looks out of it, it is.</summary>
        public const float HeroBodyShare = 0.4f;

        /// <summary>Half of how wide what flies is, as far as hitting anything goes.</summary>
        public const float FlightWidth = 1.3f;

        public static GroundShape Circle(Vector2 centre, float radius) => new GroundShape(ShapeKind.Circle, centre, Vector2.UnitY, radius, 0f);

        /// <param name="arc">Radians, from one edge to the other.</param>
        public static GroundShape Sector(Vector2 origin, Vector2 facing, float reach, float arc)
        {
            return arc >= MathF.Tau ? Circle(origin, reach) : new GroundShape(ShapeKind.Sector, origin, facing, reach, arc);
        }

        /// <param name="halfWidth">How far either side of the middle it goes.</param>
        public static GroundShape Line(Vector2 origin, Vector2 facing, float length, float halfWidth) => new GroundShape(ShapeKind.Line, origin, facing, length, halfWidth);

        public float Yaw => MathF.Atan2(Facing.X, Facing.Y);

        /// <summary>The furthest anything in it is from <see cref="Origin"/>, for finding what might be.</summary>
        public float Extent => Kind == ShapeKind.Line ? Length + Width : Length;

        /// <summary>Whether a body of this radius, standing here, is touching this.</summary>
        public bool Covers(Vector2 point, float body)
        {
            Vector2 offset = point - Origin;
            switch (Kind)
            {
                case ShapeKind.Circle:
                    return offset.LengthSquared <= (Length + body) * (Length + body);

                case ShapeKind.Sector:
                    float distance = offset.Length;
                    if (distance > Length + body) return false;
                    return distance <= 0.5f || Vector2.Dot(offset / distance, Facing) >= MathF.Cos(Width * 0.5f);

                default:
                    float along = Vector2.Dot(offset, Facing);
                    if (along < 0f || along > Length) return false;
                    return (offset - Facing * along).Length <= Width + body;
            }
        }

        /// <summary>What an area page covers, from somewhere, going some way. Anything that isn't in someone's hands is centred on where it is, as that's what's being pointed at.</summary>
        public static GroundShape Of(PageProfile page, Vector2 from, Vector2 facing)
        {
            switch (page.Page.Shape)
            {
                case AreaShape.Circle:
                    return Circle(from, page.Length);
                case AreaShape.Line:
                    return Line(from, facing, page.Length, page.Width);
                case AreaShape.Rectangle:
                    return Line(page.Page.From == Start.You ? from : from - facing * (page.Length * 0.5f), facing, page.Length, page.Width);
                default:
                    return Sector(from, facing, page.Length, page.Width);
            }
        }

        /// <summary>
        /// What a book is going to cover, used from somewhere, at something. Whatever's marked comes through here, so that's what has to be true: where something flies is its line, and where it comes down or carries someone to
        /// is whatever the next page does there.
        /// </summary>
        /// <param name="reach">How far off it was aimed.</param>
        public static GroundShape Of(BookProfile book, Vector2 origin, Vector2 facing, float reach)
        {
            PageProfile page = book.First;
            PagePlan plan = page.Page;
            Vector2 target = origin + facing * MathF.Min(reach, page.Range);
            Vector2 from = plan.From == Start.Cursor ? target : origin;

            if (plan.Kind == PageKind.Area) return Of(page, from, facing);
            if (plan.To == Bearing.Down || plan.Rides)
            {
                Vector2 at = plan.Rides ? target : from;
                return book.Pages.Length > 1 && book.Pages[1].Page.Kind == PageKind.Area ? Of(book.Pages[1], at, facing) : Circle(at, FlightWidth);
            }

            float length = MathF.Min(page.Range, reach + BeyondTarget);
            if (page.Projectiles <= 1) return Line(origin, facing, length, FlightWidth);
            return Sector(origin, facing, length, plan.Spread * (page.Projectiles - 1) + 0.1f);
        }

        /// <summary>Somewhere in it, for what's shown happening all over it.</summary>
        public Vector2 Anywhere(float along, float across)
        {
            switch (Kind)
            {
                case ShapeKind.Circle:
                    float angle = along * MathF.Tau;
                    return Origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (MathF.Sqrt(across) * Length);
                case ShapeKind.Sector:
                    float turn = (along - 0.5f) * Width;
                    float cos = MathF.Cos(turn);
                    float sin = MathF.Sin(turn);
                    return Origin + new Vector2(Facing.X * cos - Facing.Y * sin, Facing.X * sin + Facing.Y * cos) * (MathF.Sqrt(across) * Length);
                default:
                    return Origin + Facing * (along * Length) + new Vector2(-Facing.Y, Facing.X) * ((across * 2f - 1f) * Width);
            }
        }
    }

    /// <summary>
    /// Something is about to happen on this piece of ground, and when. Everything about it is sent once, as it's what lets a player get out of the way: it's a component and not an event, as it can't be
    /// allowed to go missing. A client works out how far along it is from <see cref="Lands"/> and its own clock, which is the clock its hero moves by. So when the fill reaches the edge, that's the tick the
    /// server decides on, for this player, whatever the line is like.
    /// </summary>
    [Replicated]
    public struct TelegraphComponent : IComponent
    {
        public uint Serial;         // Which this is, for good. An entity's id won't do: the next thing made is given it the moment this has gone.
        public ShapeKind Kind;
        public MarkTier Tier;
        public bool Hostile;
        public int Owner;           // Whose this is if it's a player's, who has been showing it since they pressed.
        [NetStep(1f / 128f), NetSnap] public Vector2 Facing;
        [NetStep(1f / 32f), NetSnap] public float Length;
        [NetStep(1f / 128f), NetSnap] public float Width;
        public uint Start;
        public uint Lands;

        public readonly GroundShape ShapeAt(Vector2 origin) => new GroundShape(Kind, origin, Facing, Length, Width);
    }

    /// <summary>
    /// What someone is doing, for those watching. It's when it started, when it lands and when it's over, so what's drawn is worked out from those, by a clock, and can be picked up at any moment.
    /// Each new one changes the serial, so that two the same in a row are seen to be two.
    /// </summary>
    [Replicated]
    public struct ActionComponent : IComponent
    {
        public byte Serial;
        public int Sign;            // What it looks like: see PageSign. Nothing means nobody's doing anything.
        public uint Start;
        public uint Lands;
        public uint Ends;
        [NetStep(1f / 64f), NetSnap] public float Aim;
        [NotReplicated] public byte Heard;
    }
}
