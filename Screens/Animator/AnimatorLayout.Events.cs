using Friflo.Engine.ECS;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// The event inspector: the selected clip event's name and time, and a delete
/// button. It floats over the bottom of the preview, just above the timeline
/// whose ruler the event's flag sits on.
/// </summary>
public static partial class AnimatorLayout
{
    /// <summary>Rows under the title: name, time, delete.</summary>
    private const int EventRows = 3;

    public static readonly Vector2 EventInspectorSize =
        new(ChainInspectorWidth, 8 * 2 + 18 + EventRows * ChainRow + EventRows * ChainSpacing);

    /// <summary>Starts hidden; the event inspector system moves it into place while an event is selected.</summary>
    private static void BuildEventInspector(UiBuilder menu, ref Refs refs)
    {
        const float inner = ChainInspectorWidth - 16f;
        menu.Container(Vector2.Zero, EventInspectorSize, StackDirection.Vertical,
            spacing: ChainSpacing, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, autoHeight: true, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.EventInspector = menu.Current;
        refs.Ui.EventTitle = menu.Text("EVENT",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(inner, ChainRow), spacing: 4, color: Clear);
        menu.Text("NAME", new Vector2(ChainLabelWidth, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.EventName = TextInput(menu, new Vector2(inner - ChainLabelWidth - 4, 22));
        refs.Ui.EventName.AddComponent(new UiTooltip(
            "What the game is told when playback passes this moment, such as hit or step"));
        menu.End();

        refs.Ui.EventTime = Field(menu, "TIME", new AnimatorValueBinding(AnimatorFieldKind.EventTime),
            UiValueShape.Float, [0f], null, "Where in the clip the event sits, in seconds", inner);

        Entity delete = menu.Button("DELETE EVENT", new Vector2(inner, ChainRow),
            fontSize: UiTheme.SmallFontSize);
        delete.AddComponent(new Intent(new DeleteEvent()));
        AnimatorTooltips.Attach(delete, new DeleteEvent());
        menu.End();
    }
}
