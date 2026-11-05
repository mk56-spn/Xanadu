using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;
using Xanadu.Theme;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorBlockSizeSystemTests : EcsSystemTest
{
    [Test]
    public void SizeButtonsAreExclusiveAndTintTheSelection()
    {
        Entity editor = Store.CreateEntity(
            new EditorPlacement { BlockSize = BlockBrushSize.Cell });
        Entity cell = Button(BlockBrushSize.Cell);
        Entity quarter = Button(BlockBrushSize.Quarter);
        Entity platform = Button(BlockBrushSize.Platform);

        Root.Add(new EditorBlockSizeSystem(Store));

        Frame();
        Assert.That(cell.GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Primary));

        quarter.AddTag<Pressed>();
        Frame();
        Frame();

        Assert.That(
            editor.GetComponent<EditorPlacement>().BlockSize,
            Is.EqualTo(BlockBrushSize.Quarter));
        Assert.That(quarter.GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Primary));
        Assert.That(cell.GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Muted));

        platform.AddTag<Pressed>();
        Frame();
        Frame();

        Assert.That(
            editor.GetComponent<EditorPlacement>().BlockSize,
            Is.EqualTo(BlockBrushSize.Platform));
        Assert.That(platform.GetComponent<ButtonVisual>().Normal, Is.EqualTo(Palette.Primary));
    }

    private Entity Button(BlockBrushSize size) => Store.CreateEntity(
        new EditorButton(EditorButtonKind.BlockSize, (int)size),
        ButtonVisual.Create(Palette.Muted));
}
