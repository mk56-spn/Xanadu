using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Routes a key press to the note it should judge: a rail tap while a side is
/// absorbed, a mine the player touched, or a note in reach and in the window.
/// The judging itself lives in <see cref="PlayNoteJudge"/>, with the hit
/// effects in <see cref="PlayNoteHits"/> and the run changes in
/// <see cref="PlayNoteActions"/>; missed notes are left to
/// <see cref="PlayNoteMisses"/>.
/// </summary>
public sealed class PlayNoteInputSystem : QuerySystem<Note>
{
    private readonly EntityStore _entities;
    private readonly KeybindSettings _keybinds;
    private readonly IKeyboardState _keyboard;
    private readonly KeyPress _presses = new();
    private readonly PlayNoteJudge _judge;
    private readonly PlayNoteMisses _misses;

    public PlayNoteInputSystem(
        EntityStore entities,
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings? offsets = null)
    {
        _entities = entities;
        _keybinds = keybinds;
        _keyboard = keyboard;

        var context = new PlayNoteContext(
            entities,
            keybinds,
            offsets ?? new OffsetSettings(persist: false));
        _judge = new PlayNoteJudge(context, new PlayNoteHits(context, new PlayNoteActions(context)));
        _misses = new PlayNoteMisses(context);
    }

    protected override void OnUpdate()
    {
        Entity playfield = FindPlayfield();
        if (playfield.IsNull || playfield.Tags.Has<PlayPaused>())
            return;

        if (playfield.HasComponent<PlaySession>())
        {
            PlaySession session = playfield.GetComponent<PlaySession>();
            if (!session.Started || session.Finished)
                return;
        }

        SplitlineState splitline = playfield.TryGetComponent(out SplitlineState state)
            ? state
            : default;

        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            Key key = _keybinds.Get(action);
            if (!_presses.Started(key, _keyboard.IsPressed(key)))
                continue;

            bool absorbed = splitline.LeftActive && NoteRules.IsLeft(action) ||
                            splitline.RightActive && NoteRules.IsRight(action);
            if (absorbed)
            {
                _judge.RailHit(action, playfield);
                continue;
            }

            if (!_judge.Mine(action, playfield))
                _judge.Note(action, playfield);
        }

        _misses.Mark(playfield);
    }

    private Entity FindPlayfield()
    {
        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldView, PlayfieldMode, ScoreState>()
                     .ToEntityList())
        {
            if (entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Play &&
                entity.HasComponent<Player>() &&
                entity.HasComponent<Position>())
            {
                return entity;
            }
        }

        return default;
    }
}
