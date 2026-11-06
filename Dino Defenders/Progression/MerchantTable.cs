namespace Dino_Defenders.Progression
{
    /// <summary>
    /// What a merchant has out for one hero. Both ends of a connection run these same rules, as a client shows what it's asked for at once. Nothing on the table moves either: what's bought
    /// leaves a gap, so a second click can't buy the next thing along. What a hero sells goes on it too, for them to buy back at what they were paid, so that a slip of the hand costs nothing.
    /// That lasts as long as the table does, which is for as long as they stay in this land.
    /// </summary>
    public sealed class MerchantTable
    {
        public const int MostSold = 12;

        public Item?[] Stock { get; set; } = Array.Empty<Item?>();
        public Item?[] Sold { get; set; } = new Item?[MostSold];

        /// <summary>What something on the table costs.</summary>
        /// <param name="buyingBack">Whether it's from what the hero sold here, rather than what's for sale.</param>
        public static int PriceOf(Item item, bool buyingBack) => buyingBack ? item.SellValue : item.BuyValue;

        public bool Sell(HeroState hero, ItemPlace place)
        {
            if (hero.Take(place) is not Item item) return false;
            hero.Gold += item.SellValue;
            Keep(item);
            return true;
        }

        // With no room left, what's been there longest goes for good.
        private void Keep(Item item)
        {
            int cell = Array.IndexOf(Sold, null);
            if (cell < 0)
            {
                Array.Copy(Sold, 1, Sold, 0, Sold.Length - 1);
                cell = Sold.Length - 1;
            }
            Sold[cell] = item;
        }

        /// <param name="buyingBack">Whether it's from what the hero sold here, rather than what's for sale.</param>
        /// <param name="cell">Where in the pack it should go, if anywhere in particular.</param>
        public bool Buy(HeroState hero, bool buyingBack, int index, int? cell = null)
        {
            Item?[] shelf = buyingBack ? Sold : Stock;
            if (index < 0 || index >= shelf.Length || shelf[index] is not Item item) return false;

            int price = PriceOf(item, buyingBack);
            if (hero.Gold < price || !hero.Pickup(item, cell)) return false;
            hero.Gold -= price;
            shelf[index] = null;
            return true;
        }
    }
}
