using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct CharacterModelComponent : IComponent, ICleanupComponent
    {
        public glModel model;

        public CharacterModelComponent(glModel model)
        {
            this.model = model;
        }

        public void Cleanup()
        {
            // TODO ????????????
            //model.cleanUp();
        }
    }
}
