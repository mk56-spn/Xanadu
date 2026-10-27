using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>The small group badge drawn on an item that belongs to at least one group.</summary>
public static partial class PlayfieldRenderer
{
    public static readonly Color GroupBadgeColor = new("#38bdf8");
    private static readonly Color GroupBadgeInk = new("#04101f");

    /// <summary>
    /// Draws a compact "G1"/"G2" pill at <paramref name="center"/>, showing how
    /// many groups the item belongs to. Nothing is drawn for a zero mask.
    /// </summary>
    public static void DrawGroupBadge(
        IRectCanvas canvas,
        Rid rid,
        Vector2 center,
        uint mask,
        float alpha)
    {
        int count = ItemGroups.Count(mask);
        if (count == 0 || alpha <= 0f)
            return;

        string label = "G" + count;
        float width = 9f + label.Length * 6.2f;
        const float height = 14f;
        var pill = new Rect2(
            center - new Vector2(width / 2f, height / 2f),
            new Vector2(width, height));

        // A dark rim keeps the badge readable over bright notes and block edges.
        canvas.DrawRect(rid, pill.Grow(1f), new Color(0.02f, 0.05f, 0.1f, alpha * 0.85f));
        canvas.DrawRect(
            rid,
            pill,
            new Color(GroupBadgeColor.R, GroupBadgeColor.G, GroupBadgeColor.B, alpha));
        canvas.DrawTextStyled(
            rid,
            pill,
            label,
            new Color(GroupBadgeInk.R, GroupBadgeInk.G, GroupBadgeInk.B, alpha),
            9);
    }
}
