using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;

namespace Xanadu.Systems.Editor;

/// <summary>The sidebar's text controls on the editor entity.</summary>
public struct EditorTextPanel(Entity section, Entity field, Entity size) : IComponent
{
    public Entity Section = section;
    public Entity Field = field;
    public Entity Size = size;
}

/// <summary>
/// Edits the selected level texts from the sidebar, and shows the controls only
/// while one is selected. Typing changes the texts as it goes; the whole edit
/// becomes one undo step when the field is left. As with the colour fields, the
/// words go to what was selected when typing began.
/// </summary>
public sealed class EditorTextPanelSystem(EditorHistory history) : QuerySystem<NoteSelection, EditorTextPanel>
{
    /// <summary>How much one press of a size button changes the size, in playfield units.</summary>
    public const float SizeStep = 8f;

    private readonly List<Entity> _targets = new();
    private readonly ButtonPress _presses = new();
    private EditorSnapshot? _before;
    private bool _wasEditing;

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        EditorTextPanel panel = editor.GetComponent<EditorTextPanel>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        Entity first = FirstText(selection);
        bool editing = !panel.Field.IsNull && panel.Field.Tags.Has<TextFieldFocused>();
        UiLayout.SetHidden(panel.Section, first.IsNull && !editing);

        if (editing && !_wasEditing)
            BeginTyping(selection);
        if (editing)
            ApplyTyped(panel.Field.GetComponent<TextField>().Value);
        if (!editing && _wasEditing)
            EndTyping();
        _wasEditing = editing;

        foreach (Entity button in EditorButton.All(editor.Store, EditorButtonKind.TextSize))
        {
            if (_presses.Started(button))
                StepSize(selection, first, button.GetComponent<EditorButton>().Arg);
        }

        if (first.IsNull)
            return;

        LevelText shown = first.GetComponent<LevelText>();
        if (!editing)
        {
            ref TextField field = ref panel.Field.GetComponent<TextField>();
            if (field.Value != shown.Text)
                field.Value = shown.Text;
        }

        UiText.Set(panel.Size, $"SIZE {shown.Size:0}", CommandBuffer);
    }

    private static Entity FirstText(NoteSelection selection)
    {
        foreach (Entity item in selection.AllNotes)
        {
            if (!item.IsNull && item.HasComponent<LevelText>())
                return item;
        }

        return default;
    }

    private void BeginTyping(NoteSelection selection)
    {
        _targets.Clear();
        foreach (Entity item in selection.AllNotes)
        {
            if (!item.IsNull && item.HasComponent<LevelText>())
                _targets.Add(item);
        }

        _before = _targets.Count > 0 ? history.Capture() : null;
    }

    private void ApplyTyped(string value)
    {
        foreach (Entity item in _targets)
        {
            if (item.IsNull || !item.HasComponent<LevelText>())
                continue;

            ref LevelText text = ref item.GetComponent<LevelText>();
            if (text.Text != value)
                text.Text = value;
        }
    }

    private void EndTyping()
    {
        if (_before is { } before)
            history.Record(before, history.Capture());
        _before = null;
        _targets.Clear();
    }

    /// <summary>They all take the first one's size plus the step, so a mixed selection ends up matching.</summary>
    private void StepSize(NoteSelection selection, Entity first, int delta)
    {
        if (first.IsNull)
            return;

        float size = Mathf.Clamp(
            first.GetComponent<LevelText>().Size + delta * SizeStep,
            LevelText.MinSize,
            LevelText.MaxSize);
        EditorSnapshot before = history.Capture();
        foreach (Entity item in selection.AllNotes)
        {
            if (!item.IsNull && item.HasComponent<LevelText>())
                item.GetComponent<LevelText>().Size = size;
        }

        history.Record(before, history.Capture());
    }
}
