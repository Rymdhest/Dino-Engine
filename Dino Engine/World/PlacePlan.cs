using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// What a place with a story of its own is, as far as the game, the chart and the checks need to know (PLACES.md): its frame, how far it reaches, its ways in, and what in it moves
    /// once it's freed, the cages whose doors swing open and the beasts in them, and the paddocks whose stock grazes. Its builder works it out on the site's own fork, which every
    /// machine has, so nothing of it is ever sent. Headless.
    /// </summary>
    public sealed class PlacePlan
    {
        public Vector2 Centre;

        /// <summary>Turns the place's own +z, its front, onto the world, as a model's yaw does: the camera looks along the world's +z, so a front towards it has a yaw of pi.</summary>
        public float Yaw;

        /// <summary>How far it reaches from its centre along its own x and z.</summary>
        public Vector2 Half;

        /// <summary>The middle of each way in, on the line of its fence or wall, and the way out through it.</summary>
        public readonly List<(Vector2 At, Vector2 Out)> Gates = new();

        public readonly List<PlaceCage> Cages = new();
        public readonly List<PlacePaddock> Paddocks = new();

        /// <summary>What the chart draws of the place that isn't a building: a hull on the sand, a pit, a ring of nests.</summary>
        public readonly List<PlaceMark> Marks = new();

        /// <summary>The way along its own x, on the ground.</summary>
        public Vector2 Ax => new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));

        /// <summary>The way along its own z, on the ground: its front.</summary>
        public Vector2 Az => new Vector2(MathF.Sin(Yaw), MathF.Cos(Yaw));

        public Vector2 ToWorld(float u, float v) => Centre + Ax * u + Az * v;
        public Vector2 ToWorld(Vector2 local) => ToWorld(local.X, local.Y);
        public Vector2 Dir(Vector2 local) => Ax * local.X + Az * local.Y;

        public Vector2 ToLocal(Vector2 world)
        {
            Vector2 d = world - Centre;
            return new Vector2(Vector2.Dot(d, Ax), Vector2.Dot(d, Az));
        }

        /// <summary>Whether a point in the place's own frame is within its reach, or within <paramref name="grow"/> of it.</summary>
        public bool InsideLocal(Vector2 local, float grow = 0f) => MathF.Abs(local.X) <= Half.X + grow && MathF.Abs(local.Y) <= Half.Y + grow;

        public bool Inside(Vector2 world, float grow = 0f) => InsideLocal(ToLocal(world), grow);

        /// <summary>The farthest it gets from its middle.</summary>
        public float Reach => Half.Length;
    }

    /// <summary>
    /// A cage with a beast in it, which opens when its place is freed (PLACES.md): its door, hinged at <paramref name="Hinge"/> on the ground and turned to <paramref name="Yaw"/> when shut, drawn as the
    /// model <paramref name="Door"/> (none for a beast chained in the open), swinging <paramref name="Swing"/> radians about its hinge to open, at the height of the cage, which stands at
    /// <paramref name="Middle"/>; and what's in it, <paramref name="Look"/> at <paramref name="Scale"/>, standing at <paramref name="Captive"/> facing <paramref name="Facing"/>. Freed, it walks
    /// out to <paramref name="Out"/>, in front of the door, and on to <paramref name="Away"/>, out of the place.
    /// </summary>
    public sealed record PlaceCage(Vector2 Hinge, float Yaw, string Door, float Swing, Vector2 Middle, Vector2 Captive, float Facing, string Look, float Scale, Vector2 Out, Vector2 Away);

    /// <summary>A paddock, a box turned by <paramref name="Yaw"/> with its gate at <paramref name="Gate"/>, inside which stock grazes: what's there wanders within it and never leaves.</summary>
    public sealed record PlacePaddock(Vector2 Centre, Vector2 Half, float Yaw, Vector2 Gate, string Look, float Scale);

    /// <summary>Something the chart draws of a place that isn't a building (WorldChart).</summary>
    public enum PlaceMarkKind
    {
        Hull,       // A ship on its side: a box of its length and beam, turned by its yaw.
        Pit,        // Ground dug out: a box.
        Nest,       // A nest of eggs: a circle.
        Tower,      // A round tower of snow: a circle.
        Web,        // A weaver's web, strung between trees: a circle.
    }

    public readonly record struct PlaceMark(PlaceMarkKind Kind, Vector2 At, float Yaw, Vector2 Half);
}
