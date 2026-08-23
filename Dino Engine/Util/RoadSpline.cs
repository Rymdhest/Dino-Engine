using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Terrain
{
    public class RoadSpline
    {
        public List<Vector3> ControlPoints { get; set; } = new List<Vector3>();
        public List<Vector3> BakedPoints { get; private set; } = new List<Vector3>();

        public Vector2 MinBounds { get; private set; }
        public Vector2 MaxBounds { get; private set; }

        public RoadSpline() { }

        public RoadSpline(IEnumerable<Vector3> points, int samplesPerSegment = 10)
        {
            ControlPoints.AddRange(points);
            Bake(samplesPerSegment);
        }

        /// <summary>
        /// Samples the spline control points into discrete 3D line segments and computes the 2D bounding box.
        /// </summary>
        public void Bake(int samplesPerSegment = 10)
        {
            BakedPoints.Clear();
            if (ControlPoints.Count < 2) return;

            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < ControlPoints.Count - 1; i++)
            {
                Vector3 p0 = i > 0 ? ControlPoints[i - 1] : ControlPoints[i];
                Vector3 p1 = ControlPoints[i];
                Vector3 p2 = ControlPoints[i + 1];
                Vector3 p3 = i + 2 < ControlPoints.Count ? ControlPoints[i + 2] : p2;

                for (int s = 0; s < samplesPerSegment; s++)
                {
                    if (s == 0 && BakedPoints.Count > 0) continue; // Skip duplicate segment joints

                    float t = (float)s / samplesPerSegment;
                    Vector3 sample = GetCatmullRomPosition(t, p0, p1, p2, p3);
                    BakedPoints.Add(sample);

                    minX = MathF.Min(minX, sample.X);
                    minZ = MathF.Min(minZ, sample.Z);
                    maxX = MathF.Max(maxX, sample.X);
                    maxZ = MathF.Max(maxZ, sample.Z);
                }
            }

            Vector3 lastPoint = ControlPoints[^1];
            BakedPoints.Add(lastPoint);
            minX = MathF.Min(minX, lastPoint.X);
            minZ = MathF.Min(minZ, lastPoint.Z);
            maxX = MathF.Max(maxX, lastPoint.X);
            maxZ = MathF.Max(maxZ, lastPoint.Z);

            MinBounds = new Vector2(minX, minZ);
            MaxBounds = new Vector2(maxX, maxZ);
        }

        /// <summary>
        /// Finds the closest point on the 3D spline relative to a 2D world position (X, Z).
        /// </summary>
        public Vector3 GetClosestPoint(Vector2 position, out float distance)
        {
            distance = float.MaxValue;
            Vector3 closest3DPoint = Vector3.Zero;

            if (BakedPoints.Count < 2) return closest3DPoint;

            // Fast AABB rejection with a buffer for road widths
            const float padding = 50f;
            if (position.X < MinBounds.X - padding || position.X > MaxBounds.X + padding ||
                position.Y < MinBounds.Y - padding || position.Y > MaxBounds.Y + padding)
            {
                return closest3DPoint;
            }

            float minSqDistance = float.MaxValue;

            for (int i = 0; i < BakedPoints.Count - 1; i++)
            {
                Vector3 a3D = BakedPoints[i];
                Vector3 b3D = BakedPoints[i + 1];

                Vector2 a2D = new Vector2(a3D.X, a3D.Z);
                Vector2 b2D = new Vector2(b3D.X, b3D.Z);

                Vector2 ab = b2D - a2D;
                Vector2 ap = position - a2D;

                float abLenSq = ab.LengthSquared;
                float t = 0f;

                if (abLenSq > 0.0001f)
                {
                    t = Vector2.Dot(ap, ab) / abLenSq;
                    t = MathHelper.Clamp(t, 0f, 1f);
                }

                Vector2 proj2D = a2D + t * ab;
                float sqDist = Vector2.DistanceSquared(position, proj2D);

                if (sqDist < minSqDistance)
                {
                    minSqDistance = sqDist;
                    closest3DPoint = Vector3.Lerp(a3D, b3D, t);
                }
            }

            distance = MathF.Sqrt(minSqDistance);
            return closest3DPoint;
        }

        public static Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
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
    }
}