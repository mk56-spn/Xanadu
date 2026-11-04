using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Clears a text field when it is pressed twice in quick succession, so a value
/// can be retyped from scratch without holding backspace. The field keeps its
/// focus, and the owner sees an ordinary edit to an empty value.
/// </summary>
public sealed class TextFieldDoubleClickSystem : QuerySystem<TextField>
{
    /// <summary>The longest gap between the two presses of a double click, in seconds.</summary>
    public const float Window = 0.4f;

    private readonly HashSet<int> _held = new();
    private int _lastField = -1;
    private float _lastPressAt = float.NegativeInfinity;
    private float _clock;

    protected override void OnUpdate()
    {
        _clock += Tick.deltaTime;
        foreach (Entity entity in Query.ToEntityList())
        {
            bool pressed = entity.Tags.Has<Pressed>();
            if (!pressed)
            {
                _held.Remove(entity.Id);
                continue;
            }

            if (!_held.Add(entity.Id))
                continue;

            if (entity.Id == _lastField && _clock - _lastPressAt <= Window)
            {
                entity.GetComponent<TextField>().Value = "";
                _lastField = -1;
                _lastPressAt = float.NegativeInfinity;
                continue;
            }

            _lastField = entity.Id;
            _lastPressAt = _clock;
        }
    }
}
