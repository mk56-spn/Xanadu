using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// A titled section whose body folds away. Lives on the section container;
/// pressing <see cref="Header"/> flips <see cref="Expanded"/>, and a folded
/// body leaves its parent's stack, so an auto-height section shrinks to its
/// header and everything below it moves up.
/// </summary>
public struct UiCollapsible : IComponent
{
    public Entity Header;
    public Entity Body;
    public string Title;
    public bool Expanded;
    public bool Held;
}
