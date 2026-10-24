using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Systems.Ui;

namespace Writer.Tests.UI;

[TestFixture]
public sealed class UiInteractionTests
{
    [Test]
    public void DragDoesNotActivateBeforeThreshold()
    {
        var store = new EntityStore();
        Entity target = store.CreateEntity();
        var drag = new UiDragController(threshold: 4f);

        Assert.That(drag.Begin(target, new Vector2(10, 10)), Is.True);
        Assert.That(drag.Update(new Vector2(12, 12)), Is.False);
        Assert.That(drag.Active, Is.False);

        Assert.That(drag.Update(new Vector2(20, 20)), Is.True);
        Assert.That(drag.Active, Is.True);
        Assert.That(drag.Delta, Is.EqualTo(new Vector2(10, 10)));

        drag.End();
        Assert.That(drag.Target.IsNull, Is.True);
        Assert.That(drag.Active, Is.False);
    }

    [Test]
    public void SelectionSetPreservesOrderAndTogglesEntities()
    {
        var store = new EntityStore();
        Entity first = store.CreateEntity();
        Entity second = store.CreateEntity();
        var selection = new UiSelectionSet();

        selection.SetMany(new[] { first, second });
        Assert.That(selection.Primary.Id, Is.EqualTo(first.Id));
        Assert.That(selection.All.Count, Is.EqualTo(2));
        Assert.That(selection.Contains(second), Is.True);

        selection.Toggle(first);
        Assert.That(selection.Contains(first), Is.False);
        Assert.That(selection.Primary.Id, Is.EqualTo(second.Id));

        selection.Toggle(first);
        Assert.That(selection.All.Count, Is.EqualTo(2));

        selection.Clear();
        Assert.That(selection.All, Is.Empty);
    }
}
