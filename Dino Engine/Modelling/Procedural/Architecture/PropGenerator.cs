using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Vegetation;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Architecture
{
    /// <summary>
    /// Everything built that isn't a house: the clutter of villages, the camps of bandits, walls and gates, and the stones
    /// of older peoples. Sizes follow <see cref="StructureCatalog"/>; models stand on y = 0 with their front towards +z.
    /// </summary>
    public static class PropGenerator
    {
        private static VertexMaterial Wood(int r = 138, int g = 106, int b = 78) => new VertexMaterial(TextureGenerator.wood, new Colour(r, g, b));
        private static VertexMaterial DarkWood => Wood(84, 62, 46);
        private static VertexMaterial Stone(int shade = 200) => new VertexMaterial(TextureGenerator.stoneWall, new Colour(shade, shade, (int)(shade * 0.96f)));
        private static VertexMaterial Rough(int shade = 190) => new VertexMaterial(TextureGenerator.rock, new Colour(shade, shade, shade));
        private static VertexMaterial Cloth(int r, int g, int b) => new VertexMaterial(TextureGenerator.cloth, new Colour(r, g, b));
        private static VertexMaterial Iron => new VertexMaterial(TextureGenerator.iron, new Colour(110, 110, 115));
        private static VertexMaterial Thatch => new VertexMaterial(TextureGenerator.thatch, new Colour(232, 212, 172));
        private static VertexMaterial Glow(float r, float g, float b, float strength) => new VertexMaterial(TextureGenerator.flatGlow, new Colour(r, g, b, strength));
        private static VertexMaterial FireGlow => Glow(1f, 0.55f, 0.18f, 3f);

        public static Mesh Generate(StructureDefinition definition, BuildingStyle style)
        {
            var random = new WorldRandom(0x7A11).Fork(definition.Id);
            string id = definition.Id;
            var b = new MeshBuilder();

            switch (id)
            {
                case "well": BuildWell(b); break;
                case "stall": BuildStall(b, random); break;
                case "cart": BuildCart(b); break;
                case "haystack": BuildHaystack(b, random); break;
                case "woodpile": BuildWoodpile(b, random); break;
                case "barrels": BuildBarrels(b, random); break;
                case "crates": BuildCrates(b, random); break;
                case "lantern": BuildLantern(b); break;
                case "lantern.stone": BuildStoneLantern(b); break;
                case "signpost": BuildSignpost(b); break;
                case "scarecrow": BuildScarecrow(b); break;
                case "rack": BuildDryingRack(b, random); break;
                case "bench": BuildBench(b); break;
                case "tent": BuildTent(b, definition.HalfExtents, Cloth(150, 126, 92)); break;
                case "tent.large": BuildTent(b, definition.HalfExtents, Cloth(132, 52, 42)); break;
                case "campfire": BuildCampfire(b, random); break;
                case "logs": BuildLogStack(b, random); break;
                case "boat": BuildBoat(b); break;
                case "jetty": BuildJetty(b, definition.HalfExtents); break;
                case "tower.guard": BuildGuardTower(b); break;
                case "tower.stone": BuildStoneTower(b); break;
                case "keep": BuildKeep(b, definition.HalfExtents); break;
                case "gate.arch": BuildGateArch(b); break;
                case "gatehouse": BuildGatehouse(b, definition.HalfExtents); break;
                case "waygate": BuildWaygate(b); break;
                case "altar": BuildAltar(b, random); break;
                case "grave": BuildGrave(b, random); break;
                case "tomb": BuildTomb(b, definition.HalfExtents); break;
                case "monolith": BuildMonolith(b, random); break;
                case "ruin.wall": BuildRuinWall(b, random); break;
                case "ruin.column": BuildRuinColumn(b, random); break;
                case "ruin.rubble": BuildRubble(b, random); break;
                case "cairn": BuildCairn(b, random); break;
                case "palisade": BuildPalisade(b, random); break;
                case "wall.stone": BuildStoneWall(b); break;
                case "tower.watch": BuildWatchTower(b, style); break;
                default: throw new ArgumentException($"No model for structure '{id}'.");
            }
            return b.Build();
        }

        /// <summary>One length of log fence: a pair of posts holding stacked rails, running one metre along +x at scale 1.</summary>
        public static Mesh GenerateFence()
        {
            float fenceHeight = 0.6f;
            float poleRadius = 0.04f;
            float poleRadiusStacked = 0.03f;

            TreeBuilder builder = new TreeBuilder(new VertexMaterial(TextureGenerator.pineBark));
            TreeBuilder.StemBuildSettings stemSettings = new TreeBuilder.StemBuildSettings();
            stemSettings.radiusBase = poleRadius;
            stemSettings.radiusTop = poleRadius;
            stemSettings.height = fenceHeight;
            stemSettings.sinkAmount = 0f;
            stemSettings.stemBendRadiusStart = 0.008f;
            stemSettings.stemBendRadiusEnd = 0.008f;
            stemSettings.stemBaseHeight = 0.0f;
            stemSettings.stemWavePatternAmount = 0.0f;
            stemSettings.baseRadiusFactor = 0.0f;
            stemSettings.textureRepeats = 1;
            builder.BuildStem(stemSettings);

            Mesh fence = new Mesh();
            fence += builder.mesh.translated(new Vector3(poleRadiusStacked + poleRadius, 0f, 0f));
            fence += builder.mesh.translated(new Vector3(-poleRadiusStacked - poleRadius, 0f, 0f));

            int numberOfPolesStacked = 5;
            for (int i = 0; i < numberOfPolesStacked; i++)
            {
                stemSettings.height = 1f;
                stemSettings.radiusBase = poleRadiusStacked;
                stemSettings.radiusTop = poleRadiusStacked;
                Mesh stackPole = builder.BuildStem(stemSettings);
                stackPole.rotate(new Vector3(-MathF.PI / 2f, 0f, 0f));
                float t = i / (float)(numberOfPolesStacked - 1);

                fence += stackPole.translated(new Vector3(0f, t * (fenceHeight - poleRadius * 2f) + poleRadius, 0f));
            }
            fence.rotate(new Vector3(0f, -MathF.PI / 2f, 0f));
            return fence;
        }

        private static void BuildWell(MeshBuilder b)
        {
            b.TilesPerMetre = 0.4f;
            b.Tube(new Vector3(0f, -0.6f, 0f), new Vector3(0f, 1.25f, 0f), 1.75f, 1.65f, 12, Stone());
            b.Tube(new Vector3(0f, 1.25f, 0f), new Vector3(0f, 1.3f, 0f), 1.34f, 1.34f, 12, new VertexMaterial(TextureGenerator.flat, new Colour(14, 16, 20)));
            b.TilesPerMetre = 0.2f;

            for (int side = -1; side <= 1; side += 2)
            {
                b.Beam(new Vector3(side * 1.55f, 0.4f, 0f), new Vector3(side * 1.55f, 5.1f, 0f), 0.14f, DarkWood);
            }
            b.Tube(new Vector3(-1.6f, 3.9f, 0f), new Vector3(1.6f, 3.9f, 0f), 0.16f, 0.16f, 6, Wood());
            b.Tube(new Vector3(0.25f, 3.9f, 0f), new Vector3(0.25f, 2.1f, 0f), 0.03f, 0.03f, 4, DarkWood);
            b.Tube(new Vector3(0.25f, 2.1f, 0f), new Vector3(0.25f, 1.55f, 0f), 0.3f, 0.34f, 7, Wood());

            b.TilesPerMetre = 0.14f;
            b.Surface(3, 2, (u, v) => new Vector3((u * 2f - 1f) * 2.1f, 4.9f + v * 1.5f, 1.7f * (1f - v)), Thatch);
            b.Surface(3, 2, (u, v) => new Vector3((1f - u * 2f) * 2.1f, 4.9f + v * 1.5f, -1.7f * (1f - v)), Thatch);
        }

        private static void BuildStall(MeshBuilder b, WorldRandom random)
        {
            float x = 3.3f;
            float z = 2.2f;
            for (int i = -1; i <= 1; i += 2)
            {
                b.Beam(new Vector3(i * x, 0f, -z), new Vector3(i * x, 5.4f, -z), 0.11f, DarkWood);
                b.Beam(new Vector3(i * x, 0f, z), new Vector3(i * x, 4.4f, z), 0.11f, DarkWood);
            }

            b.Box(new Vector3(0f, 1.7f, z - 0.4f), new Vector3(x - 0.1f, 0.12f, 0.75f), Wood());
            b.Box(new Vector3(0f, 0.8f, z - 0.4f), new Vector3(x - 0.2f, 0.8f, 0.05f), Wood(116, 88, 66));

            // A striped awning, sloping down to the front.
            Vector3 colourA = new Vector3(random.Range(0.45f, 0.75f), random.Range(0.12f, 0.3f), random.Range(0.1f, 0.25f));
            int stripes = 8;
            for (int i = 0; i < stripes; i++)
            {
                float from = -x - 0.4f + (x * 2f + 0.8f) * i / stripes;
                float to = -x - 0.4f + (x * 2f + 0.8f) * (i + 1) / stripes;
                VertexMaterial cloth = i % 2 == 0
                    ? new VertexMaterial(TextureGenerator.cloth, new Colour(colourA))
                    : Cloth(226, 216, 192);
                b.Quad(new Vector3(from, 4.3f, z + 0.5f), new Vector3(to, 4.3f, z + 0.5f), new Vector3(to, 5.5f, -z - 0.2f), new Vector3(from, 5.5f, -z - 0.2f), cloth);
            }

            for (int i = 0; i < 5; i++)
            {
                float px = random.Range(-x + 0.5f, x - 0.5f);
                b.Box(new Vector3(px, 2f, z - 0.4f + random.Range(-0.3f, 0.3f)), new Vector3(random.Range(0.2f, 0.45f), 0.2f, random.Range(0.2f, 0.3f)),
                    Cloth(random.Int(120, 210), random.Int(90, 170), random.Int(60, 120)));
            }
        }

        private static void BuildCart(MeshBuilder b)
        {
            VertexMaterial boards = Wood(128, 100, 74);
            b.Box(new Vector3(0f, 1.5f, 0f), new Vector3(2.5f, 0.14f, 1.35f), boards);
            for (int i = -1; i <= 1; i += 2)
            {
                b.Box(new Vector3(0f, 2.1f, i * 1.3f), new Vector3(2.5f, 0.5f, 0.07f), boards);
                b.Box(new Vector3(i * 2.45f, 2.1f, 0f), new Vector3(0.07f, 0.5f, 1.3f), boards);
            }

            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 hub = new Vector3(0.1f, 1.15f, i * 1.6f);
                b.Tube(hub - Vector3.UnitZ * 0.11f, hub + Vector3.UnitZ * 0.11f, 1.15f, 1.15f, 14, DarkWood);
                b.Tube(hub - Vector3.UnitZ * 0.2f, hub + Vector3.UnitZ * 0.2f, 0.22f, 0.22f, 7, Iron);
            }
            b.Beam(new Vector3(0.1f, 1.15f, -1.7f), new Vector3(0.1f, 1.15f, 1.7f), 0.1f, DarkWood);

            for (int i = -1; i <= 1; i += 2)
            {
                b.Beam(new Vector3(2.4f, 1.45f, i * 0.5f), new Vector3(4.6f, 0.6f, i * 0.42f), 0.08f, DarkWood);
            }
        }

        private static void BuildHaystack(MeshBuilder b, WorldRandom random)
        {
            float seed = random.Float() * 10f;
            b.TilesPerMetre = 0.3f;
            b.Surface(16, 8, (u, v) =>
            {
                float angle = -u * MathF.Tau;
                float lump = 1f + 0.08f * MathF.Sin(angle * 3f + seed) * MathF.Sin(v * 7f);
                float radius = 2.3f * MathF.Sqrt(MathF.Max(1f - v * v * 0.96f, 0f)) * lump;
                return new Vector3(MathF.Cos(angle) * radius, v * 3.3f - 0.1f, MathF.Sin(angle) * radius);
            }, Thatch);
        }

        private static void BuildWoodpile(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial logs = new VertexMaterial(TextureGenerator.oakBark, new Colour(200, 176, 150));
            for (int row = 0; row < 3; row++)
            {
                int count = 5 - row;
                for (int i = 0; i < count; i++)
                {
                    float z = (i - (count - 1) * 0.5f) * 0.42f;
                    float y = 0.2f + row * 0.36f;
                    float x = random.Range(-0.3f, 0.3f);
                    b.Tube(new Vector3(x - 2.1f, y, z), new Vector3(x + 2.1f, y, z), 0.2f, 0.2f, 6, logs);
                }
            }
        }

        private static void AddBarrel(MeshBuilder b, Vector3 at, float scale)
        {
            VertexMaterial staves = Wood(126, 92, 62);
            float r = 0.62f * scale;
            float h = 1.9f * scale;
            b.Tube(at, at + Vector3.UnitY * h * 0.5f, r * 0.84f, r, 10, staves, capped: false);
            b.Tube(at + Vector3.UnitY * h * 0.5f, at + Vector3.UnitY * h, r, r * 0.84f, 10, staves);
            for (int i = 1; i <= 2; i++)
            {
                float y = h * (i == 1 ? 0.22f : 0.78f);
                b.Tube(at + Vector3.UnitY * (y - 0.05f), at + Vector3.UnitY * (y + 0.05f), r * 0.97f, r * 0.97f, 10, Iron, capped: false);
            }
        }

        private static void BuildBarrels(MeshBuilder b, WorldRandom random)
        {
            AddBarrel(b, new Vector3(-0.55f, 0f, 0.2f), 1f);
            AddBarrel(b, new Vector3(0.65f, 0f, -0.3f), 0.92f);
            if (random.Chance(0.6f)) AddBarrel(b, new Vector3(0.2f, 0f, 0.82f), 0.8f);
        }

        private static void BuildCrates(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial boards = Wood(150, 118, 84);
            void Crate(Vector3 at, float size, float yaw)
            {
                b.Place(at, yaw);
                b.Box(new Vector3(0f, size, 0f), new Vector3(size), boards);
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Beam(new Vector3(s * size, 0f, size + 0.02f), new Vector3(s * size, size * 2f, size + 0.02f), 0.06f, DarkWood);
                    b.Beam(new Vector3(s * size, 0f, -size - 0.02f), new Vector3(s * size, size * 2f, -size - 0.02f), 0.06f, DarkWood);
                }
                b.ResetPlacement();
            }

            Crate(new Vector3(-0.55f, 0f, 0f), 0.7f, random.Range(-0.2f, 0.2f));
            Crate(new Vector3(0.75f, 0f, 0.3f), 0.55f, random.Range(-0.4f, 0.4f));
            Crate(new Vector3(-0.4f, 1.4f, 0.05f), 0.45f, random.Range(-0.6f, 0.6f));
        }

        private static void BuildLantern(MeshBuilder b)
        {
            b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 5.4f, 0f), 0.13f, 0.09f, 6, DarkWood);
            b.Beam(new Vector3(0f, 5.2f, 0f), new Vector3(0f, 5.2f, 1f), 0.06f, DarkWood);
            b.Tube(new Vector3(0f, 5.2f, 0.95f), new Vector3(0f, 4.8f, 0.95f), 0.02f, 0.02f, 3, Iron);
            b.Box(new Vector3(0f, 4.45f, 0.95f), new Vector3(0.22f, 0.32f, 0.22f), Glow(1f, 0.68f, 0.3f, 3f));
            b.Box(new Vector3(0f, 4.82f, 0.95f), new Vector3(0.28f, 0.05f, 0.28f), Iron);
            b.Box(new Vector3(0f, 4.1f, 0.95f), new Vector3(0.28f, 0.04f, 0.28f), Iron);
        }

        private static void BuildStoneLantern(MeshBuilder b)
        {
            VertexMaterial stone = Rough(186);
            b.Tube(new Vector3(0f, -0.2f, 0f), new Vector3(0f, 0.34f, 0f), 0.85f, 0.7f, 6, stone);
            b.Tube(new Vector3(0f, 0.34f, 0f), new Vector3(0f, 2f, 0f), 0.26f, 0.22f, 6, stone);
            b.Tube(new Vector3(0f, 2f, 0f), new Vector3(0f, 2.3f, 0f), 0.62f, 0.55f, 6, stone);
            b.Box(new Vector3(0f, 2.76f, 0f), new Vector3(0.36f, 0.44f, 0.36f), Glow(1f, 0.62f, 0.25f, 2.6f));
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    b.Beam(new Vector3(x * 0.42f, 2.3f, z * 0.42f), new Vector3(x * 0.42f, 3.2f, z * 0.42f), 0.07f, stone);
                }
            }
            b.Tube(new Vector3(0f, 3.15f, 0f), new Vector3(0f, 3.85f, 0f), 1f, 0.12f, 6, stone);
            b.Tube(new Vector3(0f, 3.85f, 0f), new Vector3(0f, 4.15f, 0f), 0.2f, 0.04f, 6, stone);
        }

        private static void BuildSignpost(MeshBuilder b)
        {
            b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 4.6f, 0f), 0.14f, 0.11f, 6, DarkWood);
            b.Box(new Vector3(0.75f, 3.9f, 0f), new Vector3(0.95f, 0.26f, 0.06f), Wood());
            b.Place(Vector3.Zero, 1.2f);
            b.Box(new Vector3(0.7f, 3.2f, 0f), new Vector3(0.85f, 0.24f, 0.06f), Wood(126, 96, 70));
            b.ResetPlacement();
        }

        private static void BuildScarecrow(MeshBuilder b)
        {
            b.Beam(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 4.6f, 0f), 0.08f, DarkWood);
            b.Beam(new Vector3(-1.5f, 3.4f, 0f), new Vector3(1.5f, 3.5f, 0f), 0.07f, DarkWood);
            b.Box(new Vector3(0f, 2.8f, 0f), new Vector3(0.55f, 0.9f, 0.22f), Cloth(112, 90, 66));
            b.Box(new Vector3(-1f, 3.3f, 0f), new Vector3(0.5f, 0.2f, 0.16f), Cloth(112, 90, 66));
            b.Box(new Vector3(1f, 3.4f, 0f), new Vector3(0.5f, 0.2f, 0.16f), Cloth(112, 90, 66));
            b.Tube(new Vector3(0f, 3.9f, 0f), new Vector3(0f, 4.5f, 0f), 0.34f, 0.28f, 7, Cloth(204, 184, 140));
            b.Tube(new Vector3(0f, 4.42f, 0f), new Vector3(0f, 4.56f, 0f), 0.78f, 0.7f, 8, Thatch);
            b.Tube(new Vector3(0f, 4.56f, 0f), new Vector3(0f, 5f, 0f), 0.4f, 0.14f, 8, Thatch);
        }

        private static void BuildDryingRack(MeshBuilder b, WorldRandom random)
        {
            foreach (float x in new[] { -2.7f, 2.7f })
            {
                b.Beam(new Vector3(x, 0f, -0.6f), new Vector3(x, 3.4f, 0f), 0.07f, DarkWood);
                b.Beam(new Vector3(x, 0f, 0.6f), new Vector3(x, 3.4f, 0f), 0.07f, DarkWood);
            }
            b.Tube(new Vector3(-2.9f, 3.25f, 0f), new Vector3(2.9f, 3.25f, 0f), 0.07f, 0.07f, 5, DarkWood);

            // Nets and fish drying in the air.
            for (int i = 0; i < 9; i++)
            {
                float x = -2.3f + i * 0.58f + random.Range(-0.08f, 0.08f);
                b.Box(new Vector3(x, 2.5f, 0f), new Vector3(0.13f, random.Range(0.5f, 0.74f), 0.03f), Cloth(116, 112, 102));
            }
        }

        private static void BuildBench(MeshBuilder b)
        {
            b.Box(new Vector3(0f, 1.15f, 0f), new Vector3(1.75f, 0.1f, 0.42f), Wood());
            foreach (float x in new[] { -1.4f, 1.4f })
            {
                b.Box(new Vector3(x, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.36f), DarkWood);
            }
        }

        private static void BuildTent(MeshBuilder b, Vector2 half, VertexMaterial cloth)
        {
            float height = half.Y * 1.5f;
            float length = half.X;
            b.TilesPerMetre = 0.5f;
            b.Quad(new Vector3(-length, 0f, half.Y), new Vector3(length, 0f, half.Y), new Vector3(length, height, 0f), new Vector3(-length, height, 0f), cloth);
            b.Quad(new Vector3(length, 0f, -half.Y), new Vector3(-length, 0f, -half.Y), new Vector3(-length, height, 0f), new Vector3(length, height, 0f), cloth);

            VertexMaterial dark = new VertexMaterial(TextureGenerator.flat, new Colour(18, 15, 12));
            b.Triangle(new Vector3(length, 0f, half.Y), new Vector3(length, 0f, -half.Y), new Vector3(length, height, 0f), dark);
            b.Triangle(new Vector3(-length, 0f, -half.Y), new Vector3(-length, 0f, half.Y), new Vector3(-length, height, 0f), cloth);

            b.TilesPerMetre = 0.2f;
            b.Tube(new Vector3(-length - 0.3f, height + 0.06f, 0f), new Vector3(length + 0.3f, height + 0.06f, 0f), 0.09f, 0.09f, 5, DarkWood);
            foreach (float x in new[] { -length, length })
            {
                b.Tube(new Vector3(x, 0f, 0f), new Vector3(x, height, 0f), 0.08f, 0.08f, 5, DarkWood);
            }
        }

        private static void BuildCampfire(MeshBuilder b, WorldRandom random)
        {
            for (int i = 0; i < 9; i++)
            {
                float angle = i * (MathF.Tau / 9f);
                Vector3 at = new Vector3(MathF.Cos(angle), 0.14f, MathF.Sin(angle)) * new Vector3(1.28f, 1f, 1.28f);
                b.Add(IcoSphereGenerator.CreateIcosphere(1, Rough(150)), at, new Vector3(random.Range(0.26f, 0.4f), random.Range(0.2f, 0.3f), random.Range(0.26f, 0.4f)));
            }
            VertexMaterial charred = new VertexMaterial(TextureGenerator.oakBark, new Colour(40, 34, 30));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * (MathF.Tau / 5f) + random.Range(-0.2f, 0.2f);
                Vector3 out_ = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
                b.Tube(out_ * 0.9f + Vector3.UnitY * 0.1f, Vector3.UnitY * 0.9f, 0.13f, 0.09f, 5, charred);
            }
            b.Add(IcoSphereGenerator.CreateIcosphere(1, FireGlow), new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.34f, 0.5f));
        }

        private static void BuildLogStack(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial bark = new VertexMaterial(TextureGenerator.pineBark, new Colour(220, 200, 180));
            for (int row = 0; row < 3; row++)
            {
                int count = 4 - row;
                for (int i = 0; i < count; i++)
                {
                    float z = (i - (count - 1) * 0.5f) * 1f;
                    float y = 0.45f + row * 0.82f;
                    float x = random.Range(-0.4f, 0.4f);
                    b.Tube(new Vector3(x - 3.7f, y, z), new Vector3(x + 3.7f, y, z), 0.46f, 0.42f, 8, bark);
                }
            }
            foreach (float x in new[] { -2.6f, 2.6f })
            {
                b.Beam(new Vector3(x, 0f, 2f), new Vector3(x, 2f, 2f), 0.1f, DarkWood);
                b.Beam(new Vector3(x, 0f, -2f), new Vector3(x, 2f, -2f), 0.1f, DarkWood);
            }
        }

        private static void BuildBoat(MeshBuilder b)
        {
            VertexMaterial hull = Wood(106, 80, 60);
            Vector3 Hull(float u, float v, float side)
            {
                float along = (u * 2f - 1f) * 4.1f;
                float beam = 1.5f * MathF.Pow(MathF.Max(1f - MathF.Abs(u * 2f - 1f), 0f), 0.55f) + 0.02f;
                float sheer = 0.45f * MathF.Pow(MathF.Abs(u * 2f - 1f), 2f);
                float z = beam * v * side;
                float y = 0.1f + 1.15f * v * v + sheer * v;
                return new Vector3(along, y, z);
            }

            b.Surface(14, 3, (u, v) => Hull(u, v, 1f), hull);
            b.Surface(14, 3, (u, v) => Hull(1f - u, v, -1f), hull);

            foreach (float x in new[] { -1.4f, 0.1f, 1.6f })
            {
                b.Box(new Vector3(x, 0.92f, 0f), new Vector3(0.22f, 0.05f, 1.1f), Wood());
            }
            b.Tube(new Vector3(-0.6f, 1f, 1.05f), new Vector3(2.8f, 1.2f, 0.5f), 0.05f, 0.04f, 4, DarkWood);
        }

        private static void BuildJetty(MeshBuilder b, Vector2 half)
        {
            VertexMaterial planks = Wood(126, 104, 82);
            b.Box(new Vector3(0f, 0f, 0f), new Vector3(half.X, 0.1f, half.Y), planks);
            for (float z = -half.Y + 1f; z <= half.Y; z += 4.2f)
            {
                foreach (float x in new[] { -half.X + 0.2f, half.X - 0.2f })
                {
                    b.Tube(new Vector3(x, -3.5f, z), new Vector3(x, 1.2f, z), 0.2f, 0.17f, 6, DarkWood);
                }
            }
        }

        private static void BuildWatchTower(MeshBuilder b, BuildingStyle style)
        {
            bool stone = style == BuildingStyle.Stone;
            VertexMaterial post = stone ? Stone(190) : DarkWood;
            VertexMaterial roof = style switch
            {
                BuildingStyle.Eastern => new VertexMaterial(TextureGenerator.shingle, new Colour(150, 150, 160)),
                BuildingStyle.Stone => new VertexMaterial(TextureGenerator.slate, new Colour(210, 214, 226)),
                _ => Thatch,
            };

            float top = 15f;
            if (stone)
            {
                b.Box(new Vector3(0f, top * 0.5f - 1f, 0f), new Vector3(3.9f, top * 0.5f + 1f, 3.9f), post);
            }
            else
            {
                foreach (float x in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { -1f, 1f })
                    {
                        b.Tube(new Vector3(x * 3.9f, -1f, z * 3.9f), new Vector3(x * 3f, top + 4f, z * 3f), 0.34f, 0.26f, 6, post);
                    }
                }
                for (int level = 0; level < 3; level++)
                {
                    float y = 3f + level * 4.2f;
                    float s = 3.75f - level * 0.2f;
                    b.Beam(new Vector3(-s, y, s), new Vector3(s, y + 2.4f, s), 0.1f, post);
                    b.Beam(new Vector3(s, y, -s), new Vector3(-s, y + 2.4f, -s), 0.1f, post);
                    b.Beam(new Vector3(s, y, s), new Vector3(s, y + 2.4f, -s), 0.1f, post);
                    b.Beam(new Vector3(-s, y, -s), new Vector3(-s, y + 2.4f, s), 0.1f, post);
                }
            }

            b.Box(new Vector3(0f, top, 0f), new Vector3(4.4f, 0.2f, 4.4f), Wood());
            for (int side = 0; side < 4; side++)
            {
                b.Place(Vector3.Zero, side * MathF.PI * 0.5f);
                b.Box(new Vector3(0f, top + 1.1f, 4.3f), new Vector3(4.4f, 0.9f, 0.1f), Wood(112, 86, 64));
            }
            b.ResetPlacement();

            b.TilesPerMetre = 0.14f;
            float eave = top + 3.9f;
            for (int side = 0; side < 4; side++)
            {
                b.Place(new Vector3(0f, 0f, 0f), side * MathF.PI * 0.5f);
                b.Quad(new Vector3(-5.2f, eave, 5.2f), new Vector3(5.2f, eave, 5.2f), new Vector3(0.2f, eave + 3.9f, 0.2f), new Vector3(-0.2f, eave + 3.9f, 0.2f), roof);
            }
            b.ResetPlacement();
        }

        private static void BuildGuardTower(MeshBuilder b)
        {
            VertexMaterial logs = new VertexMaterial(TextureGenerator.pineBark, new Colour(190, 170, 150));
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    b.Tube(new Vector3(x * 3f, -1f, z * 3f), new Vector3(x * 2.5f, 10.5f, z * 2.5f), 0.27f, 0.2f, 6, logs);
                }
            }
            b.Beam(new Vector3(-2.8f, 2f, 2.8f), new Vector3(2.8f, 5f, 2.8f), 0.08f, DarkWood);
            b.Beam(new Vector3(2.8f, 2f, -2.8f), new Vector3(-2.8f, 5f, -2.8f), 0.08f, DarkWood);
            b.Box(new Vector3(0f, 7.6f, 0f), new Vector3(3.2f, 0.16f, 3.2f), Wood());
            for (int side = 0; side < 4; side++)
            {
                b.Place(Vector3.Zero, side * MathF.PI * 0.5f);
                for (int i = -3; i <= 3; i++)
                {
                    float x = i * 0.95f;
                    b.Tube(new Vector3(x, 7.6f, 3.1f), new Vector3(x, 9.5f + (i % 2) * 0.28f, 3.1f), 0.17f, 0.04f, 5, logs);
                }
            }
            b.ResetPlacement();
        }

        private static void BuildStoneTower(MeshBuilder b)
        {
            VertexMaterial wall = Stone(176);
            b.TilesPerMetre = 0.12f;
            b.Tube(new Vector3(0f, -2f, 0f), new Vector3(0f, 19f, 0f), 6.2f, 5.6f, 16, wall);
            b.Tube(new Vector3(0f, 19f, 0f), new Vector3(0f, 20.2f, 0f), 6.2f, 6.2f, 16, wall);
            b.TilesPerMetre = 0.2f;
            for (int i = 0; i < 12; i++)
            {
                float angle = i * (MathF.Tau / 12f);
                b.Box(new Vector3(MathF.Cos(angle) * 5.7f, 21f, MathF.Sin(angle) * 5.7f), new Vector3(0.76f, 0.8f, 0.76f), wall);
            }
        }

        private static void BuildKeep(MeshBuilder b, Vector2 half)
        {
            VertexMaterial wall = Stone(168);
            b.TilesPerMetre = 0.12f;
            float height = 22f;
            b.Box(new Vector3(0f, height * 0.5f - 2f, 0f), new Vector3(half.X, height * 0.5f + 2f, half.Y), wall);
            b.TilesPerMetre = 0.2f;

            // Battlements.
            for (float x = -half.X + 1f; x <= half.X - 0.5f; x += 2.4f)
            {
                foreach (float z in new[] { -half.Y, half.Y })
                {
                    b.Box(new Vector3(x, height + 1f, z), new Vector3(0.72f, 1f, 0.5f), wall);
                }
            }
            for (float z = -half.Y + 1f; z <= half.Y - 0.5f; z += 2.4f)
            {
                foreach (float x in new[] { -half.X, half.X })
                {
                    b.Box(new Vector3(x, height + 1f, z), new Vector3(0.5f, 1f, 0.72f), wall);
                }
            }

            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    b.TilesPerMetre = 0.12f;
                    Vector3 corner = new Vector3(x * half.X, 0f, z * half.Y);
                    b.Tube(corner - Vector3.UnitY * 2f, corner + Vector3.UnitY * (height + 5f), 3.2f, 2.8f, 12, wall);
                    b.Tube(corner + Vector3.UnitY * (height + 5f), corner + Vector3.UnitY * (height + 10f), 3.3f, 0.1f, 12, new VertexMaterial(TextureGenerator.slate, new Colour(190, 194, 206)));
                    b.TilesPerMetre = 0.2f;
                }
            }

            // The great door, and a banner above it.
            float front = half.Y + 0.06f;
            b.Quad(new Vector3(-3.4f, 0f, front), new Vector3(3.4f, 0f, front), new Vector3(3.4f, 9f, front), new Vector3(-3.4f, 9f, front), DarkWood);
            b.Beam(new Vector3(-3.9f, 9.3f, front + 0.2f), new Vector3(3.9f, 9.3f, front + 0.2f), 0.4f, Stone(150));
            b.Quad(new Vector3(-2f, 11f, front + 0.08f), new Vector3(2f, 11f, front + 0.08f), new Vector3(2f, 19f, front + 0.08f), new Vector3(-2f, 19f, front + 0.08f), Cloth(112, 28, 30));
        }

        // A gate of two posts and a double lintel, marking holy ground.
        private static void BuildGateArch(MeshBuilder b)
        {
            VertexMaterial red = new VertexMaterial(TextureGenerator.wood, new Colour(140, 46, 34));
            foreach (float x in new[] { -3.5f, 3.5f })
            {
                b.Tube(new Vector3(x, -0.4f, 0f), new Vector3(x * 0.94f, 9.4f, 0f), 0.42f, 0.34f, 10, red);
                b.Tube(new Vector3(x, -0.4f, 0f), new Vector3(x, 1f, 0f), 0.62f, 0.54f, 10, Rough(170));
            }
            b.Beam(new Vector3(-4.1f, 7.6f, 0f), new Vector3(4.1f, 7.6f, 0f), 0.24f, red);

            VertexMaterial top = new VertexMaterial(TextureGenerator.wood, new Colour(46, 36, 32));
            b.Surface(10, 1, (u, v) =>
            {
                float x = (u * 2f - 1f) * 5.3f;
                float lift = 0.42f * x * x / 28f;
                return new Vector3(x, 9.7f + lift + v * 0.42f, 0f);
            }, top);
            b.Surface(10, 1, (u, v) =>
            {
                float x = (u * 2f - 1f) * 5.3f;
                float lift = 0.42f * x * x / 28f;
                return new Vector3(x, 9.7f + lift + 0.42f, (v * 2f - 1f) * 0.42f);
            }, top);
            b.Box(new Vector3(0f, 8.75f, 0f), new Vector3(0.48f, 0.9f, 0.14f), Wood());
        }

        private static void BuildGatehouse(MeshBuilder b, Vector2 half)
        {
            VertexMaterial wall = Stone(172);
            foreach (float x in new[] { -1f, 1f })
            {
                b.Box(new Vector3(x * (half.X - 1.4f), 7f, 0f), new Vector3(1.6f, 9.5f, half.Y), wall);
            }
            b.Box(new Vector3(0f, 12.5f, 0f), new Vector3(half.X - 0.1f, 3.9f, half.Y), wall);
            b.Beam(new Vector3(-half.X + 2.6f, 8f, half.Y + 0.1f), new Vector3(half.X - 2.6f, 8f, half.Y + 0.1f), 0.36f, DarkWood);
            for (float x = -half.X + 0.8f; x <= half.X - 0.5f; x += 2.2f)
            {
                foreach (float z in new[] { -half.Y, half.Y })
                {
                    b.Box(new Vector3(x, 17.4f, z), new Vector3(0.66f, 1f, 0.42f), wall);
                }
            }
        }

        // The way between worlds: a ring of old stone with a pale light standing in it.
        private static void BuildWaygate(MeshBuilder b)
        {
            VertexMaterial stone = Rough(158);
            const int Blocks = 11;
            const float Radius = 4.4f;
            for (int i = 0; i <= Blocks; i++)
            {
                float angle = MathF.PI * i / Blocks;
                Vector3 at = new Vector3(MathF.Cos(angle) * Radius, 1.2f + MathF.Sin(angle) * Radius * 1.25f, 0f);
                Vector3 outward = new Vector3(MathF.Cos(angle), MathF.Sin(angle) * 1.25f, 0f).Normalized();
                Vector3 next = at + outward * 1.1f;
                b.Beam(at, next, 0.74f, stone);
                if (i % 2 == 0)
                {
                    b.Box(at + outward * 0.55f + Vector3.UnitZ * 0.76f, new Vector3(0.18f), Glow(0.35f, 0.95f, 1f, 1.1f));
                }
            }
            b.Box(new Vector3(0f, 0f, 0f), new Vector3(4.6f, 0.34f, 1.5f), stone);

            b.Surface(16, 8, (u, v) =>
            {
                float angle = MathF.PI * (1f - u);
                float radius = (Radius - 0.5f) * v;
                return new Vector3(MathF.Cos(angle) * radius, 1.2f + MathF.Sin(angle) * radius * 1.25f, 0f);
            }, Glow(0.22f, 0.6f, 0.75f, 0.32f));
            b.Quad(new Vector3(-(Radius - 0.5f), 0.3f, 0f), new Vector3(Radius - 0.5f, 0.3f, 0f), new Vector3(Radius - 0.5f, 1.2f, 0f), new Vector3(-(Radius - 0.5f), 1.2f, 0f), Glow(0.22f, 0.6f, 0.75f, 0.32f));
        }

        private static void BuildAltar(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial stone = Rough(176);
            b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 0.32f, 0f), 1.8f, 1.6f, 8, stone);
            b.Tube(new Vector3(0f, 0.32f, 0f), new Vector3(0f, 1.3f, 0f), 0.92f, 0.76f, 8, stone);
            b.Tube(new Vector3(0f, 1.3f, 0f), new Vector3(0f, 1.6f, 0f), 1.1f, 1.1f, 8, stone);

            Mesh crystal = IcoSphereGenerator.CreateIcosphere(1, Glow(0.45f, 1f, 0.72f, 2.8f));
            b.Add(crystal, new Vector3(0f, 2.5f, 0f), new Vector3(0.38f, 0.92f, 0.38f), random.Float() * MathF.Tau);
        }

        private static void BuildGrave(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial stone = Rough(random.Int(128, 168));
            b.Box(new Vector3(0f, 0.76f, -1.05f), new Vector3(0.64f, 1.1f, 0.14f), stone);
            b.Tube(new Vector3(-0.64f, 1.86f, -1.05f), new Vector3(0.64f, 1.86f, -1.05f), 0.14f, 0.14f, 6, stone);

            VertexMaterial earth = new VertexMaterial(TextureGenerator.soil, new Colour(186, 170, 150));
            b.Surface(6, 3, (u, v) =>
            {
                float x = (u * 2f - 1f) * 0.72f;
                float z = (v * 2f - 1f) * 1.2f;
                return new Vector3(x, 0.36f * (1f - x * x / 0.6f) * (1f - z * z / 1.6f), z + 0.3f);
            }, earth);
        }

        private static void BuildTomb(MeshBuilder b, Vector2 half)
        {
            VertexMaterial stone = Stone(158);
            b.Box(new Vector3(0f, 0.7f, 0f), new Vector3(half.X, 1.8f, half.Y), stone);
            b.Box(new Vector3(0f, 3.8f, 0f), new Vector3(half.X - 0.6f, 1.4f, half.Y - 0.6f), stone);
            float z = half.Y - 0.5f;
            b.TilesPerMetre = 0.16f;
            b.Quad(new Vector3(-half.X - 0.3f, 5.2f, half.Y + 0.3f), new Vector3(half.X + 0.3f, 5.2f, half.Y + 0.3f), new Vector3(half.X * 0.3f, 7.4f, 0f), new Vector3(-half.X * 0.3f, 7.4f, 0f), stone);
            b.Quad(new Vector3(half.X + 0.3f, 5.2f, -half.Y - 0.3f), new Vector3(-half.X - 0.3f, 5.2f, -half.Y - 0.3f), new Vector3(-half.X * 0.3f, 7.4f, 0f), new Vector3(half.X * 0.3f, 7.4f, 0f), stone);
            b.TilesPerMetre = 0.2f;
            b.Quad(new Vector3(-1.5f, 0f, z + 0.56f), new Vector3(1.5f, 0f, z + 0.56f), new Vector3(1.5f, 3.2f, z + 0.56f), new Vector3(-1.5f, 3.2f, z + 0.56f), new VertexMaterial(TextureGenerator.flat, new Colour(10, 10, 12)));
        }

        private static void BuildMonolith(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial stone = Rough(160);
            float height = random.Range(7.5f, 10f);
            float lean = random.Range(-0.5f, 0.5f);
            b.Tube(new Vector3(0f, -1f, 0f), new Vector3(lean, height, lean * 0.5f), 1.45f, 0.72f, 5, stone);
            b.Add(IcoSphereGenerator.CreateIcosphere(1, Glow(0.3f, 0.75f, 1f, 1.2f)), new Vector3(lean * 0.55f, height * 0.62f, 1f), new Vector3(0.22f, 0.5f, 0.1f));
        }

        private static void BuildRuinWall(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial wall = Stone(162);
            float top = 0f;
            for (float x = -4.5f; x < 4.5f; x += 1.5f)
            {
                top = random.Range(1.4f, 5.6f);
                b.Box(new Vector3(x + 0.75f, top * 0.5f - 0.5f, 0f), new Vector3(0.76f, top * 0.5f + 0.5f, 0.7f), wall);
            }
        }

        private static void BuildRuinColumn(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial stone = Rough(174);
            float height = random.Range(2.5f, 8f);
            b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 0.5f, 0f), 1.1f, 0.96f, 8, stone);
            b.Tube(new Vector3(0f, 0.5f, 0f), new Vector3(0f, height, 0f), 0.76f, 0.68f, 10, stone);
        }

        private static void BuildRubble(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial stone = Stone(150);
            for (int i = 0; i < 8; i++)
            {
                Vector3 at = new Vector3(random.Range(-1.8f, 1.8f), random.Range(0f, 0.45f), random.Range(-1.8f, 1.8f));
                b.Place(at, random.Float() * MathF.Tau);
                b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(random.Range(0.3f, 0.75f), random.Range(0.2f, 0.45f), random.Range(0.3f, 0.6f)), stone);
            }
            b.ResetPlacement();
        }

        private static void BuildCairn(MeshBuilder b, WorldRandom random)
        {
            for (int layer = 0; layer < 5; layer++)
            {
                int stones = 5 - layer;
                float radius = 0.84f - layer * 0.17f;
                for (int i = 0; i < stones; i++)
                {
                    float angle = i * (MathF.Tau / stones) + random.Float();
                    Vector3 at = new Vector3(MathF.Cos(angle) * radius, 0.22f + layer * 0.42f, MathF.Sin(angle) * radius);
                    float size = 0.42f - layer * 0.03f;
                    b.Add(IcoSphereGenerator.CreateIcosphere(1, Rough(random.Int(140, 186))), at, new Vector3(size, size * 0.72f, size));
                }
            }
        }

        // Logs in a row along +x from 0 to 1. The run is stretched along x to its length, so widths here are one over that.
        private static void BuildPalisade(MeshBuilder b, WorldRandom random)
        {
            VertexMaterial logs = new VertexMaterial(TextureGenerator.pineBark, new Colour(200, 178, 156));
            const int Logs = 8;
            const float Stretch = 4f;
            b.Stretch = new Vector3(1f / Stretch, 1f, 1f);
            for (int i = 0; i < Logs; i++)
            {
                float x = (i + 0.5f) / Logs * Stretch;
                float height = random.Range(6.1f, 7f);
                b.Tube(new Vector3(x, -1f, 0f), new Vector3(x, height, 0f), 0.28f, 0.26f, 6, logs, capped: false);
                b.Tube(new Vector3(x, height, 0f), new Vector3(x, height + 0.8f, 0f), 0.26f, 0.02f, 6, logs);
            }
            b.Tube(new Vector3(0f, 4.2f, 0.24f), new Vector3(Stretch, 4.2f, 0.24f), 0.09f, 0.09f, 5, DarkWood);
            b.ResetPlacement();
        }

        private static void BuildStoneWall(MeshBuilder b)
        {
            VertexMaterial wall = Stone(170);
            const float Stretch = 4.5f;
            b.Stretch = new Vector3(1f / Stretch, 1f, 1f);
            b.Box(new Vector3(Stretch * 0.5f, 1.4f, 0f), new Vector3(Stretch * 0.5f, 2.6f, 0.76f), wall);
            foreach (float x in new[] { 0.7f, 2.25f, 3.8f })
            {
                b.Box(new Vector3(x, 4.5f, 0f), new Vector3(0.52f, 0.5f, 0.76f), wall);
            }
            b.ResetPlacement();
        }
    }
}
