
// Classic FBM implementation using Value noise.
// @param scale Number of tiles, must be  integer for tileable results, range: [2, inf]
// @param octaves Number of octaves for the fbm, range: [1, inf]
// @param shift Position shift for each octave, range: [0, inf]
// @param timeShift Time shift for each octave, range: [-inf, inf]
// @param gain Gain for each fbm octave, range: [0, 2], default: 0.5
// @param lacunarity Frequency of the fbm, must be integer for tileable results, range: [1, 32]
// @param octaveFactor The octave intensity factor, the lower the more pronounced the lower octaves will be, range: [-1, 1], default: 0.0
// @param seed Seed to randomize result, range: [0, inf], default: 0.0
// @return value of the noise, range: [0, inf]
float fbm(vec2 pos, vec2 scale, int octaves, float shift, float timeShift, float gain, float lacunarity, float octaveFactor, float seed)
{
    float amplitude = gain;
    float time = timeShift;
    vec2 frequency = scale;
    vec2 offset = vec2(shift, 0.0);
    vec2 p = pos * frequency;
    octaveFactor = 1.0 + octaveFactor * 0.12;

    vec2 sinCos = vec2(sin(shift), cos(shift));
    mat2 rotate = mat2(sinCos.y, sinCos.x, sinCos.x, sinCos.y);

    float value = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        float n = noise(p / frequency, frequency, time, seed);
        value += amplitude * n;

        p = p * lacunarity + offset * float(1 + i);
        frequency *= lacunarity;
        amplitude = pow(amplitude * gain, octaveFactor);
        time += timeShift;
        offset *= rotate;
    }
    return value * 0.5 + 0.5;
}


// FBM implementation using Perlin noise, can also be used to create ridges based on the mode used.
// @param scale Number of tiles, must be  integer for tileable results, range: [2, inf]
// @param octaves Number of octaves for the fbm, range: [1, inf]
// @param shift Position shift for each octave, range: [0, inf]
// @param axialShift Axial or rotational shift for each octave, range: [-inf, inf]
// @param gain Gain for each fbm octave, range: [0, 2], default: 0.5
// @param lacunarity Frequency of the fbm, must be integer for tileable results, range: [1, 32]
// @param mode Mode used in combining the noise for the ocatves, range: [0, 5]
// @param factor Pow intensity factor, range: [0, 10], default: 1.0
// @param offset Offsets the value of the noise, range: [-1, 1], default: 0.0
// @param octaveFactor The octave intensity factor, the lower the more pronounced the lower octaves will be, range: [-1, 1], default: 0.0
// @param seed Seed to randomize result, range: [0, inf], default: 0.0
// @return value of the noise, range: [0, inf]
float fbmPerlin(vec2 pos, vec2 scale, int octaves, float shift, float axialShift, float gain, float lacunarity, uint mode, float factor, float offset, float octaveFactor, float seed)
{
    float amplitude = 1.0;
    float totalAmplitude = 0.0;
    vec2 frequency = floor(scale);
    float angle = axialShift;
    float n = 1.0;
    vec2 p = fract(pos) * frequency;

    float value = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        float pn = perlinNoise(p / frequency, frequency, angle, seed) + offset;
        
        if (mode == 0u)
        {
            n *= abs(pn);
        }
        else if (mode == 1u)
        {
            n = 1.0-abs(pn);
        }
        else if (mode == 2u)
        {
            pn = pn * 0.5f + 0.5f;
            n = pn;
        }
        else if (mode == 3u)
        {
            n *= pn;
        }
        else if (mode == 4u)
        {
            n = pn * 0.5 + 0.5;
        }
        else
        {
            n *= pn * 0.5 + 0.5;
        }

        value += amplitude * n;
        totalAmplitude += amplitude;

        p = p * lacunarity + shift;
        frequency *= lacunarity;
        amplitude = pow(amplitude * gain, octaveFactor);
        angle += axialShift;
    }
    value = value/totalAmplitude;

    value = pow(value < 0.0 ? 0.0 : value, factor);


    return value;
}
