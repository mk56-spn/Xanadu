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

/// <summary>A button that thickens or thins the selected blocks' outline by whole units.</summary>
public struct EditorOutlineStepButton(int delta) : IComponent
{
    public int Delta = delta;
}

/// <summary>
/// Sets the outline of the selected blocks from the sidebar, each change one undo
/// step, and shows the controls only while a block is selected. As with the self
/// modulate field, a typed colour goes to what was selected when typing began.
/// </summary>
public sealed class EditorBlockOutlineSystem(EditorHistory history) : QuerySystem<NoteSelection, EditorBlockOutlinePanel>
{
    private readonly List<Entity> _targets = new();
    private readonly HashSet<int> _held = new();
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorBlockOutlinePanel panel = editor.GetComponent<EditorBlockOutlinePanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = FirstBlock(selection);
        bool editing = UiColorField.IsEditing(panel.Color);
        UiLayout.SetHidden(panel.Section, first.IsNull && !editing);

        if (editing && !_wasEditing)
            CollectTargets(selection);
        _wasEditing = editing;

        if (UiColorField.TakeCommit(panel.Color, out Color color))
            ApplyColor(color);

        foreach (Entity button in editor.Store.Query<EditorOutlineStepButton>().WithDisabled().ToEntityList())
        {
            bool pressed = button.Tags.Has<Pressed>();
            bool was = !_held.Add(button.Id);
            if (!pressed)
                _held.Remove(button.Id);
            if (pressed && !was)
                Step(selection, first, button.GetComponent<EditorOutlineStepButton>().Delta);
        }

        if (first.IsNull)
            return;

        Block shown = first.GetComponent<Block>();
        UiColorField.Show(panel.Color, shown.OutlineColor);
        UiText.Set(
            panel.Width,
            shown.OutlineWidth > 0f ? $"OUTLINE {shown.OutlineWidth:0}" : "OUTLINE NONE",
            CommandBuffer);
    }

    private static Entity FirstBlock(NoteSelection selection)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (IsBlock(item))
                return item;
        }

        return default;
    }

    private void CollectTargets(NoteSelection selection)
    {
        _targets.Clear();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsBlock(item))
                _targets.Add(item);
        }
    }

    private void ApplyColor(Color color)
    {
        _targets.RemoveAll(item => !IsBlock(item));
        if (_targets.Count == 0)
            return;

        EditorSnapshot before = history.Capture();
        foreach (Entity item in _targets)
            item.GetComponent<Block>().OutlineColor = color;
        history.Record(before, history.Capture());
    }

    /// <summary>They all take the first one's width plus the step, so a mixed selection ends up matching.</summary>
    private void Step(NoteSelection selection, Entity first, int delta)
    {
        if (first.IsNull)
            return;

        float width = Mathf.Clamp(
            first.GetComponent<Block>().OutlineWidth + delta,
            0f,
            Block.MaxOutlineWidth);
        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (IsBlock(item))
                item.GetComponent<Block>().OutlineWidth = width;
        }

        history.Record(before, history.Capture());
    }

    private static bool IsBlock(Entity item) => !item.IsNull && item.HasComponent<Block>();
}
