using Xanadu.Ecs;
using Xanadu.Settings;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// How a press matches a note. Action keys hit action notes: a coloured note
/// needs its own key, a neutral note takes any of the three. Direction keys hit
/// directional notes, which carry no way of their own: the key pressed is the
/// way, turned 45° clockwise on a rhombus. Contact barriers take no press.
/// </summary>
public static class NoteRules
{
    /// <summary>Kinds hit with an action key.</summary>
    public static bool IsAction(NoteKind kind) => kind is NoteKind.Hit or NoteKind.Neutral;

    /// <summary>Kinds hit with a direction key; they move the player.</summary>
    public static bool IsMovement(NoteKind kind) => kind is NoteKind.Dash or NoteKind.Momentum;

    /// <summary>Kinds that count towards the note total and are judged.</summary>
    public static bool IsScored(NoteKind kind) => kind != NoteKind.Contact;

    /// <summary>Whether this press can hit the note.</summary>
    public static bool Matches(Note note, KeybindAction action) => note.Kind switch
    {
        NoteKind.Hit => note.Target == action,
        NoteKind.Neutral => !KeybindSettings.IsDirection(action),
        NoteKind.Dash or NoteKind.Momentum => KeybindSettings.IsDirection(action),
        _ => false,
    };

    /// <summary>The way a direction key points.</summary>
    public static DashDirection DirectionOf(KeybindAction action) => action switch
    {
        KeybindAction.Left => DashDirection.Left,
        KeybindAction.Right => DashDirection.Right,
        KeybindAction.Down => DashDirection.Down,
        _ => DashDirection.Up,
    };

    /// <summary>
    /// Where a directional note sends the player for this key: the key's way,
    /// or 45° clockwise of it on a rhombus (up becomes up-right).
    /// </summary>
    public static DashDirection Heading(Note note, KeybindAction action)
    {
        DashDirection straight = DirectionOf(action);
        return note.Diagonal ? (DashDirection)(((int)straight + 1) % 8) : straight;
    }
}
