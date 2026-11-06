using Dino_Engine.Animation;
using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>Something that moves like a living thing. See <see cref="CharacterAnimator"/>.</summary>
    public struct AnimatorComponent : IComponent
    {
        public CharacterAnimator Animator;

        public AnimatorComponent(CharacterAnimator animator)
        {
            Animator = animator;
        }
    }
}
