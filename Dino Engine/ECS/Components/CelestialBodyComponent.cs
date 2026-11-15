using Dino_Engine.ECS.ECS_Architecture;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct CelestialBodyComponent : IComponent
    {
        /// <summary>
        /// How much of its glare and rays there is (CelestialBodyRenderer), of what a body has by itself. They fall off slowly from it, which is nothing to a camera that looks at the
        /// ground and a veil over half the picture to one that looks at the sun: the picture behind the main menu turns it down.
        /// </summary>
        public float Glare;

        public CelestialBodyComponent()
        {
            Glare = 1f;
        }
    }


}
