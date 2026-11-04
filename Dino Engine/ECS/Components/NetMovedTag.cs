using Dino_Engine.ECS.ECS_Architecture;

namespace Dino_Engine.ECS.Components
{
    /// <summary>
    /// Something placed from outside, by what a server says. Its velocity is what it was seen doing, for animation to read: it is
    /// not for <see cref="Systems.VelocitySystem"/> to act on.
    /// </summary>
    public struct NetMovedTag : IComponent
    {
    }
}
