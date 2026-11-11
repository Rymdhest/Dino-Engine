using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// Every kind of arena there is. A boss map every tenth level is one of them on an isle of its own, and every land's keep holds a smaller one, sealed, where its tyrant waits. Read ARENAS.md.
    /// </summary>
    public enum ArenaKind
    {
        Pit,            // A round pit dug into a hill, its floor sand, a sheer wall round it and stone stands above.
        Colosseum,      // The Reich's great amphitheatre: an oval of sand, a cavea of stone seats, lifts under the floor and bolt-throwers in the commander's box.
        Cage,           // The Amber Institute's breeding cage: black iron bars round a pen of mud and straw, with the pens of the brood round it and its eggs in the straw.
        SunTemple,      // A temple of the land's own sun the Reich made its seat of assembly: a sunken court of golden stone, a great disc, and brass mirrors that burn.
        Onslaught,      // The Ossuary's dig where the Reich's first charge was buried in ash: a quarry bowl, its floor strewn with the charge turned to stone.
        Cathedral,      // The Cathedral of the Iron Apex: a nave of dark stone under ribs of black iron, candles on iron wheels over it, and windows of oxblood and amber.
    }

    /// <summary>How the brood is let onto an arena's floor through one of its entries.</summary>
    public enum ArenaEntryKind
    {
        Portcullis,     // A grille rises into the stone over an arched gate in the wall: the Pit's gates.
        PenDoor,        // A barred door rises out of the way between two of the bars: the Jurassic Cage's pens.
        Hatch,          // Two leaves drop open in the floor, and what's below comes up on a lift: the Colosseum's.
        Doors,          // Two bronze leaves swing in: the Sun-Temple's.
        Screen,         // A gate in the iron screen between two piers rises: the Cathedral's aisles.
        Rising,         // Nothing opens: the Ossuary's dead claw their way up out of the ash at a marked spot, the Onslaught's.
    }

    /// <summary>A set piece of an arena's that takes part in the fight, each by its own rules (ArenaSystem in the game).</summary>
    public enum ArenaPieceKind
    {
        Mirror,         // A brass mirror the Sun-Temple's priests turn to burn the floor with the sun.
        Ballista,       // A bolt-thrower in the Colosseum's box.
        Chandelier,     // An iron wheel of candles hung over the Cathedral's nave, which comes down.
        Vent,           // A crack in the Onslaught's ash that breathes fire.
        Clutch,         // A nest of the Institute's eggs in the Jurassic Cage, which hatch if they're left.
    }

    /// <summary>
    /// One of the ways the brood comes onto an arena's floor: where those let through are first seen (<see cref="Out"/>, on the floor), and the moving part, if it has one, at rest: where it stands
    /// and which way it faces, the model it is (<see cref="Model"/>, an arena piece's id, grown by the game as anything of the arena's is), and how far it goes when it opens (<see cref="Lift"/>: metres
    /// up for a grille, degrees for a leaf). A hatch or a pair of doors has two leaves, the second its first turned round, each turning about its hinge, which is its model's origin, and
    /// <see cref="Span"/> apart.
    /// </summary>
    public readonly record struct ArenaEntry(ArenaEntryKind Kind, Vector2 Out, Vector3 Door, float Yaw, string Model, float Lift, float Span = 0f);

    /// <summary>A set piece, where it stands or hangs and which way it faces, and its model if the game moves it (empty if it stands in the world as it is).</summary>
    public readonly record struct ArenaPiece(ArenaPieceKind Kind, Vector3 At, float Yaw, string Model);

    /// <summary>
    /// What the game needs to know of an arena that a list of spawns can't say: its kind, the floor it's sealed round, the ways the brood comes in, the set pieces that take part, and for a keep's arena the
    /// way down into it and up out of it. Worked out with the world and never saved, as the same request grows the same arena. Headless. Read ARENAS.md.
    /// </summary>
    public sealed class ArenaPlan
    {
        public ArenaKind Kind;

        /// <summary>A keep's arena, in a land, rather than the whole of a boss map.</summary>
        public bool Keep;

        /// <summary>The middle of its floor.</summary>
        public Vector2 Centre;

        /// <summary>Where people stand on its floor.</summary>
        public float Floor;

        /// <summary>
        /// The inside face of the seal round the floor, in order round it: nothing that can walk is ever outside it, and nothing outside it can walk in (WorldValidator holds both). It's the floor, and
        /// whatever bay of it holds the waygate or the way up.
        /// </summary>
        public readonly List<Vector2> Bounds = new();

        public readonly List<ArenaEntry> Entries = new();
        public readonly List<ArenaPiece> Pieces = new();

        /// <summary>How far from the middle anything of it stands, its stands, galleries and outer walls: who's further off isn't at the fight.</summary>
        public float Reach;

        /// <summary>A keep's arena: where its way down stands, outside, on the keep's own ground, and which way it faces.</summary>
        public Vector2 Door;
        public float DoorYaw;

        /// <summary>A keep's arena: where someone who goes down arrives on its floor.</summary>
        public Vector2 Arrival;

        /// <summary>A keep's arena: where its way up stands, inside, which only lets anyone out once its tyrant is down.</summary>
        public Vector2 Exit;

        /// <summary>A keep's arena: the gate of its way up, which shuts behind whoever comes down and opens once its tyrant is down, as an entry's moving part is (its <see cref="ArenaEntry.Out"/> is the arrival).</summary>
        public ArenaEntry? Way;

        /// <summary>A keep's arena: the radius of the wall round its rim, inside which nobody walks but on its floor.</summary>
        public float Rim;

        /// <summary>Whether a point is inside the seal, or within <paramref name="grow"/> metres of it.</summary>
        public bool Contains(Vector2 point, float grow = 0f)
        {
            if (Bounds.Count < 3) return false;
            if (Inside(point)) return true;
            if (grow <= 0f) return false;
            for (int i = 0; i < Bounds.Count; i++)
            {
                if (DistanceToSegment(point, Bounds[i], Bounds[(i + 1) % Bounds.Count]) <= grow) return true;
            }
            return false;
        }

        // Crossings of a ray to +x: odd is inside.
        private bool Inside(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = Bounds.Count - 1; i < Bounds.Count; j = i++)
            {
                Vector2 a = Bounds[i], b = Bounds[j];
                if ((a.Y > point.Y) == (b.Y > point.Y)) continue;
                float x = a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y);
                if (point.X < x) inside = !inside;
            }
            return inside;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.LengthSquared > 1e-8f ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
            return (p - (a + ab * t)).Length;
        }

        /// <summary>The area inside the seal, in square metres.</summary>
        public float Area
        {
            get
            {
                float twice = 0f;
                for (int i = 0, j = Bounds.Count - 1; i < Bounds.Count; j = i++) twice += Bounds[j].X * Bounds[i].Y - Bounds[i].X * Bounds[j].Y;
                return MathF.Abs(twice) * 0.5f;
            }
        }
    }
}
