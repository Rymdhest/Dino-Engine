using Dino_Engine.Core;
using Dino_Engine.Modelling;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Flora;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Rendering;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using static Dino_Engine.Modelling.Model.glModel;
using static Dino_Engine.Textures.MaterialLayersManipulator;
using static Dino_Engine.Textures.ProceduralTextureRenderer;

namespace Dino_Engine.Textures
{

    public enum arrayType     {
        material,
        imposter
    }

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

        public static readonly Vector2i MATERIAL_RESOLUTION = new Vector2i(512*4);
        public static readonly Vector2i IMPOSTER_RESOLUTION = new Vector2i(256);
        public readonly int anglesPerImposter = 8;

        public static int flat;
        public static int test;
        public static int grain;
        public static int sand;
        public static int grassy_soil;
        public static int soil;
        public static int mud;
        public static int moss;
        public static int clay;
        public static int flatGlow;
        public static int sandDunes;
        public static int metalFloor;
        public static int gravel;
        public static int foliage_white;
        public static int rock;
        /// <summary>Bare rock, for the mountains and every stone: grey, as all its colour comes from the world's <see cref="World.Geology"/>. See Rock.glsl.</summary>
        public static int cliff;
        public static int grass;
        public static int wood;
        public static int pineBark;
        public static int BirchBark;
        public static int barkFurrowed;
        public static int barkPlated;
        public static int barkSmooth;
        public static int barkFibrous;
        public static int barkRinged;
        public static int barkBare;
        public static int barkCharred;
        public static int brick;
        public static int bone;
        public static int leather;
        public static int cobble;
        public static int crackedDesert;
        public static int crackedLava; 
        public static int snow;
        public static int ice;
        public static int dirtPath;
        public static int farmland;
        public static int thatch;
        public static int shingle;
        public static int slate;
        public static int plaster;
        public static int stoneWall;
        public static int cloth;
        public static int plate;
        public static int mail;
        public static int gem;
        public static int iron;
        public static int mirror;
        public static int wax;


        public static  ProceduralTextureRenderer procTextGen = new ProceduralTextureRenderer();

        private ShaderProgram _textureNormalShader = new ShaderProgram("Simple.vert", "procedural/Texture_Normal_Generate.frag");
        private ShaderProgram _textureMaterialPackShader = new ShaderProgram("Simple.vert", "procedural/Texture_Material_Pack.frag");

        public List<MaterialMapsTextures> preparedTextures = new List<MaterialMapsTextures>();

        /// <summary>
        /// How much water each material takes in, 0..1, by its place in the material arrays (Weather/Wetness.glsl). Sand, cloth and bare earth drink it up and go dark. Stone and tile take a
        /// little, and shine. Metal, glass and ice take none and only carry a film. A material nobody has said anything about behaves like packed earth.
        /// </summary>
        public readonly float[] Porosity = new float[Rendering.Renderers.Simulation.WeatherRenderer.MaxMaterials];

        public static MaterialLayersManipulator MaterialLayersCombiner = new MaterialLayersManipulator();

        public static MaterialLayerHandler MaterialLayerHandler = new MaterialLayerHandler();

        private TextureStudio textureImposterStudio;

        public TextureGenerator()
        {
            _textureNormalShader.bind();
            _textureNormalShader.loadUniformInt("heightMap", 0);
            _textureNormalShader.loadUniformInt("materialMap", 1);
            _textureNormalShader.unBind();

            _textureMaterialPackShader.bind();
            _textureMaterialPackShader.loadUniformInt("albedoRead", 0);
            _textureMaterialPackShader.loadUniformInt("materialRead", 1);
            _textureMaterialPackShader.loadUniformInt("heightRead", 2);
            _textureMaterialPackShader.unBind();

            MaterialLayer.procTextGen = procTextGen;
            MaterialLayer.MaterialLayersCombiner = MaterialLayersCombiner;

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

            grain = createGrainTexture();
            foliage_white = createGrassTexture(Material.FOLIAGE_WHITE);
            grass = createGrassTexture(Material.FOLIAGE_GREEN);
            flat = createFlatTexture();
            flatGlow = createFlatGlowTexture();
            sandDunes = createSandDunesTexture();
            sand = createSandTexture();

            cobble = createCobbleTexture();
            pineBark = createPineBark();
            metalFloor = createMetalFloorTexture();
            brick = brickTexture();
            crackedLava = createCrackedLAva();
            crackedDesert = createCrackedDesert();
            rock = createRock();
            cliff = createCliff();
            Modelling.Procedural.Rock.RockMaterials.Stone = cliff;
            bone = createBoneTexture();
            mirror = createMirrorTexture();
            grassy_soil = createGrassySoilTexture();
            soil = createSoilTexture();
            wax = createWaxTexture();
            ice = createIceTexture();
            BirchBark = createBirchBark();
            barkFurrowed = createFurrowedBark();
            barkPlated = createPlatedBark();
            barkSmooth = createSmoothBark();
            barkFibrous = createFibrousBark();
            barkRinged = createRingedBark();
            barkBare = createBareWood();
            barkCharred = createCharredBark();
            iron = createIron();
            snow = createSnowTexture();
            mud = createMudTexture();
            moss = createMossTexture();
            gravel = createGravelTexture();
            dirtPath = createDirtPathTexture();
            farmland = createFarmlandTexture();
            wood = createWoodTexture();
            thatch = createThatchTexture();
            shingle = createShingleTexture();
            slate = createSlateTexture();
            plaster = createPlasterTexture();
            stoneWall = createStoneWallTexture();
            cloth = createClothTexture();
            leather = createLeatherTexture();
            plate = createPlateTexture();
            mail = createMailTexture();
            gem = createGemTexture();
            SetPorosity();
            BindGearMaterials();
            BindFenceMaterials();

            BindFloraMaterials();

            addAllPreparedTexturesToTexArray(arrayType.material);

            // Every number is known before the cards are done, which can take a second or two yet, and that's all a model needs to be begun on another thread.
            // Nothing draws a card until there's a world, so the rest of the engine is made while they're painted, and they're copied up when they're done (FinishCards).
            FloraMaterials.FirstCard = loadedMaterialTextures;
            Modelling.Procedural.MaterialNumbers.Settle();
            _cards = cards;
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

        // White, as what colour gear is goes in its vertices: one metal is every metal.
        private int createPlateTexture()
        {
            var metal = procTextGen.PerlinFBM(new Vector2(14f, 14f), octaves: 4, amplitudePerOctave: 0.5f);
            metal.setMaterial(new Material(new Colour(255, 255, 255), 0.36f, 0f, 0.8f));
            metal.scaleHeight(0.2f);
            metal.addHeight(0.7f);
            return FinishTexture(metal, normalFlatness: 120f);
        }

        private int createMailTexture()
        {
            var links = procTextGen.TileWeave(new Vector2(72f, 72f), count: 2, smoothness: 0.45f, width: 0.72f);
            links.setMaterial(new Material(new Colour(255, 255, 255), 0.44f, 0f, 0.8f));
            links.scaleHeight(0.5f);
            links.addHeight(0.4f);
            return FinishTexture(links, normalFlatness: 50f);
        }

        private int createGemTexture()
        {
            return FinishTexture(procTextGen.CreateMaterial(new Material(new Colour(255, 255, 255), 0.08f, 0f, 0.2f, 0.5f)));
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
            foreach ((int material, float porosity) in new[]
            {
                (sand, 0.9f), (sandDunes, 0.85f), (soil, 0.8f), (farmland, 0.85f), (dirtPath, 0.75f), (grassy_soil, 0.7f), (moss, 0.7f), (grain, 0.7f),
                (crackedDesert, 0.85f), (crackedLava, 0.6f), (mud, 0.4f), (gravel, 0.5f), (grass, 0.35f),
                (rock, 0.35f), (cliff, 0.35f), (cobble, 0.42f), (stoneWall, 0.45f), (brick, 0.7f), (plaster, 0.75f), (slate, 0.15f), (shingle, 0.5f), (thatch, 0.8f),
                (wood, 0.65f), (pineBark, 0.7f), (BirchBark, 0.6f),
                (barkFurrowed, 0.7f), (barkPlated, 0.7f), (barkSmooth, 0.45f), (barkFibrous, 0.75f), (barkRinged, 0.6f), (barkBare, 0.6f), (barkCharred, 0.5f), (cloth, 0.9f), (leather, 0.45f), (bone, 0.3f), (wax, 0.04f),
                (foliage_white, 0.12f),
                (ice, 0f), (snow, 0f), (iron, 0f), (plate, 0f), (mail, 0.05f), (gem, 0f), (metalFloor, 0f), (mirror, 0f), (flat, 0.3f), (flatGlow, 0f),
            })
            {
                Porosity[material] = porosity;
            }
        }

        private FrameBuffer generateNormalFrameBuffer(FrameBuffer materialBuffer, float normalFlatness)
        {
            DrawBufferSettings normalAttachment = new DrawBufferSettings(FramebufferAttachment.ColorAttachment0);
            normalAttachment.formatInternal = PixelInternalFormat.Rgba8;
            normalAttachment.pixelType = PixelType.UnsignedByte;
            normalAttachment.wrapMode = TextureWrapMode.Repeat;     
            normalAttachment.formatExternal = PixelFormat.Rgba;
            FrameBufferSettings normalBufferSettings = new FrameBufferSettings(materialBuffer.getResolution());
            normalBufferSettings.drawBuffers.Add(normalAttachment);
            FrameBuffer normalBuffer = new FrameBuffer(normalBufferSettings);

            _textureNormalShader.bind();
            normalBuffer.bind(); 
            _textureNormalShader.loadUniformFloat("normalFlatness", normalFlatness * (1f/materialBuffer.getResolution().X));
            _textureNormalShader.loadUniformVector2f("texelSize", new Vector2(1f)/materialBuffer.getResolution());

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, materialBuffer.GetAttachment(2));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, materialBuffer.GetAttachment(1));
            Engine.RenderEngine.ScreenQuadRenderer.Render();

            return normalBuffer;
        }


        private void PackMaterialLayer(MaterialLayer layer)
        {
            _textureMaterialPackShader.bind();
            _textureMaterialPackShader.loadUniformFloat("alphaCutoff", 0.01f);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, layer.GetLastFrameBuffer().GetAttachment(0));
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, layer.GetLastFrameBuffer().GetAttachment(1));
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, layer.GetLastFrameBuffer().GetAttachment(2));
            layer.tap();
        }

        private int FinishTexture(MaterialLayer layer, float normalFlatness = 300.0f, float parallaxDepth = 0.06f)
        {
            FrameBuffer normalBuffer = generateNormalFrameBuffer(layer.GetLastFrameBuffer(), normalFlatness);
            PackMaterialLayer(layer);

            int albedoTexture = layer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);
            int materialTexture = layer.GetLastFrameBuffer().exportAttachmentAsTexture(ReadBufferMode.ColorAttachment1);
            int normalTexture = normalBuffer.exportAttachmentAsTexture(ReadBufferMode.ColorAttachment0);

            normalBuffer.cleanUp();

            preparedTextures.Add(new MaterialMapsTextures(albedoTexture, normalTexture, materialTexture));

            MaterialLayerHandler.cleanUp();

            return preparedTextures.Count-1;
        }

        private int loadTypeOfTextureToArray(int type, int oldArray, arrayType arrayType)
        {
            int loadedTextures;
            Vector2i resolution;

            if (arrayType == arrayType.material)
            {
                loadedTextures = loadedMaterialTextures;
                resolution = MATERIAL_RESOLUTION;
            }
            else
            {
                loadedTextures = loadedImposterTextures;
                resolution = IMPOSTER_RESOLUTION;
            }
            int textureArray = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, textureArray);

            // Allocate storage for the texture array with the correct number of slices

            int maxDimension = Math.Max(resolution.X, resolution.Y);
            int mips = (int)Math.Floor(Math.Log(maxDimension, 2)) + 1;
            //if (type == 2) mips = 1;
            //if (type == 1) mips = 1;
            //if (type == 0) mips = 1;
            GL.TexStorage3D(TextureTarget3d.Texture2DArray, mips, SizedInternalFormat.Rgba8, resolution.X, resolution.Y, preparedTextures.Count+loadedTextures);

            if (loadedTextures > 0)
            {
                GL.CopyImageSubData(oldArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, 0, resolution.X, resolution.Y, loadedTextures);
                GL.DeleteTexture(oldArray);
            }

            Engine.CheckGLError("After TexStorage3D");

            for (int i = 0; i < preparedTextures.Count; i++)
            {
                //GL.BindTexture(TextureTarget.Texture2D, preparedTextures[i].textures[type]);
                Engine.CheckGLError("After BindTexture2D");
                //GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, 0, 0, textureResolution.X, textureResolution.Y);
                GL.CopyImageSubData(preparedTextures[i].textures[type], ImageTarget.Texture2D, 0, 0, 0, 0, textureArray, ImageTarget.Texture2DArray, 0, 0, 0, i+loadedTextures, resolution.X, resolution.Y, 1 );
                Engine.CheckGLError("After CopyTexSubImage3D " +i + " : " + loadedTextures);
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            if (arrayType == arrayType.material)
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            } else
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
            }

            Engine.CheckGLError("After TexParameter");
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);

            // Unbind the texture array
            GL.BindTexture(TextureTarget.Texture2DArray, 0);

            return textureArray;
        }


        private void addAllPreparedTexturesToTexArray(arrayType arrayType)
        {
            if (arrayType == arrayType.material)
            {
                megaAlbedoTextureArray = loadTypeOfTextureToArray(0, megaAlbedoTextureArray, arrayType);
                megaNormalTextureArray = loadTypeOfTextureToArray(1, megaNormalTextureArray, arrayType);
                megaMaterialTextureArray = loadTypeOfTextureToArray(2, megaMaterialTextureArray, arrayType);

                loadedMaterialTextures += preparedTextures.Count;
            }
            else if (arrayType == arrayType.imposter)
            {
                megaAlbedoImposterTextureArray = loadTypeOfTextureToArray(0, megaAlbedoImposterTextureArray, arrayType);
                megaNormalImposterTextureArray = loadTypeOfTextureToArray(1, megaNormalImposterTextureArray, arrayType);
                megaMaterialImposterTextureArray = loadTypeOfTextureToArray(2, megaMaterialImposterTextureArray, arrayType);

                loadedImposterTextures += preparedTextures.Count;
            }



            foreach (MaterialMapsTextures materialTextures in preparedTextures)
            {
                materialTextures.CleanUp();
            }
            preparedTextures.Clear();
            MaterialLayerHandler.cleanUp();
        }

        private int createFlatGlowTexture()
        {
            return FinishTexture(procTextGen.CreateMaterial(new Material(new Colour(255, 255, 255), 1f, 0.5f, 0f)));
        }
        private int createMirrorTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(1f, 1f), octaves: 1, amplitudePerOctave: 0.8f);
            roughLayer.scaleHeight(0.0f);
            roughLayer.setMaterial(new Material(new Colour(255, 255, 255), 0.4f, 0f, 0.6f));
            return FinishTexture(roughLayer);
        }

        private int createFlatTexture()
        {
            return FinishTexture(procTextGen.CreateMaterial(new Material(new Colour(255, 255, 255), 0.5f, 0.0f, 0.0f)));
        }
        private int createCrackedDesert()
        {
            var voronoiCracks = procTextGen.VoronoiCracks(new Vector2(8f, 8f),width: 0.01f, smoothness:0.5f, jitter: 1.0f, seed:100f);
            var voronoi = procTextGen.Voronoi(new Vector2(8f, 8f),jitter: 1.0f, seed: 100f);
            voronoiCracks.setMaterial(Material.SAND);
            var noise = procTextGen.PerlinFBM(new Vector2(64f, 64f), octaves: 10, amplitudePerOctave: 0.6f);

            MaterialLayersCombiner.combine(voronoi, procTextGen.CreateFlatHeight(2.0f), FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Power, weight: 2.0f);
            MaterialLayersCombiner.combine(voronoiCracks, voronoi.scaleHeight(0.5f), FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Subtract, weight: 0.5f);

            MaterialLayersCombiner.combine(voronoiCracks, noise, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Subtract, weight: 0.2f);

            return FinishTexture(voronoiCracks, normalFlatness: 40);
        }
        private int createBoneTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            roughLayer.setMaterial(Material.BONE);
            roughLayer.scaleHeight(0.2f);
            roughLayer.addHeight(0.8f);
            return FinishTexture(roughLayer);
        }
        private int createRock()
        {
            var levelsBig = procTextGen.Voronoi(new Vector2(128f, 128f), jitter: 1.0f, returnMode:ReturnMode.ID);
            levelsBig.setMaterial(Material.GROUND_ROCK);
            var levelsSmall = procTextGen.Voronoi(new Vector2(24f, 24f), jitter: 1.0f, returnMode: ReturnMode.ID);
            var combined = MaterialLayersCombiner.combine(levelsBig, levelsSmall, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation:Operation.Add, weight:0.7f);

            var cellular = procTextGen.Cellular(new Vector2(117f, 117f), jitter:1.0f, metric:Metric.SquaredEuclidean).invertHeight();
            Material material2 = Material.GROUND_ROCK;
            material2.Colour.Intensity = 0.85f;
            cellular.setMaterial(material2);

            var combined2 = MaterialLayersCombiner.combine(combined, cellular, FilterMode.Greater, materialOperation: Operation.Override, heightOperation: Operation.Override, weight: 0.2f);

            var cracks = procTextGen.VoronoiCracks(new Vector2(19f, 19f), jitter: 1.0f, width: 0.055f, smoothness: 0.5f).setMaterial(new Material(new Colour(120, 117, 116), 0.45f, 0f, 0f));

            MaterialLayersCombiner.combine(combined2, cracks, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Scale, weight: 0.2f);

            var noise = procTextGen.PerlinFBM(new Vector2(64f, 64f), octaves: 10, amplitudePerOctave: 0.6f);

            MaterialLayersCombiner.combine(combined2, noise, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Add, weight: 0.7f);
            combined2.scaleHeight(0.15f);
            combined2.addHeight(0.85f);
            return FinishTexture(combined2, normalFlatness:50.0f);
        }

        // What rock is made of at the scale of a hand and a stride: blocks with flat faces, the cracks between them, and a grain over all of it. It's read at two sizes
        // at once, and from a long way up, so it's the large strong shapes that count. The grey is as bright as rock gets, as the world's layers multiply it.
        private int createCliff()
        {
            Material stone = new Material(new Colour(232, 232, 232), 0.86f);

            // Wider than they're tall: seen from the side a rock face is laid down in flakes.
            var faces = procTextGen.Facets(new Vector2(3f, 6f), tilt: 0.42f, relief: 0.5f, crack: 0.28f, width: 0.022f).setMaterial(stone);
            var smaller = procTextGen.Facets(new Vector2(8f, 13f), tilt: 0.5f, relief: 0.55f, crack: 0.3f, width: 0.04f);
            var chips = procTextGen.Facets(new Vector2(21f, 31f), tilt: 0.6f, relief: 0.5f, crack: 0.24f, width: 0.07f);
            MaterialLayersCombiner.combine(faces, smaller, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Add, weight: 0.42f);
            MaterialLayersCombiner.combine(faces, chips, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Add, weight: 0.3f);

            var grain = procTextGen.PerlinFBM(new Vector2(29f, 29f), octaves: 7, amplitudePerOctave: 0.58f);
            MaterialLayersCombiner.combine(faces, grain, FilterMode.Everywhere, materialOperation: Operation.Nothing, heightOperation: Operation.Add, weight: 0.16f);
            return FinishTexture(faces, normalFlatness: 7.0f);
        }

        private int createCobbleTexture()
        {
            float stoneSize = 8;
            float seed = MyMath.rand.NextSingle() * 100000.0f;
            var stones = procTextGen.VoronoiCracks(new Vector2(stoneSize, stoneSize), jitter: 1.0f, width:0.15f, smoothness:0.5f, seed: seed).setMaterial(Material.GROUND_ROCK);
            var stonesID = procTextGen.VoronoiCracks(new Vector2(stoneSize, stoneSize), jitter: 1.0f, width: 0.15f, smoothness: 0.5f, returnMode:ReturnMode.ID, seed: seed).setMaterial(Material.GROUND_SOIL);
            var background = procTextGen.PerlinFBM(new Vector2(64f, 64f), octaves: 10, amplitudePerOctave: 0.5f, rigged: false).setMaterial(Material.GROUND_SOIL);
            var roughLayer = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 10, amplitudePerOctave: 0.5f, rigged: false).setMaterial(Material.GROUND_ROCK);
            var stoneTop = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves:1, rigged:true).setMaterial(new Material(new Colour(114, 103, 76), 0.95f, 0f, 0f));

            var scratch = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 1, amplitudePerOctave: 0.5f, rigged: true).setMaterial(Material.FOLIAGE_OLIVE).invertHeight();

            //var dirtMask = procTextGen.PerlinFBM(new Vector2(20f, 20f), octaves: 1, rigged: true).setMaterial(Material.FOLIAGE_OLIVE).addHeight(0.5f);
            //MaterialLayersCombiner.combine(dirtMask, background, FilterMode.Greater, heightOperation: Operation.Nothing, materialOperation: Operation.Override, weight: -0.5f, smoothness: 0.1f);


            //MaterialLayersCombiner.combine(stones, procTextGen.CreateFlatHeight(.25f), FilterMode.Everywhere, heightOperation: Operation.Power, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 1.0f);

            MaterialLayersCombiner.combine(stones, roughLayer, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.3f, smoothness: 0.5f);

            //MaterialLayersCombiner.combine(stonesID, stonesID, FilterMode.Everywhere, heightOperation: Operation.Hash, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(stones, stonesID, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.3f, smoothness: 0.5f);
            stonesID.scaleHeight(0.8f);
            stonesID.addHeight(0.0f);
            MaterialLayersCombiner.combine(stones, stonesID, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Mix, weight: 0.5f, smoothness: 0.5f);
            //MaterialLayersCombiner.combine(stones, scratch, FilterMode.Everywhere, heightOperation: Operation.Subtract, materialOperation: Operation.Nothing, weight: 0.05f, smoothness: 1.0f);


            MaterialLayersCombiner.combine(stones, background.scaleHeight(0.5f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.9f, smoothness: 0.1f);

            return FinishTexture(stones, normalFlatness:30.0f);
            //MaterialLayersCombiner.combine(stones, dirtMask, FilterMode.Greater, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(stones, stoneTop, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Scale, weight: 0.15f, smoothness: 0.1f);

            background.setMaterial(new Material(new Colour(163, 152, 138), 0.98f, 0f, 0f));
            MaterialLayersCombiner.combine(background, procTextGen.CreateFlatHeight(0.3f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: -0.2f);
            MaterialLayersCombiner.combine(stones, background, FilterMode.Greater, heightOperation: Operation.Smoothstep, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.1f);
            return FinishTexture(stones);
        }
        private int createCrackedLAva()
        {
            var lava = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves: 5, amplitudePerOctave: 0.9f);
            lava.setMaterial(new Material(new Colour(0, 0, 0), 0.95f, 0f, 0f));
            lava.mix(procTextGen.CreateMaterial(new Material(new Colour(255, 45, 23), 1.0f, 0.1f, 0f), height: 1.0f), FilterMode.Everywhere, Operation.Mix);

            var cracks = procTextGen.VoronoiCracks(new Vector2(8f, 8f), width: 0.15f, smoothness: 0.8f, jitter: 1f);

            var cracks2 = procTextGen.VoronoiCracks(new Vector2(16f, 16f), width: 0.08f, smoothness: 0.5f, jitter: 1f);
            cracks2.mix(procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 4, amplitudePerOctave: 0.7f), FilterMode.Greater, Operation.Override, weight: 0.5f);
            cracks2.setMaterial(Material.Charcoal);

            cracks.setMaterial(Material.Charcoal);
            var noise = procTextGen.PerlinFBM(new Vector2(32f, 32f), octaves: 4, amplitudePerOctave: 0.6f, rigged:false);
            //noise.invertHeight();
            cracks.mix(cracks2, FilterMode.Everywhere, Operation.Scale,  weight:0.35f);

            MaterialLayersCombiner.combine(cracks, noise.scaleHeight(1.0f), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.4f);


            var crackedLava = MaterialLayersCombiner.combine(cracks, lava.scaleHeight(0.14f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.5f);

            return FinishTexture(crackedLava, normalFlatness: 50.0f);
        }
        private int createBirchBark()
        {
            var bark = procTextGen.PerlinFBM(new Vector2(2f, 4f), octaves: 10, amplitudePerOctave: 0.6f);
            var noise = procTextGen.PerlinFBM(new Vector2(8f, 4f), octaves: 10, amplitudePerOctave: 0.5f);
            var barkCracks = procTextGen.VoronoiCracks(new Vector2(4f, 2f), width: 0.005f, smoothness: 0.1f, jitter: 0.6f);
            var wavy = procTextGen.PerlinFBM(new Vector2(8,32), octaves: 3, amplitudePerOctave: 0.6f, rigged: false);
            bark.setMaterial(new Material(new Colour(240, 260, 225), 0.95f, 0.0f, 0f));
            noise.setMaterial(new Material(new Colour(10, 7, 2), 0.55f, 0f, 0f));
            barkCracks.setMaterial(new Material(new Colour(130, 125, 125), 0.95f, 0f, 0f));
            wavy.setMaterial(new Material(new Colour(22, 26, 20), 0.35f, 0f, 0f));

            MaterialLayersCombiner.combine(barkCracks, noise.scaleHeight(0.9f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: -0.1f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bark, barkCracks, FilterMode.Lesser, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.1f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(bark, wavy.scaleHeight(0.65f), FilterMode.Greater, heightOperation: Operation.Scale, materialOperation: Operation.Override, weight: 0.1f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(bark, noise.scaleHeight(1.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.4f, smoothness: 0.8f);

            bark.addHeight(-0.05f);
            bark.scaleHeight(4.0f);
            return FinishTexture(bark, normalFlatness: 20.0f);
        }

        private int createPineBark()
        {
            var bark = procTextGen.PerlinFBM(new Vector2(30f, 5f), octaves: 10, amplitudePerOctave: 0.6f);
            var noise = procTextGen.PerlinFBM(new Vector2(8f, 4f), octaves: 10, amplitudePerOctave: 0.5f);
            var barkCracks = procTextGen.VoronoiCracks(new Vector2(16f, 8f), width: 0.055f, smoothness: 0.1f, jitter: 0.6f);
            var wavy = procTextGen.PerlinFBM(new Vector2(2, 16), octaves: 3, amplitudePerOctave: 0.6f, rigged: true);
            bark.setMaterial(Material.BARK_PINE);
            noise.setMaterial(new Material(new Colour(21, 17, 17), 0.55f, 0f, 0f));
            barkCracks.setMaterial(new Material(new Colour(25, 25, 25), 0.95f, 0f, 0f));
            wavy.setMaterial(Material.FOLIAGE_OLIVE);

            MaterialLayersCombiner.combine(barkCracks, noise.scaleHeight(0.9f), FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: -0.1f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bark, barkCracks, FilterMode.Lesser, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.1f, smoothness: 0.9f);

            MaterialLayersCombiner.combine(bark, wavy.invertHeight(), FilterMode.Greater, heightOperation: Operation.Scale, materialOperation: Operation.Override, weight: 0.7f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(bark, noise.scaleHeight(1.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.4f, smoothness: 0.8f);

            bark.addHeight(-0.05f);
            bark.scaleHeight(4.0f);
            return FinishTexture(bark, normalFlatness: 20.0f);
        }
        private int createIron()
        {
            var metal = procTextGen.PerlinFBM(new Vector2(30f, 5f), octaves: 5, amplitudePerOctave: 0.6f);
            metal.setMaterial(Material.IRON);

            metal.scaleHeight(0.3f);
            metal.addHeight(0.7f);
            return FinishTexture(metal, normalFlatness: 50.0f);
        }
        // What every bark comes to: a surface, what's cut into it, and a finer grain over both.
        private int FinishBark(MaterialLayer surface, MaterialLayer cuts, MaterialLayer grain, float grainWeight = 0.3f, float normalFlatness = 22f)
        {
            MaterialLayersCombiner.combine(surface, cuts, FilterMode.Lesser, heightOperation: Operation.Override, materialOperation: Operation.Override);
            MaterialLayersCombiner.combine(surface, grain, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: grainWeight, smoothness: 0.8f);
            surface.addHeight(-0.05f);
            surface.scaleHeight(3f);
            return FinishTexture(surface, normalFlatness: normalFlatness);
        }

        private static Material Pale(int value, float roughness = 0.93f) => new Material(new Colour(value, value - 4, value - 9), roughness, 0f, 0f);

        // Darkens a surface by how far down it is, as what's in a crack gets no light. The flat layer stands at height 1, so it's the distance from the top that counts.
        private MaterialLayer DarkenDepths(MaterialLayer surface, int darkest, float reach)
        {
            MaterialLayer shadow = procTextGen.CreateMaterial(Pale(darkest, 0.98f), 1f);
            return MaterialLayersCombiner.combine(surface, shadow, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, smoothness: reach);
        }

        // Ridges running up the trunk that meet and part, with deep fissures between them: oak, elm, old maple.
        private int createFurrowedBark()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(11f, 2f), octaves: 8, amplitudePerOctave: 0.58f, rigged: true);
            var grain = procTextGen.PerlinFBM(new Vector2(52f, 6f), octaves: 6, amplitudePerOctave: 0.6f, rigged: true);
            surface.setMaterial(Pale(186));
            grain.setMaterial(Pale(150));
            MaterialLayersCombiner.combine(surface, grain, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Smoothstep, weight: 0.28f, smoothness: 0.8f);
            DarkenDepths(surface, 26, 0.72f);
            surface.scaleHeight(2.6f);
            return FinishTexture(surface, normalFlatness: 16f);
        }

        // Broad plates in overlapping scales, with soft dark seams: pine, spruce.
        private int createPlatedBark()
        {
            var surface = procTextGen.Cellular(new Vector2(9f, 6f), jitter: 0.8f);
            var flake = procTextGen.PerlinFBM(new Vector2(24f, 30f), octaves: 7, amplitudePerOctave: 0.62f, rigged: true);
            surface.setMaterial(Pale(190));
            flake.setMaterial(Pale(144));
            surface.invertHeight();
            MaterialLayersCombiner.combine(surface, flake, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Smoothstep, weight: 0.3f, smoothness: 0.8f);
            DarkenDepths(surface, 30, 0.8f);
            surface.scaleHeight(2.4f);
            return FinishTexture(surface, normalFlatness: 18f);
        }

        // Tight and fine, with lenticels lying across it: cherry, beech, the young of most things.
        private int createSmoothBark()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(4f, 3f), octaves: 7, amplitudePerOctave: 0.5f);
            var cuts = procTextGen.VoronoiCracks(new Vector2(9f, 64f), width: 0.03f, smoothness: 0.08f, jitter: 0.9f);
            var grain = procTextGen.PerlinFBM(new Vector2(16f, 90f), octaves: 5, amplitudePerOctave: 0.5f);
            surface.setMaterial(Pale(176, 0.94f));
            cuts.setMaterial(Pale(70, 0.96f));
            grain.setMaterial(Pale(150, 0.94f));
            surface.scaleHeight(0.5f);
            surface.addHeight(0.45f);
            return FinishBark(surface, cuts, grain, 0.2f, 40f);
        }

        // Long soft strips, running the length of the tree: redwood, cedar, cypress.
        private int createFibrousBark()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(19f, 1f), octaves: 8, amplitudePerOctave: 0.62f, rigged: true);
            var grain = procTextGen.PerlinFBM(new Vector2(84f, 3f), octaves: 6, amplitudePerOctave: 0.6f);
            surface.setMaterial(Pale(196));
            grain.setMaterial(Pale(140));
            MaterialLayersCombiner.combine(surface, grain, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Smoothstep, weight: 0.34f, smoothness: 0.8f);
            DarkenDepths(surface, 36, 0.66f);
            surface.scaleHeight(2.4f);
            return FinishTexture(surface, normalFlatness: 18f);
        }

        // The scars of old leaves, in rings: palms.
        private int createRingedBark()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(5f, 3f), octaves: 7, amplitudePerOctave: 0.55f);
            var cuts = procTextGen.VoronoiCracks(new Vector2(3f, 14f), width: 0.09f, smoothness: 0.22f, jitter: 0.35f, warp: 0.12f, warpScale: 0.3f);
            var grain = procTextGen.PerlinFBM(new Vector2(40f, 8f), octaves: 6, amplitudePerOctave: 0.6f, rigged: true);
            surface.setMaterial(Pale(176));
            cuts.setMaterial(Pale(44, 0.97f));
            grain.setMaterial(Pale(140));
            return FinishBark(surface, cuts, grain, 0.3f);
        }

        // Wood with the bark long gone, silvered and split along the grain.
        private int createBareWood()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(10f, 1f), octaves: 8, amplitudePerOctave: 0.55f);
            var cuts = procTextGen.VoronoiCracks(new Vector2(30f, 1f), width: 0.05f, smoothness: 0.12f, jitter: 0.9f, warp: 0.1f, warpScale: 0.1f);
            var grain = procTextGen.PerlinFBM(new Vector2(90f, 2f), octaves: 6, amplitudePerOctave: 0.6f);
            surface.setMaterial(Pale(196, 0.85f));
            cuts.setMaterial(Pale(50, 0.95f));
            grain.setMaterial(Pale(150, 0.85f));
            return FinishBark(surface, cuts, grain, 0.32f, 30f);
        }

        // Burnt to black scales, with what's left of the fire in the seams.
        private int createCharredBark()
        {
            var surface = procTextGen.PerlinFBM(new Vector2(12f, 12f), octaves: 7, amplitudePerOctave: 0.55f);
            var cuts = procTextGen.VoronoiCracks(new Vector2(11f, 9f), width: 0.11f, smoothness: 0.1f, jitter: 0.8f);
            var grain = procTextGen.PerlinFBM(new Vector2(30f, 30f), octaves: 6, amplitudePerOctave: 0.6f, rigged: true);
            surface.setMaterial(new Material(new Colour(58, 54, 52), 0.75f, 0f, 0f));
            cuts.setMaterial(new Material(new Colour(10, 9, 9), 0.98f, 0f, 0f));
            grain.setMaterial(new Material(new Colour(30, 28, 27), 0.85f, 0f, 0f));
            return FinishBark(surface, cuts, grain);
        }

        private int brickTexture()
        {
            Vector2 brickResolution = new Vector2(4, 12)*1;
            float brickSpacing = 0.016f;
            float brickExtrudeSmoothness = 0.07f;
            Material brickColour1 = new Material(new Colour(198, 61, 34),0.95f, 0f, 0.0f);
            Material brickColour2 = new Material(new Colour(202, 46, 32),0.95f, 0f, 0.0f);
            Material cementColour = new Material(new Colour(163, 152, 138),0.95f, 0f, 0.0f);

            var bricks = procTextGen.Createbricks(brickResolution, smoothness: brickExtrudeSmoothness, spacing: brickSpacing, returnMode: ReturnMode.Height);
            var bricksID = procTextGen.Createbricks(brickResolution, smoothness: brickExtrudeSmoothness, spacing: brickSpacing, returnMode: ReturnMode.ID);
            bricks.setMaterial(brickColour1);
            var bricks2 = procTextGen.Createbricks(brickResolution, smoothness: brickExtrudeSmoothness, spacing: brickSpacing, returnMode: ReturnMode.Height);
            bricks2.setMaterial(brickColour2);

            var background = procTextGen.PerlinFBM(new Vector2(200f, 200f), octaves: 5, amplitudePerOctave: 0.5f, rigged: false);
            background.setMaterial(cementColour);
            background.scaleHeight(0.1f);
            background.addHeight(0.95f);
            //MaterialLayersCombiner.combine(background, procTextGen.CreateFlatHeight(0.0f), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: -0.2f);

            MaterialLayersCombiner.combine(bricks, procTextGen.PerlinFBM(new Vector2(50f, 50f), octaves: 4), FilterMode.Everywhere, heightOperation: Operation.Subtract, materialOperation: Operation.Nothing, weight: 0.2f, smoothness: 0.1f);
            //MaterialLayersCombiner.combine(bricks, bricksID, FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.2f, smoothness: 0.5f);

            MaterialLayersCombiner.combine(bricks2, bricksID, FilterMode.Everywhere, heightOperation: Operation.Override, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.5f);

            MaterialLayersCombiner.combine(bricks, bricks2, FilterMode.Greater, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.3f);


            MaterialLayersCombiner.combine(bricks, procTextGen.PerlinFBM(new Vector2(1000f, 1000f), octaves: 4), FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.02f, smoothness: 0.9f);
            MaterialLayersCombiner.combine(bricks, procTextGen.PerlinFBM(new Vector2(50f, 50f), octaves: 4), FilterMode.Lesser, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.1f, smoothness: 0.9f);

            var sine = procTextGen.Sine(new Vector2(0, brickResolution.Y *4));

            var groove = procTextGen.CreateFlatHeight(1.0f);

            MaterialLayersCombiner.combine(groove, sine, FilterMode.Everywhere, heightOperation: Operation.Subtract, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.1f);

            groove.scaleHeight(0.9f);
            groove.addHeight(0.1f);
            MaterialLayersCombiner.combine(groove, procTextGen.Createbricks(new Vector2(2f, brickResolution.Y), smoothness: brickExtrudeSmoothness, spacing: brickSpacing, returnMode: ReturnMode.Height).invertHeight(), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.1f);
            MaterialLayersCombiner.combine(background, groove, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.5f, smoothness: 0.1f);

            MaterialLayersCombiner.combine(bricks, background, FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.1f);



             return FinishTexture(bricks, normalFlatness: 90);
        }

        private int createMetalFloorTexture()
        {
            MaterialLayer tileWweaves = procTextGen.TileWeave(new Vector2(16f, 16f), count: 3, smoothness: 0.9f, width: 0.5f);
            return FinishTexture(tileWweaves);
        }
        private int createSoilTexture()
        {

            MaterialLayer grassLayer = procTextGen.PerlinFBM(new Vector2(64f, 64f), octaves: 8, amplitudePerOctave: 0.5f);
            grassLayer.setMaterial(Material.GROUND_SOIL);


            MaterialLayer rockLayer = procTextGen.Voronoi(new Vector2(200f, 200f), jitter: 1.0f);
            MaterialLayersCombiner.combine(rockLayer, procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 2, amplitudePerOctave: 0.5f), FilterMode.Everywhere, Operation.Nothing, Operation.Scale, weight: 0.2f);
            rockLayer.setMaterial(Material.GROUND_ROCK);
            rockLayer.scaleHeight(0.8f);
            MaterialLayer hilly = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves: 3, amplitudePerOctave: 0.5f, rigged: true);
            MaterialLayersCombiner.combine(grassLayer, hilly, FilterMode.Everywhere, Operation.Nothing, Operation.Mix, weight: 0.5f);
            grassLayer.scaleHeight(0.4f);
            grassLayer.addHeight(0.6f);
            return FinishTexture(grassLayer, normalFlatness: 100);
        }
        private int createGrassySoilTexture()
        {
            MaterialLayer soilLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            soilLayer.setMaterial(Material.FOLIAGE_PINE);

            MaterialLayer grassLayer = procTextGen.PerlinFBM(new Vector2(64f, 64f), octaves: 8, amplitudePerOctave: 0.5f);
            grassLayer.setMaterial(Material.GROUND_SOIL);


            MaterialLayer rockLayer = procTextGen.Voronoi(new Vector2(200f, 200f), jitter:1.0f);
            MaterialLayersCombiner.combine(rockLayer, procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 2, amplitudePerOctave: 0.5f), FilterMode.Everywhere, Operation.Nothing, Operation.Scale, weight: 0.2f);
            rockLayer.setMaterial(Material.GROUND_ROCK);
            rockLayer.scaleHeight(0.8f);
            MaterialLayer hilly = procTextGen.PerlinFBM(new Vector2(16f, 16f), octaves: 3, amplitudePerOctave: 0.5f, rigged:true);
            MaterialLayersCombiner.combine(grassLayer, hilly, FilterMode.Everywhere, Operation.Nothing, Operation.Mix, weight:0.5f);

            MaterialLayersCombiner.combine(soilLayer, rockLayer, FilterMode.Greater, Operation.Override, Operation.Override, weight: 0.5f);

            MaterialLayersCombiner.combine(grassLayer, soilLayer, FilterMode.Greater, Operation.Override, Operation.Override, weight: 0.5f);

            return FinishTexture(grassLayer, normalFlatness:100);
        }

        private int createSnowTexture()
        {
            var drifts = procTextGen.PerlinFBM(new Vector2(5f, 5f), octaves: 7, amplitudePerOctave: 0.5f);
            drifts.setMaterial(new Material(new Colour(226, 233, 242), 0.72f, 0f, 0f, 0.4f));
            drifts.scaleHeight(0.4f);
            drifts.addHeight(0.5f);
            return FinishTexture(drifts, normalFlatness: 90f);
        }

        private int createMudTexture()
        {
            var mud = procTextGen.PerlinFBM(new Vector2(14f, 14f), octaves: 8, amplitudePerOctave: 0.55f);
            mud.setMaterial(new Material(new Colour(48, 38, 29), 0.42f, 0f, 0f, 0f));
            var wet = procTextGen.PerlinFBM(new Vector2(6f, 6f), octaves: 4, amplitudePerOctave: 0.6f);
            wet.setMaterial(new Material(new Colour(30, 26, 22), 0.16f, 0f, 0f, 0f));
            MaterialLayersCombiner.combine(mud, wet, FilterMode.Lesser, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.4f);
            mud.scaleHeight(0.5f);
            mud.addHeight(0.4f);
            return FinishTexture(mud, normalFlatness: 70f);
        }

        private int createMossTexture()
        {
            var moss = procTextGen.PerlinFBM(new Vector2(40f, 40f), octaves: 7, amplitudePerOctave: 0.7f);
            moss.setMaterial(new Material(new Colour(58, 74, 31), 0.92f, 0f, 0f, 0.3f));
            var soil = procTextGen.PerlinFBM(new Vector2(10f, 10f), octaves: 5, amplitudePerOctave: 0.55f);
            soil.setMaterial(new Material(new Colour(52, 44, 30), 0.95f, 0f, 0f, 0f));
            MaterialLayersCombiner.combine(moss, soil, FilterMode.Lesser, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.42f, smoothness: 0.3f);
            moss.scaleHeight(0.6f);
            moss.addHeight(0.35f);
            return FinishTexture(moss, normalFlatness: 60f);
        }

        private int createGravelTexture()
        {
            float seed = MyMath.rand.NextSingle() * 100000f;
            var stones = procTextGen.Cellular(new Vector2(46f, 46f), jitter: 1f, metric: Metric.SquaredEuclidean, seed: seed).invertHeight();
            stones.setMaterial(new Material(new Colour(82, 74, 62), 0.9f, 0f, 0f));
            var tones = procTextGen.Voronoi(new Vector2(46f, 46f), jitter: 1f, returnMode: ReturnMode.ID, seed: seed);
            tones.setMaterial(new Material(new Colour(46, 40, 32), 0.9f, 0f, 0f));
            MaterialLayersCombiner.combine(stones, tones, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Mix, weight: 0.45f, smoothness: 0.5f);
            var grit = procTextGen.PerlinFBM(new Vector2(30f, 30f), octaves: 6, amplitudePerOctave: 0.5f);
            MaterialLayersCombiner.combine(stones, grit, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.3f);
            stones.scaleHeight(0.6f);
            stones.addHeight(0.25f);
            return FinishTexture(stones, normalFlatness: 45f);
        }

        // Earth packed hard by feet and wheels: mostly flat, with stones worn into it.
        private int createDirtPathTexture()
        {
            var dirt = procTextGen.PerlinFBM(new Vector2(12f, 12f), octaves: 8, amplitudePerOctave: 0.5f);
            dirt.setMaterial(new Material(new Colour(78, 58, 40), 0.93f, 0f, 0f));
            var pale = procTextGen.PerlinFBM(new Vector2(5f, 5f), octaves: 4, amplitudePerOctave: 0.5f);
            pale.setMaterial(new Material(new Colour(98, 76, 54), 0.95f, 0f, 0f));
            MaterialLayersCombiner.combine(dirt, pale, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.5f);

            var stones = procTextGen.Cellular(new Vector2(30f, 30f), jitter: 1f, metric: Metric.SquaredEuclidean).invertHeight();
            stones.setMaterial(new Material(new Colour(84, 76, 66), 0.85f, 0f, 0f));
            MaterialLayersCombiner.combine(dirt, stones, FilterMode.Greater, heightOperation: Operation.Nothing, materialOperation: Operation.Override, weight: 0.86f, smoothness: 0.05f);

            dirt.scaleHeight(0.45f);
            dirt.addHeight(0.45f);
            return FinishTexture(dirt, normalFlatness: 70f);
        }

        private int createFarmlandTexture()
        {
            var clods = procTextGen.PerlinFBM(new Vector2(22f, 22f), octaves: 6, amplitudePerOctave: 0.55f, rigged: true);
            clods.setMaterial(new Material(new Colour(62, 44, 30), 0.97f, 0f, 0f));
            var dry = procTextGen.PerlinFBM(new Vector2(7f, 7f), octaves: 5, amplitudePerOctave: 0.5f);
            dry.setMaterial(new Material(new Colour(92, 68, 46), 0.97f, 0f, 0f));
            MaterialLayersCombiner.combine(clods, dry, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.55f, smoothness: 0.4f);
            clods.scaleHeight(0.8f);
            return FinishTexture(clods, normalFlatness: 40f);
        }

        // Boards running along v, a little uneven, with dark gaps between them. Pale, to be stained by vertex colour.
        private int createWoodTexture()
        {
            var boards = procTextGen.Createbricks(new Vector2(1f, 6f), smoothness: 0.05f, spacing: 0.012f, returnMode: ReturnMode.Height);
            boards.setMaterial(new Material(new Colour(112, 84, 58), 0.72f, 0f, 0f));
            var grain = procTextGen.PerlinFBM(new Vector2(3f, 70f), octaves: 6, amplitudePerOctave: 0.6f);
            grain.setMaterial(new Material(new Colour(74, 54, 36), 0.8f, 0f, 0f));
            MaterialLayersCombiner.combine(boards, grain, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Smoothstep, weight: 0.32f, smoothness: 0.5f);
            return FinishTexture(boards, normalFlatness: 60f);
        }

        // Bundled reeds running down the roof: long strands in overlapping courses.
        private int createThatchTexture()
        {
            var strands = procTextGen.PerlinFBM(new Vector2(70f, 3f), octaves: 7, amplitudePerOctave: 0.65f);
            strands.setMaterial(new Material(new Colour(108, 86, 50), 0.95f, 0f, 0f, 0.15f));
            var dark = procTextGen.PerlinFBM(new Vector2(30f, 2f), octaves: 5, amplitudePerOctave: 0.55f);
            dark.setMaterial(new Material(new Colour(54, 42, 26), 0.97f, 0f, 0f));
            MaterialLayersCombiner.combine(strands, dark, FilterMode.Lesser, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.5f, smoothness: 0.3f);

            var courses = procTextGen.Sine(new Vector2(0f, 5f));
            MaterialLayersCombiner.combine(strands, courses, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.35f);
            strands.scaleHeight(0.75f);
            return FinishTexture(strands, normalFlatness: 45f);
        }

        // Overlapping wooden shingles.
        private int createShingleTexture()
        {
            Vector2 count = new Vector2(10f, 12f);
            var tiles = procTextGen.Createbricks(count, smoothness: 0.08f, spacing: 0.012f, returnMode: ReturnMode.Height);
            var tone = procTextGen.Createbricks(count, smoothness: 0.08f, spacing: 0.012f, returnMode: ReturnMode.ID);
            tiles.setMaterial(new Material(new Colour(112, 104, 96), 0.78f, 0f, 0f));
            tone.setMaterial(new Material(new Colour(70, 66, 64), 0.82f, 0f, 0f));
            MaterialLayersCombiner.combine(tiles, tone, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Mix, weight: 0.5f, smoothness: 0.5f);
            var wear = procTextGen.PerlinFBM(new Vector2(40f, 40f), octaves: 5, amplitudePerOctave: 0.55f);
            MaterialLayersCombiner.combine(tiles, wear, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.12f);
            return FinishTexture(tiles, normalFlatness: 40f);
        }

        private int createSlateTexture()
        {
            Vector2 count = new Vector2(8f, 16f);
            var tiles = procTextGen.Createbricks(count, smoothness: 0.04f, spacing: 0.01f, returnMode: ReturnMode.Height);
            var tone = procTextGen.Createbricks(count, smoothness: 0.04f, spacing: 0.01f, returnMode: ReturnMode.ID);
            tiles.setMaterial(new Material(new Colour(72, 78, 86), 0.55f, 0f, 0f));
            tone.setMaterial(new Material(new Colour(44, 48, 56), 0.6f, 0f, 0f));
            MaterialLayersCombiner.combine(tiles, tone, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Mix, weight: 0.5f, smoothness: 0.5f);
            return FinishTexture(tiles, normalFlatness: 40f);
        }

        private int createPlasterTexture()
        {
            var wall = procTextGen.PerlinFBM(new Vector2(9f, 9f), octaves: 8, amplitudePerOctave: 0.55f);
            wall.setMaterial(new Material(new Colour(150, 140, 120), 0.92f, 0f, 0f));
            var stain = procTextGen.PerlinFBM(new Vector2(3f, 2f), octaves: 4, amplitudePerOctave: 0.6f);
            stain.setMaterial(new Material(new Colour(112, 100, 80), 0.95f, 0f, 0f));
            MaterialLayersCombiner.combine(wall, stain, FilterMode.Lesser, heightOperation: Operation.Nothing, materialOperation: Operation.Smoothstep, weight: 0.45f, smoothness: 0.4f);
            wall.scaleHeight(0.2f);
            wall.addHeight(0.7f);
            return FinishTexture(wall, normalFlatness: 120f);
        }

        // Rough courses of stone with deep mortar.
        private int createStoneWallTexture()
        {
            Vector2 count = new Vector2(4f, 8f);
            var stones = procTextGen.Createbricks(count, smoothness: 0.16f, spacing: 0.03f, returnMode: ReturnMode.Height);
            var tone = procTextGen.Createbricks(count, smoothness: 0.16f, spacing: 0.03f, returnMode: ReturnMode.ID);
            stones.setMaterial(new Material(new Colour(112, 106, 96), 0.93f, 0f, 0f));
            tone.setMaterial(new Material(new Colour(70, 66, 60), 0.95f, 0f, 0f));
            MaterialLayersCombiner.combine(stones, tone, FilterMode.Everywhere, heightOperation: Operation.Nothing, materialOperation: Operation.Mix, weight: 0.5f, smoothness: 0.5f);
            var rough = procTextGen.PerlinFBM(new Vector2(18f, 18f), octaves: 7, amplitudePerOctave: 0.6f);
            MaterialLayersCombiner.combine(stones, rough, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.32f);
            return FinishTexture(stones, normalFlatness: 35f);
        }

        // Plain woven cloth, near white so that vertex colour dyes it.
        private int createClothTexture()
        {
            var weave = procTextGen.TileWeave(new Vector2(48f, 48f), count: 2, smoothness: 0.8f, width: 0.6f);
            weave.setMaterial(new Material(new Colour(GearMaterials.ClothAlbedo), 0.96f, 0f, 0f, 0.3f));
            weave.scaleHeight(0.3f);
            weave.addHeight(0.5f);
            return FinishTexture(weave, normalFlatness: 80f);
        }

        private int createLeatherTexture()
        {
            var hide = procTextGen.Cellular(new Vector2(30f, 30f), jitter: 1f, metric: Metric.SquaredEuclidean);
            hide.setMaterial(new Material(new Colour(GearMaterials.LeatherAlbedo), 0.62f, 0f, 0f));
            hide.scaleHeight(0.3f);
            hide.addHeight(0.5f);
            return FinishTexture(hide, normalFlatness: 80f);
        }

        private int createGrassTexture(Material material)
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            roughLayer.setMaterial(material);
            roughLayer.scaleHeight(1.0f);
            return FinishTexture(roughLayer);
        }
        private int createIceTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 2, amplitudePerOctave: 0.9f);
            roughLayer.setMaterial(new Material(new Colour(100, 152, 243), 0.3f, 0f, 0.85f, 0.90f));
            return FinishTexture(roughLayer);
        }

        private int createWaxTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(6f, 6f), octaves: 2, amplitudePerOctave: 0.5f);
            roughLayer.setMaterial(new Material(new Colour(105, 105, 85), 0.9f, 0f, 0.0f, 0.1f));
            return FinishTexture(roughLayer);
        }

        private int createGrainTexture()
        {
            MaterialLayer roughLayer = procTextGen.PerlinFBM(new Vector2(8f, 8f), octaves: 8, amplitudePerOctave: 0.8f);
            roughLayer.setMaterial(new Material(new Colour(255, 10, 25), 0.4f, 0f, 0.0f, 0.8f));
            return FinishTexture(roughLayer);
        }

        private int createSandTexture()
        {
            var noise = procTextGen.PerlinFBM(new Vector2(2f, 2f), octaves: 10, amplitudePerOctave: 0.8f);
            noise.setMaterial(Material.SAND);
            return FinishTexture(noise, normalFlatness: 40);
        }

        private int createSandDunesTexture()
        {
            var sandDunes = procTextGen.PerlinFBM(new Vector2(4f, 8f), octaves: 1, amplitudePerOctave: 0.27f, rigged: true);
            var noise = procTextGen.PerlinFBM(new Vector2(2f, 2f), octaves: 10, amplitudePerOctave: 0.8f);
            var noiseLarge = procTextGen.PerlinFBM(new Vector2(22f, 22f), octaves: 2, amplitudePerOctave: 0.5f);
            sandDunes.setMaterial(Material.SAND);
            noiseLarge.setMaterial(Material.SAND);
            noise.setMaterial(Material.GROUND_ROCK);

            MaterialLayersCombiner.combine(sandDunes, noise, FilterMode.Everywhere, heightOperation: Operation.Add, materialOperation: Operation.Nothing, weight: 0.2f, smoothness: 0.9f);

            //MaterialLayersCombiner.combine(noise, noiseLarge.scaleHeight(1f), FilterMode.Everywhere, heightOperation: Operation.Scale, materialOperation: Operation.Mix, weight: 0.2f, smoothness: 0.5f);
            //MaterialLayersCombiner.combine(sandDunes, noise, FilterMode.Greater, heightOperation: Operation.Override, materialOperation: Operation.Override, weight: 0.5f, smoothness: 0.9f);

            return FinishTexture(sandDunes, normalFlatness:60);
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
            _textureNormalShader.cleanUp();
            textureImposterStudio.CleanUp();
        }
    }
}
