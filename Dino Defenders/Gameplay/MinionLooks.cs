using Dino_Defenders.Progression;
using System.Globalization;

namespace Dino_Defenders.Gameplay
{
    /// <summary>
    /// What a Minion is drawn as when it's holding what its Summon Page gave it: a skeleton with that weapon in its hand and that shield or second weapon in the other, the same meshes as anywhere else
    /// (GEAR.md). Nothing about a look is sent but a seed and its traits, so a model's name carries those of both hands, and a client grows the body from it. Only a skeleton is drawn holding anything.
    /// </summary>
    public static class MinionLooks
    {
        public const string Prefix = "risen:";

        /// <summary>A turret's weapon, wielded in the air over it by nobody that can be seen: what it was given, or a plain bow if nothing.</summary>
        public const string HoverPrefix = "hover:";

        /// <summary>What a turret's stand, which never moves, is drawn as.</summary>
        public const string Stand = "turret";

        /// <summary>The model a Minion of this body is drawn as, holding these: its body's own if it holds nothing, or can't be drawn holding anything.</summary>
        public static string Of(string body, Item? weapon, Item? offHand)
        {
            bool hovers = body == Stand;
            if (!hovers && (body != "skeleton" || weapon == null && offHand == null)) return body;

            // A weapon alone in the other hand is held in the main one, as it is on a hero.
            if (weapon == null && offHand is { IsOneHandedWeapon: true })
            {
                weapon = offHand;
                offHand = null;
            }
            return $"{(hovers ? HoverPrefix : Prefix)}{Part(weapon)}:{Part(offHand)}";
        }

        private static string Part(Item? item) => item == null ? "0-0" : $"{item.LookSeed().ToString(CultureInfo.InvariantCulture)}-{ItemLook.Traits(item).ToString(CultureInfo.InvariantCulture)}";

        /// <summary>Whether a model of this name is a turret's weapon, with no body to it.</summary>
        public static bool Hovers(string key) => key.StartsWith(HoverPrefix, StringComparison.Ordinal);

        /// <summary>What's in each hand of a model of this name, as a seed and traits each, if it's one of these.</summary>
        public static bool TryParse(string key, out (ulong Seed, uint Traits)? weapon, out (ulong Seed, uint Traits)? offHand)
        {
            weapon = offHand = null;
            string? prefix = key.StartsWith(Prefix, StringComparison.Ordinal) ? Prefix : key.StartsWith(HoverPrefix, StringComparison.Ordinal) ? HoverPrefix : null;
            if (prefix == null) return false;
            string[] hands = key.Substring(prefix.Length).Split(':');
            if (hands.Length != 2 || !Read(hands[0], out weapon) || !Read(hands[1], out offHand)) return false;
            return true;
        }

        private static bool Read(string part, out (ulong, uint)? held)
        {
            held = null;
            string[] numbers = part.Split('-');
            if (numbers.Length != 2 || !ulong.TryParse(numbers[0], NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) || !uint.TryParse(numbers[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint traits)) return false;
            if (seed != 0) held = (seed, traits);
            return true;
        }
    }
}
