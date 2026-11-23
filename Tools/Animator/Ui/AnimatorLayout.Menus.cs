using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Ui;

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
            ("FILE INFO…", ToolActionKind.OpenInfo),
            ("CLOSE EDITOR", ToolActionKind.CloseEditor),
        });

    /// <summary>
    /// The CONFIG menu holds the clip edits that are not part of playing or
    /// previewing (its length and deleting it), the file's info, and closing
    /// the animator.
    /// </summary>
    private static void BuildConfigMenu(UiBuilder menu, Entity configButton) =>
        BuildActionMenu(menu, configButton, new[]
        {
            ("NEW CLIP…", ToolActionKind.OpenNewClip),
            ("LENGTH…", ToolActionKind.OpenClipLength),
            ("CLEAR KEYS", ToolActionKind.ClearClipKeys),
            ("DELETE CLIP", ToolActionKind.DeleteClip),
        });

    /// <summary>
    /// The clip length panel starts hidden; <see cref="Systems.AnimatorClipConfigSystem"/>
    /// places it under the CONFIG button and closes it on any click outside.
    /// Six arrows step the length by 0.1 s, 1 s and 10 s in either direction.
    /// </summary>
    private static void BuildClipLengthPanel(UiBuilder menu, ref Refs refs)
    {
        float inner = ClipLengthSize.X - ClipLengthPadding * 2;
        menu.Container(Vector2.Zero, ClipLengthSize, StackDirection.Vertical,
            spacing: 6, color: UiTheme.Panel, padding: ClipLengthPadding,
            takesInput: true, cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
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
        button.AddComponent(ToolAction.Of(
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
            Vector2.Zero,
            screen,
            Palette.WithAlpha(Palette.Stage, 0.6f),
            takesInput: true);
        UiLayout.SetHidden(backdrop, true);
        backdrop.AddTag<UiOverlay>();
        refs.Ui.NewClipBackdrop = backdrop;

        float inner = NewClipDialogSize.X - NewClipPadding * 2;
        float fieldWidth = inner - NewClipLabelWidth - NewClipSpacing;
        menu.Container(Vector2.Zero, NewClipDialogSize, StackDirection.Vertical,
            spacing: NewClipSpacing, color: UiTheme.Panel, padding: NewClipPadding,
            takesInput: true, cross: StackCrossAlignment.Start,
            main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
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
            dropdown.Options[i].AddComponent(ToolAction.Of(entries[i].Kind));
            AnimatorTooltips.Attach(dropdown.Options[i], entries[i].Kind);
        }
    }

    /// <summary>Size of the transform inspector, which the chain inspector shares the spot of.</summary>
    private const float InspectorWidth = 236f;
    /// <summary>
    /// Title, subtitle, five field and button rows and the two sliders, with
    /// the spacing between them and the padding round them, so the bone card
    /// can hang straight under it.
    /// </summary>
    private const float InspectorHeight =
        InspectorTitleHeight + InspectorSubtitleHeight + 5 * 26f + 2 * InspectorSliderRow + 8 * 6f + 16f;

    private const float InspectorTitleHeight = 26f;
    private const float InspectorSubtitleHeight = 16f;
    private const int InspectorTitleFontSize = 20;

    /// <summary>Top-left of the floating inspectors: the preview's top-right corner.</summary>
    public static Vector2 InspectorTopLeft(Vector2 screen) =>
        new(
            screen.X - Margin - RightPanelWidth - Gap - InspectorWidth - 12f,
            Margin + TopHeight + Gap + 12f);

    /// <summary>Where the XIF editor sits when it is open: over the whole window below the header, with a margin.</summary>
    public static Rect2 XifEditorArea(Vector2 screen)
    {
        const float inset = 40f;
        var corner = new Vector2(inset, TopHeight + inset / 2f);
        return new Rect2(corner, new Vector2(screen.X - inset * 2f, screen.Y - corner.Y - inset));
    }

    /// <summary>Where the particle editor is centred while a particle effect is selected: in the XIF editor's place.</summary>
    public static Vector2 ParticleEditorCenter(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(-8f - ParticleEditorPanel.Width / 2, ParticleEditorPanel.Height / 2);

    /// <summary>Where the transform inspector is centred when it is shown.</summary>
    public static Vector2 InspectorCenter(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(InspectorWidth, InspectorHeight) / 2;

    /// <summary>Size of the bone context menu opened by right-clicking a bone label.</summary>
    public static readonly Vector2 BoneMenuSize = new(150, 6 * 28 + 5 * 2 + 8);

    public static readonly Vector2 BoneNameSize = new(140, 22);

    /// <summary>Size of the key menu opened by right-clicking a timeline track row.</summary>
    public static readonly Vector2 KeyMenuSize = new(150, 3 * 28 + 2 * 2 + 8);

    /// <summary>Width of a picker panel beside the inspector.</summary>
    public const float PickerWidth = 240f;

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
            ("PART", ToolActionKind.AddPart),
            ("CHAIN", ToolActionKind.AddChain),
            ("DEL", ToolActionKind.DeleteBone),
        };

        menu.Container(Vector2.Zero, BoneMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.BoneMenu = menu.Current;
        foreach ((string label, ToolActionKind kind) in entries)
        {
            Entity option = menu.Button(label, new Vector2(BoneMenuSize.X - 8, 28),
                fontSize: UiTheme.SmallFontSize);
            option.AddComponent(ToolAction.Of(kind));
            AnimatorTooltips.Attach(option, kind);
        }
        menu.End();

        refs.Ui.BoneNameField = TextInput(menu, BoneNameSize);
        UiLayout.SetHidden(refs.Ui.BoneNameField, true);

        // The menu for a part, chain or cloth row of the rig tree: the bone
        // menu system writes the delete that fits the row on its one button.
        menu.Container(Vector2.Zero, RowMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.RowMenu = menu.Current;
        refs.Ui.RowMenuDelete = menu.Button("DELETE", new Vector2(RowMenuSize.X - 8, 28),
            fontSize: UiTheme.SmallFontSize);
        refs.Ui.RowMenuDelete.AddComponent(ToolAction.Of(ToolActionKind.DeleteChain));
        menu.End();
    }

    /// <summary>Size of the menu a part, chain or cloth row of the rig tree opens on right-click.</summary>
    public static readonly Vector2 RowMenuSize = new(150, 28 + 8);

    /// <summary>
    /// A floating explanation for the hovered button. It sits on the menu
    /// layer, last, so it draws over menus; the tooltip system sizes it.
    /// </summary>
    private static void BuildTooltip(
        EntityStore store,
        IVisualItemFactory? visuals,
        ref Refs refs)
    {
        UiBuilder tip = UiBuilder.Begin(store, visuals).Layer(UiLayers.AnimatorTooltip);
        Entity tooltip = tip.RectEntity(
            Vector2.Zero,
            new Vector2(200, 24),
            Palette.SurfaceAlt,
            "",
            takesInput: false);
        UiLayout.SetHidden(tooltip, true);
        tooltip.AddComponent(new TextStyle(11, UiTheme.Text));
        tooltip.AddComponent(new TextAlignment(TextAlign.Center));
        tip.End();
        refs.Ui.Tooltip = tooltip;
    }

    /// <summary>The key menu starts hidden; the key menu system moves it into place.</summary>
    private static void BuildKeyMenu(UiBuilder menu, ref Refs refs)
    {
        menu.Container(Vector2.Zero, KeyMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.KeyMenu = menu.Current;
        var size = new Vector2(KeyMenuSize.X - 8, 28);
        refs.Ui.KeyMenuAdd = menu.Button("ADD KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuAdd.AddComponent(ToolAction.Of(ToolActionKind.AddKey));
        AnimatorTooltips.Attach(refs.Ui.KeyMenuAdd, ToolActionKind.AddKey);
        refs.Ui.KeyMenuDelete = menu.Button("DEL KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuDelete.AddComponent(ToolAction.Of(ToolActionKind.DeleteKey));
        AnimatorTooltips.Attach(refs.Ui.KeyMenuDelete, ToolActionKind.DeleteKey);
        Entity selectAll = menu.Button("SELECT ALL", size, fontSize: UiTheme.SmallFontSize);
        selectAll.AddComponent(ToolAction.Of(ToolActionKind.SelectAllKeys));
        AnimatorTooltips.Attach(selectAll, ToolActionKind.SelectAllKeys);
        menu.End();
    }

    /// <summary>Size of the menu opened by right-clicking an override's bar.</summary>
    public static readonly Vector2 OverrideMenuSize = new(150, 2 * 28 + 2 + 8);

    /// <summary>The override menu starts hidden; the key menu system opens it on an override's bar.</summary>
    private static void BuildOverrideMenu(UiBuilder menu, ref Refs refs)
    {
        menu.Container(Vector2.Zero, OverrideMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.OverrideMenu = menu.Current;
        var size = new Vector2(OverrideMenuSize.X - 8, 28);
        Action(menu, "REMOVE", ToolActionKind.RemoveOverride, size, UiTheme.SmallFontSize);
        Action(menu, "KEEP KEYS", ToolActionKind.DetachOverride, size, UiTheme.SmallFontSize);
        menu.End();
    }

    /// <summary>
    /// The transform inspector, first card of the SELECTED tab: the selected
    /// bone or attachment's position, rotation and scale, plus reset.
    /// </summary>
    private static void BuildInspector(UiBuilder menu, ref Refs refs)
    {
        const float width = InspectorWidth;
        const float height = InspectorHeight;
        const float row = 26f;
        const float inner = width - 16f;
        var size = new Vector2(width, height);

        menu.Container(Vector2.Zero, size, StackDirection.Vertical,
            spacing: 6, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, autoHeight: true, main: StackMainAlignment.Start);
        refs.Ui.Inspector = menu.Current;
        refs.Ui.InspectorTitle = menu.Text("NOTHING SELECTED",
            new Vector2(inner, InspectorTitleHeight), InspectorTitleFontSize, UiTheme.Accent, TextAlign.Left);
        refs.Ui.InspectorSubtitle = menu.Text("",
            new Vector2(inner, InspectorSubtitleHeight), UiTheme.SmallFontSize, UiTheme.TextDim, TextAlign.Left);

        refs.Ui.InspectPosX = Field(menu, "POSITION", new AnimatorValueBinding(AnimatorFieldKind.Position),
            UiValueShape.Vector(2), [0f, 0f], (-200f, 200f), "Where the selection sits, in its parent's space", inner);
        refs.Ui.InspectRotation = Field(menu, "ROTATION °", new AnimatorValueBinding(AnimatorFieldKind.Rotation),
            UiValueShape.Float, [0f], (-180f, 180f), "How far the selection is turned", inner);
        refs.Ui.InspectScaleX = Field(menu, "SCALE", new AnimatorValueBinding(AnimatorFieldKind.Scale),
            UiValueShape.Vector(2), [1f, 1f], (0.1f, 3f), "The selection's stretch along and across", inner);
        refs.Ui.InspectLayer = Field(menu, "LAYER", new AnimatorValueBinding(AnimatorFieldKind.Layer),
            UiValueShape.Int, [0f], null, "Draw order of the art: higher layers draw in front", inner);

        Entity reset = menu.Button("RESET", new Vector2(inner, row),
            fontSize: UiTheme.SmallFontSize);
        reset.AddComponent(ToolAction.Of(ToolActionKind.ResetTransform));
        AnimatorTooltips.Attach(reset, ToolActionKind.ResetTransform);
        menu.End();
    }

    private const float InspectorSliderRow = UiNode.ControlRow;

    /// <summary>Width of the bone card, which hangs under the inspector as one panel with it.</summary>
    private const float BoneCardInner = InspectorWidth - 16f;

    /// <summary>Subheads, slider rows and button rows of the bone card, with its spacing and padding.</summary>
    public static readonly Vector2 BoneCardSize = new(
        InspectorWidth,
        3 * CaptionHeight + 3 * RowHeight + 5 * RowHeight + 10 * 6f + 16f);

    /// <summary>Where the bone card is centred: straight under the transform inspector.</summary>
    public static Vector2 BoneCardCenter(Vector2 screen) =>
        InspectorTopLeft(screen) + new Vector2(0f, InspectorHeight) + BoneCardSize / 2;

    /// <summary>
    /// Everything about the selected bone that is not its transform: its depth
    /// and key, how its joint may move, and adding, renaming and removing it.
    /// It starts hidden; <see cref="Systems.AnimatorBoneCardSystem"/> shows it
    /// under the inspector while a bone is selected.
    /// </summary>
    private static void BuildBoneCard(UiBuilder menu, ref Refs refs)
    {
        const float inner = BoneCardInner;
        menu.Container(Vector2.Zero, BoneCardSize, StackDirection.Vertical,
            spacing: 6, color: UiTheme.Panel, padding: 8, takesInput: true,
            cross: StackCrossAlignment.Start, autoHeight: true, main: StackMainAlignment.Start);
        UiLayout.SetHidden(menu.Current, true);
        refs.Ui.BoneCard = menu.Current;

        CardSubhead(menu, "POSE");
        SliderRow(menu, "DEPTH", AnimatorSliderValue.Depth, -60f, 60f, 0f, "0",
            "Move the joint toward (right) or away from the viewer; keyed when let go", inner);
        CardRow(menu, inner, ("KEY BONE", ToolActionKind.KeyBone));
        refs.Ui.BoneRigHint = RigHint(menu, inner, "JOINT, PARTS AND CHILDREN: EDIT RIG");

        // Joint and bone building change the rig, so they only show while EDIT RIG is on.
        menu.Container(Vector2.Zero, new Vector2(inner, 4), StackDirection.Vertical,
            spacing: 6, color: Clear, cross: StackCrossAlignment.Start, autoHeight: true,
            main: StackMainAlignment.Start);
        refs.Ui.BoneRigRows = menu.Current;
        CardSubhead(menu, "JOINT");
        Entity[] joint = CardRow(menu, inner,
            ("MOVE", ToolActionKind.ToggleMovable),
            ("ONE-WAY", ToolActionKind.LockBend),
            ("LIMIT", ToolActionKind.ToggleLimit));
        refs.Ui.BoneMoveButton = joint[0];
        refs.Ui.BoneBendButton = joint[1];
        refs.Ui.BoneLimitButton = joint[2];
        SliderRow(menu, "MIN \u00b0", AnimatorSliderValue.LimitMin, -180f, 180f,
            -float.RadiansToDegrees(Xanadu.Model.Animation.RigJointLocks.DefaultLimit), "0",
            "The furthest the bone may turn the negative way from rest; turn LIMIT on first", inner);
        SliderRow(menu, "MAX \u00b0", AnimatorSliderValue.LimitMax, -180f, 180f,
            float.RadiansToDegrees(Xanadu.Model.Animation.RigJointLocks.DefaultLimit), "0",
            "The furthest the bone may turn the positive way from rest; turn LIMIT on first", inner);

        CardSubhead(menu, "BONE");
        CardRow(menu, inner,
            ("+ CHILD", ToolActionKind.AddBone),
            ("+ PART", ToolActionKind.AddPart),
            ("+ CHAIN", ToolActionKind.AddChain));
        CardRow(menu, inner,
            ("+ STREAK", ToolActionKind.AddEffect),
            ("+ PARTICLES", ToolActionKind.AddParticles),
            ("+ CLOTH", ToolActionKind.AddCloth));
        CardRow(menu, inner,
            ("RENAME", ToolActionKind.RenameBone),
            ("PARENT", ToolActionKind.ReparentBone),
            ("DELETE", ToolActionKind.DeleteBone));
        menu.End();
        menu.End();
    }

    /// <summary>A dim line saying where the hidden rig-building rows went.</summary>
    private static Entity RigHint(UiBuilder ui, float width, string text) =>
        ui.Text(text, new Vector2(width, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

    private static void CardSubhead(UiBuilder ui, string title) =>
        ui.Text(title, new Vector2(BoneCardInner, CaptionHeight), UiTheme.SmallFontSize,
            UiTheme.TextDim, TextAlign.Left);

    /// <summary>Buttons sharing a card row's width, in the small font.</summary>
    private static Entity[] CardRow(UiBuilder ui, float width, params (string Label, ToolActionKind Kind)[] buttons)
    {
        ui.Container(Vector2.Zero, new Vector2(width, RowHeight), spacing: 4, color: Clear);
        var size = new Vector2((width - 4 * (buttons.Length - 1)) / buttons.Length, RowHeight);
        var made = new Entity[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
            made[i] = Action(ui, buttons[i].Label, buttons[i].Kind, size, UiTheme.SmallFontSize);
        ui.End();
        return made;
    }

    /// <summary>
    /// A slider under a field, the inspector's full width. It shows no number of
    /// its own: the field above it does. The inspector system keeps it on the
    /// selection and applies drags.
    /// </summary>
    private static Entity InspectorSlider(
        UiBuilder menu,
        float width,
        float min,
        float max,
        float rest,
        string tooltip)
    {
        return UiNode.Wrap(menu.Current, menu.Visuals).Slider("", min, max, rest, width, readout: false, tooltip: tooltip).Entity;
    }

    /// <summary>A text field that only accepts the characters a number needs.</summary>
    private static Entity NumericField(UiBuilder menu, Vector2 size)
    {
        Entity field = TextInput(menu, size);
        field.GetComponent<TextField>().Numeric = true;
        return field;
    }
}
