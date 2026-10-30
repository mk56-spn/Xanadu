using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>Reads a contact barrier's two costs.</summary>
public static class ContactRules
{
    /// <summary>
    /// The left and right charge a barrier asks for. Maps written before
    /// barriers had side costs stored one number and a key; those load as a
    /// one-sided cost on the key's side so old charts still mean something.
    /// </summary>
    public static (int Left, int Right) Costs(Note note)
    {
        if (note.ContactLeft != 0 || note.ContactRight != 0)
            return (note.ContactLeft, note.ContactRight);

        if (note.ContactRequired <= 0)
            return (0, 0);

        return NoteRules.IsLeft(note.Target)
            ? (note.ContactRequired, 0)
            : NoteRules.IsRight(note.Target)
                ? (0, note.ContactRequired)
                : (note.ContactRequired, 0);
    }
}
