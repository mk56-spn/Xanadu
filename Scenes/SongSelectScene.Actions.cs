using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// The level's actions: a frosted black bar holding two square buttons, a
/// hammer that edits the level and a bin that deletes it. Playing is done by
/// clicking the level's art.
/// </summary>
public static partial class SongSelectScene
{
    private const float ActionButtonSize = 72;
    private const float ActionPad = 12;

    // In the space the narrower scores panel left free.
    private static readonly Vector2 ActionBarCenter = new(1150, 960);

    private readonly record struct ActionViews(
        Entity Edit,
        List<Entity> EditIcons,
        Entity Delete,
        List<Entity> DeleteIcons);

    private static ActionViews BuildActions(UiNode ui, bool loadAssets)
    {
        UiNode bar = ui.Row()
            .Size(ActionButtonSize * 2 + ActionPad * 3, ActionButtonSize + ActionPad * 2)
            .At(ActionBarCenter)
            .Pad(ActionPad)
            .Gap(ActionPad)
            .Fill(PanelBlack);
        Frost(bar.Entity, loadAssets);

        UiNode edit = ActionButton(
            bar, LocalizedText.Get(SongSelectStrings.EditLevel), SceneActionId.Edit);
        var editIcons = new List<Entity>();
        foreach (Vector2[] part in Hammer(ActionButtonSize * 0.5f))
            editIcons.Add(Icon(edit, part));

        UiNode delete = ActionButton(
            bar, LocalizedText.Get(SongSelectStrings.DeleteLevel), SceneActionId.DeleteLevel);
        var deleteIcons = new List<Entity>();
        foreach (Vector2[] part in Bin(ActionButtonSize * 0.44f))
            deleteIcons.Add(Icon(delete, part));

        return new ActionViews(edit.Entity, editIcons, delete.Entity, deleteIcons);
    }

    /// <summary>A clickable square with a thin gold outline and its icon inside.</summary>
    private static UiNode ActionButton(UiNode bar, string tooltip, SceneActionId action) =>
        bar.Column()
            .Size(ActionButtonSize, ActionButtonSize)
            .Fill(Dim)
            .Input()
            .Border(Gold, 2)
            .With(ButtonVisual.Create(Dim))
            .Tooltip(tooltip)
            .On(action);

    /// <summary>A filled shape inside the button, centred on it and left out of its stacking.</summary>
    private static Entity Icon(UiNode button, Vector2[] points) =>
        button.Shape(points, Gold).IgnoreLayout();

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

    /// <summary>A bin, <paramref name="size"/> tall: the handle, the lid, then the body narrowing to its foot.</summary>
    private static Vector2[][] Bin(float size)
    {
        float u = size / 10;
        return
        [
            [new(-1.6f * u, -5f * u), new(1.6f * u, -5f * u), new(1.6f * u, -3.9f * u), new(-1.6f * u, -3.9f * u)],
            [new(-4.6f * u, -3.9f * u), new(4.6f * u, -3.9f * u), new(4.6f * u, -2.7f * u), new(-4.6f * u, -2.7f * u)],
            [new(-3.8f * u, -1.9f * u), new(3.8f * u, -1.9f * u), new(3.1f * u, 5f * u), new(-3.1f * u, 5f * u)],
        ];
    }

    /// <summary>
    /// Shows or hides the action buttons, and lets the level's art take clicks
    /// only when the level can be played. Editing needs a playable level;
    /// deleting needs only a level. A hidden button stops taking input and
    /// draws nothing, but keeps its place.
    /// </summary>
    private static void RefreshActions(ref SongSelectState state, bool playable, bool hasLevel)
    {
        SetTakesInput(state.Play, playable);
        ShowAction(state.Edit, state.ActionIcons, playable);
        ShowAction(state.Delete, state.DeleteIcons, hasLevel);
    }

    private static void ShowAction(Entity button, List<Entity> icons, bool shown)
    {
        SetTileColor(button, shown ? Dim : Transparent);
        SetTakesInput(button, shown);
        ref UiBorder border = ref button.GetComponent<UiBorder>();
        Color edge = shown ? Gold : Transparent;
        if (border.Color != edge)
        {
            border.Color = edge;
            button.AddTag<Dirty>();
        }

        foreach (Entity icon in icons)
            SetFill(icon, shown ? Gold : Transparent);
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
