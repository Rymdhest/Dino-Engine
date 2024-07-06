using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.Util.Data_Structures
{
    internal class QuadTree
    {
        private readonly int maxCapacity;
        private readonly float minX, minY, maxX, maxY;
        private readonly List<Vector2> points;
        private QuadTree[] children;

        public QuadTree(float minX, float minY, float maxX, float maxY, int maxCapacity = 4)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
            this.maxCapacity = maxCapacity;
            points = new List<Vector2>();
            children = null;
        }

        public bool Insert(Vector2 point)
        {
            if (!Contains(point))
                return false;

            if (points.Count < maxCapacity)
            {
                points.Add(point);
                return true;
            }

            if (children == null)
            {
                Subdivide();

                // Redistribute points to children after subdivision
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    var pt = points[i];
                    foreach (var child in children)
                    {
                        if (child.Insert(pt))
                        {
                            points.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            foreach (var child in children)
            {
                if (child.Insert(point))
                    return true;
            }

            return false;
        }

        public List<Vector2> QueryRange(float x, float y, float range)
        {
            List<Vector2> result = new List<Vector2>();

            if (!IntersectsRange(x, y, range))
                return result;

            foreach (var point in points)
            {
                if (Vector2.Distance(point, new Vector2(x, y)) <= range)
                    result.Add(point);
            }

            if (children != null)
            {
                foreach (var child in children)
                {
                    result.AddRange(child.QueryRange(x, y, range));
                }
            }

            return result;
        }

        private bool Contains(Vector2 point)
        {
            // Ensure that points exactly on the boundary are correctly handled
            return point.X >= minX && point.X < maxX && point.Y >= minY && point.Y < maxY;
        }

        private bool IntersectsRange(float x, float y, float range)
        {
            // Corrected range intersection logic to handle boundary cases
            return !(x + range < minX || x - range > maxX || y + range < minY || y - range > maxY);
        }

        private void Subdivide()
        {
            float midX = (minX + maxX) / 2;
            float midY = (minY + maxY) / 2;

            children = new QuadTree[4];
            children[0] = new QuadTree(minX, minY, midX, midY, maxCapacity);
            children[1] = new QuadTree(midX, minY, maxX, midY, maxCapacity);
            children[2] = new QuadTree(minX, midY, midX, maxY, maxCapacity);
            children[3] = new QuadTree(midX, midY, maxX, maxY, maxCapacity);

        }
    }
}