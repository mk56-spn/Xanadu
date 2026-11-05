using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The sidebar's layer and z steppers for the selection, shown while anything
/// is selected. They read the first selected item. LAYER puts the whole
/// selection on the layer one back or forward from the first item's, stepping
/// over the player wedge, in front of what is already there; Z raises or
/// lowers every selected item's z. Each press is one undo step.
/// </summary>
public sealed class EditorLayerInspectorSystem(EditorHistory history)
    : QuerySystem<NoteSelection, EditorLayerInspector>
{
    private readonly ButtonPress _presses = new();

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorLayerInspector inspector = editor.GetComponent<EditorLayerInspector>();
        IReadOnlyList<Entity> selection = editor.GetComponent<NoteSelection>().AllNotes;
        Entity first = EditorSelectionScan.First(editor.GetComponent<NoteSelection>(), EditorItems.IsItem);
        UiLayout.SetHidden(inspector.Section, CommandBuffer, first.IsNull);

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.LayerStep))
        {
            if (_presses.Started(button) && !first.IsNull)
            {
                int layer = EditorLayerEdits.StepLayer(ItemLayer.Of(first).Layer, button.GetComponent<EditorButton>().Arg);
                string? problem = EditorBufferGuard.MovingToLayer(editor.Store, selection, layer);
                if (problem is not null)
                    CommandBuffer.AddComponent(editor.Id, new WarningRequest(problem));
                else
                    Edit(() => EditorLayerEdits.PutOnLayer(editor.Store, selection, layer));
            }
        }

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.ZStep))
        {
            if (_presses.Started(button) && !first.IsNull)
                Edit(() => EditorLayerEdits.StepZ(selection, button.GetComponent<EditorButton>().Arg));
        }

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.ShaderSourceStep))
        {
            if (_presses.Started(button) && !first.IsNull)
                Edit(() => EditorLayerEdits.StepShaderSource(selection, button.GetComponent<EditorButton>().Arg));
        }

        if (first.IsNull)
            return;

        ItemLayer shown = ItemLayer.Of(first);
        UiText.Set(inspector.Layer, $"LAYER {(shown.Layer > 0 ? "+" : "")}{shown.Layer}", CommandBuffer);
        UiText.Set(inspector.Z, $"Z {shown.Z}", CommandBuffer);
        int source = ItemShaderSource.Of(first);
        UiText.Set(
            inspector.ShaderSource,
            source < 0 ? "SHADER OWN" : $"SHADER G{source + 1}",
            CommandBuffer);
    }

    private void Edit(System.Func<bool> change)
    {
        EditorSnapshot before = history.Capture();
        if (change())
            history.Record(before, history.Capture());
    }
}
