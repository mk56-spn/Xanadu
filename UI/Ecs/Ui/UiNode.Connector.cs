using Friflo.Engine.ECS;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// A line joining two other nodes, drawn from wherever layout put them: from the
/// side of <see cref="From"/> that faces <see cref="To"/> to the side of
/// <see cref="To"/> that faces back. It follows both as they move, and draws
/// nothing while either end is hidden or missing. Change its ends or colour
/// with <see cref="UiConnectors.Join"/>.
/// </summary>
public struct UiConnector(Entity from, Entity to, Color color, float width) : IComponent
{
    public Entity From = from;
    public Entity To = to;
    public Color Color = color;
    public float Width = width;
}

public readonly partial struct UiNode
{
    /// <summary>How much of the background a connector lets through, so it sits under what it joins.</summary>
    private const float ConnectorAlpha = 0.55f;

    /// <summary>
    /// A line from <paramref name="from"/> to <paramref name="to"/> in category
    /// <paramref name="category"/>'s colour (<see cref="UiThemeRoles.Categories"/>).
    /// It is out of the flow and draws where its parent's canvas does, so build it
    /// before what it joins, in a shared ancestor, and they cover its ends.
    /// Either end may be null to start with and set later.
    /// </summary>
    public UiNode Connector(Entity from, Entity to, int category) =>
        Rect(Transparent, size: Vector2.One)
            .IgnoreLayout()
            .With(new UiConnector(from, to, ConnectorColour(category), UiMetrics.Border.Medium));

    /// <summary>Category <paramref name="category"/>'s colour as a connector draws it.</summary>
    internal Color ConnectorColour(int category) => new(Roles.Category(category), ConnectorAlpha);
}

/// <summary>Changing a connector after it is built.</summary>
public static class UiConnectors
{
    /// <summary>Points the connector at new ends, in category <paramref name="category"/>'s colour.</summary>
    public static void Join(Entity connector, Entity from, Entity to, int category)
    {
        if (connector.IsNull || !connector.HasComponent<UiConnector>())
            return;

        ref UiConnector line = ref connector.GetComponent<UiConnector>();
        line.From = from;
        line.To = to;
        line.Color = UiNode.Wrap(connector).ConnectorColour(category);
    }
}
