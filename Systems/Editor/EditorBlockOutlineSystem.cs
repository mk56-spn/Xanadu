using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The sidebar's outline controls on the editor entity.</summary>
public struct EditorBlockOutlinePanel(Entity section, Entity width, Entity color) : IComponent
{
    public Entity Section = section;
    public Entity Width = width;
    public Entity Color = color;
}

/// <summary>
/// Sets the outline of the selected blocks and spikes from the sidebar, each change
/// one undo step, and shows the controls only while one of them is selected. As with the self
/// modulate field, a typed colour goes to what was selected when typing began.
/// </summary>
public sealed class EditorBlockOutlineSystem(EditorHistory history) : QuerySystem<NoteSelection, EditorBlockOutlinePanel>
{
    private readonly ButtonPress _presses = new();
    private readonly EditorColorEdit _color = new(history, IsOutlined, SetColor);

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorBlockOutlinePanel panel = editor.GetComponent<EditorBlockOutlinePanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = EditorSelectionScan.First(selection, IsOutlined);

        // A dragged picker colours the outlines as it moves; the drag is one undo step.
        bool editing = _color.Update(editor, panel.Color, selection);
        UiLayout.SetHidden(panel.Section, first.IsNull && !editing);

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.OutlineStep))
        {
            if (_presses.Started(button))
                Step(selection, first, button.GetComponent<EditorButton>().Arg);
        }

        if (first.IsNull)
            return;

        (float shownWidth, Color shownColor) = OutlineOf(first);
        UiColorField.Show(panel.Color, shownColor);
        UiText.Set(
            panel.Width,
            $"{shownWidth:0}",
            CommandBuffer);
    }

    /// <summary>They all take the first one's width plus the step, so a mixed selection ends up matching.</summary>
    private void Step(NoteSelection selection, Entity first, int delta)
    {
        if (first.IsNull)
            return;

        float width = Mathf.Clamp(
            OutlineOf(first).Width + delta,
            0f,
            Block.MaxOutlineWidth);
        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsOutlined(item))
                SetWidth(item, width);
        }

        history.Record(before, history.Capture());
    }

    /// <summary>Blocks and spikes carry an outline.</summary>
    private static bool IsOutlined(Entity item) =>
        !item.IsNull && (item.HasComponent<Block>() || item.HasComponent<Spike>());

    private static (float Width, Color Color) OutlineOf(Entity item)
    {
        if (item.TryGetComponent(out Block block))
            return (block.OutlineWidth, block.OutlineColor);
        Spike spike = item.GetComponent<Spike>();
        return (spike.OutlineWidth, spike.OutlineColor);
    }

    private static void SetWidth(Entity item, float width)
    {
        if (item.HasComponent<Block>())
            item.GetComponent<Block>().OutlineWidth = width;
        else
            item.GetComponent<Spike>().OutlineWidth = width;
    }

    private static void SetColor(Entity item, Color color)
    {
        if (item.HasComponent<Block>())
            item.GetComponent<Block>().OutlineColor = color;
        else
            item.GetComponent<Spike>().OutlineColor = color;
    }
}
