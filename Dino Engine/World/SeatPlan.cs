using Dino_Engine.World.Content;
using OpenTK.Mathematics;

namespace Dino_Engine.World
{
    /// <summary>
    /// What a commander's seat is, as far as the game and the checks need to know (SEATS.md): which kind it is, where its tyrant waits and how far round that the yard is kept open, and the frame
    /// it's laid out in, which is worked out from where its road comes in before the road is cut back to its gate. Worked out with the world on every machine, and never sent or saved. Headless.
    /// </summary>
    public sealed class SeatPlan
    {
        public SeatKind Kind;

        /// <summary>Where its tyrant waits, in the world: the middle of the open yard.</summary>
        public Vector2 Heart;

        /// <summary>Metres round the heart that nothing solid stands in, and nobody of the garrison camps in, so that whoever fights the tyrant has room to lead it round.</summary>
        public float Yard;

        /// <summary>
        /// Turns the seat's own +z, its front, onto the world, as a model's yaw does. Its back, where whatever's tallest stands, is to the north, the way the camera looks, unless its road
        /// comes from there, when it's turned just far enough for the road to come in by a side.
        /// </summary>
        public float Yaw = MathF.PI;

        /// <summary>How far its walls stand from its middle: to either side, to its back and to its front.</summary>
        public float Side, Back, Front;

        /// <summary>Which wall the road comes in by: 0 its front, 1 its left (-x), 2 its right. Each of the three has a way in, and the road's is the great one.</summary>
        public int GateSide;

        /// <summary>The height everything in it stands at: its ground is laid level, as a yard is.</summary>
        public float Level;

        /// <summary>Whether the frame's been worked out from the land's roads (SeatBuilder.Frame).</summary>
        public bool Framed;
    }
}
