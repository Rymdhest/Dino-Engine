using Dino_Engine.Util;
using OpenTK.Mathematics;

public class CardinalSpline3D
{
    private List<Vector3> controlPoints;
    private float scale;

    public CardinalSpline3D(List<Vector3> points, float scale)
    {
        if (points == null || points.Count < 2)
        {
            throw new ArgumentException("At least two control points are required.");
        }

        controlPoints = points;
        this.scale = Math.Clamp(scale, 0f, 1f); // Ensure scale is between 0 and 1
    }

    public Vector3 Interpolate(float t, int segmentIndex)
    {
        if (segmentIndex < 0 || segmentIndex >= controlPoints.Count - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), "Invalid segment index.");
        }

        // Get the four points for the Cardinal Spline formula
        Vector3 p0 = controlPoints[Math.Max(segmentIndex - 1, 0)];
        Vector3 p1 = controlPoints[segmentIndex];
        Vector3 p2 = controlPoints[Math.Min(segmentIndex + 1, controlPoints.Count - 1)];
        Vector3 p3 = controlPoints[Math.Min(segmentIndex + 2, controlPoints.Count - 1)];

        float t2 = t * t;
        float t3 = t2 * t;

        // Cardinal spline blending functions
        float s = (1 - scale) / 2;

        float b0 = -s * t3 + 2 * s * t2 - s * t;
        float b1 = (2 - s) * t3 + (s - 3) * t2 + 1;
        float b2 = (s - 2) * t3 + (3 - 2 * s) * t2 + s * t;
        float b3 = s * t3 - s * t2;

        // Calculate the interpolated point
        return b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3;
    }

    public Curve3D GenerateCurve(int segmentsPerSegment)
    {
        if (segmentsPerSegment < 1)
        {
            throw new ArgumentException("Segments per segment must be at least 1.");
        }

        var result = new List<Vector3>();

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            for (int j = 0; j < segmentsPerSegment; j++)
            {
                float t = j / (float)segmentsPerSegment;
                result.Add(Interpolate(t, i));
            }
        }

        // Add the last control point to complete the curve
        result.Add(controlPoints[^1]);

        return new Curve3D(result);
    }
}
