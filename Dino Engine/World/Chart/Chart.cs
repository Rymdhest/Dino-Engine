using OpenTK.Mathematics;

namespace Dino_Engine.World.Chart
{
    /// <summary>
    /// The square of a world a chart shows, turned the way the land is seen in play: +x runs to the left of the picture and +z up it, so the top of a chart is what's ahead
    /// of the camera. A chart only shows what's around where anyone can go, as the mountains round most lands would otherwise be most of the picture.
    /// </summary>
    public readonly struct ChartFrame
    {
        /// <summary>The corner of the square with the least x and z.</summary>
        public readonly Vector2 Origin;
        /// <summary>Metres the square is across.</summary>
        public readonly float Span;
        public readonly int Resolution;

        public ChartFrame(Vector2 origin, float span, int resolution)
        {
            Origin = origin;
            Span = span;
            Resolution = resolution;
        }

        public float PixelsPerMetre => Resolution / Span;

        /// <summary>Where a point of the world is on the chart, as shares of it across from the left and down from the top.</summary>
        public Vector2 ToShare(Vector2 world) => new Vector2(1f - (world.X - Origin.X) / Span, 1f - (world.Y - Origin.Y) / Span);

        /// <summary>Where a point of the world is on the chart, in its pixels.</summary>
        public Vector2 ToPixel(Vector2 world) => ToShare(world) * Resolution;

        /// <summary>The point of the world under a pixel's position (its centre is at half a pixel).</summary>
        public Vector2 ToWorld(Vector2 pixel) => new Vector2(Origin.X + Span * (1f - pixel.X / Resolution), Origin.Y + Span * (1f - pixel.Y / Resolution));

        /// <summary>The frame of the same square at another resolution.</summary>
        public ChartFrame At(int resolution) => new ChartFrame(Origin, Span, resolution);
    }

    /// <summary>A name written across a stretch of the chart, as a region's is: where its middle is, as shares of the chart, and how much room it has there.</summary>
    public readonly record struct ChartLabel(string Text, Vector2 Share, float Room);

    /// <summary>
    /// A land drawn as an old map, read CHART.md: <see cref="Colour"/> is the map as it's known, in ink and watercolour, and <see cref="Sketch"/> the same drawing before it's been
    /// coloured, which is what stands where nobody has been yet (<see cref="ChartFog"/>). Both are RGBA rows, top row first, in the frame's orientation.
    /// </summary>
    public sealed class Chart
    {
        public readonly ChartFrame Frame;
        public readonly byte[] Colour;
        public readonly byte[] Sketch;
        public readonly IReadOnlyList<ChartLabel> Labels;

        public Chart(ChartFrame frame, byte[] colour, byte[] sketch, IReadOnlyList<ChartLabel> labels)
        {
            Frame = frame;
            Colour = colour;
            Sketch = sketch;
            Labels = labels;
        }

        public int Resolution => Frame.Resolution;
    }
}
