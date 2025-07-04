using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct NameComponent : IComponent
    {
        public string value;

        public NameComponent(string name)
        {
            value = name;
        }
    }


}
