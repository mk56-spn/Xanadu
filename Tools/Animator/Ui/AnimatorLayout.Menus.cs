using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tools.Animator;

/// <summary>The animator's floating menus and the sizes their systems place them by.</summary>
public static partial class AnimatorLayout
{
    /// <summary>The file menu floats above the panels, so it lives on the menu layer.</summary>
    private static void BuildFileMenu(UiBuilder menu, Entity fileButton) =>
        BuildActionMenu(menu, fileButton, new[]
        {
            ("SAVE", ToolActionKind.Save),
            ("LOAD", ToolActionKind.Load),
            ("NEW", ToolActionKind.NewSkeleton),
            ("NEW CLIP…", ToolActionKind.OpenNewClip),
        });

    /// <summary>
    /// The CONFIG menu holds the clip edits that are not part of playing or
    /// previewing: its length and deleting it.
    /// </summary>
    private static void BuildConfigMenu(UiBuilder menu, Entity configButton) =>
        BuildActionMenu(menu, configButton, new[]
        {
            ("LENGTH…", ToolActionKind.OpenClipLength),
            ("DELETE", ToolActionKind.DeleteClip),
        });

    /// <summary>
    /// The clip length panel starts hidden; <see cref="Systems.AnimatorClipConfigSystem"/>
    /// places it under the CONFIG button and closes it on any click outside.
    /// Six arrows step the length by 0.1 s, 1 s and 10 s in either direction.
    /// </summary>
    private static void BuildClipLengthPanel(UiBuilder menu, ref Refs refs)
    {
        float inner = ClipLengthSize.X - ClipLengthPadding * 2;
        menu.Container(UiLayout.Hidden, ClipLengthSize, StackDirection.Vertical,
            spacing: 6, color: UiTheme.Panel, padding: ClipLengthPadding,
            takesInput: true, cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        refs.Ui.ClipLengthPanel = menu.Current;
        menu.Text("CLIP LENGTH", new Vector2(inner, 18), UiTheme.SmallFontSize,
            UiTheme.Accent, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(inner, ClipLengthRowHeight),
            spacing: ClipLengthSpacing, color: Clear,
            cross: StackCrossAlignment.Center);
        for (int arrows = 3; arrows >= 1; arrows--)
            LengthStepButton(menu, shorter: true, arrows);
        refs.Ui.ClipLengthField = NumericField(menu,
            new Vector2(ClipLengthFieldWidth, ClipLengthRowHeight));
        refs.Ui.ClipLengthField.AddComponent(new UiTooltip("Clip length in seconds"));
        for (int arrows = 1; arrows <= 3; arrows++)
            LengthStepButton(menu, shorter: false, arrows);
        menu.End();
        menu.End();
    }

    /// <summary>One arrow step: 1, 2 or 3 arrows change the length by 0.1, 1 or 10 s.</summary>
    private static Entity LengthStepButton(UiBuilder menu, bool shorter, int arrows)
    {
        float seconds = Commands.AnimatorClipCommands.LengthSteps[arrows - 1];
        Entity button = menu.Button(
            new string(shorter ? '◀' : '▶', arrows),
            new Vector2(ClipLengthArrowWidth, ClipLengthRowHeight),
            fontSize: UiTheme.SmallFontSize);
        button.AddComponent(new ToolAction(
            shorter ? ToolActionKind.DurationDown : ToolActionKind.DurationUp,
            seconds.ToString(CultureInfo.InvariantCulture)));
        button.AddComponent(new UiTooltip(
            $"{(shorter ? "Shorten" : "Lengthen")} the clip by {seconds:0.###} s"));
        return button;
    }

    /// <summary>
    /// The NEW CLIP dialog and the dimmer behind it. Both start hidden;
    /// <see cref="Systems.AnimatorNewClipDialogSystem"/> centres them and
    /// <see cref="Commands.AnimatorNewClipDialogCommands"/> creates the clip.
    /// </summary>
    private static void BuildNewClipDialog(UiBuilder menu, Vector2 screen, ref Refs refs)
    {
        Entity backdrop = menu.RectEntity(
            UiLayout.Hidden,
            screen,
            Palette.WithAlpha(Palette.Stage, 0.6f),
            takesInput: true);
        backdrop.AddTag<UiOverlay>();
        refs.Ui.NewClipBackdrop = backdrop;

        float inner = NewClipDialogSize.X - NewClipPadding * 2;
        float fieldWidth = inner - NewClipLabelWidth - NewClipSpacing;
        menu.Container(UiLayout.Hidden, NewClipDialogSize, StackDirection.Vertical,
            spacing: NewClipSpacing, color: UiTheme.Panel, padding: NewClipPadding,
            takesInput: true, cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        refs.Ui.NewClipDialog = menu.Current;
        refs.Ui.NewClipDialog.AddTag<UiOverlayItem>();
        menu.Text("NEW CLIP", new Vector2(inner, 20), UiTheme.FontSize,
            UiTheme.Accent, TextAlign.Left);

        refs.Ui.NewClipDuplicate = menu.Button(
            "DUPLICATE CURRENT OFF",
            new Vector2(inner, NewClipRowHeight),
            fontSize: UiTheme.SmallFontSize);
        refs.Ui.NewClipDuplicate.AddComponent(new Toggle(
            "DUPLICATE CURRENT ON",
            "DUPLICATE CURRENT OFF",
            false));
        refs.Ui.NewClipDuplicate.AddComponent(new UiTooltip(
            "Copy the current clip's keys into the new clip"));

        DialogFieldRow(menu, "NAME", inner, fieldWidth, out Entity name);
        refs.Ui.NewClipName = name;
        DialogFieldRow(menu, "LENGTH", inner, fieldWidth, out Entity length);
        refs.Ui.NewClipLength = length;
        refs.Ui.NewClipLength.GetComponent<TextField>().Numeric = true;
        refs.Ui.NewClipLength.AddComponent(new UiTooltip("Length in seconds"));

        refs.Ui.NewClipError = menu.Text("", new Vector2(inner, 16), 11,
            Palette.Danger, TextAlign.Left);

        float buttonWidth = (inner - 12f) / 2;
        var buttonSize = new Vector2(buttonWidth, NewClipButtonHeight);
        menu.Container(Vector2.Zero, new Vector2(inner, NewClipButtonHeight),
            spacing: 12, color: Clear);
        Action(menu, "CANCEL", ToolActionKind.CloseNewClip, buttonSize,
            UiTheme.SmallFontSize);
        Action(menu, "CREATE CLIP", ToolActionKind.CreateClip, buttonSize,
            UiTheme.SmallFontSize);
        menu.End();
        menu.End();
    }

    /// <summary>A label and text field on one row of the NEW CLIP dialog.</summary>
    private static void DialogFieldRow(
        UiBuilder menu,
        string label,
        float width,
        float fieldWidth,
        out Entity field)
    {
        menu.Container(Vector2.Zero, new Vector2(width, NewClipRowHeight),
            spacing: NewClipSpacing, color: Clear,
            cross: StackCrossAlignment.Center);
        menu.Text(label, new Vector2(NewClipLabelWidth, 20), 11, UiTheme.TextDim,
            TextAlign.Left);
        field = TextInput(menu, new Vector2(fieldWidth, NewClipRowHeight));
        menu.End();
    }

    /// <summary>
    /// A header button that drops a list of <see cref="ToolAction"/> options.
    /// The list comes from <see cref="UiBuilder.DropdownList"/>, which keeps it
    /// out of the layout pass; without that the container layout collapses it.
    /// </summary>
    private static void BuildActionMenu(
        UiBuilder menu,
        Entity button,
        (string Label, ToolActionKind Kind)[] entries)
    {
        var labels = new string[entries.Length];
        for (int i = 0; i < entries.Length; i++)
            labels[i] = entries[i].Label;

        UiDropdownControl dropdown = menu.DropdownList(button, labels);
        for (int i = 0; i < entries.Length; i++)
        {
            dropdown.Options[i].AddComponent(new ToolAction(entries[i].Kind));
            AnimatorTooltips.Attach(dropdown.Options[i], entries[i].Kind);
        }
    }

    /// <summary>Size of the transform inspector, which the chain inspector shares the spot of.</summary>
    private const float InspectorWidth = 236f;
    private const float InspectorHeight = 222f;

    /// <summary>Top-left of the floating inspectors: the preview's top-right corner.</summary>
    public static Vector2 InspectorTopLeft(Vector2 screen) =>
        new(
            screen.X - Margin - RightWidth - Gap - InspectorWidth - 12f,
            Margin + TopHeight + Gap + 12f);

    /// <summary>Where the transform inspector is centred when it is shown.</summary>
    public static Vector2 InspectorCenter(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(InspectorWidth, InspectorHeight) / 2;

    /// <summary>Size of the bone context menu opened by right-clicking a bone label.</summary>
    public static readonly Vector2 BoneMenuSize = new(150, 6 * 28 + 5 * 2 + 8);

    public static readonly Vector2 BoneNameSize = new(140, 22);

    /// <summary>Size of the key menu opened by right-clicking a timeline track row.</summary>
    public static readonly Vector2 KeyMenuSize = new(150, 3 * 28 + 2 * 2 + 8);

    /// <summary>Width of the ADD SVG picker; its height follows the number of choices.</summary>
    public const float SvgMenuWidth = 240f;

    /// <summary>Most choices the picker shows at once; the rest scroll.</summary>
    public const int SvgMenuRows = 10;

    public const float SvgRowHeight = 24f;

    /// <summary>Height of the picker for <paramref name="rows"/> rows: rows, gaps and padding.</summary>
    public static float SvgMenuHeight(int rows) =>
        Math.Max(1, rows) * SvgRowHeight + (Math.Max(1, rows) - 1) * 2 + 8;

    /// <summary>
    /// The bone context menu and its rename field float above the panels and
    /// start hidden; the bone menu system moves them into place.
    /// </summary>
    private static void BuildBoneMenu(UiBuilder menu, ref Refs refs)
    {
        (string Label, ToolActionKind Kind)[] entries =
        {
            ("RENAME", ToolActionKind.RenameBone),
            ("REPARENT", ToolActionKind.ReparentBone),
            ("CHILD", ToolActionKind.AddBone),
            ("SVG", ToolActionKind.AddSvg),
            ("CHAIN", ToolActionKind.AddChain),
            ("DEL", ToolActionKind.DeleteBone),
        };

        menu.Container(UiLayout.Hidden, BoneMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        refs.Ui.BoneMenu = menu.Current;
        foreach ((string label, ToolActionKind kind) in entries)
        {
            Entity option = menu.Button(label, new Vector2(BoneMenuSize.X - 8, 28),
                fontSize: UiTheme.SmallFontSize);
            option.AddComponent(new ToolAction(kind));
            AnimatorTooltips.Attach(option, kind);
        }
        menu.End();

        refs.Ui.BoneNameField = TextInput(menu, BoneNameSize);
        refs.Ui.BoneNameField.GetComponent<Position>().Value = UiLayout.Hidden;
    }

    /// <summary>
    /// The ADD SVG picker starts hidden with an empty scrolling list; the SVG
    /// menu system fills it with the project's groups and moves it into place.
    /// </summary>
    private static void BuildSvgMenu(UiBuilder menu, ref Refs refs)
    {
        var size = new Vector2(SvgMenuWidth, SvgMenuHeight(SvgMenuRows));
        menu.Container(UiLayout.Hidden, size, StackDirection.Vertical,
            color: UiTheme.Panel, padding: 4, takesInput: true, clip: true,
            main: StackMainAlignment.Start);
        refs.Ui.SvgMenu = menu.Current;
        menu.Container(Vector2.Zero, new Vector2(SvgMenuWidth - 8, 4),
            StackDirection.Vertical, spacing: 2, color: Clear,
            cross: StackCrossAlignment.Start, autoHeight: true,
            main: StackMainAlignment.Start);
        refs.Ui.SvgMenuList = menu.Current;
        menu.End();
        refs.Ui.SvgMenu.AddComponent(new ScrollView(refs.Ui.SvgMenuList));
        menu.End();
    }

    /// <summary>
    /// A floating explanation for the hovered button. It sits on the menu
    /// layer, last, so it draws over menus; the tooltip system sizes it.
    /// </summary>
    private static void BuildTooltip(
        EntityStore store,
        IVisualRidFactory? visuals,
        ref Refs refs)
    {
        UiBuilder tip = UiBuilder.Begin(store, visuals).Layer(TooltipLayer);
        Entity tooltip = tip.RectEntity(
            UiLayout.Hidden,
            new Vector2(200, 24),
            Palette.SurfaceAlt,
            "",
            takesInput: false);
        tooltip.AddComponent(new TextStyle(11, UiTheme.Text));
        tooltip.AddComponent(new TextAlignment(TextAlign.Center));
        tip.End();
        refs.Ui.Tooltip = tooltip;
    }

    /// <summary>The key menu starts hidden; the key menu system moves it into place.</summary>
    private static void BuildKeyMenu(UiBuilder menu, ref Refs refs)
    {
        menu.Container(UiLayout.Hidden, KeyMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        refs.Ui.KeyMenu = menu.Current;
        var size = new Vector2(KeyMenuSize.X - 8, 28);
        refs.Ui.KeyMenuAdd = menu.Button("ADD KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuAdd.AddComponent(new ToolAction(ToolActionKind.AddKey));
        AnimatorTooltips.Attach(refs.Ui.KeyMenuAdd, ToolActionKind.AddKey);
        refs.Ui.KeyMenuDelete = menu.Button("DEL KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuDelete.AddComponent(new ToolAction(ToolActionKind.DeleteKey));
        AnimatorTooltips.Attach(refs.Ui.KeyMenuDelete, ToolActionKind.DeleteKey);
        Entity selectAll = menu.Button("SELECT ALL", size, fontSize: UiTheme.SmallFontSize);
        selectAll.AddComponent(new ToolAction(ToolActionKind.SelectAllKeys));
        AnimatorTooltips.Attach(selectAll, ToolActionKind.SelectAllKeys);
        menu.End();
    }

    /// <summary>
    /// The transform inspector floats over the preview's top-right corner: the
    /// selected bone or attachment's position, rotation and scale, plus reset.
    /// </summary>
    private static void BuildInspector(UiBuilder menu, Vector2 screen, ref Refs refs)
    {
        const float width = InspectorWidth;
        const float height = InspectorHeight;
        const float row = 26f;
        const float inner = width - 16f;
        var size = new Vector2(width, height);
        Vector2 topLeft = InspectorTopLeft(screen);

        menu.Container(topLeft + size / 2, size, StackDirection.Vertical,
            spacing: 6, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, main: StackMainAlignment.Start);
        refs.Ui.Inspector = menu.Current;
        refs.Ui.InspectorTitle = menu.Text("NOTHING SELECTED",
            new Vector2(inner, 18), UiTheme.SmallFontSize, UiTheme.Accent, TextAlign.Left);

        menu.Container(Vector2.Zero, new Vector2(inner, row), spacing: 4, color: Clear);
        menu.Text("POS", new Vector2(48, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.InspectPosX = NumericField(menu, new Vector2(64, 22));
        refs.Ui.InspectPosY = NumericField(menu, new Vector2(64, 22));
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, row), spacing: 4, color: Clear);
        menu.Text("ROT °", new Vector2(48, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.InspectRotation = NumericField(menu, new Vector2(72, 22));
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, row), spacing: 4, color: Clear);
        menu.Text("SCALE", new Vector2(48, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.InspectScaleX = NumericField(menu, new Vector2(64, 22));
        refs.Ui.InspectScaleY = NumericField(menu, new Vector2(64, 22));
        menu.End();

        menu.Container(Vector2.Zero, new Vector2(inner, row), spacing: 4, color: Clear);
        menu.Text("LAYER", new Vector2(48, 20), 11, UiTheme.TextDim, TextAlign.Left);
        refs.Ui.InspectLayer = NumericField(menu, new Vector2(64, 22));
        refs.Ui.InspectLayer.AddComponent(new UiTooltip("Draw order of the SVG art: higher layers draw in front"));
        menu.End();

        Entity reset = menu.Button("RESET", new Vector2(inner, row),
            fontSize: UiTheme.SmallFontSize);
        reset.AddComponent(new ToolAction(ToolActionKind.ResetTransform));
        AnimatorTooltips.Attach(reset, ToolActionKind.ResetTransform);
        menu.End();
    }

    /// <summary>A text field that only accepts the characters a number needs.</summary>
    private static Entity NumericField(UiBuilder menu, Vector2 size)
    {
        Entity field = TextInput(menu, size);
        field.GetComponent<TextField>().Numeric = true;
        return field;
    }
}
