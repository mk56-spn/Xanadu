using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// How a press matches a note: an action note (a hold's head among them) takes its own key;
/// a route note's head and a dash take any direction key, which sets the way it goes.
/// </summary>
public static class NoteRules
{
    /// <summary>Whether this press can hit the note.</summary>
    public static bool Matches(Note note, KeybindAction action) =>
        note.IsRoute || note.IsDash ? KeybindSettings.IsDirection(action) : note.Target == action;
}
