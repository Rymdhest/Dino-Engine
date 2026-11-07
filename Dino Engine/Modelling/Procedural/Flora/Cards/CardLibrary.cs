using Dino_Engine.Modelling.Procedural.Gear;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// Every card there is, and where it lands in the textures. A layer holds one kind of card, in four variations, one to a corner: a card only ever takes a corner's worth of the
    /// camera, and a kind with four pictures breaks up repetition where four kinds of one picture wouldn't.
    /// </summary>
    public static class CardLibrary
    {
        public const int LayerSize = 1024;
        public const int CellSize = LayerSize / 2;
        public const int CellsPerLayer = 4;
        public const int Levels = 11;   // log2(LayerSize) + 1.

        private const float Inset = 3f / LayerSize;     // Keeps a card off the next one, down through the mips.

        public static IReadOnlyList<CardForm> All => FloraCatalog.Cards;

        /// <summary>The part of a layer one variation takes up, as its lowest and highest UVs.</summary>
        public static void Cell(int variation, out Vector2 min, out Vector2 max)
        {
            int x = variation & 1, y = (variation >> 1) & 1;
            min = new Vector2(x * 0.5f + Inset, y * 0.5f + Inset);
            max = new Vector2(x * 0.5f + 0.5f - Inset, y * 0.5f + 0.5f - Inset);
        }

        public static int Material(CardForm card) => FloraMaterials.FirstCard + card.Layer;

        /// <summary>How many texels across a layer is at a mip level.</summary>
        public static int LevelSize(int level) => Math.Max(LayerSize >> level, 1);

        /// <summary>Paints every card. It's pure, and uses every core it's given, so a client starts this first and collects it once its materials are done.</summary>
        public static BakedCards Bake()
        {
            var cards = All;
            var images = new CardImage[cards.Count * CellsPerLayer];
            Parallel.For(0, images.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(Environment.ProcessorCount - 1, 1) }, () => new CardCanvas(CellSize), (i, _, canvas) =>
            {
                CardForm card = cards[i / CellsPerLayer];
                var genes = new Genes(0xF10A7UL).Part(card.Id).Part(i % CellsPerLayer);
                canvas.Clear();
                card.Paint(canvas, genes);
                canvas.Finish();
                images[i] = new CardImage(canvas);
                return canvas;
            }, _ => { });
            return new BakedCards(images);
        }
    }

    /// <summary>Every card, painted.</summary>
    public sealed class BakedCards
    {
        private readonly CardImage[] _images;

        public BakedCards(CardImage[] images) => _images = images;

        public int Layers => _images.Length / CardLibrary.CellsPerLayer;

        public CardImage Image(int layer, int variation) => _images[layer * CardLibrary.CellsPerLayer + variation];

        /// <summary>
        /// One level of one layer, as its three textures, into arrays that hold at least <see cref="CardLibrary.LevelSize"/> squared texels, so that one set does for every level.
        /// Below the level a cell is one texel it all comes to the same thing, so a corner stands for the rest.
        /// </summary>
        public void Level(int layer, int level, byte[] albedo, byte[] normal, byte[] material)
        {
            int size = CardLibrary.LevelSize(level);
            int cell = size / 2;
            if (cell < 1)
            {
                Image(layer, 0).Pack(Math.Min(level - 1, Image(layer, 0).Levels.Count - 1), albedo, normal, material, 0, 0, size);
                return;
            }
            for (int i = 0; i < CardLibrary.CellsPerLayer; i++)
            {
                Image(layer, i).Pack(level, albedo, normal, material, (i & 1) * cell, ((i >> 1) & 1) * cell, size);
            }
        }
    }
}
