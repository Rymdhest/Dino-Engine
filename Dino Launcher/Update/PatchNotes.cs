using System;
using System.Collections.Generic;
using System.Linq;

namespace Dino_Launcher.Update
{
    /// <summary>One release's changes, a line each, with nothing left in them of how they were written down.</summary>
    public sealed class NoteSection
    {
        public string Version = "";
        public string[] Changes = new string[0];
    }

    /// <summary>
    /// What's in a manifest's notes, in the shape a person reads them. A release with nothing to say isn't shown, and a search keeps the
    /// lines that have every word in them, so that the more someone types the less they have to read.
    /// </summary>
    public static class PatchNotes
    {
        private static readonly string[] Marks = { "- ", "* ", "• " };

        public static NoteSection[] Read(ReleaseNote[]? notes)
        {
            var sections = new List<NoteSection>();
            foreach (ReleaseNote note in notes ?? new ReleaseNote[0])
            {
                if (note == null) continue;
                string[] changes = (note.Text ?? "").Split('\n').Select(Plain).Where(line => line.Length > 0).ToArray();
                if (changes.Length > 0) sections.Add(new NoteSection { Version = note.Version ?? "", Changes = changes });
            }
            return sections.ToArray();
        }

        private static string Plain(string line)
        {
            line = line.Trim();
            foreach (string mark in Marks)
            {
                if (line.StartsWith(mark, StringComparison.Ordinal)) return line.Substring(mark.Length).Trim();
            }
            return line;
        }

        /// <summary>A version counts as a word on each of its lines, so "0.1.7" finds that release and "0.1.7 fixed" what it fixed.</summary>
        public static NoteSection[] Matching(NoteSection[] sections, string search)
        {
            string[] words = (search ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return sections;

            var found = new List<NoteSection>();
            foreach (NoteSection section in sections)
            {
                string[] changes = section.Changes.Where(line => words.All(word => Has(line, word) || Has(section.Version, word))).ToArray();
                if (changes.Length > 0) found.Add(new NoteSection { Version = section.Version, Changes = changes });
            }
            return found.ToArray();
        }

        private static bool Has(string text, string word) => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
