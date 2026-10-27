using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// The ADD SVG picker. It lists every group of every SVG the project holds,
/// built afresh the frame the picker opens, and stays beside the bone menu.
/// A row is an <see cref="ToolActionKind.AttachSvg"/> button, so the action
/// system adds the attachment; the picker stays open so several groups can be
/// added, and a click off the panel closes it.
/// </summary>
public sealed class AnimatorSvgMenuSystem(
    IAnimatorSvgCatalog catalog,
    IVisualRidFactory visuals,
    IPointerInputSource pointer) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private bool _primaryHeld;
    private bool _wasOpen;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool primary = input.Pressed && !_primaryHeld;
        _primaryHeld = input.Pressed;

        foreach (Entity session in Query.ToEntityList())
            Update(session, primary, input.Position);
    }

    private void Update(Entity session, bool primary, Vector2 point)
    {
        ref AnimatorSvgMenu menu = ref session.GetComponent<AnimatorSvgMenu>();
        AnimatorUi ui = session.GetComponent<AnimatorUi>();

        // The frame it opens is the one frame a press belongs to the option
        // that opened it, not to clicking off the panel.
        bool opened = menu.Open && !_wasOpen;
        if (opened)
            Open(session, ref menu, ui);
        else if (!menu.Open && _wasOpen)
            Clear(ui);

        if (!menu.Open)
        {
            AnimatorMenuPlacement.Place(ui.SvgMenu, UiLayout.Hidden);
            _wasOpen = false;
            return;
        }

        // The height follows the choices, so it is known before the
        // click-outside test that uses the same box the draw system shows.
        Vector2 size = SizeFor(menu);
        Vector2 placed = AnimatorMenuPlacement.Anchor(menu.Anchor, size, ui.ScreenSize);
        if (!opened && primary && !new Rect2(placed, size).HasPoint(point))
        {
            menu.Open = false;
            Clear(ui);
            AnimatorMenuPlacement.Place(ui.SvgMenu, UiLayout.Hidden);
            _wasOpen = false;
            return;
        }

        AnimatorMenuPlacement.Place(ui.SvgMenu, placed, size);
        _wasOpen = true;
    }

    /// <summary>Fills the picker from the project and sizes and builds its rows.</summary>
    private void Open(Entity session, ref AnimatorSvgMenu menu, AnimatorUi ui)
    {
        menu.Choices ??= new List<AnimatorSvgChoice>();
        menu.Choices.Clear();
        foreach (string file in catalog.Files())
        {
            foreach (string group in catalog.Groups(file))
                menu.Choices.Add(new AnimatorSvgChoice(file, group));
        }

        if (menu.Choices.Count == 0)
        {
            menu.Open = false;
            AnimatorSession.SetStatus(session, "NO SVG GROUPS IN THE PROJECT");
            return;
        }

        Resize(ui.SvgMenu, SizeFor(menu));
        BuildRows(ui, menu);
    }

    private void BuildRows(AnimatorUi ui, AnimatorSvgMenu menu)
    {
        if (ui.SvgMenuList.IsNull || menu.Choices is null)
            return;

        UiBuilder builder = UiBuilder.Begin(Query.Store, visuals).Use(ui.SvgMenuList);
        var size = new Vector2(
            AnimatorLayout.SvgMenuWidth - 8,
            AnimatorLayout.SvgRowHeight);
        for (int i = 0; i < menu.Choices.Count; i++)
        {
            Entity row = builder.Button(
                menu.Choices[i].Label,
                size,
                fontSize: UiTheme.SmallFontSize);
            row.AddComponent(new ToolAction(ToolActionKind.AttachSvg, i.ToString()));
            // The choice's label is the group name, which is what will be attached.
            row.AddComponent(new UiTooltip($"Attach {menu.Choices[i].Label}"));
        }
        builder.End();
        ui.SvgMenuList.AddTag<LayoutDirty>();
    }

    /// <summary>Removes the built rows, as a reload or another rig can change the choices.</summary>
    private void Clear(AnimatorUi ui)
    {
        if (ui.SvgMenuList.IsNull)
            return;

        var rows = new List<Entity>();
        foreach (Entity child in ui.SvgMenuList.ChildEntities)
            rows.Add(child);
        foreach (Entity row in rows)
            UiLayout.DeleteSubtree(row);
        ui.SvgMenuList.AddTag<LayoutDirty>();
    }

    private static Vector2 SizeFor(AnimatorSvgMenu menu)
    {
        int choices = menu.Choices?.Count ?? 1;
        int rows = Math.Clamp(choices, 1, AnimatorLayout.SvgMenuRows);
        return new Vector2(
            AnimatorLayout.SvgMenuWidth,
            AnimatorLayout.SvgMenuHeight(rows));
    }

    private static void Resize(Entity menu, Vector2 size)
    {
        if (menu.IsNull || !menu.HasComponent<Rectangle>())
            return;

        ref Rectangle rectangle = ref menu.GetComponent<Rectangle>();
        if (rectangle.Bounds.Size == size)
            return;

        rectangle.Bounds = new Rect2(-size / 2, size);
        menu.AddTag<LayoutDirty>();
    }
}
