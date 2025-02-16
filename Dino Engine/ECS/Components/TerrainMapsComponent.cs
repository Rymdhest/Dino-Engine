﻿using Dino_Engine.Util;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Util.Noise;

namespace Dino_Engine.ECS.Components
{
    public class TerrainMapsComponent : Component
    {

        public FloatGrid heightMap;
        public Vector3Grid normalMap;
        public FloatGrid steepnessMap;
        public FloatGrid grassMap;
        public TerrainMapsComponent(FloatGrid heightMap, Vector3Grid normalMap)
        {
            this.heightMap = heightMap;
            this.normalMap = normalMap;

            steepnessMap = new FloatGrid(normalMap.Resolution);
            for (int z = 0; z < steepnessMap.Resolution.Y; z++)
            {
                for (int x = 0; x < steepnessMap.Resolution.X; x++)
                {
                    float value = Vector3.Dot(new Vector3(0f, 1f, 0f), normalMap.Values[x, z]);
                    steepnessMap.Values[x, z] = MyMath.clamp01(MathF.Pow(value, 4.0f));
                }
            }

            OpenSimplexNoise grassNoise = new OpenSimplexNoise();

            grassMap = new FloatGrid(heightMap.Resolution);
            float min = 0.03f;
            for (int z = 0; z < grassMap.Resolution.Y; z++)
            {
                for (int x = 0; x < grassMap.Resolution.X; x++)
                {
                    float noise = grassNoise.FBM01(x, z, 0.4f, 3);
                    noise *= 0.01f+0.99f*grassNoise.FBM01(x, z, 0.1f, 3);
                    noise = noise * (1f- min) + min;

                    float value = noise * steepnessMap.Values[x, z];
                    if (heightMap.Values[x, z] < -5.1f) value = 0f;

                    grassMap.Values[x, z] = MyMath.clamp01(value);


                }
            }
        }
    }
}
