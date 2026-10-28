using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;

namespace Xanadu.Scenes;

/// <summary>
/// The edit and menu buttons: two diamonds, the big one with a hammer and the
/// small one with three bars. Playing is done by clicking the level's art.
/// </summary>
public static partial class SongSelectScene
{
    private const float EditSize = 150;
    private const float MenuSize = 104;

    // Staggered, in the space the narrower scores panel left free.
    private static readonly Vector2 EditCenter = new(1070, 870);
    private static readonly Vector2 MenuCenter = new(1215, 950);

    private readonly record struct ActionViews(Entity Edit, List<Entity> Icons);

    private static ActionViews BuildActions(UiBuilder ui)
    {
        var icons = new List<Entity>();

        Entity edit = ActionDiamond(
            ui,
            EditCenter,
            EditSize,
            LocalizedText.Get(SongSelectStrings.EditLevel),
            SceneActionId.Edit);
        foreach (Vector2[] part in Hammer(EditSize * 0.42f))
            icons.Add(Icon(ui, part, Gold));

        ui.End();

        // The menu has nothing of its own yet, so for now it opens the editor like the hammer.
        ActionDiamond(
            ui,
            MenuCenter,
            MenuSize,
            LocalizedText.Get(SongSelectStrings.Menu),
            SceneActionId.Edit);
        foreach (Vector2[] bar in Bars(MenuSize * 0.4f))
            Icon(ui, bar, Gold);

        ui.End();

        return new ActionViews(edit, icons);
    }

    /// <summary>A clickable diamond with a gold outline; left open so its icon can be added inside.</summary>
    private static Entity ActionDiamond(
        UiBuilder ui,
        Vector2 center,
        float size,
        string tooltip,
        SceneActionId action)
    {
        ui.Container(
            center,
            new Vector2(size, size),
            StackDirection.Vertical,
            color: Dim,
            takesInput: true);
        Entity button = ui.Current;
        button.AddComponent(new UiDiamond());
        button.AddComponent(new UiBorder(Gold, 4));
        button.AddComponent(ButtonVisual.Create(Dim));
        button.AddComponent(new UiTooltip(tooltip));
        if (action != SceneActionId.None)
            button.AddComponent(new SceneCommand(action));

        return button;
    }

    /// <summary>A filled shape inside the open button, centred on it and left out of its stacking.</summary>
    private static Entity Icon(UiBuilder ui, Vector2[] points, Color color)
    {
        Entity icon = ui.ShapeEntity(points, color);
        icon.AddTag<LayoutIgnore>();
        return icon;
    }

    /// <summary>A hammer leaning to the left: the head, then the handle, as two polygons.</summary>
    private static Vector2[][] Hammer(float size)
    {
        float lean = Mathf.DegToRad(-40);
        Vector2[] Turn(Vector2[] points)
        {
            var turned = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
                turned[i] = points[i].Rotated(lean);

            return turned;
        }

        float u = size / 10;
        return
        [
            Turn([new(-4.5f * u, -5f * u), new(4.5f * u, -5f * u), new(4.5f * u, -2.4f * u), new(-4.5f * u, -2.4f * u)]),
            Turn([new(-0.8f * u, -2.4f * u), new(0.8f * u, -2.4f * u), new(0.8f * u, 5f * u), new(-0.8f * u, 5f * u)]),
        ];
    }

    /// <summary>Three stacked bars, the usual menu mark, <paramref name="width"/> wide.</summary>
    private static Vector2[][] Bars(float width)
    {
        float height = width * 0.17f;
        float pitch = width * 0.36f;
        var bars = new Vector2[3][];
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * pitch;
            bars[i] =
            [
                new(-width / 2, y - height / 2),
                new(width / 2, y - height / 2),
                new(width / 2, y + height / 2),
                new(-width / 2, y + height / 2),
            ];
        }

        return bars;
    }

    /// <summary>
    /// Shows or hides the edit button, and lets the level's art take clicks only
    /// when the level can be played. A hidden button stops taking input and
    /// draws nothing, but keeps its place.
    /// </summary>
    private static void RefreshActions(ref SongSelectState state, bool playable)
    {
        SetTakesInput(state.Play, playable);

        SetTileColor(state.Edit, playable ? Dim : Transparent);
        SetTakesInput(state.Edit, playable);
        ref UiBorder border = ref state.Edit.GetComponent<UiBorder>();
        Color edge = playable ? Gold : Transparent;
        if (border.Color != edge)
        {
            border.Color = edge;
            state.Edit.AddTag<Dirty>();
        }

        foreach (Entity icon in state.ActionIcons)
            SetFill(icon, playable ? Gold : Transparent);
    }

    private static void SetTakesInput(Entity button, bool takesInput)
    {
        bool has = button.Tags.Has<TakesInput>();
        if (takesInput && !has)
            button.AddTag<TakesInput>();
        else if (!takesInput && has)
            button.RemoveTag<TakesInput>();
    }

    private static void SetFill(Entity shape, Color color)
    {
        ref UiShape component = ref shape.GetComponent<UiShape>();
        if (component.Fill == color)
            return;

        component.Fill = color;
        shape.AddTag<Dirty>();
    }
}
