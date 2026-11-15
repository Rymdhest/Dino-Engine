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

        private readonly Vector4 BoxOf(int index) => index switch { 0 => Box0, 1 => Box1, 2 => Box2, 3 => Box3, 4 => Box4, 5 => Box5, 6 => Box6, _ => Box7 };
        private readonly Vector4 FrameOf(int index) => index switch { 0 => Frame0, 1 => Frame1, 2 => Frame2, 3 => Frame3, 4 => Frame4, 5 => Frame5, 6 => Frame6, _ => Frame7 };
        private readonly Vector4 CutOf(int index) => index switch { 0 => Cut0, 1 => Cut1, 2 => Cut2, 3 => Cut3, 4 => Cut4, 5 => Cut5, 6 => Cut6, _ => Cut7 };

        /// <summary>Whether a point is in a hall's room: Halls.glsl's insideHall, which this has to agree with.</summary>
        public readonly bool InsideHall(int index, Vector3 p)
        {
            Vector4 box = BoxOf(index), frame = FrameOf(index), cut = CutOf(index);
            float ox = p.X - box.X, oz = p.Z - box.Y;
            float c = frame.X, s = frame.Y;
            float lx = ox * c - oz * s, lz = ox * s + oz * c;
            float innerX = box.Z - (cut.X - 0.05f), innerZ = box.W - (cut.X - 0.05f);
            float floorY = frame.Z;
            return MathF.Abs(lx) < innerX && MathF.Abs(lz) < innerZ && p.Y > floorY - 2f && p.Y < floorY + frame.W + 0.4f;
        }

        /// <summary>Which halls' rooms a point is in, a bit a hall (Lighting/PointLightTiles.glsl's hallRoomsOf).</summary>
        public readonly uint RoomsOf(Vector3 p)
        {
            uint rooms = 0;
            int count = Math.Min((int)(Cutaway.Z + 0.5f), Most);
            for (int i = 0; i < count; i++)
            {
                if (InsideHall(i, p)) rooms |= 1u << i;
            }
            return rooms;
        }
    }
}
