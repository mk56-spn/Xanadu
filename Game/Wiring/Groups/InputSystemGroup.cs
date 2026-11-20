using Friflo.Engine.ECS.Systems;
using Xanadu.Wiring.Scenes;

namespace Xanadu.Wiring.Groups;

/// <summary>
/// The game's answers to this frame's UI input: scene buttons, settings among them. The UI's own input ran before the game's root.
/// </summary>
public sealed class InputSystemGroup : SystemGroup
{
    public InputSystemGroup(SceneSystem scenes)
        : base("Input")
    {
        Add(scenes);
    }
}
