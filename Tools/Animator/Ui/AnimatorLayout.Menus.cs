using System;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Tools.Animator;

/// <summary>The animator's floating menus and the sizes their systems place them by.</summary>
public static partial class AnimatorLayout
{
    /// <summary>The file menu floats above the panels, so it lives on the menu layer.</summary>
    private static void BuildFileMenu(UiBuilder menu, Entity fileButton)
    {
        (string Label, ToolActionKind Kind)[] entries =
        {
            ("SAVE XAF", ToolActionKind.Save),
            ("LOAD XAF", ToolActionKind.Load),
            ("NEW SKELETON", ToolActionKind.NewSkeleton),
        };
        const float optionHeight = 30f;
        float listHeight = entries.Length *
            (optionHeight + UiTheme.DropdownOptionSpacing) + 4f;
        var listSize = new Vector2(FileMenuWidth, listHeight);

        Entity list = menu.RectEntity(UiLayout.Hidden, listSize, UiTheme.Panel,
            takesInput: true);
        for (int i = 0; i < entries.Length; i++)
        {
            Entity option = menu.Button(entries[i].Label,
                new Vector2(168, optionHeight), position: UiLayout.Hidden);
            option.AddComponent(new ToolAction(entries[i].Kind));
            option.AddComponent(new DropdownOption(fileButton, i));
        }
        fileButton.AddComponent(new Dropdown(list, 0, listSize));
    }

    /// <summary>Size of the bone context menu opened by right-clicking a bone label.</summary>
    public static readonly Vector2 BoneMenuSize = new(150, 5 * 28 + 4 * 2 + 8);

    public static readonly Vector2 BoneNameSize = new(140, 22);

    /// <summary>Size of the key menu opened by right-clicking a timeline track row.</summary>
    public static readonly Vector2 KeyMenuSize = new(150, 2 * 28 + 2 + 8);

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
            ("ADD CHILD", ToolActionKind.AddBone),
            ("ADD SVG", ToolActionKind.AddSvg),
            ("DELETE", ToolActionKind.DeleteBone),
        };

        menu.Container(UiLayout.Hidden, BoneMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        refs.Ui.BoneMenu = menu.Current;
        foreach ((string label, ToolActionKind kind) in entries)
        {
            menu.Button(label, new Vector2(BoneMenuSize.X - 8, 28),
                fontSize: UiTheme.SmallFontSize).AddComponent(new ToolAction(kind));
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

    /// <summary>The key menu starts hidden; the key menu system moves it into place.</summary>
    private static void BuildKeyMenu(UiBuilder menu, ref Refs refs)
    {
        menu.Container(UiLayout.Hidden, KeyMenuSize, StackDirection.Vertical,
            spacing: 2, color: UiTheme.Panel, padding: 4, takesInput: true);
        refs.Ui.KeyMenu = menu.Current;
        var size = new Vector2(KeyMenuSize.X - 8, 28);
        refs.Ui.KeyMenuAdd = menu.Button("ADD KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuAdd.AddComponent(new ToolAction(ToolActionKind.AddKey));
        refs.Ui.KeyMenuDelete = menu.Button("DELETE KEY", size, fontSize: UiTheme.SmallFontSize);
        refs.Ui.KeyMenuDelete.AddComponent(new ToolAction(ToolActionKind.DeleteKey));
        menu.End();
    }
}
