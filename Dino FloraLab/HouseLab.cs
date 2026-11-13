using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Architecture;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Textures;
using Dino_Engine.Util;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;
using System.Diagnostics;

namespace Dino_FloraLab
{
    /// <summary>
    /// Builds a way of building's houses with no window and no GPU, to look at them from the game's own height and low down, from the front and the back.
    ///   houses [--style nordic] [--kinds hut,cottage] [--variants N] [--tile px] [--out file.png]   Every building of the catalogue in a style, a row each.
    /// A building is drawn flat-shaded, each face lit by the way it's wound, so a face wound inside out shows dark: the game's own light and textures are what it really
    /// looks like.
    /// </summary>
    internal static class HouseLab
    {
        // What each material's texture comes to, roughly, under the colour in its vertices: the lab has no textures, and this is what they'd multiply it by.
        private static readonly Dictionary<int, Vector3> Albedo = new()
        {
            [MaterialCatalog.PaintedBoards.Index] = new Vector3(0.7f, 0.69f, 0.66f),
            [MaterialCatalog.Plaster.Index] = new Vector3(0.59f, 0.56f, 0.49f),
            [MaterialCatalog.StoneWall.Index] = new Vector3(0.45f, 0.43f, 0.39f),
            [MaterialCatalog.RoofTiles.Index] = new Vector3(0.42f, 0.19f, 0.11f),
            [MaterialCatalog.Grass.Index] = new Vector3(0.22f, 0.24f, 0.12f),
            [MaterialCatalog.Shingle.Index] = new Vector3(0.34f, 0.31f, 0.28f),
            [MaterialCatalog.Wood.Index] = new Vector3(0.38f, 0.29f, 0.2f),
            [MaterialCatalog.Brick.Index] = new Vector3(0.37f, 0.17f, 0.12f),
            [MaterialCatalog.Slate.Index] = new Vector3(0.27f, 0.29f, 0.33f),
            [MaterialCatalog.BarkBare.Index] = new Vector3(0.7f, 0.68f, 0.64f),
            [MaterialCatalog.Iron.Index] = new Vector3(0.6f, 0.6f, 0.6f),
            [MaterialCatalog.Thatch.Index] = new Vector3(0.42f, 0.34f, 0.22f),
            [MaterialCatalog.Flat.Index] = Vector3.One,
            [MaterialCatalog.FlatGlow.Index] = new Vector3(0.45f),     // Glass: what glows faintly in the game is dark in daylight here.
        };

        private const int Ground = 999;

        public static int Sheet(Options options)
        {
            BuildingStyle style = Enum.Parse<BuildingStyle>(options.Text("style", "nordic"), ignoreCase: true);
            StructureDefinition[] all = { StructureCatalog.Hut, StructureCatalog.Cottage, StructureCatalog.House, StructureCatalog.Longhouse, StructureCatalog.Barn, StructureCatalog.Inn, StructureCatalog.Hall, StructureCatalog.Temple, StructureCatalog.Shed };
            // Any structure of the catalogue by its id, props among them.
            StructureDefinition[] kinds = options.Has("kinds")
                ? options.Text("kinds", "").Split(',').Select(id => StructureCatalog.TryGetPlain(id, out StructureDefinition found) ? found : throw new ArgumentException($"No structure called {id}.")).ToArray()
                : all;
            int variants = options.Int("variants", 2);
            int tile = options.Int("tile", 520);
            const int Shrink = 2, Columns = 3;
            float high = MathHelper.DegreesToRadians(67f), low = MathHelper.DegreesToRadians(options.Float("angle", 22f));

            var jobs = new List<(int Row, StructureDefinition Kind, int Variant)>();
            foreach (StructureDefinition kind in kinds)
            {
                for (int v = 0; v < Math.Min(variants, Math.Max(kind.Variants, 1)); v++) jobs.Add((jobs.Count, kind, v));
            }

            var sheet = new byte[Columns * tile * jobs.Count * tile * 4];
            Parallel.ForEach(jobs, job =>
            {
                var clock = Stopwatch.StartNew();
                var chimneys = new List<Vector3>();
                var lights = new List<AssetLight>();
                // As WorldAssetLibrary does: a house of the catalogue by its builder, anything else, a watchtower among them, as a prop.
                Mesh mesh = job.Kind.Styled && job.Kind != StructureCatalog.WatchTower
                    ? BuildingGenerator.Generate(job.Kind, style, job.Variant, chimneys, lights)
                    : PropGenerator.Generate(job.Kind, style, lights);
                long built = clock.ElapsedMilliseconds;
                FloraMesh scene = ToFlora(mesh, out int triangles, out Vector3 min, out Vector3 max);
                AddGround(scene, MathF.Max(max.X - min.X, max.Z - min.Z) * 1.6f + 20f);
                float span = MathF.Max(MathF.Max(max.X - min.X, max.Z - min.Z), max.Y) * 1.3f;
                Vector3 focus = new Vector3((min.X + max.X) * 0.5f, max.Y * 0.38f, (min.Z + max.Z) * 0.5f);
                var preview = new Preview(tile * Shrink, tile * Shrink, null!) { GrainFloor = 0.9f, GrainDepth = 0.2f };
                (float pitch, float yaw)[] views = { (high, MathF.PI), (low, MathF.PI + 0.62f), (high, 0.35f) };
                for (int c = 0; c < Columns; c++)
                {
                    preview.Draw(scene, views[c].pitch, views[c].yaw, span, focus);
                    preview.Blit(sheet, Columns * tile, c * tile, job.Row * tile, Shrink);
                }
                Console.WriteLine($"{StructureCatalog.AssetId(job.Kind, style, job.Variant),-22} {triangles,6} triangles, {lights.Count,3} lights, {chimneys.Count} chimneys, {max.Y:0.0} m tall, built in {built} ms");
            });

            string path = options.Text("out", "houses.png");
            PngWriter.Write(path, sheet, Columns * tile, jobs.Count * tile);
            Console.WriteLine($"Wrote {path}");
            return 0;
        }

        // A building as the lab draws it: every triangle flat, its normal by its winding, its colour its vertices' times what its material comes to. Its bounds leave out
        // what's under the ground.
        private static FloraMesh ToFlora(Mesh mesh, out int triangles, out Vector3 min, out Vector3 max)
        {
            Vertex[] vertices = mesh.getAllVerticesArray();
            vIndex[] indices = mesh.getAllIndicesArray();
            var flora = new FloraMesh();
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            triangles = indices.Length / 3;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vertex a = vertices[indices[i].index], b = vertices[indices[i + 1].index], c = vertices[indices[i + 2].index];
                Vector3 normal = Vector3.Cross(b.position - a.position, c.position - a.position);
                if (normal.LengthSquared < 1e-12f) continue;
                normal.Normalize();
                int first = flora.VertexCount;
                foreach (Vertex v in new[] { a, b, c })
                {
                    Vector3 albedo = Albedo.TryGetValue(v.materialTextureIndex, out Vector3 found) ? found : new Vector3(0.5f);
                    flora.Add(new FloraVertex { Position = v.position, Normal = normal, Tangent = Vector3.UnitX, Colour = v.colour.ToVector3() * albedo, Material = 0, Joint = -1 });
                    if (v.position.Y > -0.05f)
                    {
                        min = Vector3.ComponentMin(min, v.position);
                        max = Vector3.ComponentMax(max, v.position);
                    }
                }
                flora.Triangle(first, first + 1, first + 2);
            }
            return flora;
        }

        // The ground, flat, which hides what reaches down into it.
        private static void AddGround(FloraMesh scene, float size)
        {
            const int Cells = 40;
            int first = scene.VertexCount;
            for (int j = 0; j <= Cells; j++)
            {
                for (int i = 0; i <= Cells; i++)
                {
                    Vector2 p = new Vector2(i / (float)Cells - 0.5f, j / (float)Cells - 0.5f) * size;
                    float mottle = FloraNoise.Fbm(new Vector3(p.X * 0.3f, p.Y * 0.3f, 0.5f), 5u, 3);
                    Vector3 colour = Vector3.Lerp(new Vector3(0.12f, 0.16f, 0.06f), new Vector3(0.17f, 0.2f, 0.08f), mottle);
                    scene.Add(new FloraVertex { Position = new Vector3(p.X, -0.02f, p.Y), Normal = Vector3.UnitY, Tangent = Vector3.UnitX, Colour = colour, Material = Ground, Joint = -1 });
                }
            }
            for (int j = 0; j < Cells; j++)
            {
                for (int i = 0; i < Cells; i++)
                {
                    int a = first + j * (Cells + 1) + i;
                    scene.Triangle(a, a + Cells + 1, a + Cells + 2);
                    scene.Triangle(a, a + Cells + 2, a + 1);
                }
            }
        }
    }
}
