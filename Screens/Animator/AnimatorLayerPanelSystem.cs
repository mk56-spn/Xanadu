using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Core.Theme;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// Keeps the LAYERS tab showing the art in draw order. Row entities stay where they
/// are and take on whichever art is at that place, so a press survives a reorder.
/// </summary>
public sealed class AnimatorLayerPanelSystem(IVisualItemFactory visuals)
    : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private const float ArrowWidth = 30f;
    private const float Gap = 2f;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            Entity list = session.GetComponent<AnimatorUi>().LayerList;
            if (list.IsNull || !list.Enabled)
                continue;

            List<AnimatorLayerRow> rows = AnimatorLayerCommands.Rows(session);
            while (list.ChildCount < rows.Count)
                AddRow(list);
            while (list.ChildCount > rows.Count)
                UiLayout.DeleteSubtree(list.ChildEntities[list.ChildCount - 1]);

            AnimatorSelection selection = session.GetComponent<AnimatorSelection>();
            for (int i = 0; i < rows.Count; i++)
                Present(list.ChildEntities[i], rows[i], i, rows.Count, selection);
        }
    }

    private void AddRow(Entity list)
    {
        var height = UiTheme.TreeRowHeight;
        UiBuilder ui = UiBuilder.Begin(Query.Store, visuals).Use(list);
        ui.Container(Vector2.Zero, new Vector2(AnimatorLayout.LayerRowWidth, height), spacing: Gap,
            color: new Color(0, 0, 0, 0));
        Entity label = ui.Button(
            "",
            new Vector2(AnimatorLayout.LayerRowWidth - (ArrowWidth + Gap) * 2, height),
            UiTheme.PanelAlt);
        label.AddComponent(new TextAlignment(TextAlign.Left));
        label.AddComponent(new Intent(new SelectPart(-1)));
        label.AddComponent(new UiTooltip("Select this art"));
        Arrow(ui, "▲", new MoveLayer(0, 1), height);
        Arrow(ui, "▼", new MoveLayer(0, -1), height);
        ui.End();
    }

    private static void Arrow(UiBuilder ui, string glyph, IIntent kind, float height)
    {
        Entity arrow = ui.Button(glyph, new Vector2(ArrowWidth, height), UiTheme.ButtonMuted);
        arrow.AddComponent(new Intent(kind));
        AnimatorTooltips.Attach(arrow, kind);
    }

    private static void Present(
        Entity row,
        in AnimatorLayerRow art,
        int place,
        int count,
        in AnimatorSelection selection)
    {
        Entity label = row.ChildEntities[0];
        UiText.Set(label, $"{art.Layer}   {art.Label}");
        SetAction(label, art.IsCloth ? new SelectCloth(art.Index) : new SelectPart(art.Index));

        bool selected = art.IsCloth ? selection.Cloth == art.Index : selection.Attachment == art.Index;
        Color fill = selected ? Palette.Selected : UiTheme.PanelAlt;
        ref ButtonVisual visual = ref label.GetComponent<ButtonVisual>();
        if (visual.Normal != fill)
        {
            visual.Normal = fill;
            visual.Hover = fill.Lerp(Colors.White, 0.16f);
        }

        Entity forward = row.ChildEntities[1];
        Entity back = row.ChildEntities[2];
        SetAction(forward, new MoveLayer(art.Key, 1));
        SetAction(back, new MoveLayer(art.Key, -1));
        UiLayout.SetDisabled(forward, place == 0);
        UiLayout.SetDisabled(back, place == count - 1);
    }

    private static void SetAction(Entity button, IIntent action)
    {
        if (!button.TryGetIntent(out IIntent was) || !was.Equals(action))
            button.AddComponent(new Intent(action));
    }
}
