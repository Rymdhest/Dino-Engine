using Dino_Engine.Util.Data_Structures;
using Dino_Engine.Util.Data_Structures.Grids;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dino_Engine.Util.Noise
{
    public class BetterNoiseSampling
    {

        private float _minRadius;
        private BucketQuadTree _quadTree;
        private Vector2 _size;

        public BetterNoiseSampling(Vector2 size)
        {
            _minRadius = 1f;
            _size = size;
            _quadTree = new BucketQuadTree(0, 0, size.X, size.Y);
        }


        public List<Vector2> GeneratePoints(FloatGrid noiseMap)
        {

            List<Vector3> candidates = new List<Vector3>();
            List<Vector2> points = new List<Vector2>();

            for (int y = 0; y < noiseMap.Resolution.Y; y++)
            {
                for (int x = 0; x < noiseMap.Resolution.X; x++)
                {
                    float value = noiseMap.Values[x, y];
                    candidates.Add(new Vector3(x, value, y));
                }
            }
            candidates.Sort((a, b) => a.Y.CompareTo(b.Y));
            

            foreach(Vector3 v in candidates)
            {
                Vector2 p = v.Xz;
                if (IsValid(p, CalcAdjustedRadius(v.Y)))
                {
                    points.Add(p);
                    _quadTree.Insert(p);
                }
            }

            return points;
        }

        private float CalcAdjustedRadius(float value01)
        {
            //return _minRadius + value01 * (100f - _minRadius);
            //return _minRadius + MathF.Pow(value01, 4f)*300f;
            //Console.WriteLine(MathF.Pow(_minRadius + value01, 8f));
            return _minRadius+MathF.Pow(1f+value01+0.391f, 4.0190124839f);
        }

        private bool IsValid(Vector2 candidate, float minDist)
        {
            if (candidate.X < 0 || candidate.Y < 0 || candidate.X >= _size.X || candidate.Y >= _size.Y)
                return false;

            /*
            List<Vector2> nearbyPoints = _quadTree.QueryRange(candidate.X, candidate.Y, minDist);
            foreach (var point in nearbyPoints)
            {
                if (Vector2.Distance(candidate, point) < minDist)
                    return false;
            }
            */
            return !_quadTree.isPointInside(candidate.X, candidate.Y, minDist);
        }

    }
}
