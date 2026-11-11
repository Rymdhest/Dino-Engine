using Dino_Engine.Modelling.Model;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    // The styles the land decides (SettlementStage.StyleOf): logs where the forest is tall and the winters long, mud where it hardly rains, posts over wet ground, and clay
    // tiles in the sun's country. Each builds every building of the catalogue to its size, front towards +z, as the older styles do, and as from up here a village is its roofs,
    // each is told by its roof first: turf or shingles on a steep gable over gables of logs, a flat terrace with its beams poking through and a dome over its holy place, a tall
    // thatch with its rafters crossed over the gables, low tiles beside a bell tower.
    public static partial class BuildingGenerator
    {
        private static Kit KitForLand(BuildingStyle style, WorldRandom random, VertexMaterial stone, VertexMaterial window)
        {
            switch (style)
            {
                case BuildingStyle.Log:
                {
                    // Sod laid on birch bark where the snow lies long, riven shingles elsewhere.
                    bool turf = random.Chance(0.55f);
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.barkBare, Shade(new Vector3(0.78f, 0.64f, 0.5f), random, 0.12f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.42f, 0.31f, 0.22f), random, 0.12f)),
                        Roof = turf
                            ? new VertexMaterial(TextureGenerator.grass, Shade(new Vector3(0.66f, 0.76f, 0.5f), random, 0.12f))
                            : new VertexMaterial(TextureGenerator.shingle, Shade(new Vector3(0.56f, 0.44f, 0.34f), random, 0.12f)),
                        // The ridge log takes the full sun, which the walls don't, so it's as dark as the frame: pale, it reads as a pipe from up here.
                        Ridge = new VertexMaterial(TextureGenerator.barkBare, new Colour(84, 66, 50)),
                        Plinth = stone,
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(86, 62, 44)),
                        Window = window,
                        Pitch = random.Range(0.92f, 1.15f),
                        Overhang = random.Range(0.9f, 1.3f),
                        Lumps = turf ? 0.24f : 0f,
                    };
                }
                case BuildingStyle.Adobe:
                {
                    // Every village digs its own earth, and no two are quite the same colour.
                    Vector3[] earths = { new Vector3(0.86f, 0.62f, 0.44f), new Vector3(0.8f, 0.56f, 0.4f), new Vector3(0.9f, 0.72f, 0.54f), new Vector3(0.84f, 0.52f, 0.38f) };
                    Vector3 earth = earths[random.Int(earths.Length)];
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.plaster, Shade(earth, random, 0.05f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.44f, 0.31f, 0.2f), random, 0.12f)),
                        Roof = new VertexMaterial(TextureGenerator.plaster, new Colour(earth * 0.9f)),
                        Ridge = new VertexMaterial(TextureGenerator.plaster, new Colour(earth * 0.74f)),
                        Plinth = new VertexMaterial(TextureGenerator.stoneWall, new Colour(166, 146, 122)),
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(84, 60, 40)),
                        Window = window,
                    };
                }
                case BuildingStyle.Stilt:
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.84f, 0.74f, 0.52f), random, 0.1f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.38f, 0.28f, 0.19f), random, 0.12f)),
                        Roof = new VertexMaterial(TextureGenerator.thatch, Shade(new Vector3(0.88f, 0.8f, 0.64f), random, 0.12f)),
                        Ridge = new VertexMaterial(TextureGenerator.thatch, new Colour(132, 112, 82)),
                        Plinth = new VertexMaterial(TextureGenerator.wood, new Colour(98, 74, 52)),
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(82, 60, 42)),
                        Window = window,
                        Pitch = random.Range(1.05f, 1.3f),
                        Overhang = random.Range(1f, 1.35f),
                        Lumps = 0.14f,
                    };
                default:
                {
                    // Lime white mostly, and now and then a house washed cream, ochre or rose.
                    Vector3[] washes = { new Vector3(0.98f, 0.96f, 0.9f), new Vector3(0.97f, 0.91f, 0.77f), new Vector3(0.98f, 0.84f, 0.64f), new Vector3(0.95f, 0.82f, 0.74f) };
                    Vector3 wash = random.Chance(0.55f) ? washes[0] : washes[random.Int(1, washes.Length - 1)];
                    return new Kit
                    {
                        Wall = new VertexMaterial(TextureGenerator.plaster, Shade(wash, random, 0.04f)),
                        Frame = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.4f, 0.29f, 0.2f), random, 0.12f)),
                        Roof = new VertexMaterial(TextureGenerator.roofTiles, Shade(new Vector3(0.96f, 0.94f, 0.9f), random, 0.08f)),
                        Ridge = new VertexMaterial(TextureGenerator.roofTiles, new Colour(200, 180, 168)),
                        Plinth = stone,
                        Door = new VertexMaterial(TextureGenerator.wood, new Colour(78, 56, 40)),
                        Window = window,
                        Pitch = random.Range(0.4f, 0.5f),
                        Overhang = random.Range(0.6f, 0.9f),
                        HipRoof = random.Chance(0.55f),
                    };
                }
            }
        }

        // How tall each kind's walls are, how many storeys and chimneys it has, whatever it's built of, so that a house in mud and a house in logs are the same house.
        private static (float Wall, int Storeys, int Chimneys) Proportions(StructureDefinition definition)
        {
            if (definition == StructureCatalog.Inn) return (11.5f, 2, 2);
            if (definition == StructureCatalog.Hall) return (8.5f, 1, 2);
            if (definition == StructureCatalog.Hut) return (6.6f, 1, 0);
            if (definition == StructureCatalog.Longhouse) return (7.2f, 1, 1);
            if (definition == StructureCatalog.House) return (7.6f, 1, 1);
            if (definition == StructureCatalog.Barn) return (8.2f, 1, 0);
            if (definition == StructureCatalog.Temple) return (7.8f, 1, 0);
            if (definition == StructureCatalog.Shed) return (5f, 1, 0);
            return (7f, 1, 1);
        }

        // ================================================================ Shared

        private static VertexMaterial Shaded(VertexMaterial material, float shade) => new VertexMaterial(material.materialTextureIndex, new Colour(material.colour.ToVector3() * shade));

        private static Vector3 Abs(Vector3 v) => new Vector3(MathF.Abs(v.X), MathF.Abs(v.Y), MathF.Abs(v.Z));

        // The highest a gable or a hip roof on these walls can be over a line of the plan, lumps and all: what a chimney there has to clear.
        private static float RoofOver(Vector2 half, float top, Kit kit, float z)
        {
            float gable = top + RoofLift + MathF.Max(0f, half.Y - MathF.Abs(z)) * kit.Pitch;
            float hip = top + RoofLift - kit.Overhang * 0.28f + MathF.Max(0f, half.Y + kit.Overhang - MathF.Abs(z)) * kit.Pitch;
            return (kit.HipRoof ? MathF.Max(gable, hip) : gable) + 2f * kit.Lumps;
        }

        // Chimneys near the ridge, each standing well clear of the roof where it comes through, and their tops for the smoke.
        private static void AddChimneys(MeshBuilder b, Vector2 half, float top, int count, Kit kit, WorldRandom random, List<Vector3>? chimneys)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i == 0 ? 1f : -1f) * half.X * random.Range(0.3f, 0.6f);
                Vector3 at = new Vector3(x, 0f, random.Range(-0.3f, 0.3f) * half.Y);
                // AddChimney's stack stands 1.2 m over the height it's given.
                float stack = RoofOver(half, top, kit, at.Z) + 0.4f;
                AddChimney(b, at, stack, kit);
                chimneys?.Add(new Vector3(at.X, stack + 1.2f, at.Z));
            }
        }

        // An end wall carried up through the lift a roof leaves over its walls, to the foot of the gable, so that nothing shows through under one.
        private static void GableBand(MeshBuilder b, float halfX, float zFrom, float zTo, float bottom, float top, VertexMaterial material)
        {
            b.Quad(new Vector3(halfX, bottom, zTo), new Vector3(halfX, bottom, zFrom), new Vector3(halfX, top, zFrom), new Vector3(halfX, top, zTo), material);
            b.Quad(new Vector3(-halfX, bottom, zFrom), new Vector3(-halfX, bottom, zTo), new Vector3(-halfX, top, zTo), new Vector3(-halfX, top, zFrom), material);
        }

        // A door of boards on ledges, braced from its hinge side, facing +z at a depth: one leaf, or a pair for a wagon.
        private static void PlankDoor(MeshBuilder b, float x, float z, float halfWidth, float bottom, float top, VertexMaterial door, VertexMaterial frame, bool pair)
        {
            b.Quad(new Vector3(x - halfWidth, bottom, z), new Vector3(x + halfWidth, bottom, z), new Vector3(x + halfWidth, top, z), new Vector3(x - halfWidth, top, z), door);
            float front = z + 0.05f;
            int leaves = pair ? 2 : 1;
            float leaf = halfWidth * 2f / leaves;
            float low = bottom + (top - bottom) * 0.18f, high = bottom + (top - bottom) * 0.82f;
            for (int i = 0; i < leaves; i++)
            {
                float left = x - halfWidth + i * leaf + 0.14f, right = left + leaf - 0.28f;
                b.Beam(new Vector3(left, low, front), new Vector3(right, low, front), 0.07f, frame);
                b.Beam(new Vector3(left, high, front), new Vector3(right, high, front), 0.07f, frame);
                bool hingeLeft = i == 0;
                b.Beam(new Vector3(hingeLeft ? left : right, low, front), new Vector3(hingeLeft ? right : left, high, front), 0.06f, frame);
            }
            if (pair) b.Beam(new Vector3(x, bottom, front + 0.03f), new Vector3(x, top, front + 0.03f), 0.08f, frame);
        }

        // A ladder leaning from its foot on the ground to its top against a wall.
        private static void Ladder(MeshBuilder b, Vector3 foot, Vector3 top, Kit kit)
        {
            Vector3 up = top - foot;
            Vector3 across = Vector3.Cross(up, Vector3.UnitY);
            across = across.LengthSquared > 1e-6f ? across.Normalized() * 0.55f : Vector3.UnitZ * 0.55f;
            b.Beam(foot - across, top - across, 0.08f, kit.Frame);
            b.Beam(foot + across, top + across, 0.08f, kit.Frame);
            int rungs = Math.Max((int)(up.Length / 0.6f), 2);
            for (int i = 1; i < rungs; i++)
            {
                Vector3 at = foot + up * (i / (float)rungs);
                b.Beam(at - across, at + across, 0.05f, kit.Frame);
            }
        }

        // ================================================================ Log

        private const float LogFooting = 0.55f;
        private const int LogSides = 7;

        private static Mesh BuildLog(StructureDefinition definition, Vector2 half, Kit kit, WorldRandom random, List<Vector3>? chimneys)
        {
            var b = new MeshBuilder();
            (float wallHeight, _, int count) = Proportions(definition);
            bool barn = definition == StructureCatalog.Barn;
            bool shed = definition == StructureCatalog.Shed;
            float r = random.Range(0.23f, 0.28f);
            float course = r * 1.74f;       // Each log is notched down over the one below.
            float over = r * 1.9f;          // And runs on past the corner.
            int courses = Math.Max((int)MathF.Round(wallHeight / course), 8);
            float top = LogFooting + 2f * r + (courses - 1) * course;
            float plate = top + RoofLift;   // Where the roof crosses the line of the walls.
            Vector3 logColour = kit.Wall.colour.ToVector3();

            // A footing of field stones, reaching well below ground so that a slope shows no daylight under the logs.
            b.Box(new Vector3(0f, LogFooting - 2.4f, 0f), new Vector3(half.X + 0.15f, 2.4f, half.Y + 0.15f), kit.Plinth);

            // The four walls, each as the middle of its logs, the way it runs (its outside is that crossed with up), how long it is and how far up its logs lie: the ends'
            // half a course above the sides', so that they cross at the corners.
            var walls = new (Vector3 Origin, Vector3 Along, float Half, float Offset)[]
            {
                (new Vector3(0f, 0f, half.Y), Vector3.UnitX, half.X, 0f),
                (new Vector3(0f, 0f, -half.Y), -Vector3.UnitX, half.X, 0f),
                (new Vector3(half.X, 0f, 0f), -Vector3.UnitZ, half.Y, course * 0.5f),
                (new Vector3(-half.X, 0f, 0f), Vector3.UnitZ, half.Y, course * 0.5f),
            };
            var openings = new[] { new List<Vector4>(), new List<Vector4>(), new List<Vector4>(), new List<Vector4>() };

            // Whatever's cut through a wall runs from the seam between two of its logs to another, so that the logs left above and below meet it exactly.
            float Seam(float y, float offset) => LogFooting + r + offset + (MathF.Round((y - LogFooting - r - offset) / course - 0.5f) + 0.5f) * course;

            float doorX = random.Range(-0.15f, 0.15f) * half.X;
            float doorHalf = barn ? 3.2f : 1.45f;
            float doorTop = Seam(MathF.Min(LogFooting + (barn ? 6.2f : 5.2f), top - 1.2f), 0f);
            openings[0].Add(new Vector4(doorX - doorHalf, doorX + doorHalf, LogFooting - 1f, doorTop));

            // Small windows, few to a wall, none within reach of the door, each four courses tall.
            const float WindowHalf = 0.7f;
            var windows = new List<(int Wall, Vector4 Opening)>();
            if (!barn && !shed)
            {
                float wanted = LogFooting + MathF.Min(2.4f, wallHeight * 0.34f);
                for (int w = 0; w < 4; w++)
                {
                    float sill = Seam(wanted, walls[w].Offset);
                    float head = sill + 4f * course;
                    int many = Math.Max((int)(walls[w].Half * 2f / 4.6f), 1);
                    for (int i = 0; i < many; i++)
                    {
                        float at = ((i + 0.5f) / many * 2f - 1f) * (walls[w].Half - 1.4f);
                        if (w == 0 && MathF.Abs(at - doorX) < doorHalf + 2.1f) continue;
                        if (random.Chance(0.25f)) continue;
                        var opening = new Vector4(at - WindowHalf, at + WindowHalf, sill, head);
                        openings[w].Add(opening);
                        windows.Add((w, opening));
                    }
                }
            }
            foreach (List<Vector4> list in openings) list.Sort((a, c) => a.X.CompareTo(c.X));

            // The courses, from a half log under each end wall, which fills the space below its first, up through the gables, where each log of an end is shorter and
            // ends under the roof. The logs of one course are one log, broken where a wall's open.
            for (int k = -1; ; k++)
            {
                bool any = false;
                for (int w = 0; w < 4; w++)
                {
                    var wall = walls[w];
                    bool end = w >= 2;
                    if (!end && (k < 0 || k >= courses)) continue;
                    float y = LogFooting + r + wall.Offset + k * course;
                    float reach = wall.Half + over;
                    if (end)
                    {
                        reach = MathF.Min(reach, wall.Half + (plate - y - r) / kit.Pitch - 0.08f - kit.Lumps);
                        if (reach < 0.5f) continue;
                    }
                    any = true;
                    var material = new VertexMaterial(kit.Wall.materialTextureIndex, new Colour(logColour * random.Range(0.88f, 1.08f)));
                    LogRun(b, wall.Origin, wall.Along, -reach, reach, y, r, openings[w], material);
                }
                if (!any && k >= courses) break;
            }

            // The door stands back in its opening between two jambs, under a lintel, with a stone step up to it.
            float face = half.Y + r * 0.5f;
            PlankDoor(b, doorX, half.Y - 0.05f, doorHalf, LogFooting, doorTop, kit.Door, kit.Frame, barn);
            b.Beam(new Vector3(doorX - doorHalf - 0.12f, LogFooting, face), new Vector3(doorX - doorHalf - 0.12f, doorTop + 0.2f, face), 0.16f, kit.Frame);
            b.Beam(new Vector3(doorX + doorHalf + 0.12f, LogFooting, face), new Vector3(doorX + doorHalf + 0.12f, doorTop + 0.2f, face), 0.16f, kit.Frame);
            b.Beam(new Vector3(doorX - doorHalf - 0.45f, doorTop + 0.2f, face), new Vector3(doorX + doorHalf + 0.45f, doorTop + 0.2f, face), 0.18f, kit.Frame);
            b.Box(new Vector3(doorX, LogFooting - 0.5f, half.Y + r + 0.75f), new Vector3(doorHalf + 0.4f, 0.5f, 0.75f), kit.Plinth);

            foreach ((int w, Vector4 opening) in windows)
            {
                var wall = walls[w];
                Vector3 centre = wall.Origin + wall.Along * ((opening.X + opening.Y) * 0.5f) + Vector3.UnitY * ((opening.Z + opening.W) * 0.5f);
                LogWindow(b, centre, wall.Along, (opening.Y - opening.X) * 0.5f, (opening.W - opening.Z) * 0.5f, r, kit);
            }

            // Boards behind the gables' logs, showing only where each ends under the roof, and the roof over all.
            kit.Wall = new VertexMaterial(TextureGenerator.wood, Shade(new Vector3(0.6f, 0.48f, 0.36f), random, 0.08f));
            AddGableRoof(b, half, top, kit, random, kingPost: false);
            AddChimneys(b, half, top, count, kit, random, chimneys);

            // A holy place of logs, or the hall where a village meets, carries a little tower on its ridge, shingled to a point.
            if (definition == StructureCatalog.Temple || definition == StructureCatalog.Hall)
            {
                LogBelfry(b, plate + half.Y * kit.Pitch, kit.Pitch, definition == StructureCatalog.Temple ? 1.5f : 1.1f);
            }
            return b.Build();
        }

        // One course of one wall, broken wherever it's open.
        private static void LogRun(MeshBuilder b, Vector3 origin, Vector3 along, float from, float to, float y, float r, List<Vector4> openings, VertexMaterial material)
        {
            float at = from;
            foreach (Vector4 opening in openings)
            {
                if (y + r * 0.5f < opening.Z || y - r * 0.5f > opening.W) continue;
                if (opening.X > at + 0.1f) Log(b, origin, along, at, opening.X, y, r, material);
                at = MathF.Max(at, opening.Y);
            }
            if (to > at + 0.1f) Log(b, origin, along, at, to, y, r, material);
        }

        // Drawn from the middle out, so that both ends are capped: the cut end grain shows at every corner and every opening.
        private static void Log(MeshBuilder b, Vector3 origin, Vector3 along, float from, float to, float y, float r, VertexMaterial material)
        {
            float middle = (from + to) * 0.5f;
            Vector3 lift = Vector3.UnitY * y;
            b.Tube(origin + along * middle + lift, origin + along * from + lift, r, r * 0.96f, LogSides, material);
            b.Tube(origin + along * middle + lift, origin + along * to + lift, r, r * 0.96f, LogSides, material);
        }

        // Glass in the middle of the logs' thickness, framed in front, with its shutters hooked back against the logs either side.
        private static void LogWindow(MeshBuilder b, Vector3 centre, Vector3 along, float halfWidth, float halfHeight, float r, Kit kit)
        {
            Vector3 up = Vector3.UnitY;
            Vector3 outward = Vector3.Cross(along, up);
            Vector3 pane = centre - outward * 0.04f;
            b.Quad(pane - along * halfWidth - up * halfHeight, pane + along * halfWidth - up * halfHeight, pane + along * halfWidth + up * halfHeight, pane - along * halfWidth + up * halfHeight, kit.Window);
            kit.Lights?.Add(AssetLight.Window(centre + outward * 0.5f, outward));

            Vector3 frame = centre + outward * (r * 0.5f);
            b.Beam(frame - along * (halfWidth + 0.06f) - up * halfHeight, frame - along * (halfWidth + 0.06f) + up * halfHeight, 0.1f, kit.Frame);
            b.Beam(frame + along * (halfWidth + 0.06f) - up * halfHeight, frame + along * (halfWidth + 0.06f) + up * halfHeight, 0.1f, kit.Frame);
            b.Beam(frame - along * (halfWidth + 0.3f) + up * (halfHeight + 0.06f), frame + along * (halfWidth + 0.3f) + up * (halfHeight + 0.06f), 0.12f, kit.Frame);
            b.Beam(frame - along * (halfWidth + 0.3f) - up * (halfHeight + 0.06f), frame + along * (halfWidth + 0.3f) - up * (halfHeight + 0.06f), 0.12f, kit.Frame);
            b.Beam(frame - up * halfHeight, frame + up * halfHeight, 0.05f, kit.Frame);

            Vector3 shutter = centre + outward * (r + 0.07f);
            float leaf = halfWidth * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(shutter + along * (side * (halfWidth + 0.18f + leaf)), Abs(along) * leaf + up * (halfHeight - 0.04f) + Abs(outward) * 0.05f, kit.Frame);
            }
        }

        // A small square belfry astride the ridge, standing down into the roof as far as the roof falls away under its sides, its openings dark, under a shingled spire.
        private static void LogBelfry(MeshBuilder b, float ridgeY, float pitch, float half)
        {
            VertexMaterial boards = new VertexMaterial(TextureGenerator.wood, new Colour(112, 88, 66));
            VertexMaterial dark = new VertexMaterial(TextureGenerator.flat, new Colour(16, 13, 11));
            VertexMaterial spire = new VertexMaterial(TextureGenerator.shingle, new Colour(200, 184, 164));
            float bottom = ridgeY - half * pitch - 0.6f, top = ridgeY + 2.8f;
            b.Box(new Vector3(0f, (bottom + top) * 0.5f, 0f), new Vector3(half, (top - bottom) * 0.5f, half), boards);
            float point = top + half * 3.4f, edge = half + 0.45f;
            for (int side = 0; side < 4; side++)
            {
                b.Place(Vector3.Zero, side * MathF.PI * 0.5f);
                b.Quad(new Vector3(-half * 0.5f, top - 1.6f, half + 0.02f), new Vector3(half * 0.5f, top - 1.6f, half + 0.02f), new Vector3(half * 0.5f, top - 0.4f, half + 0.02f), new Vector3(-half * 0.5f, top - 0.4f, half + 0.02f), dark);
                b.Triangle(new Vector3(-edge, top, edge), new Vector3(edge, top, edge), new Vector3(0f, point, 0f), spire);
            }
            b.ResetPlacement();
        }

        // ================================================================ Adobe

        private const float AdobeFooting = 0.25f;

        private static Mesh BuildAdobe(StructureDefinition definition, Vector2 half, Kit kit, WorldRandom random, List<Vector3>? chimneys)
        {
            var b = new MeshBuilder();
            if (definition == StructureCatalog.Hut) return BuildRoundHut(b, half, kit, random);

            (float wallHeight, _, int count) = Proportions(definition);
            bool barn = definition == StructureCatalog.Barn;
            bool shed = definition == StructureCatalog.Shed;
            bool domed = definition == StructureCatalog.Hall || definition == StructureCatalog.Temple;
            // Mud walls are thick and seldom tall: a second storey is a smaller block on the first, whose roof is its terrace.
            float lower = definition == StructureCatalog.Inn ? 6.8f : shed ? 4.6f : MathF.Min(wallHeight, 7.4f);
            float top = AdobeFooting + lower;
            float roof = top + 0.24f;

            b.Box(new Vector3(0f, AdobeFooting - 2.4f, 0f), new Vector3(half.X + 0.14f, 2.4f, half.Y + 0.14f), kit.Plinth);

            // A barn here is a storehouse with a shade of poles out in front of it, where the yard's work is done out of the sun.
            Vector2 block = barn ? new Vector2(half.X, half.Y - 2.2f) : half;
            Vector2 at = barn ? new Vector2(0f, -2.2f) : Vector2.Zero;
            AdobeBlock(b, at, block, AdobeFooting, top, kit, random);

            float doorX = at.X + random.Range(-0.15f, 0.15f) * block.X;
            float doorHalf = barn ? 2.8f : 1.4f;
            float doorTop = AdobeFooting + (barn ? 4.2f : MathF.Min(5f, lower - 1.4f));
            AdobeDoor(b, new Vector3(doorX, AdobeFooting, at.Y + block.Y), doorHalf, doorTop, kit, barn);
            if (!shed && !barn)
            {
                float y = AdobeFooting + MathF.Min(3.4f, lower * 0.5f);
                AdobeWindows(b, new Vector3(at.X, y, at.Y + block.Y), Vector3.UnitX, block.X, doorX - at.X, doorHalf + 1.7f, kit, random);
                AdobeWindows(b, new Vector3(at.X, y, at.Y - block.Y), -Vector3.UnitX, block.X, 0f, 0f, kit, random);
                AdobeWindows(b, new Vector3(at.X + block.X, y, at.Y), -Vector3.UnitZ, block.Y, 0f, 0f, kit, random);
                AdobeWindows(b, new Vector3(at.X - block.X, y, at.Y), Vector3.UnitZ, block.Y, 0f, 0f, kit, random);
            }
            if (barn) Ramada(b, new Vector2(half.X - 0.4f, 2.1f), new Vector2(0f, half.Y - 2.1f), AdobeFooting + 5f, kit, random);

            // An upper room stands on the roof at the back to one side, flush with the walls below, its door onto the terrace in front of it, and a ladder goes up the other side.
            float side = random.Chance(0.5f) ? 1f : -1f;
            bool upper = definition == StructureCatalog.Inn
                || (definition == StructureCatalog.House && random.Chance(0.45f))
                || (definition == StructureCatalog.Longhouse && random.Chance(0.3f))
                || (definition == StructureCatalog.Cottage && random.Chance(0.25f));
            if (upper)
            {
                Vector2 room = new Vector2(block.X * 0.52f, block.Y * 0.55f);
                Vector2 roomAt = new Vector2(at.X + side * (block.X - room.X), at.Y - (block.Y - room.Y));
                float upperTop = roof + (definition == StructureCatalog.Inn ? 4.9f : 4.4f);
                AdobeBlock(b, roomAt, room, roof, upperTop, kit, random);
                AdobeDoor(b, new Vector3(roomAt.X, roof, roomAt.Y + room.Y), 1.2f, roof + MathF.Min(3.8f, upperTop - roof - 0.9f), kit, false);
                AdobeWindows(b, new Vector3(roomAt.X, roof + 2.4f, roomAt.Y - room.Y), -Vector3.UnitX, room.X, 0f, 0f, kit, random);
                Ladder(b, new Vector3(at.X - side * (block.X + 1.3f), 0f, at.Y + block.Y * 0.35f), new Vector3(at.X - side * (block.X + 0.1f), roof + 1f, at.Y + block.Y * 0.35f), kit);
            }
            else if (!shed && !domed && random.Chance(0.5f))
            {
                // Even one room has its roof to sleep out on in the heat, and a ladder up to it.
                Ladder(b, new Vector3(at.X + side * (block.X + 1.3f), 0f, at.Y - block.Y * 0.3f), new Vector3(at.X + side * (block.X + 0.1f), roof + 1f, at.Y - block.Y * 0.3f), kit);
            }

            // The village's hall and its holy place carry a dome on a drum, and the holy place a little dome at each corner.
            if (domed)
            {
                float radius = MathF.Min(block.X, block.Y) * (definition == StructureCatalog.Temple ? 0.7f : 0.56f);
                Dome(b, new Vector3(at.X, roof, at.Y), radius, 1.2f, kit.Wall, kit.Roof);
                if (definition == StructureCatalog.Temple)
                {
                    foreach (float x in new[] { -1f, 1f })
                    {
                        foreach (float z in new[] { -1f, 1f })
                        {
                            Dome(b, new Vector3(at.X + x * (block.X - 0.75f), roof, at.Y + z * (block.Y - 0.75f)), 0.6f, 1.7f, kit.Wall, kit.Roof);
                        }
                    }
                }
            }

            // Rounded stacks for the hearths below: clear of a dome at the back corners, and of an upper room on its terrace.
            for (int i = 0; i < count; i++)
            {
                float sign = i == 0 ? 1f : -1f;
                Vector2 stack;
                if (domed) stack = new Vector2(at.X + sign * (block.X - 1.3f), at.Y - (block.Y - 1.3f));
                else if (upper) stack = new Vector2(at.X - side * block.X * random.Range(0.35f, 0.62f), at.Y + sign * block.Y * random.Range(0.2f, 0.5f));
                else stack = new Vector2(at.X + sign * block.X * random.Range(0.35f, 0.62f), at.Y + block.Y * random.Range(0.1f, 0.5f));
                b.Tube(new Vector3(stack.X, roof - 0.2f, stack.Y), new Vector3(stack.X, roof + 1.9f, stack.Y), 0.58f, 0.46f, 9, kit.Wall);
                b.Tube(new Vector3(stack.X, roof + 1.9f, stack.Y), new Vector3(stack.X, roof + 2.12f, stack.Y), 0.52f, 0.52f, 9, kit.Ridge);
                chimneys?.Add(new Vector3(stack.X, roof + 2.6f, stack.Y));
            }
            return b.Build();
        }

        // A block of mud wall with its flat roof, a parapet round the roof, the ends of the roof beams poking out under it, and a spout each side to throw the rain off.
        private static void AdobeBlock(MeshBuilder b, Vector2 centre, Vector2 half, float bottom, float top, Kit kit, WorldRandom random)
        {
            Vector3 at = new Vector3(centre.X, 0f, centre.Y);
            b.Box(at + new Vector3(0f, (bottom + top) * 0.5f, 0f), new Vector3(half.X, (top - bottom) * 0.5f, half.Y), kit.Wall);
            // Darker at the foot, where the rain splashes the wall.
            b.Box(at + new Vector3(0f, bottom + 0.45f, 0f), new Vector3(half.X + 0.06f, 0.45f, half.Y + 0.06f), kit.Ridge);

            b.Box(at + new Vector3(0f, top + 0.12f, 0f), new Vector3(half.X + 0.1f, 0.12f, half.Y + 0.1f), kit.Roof);
            const float Thick = 0.28f, Tall = 0.62f;
            float y = top + 0.24f + Tall * 0.5f;
            b.Box(at + new Vector3(0f, y, half.Y + 0.1f - Thick * 0.5f), new Vector3(half.X + 0.1f, Tall * 0.5f, Thick * 0.5f), kit.Wall);
            b.Box(at + new Vector3(0f, y, -half.Y - 0.1f + Thick * 0.5f), new Vector3(half.X + 0.1f, Tall * 0.5f, Thick * 0.5f), kit.Wall);
            b.Box(at + new Vector3(half.X + 0.1f - Thick * 0.5f, y, 0f), new Vector3(Thick * 0.5f, Tall * 0.5f, half.Y + 0.1f - Thick), kit.Wall);
            b.Box(at + new Vector3(-half.X - 0.1f + Thick * 0.5f, y, 0f), new Vector3(Thick * 0.5f, Tall * 0.5f, half.Y + 0.1f - Thick), kit.Wall);

            // The beams run front to back, so their ends show along the front and the back.
            for (float x = -half.X + 0.8f; x <= half.X - 0.6f; x += 1.3f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Tube(at + new Vector3(x, top - 0.28f, side * (half.Y - 0.3f)), at + new Vector3(x, top - 0.26f, side * (half.Y + random.Range(0.5f, 0.75f))), 0.12f, 0.11f, 6, Shaded(kit.Frame, random.Range(0.85f, 1.1f)));
                }
            }
            float spout = random.Range(-0.4f, 0.4f) * half.Y;
            foreach (float side in new[] { -1f, 1f })
            {
                b.Beam(at + new Vector3(side * (half.X - 0.2f), top + 0.36f, spout), at + new Vector3(side * (half.X + 0.95f), top + 0.3f, spout), 0.1f, kit.Frame);
            }
        }

        // A door in front of the wall's darker foot, framed, under a heavy lintel longer than it, with a step up to it.
        private static void AdobeDoor(MeshBuilder b, Vector3 foot, float halfWidth, float top, Kit kit, bool pair)
        {
            float z = foot.Z + 0.08f;
            PlankDoor(b, foot.X, z, halfWidth, foot.Y, top, kit.Door, kit.Frame, pair);
            float frame = z + 0.1f;
            b.Beam(new Vector3(foot.X - halfWidth - 0.1f, foot.Y, frame), new Vector3(foot.X - halfWidth - 0.1f, top, frame), 0.12f, kit.Frame);
            b.Beam(new Vector3(foot.X + halfWidth + 0.1f, foot.Y, frame), new Vector3(foot.X + halfWidth + 0.1f, top, frame), 0.12f, kit.Frame);
            b.Beam(new Vector3(foot.X - halfWidth - 0.6f, top + 0.16f, frame), new Vector3(foot.X + halfWidth + 0.6f, top + 0.16f, frame), 0.18f, kit.Frame);
            b.Box(new Vector3(foot.X, foot.Y - 0.3f, foot.Z + 0.85f), new Vector3(halfWidth + 0.45f, 0.4f, 0.75f), kit.Plinth);
        }

        // Small windows set deep, each under a lintel with a grille of two bars, few to a wall: the walls keep the heat out. A wall runs along `along` from its middle.
        private static void AdobeWindows(MeshBuilder b, Vector3 middle, Vector3 along, float halfLength, float avoid, float avoidReach, Kit kit, WorldRandom random)
        {
            const float HalfWidth = 0.6f, HalfHeight = 0.72f;
            int count = Math.Max((int)(halfLength * 2f / 5.4f), 1);
            Vector3 outward = Vector3.Cross(along, Vector3.UnitY);
            for (int i = 0; i < count; i++)
            {
                float at = ((i + 0.5f) / count * 2f - 1f) * (halfLength - 1.2f);
                if (MathF.Abs(at - avoid) < avoidReach) continue;
                if (random.Chance(0.3f)) continue;
                Vector3 centre = middle + along * at;
                Vector3 pane = centre + outward * 0.03f;
                b.Quad(pane - along * HalfWidth - Vector3.UnitY * HalfHeight, pane + along * HalfWidth - Vector3.UnitY * HalfHeight, pane + along * HalfWidth + Vector3.UnitY * HalfHeight, pane - along * HalfWidth + Vector3.UnitY * HalfHeight, kit.Window);
                kit.Lights?.Add(AssetLight.Window(centre + outward * 0.5f, outward));
                Vector3 face = centre + outward * 0.1f;
                b.Beam(face - along * (HalfWidth + 0.35f) + Vector3.UnitY * (HalfHeight + 0.12f), face + along * (HalfWidth + 0.35f) + Vector3.UnitY * (HalfHeight + 0.12f), 0.12f, kit.Frame);
                foreach (float bar in new[] { -0.3f, 0.3f })
                {
                    b.Beam(face + along * (HalfWidth * bar) - Vector3.UnitY * HalfHeight, face + along * (HalfWidth * bar) + Vector3.UnitY * HalfHeight, 0.04f, kit.Frame);
                }
            }
        }

        // A dome on a low drum: a hemisphere a little raised, as a mud dome is built up in rings.
        private static void Dome(MeshBuilder b, Vector3 foot, float radius, float drum, VertexMaterial wall, VertexMaterial roof)
        {
            b.Tube(foot - Vector3.UnitY * 0.2f, foot + Vector3.UnitY * drum, radius, radius, 14, wall, capped: false);
            Vector3 springing = foot + Vector3.UnitY * drum;
            b.Surface(16, 6, (u, v) =>
            {
                float angle = -u * MathF.Tau;
                float rise = v * MathF.PI * 0.5f;
                float ring = MathF.Cos(rise) * radius;
                return springing + new Vector3(MathF.Cos(angle) * ring, MathF.Sin(rise) * radius * 1.08f, MathF.Sin(angle) * ring);
            }, roof);
        }

        // A shade of poles on posts, roofed with brush, its back against a wall.
        private static void Ramada(MeshBuilder b, Vector2 half, Vector2 centre, float top, Kit kit, WorldRandom random)
        {
            VertexMaterial brush = new VertexMaterial(TextureGenerator.thatch, new Colour(170, 150, 112));
            float front = centre.Y + half.Y;
            for (int i = -1; i <= 1; i++)
            {
                b.Tube(new Vector3(centre.X + i * half.X, AdobeFooting - 0.6f, front), new Vector3(centre.X + i * half.X, top, front), 0.16f, 0.13f, 6, kit.Frame);
            }
            b.Tube(new Vector3(centre.X - half.X - 0.4f, top, front), new Vector3(centre.X + half.X + 0.4f, top, front), 0.15f, 0.15f, 6, kit.Frame);
            for (float x = -half.X; x <= half.X; x += 0.75f)
            {
                b.Tube(new Vector3(centre.X + x, top + 0.18f, centre.Y - half.Y - 0.3f), new Vector3(centre.X + x + random.Range(-0.1f, 0.1f), top + 0.18f, front + 0.5f), 0.07f, 0.06f, 5, kit.Frame);
            }
            b.Surface(4, 2, (u, v) => new Vector3(centre.X + (u * 2f - 1f) * (half.X + 0.2f), top + 0.32f + MathF.Sin(u * 9f) * 0.05f, front + 0.3f - v * (half.Y * 2f + 0.5f)), brush);
        }

        // A round hut of mud under a cone of thatch, the oldest way of building in the dry country.
        private static Mesh BuildRoundHut(MeshBuilder b, Vector2 half, Kit kit, WorldRandom random)
        {
            float radius = MathF.Min(half.X, half.Y) * 0.92f;
            float wall = random.Range(4.4f, 5.2f);
            float foot = AdobeFooting - 2f, head = AdobeFooting + wall;
            VertexMaterial thatch = new VertexMaterial(TextureGenerator.thatch, Shade(new Vector3(0.86f, 0.76f, 0.58f), random, 0.1f));
            // The wall leans in a little as it rises, as mud built up by hand does.
            float RadiusAt(float y) => radius * (1f - 0.03f * (y - foot) / (head - foot));
            b.Tube(new Vector3(0f, foot, 0f), new Vector3(0f, head, 0f), radius, RadiusAt(head), 16, kit.Wall, capped: false);
            b.Tube(new Vector3(0f, AdobeFooting, 0f), new Vector3(0f, AdobeFooting + 0.8f, 0f), RadiusAt(AdobeFooting) + 0.06f, RadiusAt(AdobeFooting + 0.8f) + 0.06f, 16, kit.Ridge, capped: false);
            float eave = head - 0.35f, apex = eave + radius * 1.25f + 1.6f;
            b.Tube(new Vector3(0f, eave, 0f), new Vector3(0f, apex, 0f), radius + 1.15f, 0.12f, 16, thatch);
            // The cone ends in a knot of thatch bound round.
            b.Tube(new Vector3(0f, apex - 0.2f, 0f), new Vector3(0f, apex + 0.6f, 0f), 0.3f, 0.06f, 7, kit.Ridge);

            // The door follows the curve of the wall, in front of its darker foot, between two posts and under a lintel bent to the wall.
            const float DoorHalf = 1.25f;
            float doorTop = AdobeFooting + MathF.Min(4.1f, wall - 0.4f);
            Vector3 Round(float angle, float r, float y) => new Vector3(MathF.Sin(angle) * r, y, MathF.Cos(angle) * r);
            b.Surface(4, 1, (u, v) =>
            {
                float y = AdobeFooting + v * (doorTop - AdobeFooting);
                float r = RadiusAt(y) + 0.12f;
                return Round((u * 2f - 1f) * DoorHalf / r, r, y);
            }, kit.Door);
            foreach (float s in new[] { -1f, 1f })
            {
                float low = RadiusAt(AdobeFooting) + 0.2f, high = RadiusAt(doorTop) + 0.2f;
                b.Beam(Round(s * (DoorHalf / low + 0.08f), low, AdobeFooting), Round(s * (DoorHalf / high + 0.08f), high, doorTop + 0.1f), 0.1f, kit.Frame);
            }
            float lintel = RadiusAt(doorTop) + 0.22f;
            float spread = DoorHalf / lintel + 0.35f;
            const int Pieces = 4;
            for (int i = 0; i < Pieces; i++)
            {
                // Each piece a little longer than its share, so that they meet without a gap where they turn.
                float a0 = -spread + 2f * spread * i / Pieces - 0.01f, a1 = -spread + 2f * spread * (i + 1) / Pieces + 0.01f;
                b.Beam(Round(a0, lintel, doorTop + 0.2f), Round(a1, lintel, doorTop + 0.2f), 0.13f, kit.Frame);
            }
            b.Box(new Vector3(0f, AdobeFooting - 0.35f, radius + 0.85f), new Vector3(1.5f, 0.4f, 0.6f), kit.Plinth);
            return b.Build();
        }

        // ================================================================ Stilt

        private static Mesh BuildStilt(StructureDefinition definition, Vector2 half, Kit kit, WorldRandom random)
        {
            var b = new MeshBuilder();
            (float wallHeight, _, _) = Proportions(definition);
            bool granary = definition == StructureCatalog.Barn;
            bool shed = definition == StructureCatalog.Shed;
            // A big house would be all roof at the pitch a small one's thatch wants.
            kit.Pitch *= Math.Clamp(6.5f / half.Y, 0.78f, 1f);
            float floor = random.Range(2.5f, 3.1f) + (granary ? 0.8f : 0f);
            float walls = MathF.Min(wallHeight * 0.68f, granary ? 4.6f : 5.6f);
            // The deck in front is open to the sky, as from up here it's what shows the house stands up on posts.
            float veranda = granary || shed ? 0f : MathF.Min(3.2f, half.Y * 0.5f);
            VertexMaterial posts = kit.Frame;

            // Posts in rows under the floor, standing in the ground, braced from post to post round the outside.
            int alongX = Math.Max((int)MathF.Round((half.X * 2f - 0.6f) / 2.9f), 1);
            int alongZ = Math.Max((int)MathF.Round((half.Y * 2f - 0.6f) / 2.9f), 1);
            float Px(int i) => -half.X + 0.3f + (half.X * 2f - 0.6f) * i / alongX;
            float Pz(int j) => -half.Y + 0.3f + (half.Y * 2f - 0.6f) * j / alongZ;
            for (int i = 0; i <= alongX; i++)
            {
                for (int j = 0; j <= alongZ; j++)
                {
                    float x = Px(i), z = Pz(j);
                    b.Tube(new Vector3(x, -1.2f, z), new Vector3(x + random.Range(-0.06f, 0.06f), floor - 0.24f, z + random.Range(-0.06f, 0.06f)), 0.18f, 0.15f, 6, Shaded(posts, random.Range(0.85f, 1.1f)));
                    // A granary's posts each wear a broad disc under the floor that no rat climbs past.
                    if (granary) b.Tube(new Vector3(x, floor - 1.1f, z), new Vector3(x, floor - 0.98f, z), 0.5f, 0.5f, 8, kit.Plinth);
                }
            }
            for (int i = 0; i < alongX; i++)
            {
                foreach (int j in new[] { 0, alongZ })
                {
                    bool rising = (i + j) % 2 == 0;
                    b.Beam(new Vector3(Px(i), rising ? 0.4f : floor - 0.5f, Pz(j)), new Vector3(Px(i + 1), rising ? floor - 0.5f : 0.4f, Pz(j)), 0.06f, posts);
                }
            }
            for (int j = 0; j < alongZ; j++)
            {
                foreach (int i in new[] { 0, alongX })
                {
                    bool rising = (i + j) % 2 == 0;
                    b.Beam(new Vector3(Px(i), rising ? 0.4f : floor - 0.5f, Pz(j)), new Vector3(Px(i), rising ? floor - 0.5f : 0.4f, Pz(j + 1)), 0.06f, posts);
                }
            }

            // The floor, of planks, and the deck it makes in front.
            b.Box(new Vector3(0f, floor - 0.12f, 0f), new Vector3(half.X, 0.12f, half.Y), kit.Plinth);

            // The room, of planks between corner posts, set back from the front by the deck.
            Vector2 room = new Vector2(half.X - 0.25f, half.Y - veranda * 0.5f - 0.25f);
            float roomZ = -veranda * 0.5f;
            float top = floor + walls;
            float front = roomZ + room.Y;
            b.Box(new Vector3(0f, (floor + top) * 0.5f, roomZ), new Vector3(room.X, walls * 0.5f, room.Y), kit.Wall);
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    b.Beam(new Vector3(x * (room.X + 0.05f), floor, roomZ + z * (room.Y + 0.05f)), new Vector3(x * (room.X + 0.05f), top, roomZ + z * (room.Y + 0.05f)), 0.15f, posts);
                }
            }
            b.Beam(new Vector3(-room.X - 0.2f, top - 0.12f, front + 0.06f), new Vector3(room.X + 0.2f, top - 0.12f, front + 0.06f), 0.14f, posts);

            // The door onto the deck, and windows round the other sides.
            float doorX = random.Range(-0.2f, 0.2f) * room.X;
            float doorTop = floor + MathF.Min(4.6f, walls - 0.45f);
            PlankDoor(b, doorX, front + 0.04f, 1.3f, floor, doorTop, kit.Door, kit.Frame, false);
            b.Beam(new Vector3(doorX - 1.55f, doorTop + 0.12f, front + 0.08f), new Vector3(doorX + 1.55f, doorTop + 0.12f, front + 0.08f), 0.12f, posts);
            if (!granary && !shed)
            {
                float y = floor + walls * 0.55f;
                AddWallWindows(b, new Vector3(0f, y, roomZ - room.Y), -Vector3.UnitX, room.X, 0f, kit, random);
                AddWallWindows(b, new Vector3(room.X, y, roomZ), -Vector3.UnitZ, room.Y, 0f, kit, random);
                AddWallWindows(b, new Vector3(-room.X, y, roomZ), Vector3.UnitZ, room.Y, 0f, kit, random);
            }

            // A rail round the deck, open where the ladder comes up.
            float deck = half.Y - 0.08f;
            if (veranda > 0f)
            {
                float rail = floor + 1.25f;
                int bays = Math.Max((int)MathF.Round((half.X * 2f - 0.3f) / 1.6f), 1);
                for (int i = 0; i <= bays; i++)
                {
                    float x = -half.X + 0.15f + (half.X * 2f - 0.3f) * i / bays;
                    if (MathF.Abs(x - doorX) < 0.9f) continue;
                    b.Beam(new Vector3(x, floor, deck), new Vector3(x, rail, deck), 0.06f, posts);
                }
                b.Beam(new Vector3(-half.X + 0.15f, rail, deck), new Vector3(doorX - 0.9f, rail, deck), 0.06f, posts);
                b.Beam(new Vector3(doorX + 0.9f, rail, deck), new Vector3(half.X - 0.15f, rail, deck), 0.06f, posts);
                foreach (float x in new[] { -half.X + 0.15f, half.X - 0.15f })
                {
                    b.Beam(new Vector3(x, rail, deck), new Vector3(x, rail, front), 0.06f, posts);
                    b.Beam(new Vector3(x, floor, (deck + front) * 0.5f), new Vector3(x, rail, (deck + front) * 0.5f), 0.06f, posts);
                }
            }
            Ladder(b, new Vector3(doorX, 0f, half.Y + floor * 0.42f), new Vector3(doorX, floor + 0.1f, half.Y - 0.05f), kit);

            // A tall thatch over the room alone, its ends carried up to its gables, and the verges' rafters running on past the ridge to cross there at both ends.
            b.Place(new Vector3(0f, 0f, roomZ));
            AddGableRoof(b, room, top, kit, random);
            GableBand(b, room.X, -room.Y, room.Y, top, top + RoofLift, kit.Wall);
            float reach = room.Y + kit.Overhang;
            float eaveY = top + RoofLift - kit.Overhang * kit.Pitch;
            float ridgeY = eaveY + reach * kit.Pitch;
            float verge = room.X + kit.Overhang * 0.75f + 0.06f;
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float lean in new[] { -1f, 1f })
                {
                    Vector3 low = new Vector3(s * verge, eaveY + reach * 0.5f * kit.Pitch + 0.16f, lean * reach * 0.5f);
                    Vector3 peak = new Vector3(s * verge, ridgeY + 0.16f, 0f);
                    b.Beam(low, peak + (peak - low).Normalized() * 1.6f, 0.1f, posts);
                }
            }
            b.ResetPlacement();
            return b.Build();
        }

        // ================================================================ Tiled

        private static Mesh BuildTiled(StructureDefinition definition, Vector2 half, Kit kit, WorldRandom random, List<Vector3>? chimneys)
        {
            var b = new MeshBuilder();
            (float wallHeight, int storeys, int count) = Proportions(definition);
            bool barn = definition == StructureCatalog.Barn;
            bool shed = definition == StructureCatalog.Shed;
            VertexMaterial dressed = new VertexMaterial(TextureGenerator.stoneWall, Shade(new Vector3(0.88f, 0.84f, 0.76f), random, 0.05f));
            // A barn is built of the stone cleared from its fields, and left bare.
            if (barn) kit.Wall = new VertexMaterial(TextureGenerator.stoneWall, Shade(new Vector3(0.9f, 0.86f, 0.78f), random, 0.06f));

            AddPlinth(b, half, kit);
            float top = PlinthHeight + wallHeight;
            AddWalls(b, half, PlinthHeight, top, kit);
            if (!barn) Quoins(b, half, PlinthHeight, top, dressed);
            if (!kit.HipRoof) GableBand(b, half.X, -half.Y, half.Y, top, top + RoofLift, kit.Wall);

            // The door in the middle of the front, as a front here is laid out about its door, under a flat arch of dressed stone where the wall is tall enough.
            float doorHalf = barn ? 3f : shed ? 1.4f : DoorHalfWidth;
            float doorHeight = barn ? 5.4f : MathF.Min(DoorHeight, wallHeight - 1.3f);
            float archRadius = doorHalf + 0.25f, archRise = archRadius * 0.55f;
            TiledDoor(b, half, doorHalf, doorHeight, kit, barn);
            bool arched = PlinthHeight + doorHeight + archRise + 0.4f < top;
            if (arched) Arch(b, new Vector3(0f, PlinthHeight + doorHeight, half.Y + 0.12f), archRadius, dressed);

            // A band of stone where an upper floor is, clear above the door and its arch.
            for (int storey = 1; storey < storeys; storey++)
            {
                float y = MathF.Max(PlinthHeight + wallHeight * storey / storeys, PlinthHeight + doorHeight + (arched ? archRise : 0f) + 0.35f);
                b.Box(new Vector3(0f, y, 0f), new Vector3(half.X + 0.08f, 0.14f, half.Y + 0.08f), dressed);
            }

            // Windows in frames, their shutters folded back either side in one of the colours shutters are painted here; the front's are never left out, as it's a front.
            Vector3[] paints = { new Vector3(0.24f, 0.42f, 0.5f), new Vector3(0.26f, 0.42f, 0.3f), new Vector3(0.42f, 0.48f, 0.36f), new Vector3(0.32f, 0.36f, 0.46f) };
            VertexMaterial shutters = new VertexMaterial(TextureGenerator.wood, Shade(paints[random.Int(paints.Length)], random, 0.06f));
            if (!barn && !shed)
            {
                float storeyHeight = (top - PlinthHeight) / storeys;
                float doorClear = archRadius + 0.17f + 1.96f + 0.1f;
                for (int storey = 0; storey < storeys; storey++)
                {
                    float y = PlinthHeight + storeyHeight * (storey + 0.56f);
                    ShutteredWindows(b, new Vector3(0f, y, half.Y), Vector3.UnitX, half.X, storey == 0 ? doorClear : 0f, 0f, kit, shutters, random);
                    ShutteredWindows(b, new Vector3(0f, y, -half.Y), -Vector3.UnitX, half.X, 0f, 0.25f, kit, shutters, random);
                    ShutteredWindows(b, new Vector3(half.X, y, 0f), -Vector3.UnitZ, half.Y, 0f, 0.25f, kit, shutters, random);
                    ShutteredWindows(b, new Vector3(-half.X, y, 0f), Vector3.UnitZ, half.Y, 0f, 0.25f, kit, shutters, random);
                }
            }

            AddRoof(b, half, top, kit, random);
            AddChimneys(b, half, top, count, kit, random, chimneys);

            // The holy place has a bell tower at its back corner, the tallest thing in a village; a hall has a porch over its door, where its people meet out of the sun.
            if (definition == StructureCatalog.Temple) Campanile(b, new Vector3(half.X - 1.7f, 0f, -half.Y + 1.7f), 1.9f, top + 8.5f, dressed, kit);
            if (definition == StructureCatalog.Hall) Porch(b, half, kit, dressed);
            return b.Build();
        }

        // AddDoor's door at any size, of boards on ledges, in the middle of the front, with a worn step up to it.
        private static void TiledDoor(MeshBuilder b, Vector2 half, float halfWidth, float height, Kit kit, bool pair)
        {
            float z = half.Y + 0.07f;
            float bottom = PlinthHeight, top = PlinthHeight + height;
            PlankDoor(b, 0f, z, halfWidth, bottom, top, kit.Door, kit.Frame, pair);
            float frame = z + 0.1f;
            b.Beam(new Vector3(-halfWidth, bottom, frame), new Vector3(-halfWidth, top, frame), 0.2f, kit.Frame);
            b.Beam(new Vector3(halfWidth, bottom, frame), new Vector3(halfWidth, top, frame), 0.2f, kit.Frame);
            b.Beam(new Vector3(-halfWidth - 0.3f, top, frame), new Vector3(halfWidth + 0.3f, top, frame), 0.24f, kit.Frame);
            b.Box(new Vector3(0f, PlinthHeight - 0.32f, half.Y + 1.1f), new Vector3(halfWidth + 0.5f, 0.5f, 0.9f), kit.Plinth);
        }

        // Dressed stones up every corner, long and short in turn on each face, standing proud of the plaster.
        private static void Quoins(MeshBuilder b, Vector2 half, float bottom, float top, VertexMaterial stone)
        {
            const float Course = 0.66f, Proud = 0.12f, Inset = 0.04f;
            int i = 0;
            for (float y = bottom; y + Course <= top + 0.01f; y += Course, i++)
            {
                float alongFront = i % 2 == 0 ? 0.95f : 0.55f;
                float alongEnd = i % 2 == 0 ? 0.55f : 0.95f;
                float mid = y + Course * 0.5f, h = Course * 0.46f;
                // One stone turns the corner, proud of both faces, and the other runs on along the end from behind it, so that no two faces lie in one plane.
                float x0 = half.X + Proud - alongFront, x1 = half.X + Proud, z0 = half.Y - Inset, z1 = half.Y + Proud;
                float ex0 = half.X - Inset, ex1 = half.X + Proud, ez0 = half.Y - Inset - alongEnd, ez1 = half.Y - Inset;
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        b.Box(new Vector3(sx * (x0 + x1) * 0.5f, mid, sz * (z0 + z1) * 0.5f), new Vector3((x1 - x0) * 0.5f, h, (z1 - z0) * 0.5f), stone);
                        b.Box(new Vector3(sx * (ex0 + ex1) * 0.5f, mid, sz * (ez0 + ez1) * 0.5f), new Vector3((ex1 - ex0) * 0.5f, h, (ez1 - ez0) * 0.5f), stone);
                    }
                }
            }
        }

        // A flat arch of dressed stones standing on the wall over an opening, from one end of its springing over to the other.
        private static void Arch(MeshBuilder b, Vector3 springing, float radius, VertexMaterial stone)
        {
            const int Stones = 9;
            for (int i = 0; i < Stones; i++)
            {
                float a0 = MathF.PI * i / Stones, a1 = MathF.PI * (i + 1) / Stones;
                Vector3 from = springing + new Vector3(MathF.Cos(a0) * radius, MathF.Sin(a0) * radius * 0.55f, 0f);
                Vector3 to = springing + new Vector3(MathF.Cos(a1) * radius, MathF.Sin(a1) * radius * 0.55f, 0f);
                b.Beam(from, to, 0.17f, stone);
            }
        }

        // Windows evenly along a wall clear of its quoins, each with room for its shutters open, so that no two shutters meet.
        private static void ShutteredWindows(MeshBuilder b, Vector3 middle, Vector3 along, float halfLength, float doorClear, float skip, Kit kit, VertexMaterial shutters, WorldRandom random)
        {
            const float Cell = 4.4f;
            float usable = 2f * (halfLength - 1.25f);
            int count = (int)(usable / Cell);
            if (count < 1) return;
            float spacing = usable / count;
            Vector3 outward = Vector3.Cross(along, Vector3.UnitY);
            for (int i = 0; i < count; i++)
            {
                float offset = -usable * 0.5f + spacing * (i + 0.5f);
                if (MathF.Abs(offset) < doorClear) continue;
                if (skip > 0f && random.Chance(skip)) continue;
                Vector3 centre = middle + along * offset;
                AddWindow(b, centre, along, kit);
                foreach (float side in new[] { -1f, 1f })
                {
                    b.Box(centre + outward * 0.1f + along * (side * 1.52f), Abs(along) * 0.44f + Vector3.UnitY * 1.12f + Abs(outward) * 0.05f, shutters);
                }
            }
        }

        // A square bell tower standing a little out of the corner it's built into, open near its top on every side, under a cornice and a low tiled pyramid.
        private static void Campanile(MeshBuilder b, Vector3 foot, float half, float top, VertexMaterial stone, Kit kit)
        {
            b.Box(new Vector3(foot.X, (top - 2f) * 0.5f, foot.Z), new Vector3(half, (top + 2f) * 0.5f, half), stone);
            b.Box(new Vector3(foot.X, top + 0.1f, foot.Z), new Vector3(half + 0.15f, 0.12f, half + 0.15f), stone);
            VertexMaterial dark = new VertexMaterial(TextureGenerator.flat, new Colour(14, 12, 11));
            float edge = half + 0.45f, eave = top + 0.22f;
            for (int side = 0; side < 4; side++)
            {
                b.Place(foot, side * MathF.PI * 0.5f);
                b.Quad(new Vector3(-half * 0.45f, top - 3.1f, half + 0.02f), new Vector3(half * 0.45f, top - 3.1f, half + 0.02f), new Vector3(half * 0.45f, top - 0.9f, half + 0.02f), new Vector3(-half * 0.45f, top - 0.9f, half + 0.02f), dark);
                b.Triangle(new Vector3(-edge, eave, edge), new Vector3(edge, eave, edge), new Vector3(0f, eave + half * 1.1f, 0f), kit.Roof);
            }
            b.ResetPlacement();
        }

        // A lean-to of tiles on two columns over a hall's door, on a platform level with the plinth, below the eaves and above the door's arch.
        private static void Porch(MeshBuilder b, Vector2 half, Kit kit, VertexMaterial stone)
        {
            const float Wide = 3.4f, Deep = 2.6f, High = 8.4f, Low = 7.6f;
            float wall = half.Y, columns = wall + Deep - 0.4f;
            b.Box(new Vector3(0f, PlinthHeight - 1.5f, (wall + 0.28f + wall + Deep + 0.3f) * 0.5f), new Vector3(Wide + 0.3f, 1.5f, (Deep + 0.02f) * 0.5f), kit.Plinth);
            foreach (float x in new[] { -Wide + 0.5f, Wide - 0.5f })
            {
                b.Box(new Vector3(x, PlinthHeight + 0.15f, columns), new Vector3(0.48f, 0.15f, 0.48f), stone);
                b.Tube(new Vector3(x, PlinthHeight + 0.3f, columns), new Vector3(x, Low - 0.52f, columns), 0.34f, 0.29f, 10, stone);
                b.Box(new Vector3(x, Low - 0.38f, columns), new Vector3(0.44f, 0.14f, 0.44f), stone);
            }
            b.Beam(new Vector3(-Wide, Low - 0.12f, columns), new Vector3(Wide, Low - 0.12f, columns), 0.14f, kit.Frame);
            b.Beam(new Vector3(-Wide - 0.3f, High - 0.1f, wall + 0.1f), new Vector3(Wide + 0.3f, High - 0.1f, wall + 0.1f), 0.12f, kit.Frame);

            float x0 = -Wide - 0.3f, x1 = Wide + 0.3f, z0 = wall, z1 = wall + Deep + 0.3f;
            const float Lip = 0.2f;
            b.Surface(4, 2, (u, v) => new Vector3(x0 + (x1 - x0) * u, Low + (High - Low) * v, z1 + (z0 - z1) * v), kit.Roof);
            b.Quad(new Vector3(x0, Low - Lip, z1), new Vector3(x1, Low - Lip, z1), new Vector3(x1, Low, z1), new Vector3(x0, Low, z1), kit.Roof);
            b.Quad(new Vector3(x1, Low - Lip, z1), new Vector3(x1, High - Lip, z0), new Vector3(x1, High, z0), new Vector3(x1, Low, z1), kit.Roof);
            b.Quad(new Vector3(x0, Low - Lip, z1), new Vector3(x0, Low, z1), new Vector3(x0, High, z0), new Vector3(x0, High - Lip, z0), kit.Roof);
        }
    }
}
