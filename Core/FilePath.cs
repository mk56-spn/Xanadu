using Friflo.Engine.ECS;

namespace Xanadu.Core;

/// <summary>The file the entity's document is saved to (game-folder, <c>user://</c> or absolute); empty until it has one.</summary>
public struct FilePath(string value) : IComponent
{
    public string Value = value;
}
