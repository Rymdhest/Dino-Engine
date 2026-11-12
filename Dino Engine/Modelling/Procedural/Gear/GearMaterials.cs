using Dino_Engine.Modelling.Model;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.Modelling.Procedural.Gear
{
    /// <summary>
    /// Which materials gear is made of, by their place in the material arrays. It's here and not read from <see cref="Textures.TextureGenerator"/>, as just touching that class compiles
    /// shaders, and generators have to run where there's no GPU. The game fills these in once its textures exist. Anywhere else they stay as they are, which tells them apart.
    /// </summary>
    public static class GearMaterials
    {
        public static int Metal = 1;
        public static int Mail = 2;
        public static int Leather = 3;
        public static int Cloth = 4;
        public static int Wood = 5;         // Boards, as a building's: planks on a shield's back.
        public static int Bone = 6;
        public static int Gem = 7;
        public static int Glow = 8;
        public static int Skin = 9;
        public static int Straw = 10;
        public static int Timber = 11;      // Long grain with no joints, for hafts, bows, staves and grips.
        public static int Scale = 12;       // Metal in overlapping scales, laced in rows.
        public static int Quilt = 13;       // Cloth quilted in channels over padding, as a gambeson is.
        public static int Fur = 14;

        // Leather, cloth, bone, timber, quilting and fur are each a colour of their own, which the shader multiplies into whatever's drawn on them, and each recipe averages exactly this
        // (TEXTURES.md). Metal, scales and gems are white, so what's drawn on them is the colour.
        public static readonly Vector3 LeatherAlbedo = new Vector3(112f, 76f, 50f) / 255f;
        public static readonly Vector3 ClothAlbedo = new Vector3(178f, 172f, 158f) / 255f;
        public static readonly Vector3 TimberAlbedo = new Vector3(158f, 132f, 102f) / 255f;
        public static readonly Vector3 FurAlbedo = new Vector3(150f, 138f, 120f) / 255f;
        public static Vector3 BoneAlbedo => Material.BONE.Colour.ToVector3();

        /// <summary>What a material does to the colour drawn on it. A colour meant as an albedo divides by this, and a picture drawn with no textures multiplies by it to show what the game will.</summary>
        public static Vector3 AlbedoOf(int material) =>
            material == Leather ? LeatherAlbedo
            : material == Cloth || material == Quilt ? ClothAlbedo
            : material == Bone ? BoneAlbedo
            : material == Timber ? TimberAlbedo
            : material == Fur ? FurAlbedo
            : Vector3.One;

        /// <summary>Whether a material is metal through and through, as far as its colour goes.</summary>
        public static bool IsMetal(int material) => material == Metal || material == Mail || material == Scale;
    }
}
