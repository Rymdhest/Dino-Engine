using Dino_Engine.Core;
using Dino_Engine.Debug;
using Dino_Engine.Rendering;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Dino_Engine.Util.Data_Structures
{
    internal class BucketQuadTree
    {
        private readonly int maxCapacity;
        private readonly float minX, minY, maxX, maxY;
        private readonly List<Vector2> points;
        private BucketQuadTree[] children;

        public BucketQuadTree(float minX, float minY, float maxX, float maxY, int maxCapacity = 1)
        {
            this.minX = minX;
            this.minY = minY;
            this.maxX = maxX;
            this.maxY = maxY;
            this.maxCapacity = maxCapacity;
            points = new List<Vector2>();
            children = null;

            
            RenderEngine._debugRenderer.lines.Add(new Line(new Vector2(minX, minY), new Vector2(minX, maxY), 1f));
            RenderEngine._debugRenderer.lines.Add(new Line(new Vector2(minX, maxY), new Vector2(maxX, maxY), 1f));
            RenderEngine._debugRenderer.lines.Add(new Line(new Vector2(maxX, maxY), new Vector2(maxX, minY), 1f));
            RenderEngine._debugRenderer.lines.Add(new Line(new Vector2(maxX, minY), new Vector2(minX, minY), 1f));

            
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

        public bool isPointInside(float x, float y, float range)
        {

            if (!IntersectsRange(x, y, range)) return false;

            foreach (var point in points)
            {
                if (Vector2.Distance(point, new Vector2(x, y)) <= range)
                    return true;
            }
            if (children != null)
            {
                foreach (var child in children)
                {
                    if (child.isPointInside(x, y, range)) return true;
                }
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

            children = new BucketQuadTree[4];
            children[0] = new BucketQuadTree(minX, minY, midX, midY, maxCapacity);
            children[1] = new BucketQuadTree(midX, minY, maxX, midY, maxCapacity);
            children[2] = new BucketQuadTree(minX, midY, midX, maxY, maxCapacity);
            children[3] = new BucketQuadTree(midX, midY, maxX, maxY, maxCapacity);

        }
    }
}