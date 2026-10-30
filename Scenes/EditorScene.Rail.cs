using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Editor;

namespace Xanadu.Scenes;

public static partial class EditorScene
{
    /// <summary>
    /// The separate Splitline rail layer: taps and holds for the six side keys
    /// are placed at the playhead, and channel end markers are placed the same
    /// way. Channel starts are ordinary Channel notes on the playfield.
    /// </summary>
    private static void BuildRailLayer(
        EntityStore entities,
        Entity root,
        Entity editor,
        EditorShellRects shell)
    {
        UiNode ui = UiNode.Layer(entities, root, 964, null, UiStage.Full);
        Vector2 home = shell.Viewport.Position + new Vector2(
            12,
            shell.Viewport.Size.Y - 128);
        UiNode panel = ui.Column()
            .At(home)
            .Size(376, 116)
            .Fill(SceneUi.PanelColor)
            .Pad(7)
            .Gap(4);

        panel.Text("RAIL LAYER", 12, SceneUi.DimTextColor, TextAlign.Left)
            .Size(362, 16);

        UiNode keys = panel.Row().Size(362, 26).Gap(4);
        foreach ((KeybindAction action, string label) in new[]
                 {
                     (KeybindAction.L1, "L1"),
                     (KeybindAction.L2, "L2"),
                     (KeybindAction.L3, "L3"),
                     (KeybindAction.R1, "R1"),
                     (KeybindAction.R2, "R2"),
                     (KeybindAction.R3, "R3"),
                 })
        {
            keys.Button(label, 10, SceneUi.MutedButtonColor)
                .Size(56, 24)
                .With(new EditorRailButton(action))
                .Tooltip($"Place a rail note for {label} at the playhead");
        }

        UiNode controls = panel.Row().Size(362, 26).Gap(4);
        Entity holdButton = controls.Button("TAP", 10, SceneUi.MutedButtonColor)
            .Size(110, 24)
            .With(new EditorRailHoldButton())
            .Tooltip("Cycle the hold length new rail notes get");
        controls.Button("L END", 10, SceneUi.MutedButtonColor)
            .Size(118, 24)
            .With(new EditorChannelEndButton(ChannelSide.Left))
            .Tooltip("Place a left channel end at the playhead");
        controls.Button("R END", 10, SceneUi.MutedButtonColor)
            .Size(118, 24)
            .With(new EditorChannelEndButton(ChannelSide.Right))
            .Tooltip("Place a right channel end at the playhead");

        editor.AddComponent(new EditorRailHoldState(0));
        editor.AddComponent(new EditorRailHoldReadout(holdButton));
    }
}
