namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What a hero has done, kept for the Archive in the Haven, where each record is a display of its own (HALLS.md): the foes and tyrants they've brought down, their hardest blow,
    /// how often they've fallen, the swiftest they've freed a land, the gold they've picked up and every unique they've found. The server keeps it as things happen and it goes to
    /// the client with the rest of the hero (<see cref="Net.Protocol.WriteHeroSummary"/>), which is what ends up in their save. Foes fall, and heroes with them, only in a land;
    /// the hardest blow counts any foe, the training ground's dummies among them; a unique counts when it's picked up off the ground, and never when a vendor hands it over.
    /// </summary>
    public sealed class HeroRecords
    {
        /// <summary>Foes this hero or what fights for them brought down.</summary>
        public long Slain { get; set; }

        /// <summary>Of those, how many were a land's tyrant.</summary>
        public int TyrantsSlain { get; set; }

        /// <summary>The most a single blow of theirs has ever done to a foe, after everything that stood in its way.</summary>
        public float HardestBlow { get; set; }

        /// <summary>How many times they've fallen in a land.</summary>
        public int Falls { get; set; }

        /// <summary>The fewest seconds from arriving in a land to freeing it, or 0 if they've freed none in one go.</summary>
        public float SwiftestLand { get; set; }

        /// <summary>The level of the land that was.</summary>
        public int SwiftestLevel { get; set; }

        /// <summary>Every coin they've picked up.</summary>
        public long GoldGathered { get; set; }

        /// <summary>The ids of the uniques they've found, each once.</summary>
        public List<string> UniquesFound { get; set; } = new();

        // What changes these: methods, so that they aren't saved.

        public void Struck(float damage)
        {
            if (float.IsFinite(damage) && damage > HardestBlow) HardestBlow = damage;
        }

        public void Freed(float seconds, int level)
        {
            if (!float.IsFinite(seconds) || seconds <= 0f) return;
            if (SwiftestLand > 0f && seconds >= SwiftestLand) return;
            SwiftestLand = seconds;
            SwiftestLevel = level;
        }

        /// <summary>A unique has been found. Says whether it's one they hadn't found before.</summary>
        public bool Found(string unique)
        {
            if (UniquesFound.Contains(unique)) return false;
            UniquesFound.Add(unique);
            return true;
        }

        /// <summary>Brings whatever a save or a stranger sent into line: nothing below nothing, nothing that isn't a number, no unique twice.</summary>
        public void Sanitize()
        {
            Slain = Math.Max(Slain, 0);
            TyrantsSlain = Math.Max(TyrantsSlain, 0);
            HardestBlow = float.IsFinite(HardestBlow) ? Math.Max(HardestBlow, 0f) : 0f;
            Falls = Math.Max(Falls, 0);
            SwiftestLand = float.IsFinite(SwiftestLand) ? Math.Max(SwiftestLand, 0f) : 0f;
            SwiftestLevel = Math.Max(SwiftestLevel, 0);
            GoldGathered = Math.Max(GoldGathered, 0);
            UniquesFound = (UniquesFound ?? new List<string>()).Where(id => UniqueCatalog.Find(id) != null).Distinct().ToList();
        }
    }
}
