using Xanadu.Storage.Settings;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Rendering.Rhythm;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Playfields;

/// <summary>
/// Routes an action key press to the note it should judge: one in reach and in
/// the window.
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
        _judge = new PlayNoteJudge(context, new PlayNoteHits(context, new PlayNoteActions()));
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
            if (!session.Started || session.Finished || session.Died)
                return;
        }

        foreach (KeybindAction action in KeybindActions.All)
        {
            Key key = _keybinds.Get(action);
            if (!_presses.Started(key, _keyboard.IsPressed(key)))
                continue;

            // Level shaders read each key's last press (keys. in xanadu.anim).
            ShaderKeys.Press((int)action);

            _judge.Note(action, playfield);
        }

        _misses.Mark(playfield);
    }

    private Entity FindPlayfield()
    {
        foreach (Entity entity in _entities
                     .Query<Playhead, PlayfieldView, PlayfieldMode, ScoreState>()
                     .Entities)
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
