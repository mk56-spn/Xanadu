using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Everything a board's drawing shows, folded into one number: where it is and how it is panned and zoomed, its
/// theme and underlay, its boxes and lines, and what is selected, hovered or typed into. The draw system draws a
/// board again only when this changes (or while a gesture or typing is under way, which follow the pointer and the
/// caret). Owners may change the board's boxes and lines directly; they are read here each frame, which is cheap.
/// </summary>
internal static class BoardLook
{
    public static int Of(Entity entity, Board board, BoardState state, UiThemeRoles roles)
    {
        var look = new HashCode();
        look.Add(state.Area);
        look.Add(state.Pan);
        look.Add(state.Zoom);
        look.Add(state.Origin);
        look.Add(state.Underlay);
        look.Add(roles);
        look.Add(entity.Tags.Has<ReadOnly>());
        look.Add(entity.Tags.Has<Arrowheads>());
        look.Add(state.Hover);
        look.Add(state.HoverLinkHandle);
        look.Add(state.HoverResize);
        look.Add(state.SelectedLink);
        foreach (int id in state.Selected)
            look.Add(id);
        foreach (BoardBox box in board.Boxes)
        {
            look.Add(box.Id);
            look.Add(box.Rect);
            look.Add(box.Text);
            look.Add(box.Kind);
            look.Add(box.Value);
            look.Add(box.Category);
            look.Add(box.Flag);
            look.Add(box.FlagTone);
        }

        foreach (BoardLink link in board.Links)
            look.Add(link);
        return look.ToHashCode();
    }
}
