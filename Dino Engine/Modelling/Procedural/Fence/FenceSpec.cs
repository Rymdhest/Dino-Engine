using System.Globalization;

namespace Dino_Engine.Modelling.Procedural.Fence
{
    /// <summary>Which part of a fence a model is.</summary>
    public enum FencePiece
    {
        Post,   // Between two spans of a straight run.
        End,    // Where a run stops, at a gate or a loose end, and where two runs meet at a corner: heavier, as it's what holds the rest.
        Span,   // From one post to the next, built for a slope.
    }

    /// <summary>
    /// Everything it takes to build one model of a fence, and its id, which is all that's kept: "fence:rails:1f3a9c:s1+3" is the second span of that style of rails, built for a
    /// rise of three steps. Ids have no dots and no hash, so that nothing reads one as a building's or a plant's.
    /// </summary>
    public readonly record struct FenceSpec(FenceKind Kind, ulong Style, FencePiece Piece, int Variant, int Slope)
    {
        private const string Prefix = "fence:";

        public string Id => Piece switch
        {
            FencePiece.Post => $"{Prefix}{Kind.Id}:{Style:x}:p{Variant}",
            FencePiece.End => $"{Prefix}{Kind.Id}:{Style:x}:e{Variant}",
            _ => $"{Prefix}{Kind.Id}:{Style:x}:s{Variant}{(Slope >= 0 ? "+" : "-")}{Math.Abs(Slope)}",
        };

        /// <summary>The gradient a span is built for.</summary>
        public float Gradient => Piece == FencePiece.Span ? Slope * Kind.Step : 0f;

        public static bool IsFence(string id) => id.StartsWith(Prefix, StringComparison.Ordinal);

        public static bool TryParse(string id, out FenceSpec spec)
        {
            spec = default;
            if (!IsFence(id)) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length != 3 || parts[2].Length < 2) return false;
            if (!FenceCatalog.TryGet(parts[0], out FenceKind kind)) return false;
            if (!ulong.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong style)) return false;

            string piece = parts[2];
            if (piece[0] == 's')
            {
                int sign = piece.IndexOfAny(new[] { '+', '-' });
                if (sign < 2) return false;
                if (!int.TryParse(piece.AsSpan(1, sign - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int span)) return false;
                if (!int.TryParse(piece.AsSpan(sign + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int steps)) return false;
                spec = new FenceSpec(kind, style, FencePiece.Span, span, piece[sign] == '-' ? -steps : steps);
                return true;
            }

            if (piece[0] != 'p' && piece[0] != 'e') return false;
            if (!int.TryParse(piece.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int variant)) return false;
            spec = new FenceSpec(kind, style, piece[0] == 'p' ? FencePiece.Post : FencePiece.End, variant, 0);
            return true;
        }
    }
}
