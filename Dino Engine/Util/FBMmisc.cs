using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dino_Engine.Util
{
    public class FBMmisc
    {


        public static float[] sinFBM(int octaves, float frequenzy, int resolution)
        {
            float[] result = new float[resolution];

            float amplitude = 1f;
            float totalAmplitde = 0f;
            for (int i = 0; i < resolution; i++)
            {
                result[i] = 0;
            }

            for (int octave = 0; octave < octaves; octave++)
            {
                for (int i = 0; i < resolution; i++)
                {
                    result[i] += MathF.Sin(frequenzy* (i/(float)(resolution))*MathF.Tau);
                }

                totalAmplitde += amplitude;

                amplitude *= 0.5f;
                frequenzy *= 2.0f;
            }
            for (int i = 0; i < resolution; i++)
            {
                result[i] /= totalAmplitde;
            }

            return result;
        }
    }
}
