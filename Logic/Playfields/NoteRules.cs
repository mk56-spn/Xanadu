using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Logic.Playfields;

/// <summary>How a press matches a note: an action note takes its own key.</summary>
public static class NoteRules
{
    /// <summary>Whether this press can hit the note.</summary>
    public static bool Matches(Note note, KeybindAction action) =>
        note.Target == action;
}
