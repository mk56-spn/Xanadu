using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Scenes;

/// <summary>The left and right arrows on song select: a tap moves one level, a hold keeps moving.</summary>
public sealed class SongSelectHoldSystem(IKeyboardState keyboard, SceneSystem scenes)
    : QuerySystem<SongSelectState>
{
    private readonly KeyRepeat _repeat = new();

    protected override void OnUpdate()
    {
        int direction = (keyboard.IsPressed(Key.Right) ? 1 : 0) - (keyboard.IsPressed(Key.Left) ? 1 : 0);

        // An arrow already down when the screen opens belongs to whatever came before.
        if (Query.Count == 0)
        {
            _repeat.Ignore(direction);
            return;
        }

        int steps = _repeat.Update(direction, Tick.deltaTime);
        for (int i = 0; i < steps; i++)
            scenes.TryStepCarousel(direction);
    }
}
