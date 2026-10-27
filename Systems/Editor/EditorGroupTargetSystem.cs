using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Drags the selected move effect's offset by its on-canvas handle. The handle
/// sits at a member's position plus the full offset, so the whole group's move
/// can be shaped without typing numbers.
/// </summary>
public sealed class EditorGroupTargetSystem :
    QuerySystem<EditorGroupState, LevelGroups, Playhead, PlayfieldView>
{
    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly EditorHistory _history;
    private bool _wasPressed;
    private bool _dragging;
    private EditorSnapshot _before;
    private Vector2 _startOffset;
    private Vector2 _startPointer;
    private int _group;
    private int _row;

    public EditorGroupTargetSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        EditorHistory history)
    {
        _entities = entities;
        _pointer = pointer;
        _history = history;
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        Entity editor = FindEditor();
        if (editor.IsNull)
        {
            _wasPressed = input.Pressed;
            return;
        }

        ref EditorGroupState state = ref editor.GetComponent<EditorGroupState>();
        if (!state.TargetEdit)
        {
            if (_dragging)
            {
                _history.Record(_before, _history.Capture());
                _dragging = false;
            }

            _wasPressed = input.Pressed;
            return;
        }

        LevelGroups levelGroups = editor.GetComponent<LevelGroups>();
        if (state.SelectedGroup < 0 ||
            state.SelectedGroup >= levelGroups.Groups.Length ||
            state.SelectedEffect < 0 ||
            state.SelectedEffect >= levelGroups.Groups[state.SelectedGroup].Effects.Count)
        {
            if (_dragging)
            {
                _history.Record(_before, _history.Capture());
                _dragging = false;
            }

            state.TargetEdit = false;
            _wasPressed = input.Pressed;
            return;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Playfield playfield = editor.GetComponent<Playfield>();
        NoteSelection selection = editor.GetComponent<NoteSelection>();
        GroupDefinition group = levelGroups.Groups[state.SelectedGroup];
        GroupEffect effect = group.Effects[state.SelectedEffect];
        if (effect.Kind != GroupEffectKind.Move)
        {
            state.TargetEdit = false;
            _dragging = false;
            _wasPressed = input.Pressed;
            return;
        }

        Vector2 anchor = Anchor(playfield, selection, state.SelectedGroup);
        Vector2 handle = view.PlayfieldToScreen(anchor + effect.Offset);
        bool press = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        if (press && EditorGroupTarget.HandleRect(handle).HasPoint(input.Position))
        {
            _dragging = true;
            _before = _history.Capture();
            _startOffset = effect.Offset;
            _startPointer = view.ScreenToPlayfield(input.Position);
            _group = state.SelectedGroup;
            _row = state.SelectedEffect;
        }

        if (!_dragging)
            return;

        if (!input.Pressed)
        {
            _history.Record(_before, _history.Capture());
            _dragging = false;
            return;
        }

        if (state.SelectedGroup != _group ||
            state.SelectedEffect != _row ||
            _row >= group.Effects.Count)
        {
            _dragging = false;
            return;
        }

        Vector2 delta = view.ScreenToPlayfield(input.Position) - _startPointer;
        GroupEffect current = group.Effects[_row];
        group.Effects[_row] = current with
        {
            Offset = EditorGroupTarget.Tidy(_startOffset + delta),
        };
    }

    private Vector2 Anchor(Playfield playfield, NoteSelection selection, int group)
    {
        Entity member = EditorGroupTarget.AnchorMember(_entities, selection, group);
        return EditorGroupTarget.TryPosition(member, out Vector2 position)
            ? position
            : new Vector2(playfield.Width, playfield.Height) / 2f;
    }

    private Entity FindEditor()
    {
        foreach (Entity entity in _entities
                     .Query<EditorGroupState, LevelGroups, Playhead, PlayfieldView>()
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }
}
