using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Systems.Layout;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI.Layout;

[TestFixture]
public sealed class GridLayoutTests : EcsSystemTest
{
    protected override void OnEcsSetUp()
    {
        Root.Add(new ContainerLayoutSystem());
    }

    private Entity Grid(
        int columns,
        float spacing = 0,
        float lineSpacing = -1,
        float padding = 0,
        bool autoSize = false,
        StackMainAlignment main = StackMainAlignment.Center,
        StackCrossAlignment cross = StackCrossAlignment.Center) =>
        Store.CreateEntity(
            new Rectangle(new Rect2(-50, -50, 100, 100)),
            new UiContainer(
                StackDirection.Horizontal,
                spacing,
                padding: padding,
                cross: cross,
                autoWidth: autoSize,
                autoHeight: autoSize,
                main: main,
                columns: columns,
                lineSpacing: lineSpacing),
            Tags.Get<LayoutDirty>());

    private Entity Cell(Entity grid, float width, float height)
    {
        Entity child = Store.CreateEntity(
            new Rectangle(new Rect2(-width / 2, -height / 2, width, height)),
            new Position(Vector2.Zero));
        grid.AddChild(child);
        return child;
    }

    private static Vector2 At(Entity entity) => entity.GetComponent<Position>().Value;

    [Test]
    public void CellsFillRowByRowAndTheGridIsCentred()
    {
        Entity grid = Grid(columns: 2, spacing: 4);
        Entity[] cells = new Entity[4];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = Cell(grid, 20, 20);

        Frame();

        // The grid is 44 by 44: two 20 px cells and a 4 px gap each way.
        Assert.That(At(cells[0]), Is.EqualTo(new Vector2(-12, -12)));
        Assert.That(At(cells[1]), Is.EqualTo(new Vector2(12, -12)));
        Assert.That(At(cells[2]), Is.EqualTo(new Vector2(-12, 12)));
        Assert.That(At(cells[3]), Is.EqualTo(new Vector2(12, 12)));
        Assert.That(grid.Tags.Has<LayoutDirty>(), Is.False);
    }

    [Test]
    public void ColumnsAreAsWideAsTheirWidestAndRowsAsTallAsTheirTallest()
    {
        Entity grid = Grid(columns: 2);
        Entity a = Cell(grid, 20, 10);
        Entity b = Cell(grid, 10, 30);
        Entity c = Cell(grid, 40, 10);
        Entity d = Cell(grid, 10, 10);

        Frame();

        // Columns 40 and 10 wide, rows 30 and 10 tall: a 50 by 40 grid.
        Assert.That(At(a).X, Is.EqualTo(-25 + 20f));
        Assert.That(At(b).X, Is.EqualTo(-25 + 40 + 5f));
        Assert.That(At(c).X, Is.EqualTo(-25 + 20f));
        Assert.That(At(a).Y, Is.EqualTo(-20 + 15f));
        Assert.That(At(c).Y, Is.EqualTo(-20 + 30 + 5f));
        Assert.That(At(b).Y, Is.EqualTo(At(a).Y));
        Assert.That(At(d).X, Is.EqualTo(At(b).X));
    }

    [Test]
    public void RowsUseTheirOwnSpacingWhenGiven()
    {
        Entity grid = Grid(columns: 2, spacing: 2, lineSpacing: 10);
        Entity top = Cell(grid, 10, 10);
        Cell(grid, 10, 10);
        Entity bottom = Cell(grid, 10, 10);

        Frame();

        Assert.That(At(bottom).Y - At(top).Y, Is.EqualTo(20f));
    }

    [Test]
    public void RowsFallBackToTheColumnSpacingByDefault()
    {
        Entity grid = Grid(columns: 2, spacing: 6);
        Entity top = Cell(grid, 10, 10);
        Cell(grid, 10, 10);
        Entity bottom = Cell(grid, 10, 10);

        Frame();

        Assert.That(At(bottom).Y - At(top).Y, Is.EqualTo(16f));
    }

    [Test]
    public void StartAlignmentAndPaddingPinTheGridToTheTopLeft()
    {
        Entity grid = Grid(
            columns: 2,
            padding: 5,
            main: StackMainAlignment.Start,
            cross: StackCrossAlignment.Start);
        Entity first = Cell(grid, 20, 20);

        Frame();

        Assert.That(At(first), Is.EqualTo(new Vector2(-35, -35)));
    }

    [Test]
    public void EndAlignmentPinsTheGridToTheBottomRight()
    {
        Entity grid = Grid(
            columns: 2,
            main: StackMainAlignment.End,
            cross: StackCrossAlignment.End);
        Cell(grid, 20, 20);
        Entity second = Cell(grid, 20, 20);

        Frame();

        Assert.That(At(second), Is.EqualTo(new Vector2(40, 40)));
    }

    [Test]
    public void AutoSizeShrinksToTheCellsAndPadding()
    {
        Entity grid = Grid(columns: 4, spacing: 6, padding: 3, autoSize: true);
        for (int i = 0; i < 3; i++)
            Cell(grid, 40, 34);

        Frame(2);

        // Three cells in a row, as fewer than four children fill only three columns.
        Vector2 size = grid.GetComponent<Rectangle>().Bounds.Size;
        Assert.That(size, Is.EqualTo(new Vector2(3 * 40 + 2 * 6 + 6, 34 + 6)));
    }

    [Test]
    public void AutoSizeFollowsTheRowCount()
    {
        Entity grid = Grid(columns: 4, spacing: 6, autoSize: true);
        for (int i = 0; i < 16; i++)
            Cell(grid, 40, 34);

        Frame(2);

        Vector2 size = grid.GetComponent<Rectangle>().Bounds.Size;
        Assert.That(size, Is.EqualTo(new Vector2(4 * 40 + 3 * 6, 4 * 34 + 3 * 6)));
    }

    [Test]
    public void IgnoredChildrenTakeNoCell()
    {
        Entity grid = Grid(columns: 2, spacing: 4);
        Entity first = Cell(grid, 20, 20);
        Entity floating = Cell(grid, 200, 200);
        floating.AddTag<LayoutIgnore>();
        Entity second = Cell(grid, 20, 20);

        Frame();

        Assert.That(At(second).X - At(first).X, Is.EqualTo(24f));
        Assert.That(At(second).Y, Is.EqualTo(At(first).Y));
    }

    [Test]
    public void AnAutoSizedGridSettlesInsideAnAutoHeightStack()
    {
        Entity stack = Store.CreateEntity(
            new Rectangle(new Rect2(-60, -10, 120, 20)),
            new UiContainer(StackDirection.Vertical, spacing: 8, autoHeight: true),
            Tags.Get<LayoutDirty>());
        Entity label = Store.CreateEntity(
            new Rectangle(new Rect2(-30, -10, 60, 20)),
            new Position(Vector2.Zero));
        stack.AddChild(label);
        Entity grid = Store.CreateEntity(
            new Rectangle(new Rect2(-5, -5, 10, 10)),
            new Position(Vector2.Zero),
            new UiContainer(
                StackDirection.Horizontal,
                spacing: 6,
                autoWidth: true,
                autoHeight: true,
                columns: 2),
            Tags.Get<LayoutDirty>());
        stack.AddChild(grid);
        for (int i = 0; i < 6; i++)
            Cell(grid, 30, 20);

        Frame(3);

        // Label 20 + gap 8 + three rows of 20 with two 6 px gaps.
        Assert.That(stack.GetComponent<Rectangle>().Bounds.Size.Y, Is.EqualTo(20 + 8 + 3 * 20 + 2 * 6f));
    }

    [Test]
    public void BuilderGridMakesAnAutoSizedColumnContainer()
    {
        UiBuilder ui = UiBuilder.Begin(Store, new FakeVisualRidFactory()).Layer(0);
        ui.Grid(Vector2.Zero, new Vector2(10, 10), columns: 3, spacing: 4, lineSpacing: 8);
        Entity grid = ui.Current;

        UiContainer container = grid.GetComponent<UiContainer>();
        Assert.That(container.Columns, Is.EqualTo(3));
        Assert.That(container.IsGrid, Is.True);
        Assert.That(container.RowSpacing, Is.EqualTo(8f));
        Assert.That(container.AutoWidth, Is.True);
        Assert.That(container.AutoHeight, Is.True);
    }
}
