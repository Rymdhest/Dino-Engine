using Dino_Defenders.Progression;
using Dino_Engine.Modelling.Procedural.Architecture.Waygates;
using Dino_Engine.World;
using System.Globalization;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// What a server calls the way standing open in a gate (WAYGATES.md), as a model's name, which is all a client needs to grow it: which gate it fills, the land it goes to and a seed for
    /// how it moves. The Haven's names the land the map table opened it to, by the request that grows that land, so what's seen in it is that land; a land's own gates go home, and every one
    /// of them shows the Haven, each turning in a way of its own.
    /// </summary>
    public static class WayKeys
    {
        public const string PortalPrefix = "portal:";

        /// <summary>The way open in the Haven's great waygate, to <paramref name="destination"/>.</summary>
        public static string Portal(in WorldRequest destination) =>
            $"{PortalPrefix}{destination.Seed.ToString(CultureInfo.InvariantCulture)}:{destination.ArchetypeId ?? "-"}:{destination.Level.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>The way standing in one of a land's gates once it's lit, which goes home: <paramref name="seed"/> is the gate's own.</summary>
        public static string Lit(int seed) => $"{Roads.Lit}:{((uint)seed).ToString("x8", CultureInfo.InvariantCulture)}";

        /// <summary>A gate's own seed, from where it stands in which land, the same every time it's lit.</summary>
        public static int GateSeed(int landSeed, OpenTK.Mathematics.Vector3 at) =>
            unchecked(landSeed * 486187739 + (int)MathF.Round(at.X * 8f) * 73856093 + (int)MathF.Round(at.Z * 8f) * 19349663);

        public static bool IsWay(string key) => key.StartsWith(Roads.Lit, StringComparison.Ordinal) || key.StartsWith(PortalPrefix, StringComparison.Ordinal);

        /// <summary>What <paramref name="key"/> names: the gate it fills, the land it goes to, and the seed for how it moves.</summary>
        public static bool TryRead(string key, out WaygateDesign design, out WorldRequest destination, out ulong seed)
        {
            design = WaygateDesign.Land;
            destination = Lands.Haven;
            seed = 0;
            if (key.StartsWith(Roads.Lit, StringComparison.Ordinal))
            {
                string rest = key.Length > Roads.Lit.Length + 1 ? key.Substring(Roads.Lit.Length + 1) : "0";
                if (!uint.TryParse(rest, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint gate)) gate = 0;
                seed = gate * 0x9E3779B97F4A7C15UL + 0x51A7UL;
                return true;
            }
            if (!key.StartsWith(PortalPrefix, StringComparison.Ordinal)) return false;

            string[] parts = key.Substring(PortalPrefix.Length).Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int landSeed)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)) return false;
            string? archetype = parts[1] == "-" ? null : parts[1];
            design = WaygateDesign.Great;
            destination = new WorldRequest(landSeed, archetype, level);
            seed = (ulong)(uint)landSeed * 0x9E3779B97F4A7C15UL + (ulong)level * 0xBF58476D1CE4E5B9UL + 0x9047UL;
            return true;
        }
    }
}
