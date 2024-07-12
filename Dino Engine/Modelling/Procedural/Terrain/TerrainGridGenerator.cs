using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
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

        public float _mountainCoverage = 0.2f;
        public float _frequenzy = 0.007f;
        public int _octaves = 9;

        private OpenSimplexNoise noise = new OpenSimplexNoise();

        public FloatGrid generateChunk(Vector2i resolution)
        {
            FloatGrid grid = new FloatGrid(resolution);

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
            float amplitude = 1f;
            float totalAmplitude = 0f;

            float mountainFactor = MyMath.clamp01( noise.Evaluate(position.X*0.006f, position.Y*0.006f)/2f+0.5f);
            mountainFactor = MathF.Pow(mountainFactor, 2.8f);
            for (int i = 0; i < _octaves; i++)
            {
                y +=( 1f-MathF.Abs( noise.Evaluate(x * frequency, z * frequency))) * amplitude;
                totalAmplitude += amplitude;
                frequency *= 2f;
                amplitude *= 0.5f;
            }
            y /= totalAmplitude;
            y *= mountainFactor;

            y *= 200f;
            y -= 5f;
            if (y < 0f) y *= 0.1f;

            return y;
        }

        
    }
}
