using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public static class ContourRoadBuilder
    {
        public static RoadSpline CreateRoad(
            List<Vector2> waypoints2D,
            TerrainGenerator terrain,
            float sampleInterval = 4f,
            float maxGrade = 0.15f)
        {
            if (waypoints2D == null || waypoints2D.Count < 2) return null;

            List<Vector2> path2D = Sample2DCurveDensely(waypoints2D, sampleInterval);

            List<Vector3> path3D = new List<Vector3>();
            for (int i = 0; i < path2D.Count; i++)
            {
                Vector2 pos = path2D[i];
                float terrainY = terrain.getRawNoiseHeightAt(pos);
                path3D.Add(new Vector3(pos.X, terrainY, pos.Y));
            }

            EnforceMaxGrade(path3D, maxGrade);

            RoadSpline spline = new RoadSpline();
            spline.BakedPoints.AddRange(path3D);
            UpdateSplineBounds(spline);

            return spline;
        }

        private static List<Vector2> Sample2DCurveDensely(List<Vector2> waypoints, float interval)
        {
            List<Vector2> path = new List<Vector2>();

            List<Vector2> anchored = new List<Vector2> { waypoints[0] };
            anchored.AddRange(waypoints);
            anchored.Add(waypoints[^1]);

            for (int i = 1; i < anchored.Count - 2; i++)
            {
                Vector2 p0 = anchored[i - 1];
                Vector2 p1 = anchored[i];
                Vector2 p2 = anchored[i + 1];
                Vector2 p3 = anchored[i + 2];

                float dist = Vector2.Distance(p1, p2);
                int samples = Math.Max(2, (int)MathF.Ceiling(dist / interval));

                for (int s = 0; s < samples; s++)
                {
                    if (s == 0 && path.Count > 0) continue;

                    float t = (float)s / samples;
                    path.Add(GetCatmullRom2D(t, p0, p1, p2, p3));
                }
            }

            path.Add(waypoints[^1]);
            return path;
        }

        private static Vector2 GetCatmullRom2D(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private static void EnforceMaxGrade(List<Vector3> points, float maxGrade)
        {
            for (int i = 1; i < points.Count; i++)
            {
                float dist = Vector2.Distance(
                    new Vector2(points[i - 1].X, points[i - 1].Z),
                    new Vector2(points[i].X, points[i].Z));

                float maxChange = dist * maxGrade;
                float minY = points[i - 1].Y - maxChange;
                float maxY = points[i - 1].Y + maxChange;

                points[i] = new Vector3(points[i].X, MathHelper.Clamp(points[i].Y, minY, maxY), points[i].Z);
            }

            for (int i = points.Count - 2; i >= 0; i--)
            {
                float dist = Vector2.Distance(
                    new Vector2(points[i + 1].X, points[i + 1].Z),
                    new Vector2(points[i].X, points[i].Z));

                float maxChange = dist * maxGrade;
                float minY = points[i + 1].Y - maxChange;
                float maxY = points[i + 1].Y + maxChange;

                points[i] = new Vector3(points[i].X, MathHelper.Clamp(points[i].Y, minY, maxY), points[i].Z);
            }
        }

        private static void UpdateSplineBounds(RoadSpline spline)
        {
            if (spline.BakedPoints.Count == 0) return;
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            foreach (var pt in spline.BakedPoints)
            {
                minX = MathF.Min(minX, pt.X);
                minZ = MathF.Min(minZ, pt.Z);
                maxX = MathF.Max(maxX, pt.X);
                maxZ = MathF.Max(maxZ, pt.Z);
            }

            typeof(RoadSpline).GetProperty("MinBounds")?.SetValue(spline, new Vector2(minX, minZ));
            typeof(RoadSpline).GetProperty("MaxBounds")?.SetValue(spline, new Vector2(maxX, maxZ));
        }
    }
}