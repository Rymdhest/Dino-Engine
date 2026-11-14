using Dino_Engine.World;
using Dino_Engine.World.Chart;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Where this player has been in the last few lands, for the map (read CHART.md). It's this machine's alone, never sent or saved, and kept across a trip home so that
    /// coming back into a land finds the map as it was left. The Haven is home, and known from the first step.
    /// </summary>
    internal static class Discoveries
    {
        private const int Kept = 6;
        private static readonly List<(WorldRequest Request, ChartDiscovery Seen)> _lands = new();

        public static ChartDiscovery For(WorldRequest request, WorldData world, bool home)
        {
            int index = _lands.FindIndex(land => land.Request.Equals(request));
            ChartDiscovery seen;
            if (index >= 0)
            {
                seen = _lands[index].Seen;
                _lands.RemoveAt(index);
            }
            else
            {
                seen = new ChartDiscovery(world.Size);
                if (home) seen.RevealAll();
            }
            _lands.Add((request, seen));
            if (_lands.Count > Kept) _lands.RemoveAt(0);
            // Whatever reads it next draws all of it, as a new map has never drawn any.
            seen.Touch();
            return seen;
        }
    }
}
