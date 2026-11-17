using Dino_Engine.Modelling.Procedural.Architecture.Places;
using Dino_Engine.Modelling.Procedural.Architecture.Plaza;
using Dino_Engine.Modelling.Procedural.Architecture.Town;
using Dino_Engine.Modelling.Procedural.Fence;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using static Dino_Engine.World.Generation.SiteBuilder;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// What the Haven's town is laid out with, beyond its square (HAVEN.md): walls that hold ground up, flights of steps cut into them, kerbs, pots and planting, as
    /// <see cref="HavenSquare"/> lays its own, but each at whatever level it's told, as the town stands on several, and the pieces of the town itself
    /// (<see cref="TownSpec"/>) and of the places with a story (<see cref="PlaceSpec"/>, PLACES.md), each put down with what's solid about it and nothing more. Positions are
    /// metres east and north of the town's middle, and heights are where people stand, above the sea. Every piece draws its seed from one stream, in the order it's laid.
    /// </summary>
    internal sealed class TownMason
    {
        /// <summary>How far inside a wall's face the ground it holds up stops, as a grid of heights a metre apart lays a step as a slope (SQUARE.md).</summary>
        public const float Inset = 1.47f;
        public const float Sharp = 0.02f;
        public const float Rise = 0.4f;             // A step's rise and tread at a person's 4.6 m.
        public const float Tread = 1f;
        public const float Parapet = 0.85f;
        private const float LongestRun = 13f;       // Metres of wall in one piece, so that a long wall is culled and built in parts.

        public readonly SiteBuilder B;
        public readonly Vector2 C;
        public readonly WorldRandom Random;
        private uint _seed;
        private readonly List<HitPart> _parts = new();

        public TownMason(SiteBuilder b, string name, uint seed)
        {
            B = b;
            C = b.Centre;
            Random = b.Random.Fork(name);
            _seed = seed;
        }

        public WorldData World => B.World;

        public uint NextSeed()
        {
            _seed = _seed * 0x9E3779B1u + 0x7F4A7C15u;
            return _seed >> 4;
        }

        public static int Cm(float metres) => PlazaSpec.Cm(metres);

        /// <summary>The direction of a bearing, clockwise from north.</summary>
        public static Vector2 Toward(float degrees) => new Vector2(MathF.Sin(MathHelper.DegreesToRadians(degrees)), MathF.Cos(MathHelper.DegreesToRadians(degrees)));

        public Vector2 At(Vector2 local) => C + local;

        // ---------------------------------------------------------------- Ground

        public void Solid(in Footprint footprint, float height) => World.Placement.Add(footprint, PlacementLayer.Prop, solid: true, height: height);

        /// <summary>Something in the way that lays out no ground of its own, as a bench by a street doesn't: nothing's kept off it but whoever walks into it.</summary>
        public void Block(in Footprint footprint, float height) => World.Placement.Add(footprint, PlacementLayer.None, solid: true, height: height);
        public void Garden(in Footprint footprint) => World.Placement.Add(footprint, PlacementLayer.Garden);
        public void Keep(in Footprint footprint) => World.Placement.Add(footprint, PlacementLayer.Keepout);

        /// <summary>Ground laid exactly to a height, its edges sharp, for walls to hide (<see cref="WorldData.Terraces"/>).</summary>
        public void Raise(in Footprint footprint, float height) => World.Terraces.Add(new GroundPad(footprint, height, Sharp));

        /// <summary>Ground levelled to a height before the roads are cut, easing back into the land over <paramref name="margin"/>.</summary>
        public void Level(in Footprint footprint, float height, float margin) => World.Pads.Add(new GroundPad(footprint, height, margin));

        public void Paint(GroundPaint paint, in Footprint shape, float softness) => B.Paint(paint, shape, softness);

        public Footprint Box(Vector2 from, Vector2 to) => Footprint.Box(C + (from + to) * 0.5f, new Vector2(MathF.Abs(to.X - from.X), MathF.Abs(to.Y - from.Y)) * 0.5f, 0f);

        public Footprint Circle(Vector2 at, float radius) => Footprint.Circle(C + at, radius);

        // ---------------------------------------------------------------- Pieces of the square's kind

        /// <summary>A piece's model at <paramref name="at"/>, turned by <paramref name="yaw"/>, its y = 0 at <paramref name="height"/>.</summary>
        public void Piece(in PlazaSpec spec, Vector2 at, float yaw, float height)
        {
            World.Pending.Add(new PendingObject(spec.Id, C + at, yaw, Vector3.One, height));
        }

        /// <summary>
        /// A wall along a straight line from one point to another, its face towards <paramref name="face"/>, standing on <paramref name="level"/>: <paramref name="top"/> and
        /// <paramref name="soil"/> are metres above that, and a soil under nothing means it holds nothing up. Laid in pieces no longer than a wall should be in one.
        /// </summary>
        public void Run(Vector2 a, Vector2 b, Vector2 face, WallForm form, float top, float soil, float thick, float level)
        {
            float yaw = YawFacing(face);
            Vector2 along = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
            if (Vector2.Dot(b - a, along) < 0f) (a, b) = (b, a);
            float length = (b - a).Length;
            if (length < 0.3f) return;
            int pieces = Math.Max(1, (int)MathF.Ceiling(length / LongestRun));
            for (int i = 0; i < pieces; i++)
            {
                Vector2 from = Vector2.Lerp(a, b, i / (float)pieces), to = Vector2.Lerp(a, b, (i + 1f) / pieces);
                Piece(new PlazaSpec(PlazaKind.Run, NextSeed(), (int)form, Cm((to - from).Length), Cm(top), soil < 0f ? -1 : Cm(soil), Cm(thick)), from, yaw, level);
            }
            Vector2 back = -face.Normalized() * (thick * 0.5f);
            Solid(Footprint.Segment(C + a + back, C + b + back, thick * 0.5f + 0.05f), top - PlazaStone.Ground);
        }

        /// <summary>A wall round an arc about <paramref name="centre"/>, from a bearing through <paramref name="sweep"/> degrees clockwise, its face out (1) or in (-1).</summary>
        public void Arc(Vector2 centre, float radius, float from, float sweep, float facing, WallForm form, float top, float soil, float thick, float level)
        {
            Piece(new PlazaSpec(PlazaKind.Arc, NextSeed(), (int)form, Cm(radius), (int)MathF.Round(from), (int)MathF.Round(sweep), Cm(top), soil < 0f ? -1 : Cm(soil), Cm(thick), (int)facing), centre, 0f, level);
            float middle = radius - facing * thick * 0.5f;
            int pieces = Math.Max(1, (int)MathF.Ceiling(MathHelper.DegreesToRadians(MathF.Abs(sweep)) * middle / 1.2f));
            for (int i = 0; i < pieces; i++)
            {
                Vector2 p0 = centre + Toward(from + sweep * i / pieces) * middle, p1 = centre + Toward(from + sweep * (i + 1) / pieces) * middle;
                Solid(Footprint.Segment(C + p0, C + p1, thick * 0.5f + 0.05f), top - PlazaStone.Ground);
            }
        }

        /// <summary>A square pier standing on <paramref name="level"/>, with a lantern (1) or an urn (2) on it, or nothing.</summary>
        public void Pier(Vector2 at, float width, float top, int crown, WorldAsset? flowers, float level)
        {
            Piece(new PlazaSpec(PlazaKind.Pier, NextSeed(), Cm(width), Cm(top), crown), at, 0f, level);
            Solid(Footprint.Box(C + at, new Vector2(width * 0.5f + 0.1f), 0f), PlacementIndex.Tall);
            if (crown == 2 && flowers != null) Flower(flowers, at, level + top + PlazaPieces.UrnSoil * width * 0.36f, 0.9f);
        }

        /// <summary>
        /// A flight of steps rising towards <paramref name="up"/> from the middle of its bottom riser at <paramref name="foot"/>, cut into the ground it climbs to, between cheek
        /// walls that stand <paramref name="top"/> above the lower ground, with piers out in front and the Haven's lanterns on them. The ground under it is laid to its pitch
        /// (SQUARE.md). <paramref name="level"/> is where people stand at its foot.
        /// </summary>
        public void Flight(Vector2 foot, Vector2 up, float halfWidth, int steps, float top, float level, bool lamps = true, float cheek = 1.4f, float landing = 0.8f, float pierOut = 1.2f)
        {
            up = up.Normalized();
            float yaw = YawFacing(-up);
            Vector2 across = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));      // The piece's own x on the ground; its z is -up.
            Piece(new PlazaSpec(PlazaKind.Steps, NextSeed(), Cm(halfWidth * 2f), steps, Cm(Rise), Cm(Tread), Cm(cheek), Cm(top), Cm(landing), Cm(pierOut)), foot, yaw, level);
            if (lamps)
            {
                foreach ((Vector3 at, float scale) in PlazaPieces.FlightLamps(halfWidth * 2f, steps, Tread, cheek, top, landing, pierOut))
                {
                    Piece(new PlazaSpec(PlazaKind.Lamp, NextSeed(), 0, (int)MathF.Round(scale * 100f)), foot + across * at.X - up * at.Z, yaw, level + at.Y);
                }
            }

            float length = steps * Tread + landing;
            float boxYaw = YawFacing(up);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector2 middle = foot + across * (s * (halfWidth + cheek * 0.5f)) + up * ((length - pierOut) * 0.5f);
                Solid(Footprint.Box(C + middle, new Vector2(cheek * 0.5f + 0.05f, (length + pierOut) * 0.5f), boxYaw), top);
            }
            float pitch = Rise / Tread;
            Vector2 ramp = foot + up * (steps * Tread * 0.5f);
            Footprint under = Footprint.Box(C + ramp, new Vector2(halfWidth, steps * Tread * 0.5f), boxYaw);
            World.Terraces.Add(new GroundPad(under, level + pitch * steps * Tread * 0.5f - 0.01f, Sharp, up * pitch));
            Keep(under);
        }

        /// <summary>Stones laid flush in the ground from one point to another, <paramref name="width"/> across: a band of kerbs, or a gutter.</summary>
        public void Band(Vector2 from, Vector2 to, float width, float level, bool gutter = false)
        {
            Vector2 run = to - from;
            float length = run.Length;
            if (length < 0.4f) return;
            // A line's model runs along +x, which this turns to the way the line goes.
            float yaw = MathF.Atan2(-run.Y, run.X);
            int pieces = Math.Max(1, (int)MathF.Ceiling(length / 16f));
            for (int i = 0; i < pieces; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, i / (float)pieces), b = Vector2.Lerp(from, to, (i + 1f) / pieces);
                Piece(new PlazaSpec(PlazaKind.Line, NextSeed(), gutter ? 1 : 0, Cm((b - a).Length), Cm(width)), a, yaw, level);
            }
        }

        /// <summary>Kerbs all round a box of paving, each side a band.</summary>
        public void Border(Vector2 from, Vector2 to, float width, float level)
        {
            Vector2 min = Vector2.ComponentMin(from, to), max = Vector2.ComponentMax(from, to);
            Band(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), width, level);
            Band(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), width, level);
            Band(new Vector2(min.X, min.Y), new Vector2(min.X, max.Y), width, level);
            Band(new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), width, level);
        }

        // ---------------------------------------------------------------- Pots and planting

        /// <summary>A pot standing on the ground at <paramref name="level"/>, with something growing in it.</summary>
        public void PotAt(Vector2 at, int form, float height, WorldAsset plant, float level)
        {
            Piece(new PlazaSpec(PlazaKind.Pot, NextSeed(), form, Cm(height)), at, Random.Float() * MathF.Tau, level);
            if (height >= Hitboxes.StepOver) Solid(Footprint.Circle(C + at, height * 0.5f + 0.1f), height);
            Flower(plant, at, level + PlazaDressing.PotSoil(form, height), 0.8f);
        }

        /// <summary>A pot on top of something whose top is <paramref name="on"/> above the sea, such as a wall's cope.</summary>
        public void PotOn(Vector2 at, float on, int form, float height, WorldAsset plant)
        {
            float foot = on - PlazaStone.Ground;
            Piece(new PlazaSpec(PlazaKind.Pot, NextSeed(), form, Cm(height)), at, Random.Float() * MathF.Tau, foot);
            Flower(plant, at, foot + PlazaDressing.PotSoil(form, height), 0.75f);
        }

        /// <summary>Something growing that stands on a piece rather than on the ground, placed at a height rather than planted.</summary>
        public void Flower(WorldAsset asset, Vector2 at, float height, float scale)
        {
            Vector2 world = C + at;
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, world), world, Random.Float() * MathF.Tau, new Vector3(scale), height - 0.04f));
        }

        /// <summary>
        /// A plant grown where it stands, on ground at <paramref name="height"/>, <paramref name="scale"/> times its kind's size, as a town keeps its trees smaller than the woods do
        /// (SQUARE.md). Its trunk is solid and its room kept, so the woods keep clear of it.
        /// </summary>
        public void Grow(WorldAsset asset, Vector2 at, float scale, float height)
        {
            Vector2 world = C + at;
            bool tree = asset.Kind == WorldAssetKind.Tree;
            World.Placement.Add(Footprint.Circle(world, tree ? asset.SolidRadius * scale + 0.5f : MathF.Min(asset.Footprint * scale, 0.8f)), tree ? PlacementLayer.Tree : PlacementLayer.Shrub);
            if (asset.SolidRadius > 0f) World.Placement.Add(Footprint.Circle(world, asset.SolidRadius * scale), PlacementLayer.None, solid: true);
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, world), world, Random.Float() * MathF.Tau, new Vector3(scale), height - (tree ? 0.15f : 0.05f)));
        }

        /// <summary>A plant grown wherever the ground ends up, as on a bank or a lawn that nothing levels exactly.</summary>
        public void GrowOnGround(WorldAsset asset, Vector2 at, float scale)
        {
            Vector2 world = C + at;
            bool tree = asset.Kind == WorldAssetKind.Tree;
            World.Placement.Add(Footprint.Circle(world, tree ? asset.SolidRadius * scale + 0.5f : MathF.Min(asset.Footprint * scale, 0.8f)), tree ? PlacementLayer.Tree : PlacementLayer.Shrub);
            if (asset.SolidRadius > 0f) World.Placement.Add(Footprint.Circle(world, asset.SolidRadius * scale), PlacementLayer.None, solid: true);
            World.Pending.Add(new PendingObject(NatureCatalog.ModelAt(asset, World.Plan.Seed, world), world, Random.Float() * MathF.Tau, new Vector3(scale), null));
        }

        // ---------------------------------------------------------------- Pieces of the town's own, and of the places with a story

        /// <summary>A piece of the town, its y = 0 at <paramref name="height"/>, or on the ground wherever it ends up. What's solid about it is the caller's to say.</summary>
        public void Town(in TownSpec spec, Vector2 at, float yaw, float? height)
        {
            World.Pending.Add(new PendingObject(spec.Id, C + at, yaw, Vector3.One, height));
        }

        /// <summary>
        /// A piece of a place with a story (PLACES.md), kept to the footprint it stands on and as solid as its own rule says (<see cref="Hitboxes"/>). Nothing's checked: the
        /// town is laid out to the metre, and what's put down is meant to be there.
        /// </summary>
        public void Place(in PlaceSpec spec, Vector2 at, float yaw, Vector2 half, float tall, float? height = null, bool solid = true, float scale = 1f)
        {
            Footprint footprint = half.Y <= 0f ? Footprint.Circle(C + at, half.X * scale) : Footprint.Box(C + at, half * scale, yaw);
            HitKind kind = solid ? Hitboxes.Of(spec, _parts) : HitKind.Open;
            Hitboxes.Put(World.Placement, kind, _parts, footprint, PlacementLayer.Prop, tall, C + at, yaw, scale);
            World.Pending.Add(new PendingObject(spec.Id, C + at, yaw, new Vector3(scale), height));
        }

        /// <summary>Something lying flat that nothing walks round, put down with no footprint at all: litter, a puddle, a cloth.</summary>
        public void Lay(in PlaceSpec spec, Vector2 at, float yaw, float? height = null)
        {
            World.Pending.Add(new PendingObject(spec.Id, C + at, yaw, Vector3.One, height));
        }

        /// <summary>Litter of a kind over a patch (PLACES.md), which says what's done there.</summary>
        public void Strew(LitterKind kind, Vector2 at, float radius, int count, float? height = null)
        {
            Lay(new PlaceSpec(PlacePiece.Litter, NextSeed(), (int)kind, PlaceSpec.Cm(MathF.Min(radius, 4.5f)), count), at, Random.Float() * MathF.Tau, height);
        }

        /// <summary>A structure of the catalogue put where it's wanted, as solid as its rule says, with no questions asked of the ground.</summary>
        public void Thing(StructureDefinition definition, Vector2 at, float yaw, float? height = null)
        {
            Footprint footprint = definition.FootprintAt(C + at, yaw);
            PlacementLayer layer = definition.OnPad ? PlacementLayer.Structure : PlacementLayer.Prop;
            Hitboxes.Put(World.Placement, Hitboxes.Of(definition, _parts), _parts, footprint, layer, definition.Height, C + at, yaw);
            World.Pending.Add(new PendingObject(StructureCatalog.AssetId(definition, B.Style, Random.Int(Math.Max(definition.Variants, 1))), C + at, yaw, Vector3.One, height));
        }

        /// <summary>A run of fence of a chosen kind, in the town's one style of it, its inside towards <paramref name="inside"/>.</summary>
        public void Fence(FenceKind kind, Vector2 from, Vector2 to, Vector2 inside)
        {
            Vector2 a = C + from, b = C + to;
            Vector2 span = b - a;
            if (span.Length < 0.8f) return;
            ulong style = FenceCatalog.StyleSeed(World.Plan.Seed, kind, 0);
            if (Vector2.Dot(new Vector2(-span.Y, span.X), C + inside - (a + b) * 0.5f) < 0f) (a, b) = (b, a);
            World.Placement.Add(Footprint.Segment(a, b, kind.HalfThickness), PlacementLayer.Fence, solid: true, height: kind.Height);
            World.Fences.Add(new FenceRun(kind, style, a, b));
        }

        /// <summary>Someone of the town, standing where they're put.</summary>
        public void Stand(SpawnRole role, Vector2 at) => B.Spawn(role, C + at);
    }
}
