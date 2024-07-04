using Dino_Engine.Util;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Util.Noise;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class TerrainGridGenerator
    {

        public float _mountainCoverage = 0.5f;
        public float _frequenzy = 0.005f;
        public float _amplitude = 80f;
        public int _octaves = 8;

        private OpenSimplexNoise noise = new OpenSimplexNoise(4499954);

        public Grid generateChunk(Vector2i resolution)
        {
            Grid grid = new Grid(resolution);

            for (int z = 0; z < grid.Resolution.Y; z++)
            {
                for (int x = 0; x < grid.Resolution.X; x++)
                {
                    grid.Values[x, z] = getHeightAt(new Vector2(x, z));
                }
            }
            return grid;
        }

        public float getHeightAt(Vector2 position)
        {
            float x = position.X;
            float z = position.Y;
            float y = 0f;
            float frequency = _frequenzy;
            float amplitude = _amplitude;
            float totalAmplitude = 0f;

            float mountainFactor = MyMath.clamp01( noise.Evaluate(position.X*0.01f, position.Y*0.01f)/2f+0.5f);
            mountainFactor = MathF.Pow(mountainFactor, 1.6f);
            for (int i = 0; i < _octaves; i++)
            {
                y +=( 1f-MathF.Abs( noise.Evaluate(x * frequency, z * frequency))) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            y /= totalAmplitude;
            y *= mountainFactor;
            y -= 0.1f;

            y *= 150f;

            return y;
        }

        
    }
}
