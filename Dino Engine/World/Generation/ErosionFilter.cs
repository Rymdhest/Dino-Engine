// A port to C# of Rune Skovbo Johansen's Advanced Terrain Erosion Filter and Phacelle Noise, for a height function evaluated a point at a time.
// See https://blog.runevision.com/2026/03/fast-and-gorgeous-erosion-filter.html
//
// Phacelle Noise function copyright (c) 2025 Rune Skovbo Johansen
// Advanced Terrain Erosion Filter copyright (c) 2025 Rune Skovbo Johansen
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// This file alone is under that licence, not the MIT licence of the rest of this repository.

using static Dino_Engine.World.WorldMath;

namespace Dino_Engine.World.Generation
{
    /// <summary>
    /// Branching gullies and ridges laid over any height, from nothing but the height and slope at a point, so that a world can be sculpted in parallel,
    /// in any order. Stripes run down the slope and each octave of them bends the next, which is what makes them branch. See the note at the top of this file.
    /// </summary>
    internal static class ErosionFilter
    {
        private const float Tau = MathF.Tau;

        /// <summary>How a rock weathers. Metres for <see cref="Scale"/>; the slopes it makes go by <see cref="Strength"/>.</summary>
        public struct Style
        {
            public float Scale;             // Metres between the largest gullies.
            public float Strength;          // How deep they are, as a slope. All octaves make about the same slope.
            public float GullyWeight;       // 1 is full gullies, 0 sharpens crests and valleys without any.
            public float Detail;            // Lower keeps small gullies to steeper ground.
            public float RidgeRounding, CreaseRounding, InputRounding, OctaveRounding;
            public float InputOnset, OctaveOnset, RidgeInputOnset, RidgeOctaveOnset;
            public float AssumedSlope, AssumedShare;
            public float CellScale;
            public float Normalization;
            public int Octaves;
            public float Lacunarity;
            public float Gain;
        }

        /// <summary>What it did to a point.</summary>
        public struct Result
        {
            public float Height;    // Metres to add.
            public float Reach;     // The most it could have added, in metres.
            public float Ridges;    // -1 in a crease, 1 on a crest.
            public float Fade;      // What the last octave ended on: where a crease has gathered, this is low.
        }

        public static Result Apply(float x, float z, float slopeX, float slopeZ, float fadeTarget, uint seed, in Style style)
        {
            float strength = style.Strength * style.Scale;
            fadeTarget = Math.Clamp(fadeTarget, -1f, 1f);

            float frequency = 1f / (style.Scale * style.CellScale);
            float slopeLength = MathF.Max(MathF.Sqrt(slopeX * slopeX + slopeZ * slopeZ), 1e-10f);
            float height = 0f;
            float magnitude = 0f;
            float roundingMultiplier = 1f;

            float roundingForInput = Lerp(style.CreaseRounding, style.RidgeRounding, Saturate(fadeTarget + 0.5f)) * style.InputRounding;
            float mask = EaseOut(SmoothStart(slopeLength * style.InputOnset, roundingForInput * style.InputOnset));

            float ridgeMask = EaseOut(slopeLength * style.RidgeInputOnset);
            float ridgeTarget = fadeTarget;

            // Starting the gullies from an assumed slope works better than the real one, as what comes out is shaped quite differently from what went in.
            float assumed = style.AssumedSlope / slopeLength;
            float gullyX = Lerp(slopeX, slopeX * assumed, style.AssumedShare);
            float gullyZ = Lerp(slopeZ, slopeZ * assumed, style.AssumedShare);

            for (int octave = 0; octave < style.Octaves; octave++)
            {
                float length = MathF.Sqrt(gullyX * gullyX + gullyZ * gullyZ);
                float directionX = length > 1e-10f ? gullyX / length : gullyX;
                float directionZ = length > 1e-10f ? gullyZ / length : gullyZ;

                Phacelle(x * frequency, z * frequency, directionX, directionZ, style.CellScale, 0.25f, style.Normalization, seed + (uint)octave * 0x9E3779B1u, out float wave, out float slope, out float sideX, out float sideZ);
                sideX *= -frequency;
                sideZ *= -frequency;

                float sloping = MathF.Abs(slope);
                float sign = slope < 0f ? -1f : slope > 0f ? 1f : 0f;

                gullyX += sign * sideX * strength * style.GullyWeight;
                gullyZ += sign * sideZ * strength * style.GullyWeight;

                float faded = Lerp(fadeTarget, wave * style.GullyWeight, mask);
                height += faded * strength;
                magnitude += strength;
                fadeTarget = faded;

                float roundingForOctave = Lerp(style.CreaseRounding, style.RidgeRounding, Saturate(wave + 0.5f)) * roundingMultiplier;
                float newMask = EaseOut(SmoothStart(sloping * style.OctaveOnset, roundingForOctave * style.OctaveOnset));
                mask = PowInverse(mask, style.Detail) * newMask;

                ridgeTarget = Lerp(ridgeTarget, wave, ridgeMask);
                ridgeMask *= EaseOut(sloping * style.RidgeOctaveOnset);

                strength *= style.Gain;
                frequency *= style.Lacunarity;
                roundingMultiplier *= style.OctaveRounding;
            }

            return new Result { Height = height, Reach = magnitude, Ridges = ridgeTarget * (1f - ridgeMask), Fade = fadeTarget };
        }

        // Stripes aligned with a direction: 4 x 4 cells each with a pivot, whose waves are blended as phases, not as heights.
        private static void Phacelle(float x, float z, float directionX, float directionZ, float frequency, float offset, float normalization, uint seed, out float wave, out float slope, out float sideX, out float sideZ)
        {
            sideX = -directionZ * frequency * Tau;
            sideZ = directionX * frequency * Tau;
            offset *= Tau;

            float cellX = MathF.Floor(x);
            float cellZ = MathF.Floor(z);
            float fractionX = x - cellX;
            float fractionZ = z - cellZ;
            int baseX = (int)cellX;
            int baseZ = (int)cellZ;

            float phaseCos = 0f;
            float phaseSin = 0f;
            float weightSum = 0f;

            for (int i = -1; i <= 2; i++)
            {
                for (int j = -1; j <= 2; j++)
                {
                    uint hash = Hash((uint)(baseX + i), (uint)(baseZ + j), seed);
                    float randomX = ((hash & 0xFFFF) / 65535f * 2f - 1f) * 0.5f;
                    float randomZ = ((hash >> 16) / 65535f * 2f - 1f) * 0.5f;

                    float fromX = fractionX - i - randomX;
                    float fromZ = fractionZ - j - randomZ;

                    float squared = fromX * fromX + fromZ * fromZ;
                    if (squared >= 2.25f) continue;
                    float weight = MathF.Exp(-squared * 2f) - 0.01111f;
                    if (weight <= 0f) continue;

                    weightSum += weight;
                    (float sin, float cos) = MathF.SinCos(fromX * sideX + fromZ * sideZ + offset);
                    phaseCos += cos * weight;
                    phaseSin += sin * weight;
                }
            }

            float scale = weightSum > 0f ? 1f / weightSum : 0f;
            phaseCos *= scale;
            phaseSin *= scale;

            float magnitude = MathF.Max(1f - normalization, MathF.Sqrt(phaseCos * phaseCos + phaseSin * phaseSin));
            wave = phaseCos / magnitude;
            slope = phaseSin / magnitude;
        }

        private static float PowInverse(float t, float power) => 1f - MathF.Pow(1f - Saturate(t), power);

        private static float EaseOut(float t)
        {
            float v = 1f - Saturate(t);
            return 1f - v * v;
        }

        private static float SmoothStart(float t, float smoothing)
        {
            if (t >= smoothing) return t - 0.5f * smoothing;
            return smoothing > 0f ? 0.5f * t * t / smoothing : t;
        }

        private static uint Hash(uint x, uint z, uint seed)
        {
            unchecked
            {
                uint h = seed ^ x * 0x85EBCA6Bu ^ z * 0xC2B2AE35u;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
