using Dino_Engine.Debug;
using Dino_Engine.Rendering;
using Dino_Engine.Util.Data_Structures;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.Util.Noise
{
    public class PoissonDiskSampling
    {
        private Grid noiseMap;
        private float minDist;
        private int k = 30; // Maximum number of attempts before rejection
        private int retryLimit = 50000; // Global retry limit to prevent infinite loop
        private QuadTree quadtree;

        public PoissonDiskSampling(Grid noiseMap, float minDist)
        {
            this.noiseMap = noiseMap;
            this.minDist = minDist;
            quadtree = new QuadTree(0, 0, noiseMap.Resolution.X, noiseMap.Resolution.Y);
        }

        public List<Vector2> GeneratePoints()
        {
            List<Vector2> points = new List<Vector2>();
            List<Vector2> spawnPoints = new List<Vector2>();
            Random rand = new Random();
            Vector2 initialPoint = new Vector2((float)rand.NextDouble() * noiseMap.Resolution.X, (float)rand.NextDouble() * noiseMap.Resolution.Y);
            points.Add(initialPoint);
            spawnPoints.Add(initialPoint);
            quadtree.Insert(initialPoint);
            int retries = 0;

            while (spawnPoints.Count > 0 && retries < retryLimit)
            {
                int spawnIndex = rand.Next(spawnPoints.Count);
                Vector2 spawnCenter = spawnPoints[spawnIndex];
                bool accepted = false;

                int centerX = (int)spawnCenter.X;
                int centerY = (int)spawnCenter.Y;

                if (centerX < 0 || centerY < 0 || centerX >= noiseMap.Resolution.X || centerY >= noiseMap.Resolution.Y)
                {
                    spawnPoints.RemoveAt(spawnIndex);
                    continue;
                }

                float noiseValueCenter = noiseMap.Values[centerX, centerY];
                float adjustedMinDistCenter = minDist * (noiseValueCenter);

                if (adjustedMinDistCenter <= 0)
                {
                    Console.WriteLine($"AdjustedMinDistCenter too small: {adjustedMinDistCenter} for noise value {noiseValueCenter}");
                    spawnPoints.RemoveAt(spawnIndex);
                    continue;
                }

                for (int i = 0; i < k; i++)
                {
                    float angle = (float)(rand.NextDouble() * Math.PI * 2);
                    float radius = adjustedMinDistCenter + (adjustedMinDistCenter * (float)rand.NextDouble());
                    Vector2 candidate = spawnCenter + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

                    if (IsValid(candidate, adjustedMinDistCenter))
                    {
                        points.Add(candidate);
                        spawnPoints.Add(candidate);
                        quadtree.Insert(candidate);
                        accepted = true;
                        break;
                    }
                }

                if (!accepted)
                {
                    spawnPoints.RemoveAt(spawnIndex);
                }

                retries++;
                if (retries >= retryLimit)
                {
                    Console.WriteLine("Retry limit reached, stopping algorithm.");
                }
            }

            return points;
        }

        private bool IsValid(Vector2 candidate, float adjustedMinDist)
        {
            if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= noiseMap.Resolution.X || candidate.Y >= noiseMap.Resolution.Y)
                return false;

            List<Vector2> nearbyPoints = quadtree.QueryRange(candidate.X, candidate.Y, adjustedMinDist);
            foreach (var point in nearbyPoints)
            {
                if (Vector2.Distance(candidate, point) < adjustedMinDist)
                    return false;
            }

            return true;
        }
    }
}