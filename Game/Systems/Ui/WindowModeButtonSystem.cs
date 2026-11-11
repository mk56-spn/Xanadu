using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Settings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Ui;

/// <summary>Cycles the settings screen's window mode button: windowed, fullscreen, borderless.</summary>
public sealed class WindowModeButtonSystem(DisplaySettings display) : QuerySystem<WindowModeButton, Text>
{
    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref button, ref text, entity) =>
        {
            bool pressed = entity.Tags.Has<Pressed>();
            if (pressed && !button.Held)
                display.SetWindowMode((WindowMode)(((int)display.WindowMode + 1) % 3));
            button.Held = pressed;

            string expected = SettingsScene.WindowModeLabel(display.WindowMode);
            if (text.Value != expected)
            {
                text.Value = expected;
                buffer.AddTag<Dirty>(entity.Id);
            }
        });
    }
}
