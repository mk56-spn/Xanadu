using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>
/// The sidebar's layer and z steppers for the selection, shown while anything
/// is selected. LAYER moves each selected item one layer back or forward from
/// its own, stepping over the player wedge, in front of what is already there,
/// and reads MIXED when the selection spans layers; Z raises or lowers every
/// selected item's z. SHADER steps the source through own and the item's
/// groups that have a shader. Each press is one undo step.
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
                int step = button.GetComponent<UiCommand>().Arg;
                string? problem = EditorBufferGuard.Moving(
                    editor.Store,
                    selection,
                    item => EditorLayerEdits.StepLayer(ItemLayer.Of(item).Layer, step));
                if (problem is not null)
                    CommandBuffer.AddComponent(editor.Id, new WarningRequest(problem));
                else
                    Edit(() => EditorLayerEdits.StepLayers(editor.Store, selection, step));
            }
        }

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.ZStep))
        {
            if (_presses.Started(button) && !first.IsNull)
                Edit(() => EditorLayerEdits.StepZ(selection, button.GetComponent<UiCommand>().Arg));
        }

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.ShaderSourceStep))
        {
            if (_presses.Started(button) && !first.IsNull)
                Edit(() => EditorLayerEdits.StepShaderSource(selection, button.GetComponent<UiCommand>().Arg, Groups(editor)));
        }

        if (first.IsNull)
            return;

        ItemLayer shown = ItemLayer.Of(first);
        int? shared = EditorLayerEdits.SharedLayer(selection);
        UiText.Set(
            inspector.Layer,
            shared is { } layer ? $"LAYER {(layer > 0 ? "+" : "")}{layer}" : "LAYER MIXED",
            CommandBuffer);
        UiText.Set(inspector.Z, $"Z {shown.Z}", CommandBuffer);
        UiText.Set(inspector.ShaderSource, SourceLabel(editor, first), CommandBuffer);
    }

    /// <summary>
    /// What the item's shader source reads: OWN, or the group and the name of
    /// the shader it takes. A source that isn't in effect (the group has no
    /// shader, or the item left it) reads OWN, because that's what draws.
    /// </summary>
    private static string SourceLabel(Entity editor, Entity item)
    {
        GroupDefinition[]? groups = Groups(editor);
        int group = ItemShaderSource.Of(item);
        uint mask = item.TryGetComponent(out ItemGroups member) ? member.Mask : 0u;
        if (groups is null ||
            !GroupModulation.TryItemShader(groups, mask, group, 0f, out int slot, out _))
        {
            return "SHADER OWN";
        }

        string name = editor.TryGetComponent(out LevelShaders shaders) && shaders.Get(slot) is { Name.Length: > 0 } found
            ? found.Name.ToUpperInvariant()
            : $"#{slot}";
        if (name.Length > 10)
            name = name[..9] + "…";
        return $"SHADER G{group + 1} {name}";
    }

    private static GroupDefinition[]? Groups(Entity editor) =>
        editor.TryGetComponent(out LevelGroups level) ? level.Groups : null;

    private void Edit(System.Func<bool> change)
    {
        EditorSnapshot before = history.Capture();
        if (change())
            history.Record(before, history.Capture());
    }
}
