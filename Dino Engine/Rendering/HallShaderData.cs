using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace Dino_Engine.Rendering
{
    /// <summary>
    /// The halls near the camera as every shader sees them, at the end of the globals block (Globals.glsl: hallBox, hallFrame, hallCut, hallCutaway): the one the camera is looking into
    /// is opened (Halls.glsl), and no light crosses a hall's walls. Laid out as std140 lays out four arrays of vec4, so its fields are in that order and each array is eight long.
    /// Filled by HallCutawaySystem. Read HALLS.md.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HallShaderData
    {
        public const int Most = 8;

        public Vector4 Box0, Box1, Box2, Box3, Box4, Box5, Box6, Box7;
        public Vector4 Frame0, Frame1, Frame2, Frame3, Frame4, Frame5, Frame6, Frame7;
        public Vector4 Cut0, Cut1, Cut2, Cut3, Cut4, Cut5, Cut6, Cut7;
        public Vector4 Cutaway;

        /// <summary>No halls, and nothing open.</summary>
        public static HallShaderData None => new HallShaderData { Cutaway = new Vector4(-1f, 0f, 0f, 0f) };

        public void Set(int index, Vector4 box, Vector4 frame, Vector4 cut)
        {
            switch (index)
            {
                case 0: Box0 = box; Frame0 = frame; Cut0 = cut; break;
                case 1: Box1 = box; Frame1 = frame; Cut1 = cut; break;
                case 2: Box2 = box; Frame2 = frame; Cut2 = cut; break;
                case 3: Box3 = box; Frame3 = frame; Cut3 = cut; break;
                case 4: Box4 = box; Frame4 = frame; Cut4 = cut; break;
                case 5: Box5 = box; Frame5 = frame; Cut5 = cut; break;
                case 6: Box6 = box; Frame6 = frame; Cut6 = cut; break;
                case 7: Box7 = box; Frame7 = frame; Cut7 = cut; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
