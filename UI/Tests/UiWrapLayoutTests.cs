using Xanadu.Tests.Support.Fakes;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Layout;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

/// <summary>Wrapped labels in nested auto-sized stacks: each is as tall as its lines at the width it is drawn, and its stack makes room.</summary>
[TestFixture]
public sealed class UiWrapLayoutTests : EcsSystemTest
{
    private const string Long =
        "Colours the item. shade runs once for every pixel the item covers and returns that pixel's colour; i holds where "
        + "the pixel is (i.canvas, i.local, i.uv), the time and the item's own colour. The stack has one shade.";

    private UiNode _list;
    private readonly Entity[] _labels = new Entity[3];
    private readonly Entity[] _items = new Entity[3];

    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
        UiNode layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
        UiNode panel = layer.Panel().Width(760).Gap(UiMetrics.Space.Md).Cross(StackCrossAlignment.Stretch);
        _list = panel.Column().Gap(UiMetrics.Space.Lg).Cross(StackCrossAlignment.Stretch);
        for (int k = 0; k < 3; k++)
        {
            UiNode item = _list.Column().Gap(UiMetrics.Space.Xs).Cross(StackCrossAlignment.Start);
            item.Button("NAME", UiType.Small).Width(130);
            _labels[k] = item.Text(Long, UiType.Small).WrapText().Stretch().Entity;
            _items[k] = item.Entity;
        }

        Frame(6);
    }

    private static Rect2 Box(Entity entity)
    {
        Vector2 centre = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        Vector2 size = entity.GetComponent<Rectangle>().Bounds.Size;
        return new Rect2(centre - size / 2, size);
    }

    [Test]
    public void AWrappedLabelIsAsTallAsItsLinesAtItsWidth()
    {
        Rect2 box = Box(_labels[0]);
        TextStyle style = _labels[0].GetComponent<TextStyle>();
        Assert.That(box.Size.X, Is.GreaterThan(600), "stretched across the list");
        Assert.That(box.Size.Y, Is.EqualTo(UiTextFit.WrappedHeight(Long, style.FontSize, box.Size.X)).Within(0.5f));
    }

    [Test]
    public void ItsStackMakesRoomForIt()
    {
        Rect2 label = Box(_labels[0]), next = Box(_items[1]);
        Assert.That(next.Position.Y, Is.GreaterThanOrEqualTo(label.Position.Y + label.Size.Y - 0.5f));
    }
}
