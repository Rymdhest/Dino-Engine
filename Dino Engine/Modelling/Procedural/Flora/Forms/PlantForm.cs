using Dino_Engine.Modelling.Procedural.Gear;
using Dino_Engine.World;
using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Flora
{
    /// <summary>
    /// How one kind of plant grows. A form holds ranges and nothing else, so one form is every plant of its kind: a seed picks the numbers. A new species is new numbers in a
    /// catalogue, and a new form is only wanted for something that grows a new way.
    /// </summary>
    public abstract class PlantForm
    {
        public abstract void Grow(FloraMesh mesh, Genes genes);
    }

    /// <summary>A kind of plant, as the world knows it. See FLORA.md.</summary>
    public sealed class PlantSpecies
    {
        public readonly string Id;
        public readonly WorldAssetKind Kind;
        public readonly PlantForm Form;
        public float Footprint = 1f;        // Metres at scale 1 that other things stay clear of, from its centre.
        public float SolidRadius;           // Metres at scale 1 that stop a body; 0 can be walked through.
        public float SolidLength;           // Metres at scale 1 along its x axis, for what lies down.
        public FloatRange Scale = new FloatRange(0.85f, 1.15f);
        public int Variants = 4;            // How many different plants of this kind a world grows.
        public bool Sways = true;

        private WorldAsset? _asset;

        public PlantSpecies(string id, WorldAssetKind kind, PlantForm form)
        {
            Id = id;
            Kind = kind;
            Form = form;
        }

        /// <summary>What world generation knows of it: nothing that needs it to be grown.</summary>
        public WorldAsset Asset => _asset ??= new WorldAsset(Id, Kind, Footprint, SolidRadius, Scale, Kind == WorldAssetKind.Tree ? TreeImposter : PlantImposter, Sways, Variants, SolidLength);

        private const float TreeImposter = 62f;
        private const float PlantImposter = 34f;

        public static implicit operator WorldAsset(PlantSpecies species) => species.Asset;
    }

    /// <summary>What colours a plant's leaves come in. Colours are albedos under a sun of about 13, so anything meant to read as bright is still deep.</summary>
    public sealed class Palette
    {
        public Vector3 Shade = new Vector3(0.05f, 0.09f, 0.05f);    // Deep inside, and underneath.
        public Vector3 Body = new Vector3(0.11f, 0.2f, 0.06f);      // What the crown mostly is.
        public Vector3 Sun = new Vector3(0.22f, 0.3f, 0.07f);       // Where it catches the light.
        public float Drift = 0.12f;     // How far one plant strays from the next.
        public float Patches = 0.1f;    // And one mass of leaves from the next.

        /// <summary>Between the three colours, 0 deep in the shade and 1 in full sun.</summary>
        public Vector3 At(float tone)
        {
            tone = Math.Clamp(tone, 0f, 1f);
            return tone < 0.5f ? Vector3.Lerp(Shade, Body, tone * 2f) : Vector3.Lerp(Body, Sun, tone * 2f - 1f);
        }
    }
}
