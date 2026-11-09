using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Modelling.Model;
using OpenTK.Mathematics;

namespace Dino_Engine.ECS.Components
{
    public struct CharacterModelComponent : IComponent, ICleanupComponent
    {
        public glModel model;

        /// <summary>Whether it opens for the player standing behind it, as anything big enough to hide them does (FocusCutout.glsl): a body that size is a giant.</summary>
        public bool Opens;

        /// <summary>How far it's thinned away, from 0, all there, to 1, gone, as something only for looking at comes and goes without ever being seen to pop.</summary>
        public float Faded;

        public CharacterModelComponent(glModel model)
        {
            this.model = model;
            Opens = false;
            Faded = 0f;
        }

        public void Cleanup()
        {
            // TODO ????????????
            //model.cleanUp();
        }
    }
}
