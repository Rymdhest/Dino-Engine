using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Util;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ColorComponent : IComponent
    {
        public Colour value;

        public ColorComponent(Colour color) {
            value = color;
        }
    }
}
