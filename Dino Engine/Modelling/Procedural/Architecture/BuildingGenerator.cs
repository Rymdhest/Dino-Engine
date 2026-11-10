using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    /// <summary>
    /// Builds houses to the sizes in <see cref="StructureCatalog"/>. A building is a plinth, walls, a door, windows and a roof, and
    /// a <see cref="BuildingStyle"/> decides what each of those looks like. The game is seen from above, so most of the effort
    /// goes into the roofs: that is what a village is, from where the player stands. Local +z is the front.
    /// </summary>
    public static class BuildingGenerator
    {
        private const float PlinthHeight = 0.7f;
        private const float DoorHeight = 6f;
        private const float DoorHalfWidth = 1.7f;
        private const float RoofLift = 0.8f;    // Metres the underside of a roof clears the top of its walls, lumps and all.

        private sealed class Kit
        {
            public VertexMaterial Wall;
            public VertexMaterial Frame;
            public VertexMaterial Roof;
            public VertexMaterial Plinth;
            public VertexMaterial Door;
            public VertexMaterial Window;
            public VertexMaterial Ridge;
            public float Pitch;
            public float Overhang;
            public bool Braced;         // Diagonal timbers across the walls.
            public float Lumps;         // How uneven the roof surface is, in metres.
            public float Upsweep;       // How far the corners of the eaves lift.
            public bool HipRoof;
            public List<AssetLight>? Lights;    // Where the windows shine from, for whoever asked.
        }

        private static Kit KitFor(BuildingStyle style, WorldRandom random)
        {
            VertexMaterial stone = new VertexMaterial(TextureGenerator.stoneWall, new Colour(150, 144, 136));
            // Windows glow faintly from inside, so that they read as lit rooms and not as holes.
            VertexMaterial window = new VertexMaterial(TextureGenerator.flatGlow, new Colour(1f, 0.6f, 0.26f, 0.5f));

            switch (style)
            {
                case BuildingStyle.Eastern:
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.plaster, new Colour(236, 230, 214)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.34f, 0.25f, 0.19f), random, 0.12f)),
                        Roof = new VertexMaterial(TextureGenerator.shingle, Shade(new Vector3(0.62f, 0.64f, 0.7f), random, 0.14f)),
                        Ridge = new VertexMaterial(TextureGenerator.shingle, new Colour(90, 92, 100)),
                        Plinth = stone,
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(76, 56, 42)),
                        Window = window,
                        Pitch = random.Range(0.62f, 0.78f),
                        Overhang = random.Range(1.9f, 2.5f),
                        Upsweep = random.Range(0.9f, 1.5f),
                        HipRoof = true,
                    };
                case BuildingStyle.Stone:
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.stoneWall, Shade(new Vector3(0.82f, 0.8f, 0.76f), random, 0.08f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, new Colour(92, 68, 50)),
                        Roof = new VertexMaterial(TextureGenerator.slate, Shade(new Vector3(0.82f, 0.84f, 0.9f), random, 0.12f)),
                        Ridge = new VertexMaterial(TextureGenerator.stoneWall, new Colour(120, 118, 114)),
                        Plinth = stone,
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(96, 66, 44)),
                        Window = window,
                        Pitch = random.Range(0.72f, 0.92f),
                        Overhang = random.Range(0.5f, 0.8f),
                    };
                default:
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.plaster, Shade(new Vector3(0.95f, 0.91f, 0.82f), random, 0.08f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.42f, 0.3f, 0.21f), random, 0.12f)),
                        Roof = new VertexMaterial(TextureGenerator.thatch, Shade(new Vector3(0.98f, 0.92f, 0.8f), random, 0.16f)),
                        Ridge = new VertexMaterial(TextureGenerator.thatch, new Colour(160, 138, 104)),
                        Plinth = stone,
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(102, 72, 50)),
                        Window = window,
                        Pitch = random.Range(0.98f, 1.24f),
                        Overhang = random.Range(1.0f, 1.4f),
                        Braced = true,
                        Lumps = 0.16f,
                    };
            }
        }

        private static Colour Shade(Vector3 colour, WorldRandom random, float variation)
        {
            float shade = 1f + random.Range(-variation, variation);
            return new Colour(colour * shade);
        }

        /// <summary>
        /// A building of the catalog in a style; <paramref name="variant"/> gives its look a little variety. The tops of its chimneys,
        /// in model space, are added to <paramref name="chimneys"/>, and the light from its windows to <paramref name="lights"/>.
        /// </summary>
        public static Mesh Generate(StructureDefinition definition, BuildingStyle style, int variant, List<Vector3>? chimneys = null, List<AssetLight>? lights = null)
        {
            var random = new WorldRandom(0x51ED).Fork(definition.Id).Fork((int)style * 16 + variant);
            Kit kit = KitFor(style, random);
            kit.Lights = lights;
            Vector2 half = definition.HalfExtents;

            if (definition == StructureCatalog.Shed) return BuildShed(half, kit);
            if (definition == StructureCatalog.Barn) return BuildBarn(half, kit, random);
            if (definition == StructureCatalog.Temple) return BuildTemple(half, kit, random);
            if (definition == StructureCatalog.Inn) return BuildHouse(half, 11.5f, kit, random, storeys: 2, count: 2, chimneys);
            if (definition == StructureCatalog.Hall) return BuildHouse(half, 8.5f, kit, random, storeys: 1, count: 2, chimneys);
            if (definition == StructureCatalog.Hut) return BuildHouse(half, 6.6f, kit, random, storeys: 1, count: 0, chimneys);
            if (definition == StructureCatalog.Longhouse) return BuildHouse(half, 7.2f, kit, random, storeys: 1, count: 1, chimneys);
            if (definition == StructureCatalog.House) return BuildHouse(half, 7.6f, kit, random, storeys: 1, count: 1, chimneys);
            return BuildHouse(half, 7f, kit, random, storeys: 1, count: 1, chimneys);
        }

        private static Mesh BuildHouse(Vector2 half, float wallHeight, Kit kit, WorldRandom random, int storeys, int count, List<Vector3>? chimneys)
        {
            var b = new MeshBuilder();
            AddPlinth(b, half, kit);
            float top = PlinthHeight + wallHeight;
            AddWalls(b, half, PlinthHeight, top, kit);
            AddFrame(b, half, PlinthHeight, top, kit, storeys);
            AddDoor(b, new Vector3(random.Range(-0.15f, 0.15f) * half.X, PlinthHeight, half.Y), kit);
            AddWindows(b, half, PlinthHeight, top, kit, storeys, random);
            AddRoof(b, half, top, kit, random);

            for (int i = 0; i < count; i++)
            {
                float x = (i == 0 ? 1f : -1f) * half.X * random.Range(0.3f, 0.62f);
                Vector3 at = new Vector3(x, 0f, random.Range(-0.3f, 0.3f) * half.Y);
                float chimneyTop = top + (half.Y + kit.Overhang) * kit.Pitch * 0.72f;
                AddChimney(b, at, chimneyTop, kit);
                chimneys?.Add(new Vector3(at.X, chimneyTop + 1.2f, at.Z));
            }
            return b.Build();
        }

        private static void AddPlinth(MeshBuilder b, Vector2 half, Kit kit)
        {
            // Reaches well below ground, so that a slope never shows daylight under the house.
            b.Box(new Vector3(0f, PlinthHeight - 2.5f, 0f), new Vector3(half.X + 0.28f, 2.5f, half.Y + 0.28f), kit.Plinth);
        }

        private static void AddWalls(MeshBuilder b, Vector2 half, float bottom, float top, Kit kit)
        {
            b.Box(new Vector3(0f, (bottom + top) * 0.5f, 0f), new Vector3(half.X, (top - bottom) * 0.5f, half.Y), kit.Wall);
        }

        private static void AddFrame(MeshBuilder b, Vector2 half, float bottom, float top, Kit kit, int storeys)
        {
            const float Thickness = 0.26f;
            float x = half.X + 0.04f;
            float z = half.Y + 0.04f;
            Vector3[] corners =
            {
                new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(x, 0f, -z), new Vector3(-x, 0f, -z),
            };

            for (int i = 0; i < 4; i++)
            {
                Vector3 from = corners[i];
                Vector3 to = corners[(i + 1) % 4];
                b.Beam(from + Vector3.UnitY * bottom, from + Vector3.UnitY * top, Thickness, kit.Frame);
                b.Beam(from + Vector3.UnitY * (bottom + 0.1f), to + Vector3.UnitY * (bottom + 0.1f), Thickness, kit.Frame);
                b.Beam(from + Vector3.UnitY * (top - 0.1f), to + Vector3.UnitY * (top - 0.1f), Thickness, kit.Frame);

                for (int storey = 1; storey < storeys; storey++)
                {
                    float y = bottom + (top - bottom) * storey / storeys;
                    b.Beam(from + Vector3.UnitY * y, to + Vector3.UnitY * y, Thickness, kit.Frame);
                }

                float length = (to - from).Length;
                int bays = Math.Max((int)MathF.Round(length / 4.2f), 1);
                for (int bay = 1; bay < bays; bay++)
                {
                    Vector3 at = Vector3.Lerp(from, to, (float)bay / bays);
                    // The middle of the front is the door's; a post there would run through it.
                    if (i == 0 && MathF.Abs(at.X) < DoorHalfWidth + 0.8f) continue;
                    b.Beam(at + Vector3.UnitY * bottom, at + Vector3.UnitY * top, Thickness * 0.8f, kit.Frame);
                }

                if (!kit.Braced) continue;
                for (int bay = 0; bay < bays; bay++)
                {
                    Vector3 start = Vector3.Lerp(from, to, (float)bay / bays);
                    Vector3 end = Vector3.Lerp(from, to, (float)(bay + 1) / bays);
                    if (i == 0 && MathF.Abs((start.X + end.X) * 0.5f) < DoorHalfWidth + 2f) continue;
                    Vector3 low = bay % 2 == 0 ? start : end;
                    Vector3 high = bay % 2 == 0 ? end : start;
                    b.Beam(low + Vector3.UnitY * (bottom + 0.2f), high + Vector3.UnitY * ((bottom + top) * 0.5f), Thickness * 0.65f, kit.Frame);
                }
            }
        }

        private static void AddDoor(MeshBuilder b, Vector3 foot, Kit kit)
        {
            float z = foot.Z + 0.07f;
            float left = foot.X - DoorHalfWidth;
            float right = foot.X + DoorHalfWidth;
            float bottom = foot.Y;
            float top = bottom + DoorHeight;
            b.Quad(new Vector3(left, bottom, z), new Vector3(right, bottom, z), new Vector3(right, top, z), new Vector3(left, top, z), kit.Door);

            float frame = z + 0.1f;
            b.Beam(new Vector3(left, bottom, frame), new Vector3(left, top, frame), 0.2f, kit.Frame);
            b.Beam(new Vector3(right, bottom, frame), new Vector3(right, top, frame), 0.2f, kit.Frame);
            b.Beam(new Vector3(left - 0.3f, top, frame), new Vector3(right + 0.3f, top, frame), 0.24f, kit.Frame);

            // A worn step up to the sill.
            b.Box(new Vector3(foot.X, foot.Y - 0.32f, foot.Z + 1.1f), new Vector3(DoorHalfWidth + 0.5f, 0.5f, 0.9f), kit.Plinth);
        }

        private static void AddWindows(MeshBuilder b, Vector2 half, float bottom, float top, Kit kit, int storeys, WorldRandom random)
        {
            float storeyHeight = (top - bottom) / storeys;
            for (int storey = 0; storey < storeys; storey++)
            {
                float y = bottom + storeyHeight * (storey + 0.56f);
                AddWallWindows(b, new Vector3(0f, y, half.Y), Vector3.UnitX, half.X, storey == 0 ? DoorHalfWidth + 1.6f : 0f, kit, random);
                AddWallWindows(b, new Vector3(0f, y, -half.Y), -Vector3.UnitX, half.X, 0f, kit, random);
                AddWallWindows(b, new Vector3(half.X, y, 0f), -Vector3.UnitZ, half.Y, 0f, kit, random);
                AddWallWindows(b, new Vector3(-half.X, y, 0f), Vector3.UnitZ, half.Y, 0f, kit, random);
            }
        }

        private static void AddWallWindows(MeshBuilder b, Vector3 middle, Vector3 along, float halfLength, float clearMiddle, Kit kit, WorldRandom random)
        {
            int count = Math.Max((int)(halfLength * 2f / 4.6f), 1);
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count * 2f - 1f;
                float offset = t * (halfLength - 1.1f);
                if (MathF.Abs(offset) < clearMiddle) continue;
                if (random.Chance(0.25f)) continue;
                AddWindow(b, middle + along * offset, along, kit);
            }
        }

        private static void AddWindow(MeshBuilder b, Vector3 centre, Vector3 along, Kit kit)
        {
            const float HalfWidth = 0.95f;
            const float HalfHeight = 1.1f;
            Vector3 up = Vector3.UnitY;
            Vector3 outward = Vector3.Cross(along, up);
            Vector3 face = centre + outward * 0.08f;

            b.Quad(face - along * HalfWidth - up * HalfHeight, face + along * HalfWidth - up * HalfHeight,
                   face + along * HalfWidth + up * HalfHeight, face - along * HalfWidth + up * HalfHeight, kit.Window);

            kit.Lights?.Add(AssetLight.Window(centre + outward * 0.5f, outward));

            Vector3 frame = centre + outward * 0.18f;
            b.Beam(frame - along * HalfWidth - up * HalfHeight, frame - along * HalfWidth + up * HalfHeight, 0.12f, kit.Frame);
            b.Beam(frame + along * HalfWidth - up * HalfHeight, frame + along * HalfWidth + up * HalfHeight, 0.12f, kit.Frame);
            b.Beam(frame - along * HalfWidth - up * HalfHeight, frame + along * HalfWidth - up * HalfHeight, 0.14f, kit.Frame);
            b.Beam(frame - along * HalfWidth + up * HalfHeight, frame + along * HalfWidth + up * HalfHeight, 0.14f, kit.Frame);
            b.Beam(frame - up * HalfHeight, frame + up * HalfHeight, 0.07f, kit.Frame);
            b.Beam(frame - along * HalfWidth, frame + along * HalfWidth, 0.07f, kit.Frame);
        }

        private static void AddChimney(MeshBuilder b, Vector3 at, float top, Kit kit)
        {
            b.Box(new Vector3(at.X, top * 0.5f + 2f, at.Z), new Vector3(0.75f, top * 0.5f - 1f, 0.75f), kit.Plinth);
            b.Box(new Vector3(at.X, top + 1.05f, at.Z), new Vector3(0.95f, 0.14f, 0.95f), kit.Plinth);
        }

        private static void AddRoof(MeshBuilder b, Vector2 half, float eaveHeight, Kit kit, WorldRandom random)
        {
            if (kit.HipRoof) AddHipRoof(b, half, eaveHeight, kit, kit.Roof);
            else AddGableRoof(b, half, eaveHeight, kit, random);
        }

        // A roof of two slopes with its ridge along x, and the wall carried up to it at each end.
        private static void AddGableRoof(MeshBuilder b, Vector2 half, float eaveHeight, Kit kit, WorldRandom random)
        {
            float overhang = kit.Overhang;
            float reach = half.Y + overhang;
            float rise = reach * kit.Pitch;
            // The roof sits on the wall plate, so it clears the top beams where it crosses the wall line.
            float eaveY = eaveHeight + RoofLift - overhang * kit.Pitch;
            float ridgeY = eaveY + rise;
            float ridgeHalf = half.X + overhang * 0.75f;

            float seedA = random.Float() * 20f;
            float seedB = random.Float() * 20f;

            // The two slopes, each drawn so its outside faces up and away from the ridge.
            b.Surface(12, 6, (u, v) => RoofPoint(u, v, ridgeHalf, reach, eaveY, ridgeY, kit.Lumps, seedA, 1f), kit.Roof);
            b.Surface(12, 6, (u, v) => RoofPoint(1f - u, v, ridgeHalf, reach, eaveY, ridgeY, kit.Lumps, seedB, -1f), kit.Roof);

            // A roof has thickness: the cut ends of the thatch along the eaves, and down the verges at either end.
            float lip = kit.Lumps > 0f ? 0.5f : 0.22f;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = reach * side;
                b.Quad(new Vector3(-ridgeHalf * side, eaveY - lip, z), new Vector3(ridgeHalf * side, eaveY - lip, z),
                       new Vector3(ridgeHalf * side, eaveY, z), new Vector3(-ridgeHalf * side, eaveY, z), kit.Roof);

                float x = ridgeHalf * side;
                Vector3 outer = new Vector3(x, eaveY, reach);
                Vector3 far = new Vector3(x, eaveY, -reach);
                Vector3 peak = new Vector3(x, ridgeY, 0f);
                Vector3 down = -Vector3.UnitY * lip;
                b.Quad(side > 0 ? outer + down : peak + down, side > 0 ? peak + down : outer + down, side > 0 ? peak : outer, side > 0 ? outer : peak, kit.Roof);
                b.Quad(side > 0 ? peak + down : far + down, side > 0 ? far + down : peak + down, side > 0 ? far : peak, side > 0 ? peak : far, kit.Roof);
            }

            // Gable ends.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = half.X * side;
                float wallTop = eaveY + overhang * kit.Pitch;
                Vector3 a = new Vector3(x, wallTop, half.Y * side);
                Vector3 c = new Vector3(x, wallTop, -half.Y * side);
                Vector3 peak = new Vector3(x, ridgeY - 0.1f, 0f);
                b.Triangle(a, c, peak, kit.Wall);
                b.Beam(a + Vector3.UnitX * side * 0.05f, peak + Vector3.UnitX * side * 0.05f, 0.2f, kit.Frame);
                b.Beam(c + Vector3.UnitX * side * 0.05f, peak + Vector3.UnitX * side * 0.05f, 0.2f, kit.Frame);
                b.Beam(new Vector3(x, wallTop, 0f) + Vector3.UnitX * side * 0.05f, peak + Vector3.UnitX * side * 0.05f, 0.18f, kit.Frame);
            }

            // Along the ridge, where the two slopes meet and the weather gets in.
            b.Tube(new Vector3(-ridgeHalf - 0.1f, ridgeY + 0.06f, 0f), new Vector3(ridgeHalf + 0.1f, ridgeY + 0.06f, 0f), 0.5f, 0.5f, 7, kit.Ridge);
        }

        // Thatch settles into soft courses, so its surface wanders a little rather than lying flat.
        private static Vector3 RoofPoint(float u, float v, float ridgeHalf, float reach, float eaveY, float ridgeY, float lumps, float seed, float side)
        {
            float x = (u * 2f - 1f) * ridgeHalf;
            float z = reach * (1f - v) * side;
            float y = eaveY + (ridgeY - eaveY) * v;
            if (lumps > 0f)
            {
                float edge = MathF.Min(MathF.Min(u, 1f - u) * 5f, 1f) * MathF.Min(v * 5f, 1f);
                y += lumps * (MathF.Sin(x * 1.3f + seed) * MathF.Cos(z * 1.7f + seed * 1.7f) + MathF.Sin(x * 0.45f + z * 0.8f + seed)) * edge;
            }
            return new Vector3(x, y, z);
        }

        // Four slopes meeting at a short ridge, with eaves that sweep up at the corners and a slight dish to each slope.
        private static void AddHipRoof(MeshBuilder b, Vector2 half, float eaveHeight, Kit kit, VertexMaterial material)
        {
            float overhang = kit.Overhang;
            float x = half.X + overhang;
            float z = half.Y + overhang;
            float rise = z * kit.Pitch;
            float eaveY = eaveHeight + RoofLift - overhang * 0.28f;
            float ridgeY = eaveY + rise;
            float ridgeHalf = MathF.Max(x - z, x * 0.14f);

            float upsweep = kit.Upsweep;

            // A slope is the eave along one edge running up to the ridge; `sag` dishes it, and the end of the eave lifts.
            Vector3 Point(Vector3 eaveFrom, Vector3 eaveTo, Vector3 ridgeFrom, Vector3 ridgeTo, float u, float v)
            {
                Vector3 eave = Vector3.Lerp(eaveFrom, eaveTo, u);
                Vector3 ridge = Vector3.Lerp(ridgeFrom, ridgeTo, u);
                Vector3 p = Vector3.Lerp(eave, ridge, v);
                float corner = MathF.Pow(MathF.Abs(u * 2f - 1f), 3f);
                p.Y += upsweep * corner * (1f - v) * (1f - v);
                p.Y -= 0.42f * rise * 0.3f * MathF.Sin(v * MathF.PI);
                return p;
            }

            Vector3 r0 = new Vector3(-ridgeHalf, ridgeY, 0f);
            Vector3 r1 = new Vector3(ridgeHalf, ridgeY, 0f);
            Vector3 frontLeft = new Vector3(-x, eaveY, z);
            Vector3 frontRight = new Vector3(x, eaveY, z);
            Vector3 backRight = new Vector3(x, eaveY, -z);
            Vector3 backLeft = new Vector3(-x, eaveY, -z);

            b.Surface(14, 7, (u, v) => Point(frontLeft, frontRight, r0, r1, u, v), material);
            b.Surface(14, 7, (u, v) => Point(backRight, backLeft, r1, r0, u, v), material);
            b.Surface(10, 7, (u, v) => Point(frontRight, backRight, r1, r1, u, v), material);
            b.Surface(10, 7, (u, v) => Point(backLeft, frontLeft, r0, r0, u, v), material);

            // The heavy ridge, and its ends turned up.
            float ridgeTop = ridgeY - 0.18f;
            b.Beam(new Vector3(-ridgeHalf - 0.6f, ridgeTop, 0f), new Vector3(ridgeHalf + 0.6f, ridgeTop, 0f), 0.34f, kit.Ridge);
            b.Beam(new Vector3(-ridgeHalf - 0.6f, ridgeTop, 0f), new Vector3(-ridgeHalf - 1.2f, ridgeTop + 0.8f, 0f), 0.3f, kit.Ridge);
            b.Beam(new Vector3(ridgeHalf + 0.6f, ridgeTop, 0f), new Vector3(ridgeHalf + 1.2f, ridgeTop + 0.8f, 0f), 0.3f, kit.Ridge);
        }

        private static Mesh BuildBarn(Vector2 half, Kit kit, WorldRandom random)
        {
            var b = new MeshBuilder();
            AddPlinth(b, half, kit);
            float top = PlinthHeight + 9f;

            // Barns are boarded, whatever the houses are.
            var boards = new VertexMaterial(TextureGenerator.wood, new Colour(150, 120, 92));
            var saved = kit.Wall;
            kit.Wall = boards;
            AddWalls(b, half, PlinthHeight, top, kit);
            AddFrame(b, half, PlinthHeight, top, kit, 1);
            kit.Wall = saved;

            // A wagon door, standing a little open.
            float z = half.Y + 0.09f;
            float door = 3.8f;
            b.Quad(new Vector3(-door, PlinthHeight, z), new Vector3(door, PlinthHeight, z), new Vector3(door, PlinthHeight + 7f, z), new Vector3(-door, PlinthHeight + 7f, z), kit.Door);
            b.Beam(new Vector3(-door, PlinthHeight + 7f, z + 0.1f), new Vector3(door, PlinthHeight + 7f, z + 0.1f), 0.24f, kit.Frame);
            b.Beam(new Vector3(0f, PlinthHeight, z + 0.1f), new Vector3(0f, PlinthHeight + 7f, z + 0.1f), 0.18f, kit.Frame);
            b.Beam(new Vector3(-door, PlinthHeight, z + 0.1f), new Vector3(0f, PlinthHeight + 7f, z + 0.1f), 0.14f, kit.Frame);
            b.Beam(new Vector3(door, PlinthHeight, z + 0.1f), new Vector3(0f, PlinthHeight + 7f, z + 0.1f), 0.14f, kit.Frame);

            AddRoof(b, half, top, kit, random);
            return b.Build();
        }

        private static Mesh BuildShed(Vector2 half, Kit kit)
        {
            var b = new MeshBuilder();
            var boards = new VertexMaterial(TextureGenerator.wood, new Colour(138, 112, 86));
            b.Box(new Vector3(0f, -0.5f, 0f), new Vector3(half.X, 1f, half.Y), kit.Plinth);
            b.Box(new Vector3(0f, 3f, 0f), new Vector3(half.X - 0.1f, 2.6f, half.Y - 0.1f), boards);

            // One slope, higher at the front.
            float reachX = half.X + 0.5f;
            float reachZ = half.Y + 0.6f;
            b.Surface(4, 3, (u, v) => new Vector3((u * 2f - 1f) * reachX, 5.3f + (0.5f - v) * 1.5f, (1f - v * 2f) * reachZ), kit.Roof);
            b.Quad(new Vector3(-1.4f, 0.4f, half.Y), new Vector3(1.4f, 0.4f, half.Y), new Vector3(1.4f, 4.6f, half.Y), new Vector3(-1.4f, 4.6f, half.Y), kit.Door);
            return b.Build();
        }

        // Two tiers of roof over a raised floor: the one building in a village meant to be noticed.
        private static Mesh BuildTemple(Vector2 half, Kit kit, WorldRandom random)
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, -0.2f, 0f), new Vector3(half.X + 0.8f, 2.2f, half.Y + 0.8f), kit.Plinth);
            float floor = 2f;
            float top = floor + 7.5f;
            AddWalls(b, new Vector2(half.X - 1f, half.Y - 1f), floor, top, kit);
            AddFrame(b, new Vector2(half.X - 1f, half.Y - 1f), floor, top, kit, 1);
            AddDoor(b, new Vector3(0f, floor, half.Y - 1f), kit);

            bool wasHip = kit.HipRoof;
            kit.HipRoof = true;
            float savedPitch = kit.Pitch;
            float savedUpsweep = kit.Upsweep;
            kit.Pitch = MathF.Min(kit.Pitch, 0.5f);
            kit.Upsweep = MathF.Max(kit.Upsweep, 1f);
            AddHipRoof(b, half, top, kit, kit.Roof);
            AddHipRoof(b, half * 0.62f, top + 4.6f, kit, kit.Roof);
            kit.HipRoof = wasHip;
            kit.Pitch = savedPitch;
            kit.Upsweep = savedUpsweep;
            return b.Build();
        }
    }
}
