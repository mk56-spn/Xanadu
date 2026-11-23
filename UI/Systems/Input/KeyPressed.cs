using Friflo.Engine.ECS;

namespace Xanadu.Ui.Systems.Input;

/// <summary>
/// Signal: a key press handed to the entity that takes keys (it carries
/// <see cref="KeyTarget"/>). The entity's own listener says what it does.
/// </summary>
public readonly record struct KeyPressed(KeyChord Chord);

/// <summary>This entity takes the key presses the host hands out, as <see cref="KeyPressed"/> signals.</summary>
public struct KeyTarget : ITag;
