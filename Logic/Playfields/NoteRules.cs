using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Settings;

namespace Xanadu.Logic.Playfields;

/// <summary>How a press matches a note: an action note takes its own key; dashes and orb heads take a direction (an orb only where the dash catches it, <see cref="Xanadu.Model.Playfields.Orbs.Catches"/>).</summary>
public static class NoteRules
{
    /// <summary>Whether this press can hit the note.</summary>
    public static bool Matches(Note note, KeybindAction action) =>
        note.Kind switch
        {
            NoteKind.Dash => KeybindActions.IsDirection(action),
            NoteKind.Entrance => true,
            NoteKind.Orb => KeybindActions.IsDirection(action),
            _ => note.Target == action,
        };
}
