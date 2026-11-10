using Friflo.Engine.ECS;
using Xanadu.Systems.Ui;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>The unsaved-changes flag a shell header shows.</summary>
public readonly record struct ShellStatusLabels(Entity Flag);

/// <summary>
/// The status every editor shell shows: an unsaved-changes flag in its header,
/// and the last action's message (undo, redo, saved…) as a toast, wide enough
/// to read all of it. The level editor and the animator both go through here,
/// so both read the same.
/// </summary>
public static class ShellStatus
{
    public const float FlagWidth = 96f;
    public const string UnsavedFlag = "● UNSAVED";

    public static readonly Color FlagColor = Palette.Accent;

    /// <summary>Adds the flag to a header row, one control tall.</summary>
    public static ShellStatusLabels Add(UiNode row, float height)
    {
        Entity flag = row.Text("", UiTheme.SmallFontSize, FlagColor, TextAlign.Left)
            .Size(FlagWidth, height)
            .Entity;
        return new ShellStatusLabels(flag);
    }

    /// <summary>
    /// Toasts the message when its serial has moved on since <paramref name="shown"/>,
    /// so repeating the same action shows it again; returns the serial now shown.
    /// </summary>
    public static int Show(ToastSystem? toasts, string? message, int serial, int shown)
    {
        if (serial == shown)
            return shown;
        toasts?.ShowStatus((message ?? "").ToUpperInvariant());
        return serial;
    }

    public static void ShowUnsaved(ShellStatusLabels labels, bool unsaved, CommandBuffer? buffer = null)
    {
        if (labels.Flag.IsNull || !labels.Flag.HasComponent<Text>())
            return;
        UiText.Set(labels.Flag, unsaved ? UnsavedFlag : "", buffer);
    }
}
