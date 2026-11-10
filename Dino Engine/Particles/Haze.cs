using OpenTK.Mathematics;

namespace Dino_Engine.Particles
{
    /// <summary>
    /// How much smoke hangs over each patch of ground, so that it can never pile up until nothing under it can be seen. A person found a few spells cast into one place
    /// covered the screen in smoke: each was fine alone, and they added up. Every puff of smoke is counted where it's born, as the ground its disc would hide halfway
    /// through its life, times how solid it is then, and that wears off as smoke does (<see cref="Settles"/>). A puff born where its patch is already as hazy as
    /// <see cref="MostCover"/> allows comes out fainter by as much as there's no room for, or not at all. Patches are counted apart, so a fight spread over the land
    /// has all the smoke it ever had, and only one place spammed is thinned.
    /// </summary>
    public static class Haze
    {
        public const float Cell = 8f;           // Metres on a side of a patch.
        public const float MostCover = 0.25f;   // Share of a patch's ground that smoke may hide.
        public const float Settles = 3.5f;      // Seconds for what's counted to fall to a third, about as long as a spell's smoke lasts, or it outlives the count.
        private const float Least = 0.2f;       // A puff that would be fainter than this is left out: a faint ball reads as a stain.
        private const int MostCells = 1024;

        private static readonly Dictionary<long, (float Cover, double At)> _cells = new();

        /// <summary>
        /// Counts a puff of <paramref name="style"/> <paramref name="size"/> metres across and as strong as <paramref name="strength"/>, born at
        /// <paramref name="at"/> at <paramref name="now"/>, and says how much of it there's room for: 1 for all of it, 0 for none.
        /// </summary>
        public static float Admit(ParticleStyle style, Vector3 at, float size, float strength, double now)
        {
            float across = size * style.Size[0.5f];
            float cover = MathF.PI * 0.25f * across * across * style.Opacity[0.5f] * strength;
            if (!(cover > 0f)) return 1f;

            long key = ((long)(int)MathF.Floor(at.X / Cell) << 32) ^ (uint)(int)MathF.Floor(at.Z / Cell);
            float there = _cells.TryGetValue(key, out var cell) ? cell.Cover * MathF.Exp(-(float)(now - cell.At) / Settles) : 0f;
            float room = MostCover * Cell * Cell - there;
            float kept = Math.Clamp(room / cover, 0f, 1f);
            if (kept < Least) kept = 0f;

            if (_cells.Count >= MostCells && !_cells.ContainsKey(key)) Forget(now);
            _cells[key] = (there + cover * kept, now);
            return kept;
        }

        /// <summary>Forgets every patch, as when a world is left behind.</summary>
        public static void Clear() => _cells.Clear();

        // Lets go of every patch whose smoke has all but gone.
        private static void Forget(double now)
        {
            var gone = new List<long>();
            foreach (var (key, cell) in _cells)
            {
                if (cell.Cover * MathF.Exp(-(float)(now - cell.At) / Settles) < 1f) gone.Add(key);
            }
            foreach (long key in gone) _cells.Remove(key);
        }
    }
}
