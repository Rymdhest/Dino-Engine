using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Util
{

    public class CurvePoint
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector3 tangent;
        public Vector3 biTangent;
        public float width;
        public float traversed;
        public Quaternion rotation;

        public CurvePoint(Vector3 point, float width, float traversed)
        {
            this.pos = point;
            this.width = width;
            this.traversed = traversed;
        }
    }

    public class Curve3D
    {
        public float totalTraveresed;

        public List<CurvePoint> curvePoints;

        public Curve3D(List<Vector3> points, float width = 1.0f)
        {
            curvePoints = new List<CurvePoint>(points.Count);
            float traversed = 0f;
            for (int i = 0; i<points.Count; i++)
            {
                if (i > 0) traversed += Vector3.Distance(points[i], points[i-1]);
                curvePoints.Add(new CurvePoint(points[i], width, traversed));
            }
            totalTraveresed = traversed;



            bool flatStart = true;


            // Initialize the first frame (tangent, normal, bitangent)
            Vector3 tangent = Vector3.Normalize(curvePoints[1].pos - curvePoints[0].pos); // First tangent
            Vector3 normal = Vector3.Cross(tangent, Vector3.UnitY);   // Arbitrary initial normal
            if (!(normal.Length > 0.0f)) normal = Vector3.Cross(tangent, Vector3.UnitX); // Safe fallback
            normal = Vector3.Normalize(normal);

            Vector3 bitangent = Vector3.Cross(tangent, normal); // Initial bitangent

            Quaternion totalRotation = Quaternion.Identity;

            for (int i = 0; i < curvePoints.Count; i++)
            {
                // Update tangent for the current ring
                Vector3 newTangent = i == curvePoints.Count - 1
                ? Vector3.Normalize(curvePoints[i].pos - curvePoints[i - 1].pos) // Use backward difference for the last point
                    : Vector3.Normalize(curvePoints[i + 1].pos - curvePoints[i].pos); // Forward difference otherwise
                if (flatStart && i == 0) newTangent = Vector3.UnitY;
                // Compute rotation quaternion to align the old tangent with the new tangent
                if (Vector3.Dot(tangent, newTangent) < 0.999999f) // Only rotate if tangents are not nearly the same
                {
                    Vector3 axis = Vector3.Cross(tangent, newTangent);
                    axis = Vector3.Normalize(axis);
                    float angle = MathF.Acos(Vector3.Dot(tangent, newTangent));

                    Quaternion rotation = Quaternion.FromAxisAngle(axis, angle);
                    totalRotation *= rotation;
                    curvePoints[i].rotation = totalRotation;

                    // Rotate normal and bitangent
                    normal = Vector3.Transform(normal, rotation);
                    bitangent = Vector3.Transform(bitangent, rotation);
                }
                tangent = newTangent; // Update tangent for next iteration

                curvePoints[i].normal = normal;
                curvePoints[i].tangent = tangent;
                curvePoints[i].biTangent = bitangent;
            }
        }

        /*
         * Where t is between 0-1
         */
        public CurvePoint getPointAt(float t)
        {
            float targetTraversed = t * totalTraveresed;

            CurvePoint? p0 = curvePoints[0];
            CurvePoint? p1 = curvePoints[1];
            for (int i = 1; i<curvePoints.Count; i++)
            {
                if (curvePoints[i].traversed >= targetTraversed)
                {
                    i--;
                    p0 = curvePoints[i];

                    if (i >= curvePoints.Count-1) p1 = curvePoints[i];
                    else p1 = curvePoints[i+1];

                    break;
                }
            }

            float deltaTraverse = (p1.traversed-p0.traversed);
            float interpolateValue = 0f;
            if (deltaTraverse > 0f) interpolateValue = (targetTraversed - p0.traversed) / deltaTraverse;


            Vector3 pos = MyMath.lerp(p0.pos, p1.pos, interpolateValue);
            Vector3 normal = MyMath.lerp(p0.normal, p1.normal, interpolateValue);
            Vector3 tangent = MyMath.lerp(p0.tangent, p1.tangent, interpolateValue);
            Vector3 biTangent = MyMath.lerp(p0.biTangent, p1.biTangent, interpolateValue);
            float width = MyMath.lerp(p0.width, p1.width, interpolateValue);
            float traversed = MyMath.lerp(p0.traversed, p1.traversed, interpolateValue); ;
            Quaternion rotation = Quaternion.Slerp(p0.rotation, p1.rotation, interpolateValue);

            CurvePoint result = new CurvePoint(pos, width, traversed);
            result.traversed = traversed;
            result.normal = normal;
            result.tangent = tangent;
            result.biTangent = biTangent;
            result.rotation = rotation;

            return result;
        }

        public Curve3D(List<Vector4> points, float width = 1.0f)
        {
            curvePoints = new List<CurvePoint>(points.Count);
            float traversed = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) traversed += Vector3.Distance(points[i].Xyz, points[i - 1].Xyz);
                curvePoints.Add(new CurvePoint(points[i].Xyz, points[i].W, traversed));
            }
            totalTraveresed = traversed;
        }

        public void LERPWidth(float from, float to)
        {
            foreach(CurvePoint curvePoint in curvePoints)
            {
                curvePoint.width = MyMath.lerp(from, to, curvePoint.traversed/totalTraveresed);
            }
        }
    }
}
