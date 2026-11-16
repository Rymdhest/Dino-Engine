using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>What a thing is to whoever walks into it: in the way, stepped over, or broken.</summary>
    public enum HitKind
    {
        Solid,      // In the way, where its parts say and nowhere else.
        Open,       // Walked over or through: lower than a knee, flat, hung up out of the way, or strewn.
        Loose,      // Clutter a body or a blast goes through and breaks, on each client and for show alone (BreakageSystem).
        Layout,     // Solid all over the ground it's laid out on, as a building is: its footprint is its hitbox.
    }

    /// <summary>
    /// One part of what's solid about a thing, in its own frame, as its model is built: x across, +z its front, metres. A box of half size <see cref="Half"/> turned by
    /// <see cref="Yaw"/> about <see cref="Centre"/>, or a circle of radius Half.X when Half.Y is 0. It's solid up to <see cref="Height"/>, which is what a blow or what's thrown
    /// goes over (Sight.Above), and 0 for what's seen through, a post, a rack, a wheel, which is walked round and stops nothing in the air.
    /// </summary>
    public readonly struct HitPart
    {
        public readonly Vector2 Centre;
        public readonly Vector2 Half;
        public readonly float Yaw;
        public readonly float Height;

        public HitPart(Vector2 centre, Vector2 half, float height, float yaw = 0f)
        {
            Centre = centre;
            Half = half;
            Height = height;
            Yaw = yaw;
        }

        public bool IsRound => Half.Y == 0f;

        /// <summary>Where it is in the world, for a thing standing at <paramref name="position"/>, turned by <paramref name="yaw"/> as its model is, and grown by <paramref name="scale"/>.</summary>
        public Footprint At(Vector2 position, float yaw, float scale = 1f)
        {
            Vector2 x = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)), z = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
            Vector2 centre = position + (x * Centre.X + z * Centre.Y) * scale;
            return IsRound ? Footprint.Circle(centre, Half.X * scale) : Footprint.Box(centre, Half * scale, yaw + Yaw);
        }
    }

    /// <summary>
    /// Clutter that breaks: what it breaks into (the catalog's <see cref="StructureDefinition"/> whose pieces it throws, PropGenerator.Pieces), how far from where it stands a body
    /// breaks it by walking into it and its pieces fly from, and how tall it is, which is where they're thrown from.
    /// </summary>
    public sealed record LooseKind(StructureDefinition Pieces, float Reach, float Height);

    /// <summary>
    /// What of everything built is in a body's way and a blow's: far less than the ground it's laid out on, which keeps everything else off it. A thing's footprint in the
    /// <see cref="PlacementIndex"/> is where nothing else may be put; its hitboxes are what a character walks into, what passable ground goes round and what hides someone
    /// from a blow, and they're inside what's drawn, never round it: a person walking round air, or shut out of a gap they can see through, is what this is against, as a
    /// person found a camp they couldn't get about in, a tent and a cart with a gap between them nobody could use and a cart's pole as wide as its wheels. So anything lower
    /// than a knee is stepped over, a barrel or a crate breaks when it's walked into, posts, racks and wheels are walked round and seen through, and whatever really stands
    /// in the way is solid where it stands and nowhere else. Every kind of thing has a rule, measured off its model, and `Dino WorldLab hitboxes` holds every one to what's drawn.
    /// Headless, and the same on every machine, as both ends of a game read the same world.
    /// </summary>
    public static partial class Hitboxes
    {
        /// <summary>Metres up something has to stand before it's in anyone's way: a little over a person's knee (BodyForm.Knee, 1 m), so a log, a stump or a low stone is stepped over.</summary>
        public const float StepOver = 1.25f;

        /// <summary>Puts a thing's parts into <paramref name="placement"/>, solid and on no layer, so nothing's laid out by them and everything that moves minds them.</summary>
        public static void Add(PlacementIndex placement, List<HitPart> parts, Vector2 position, float yaw, float scale = 1f)
        {
            foreach (HitPart part in parts) placement.Add(part.At(position, yaw, scale), PlacementLayer.None, solid: true, height: part.Height * scale);
        }

        /// <summary>
        /// Lays a thing out and makes it as solid as its rule says, for whatever's put down with a footprint of its own: <paramref name="layout"/> is the ground it keeps in
        /// <paramref name="layer"/>, never solid, and its <paramref name="kind"/>'s parts at <paramref name="position"/> and <paramref name="yaw"/> are what's in the way, or the
        /// footprint itself, solid up to <paramref name="height"/>, for a <see cref="HitKind.Layout"/>.
        /// </summary>
        public static void Put(PlacementIndex placement, HitKind kind, List<HitPart> parts, in Footprint layout, PlacementLayer layer, float height, Vector2 position, float yaw, float scale = 1f)
        {
            if (kind == HitKind.Layout)
            {
                placement.Add(layout, layer, solid: true, height: height);
                return;
            }
            placement.Add(layout, layer, solid: false, height: height);
            if (kind == HitKind.Solid) Add(placement, parts, position, yaw, scale);
        }

        // ---------------------------------------------------------------- Making parts

        /// <summary>A box of half size (<paramref name="hx"/>, <paramref name="hz"/>) about (<paramref name="x"/>, <paramref name="z"/>), solid up to <paramref name="height"/>: nothing if that's under a knee.</summary>
        private static HitKind Box(List<HitPart> parts, float hx, float hz, float height, float x = 0f, float z = 0f, float yaw = 0f)
        {
            if (height > 0f && height < StepOver) return Done(parts);
            parts.Add(new HitPart(new Vector2(x, z), new Vector2(MathF.Max(hx, 0.05f), MathF.Max(hz, 0.05f)), height, yaw));
            return HitKind.Solid;
        }

        /// <summary>A circle of <paramref name="radius"/> about (<paramref name="x"/>, <paramref name="z"/>), solid up to <paramref name="height"/>: nothing if that's under a knee.</summary>
        private static HitKind Round(List<HitPart> parts, float radius, float height, float x = 0f, float z = 0f)
        {
            if (height > 0f && height < StepOver) return Done(parts);
            parts.Add(new HitPart(new Vector2(x, z), new Vector2(MathF.Max(radius, 0.05f), 0f), height));
            return HitKind.Solid;
        }

        /// <summary>A post or a pole: walked round, and in nobody's way in the air.</summary>
        private static HitKind Post(List<HitPart> parts, float radius = 0.22f, float x = 0f, float z = 0f) => Round(parts, radius, 0f, x, z);

        /// <summary>A rack, a frame, a wheel or bars: walked round, and seen and struck through.</summary>
        private static HitKind Rack(List<HitPart> parts, float hx, float hz, float x = 0f, float z = 0f) => Box(parts, hx, hz, 0f, x, z);

        /// <summary>Solid if any part was made, open if every part was too low to be in anyone's way.</summary>
        private static HitKind Done(List<HitPart> parts) => parts.Count > 0 ? HitKind.Solid : HitKind.Open;

        /// <summary>Two of something, either side of the middle along x, as a gate's posts or an arch's piers.</summary>
        private static HitKind Pair(List<HitPart> parts, float hx, float hz, float height, float apart)
        {
            Box(parts, hx, hz, height, -apart);
            Box(parts, hx, hz, height, apart);
            return Done(parts);
        }
    }
}
