using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>The two labels a shell header shows its status with, and how wide the message may run.</summary>
public readonly record struct ShellStatusLabels(Entity Message, Entity Flag, float MessageWidth);

/// <summary>
/// The status every editor shell shows in its header: the last action's
/// message (undo, redo, saved…) in lime, and an unsaved-changes flag beside it.
/// The level editor and the animator build and update it through here, so
/// both read the same.
/// </summary>
public static class ShellStatus
{
    public const float MessageWidth = 240f;
    public const float FlagWidth = 96f;
    public const string UnsavedFlag = "● UNSAVED";

    public static readonly Color MessageColor = Palette.Lime;
    public static readonly Color FlagColor = Palette.Accent;

    /// <summary>
    /// Adds the flag and then the message to a header row, each one control
    /// tall; the message gets <paramref name="messageWidth"/>, as much as the
    /// shell's header can spare.
    /// </summary>
    public static ShellStatusLabels Add(UiNode row, float height, float messageWidth = MessageWidth)
    {
        Entity flag = row.Text("", UiTheme.SmallFontSize, FlagColor, TextAlign.Left)
            .Size(FlagWidth, height)
            .Entity;
        Entity message = row.Text("", UiTheme.FontSize, MessageColor, TextAlign.Left)
            .Size(messageWidth, height)
            .Entity;
        return new ShellStatusLabels(message, flag, messageWidth);
    }

    /// <summary>How a message is shown: in capitals, cut to fit <paramref name="width"/>.</summary>
    public static string Format(string? message, float width = MessageWidth) =>
        UiTextFit.Cut((message ?? "").ToUpperInvariant(), UiTheme.FontSize, width);

    public static void Show(ShellStatusLabels labels, string? message, CommandBuffer? buffer = null) =>
        Set(labels.Message, Format(message, labels.MessageWidth), buffer);

    public static void ShowUnsaved(ShellStatusLabels labels, bool unsaved, CommandBuffer? buffer = null) =>
        Set(labels.Flag, unsaved ? UnsavedFlag : "", buffer);

    private static void Set(Entity label, string value, CommandBuffer? buffer)
    {
        if (label.IsNull || !label.HasComponent<Text>())
            return;
        UiText.Set(label, value, buffer);
    }
}
