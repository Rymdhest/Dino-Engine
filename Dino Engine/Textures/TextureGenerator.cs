using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Rendering;
using Dino_Engine.Textures.Synthesis;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System.Diagnostics;
using static Dino_Engine.Modelling.Model.glModel;

namespace Dino_Engine.Textures
{

    public enum arrayType     {
        material,
        imposter
    }

    /// <summary>
    /// Makes every texture the engine draws with: the tiling materials of <see cref="MaterialCatalog"/>, each a layer of three arrays and every one grown from its
    /// recipe (<see cref="MaterialRecipes"/>, TEXTURES.md) at start; the cards plants are made of (<see cref="CardLibrary"/>); and the imposters far trees become.
    /// </summary>
    public class TextureGenerator
    {
        public int megaAlbedoTextureArray;
        public int megaNormalTextureArray;
        public int megaMaterialTextureArray;

        public int megaAlbedoModelTextureArray;
        public int megaNormalModelTextureArray;
        public int megaMaterialModelTextureArray;

        public int megaAlbedoImposterTextureArray;
        public int megaNormalImposterTextureArray;
        public int megaMaterialImposterTextureArray;

        public int loadedModelTextures = 0;
        public int loadedMaterialTextures = 0;
        public int loadedImposterTextures = 0;

        public static readonly Vector2i MATERIAL_RESOLUTION = new Vector2i(2048);
        public static readonly Vector2i IMPOSTER_RESOLUTION = new Vector2i(256);
        public readonly int anglesPerImposter = 8;

        // Every material's layer, by the names the rest of the engine has always used. They're known before anything is drawn, from the catalogue's order.
        public static int flat = MaterialCatalog.Flat.Index;
        public static int flatGlow = MaterialCatalog.FlatGlow.Index;
        public static int mirror = MaterialCatalog.Mirror.Index;
        public static int gem = MaterialCatalog.Gem.Index;
        public static int wax = MaterialCatalog.Wax.Index;
        public static int grain = MaterialCatalog.Grain.Index;
        public static int foliage_white = MaterialCatalog.FoliageWhite.Index;
        public static int soil = MaterialCatalog.Soil.Index;
        public static int grass = MaterialCatalog.Grass.Index;
        public static int grassy_soil = MaterialCatalog.GrassySoil.Index;
        public static int sand = MaterialCatalog.Sand.Index;
        public static int sandDunes = MaterialCatalog.SandDunes.Index;
        public static int crackedDesert = MaterialCatalog.CrackedDesert.Index;
        public static int crackedLava = MaterialCatalog.CrackedLava.Index;
        public static int snow = MaterialCatalog.Snow.Index;
        public static int ice = MaterialCatalog.Ice.Index;
        public static int mud = MaterialCatalog.Mud.Index;
        public static int moss = MaterialCatalog.Moss.Index;
        public static int gravel = MaterialCatalog.Gravel.Index;
        public static int pebbles = MaterialCatalog.Pebbles.Index;
        public static int cobble = MaterialCatalog.Cobble.Index;
        public static int setts = MaterialCatalog.Setts.Index;
        public static int roadDirt = MaterialCatalog.RoadDirt.Index;
        public static int farmland = MaterialCatalog.Farmland.Index;
        /// <summary>Bare rock, for the mountains and every stone: grey, as all its colour comes from the world's <see cref="World.Geology"/>. See Rock.glsl.</summary>
        public static int cliff = MaterialCatalog.Cliff.Index;
        public static int rock = MaterialCatalog.Rock.Index;
        public static int stoneWall = MaterialCatalog.StoneWall.Index;
        public static int brick = MaterialCatalog.Brick.Index;
        public static int plaster = MaterialCatalog.Plaster.Index;
        public static int wood = MaterialCatalog.Wood.Index;
        public static int thatch = MaterialCatalog.Thatch.Index;
        public static int shingle = MaterialCatalog.Shingle.Index;
        public static int slate = MaterialCatalog.Slate.Index;
        public static int metalFloor = MaterialCatalog.MetalFloor.Index;
        public static int iron = MaterialCatalog.Iron.Index;
        public static int barkFurrowed = MaterialCatalog.BarkFurrowed.Index;
        public static int barkPlated = MaterialCatalog.BarkPlated.Index;
        public static int BirchBark = MaterialCatalog.BarkPapery.Index;
        public static int barkSmooth = MaterialCatalog.BarkSmooth.Index;
        public static int barkFibrous = MaterialCatalog.BarkFibrous.Index;
        public static int barkRinged = MaterialCatalog.BarkRinged.Index;
        public static int barkBare = MaterialCatalog.BarkBare.Index;
        public static int barkCharred = MaterialCatalog.BarkCharred.Index;
        public static int pineBark = MaterialCatalog.PineBark.Index;
        public static int plate = MaterialCatalog.Plate.Index;
        public static int mail = MaterialCatalog.Mail.Index;
        public static int leather = MaterialCatalog.Leather.Index;
        public static int cloth = MaterialCatalog.Cloth.Index;
        public static int bone = MaterialCatalog.Bone.Index;
        public static int flagstones = MaterialCatalog.Flagstones.Index;
        public static int needles = MaterialCatalog.Needles.Index;
        public static int redEarth = MaterialCatalog.RedEarth.Index;
        public static int litter = MaterialCatalog.Litter.Index;
        public static int roofTiles = MaterialCatalog.RoofTiles.Index;

        public List<MaterialMapsTextures> preparedTextures = new List<MaterialMapsTextures>();

        /// <summary>
        /// How much water each material takes in, 0..1, by its place in the material arrays (Weather/Wetness.glsl), from <see cref="MaterialSpec.Porosity"/>. A layer
        /// that isn't a material behaves like packed earth.
        /// </summary>
        public readonly float[] Porosity = new float[Rendering.Renderers.Simulation.WeatherRenderer.MaxMaterials];

        private TextureStudio textureImposterStudio;

        /// <summary>Whether to measure each kind of noise once the materials are made, and print it (--texturecheck): what Canvas.OctaveStatistics holds.</summary>
        public static bool MeasureNoise;

        /// <summary>A folder to write every material's maps to as pictures, once they're made (--materialsheet), or null.</summary>
        public static string? SheetFolder;

        /// <summary>How long the materials took to make, for the log and --perf.</summary>
        public TimeSpan MaterialTime { get; private set; }

        public TextureGenerator()
        {
            textureImposterStudio = new TextureStudio(TextureGenerator.IMPOSTER_RESOLUTION);
        }

        public void AddImposterToModel(glModel model, float distance)
        {

            ImposterData imposterData = new ImposterData(loadedImposterTextures/anglesPerImposter, distance, model.box);
            model.Imposter = imposterData;
            for (int i = 0; i < anglesPerImposter; i++)
            {
                preparedTextures.Add(textureImposterStudio.GenerateTextureFromModel(model, fullStretch: true, rotY: i * (MathF.Tau / anglesPerImposter)));
            }
            addAllPreparedTexturesToTexArray(arrayType.imposter);
        }

        public void GenerateAllTextures()
        {
            // Cards are painted on other cores while this one makes the materials.
            Task<BakedCards> cards = Task.Run(CardLibrary.Bake);

            MakeMaterials();
            SetPorosity();
            BindGearMaterials();
            BindFenceMaterials();
            BindFloraMaterials();
            Modelling.Procedural.Rock.RockMaterials.Stone = cliff;

            // Every number is known before the cards are done, which can take a second or two yet, and that's all a model needs to be begun on another thread.
            // Nothing draws a card until there's a world, so the rest of the engine is made while they're painted, and they're copied up when they're done (FinishCards).
            FloraMaterials.FirstCard = loadedMaterialTextures;
            Modelling.Procedural.MaterialNumbers.Settle();
            _cards = cards;
        }

        // Every material in the catalogue, straight into its layer: nothing is made twice or held while the rest are.
        private void MakeMaterials()
        {
            var clock = Stopwatch.StartNew();
            int size = MATERIAL_RESOLUTION.X;
            int levels = MaterialBaker.LevelsFor(size);
            int count = MaterialCatalog.Count;
            megaAlbedoTextureArray = NewMaterialArray((SizedInternalFormat)0x8C43, size, levels, count);    // GL_SRGB8_ALPHA8
            megaNormalTextureArray = NewMaterialArray(SizedInternalFormat.Rgba8, size, levels, count);
            megaMaterialTextureArray = NewMaterialArray(SizedInternalFormat.Rgba8, size, levels, count);
            SetAnisotropy(Anisotropy);
            var arrays = new MaterialArrays(megaAlbedoTextureArray, megaNormalTextureArray, megaMaterialTextureArray, size, levels);

            using (var gpu = new Synthesizer())
            {
                foreach (MaterialSpec spec in MaterialCatalog.All)
                {
                    var canvas = new Canvas(gpu, spec, size);
                    Surface surface = MaterialRecipes.Make(canvas);
                    MaterialBaker.Bake(gpu, canvas, surface, arrays, spec.Index);
                }
                if (MeasureNoise) Console.WriteLine(Canvas.MeasureNoise(gpu, MaterialCatalog.Flat, size));
                GL.Finish();
                Console.WriteLine($"Made {count} materials in {clock.ElapsedMilliseconds} ms, at most {gpu.Pool.Made} images at once.");
            }
            loadedMaterialTextures = count;
            MaterialTime = clock.Elapsed;
            Engine.CheckGLError("After making the materials");
            if (SheetFolder != null) MaterialSheet.Write(SheetFolder, megaAlbedoTextureArray, megaNormalTextureArray, megaMaterialTextureArray, size, count);
        }

        private int NewMaterialArray(SizedInternalFormat format, int size, int levels, int layers)
        {
            int array = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, array);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, levels, format, size, size, layers);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            // Walls and the ground far off are seen at a slant, where a square footprint picks a mip for the long side and blurs the short one away.
            GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)0x84FE, Math.Clamp(Anisotropy, 1f, MaxAnisotropy));
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return array;
        }

        /// <summary>The most anisotropic filtering the card allows, up to 16.</summary>
        public static float MaxAnisotropy => MathF.Min(GL.GetFloat((GetPName)0x84FF), 16f);

        /// <summary>How far the material arrays filter along a slant (GraphicsPresets): what they're made with, until the quality says otherwise.</summary>
        public float Anisotropy { get; private set; } = 4f;

        /// <summary>How far the material arrays filter along a slant, from 1 (not at all) to <see cref="MaxAnisotropy"/>.</summary>
        public void SetAnisotropy(float samples)
        {
            Anisotropy = samples;
            if (megaAlbedoTextureArray == 0) return;
            float value = Math.Clamp(samples, 1f, MaxAnisotropy);
            // Colour is what a slant blurs that anyone would see; the normal's slopes far less, and roughness, metal and height read square are as good and cost
            // nothing extra.
            foreach ((int array, float most) in new[] { (megaAlbedoTextureArray, value), (megaNormalTextureArray, MathF.Min(value, 2f)), (megaMaterialTextureArray, 1f) })
            {
                GL.BindTexture(TextureTarget.Texture2DArray, array);
                GL.TexParameter(TextureTarget.Texture2DArray, (TextureParameterName)0x84FE, most);
            }
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
        }

        private Task<BakedCards>? _cards;

        /// <summary>
        /// Copies the cards up once they're painted, which they may not be for a second or two after the materials: asked every frame before anything's drawn, it waits for them
        /// only if <paramref name="wait"/>, which it has to be from the first frame that can draw a model.
        /// </summary>
        public void FinishCards(bool wait)
        {
            if (_cards == null || !(wait || _cards.IsCompleted)) return;
            LoadCards(_cards.Result);
            _cards = null;
        }

        // Cards come painted, with their own mips (CardImage), so all that's left is to copy them up.
        private void LoadCards(BakedCards cards)
        {
            loadedModelTextures = cards.Layers;
            megaAlbedoModelTextureArray = NewCardArray(cards.Layers);
            megaNormalModelTextureArray = NewCardArray(cards.Layers);
            megaMaterialModelTextureArray = NewCardArray(cards.Layers);

            byte[] albedo = new byte[CardLibrary.LayerSize * CardLibrary.LayerSize * 4];
            byte[] normal = new byte[albedo.Length];
            byte[] material = new byte[albedo.Length];
            for (int layer = 0; layer < cards.Layers; layer++)
            {
                for (int level = 0; level < CardLibrary.Levels; level++)
                {
                    int size = CardLibrary.LevelSize(level);
                    cards.Level(layer, level, albedo, normal, material);
                    UploadCard(megaAlbedoModelTextureArray, layer, level, size, albedo);
                    UploadCard(megaNormalModelTextureArray, layer, level, size, normal);
                    UploadCard(megaMaterialModelTextureArray, layer, level, size, material);
                }
            }
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            Engine.CheckGLError("After uploading cards");
        }

        private static int NewCardArray(int layers)
        {
            int array = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, array);
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, CardLibrary.Levels, SizedInternalFormat.Rgba8, CardLibrary.LayerSize, CardLibrary.LayerSize, layers);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            return array;
        }

        private static void UploadCard(int array, int layer, int level, int size, byte[] pixels)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, array);
            GL.TexSubImage3D(TextureTarget.Texture2DArray, level, 0, 0, layer, size, size, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }

        // Generators can't read these from here, as anything that does has to be able to run with no GPU.
        private static void BindGearMaterials()
        {
            GearMaterials.Metal = plate;
            GearMaterials.Mail = mail;
            GearMaterials.Leather = leather;
            GearMaterials.Cloth = cloth;
            GearMaterials.Wood = wood;
            GearMaterials.Bone = bone;
            GearMaterials.Gem = gem;
            GearMaterials.Glow = flatGlow;
            GearMaterials.Skin = flat;
            GearMaterials.Straw = thatch;
        }

        private static void BindFenceMaterials()
        {
            Modelling.Procedural.Fence.FenceMaterials.Weathered = barkBare;
            Modelling.Procedural.Fence.FenceMaterials.Bark = barkPlated;
            Modelling.Procedural.Fence.FenceMaterials.Cane = barkSmooth;
            Modelling.Procedural.Fence.FenceMaterials.Stone = cliff;
            Modelling.Procedural.Fence.FenceMaterials.Masonry = stoneWall;
            Modelling.Procedural.Fence.FenceMaterials.Timber = wood;
            Modelling.Procedural.Fence.FenceMaterials.Plaster = plaster;
            Modelling.Procedural.Fence.FenceMaterials.Roof = shingle;
            Modelling.Procedural.Fence.FenceMaterials.Iron = iron;
            Modelling.Procedural.Fence.FenceMaterials.Cord = thatch;
        }

        private static void BindFloraMaterials()
        {
            FloraMaterials.Flesh = flat;
            FloraMaterials.Glow = flatGlow;
            FloraMaterials.SetBark(BarkKind.Furrowed, barkFurrowed);
            FloraMaterials.SetBark(BarkKind.Plated, barkPlated);
            FloraMaterials.SetBark(BarkKind.Papery, BirchBark);
            FloraMaterials.SetBark(BarkKind.Smooth, barkSmooth);
            FloraMaterials.SetBark(BarkKind.Fibrous, barkFibrous);
            FloraMaterials.SetBark(BarkKind.Ringed, barkRinged);
            FloraMaterials.SetBark(BarkKind.Cane, barkSmooth);
            FloraMaterials.SetBark(BarkKind.Bare, barkBare);
            FloraMaterials.SetBark(BarkKind.Charred, barkCharred);
            FloraMaterials.SetBark(BarkKind.Stem, foliage_white);
        }

        private void SetPorosity()
        {
            Array.Fill(Porosity, 0.5f);
            foreach (MaterialSpec spec in MaterialCatalog.All) Porosity[spec.Index] = spec.Porosity;
        }

        private int loadTypeOfTextureToArray(int type, int oldArray, arrayType arrayType)
        {
            int loadedTextures = loadedImposterTextures;
            Vector2i resolution = IMPOSTER_RESOLUTION;
            int textureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);

            int maxDimension = Math.Max(resolution.X, resolution.Y);
            int mips = (int)Math.Floor(Math.Log(maxDimension, 2)) + 1;
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, mips, SizedInternalFormat.Rgba8, resolution.X, resolution.Y, preparedTextures.Count+loadedTextures);

            if (loadedTextures > 0)
            {
                GL.CopyImageSubData(oldArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, resolution.X, resolution.Y, loadedTextures);
                GL.DeleteTexture(oldArray);
            }

            Engine.CheckGLError("After TexStorage3D");

            for (int i = 0; i < preparedTextures.Count; i++)
            {
                GL.CopyImageSubData(preparedTextures[i].textures[type], ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i+loadedTextures, resolution.X, resolution.Y, 1 );
                Engine.CheckGLError("After CopyTexSubImage3D " +i + " : " + loadedTextures);
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);

            Engine.CheckGLError("After TexParameter");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
            GL.BindTexture(TextureTarget.Texture2DArray, 0);
            return textureArray;
        }

        private void addAllPreparedTexturesToTexArray(arrayType arrayType)
        {
            megaAlbedoImposterTextureArray = loadTypeOfTextureToArray(0, megaAlbedoImposterTextureArray, arrayType);
            megaNormalImposterTextureArray = loadTypeOfTextureToArray(1, megaNormalImposterTextureArray, arrayType);
            megaMaterialImposterTextureArray = loadTypeOfTextureToArray(2, megaMaterialImposterTextureArray, arrayType);
            loadedImposterTextures += preparedTextures.Count;

            foreach (MaterialMapsTextures materialTextures in preparedTextures)
            {
                materialTextures.CleanUp();
            }
            preparedTextures.Clear();
        }

        public void CleanUp()
        {
            GL.DeleteTexture(megaAlbedoTextureArray);
            GL.DeleteTexture(megaMaterialTextureArray);
            GL.DeleteTexture(megaNormalTextureArray);

            GL.DeleteTexture(megaAlbedoModelTextureArray);
            GL.DeleteTexture(megaNormalModelTextureArray);
            GL.DeleteTexture(megaMaterialModelTextureArray);

            GL.DeleteTexture(megaAlbedoImposterTextureArray);
            GL.DeleteTexture(megaNormalImposterTextureArray);
            GL.DeleteTexture(megaMaterialImposterTextureArray);

            loadedModelTextures = 0;
            loadedMaterialTextures = 0;
            textureImposterStudio.CleanUp();
        }
    }
}
