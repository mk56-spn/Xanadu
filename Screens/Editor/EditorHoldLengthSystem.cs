using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.Commands;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The hold length stepper: - and + ask Logic to step the selected holds by one
/// snap step (<see cref="EditorHolds"/>). A hold just laid is selected, so it can
/// be trimmed straight away. The readout shows the first selected hold's length.
/// </summary>
public sealed class EditorHoldLengthSystem(EntityStore entities, EditorCommandBus commands) : QuerySystem<EditorHoldLengthReadout>
{
    private readonly ButtonPress _presses = new();

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<EditorPlacement, Playhead>(entities);
        if (editor.IsNull)
            return;

        foreach (Entity button in EditorButton.All(entities, EditorButtonKind.HoldLength))
        {
            if (_presses.Started(button))
                commands.Enqueue(EditorCommand.StepHoldLength(button.GetComponent<UiCommand>().Arg));
        }

        Query.EachEntity(new ShowLength(Query.Store, CommandBuffer, EditorHolds.Label(editor)));
    }

    private readonly record struct ShowLength(EntityStore Store, CommandBuffer Buffer, string Text) : IEachEntity<EditorHoldLengthReadout>
    {
        public void Execute(ref EditorHoldLengthReadout _, int id) => UiText.Set(Store.GetEntityById(id), Text, Buffer);
    }
}
