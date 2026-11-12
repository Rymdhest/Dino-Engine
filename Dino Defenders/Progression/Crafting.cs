using Dino_Engine.World;
using Dino_Engine.World.Content;

namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What can be done at the Forge's crafting bench, as rules both ends run: a client shows a craft at once and the server has the last word, through <c>AskGear</c>, like anything else that changes
    /// what's carried (NETWORKING.md). For now there's one craft, fusing: two Magic things of the same sort become one Rare, the first's base with its own lines and the second's, for gold. The second
    /// is used up. Two of the same line both stay, as two lines. Everything worked on is in the pack, and what's made goes in the first one's cell, so nothing else moves (HALLS.md).
    /// </summary>
    public static class Crafting
    {
        /// <summary>Gold a fuse costs for each level of what's made, which is the first one's level.</summary>
        public const int FuseGoldPerLevel = 25;

        /// <summary>What fusing these costs.</summary>
        public static int FuseCost(Item first) => FuseGoldPerLevel * Math.Max(first.Level, 1);

        /// <summary>Whether something can go into a fuse at all, whatever it's fused with.</summary>
        public static string? WhyNotFusing(Item item)
        {
            if (item.Rarity != Rarity.Magic) return "Only Magic things can be fused";
            if (item.IsOrders) return "Orders can't be fused";
            return null;
        }

        /// <summary>Why these two can't be fused, the first keeping its base, or nothing if they can.</summary>
        public static string? WhyNot(Item first, Item second)
        {
            if (ReferenceEquals(first, second)) return "Something can't be fused with itself";
            if (WhyNotFusing(first) is string notFirst) return notFirst;
            if (WhyNotFusing(second) is string notSecond) return notSecond;
            if (first.Slot != second.Slot) return "Only things of the same sort can be fused";
            if (first.IsWeapon && first.Kind != second.Kind) return "Only weapons of the same kind can be fused";
            if (first.IsPage && first.Page?.Kind != second.Page?.Kind) return "Only Pages of the same kind can be fused";
            if (second.Held?.Any(held => held != null) == true) return second.IsBook ? "Take the Pages out of the second Book first" : "Take everything off the second Page first";
            foreach (ItemLine line in second.Lines)
            {
                if (!Fits(line, first)) return "The second has a line the first can't have";
            }
            if (Sides(first) != (1, 1) || Sides(second) != (1, 1)) return "Each needs a prefix and a suffix";
            return null;
        }

        // Whether a line could be rolled on this.
        private static bool Fits(ItemLine line, Item item) => item.IsFolio
            ? FolioAffixes.Find(line.Affix) is FolioAffix folio && folio.Fits(item)
            : AffixCatalog.Find(line.Affix) is Affix affix && affix.Fits(item.Slot, item.Kind);

        private static bool IsPrefix(ItemLine line, Item item) => item.IsFolio ? FolioAffixes.Find(line.Affix)?.Prefix == true : AffixCatalog.Find(line.Affix)?.Prefix == true;

        private static (int prefixes, int suffixes) Sides(Item item)
        {
            int prefixes = item.Lines.Count(line => IsPrefix(line, item));
            return (prefixes, item.Lines.Count - prefixes);
        }

        /// <summary>
        /// What fusing these makes, which changes neither: the first, Rare, with its prefix and the second's, then its suffix and the second's, so a card reads the same way round as anything rolled.
        /// It looks as the first did, and a name someone gave the first stays. What's in a book or a page stays in it, and anything it now has room for is empty.
        /// </summary>
        public static Item Fused(Item first, Item second)
        {
            var lines = new List<ItemLine>(4);
            foreach (bool prefix in new[] { true, false })
            {
                lines.AddRange(first.Lines.Where(line => IsPrefix(line, first) == prefix).Select(line => new ItemLine(line.Affix, line.Value)));
                lines.AddRange(second.Lines.Where(line => IsPrefix(line, second) == prefix).Select(line => new ItemLine(line.Affix, line.Value)));
            }

            var made = new Item
            {
                Name = first.Name,
                Slot = first.Slot,
                Rarity = Rarity.Rare,
                Level = first.Level,
                WeaponDamage = first.WeaponDamage,
                Kind = first.Kind,
                Armour = first.Armour,
                Evasion = first.Evasion,
                Barrier = first.Barrier,
                Lines = lines,
                Seed = first.LookSeed(),
                Base = first.Base,
                Page = first.Page,
                Sentence = first.Sentence,
                Mode = first.Mode,
                When = first.When,
                Held = first.Held,
            };
            if (first.IsBook || first.IsPage)
            {
                var held = new Item?[FolioAffixes.HeldOf(made)];
                if (first.Held != null) Array.Copy(first.Held, held, Math.Min(held.Length, first.Held.Length));
                made.Held = held;
            }
            if (first.Name == ItemGenerator.RolledName(first)) made.Name = ItemGenerator.RolledName(made);
            return made;
        }

        /// <summary>Why the hero can't fuse what's in these two cells of their pack now, or nothing if they can.</summary>
        public static string? WhyNot(HeroState hero, int firstCell, int secondCell)
        {
            if (!Halls.IsOpen(hero, HallKind.Forge)) return Halls.Shut(HallKind.Forge);
            if (!ItemPlace.Cell(firstCell).IsCell || !ItemPlace.Cell(secondCell).IsCell || firstCell == secondCell) return "Put two things from your pack on the bench";
            if (hero.Inventory[firstCell] is not Item first || hero.Inventory[secondCell] is not Item second) return "Put two things from your pack on the bench";
            if (WhyNot(first, second) is string why) return why;
            return hero.Gold < FuseCost(first) ? "You can't afford that" : null;
        }

        /// <summary>Fuses what's in these two cells of the hero's pack, if it can be: what's made goes in the first's cell, and the second's is left empty. Says whether it was done.</summary>
        public static bool Fuse(HeroState hero, int firstCell, int secondCell)
        {
            if (WhyNot(hero, firstCell, secondCell) != null) return false;
            Item first = hero.Inventory[firstCell]!;
            hero.Gold -= FuseCost(first);
            hero.Inventory[firstCell] = Fused(first, hero.Inventory[secondCell]!);
            hero.Inventory[secondCell] = null;
            return true;
        }
    }
}
