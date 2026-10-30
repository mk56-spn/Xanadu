using Xanadu.Ecs;
using Xanadu.Settings;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// How a press matches a note. Normal keynotes and the old keyed contact notes
/// match their own key; pair notes match only the number, so either side works.
/// Dash and momentum notes are claimed by any key and are handled separately.
/// </summary>
public static class NoteRules
{
    /// <summary>Kinds that are resolved by a key press.</summary>
    public static bool IsKeyed(NoteKind kind) => kind is
        NoteKind.Hit or
        NoteKind.Pair or
        NoteKind.CharacterSwitch or
        NoteKind.WeaponSwitch or
        NoteKind.Channel or
        NoteKind.Construction or
        NoteKind.ConstructionStep;

    /// <summary>Kinds that resolve as an automatic charge check.</summary>
    public static bool IsBarrier(NoteKind kind) => kind == NoteKind.Contact;

    /// <summary>Rail notes are played on the Splitline rail, not the field.</summary>
    public static bool IsRail(NoteKind kind) => kind == NoteKind.Rail;

    /// <summary>Channel-end markers are chart data, not scored notes.</summary>
    public static bool IsChannelEnd(NoteKind kind) => kind == NoteKind.ChannelEnd;

    /// <summary>Whether the active character can use this special note.</summary>
    public static bool ActiveFor(NoteKind kind, CharacterKind? character) => kind switch
    {
        NoteKind.WeaponSwitch => character == CharacterKind.Armed,
        NoteKind.Channel => character == CharacterKind.SplitLine,
        NoteKind.Construction => character == CharacterKind.Form,
        NoteKind.ConstructionStep => character == CharacterKind.Form,
        _ => true,
    };

    /// <summary>Kinds that move the player and can be claimed by any key.</summary>
    public static bool IsMovement(NoteKind kind) => kind is
        NoteKind.Dash or
        NoteKind.Momentum;

    /// <summary>Whether this press is the key or pair the note asks for.</summary>
    public static bool Matches(Note note, KeybindAction action) =>
        note.Kind switch
        {
            NoteKind.Pair =>
                HasPairIndex(action) && note.PairIndex == PairIndexFor(action),
            NoteKind.WeaponSwitch => IsLeft(action) || IsRight(action),
            _ => note.Target == action,
        };

    /// <summary>The pair a side key belongs to; M has no pair.</summary>
    public static PairIndex PairIndexFor(KeybindAction action) => action switch
    {
        KeybindAction.L1 or KeybindAction.R1 => PairIndex.One,
        KeybindAction.L2 or KeybindAction.R2 => PairIndex.Two,
        KeybindAction.L3 or KeybindAction.R3 => PairIndex.Three,
        _ => PairIndex.One,
    };

    /// <summary>Whether the action belongs to a pair at all.</summary>
    public static bool HasPairIndex(KeybindAction action) =>
        action != KeybindAction.M;

    /// <summary>The canonical left key for a pair number, used by the editor.</summary>
    public static KeybindAction ActionFor(PairIndex index) => index switch
    {
        PairIndex.Two => KeybindAction.L2,
        PairIndex.Three => KeybindAction.L3,
        _ => KeybindAction.L1,
    };

    /// <summary>The side a key belongs to; M has no side.</summary>
    public static ChannelSide SideOf(KeybindAction action) =>
        IsLeft(action) ? ChannelSide.Left : ChannelSide.Right;

    /// <summary>The key for a side and pair number.</summary>
    public static KeybindAction ActionFor(ChannelSide side, PairIndex index) =>
        side == ChannelSide.Left
            ? ActionFor(index)
            : index switch
            {
                PairIndex.Two => KeybindAction.R2,
                PairIndex.Three => KeybindAction.R3,
                _ => KeybindAction.R1,
            };

    /// <summary>Whether the action is a left-side key.</summary>
    public static bool IsLeft(KeybindAction action) => action is
        KeybindAction.L1 or KeybindAction.L2 or KeybindAction.L3;

    /// <summary>Whether the action is a right-side key.</summary>
    public static bool IsRight(KeybindAction action) => action is
        KeybindAction.R1 or KeybindAction.R2 or KeybindAction.R3;
}
