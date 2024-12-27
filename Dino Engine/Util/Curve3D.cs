using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Util
{

    public class CurvePoint
    {
        public Vector3 pos;
        public float width;
        public float traversed;

        public CurvePoint(Vector3 point, float width, float traversed)
        {
            this.pos = point;
            this.width = width;
            this.traversed = traversed;
        }
    }

    public class Curve3D
    {
        public float totalLenght;

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
            totalLenght = traversed;
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
            totalLenght = traversed;
        }

        public void LERPWidth(float from, float to)
        {
            foreach(CurvePoint curvePoint in curvePoints)
            {
                curvePoint.width = MyMath.lerp(from, to, curvePoint.traversed/totalLenght);
            }
        }
    }
}
