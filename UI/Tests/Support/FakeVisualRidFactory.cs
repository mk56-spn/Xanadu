using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Support.Fakes;

/// <summary>Visual factory that does not allocate rendering server resources.</summary>
public sealed class FakeVisualRidFactory : IVisualItemFactory
{
    public Handle CreateCanvasItem() => default;
}
