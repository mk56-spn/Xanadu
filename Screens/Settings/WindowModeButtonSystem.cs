using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;

namespace Xanadu.Screens.Settings;

/// <summary>Cycles the settings screen's window mode button: windowed, fullscreen, borderless.</summary>
public sealed class WindowModeButtonSystem(DisplaySettings display) : QuerySystem<WindowModeButton, Text>
{
    protected override void OnUpdate()
    {
        Query.EachEntity(new CycleMode(Query.Store, CommandBuffer, display));
    }

    private readonly record struct CycleMode(EntityStore Store, CommandBuffer Buffer, DisplaySettings Display) : IEachEntity<WindowModeButton, Text>
    {
        public void Execute(ref WindowModeButton button, ref Text text, int id)
        {
            Entity entity = Store.GetEntityById(id);

            bool pressed = entity.Tags.Has<Pressed>();
            if (pressed && !button.Held)
                Display.SetWindowMode((WindowMode)(((int)Display.WindowMode + 1) % 3));
            button.Held = pressed;

            string expected = SettingsScene.WindowModeLabel(Display.WindowMode);
            if (text.Value != expected)
            {
                text.Value = expected;
                Buffer.AddTag<Dirty>(id);
            }
        }
    }
}
