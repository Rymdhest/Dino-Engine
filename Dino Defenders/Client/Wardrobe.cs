using Dino_Defenders.Progression;
using Dino_Engine.Core;
using Dino_Engine.Modelling.Model;
using Dino_Engine.Modelling.Procedural.Character;
using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.Rendering.Renderers.Studio;
using Dino_Engine.Util;

namespace Dino_Defenders.Client
{
    /// <summary>
    /// Everything on this machine that shows gear gets it from here, and everything here comes from the same seed: what's in a hero's hand, what's lying on the ground, and what's in
    /// the icon are the same mesh. Whatever's asking has to keep asking, as that's what keeps a model alive. Nothing here is the server's business.
    /// </summary>
    public sealed class Wardrobe
    {
        private readonly GearWorkshop _workshop = new();

        public int Models => _workshop.Count;

        public void Update() => _workshop.Update();

        /// <summary>Something as it lies on the ground, or nothing yet.</summary>
        public glModel? Lying(ulong seed, uint traits)
        {
            return _workshop.Get(new GearKey(seed, traits), () =>
            {
                GearSpec spec = ItemLook.SpecOf(seed, traits);
                return GearDisplay.Lying(GearForge.Make(spec), spec.Kind).Build(bones: false);
            });
        }

        /// <summary>A hero in what they have on, or nothing yet.</summary>
        public glModel? Dressed(int dye, IReadOnlyList<WornLook> worn)
        {
            return _workshop.Get(KeyOf(dye, worn), () => Outfit.Dress(CharacterFactory.HeroLook(dye), worn.Select(look => (ItemLook.SpecOf(look.Seed, look.Traits), look.Place == WornSlot.Finger2 ? 1f : -1f)).ToList()));
        }

        private static GearKey KeyOf(int dye, IReadOnlyList<WornLook> worn)
        {
            ulong seeds = (ulong)dye + 1;
            ulong traits = 0xA5A5A5A5UL;
            foreach (WornLook look in worn.OrderBy(l => l.Place))
            {
                seeds = (seeds ^ look.Seed) * 0x100000001B3UL;
                traits = (traits ^ look.Traits ^ (ulong)look.Place << 32) * 0x100000001B3UL;
            }
            return new GearKey(seeds, traits | 1ul << 63);
        }

        /// <summary>The picture of something, or nothing until it's been taken.</summary>
        public StudioPicture? Picture(Item item, Colour halo)
        {
            ulong seed = item.LookSeed();
            uint traits = ItemLook.Traits(item);
            GearKind kind = ItemLook.SpecOf(seed, traits).Kind;
            return Engine.RenderEngine.Studio.Picture(seed ^ (ulong)traits * 0x9E3779B97F4A7C15UL, () => Lying(seed, traits), GearDisplay.PictureOf(kind), halo);
        }
    }
}
