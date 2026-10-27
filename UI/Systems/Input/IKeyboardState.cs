using Godot;

namespace Xanadu.Systems.Input;

/// <summary>Frame-sampled keyboard state, abstracted for tests.</summary>
public interface IKeyboardState
{
    bool IsPressed(Key key);
}
