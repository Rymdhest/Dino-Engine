using Dino_Engine.Modelling.Procedural.Architecture.Arena;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// What every arena's layout is built with: pieces grown from their ids (<see cref="ArenaSpec"/>), ground raised and sunk exactly (<see cref="WorldData.Terraces"/>), and the seal round its floor, a ring of
    /// solid footprints along the inside of its walls that nothing walks through, with the floor sunk below everything round it so that the walkable ground ends there too (ARENAS.md). Positions are metres
    /// east and north of the arena's middle (east is the left of the screen, as the camera looks north), and heights are above its floor. Bearings run clockwise from north, seen from above.
    /// </summary>
    internal abstract class ArenaLayout
    {
        // How far inside a wall's face the ground under it stops: a grid of heights a metre apart lays a step as a slope up to 1.42 m wide, which has to fall inside the wall (SQUARE.md).
        protected const float Inset = 1.47f;
        protected const float Sharp = 0.02f;

        // The thinnest a seal is laid, whatever the wall it's in: a body thrown hard sinks about two metres into anything solid before it's stopped, and a seal it got past the middle of
        // it came out the far side of. Behind a wall's face is only stands, galleries or aisles, where nobody walks.
        protected const float LeastSeal = 4.5f;

        protected static readonly Vector2 North = new Vector2(0f, 1f);
        protected static readonly Vector2 East = new Vector2(1f, 0f);

        protected readonly SiteBuilder B;
        protected readonly Vector2 C;
        protected readonly WorldRandom Random;
        public readonly ArenaPlan Plan;
        private uint _seed;

        /// <summary>Where people stand on the floor.</summary>
        protected float F;

        protected ArenaLayout(SiteBuilder b, ArenaKind kind, Vector2 centre, string fork)
        {
            B = b;
            C = centre;
            Random = b.Random.Fork(fork);
            Shape = b.Random.Fork(fork + " shape");
            _seed = (uint)(b.World.Plan.Seed * 2654435761L) ^ (uint)kind * 0x9E37u;
            Plan = new ArenaPlan { Kind = kind, Centre = centre };
        }

        /// <summary>
        /// What an arena's size and layout are drawn from, a fork of its own taken before anything is built, so that one seed's arena is larger or smaller, with more or fewer gates and something or
        /// nothing to fight round on its floor, without moving anything else drawn after.
        /// </summary>
        protected readonly WorldRandom Shape;

        protected WorldData World => B.World;

        /// <summary>Lays everything, and fills in <see cref="Plan"/>.</summary>
        public abstract void Build();

        // ---------------------------------------------------------------- Ground

        /// <summary>The average of the land within a radius, as a layout's own level is.</summary>
        protected float AverageGround(float radius)
        {
            float total = 0f;
            int count = 0;
            for (float x = -radius; x <= radius; x += 4f)
            {
                for (float z = -radius; z <= radius; z += 4f)
                {
                    if (x * x + z * z > radius * radius) continue;
                    total += World.Elevation.Sample(C + new Vector2(x, z));
                    count++;
                }
            }
            return count > 0 ? total / count : World.Elevation.Sample(C);
        }

        /// <summary>Ground levelled softly, as the land an arena's dug into is: laid before the roads.</summary>
        protected void Level(in Footprint footprint, float lift, float margin) => World.Pads.Add(new GroundPad(footprint, F + lift, margin));

        /// <summary>Ground raised or sunk exactly to a height above the floor, its edge sharp, as walls hide it: laid after the roads, in order, each cutting into those before.</summary>
        protected void Raise(in Footprint footprint, float lift) => World.Terraces.Add(new GroundPad(footprint, F + lift, Sharp));

        /// <summary>A ring of ground at a height, from one radius to another, as a chain of boxes round it, which a footprint of its own can't be.</summary>
        protected void RaiseRing(float inner, float outer, float lift, float from = 0f, float sweep = 360f)
        {
            float middle = (inner + outer) * 0.5f, half = (outer - inner) * 0.5f;
            int links = Math.Max(8, (int)MathF.Ceiling(MathHelper.DegreesToRadians(MathF.Abs(sweep)) * middle / 1.6f));
            for (int i = 0; i < links; i++)
            {
                float bearing = from + sweep * (i + 0.5f) / links;
                float length = MathHelper.DegreesToRadians(MathF.Abs(sweep)) * middle / links + 0.6f;
                Vector2 at = Toward(bearing) * middle;
                Raise(Footprint.Box(C + at, new Vector2(length * 0.5f, half), YawFacing(at) + MathF.PI * 0.5f), lift);
            }
        }

        protected void Paint(GroundPaint paint, in Footprint footprint, float softness) => B.Paint(paint, footprint, softness);

        // ---------------------------------------------------------------- What's solid

        protected void Solid(in Footprint footprint, float height = PlacementIndex.Tall) => World.Placement.Add(footprint, PlacementLayer.Prop, solid: true, height: height);
        protected void Keepout(in Footprint footprint) => World.Placement.Add(footprint, PlacementLayer.Keepout);

        /// <summary>
        /// The seal along a run of the walls round the floor, from one point to the next, <paramref name="thick"/> deep behind the line: solid all the way up, its joints overlapping so that nothing can slip
        /// through a corner. The line is the inside of the seal, which is what <see cref="ArenaPlan.Bounds"/> follows.
        /// </summary>
        protected void SealLine(IReadOnlyList<Vector2> points, float thick, bool closed)
        {
            thick = MathF.Max(thick, LeastSeal);
            int count = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                Vector2 along = b - a;
                float length = along.Length;
                if (length < 0.01f) continue;
                along /= length;
                // Behind the line is to its right, going round the floor counter-clockwise from above... whichever way round, the layout gives the outward side by the order of its points: here,
                // outward is away from the arena's middle.
                Vector2 normal = new Vector2(-along.Y, along.X);
                Vector2 mid = (a + b) * 0.5f;
                if (Vector2.Dot(normal, mid) < 0f) normal = -normal;
                Vector2 extend = along * (thick * 0.5f);
                Solid(Footprint.Segment(C + a - extend + normal * (thick * 0.5f), C + b + extend + normal * (thick * 0.5f), thick * 0.5f));
            }
        }

        /// <summary>The seal round an arc of a circle about the middle, from one bearing through a sweep.</summary>
        protected void SealArc(float radius, float from, float sweep, float thick)
        {
            var points = new List<Vector2>();
            int pieces = Math.Max(2, (int)MathF.Ceiling(MathHelper.DegreesToRadians(MathF.Abs(sweep)) * radius / 1.5f));
            for (int i = 0; i <= pieces; i++) points.Add(Toward(from + sweep * i / pieces) * radius);
            SealLine(points, thick, closed: false);
        }

        /// <summary>Points round an arc of a circle about the middle, for the plan's bounds.</summary>
        protected static IEnumerable<Vector2> ArcPoints(float radius, float from, float sweep, float step = 4f)
        {
            int pieces = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(sweep) / step));
            for (int i = 0; i <= pieces; i++) yield return Toward(from + sweep * i / pieces) * radius;
        }

        // ---------------------------------------------------------------- Pieces

        protected uint NextSeed()
        {
            _seed = _seed * 0x9E3779B1u + 0x7F4A7C15u;
            return _seed >> 4;
        }

        protected static int Cm(float metres) => ArenaSpec.Cm(metres);

        protected static Vector2 Toward(float degrees) => new Vector2(MathF.Sin(MathHelper.DegreesToRadians(degrees)), MathF.Cos(MathHelper.DegreesToRadians(degrees)));

        protected static float Bearing(Vector2 direction)
        {
            float b = MathHelper.RadiansToDegrees(MathF.Atan2(direction.X, direction.Y));
            return b < 0f ? b + 360f : b;
        }

        /// <summary>A piece's model, <paramref name="at"/> from the middle, turned by <paramref name="yaw"/>, standing <paramref name="lift"/> above the floor.</summary>
        protected string Piece(in ArenaSpec spec, Vector2 at, float yaw, float lift)
        {
            string id = spec.Id;
            World.Pending.Add(new PendingObject(id, C + at, yaw, Vector3.One, F + lift));
            return id;
        }

        /// <summary>A wall round an arc about <paramref name="centre"/>, from a bearing through a sweep, its face out (1) or in (-1), standing on ground <paramref name="lift"/> above the floor.</summary>
        protected void ArcWall(ArenaWallForm form, ArenaStoneKind stone, Vector2 centre, float radius, float from, float sweep, float top, float thick, float soil, float facing, ArenaTrim trim, float lift = 0f)
        {
            Piece(new ArenaSpec(ArenaShape.Arc, NextSeed(), (int)form, (int)stone, Cm(radius), (int)MathF.Round(from), (int)MathF.Round(sweep), Cm(top), Cm(thick), soil < 0f ? -1 : Cm(soil), (int)facing, (int)trim),
                centre, 0f, lift);
        }

        /// <summary>A wall along a straight line from one point to another, its face towards <paramref name="face"/>, standing on ground <paramref name="lift"/> above the floor.</summary>
        protected void RunWall(ArenaWallForm form, ArenaStoneKind stone, Vector2 a, Vector2 b, Vector2 face, float top, float thick, float soil, ArenaTrim trim, float lift = 0f)
        {
            float yaw = YawFacing(face);
            Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
            if (Vector2.Dot(b - a, along) < 0f) (a, b) = (b, a);
            Piece(new ArenaSpec(ArenaShape.Run, NextSeed(), (int)form, (int)stone, Cm((b - a).Length), Cm(top), Cm(thick), soil < 0f ? -1 : Cm(soil), (int)trim), a, yaw, lift);
        }

        /// <summary>A pylon, solid, <paramref name="lift"/> above the floor.</summary>
        protected void Pylon(ArenaStoneKind stone, Vector2 at, float width, float height, int crown, float yaw, float lift)
        {
            Piece(new ArenaSpec(ArenaShape.Pylon, NextSeed(), (int)stone, Cm(width), Cm(height), crown), at, yaw, lift);
            Solid(Footprint.Box(C + at, new Vector2(width * 0.5f + 0.25f), yaw));
        }

        protected void Brazier(Vector2 at, int form, float size, float lift)
        {
            Piece(new ArenaSpec(ArenaShape.Brazier, NextSeed(), form, (int)MathF.Round(size * 100f)), at, Random.Float() * MathF.Tau, lift);
            Solid(Footprint.Circle(C + at, 0.8f * size), 2f);
        }

        protected void Lamp(int form, Vector2 at, float yaw, float lift) => Piece(new ArenaSpec(ArenaShape.Lamp, NextSeed(), form), at, yaw, lift);

        // ---------------------------------------------------------------- The waygate

        /// <summary>
        /// The waygate, in the bay at <paramref name="at"/>, facing <paramref name="facing"/> across the floor: the Reich builds its arenas round the land's old roads, to show the land whose they are, and
        /// whoever comes through comes out on the sand. Placed first, as a site is nothing without it.
        /// </summary>
        protected Vector2 Waygate(Vector2 at, Vector2 facing)
        {
            Vector2 gate = C + at;
            if (B.PlaceNear(StructureCatalog.Waygate, C + at, YawFacing(facing), 3f, out Vector2 placed, padding: 0.3f)) gate = placed;
            B.Spawn(SpawnRole.Waygate, gate + facing * 2.5f);
            return gate - C;
        }

        /// <summary>The tyrant's place on the floor, and beside it a second's, for orders that ask for two.</summary>
        protected void Tyrants(Vector2 boss, Vector2 beside)
        {
            B.Spawn(SpawnRole.Boss, boss);
            B.Spawn(SpawnRole.Rival, beside);
        }

        // Free-standing pillars on a floor: as tall as two people, so that each stops whatever's aimed through it and hides whoever's behind it from a blow (Sight), and low enough that the camera
        // still sees over one, but not so wide that someone behind it is lost.
        protected const float PillarWidth = 1.9f;
        protected const float PillarHeight = 3.4f;

        /// <summary>
        /// Pillars to fight round, <paramref name="count"/> of them evenly round a ring about the middle, starting at a bearing, each left out where it would stand within <paramref name="clear"/> (the
        /// waygate, the tyrant, where the brood comes in, the set pieces), so that a floor with them is somewhere to break a line of sight and a floor without is somewhere to run.
        /// </summary>
        protected int Pillars(ArenaStoneKind stone, int count, float ring, float from, IReadOnlyList<(Vector2 At, float Radius)> clear)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 at = Toward(from + i * 360f / count) * ring;
                if (!Plan.Contains(C + at) || clear.Any(c => (C + at - c.At).Length < c.Radius + PillarWidth)) continue;
                Pylon(stone, at, PillarWidth, PillarHeight, 0, YawFacing(-at), 0f);
                placed++;
            }
            return placed;
        }

        /// <summary>
        /// The share of a stand's places that are filled, so that a larger arena is no more crowded than its size needs: the land's people are each a body to draw and animate, and what reads at a
        /// glance is a full stand, not how many are in it. <paramref name="usual"/> is the share at <paramref name="usualRound"/> metres across, and it thins in proportion past that, so that a stand
        /// holds about as many however large it's grown.
        /// </summary>
        protected static float Crowd(float usual, float round, float usualRound) => usual * MathF.Min(1f, usualRound / MathF.Max(round, 1f));

        // ---------------------------------------------------------------- What's on the floor

        /// <summary>
        /// What's left of the fights before on a floor, inside the seal once its bounds are laid: old blood soaked dark into it here and there, and the bones of what was fed to the tyrant along the
        /// foot of its walls, kept clear of <paramref name="clear"/> (the waygate, the ways the brood comes in), none of it solid, as a fight goes over it.
        /// </summary>
        protected void Remains(int stains, int piles, IReadOnlyList<(Vector2 At, float Radius)> clear)
        {
            if (Plan.Bounds.Count < 3) return;
            bool Clear(Vector2 at) => clear.All(c => (C + at - c.At).Length > c.Radius);
            Vector2 least = new Vector2(float.MaxValue), most = new Vector2(float.MinValue);
            foreach (Vector2 p in Plan.Bounds) { least = Vector2.ComponentMin(least, p); most = Vector2.ComponentMax(most, p); }
            for (int i = 0, placed = 0; i < stains * 6 && placed < stains; i++)
            {
                Vector2 at = new Vector2(Random.Range(least.X, most.X), Random.Range(least.Y, most.Y)) - C;
                if (!Inside(C + at, 3.5f) || !Clear(at)) continue;
                Piece(new ArenaSpec(ArenaShape.Remains, NextSeed(), 0, (int)MathF.Round(Random.Range(0.8f, 1.4f) * 100f)), at, Random.Float() * MathF.Tau, 0f);
                placed++;
            }
            for (int i = 0, placed = 0; i < piles * 8 && placed < piles; i++)
            {
                int k = Random.Int(Plan.Bounds.Count);
                Vector2 a = Plan.Bounds[k], b = Plan.Bounds[(k + 1) % Plan.Bounds.Count];
                Vector2 edge = Vector2.Lerp(a, b, Random.Float());
                Vector2 inward = (Plan.Centre - edge).Normalized();
                Vector2 at = edge + inward * 2.6f - C;
                if (!Inside(C + at, 1.5f) || !Clear(at)) continue;
                Piece(new ArenaSpec(ArenaShape.Remains, NextSeed(), 1, (int)MathF.Round(Random.Range(0.8f, 1.15f) * 100f)), at, Random.Float() * MathF.Tau, 0f);
                placed++;
            }
        }

        // Whether a point is inside the seal by at least so much.
        private bool Inside(Vector2 point, float margin)
        {
            if (!Plan.Contains(point)) return false;
            for (int i = 0; i < Plan.Bounds.Count; i++)
            {
                Vector2 a = Plan.Bounds[i], b = Plan.Bounds[(i + 1) % Plan.Bounds.Count], ab = b - a;
                float t = ab.LengthSquared > 1e-6f ? Math.Clamp(Vector2.Dot(point - a, ab) / ab.LengthSquared, 0f, 1f) : 0f;
                if ((point - (a + ab * t)).Length < margin) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- People

        /// <summary>The land's people, made to watch from a tier at a radius, every so many degrees round it but where nobody may stand, with a share of the places empty.</summary>
        protected void Watchers(float radius, float step, float offset, Func<float, bool> clear, float share = 0.72f, float spread = 0.35f)
        {
            for (float bearing = offset; bearing < 360f + offset - 0.01f; bearing += step)
            {
                float b = bearing % 360f;
                if (!clear(b)) continue;
                if (!Random.Chance(share)) continue;
                float jitter = Random.Range(-step * 0.2f, step * 0.2f);
                B.Spawn(SpawnRole.Spectator, C + Toward(b + jitter) * (radius + Random.Range(-spread, spread)));
            }
        }

        /// <summary>Whether a bearing is within <paramref name="half"/> degrees of another.</summary>
        protected static bool Near(float bearing, float of, float half)
        {
            float d = MathF.Abs(((bearing - of) % 360f + 540f) % 360f - 180f);
            return d < half;
        }
    }
}
