using OpenTK.Mathematics;

namespace Dino_Engine.World.Generation.Arenas
{
    /// <summary>
    /// A four-centred oval, as amphitheatres were laid out: two long arcs north and south about centres on the minor axis, and two short ones at the ends about centres on the major axis, tangent where
    /// they meet, so that every line offset from it is an oval about the same four centres. Its long axis runs east and west, as the camera sees an oval best that way. Positions are from its middle,
    /// bearings clockwise from north. Headless.
    /// </summary>
    internal readonly struct Oval
    {
        /// <summary>Half its length, east to west, and half its width, north to south, at no offset.</summary>
        public readonly float A, B;

        /// <summary>The radius of its ends, and of its long sides.</summary>
        public readonly float End, Side;

        /// <summary>Degrees from north, about a long side's centre, at which it hands over to an end.</summary>
        public readonly float Turn;

        public Oval(float a, float b, float end)
        {
            A = a;
            B = b;
            End = end;
            Side = ((a - end) * (a - end) + b * b - end * end) / (2f * (b - end));
            Turn = MathHelper.RadiansToDegrees(MathF.Atan2(a - end, Side - b));
        }

        public Vector2 NorthCentre => new Vector2(0f, -(Side - B));
        public Vector2 SouthCentre => new Vector2(0f, Side - B);
        public Vector2 EastCentre => new Vector2(A - End, 0f);
        public Vector2 WestCentre => new Vector2(-(A - End), 0f);

        /// <summary>Its four arcs at an offset out from it: each centre, radius, the bearing it starts at and the degrees it sweeps, clockwise round from the north.</summary>
        public (Vector2 Centre, float Radius, float From, float Sweep)[] Arcs(float offset) => new[]
        {
            (NorthCentre, Side + offset, -Turn, 2f * Turn),
            (EastCentre, End + offset, Turn, 180f - 2f * Turn),
            (SouthCentre, Side + offset, 180f - Turn, 2f * Turn),
            (WestCentre, End + offset, 180f + Turn, 180f - 2f * Turn),
        };

        /// <summary>How long it is all round, at an offset.</summary>
        public float Perimeter(float offset)
        {
            float total = 0f;
            foreach ((_, float radius, _, float sweep) in Arcs(offset)) total += MathHelper.DegreesToRadians(sweep) * radius;
            return total;
        }

        /// <summary>The point that far round it from due north, going clockwise, at an offset, and the way out from it there.</summary>
        public (Vector2 Point, Vector2 Outward) Along(float distance, float offset)
        {
            float perimeter = Perimeter(offset);
            distance = ((distance % perimeter) + perimeter) % perimeter;
            // From due north: half the north arc first, then round, and the other half last.
            (Vector2 Centre, float Radius, float From, float Sweep)[] arcs = Arcs(offset);
            float half = MathHelper.DegreesToRadians(arcs[0].Sweep * 0.5f) * arcs[0].Radius;
            float d = (distance + half) % perimeter;
            foreach ((Vector2 centre, float radius, float from, float sweep) in arcs)
            {
                float length = MathHelper.DegreesToRadians(sweep) * radius;
                if (d <= length)
                {
                    float bearing = from + MathHelper.RadiansToDegrees(d / radius);
                    Vector2 outward = new Vector2(MathF.Sin(MathHelper.DegreesToRadians(bearing)), MathF.Cos(MathHelper.DegreesToRadians(bearing)));
                    return (centre + outward * radius, outward);
                }
                d -= length;
            }
            return (new Vector2(0f, B + offset), Vector2.UnitY);
        }

        /// <summary>How far east and west of the middle it reaches at a distance north or south, at an offset; nothing past its ends.</summary>
        public float HalfWidthAt(float z, float offset)
        {
            float end = End + offset, side = Side + offset;
            float turnZ = end * MathF.Cos(MathHelper.DegreesToRadians(Turn));
            float az = MathF.Abs(z);
            if (az > B + offset) return 0f;
            if (az <= turnZ) return (A - End) + MathF.Sqrt(MathF.Max(end * end - az * az, 0f));
            float dz = az + (Side - B);
            return MathF.Sqrt(MathF.Max(side * side - dz * dz, 0f));
        }

        /// <summary>Points all round it, a step apart, starting due north and going clockwise.</summary>
        public IEnumerable<Vector2> Points(float offset, float step)
        {
            float perimeter = Perimeter(offset);
            int count = Math.Max(16, (int)MathF.Ceiling(perimeter / step));
            for (int i = 0; i < count; i++) yield return Along(perimeter * i / count, offset).Point;
        }
    }
}
