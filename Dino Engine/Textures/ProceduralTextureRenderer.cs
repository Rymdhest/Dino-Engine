using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Rendering;
using Dino_Engine.Rendering.Renderers;
using Dino_Engine.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Reflection.Emit;

namespace Dino_Engine.Textures
{
    public class ProceduralTextureRenderer
    {

        private Random rand = new Random();

        private ShaderProgram _textureFBMShader = new ShaderProgram("Simple.vert", "Texture_FBM.frag");
        private ShaderProgram _textureTileweaveShader = new ShaderProgram("Simple.vert", "Texture_Tileweave.frag");
        private ShaderProgram _textureVoronoiShader = new ShaderProgram("Simple.vert", "Texture_Voronoi.frag");
        private ShaderProgram _textureVoronoiCracksShader = new ShaderProgram("Simple.vert", "Texture_Voronoi_Cracks.frag");
        private ShaderProgram _textureCellularShader = new ShaderProgram("Simple.vert", "Texture_Cellular.frag");
        private ShaderProgram _textureFlatShader = new ShaderProgram("Simple.vert", "Texture_Flat.frag");
        private ShaderProgram _textureBricksShader = new ShaderProgram("Simple.vert", "Texture_Bricks.frag");
        private ShaderProgram _textureSineShader = new ShaderProgram("Simple.vert", "Texture_Sine.frag");

        private static readonly Colour defaultColour = new Colour(255, 255, 255);
        private Vector4 defaultmaterial = new Vector4(0.5f, 0f, 0f, 1.0f);
        public enum Metric
        {
            SquaredEuclidean,
            manhattam,   
            Chebyshev,
            Triangular
        }
        public enum ReturnMode
        {
            Height,
            ID
        }

        public ProceduralTextureRenderer()
        {
        }
        public MaterialLayer CreateFlatHeight(float height)
        {
            var layer = new MaterialLayer(bind: true);
            _textureFlatShader.bind();
            _textureFlatShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureFlatShader.loadUniformVector4f("material", defaultmaterial);
            _textureFlatShader.loadUniformFloat("height", height);
            return layer.tap();
        }
        public MaterialLayer CreateMaterial(Colour colour, Vector3 material, float height = 1.0f)
        {
            var layer = new MaterialLayer(bind: true);
            _textureFlatShader.bind();
            _textureFlatShader.loadUniformVector4f("albedo", colour.ToVector4());
            _textureFlatShader.loadUniformVector4f("material", new Vector4(material.X, material.Y, material.Z, 1.0f));
            _textureFlatShader.loadUniformFloat("height", height);
            return layer.tap();
        }

        public MaterialLayer Flat(float height)
        {
            var layer = new MaterialLayer(bind: true);
            _textureFlatShader.bind();
            _textureFlatShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureFlatShader.loadUniformVector4f("material", defaultmaterial);
            _textureFlatShader.loadUniformFloat("height", height);
            return layer.tap();
        }
        public MaterialLayer Createbricks(Vector2 numBricks, float spacing = 0.01f, float smoothness = 0.0f, ReturnMode returnMode = ReturnMode.Height)
        {
            var layer = new MaterialLayer(bind: true);
            _textureBricksShader.bind();
            _textureBricksShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureBricksShader.loadUniformVector4f("material", defaultmaterial);
            _textureBricksShader.loadUniformVector2f("numBricks", numBricks);
            _textureBricksShader.loadUniformFloat("spacing", spacing);
            _textureBricksShader.loadUniformFloat("smoothness", smoothness);
            _textureBricksShader.loadUniformInt("returnMode", (int)returnMode);
            return layer.tap();
        }

        public MaterialLayer Sine(Vector2 frequenzy)
        {
            var layer = new MaterialLayer(bind: true);
            _textureSineShader.bind();
            _textureSineShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureSineShader.loadUniformVector4f("material", defaultmaterial);
            _textureSineShader.loadUniformVector2f("frequenzy", frequenzy);
            return layer.tap();
        }

        public MaterialLayer Cellular(Vector2 scale, Metric metric = Metric.SquaredEuclidean, float jitter = 0.5f, float phase = 0.5f, bool rigged = false, float seed = -1.0f)
        {
            var layer = new MaterialLayer(bind: true);
            if (seed < 0) seed = rand.NextSingle() * 100000.0f;
            _textureCellularShader.bind();
            _textureCellularShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureCellularShader.loadUniformVector4f("material", defaultmaterial);
            _textureCellularShader.loadUniformVector2f("scale", scale);
            _textureCellularShader.loadUniformFloat("jitter", jitter);
            _textureCellularShader.loadUniformFloat("phase", phase);
            _textureCellularShader.loadUniformFloat("seed", seed);
            _textureCellularShader.loadUniformInt("metric", (int)metric);
            _textureCellularShader.loadUniformBool("rigged", rigged);
            return layer.tap();
        }

        public MaterialLayer VoronoiCracks(Vector2 scale, ReturnMode returnMode = ReturnMode.Height, float jitter = 0.5f, float width = 0.1f, float smoothness = 0.1f, float warp = 0.1f, float warpScale = 0.1f, bool warpSmudge = false, float smudgePhase = 0.1f, float seed = -1.0f)
        {
            var layer = new MaterialLayer(bind: true);
            if (seed < 0) seed = rand.NextSingle() * 100000.0f;
            _textureVoronoiCracksShader.bind();
            _textureVoronoiCracksShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureVoronoiCracksShader.loadUniformVector4f("material", defaultmaterial);
            _textureVoronoiCracksShader.loadUniformVector2f("scale", scale);
            _textureVoronoiCracksShader.loadUniformFloat("jitter", jitter);
            _textureVoronoiCracksShader.loadUniformFloat("width", width);
            _textureVoronoiCracksShader.loadUniformFloat("smoothness", smoothness);
            _textureVoronoiCracksShader.loadUniformFloat("warp", warp);
            _textureVoronoiCracksShader.loadUniformFloat("warpScale", warpScale);
            _textureVoronoiCracksShader.loadUniformFloat("smudgePhase", smudgePhase);
            _textureVoronoiCracksShader.loadUniformBool("warpSmudge", warpSmudge);
            _textureVoronoiCracksShader.loadUniformFloat("seed", seed);
            _textureVoronoiCracksShader.loadUniformInt("returnMode", (int)returnMode);
            return layer.tap();
        }

        public MaterialLayer Voronoi(Vector2 scale, ReturnMode returnMode = ReturnMode.Height, float jitter = 0.5f, float phase = 0.5f, float seed = -1.0f)
        {
            var layer = new MaterialLayer(bind: true);
            if (seed < 0) seed = rand.NextSingle() * 100000.0f;
            _textureVoronoiShader.bind();
            _textureVoronoiShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureVoronoiShader.loadUniformVector4f("material", defaultmaterial);
            _textureVoronoiShader.loadUniformVector2f("scale", scale);
            _textureVoronoiShader.loadUniformFloat("jitter", jitter);
            _textureVoronoiShader.loadUniformFloat("phase", phase);
            _textureVoronoiShader.loadUniformFloat("seed", seed);
            _textureVoronoiShader.loadUniformInt("returnMode", (int)returnMode);
            return layer.tap();
        }

        public MaterialLayer TileWeave(Vector2 scale, int count = 3, float width = 0.5f, float smoothness = 0.6f)
        {
            var layer = new MaterialLayer(bind: true);
            _textureTileweaveShader.bind();
            _textureTileweaveShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureTileweaveShader.loadUniformVector4f("material", defaultmaterial);
            _textureTileweaveShader.loadUniformVector2f("scale", scale);
            _textureTileweaveShader.loadUniformInt("count", count);
            _textureTileweaveShader.loadUniformFloat("width", width);
            _textureTileweaveShader.loadUniformFloat("smoothness", smoothness);
            return layer.tap();
        }

        public MaterialLayer PerlinFBM(Vector2 frequenzy, int octaves = 10,bool rigged = false, int frequenzyPerOctave = 2, float amplitudePerOctave = 0.5f, float exponent = 1f ,float seed = -1)
        {
            var layer = new MaterialLayer(bind: true);
            if (seed < 0) seed = rand.NextSingle()*100000.0f;
            _textureFBMShader.bind();
            _textureFBMShader.loadUniformInt("octaves", octaves);
            _textureFBMShader.loadUniformFloat("seed", seed);
            _textureFBMShader.loadUniformFloat("exponent", exponent);
            _textureFBMShader.loadUniformFloat("amplitudePerOctave", amplitudePerOctave);
            _textureFBMShader.loadUniformInt("frequenzyPerOctave", frequenzyPerOctave);
            _textureFBMShader.loadUniformVector4f("albedo", defaultColour.ToVector4());
            _textureFBMShader.loadUniformVector4f("material", defaultmaterial);
            _textureFBMShader.loadUniformBool("rigged", rigged);
            _textureFBMShader.loadUniformVector2f("startFrequenzy", frequenzy);
            return layer.tap();
        }

        public void CleanUp()
        {
            _textureFBMShader.cleanUp();
        }
    }
}
