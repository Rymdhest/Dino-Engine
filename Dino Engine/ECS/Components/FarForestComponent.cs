using Dino_Engine.ECS.ECS_Architecture;
using Dino_Engine.Rendering.Renderers.Geometry;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Trees a long way off, for a view that looks out across a land and past where its trees are streamed (the picture behind the main menu, whose woods go on to a mountain 700 m away):
    /// all of them on one entity, each drawn as a picture of its model turned to the eye (<see cref="ImposterRenderer"/>) and never as the model, however near it stands. Going by distance,
    /// as a model with an imposter does, a camera that's never quite still has the trees at that distance change from one to the other and back for as long as it's looked at.
    /// <see cref="Systems.FarForestSystem"/> draws them, and has them shade each other in the sun's widest map.
    /// </summary>
    public struct FarForestComponent : IComponent
    {
        public ImposterInstanceData[] Trees;

        public FarForestComponent(ImposterInstanceData[] trees)
        {
            Trees = trees;
        }
    }
}
