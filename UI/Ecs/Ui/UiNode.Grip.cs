using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

public readonly partial struct UiNode
{
    /// <summary>The size a grip's dot or square is drawn at, across.</summary>
    public const float GripSize = 11;

    /// <summary>
    /// A grip (<see cref="UiGrip"/>) out of the flow, placed with <see cref="At"/> from its
    /// parent's centre. <paramref name="look"/> is <see cref="GripDot"/>, <see cref="GripSquare"/>
    /// or default for one that is only somewhere to press (a shape's body).
    /// </summary>
    public UiNode Grip(float reach, Tags look = default)
    {
        UiNode grip = Rect(Transparent, size: new Vector2(GripSize)).IgnoreLayout().With(new UiGrip(reach));
        grip.Entity.AddTags(look);
        return grip;
    }

    /// <summary>Gives a shape (<see cref="Shape"/>) new points, in its own space, and redraws it.</summary>
    public UiNode Reshape(Vector2[] points)
    {
        if (IsNull || !_entity.HasComponent<UiShape>())
            return this;
        _entity.GetComponent<UiShape>().Points = points;
        return Touch(layout: false);
    }
}
