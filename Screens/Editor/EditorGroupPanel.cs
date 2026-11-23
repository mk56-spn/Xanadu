using System;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>
/// The sidebar's GROUPS section: a row per group to pick it, the buttons that
/// make, fill, empty and delete groups from the selected decos, and for the
/// picked group one slider per field it can key (<see cref="LevelItemFields"/>),
/// showing its value at the playhead and keying it there when dragged.
/// <see cref="EditorGroupPanelSystem"/> keeps it current.
/// </summary>
public static class EditorGroupPanel
{
    /// <summary>How many group rows show at once; past this the ▲ ▼ row pages through them.</summary>
    public const int Rows = 6;

    private const float RowHeight = 28;
    private const float Gap = 6;

    /// <summary>What each field's slider spans, in the units it shows (a turn in degrees).</summary>
    private static readonly (float Min, float Max)[] Ranges =
    [
        (-400f, 400f),
        (-400f, 400f),
        (-180f, 180f),
        (0f, 3f),
        (0f, 1f),
    ];

    internal static void Build(UiNode body, float width)
    {
        // The hint shows while there are no groups.
        body.Text("Select decos, then NEW", UiType.Caption, SceneUi.DimTextColor, TextAlign.Left)
            .Size(width, 18)
            .With(EditorButton.Of(EditorButtonKind.GroupPick, -1));

        // The rows are a window onto the groups: each row's argument is set to
        // the group it shows; its text keeps which row it is.
        for (int i = 0; i < Rows; i++)
        {
            body.Button("", UiType.Label, SceneUi.MutedButtonColor)
                .Size(width, RowHeight)
                .Hidden()
                .With(UiCommand.Of(EditorButtonKind.GroupPick, i, i.ToString()));
        }

        UiNode page = body.Row().Size(width, RowHeight).Gap(Gap).Main(StackMainAlignment.Start)
            .Hidden()
            .With(EditorButton.Of(EditorButtonKind.GroupPage, -2));
        page.Text("", UiType.Caption, SceneUi.DimTextColor, TextAlign.Left)
            .Size(width - 2 * (RowHeight + Gap), RowHeight)
            .With(EditorButton.Of(EditorButtonKind.GroupPage, 0));
        Page(page, "▲", "Earlier groups", -1);
        Page(page, "▼", "Later groups", +1);

        float half = (width - Gap) / 2;
        UiNode make = body.Row().Size(width, RowHeight).Gap(Gap).Main(StackMainAlignment.Start);
        Action(make, half, "NEW", "A group of the selected decos", EditorButtonKind.GroupNew);
        Action(make, half, "DELETE", "Delete the picked group", EditorButtonKind.GroupDelete);
        UiNode members = body.Row().Size(width, RowHeight).Gap(Gap).Main(StackMainAlignment.Start);
        Action(members, half, "+ SELECTED", "Put the selected decos in the picked group", EditorButtonKind.GroupAdd);
        Action(members, half, "- SELECTED", "Take the selected decos out of the picked group", EditorButtonKind.GroupRemove);

        // The picked group: its name, loop and fields at the playhead; hidden while none is picked.
        UiNode keys = body.Column()
            .Width(width)
            .AutoHeight()
            .Gap(Gap)
            .Main(StackMainAlignment.Start)
            .With(EditorButton.Of(EditorButtonKind.GroupField, -1));
        keys.Rect(Palette.SurfaceInset, "", new Vector2(width, RowHeight))
            .Input()
            .With(new TextStyle(UiType.Label, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .Tooltip("The group's name")
            .With(EditorButton.Of(EditorButtonKind.GroupName));

        // The loop: a stepped length, so the label, then - and +.
        UiNode loop = keys.Row().Size(width, RowHeight).Gap(Gap).Main(StackMainAlignment.Start);
        loop.Text("LOOP", UiType.Caption, UiTheme.Accent, TextAlign.Left).Size(48, RowHeight);
        loop.Text("off", UiType.Label, SceneUi.DimTextColor, TextAlign.Center)
            .Size(width - 48 - 2 * (RowHeight + Gap) - Gap, RowHeight)
            .With(EditorButton.Of(EditorButtonKind.GroupLoop, 0));
        Step(loop, "-", "A shorter loop (down to none)", -1);
        Step(loop, "+", "A longer loop", +1);

        // What the sliders key: the whole group, or (escape hatch) one member.
        keys.Text("AT THE PLAYHEAD (DRAG TO KEY)", UiType.Caption, SceneUi.DimTextColor, TextAlign.Left)
            .Size(width, 18)
            .With(EditorButton.Of(EditorButtonKind.GroupField, -2));
        for (int i = 0; i < LevelItemFields.All.Length; i++)
        {
            (float min, float max) = Ranges[i];
            float neutral = LevelItemFields.Neutral(LevelItemFields.All[i].Name);
            keys.ValueField(LevelItemFields.All[i].Shown, UiValueShape.Float, [neutral], width, (min, max))
                .With(EditorButton.Of(EditorButtonKind.GroupField, i));
        }

        UiNode here = keys.Row().Size(width, RowHeight).Gap(Gap).Main(StackMainAlignment.Start);
        Action(here, half, "EASE", "How the keys here travel to the next ones", EditorButtonKind.GroupEase);
        Action(here, half, "DELETE KEYS", "Take out the keys at the playhead", EditorButtonKind.GroupDeleteKeys);

        // The escape hatch, small and last: groups are how things move; one
        // member's own keys are for the odd case a group can't cover.
        keys.Button("key one member only", UiType.Caption, SceneUi.MutedButtonColor)
            .Size(width, 22)
            .Tooltip("Escape hatch: the sliders key only the one selected member, on top of the group")
            .With(EditorButton.Of(EditorButtonKind.GroupOneMember));
    }

    /// <summary>A field's value as its slider shows it: a turn in degrees, the rest as they are.</summary>
    public static float Shown(int field, float value) =>
        LevelItemFields.All[field].Kind == Xanadu.Core.Keys.KeyValueKind.Angle ? value * 180f / MathF.PI : value;

    /// <summary>A slider's value as the field keeps it: <see cref="Shown"/> undone.</summary>
    public static float Kept(int field, float shown) =>
        LevelItemFields.All[field].Kind == Xanadu.Core.Keys.KeyValueKind.Angle ? shown * MathF.PI / 180f : shown;

    private static void Page(UiNode parent, string sign, string tooltip, int direction) =>
        parent.Button(sign, UiType.Label, SceneUi.EditorButtonColor)
            .Size(RowHeight, RowHeight)
            .Tooltip(tooltip)
            .With(EditorButton.Of(EditorButtonKind.GroupPage, direction));

    private static void Step(UiNode parent, string sign, string tooltip, int direction) =>
        parent.Button(sign, UiType.Label, SceneUi.EditorButtonColor)
            .Size(RowHeight, RowHeight)
            .Tooltip(tooltip)
            .With(EditorButton.Of(EditorButtonKind.GroupLoop, direction));

    private static void Action(UiNode parent, float width, string label, string tooltip, EditorButtonKind kind) =>
        parent.Button(label, UiType.Label, SceneUi.EditorButtonColor)
            .Size(width, RowHeight)
            .Tooltip(tooltip)
            .With(EditorButton.Of(kind));
}
