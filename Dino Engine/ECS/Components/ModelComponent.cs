using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct ModelComponent : IComponent, ICleanupComponent
    {
        public glModel model;
        public float swayAmount;

        public ModelComponent(glModel model, float swayAmount = 0.0f)
        {
            this.model = model;
            this.swayAmount = swayAmount;
        }

        public void Cleanup()
        {
            // TODO ????????????
            //model.cleanUp();
        }
    }
}
