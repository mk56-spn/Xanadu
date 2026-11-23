using System.Collections.Generic;
using Xanadu.Ui.Systems.Input;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Systems.Timeline;

namespace Xanadu.Tools.Animator.Ui;

/// <summary>
/// The rows of the SHORTCUTS sheet, made from the live bindings and their
/// tooltips so the sheet can never drift from what the keys do. Timeline keys
/// come first, then animating, then what only works while EDIT RIG is on.
/// </summary>
public static class AnimatorShortcutSheet
{
    /// <summary>One sheet row: a heading (no keys) or the keys and what they do.</summary>
    public readonly record struct Row(string Keys, string Text, bool Heading = false);

    /// <summary>Longest description shown; the tooltip on the button has the rest.</summary>
    public const int MaxText = 52;

    public static List<Row> Rows()
    {
        var rows = new List<Row> { new("", "TIMELINE", Heading: true) };
        rows.Add(new("Left / Right", "Step one snap back or forward"));
        foreach ((KeyChord chord, TimelineCommand command) in TimelineShortcuts.All)
            rows.Add(new(Name(chord.Key, chord.Ctrl, chord.Shift), Timeline(command)));

        var animate = new List<Row>();
        var rig = new List<Row>();
        var seen = new Dictionary<(ToolActionKind, string), int>();
        foreach (AnimatorBindings.Binding binding in AnimatorBindings.All)
        {
            List<Row> into = AnimatorCommands.ChangesRig(binding.Action.Kind) ? rig : animate;
            string keys = Name(binding.Key, binding.Ctrl, binding.Shift);
            var id = (binding.Action.Kind, binding.Action.Argument ?? "");
            if (seen.TryGetValue(id, out int at) && into.Count > at && into[at].Text == Describe(binding.Action))
            {
                into[at] = into[at] with { Keys = $"{into[at].Keys} / {keys}" };
                continue;
            }
            seen[id] = into.Count;
            into.Add(new(keys, Describe(binding.Action)));
        }

        rows.Add(new("", "ANIMATING", Heading: true));
        rows.AddRange(animate);
        rows.Add(new("", "EDIT RIG ONLY", Heading: true));
        rows.AddRange(rig);
        return rows;
    }

    /// <summary>The tooltip without its trailing "(keys)", cut to fit a sheet column.</summary>
    private static string Describe(ToolAction action)
    {
        string text = AnimatorTooltips.For(action.Kind, action.Argument ?? "") ?? action.Kind.ToString();
        int open = text.LastIndexOf(" (");
        if (open > 0 && text.EndsWith(')'))
            text = text[..open];
        return text.Length <= MaxText ? text : text[..(MaxText - 1)].TrimEnd() + "…";
    }

    private static string Timeline(TimelineCommand command) => command switch
    {
        TimelineCommand.TogglePlay => "Play or pause",
        TimelineCommand.StepBack => "Step one snap back",
        TimelineCommand.StepForward => "Step one snap forward",
        TimelineCommand.SeekBack => "Back one second",
        TimelineCommand.SeekForward => "Forward one second",
        TimelineCommand.PreviousKey => "Jump to the previous key",
        TimelineCommand.NextKey => "Jump to the next key",
        TimelineCommand.Start => "Go to the start",
        _ => "Go to the end",
    };

    /// <summary>A chord as it is printed on the sheet, e.g. "Ctrl+Shift+[".</summary>
    public static string Name(Key key, bool ctrl, bool shift) =>
        (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + KeyName(key);

    private static string KeyName(Key key) => key switch
    {
        Key.Bracketleft => "[",
        Key.Bracketright => "]",
        Key.Comma => ",",
        Key.Period => ".",
        Key.Minus => "-",
        Key.Equal => "=",
        Key.Semicolon => ";",
        Key.Apostrophe => "'",
        Key.Slash => "/",
        Key.Delete => "Del",
        Key.Escape => "Esc",
        Key.Pageup => "PgUp",
        Key.Pagedown => "PgDn",
        >= Key.Key0 and <= Key.Key9 => ((int)(key - Key.Key0)).ToString(),
        _ => key.ToString(),
    };
}
