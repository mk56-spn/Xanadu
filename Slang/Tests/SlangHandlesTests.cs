using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Xanadu.Slang.Tests;

/// <summary>The shapes the editor finds in the code, where their handles sit on the preview, and what dragging one writes, read with the shipped library.</summary>
[TestFixture]
public sealed class SlangHandlesTests
{
    private static readonly Vector2 Canvas = new(400, 300);
    private SlangCompletions _completions = null!;
    private SlangHandleTags _tags = null!;

    [OneTimeSetUp]
    public void Build()
    {
        _completions = SlangCompletions.FromSources(ShippedLibrary.Sources, ShaderConstants.FromSources(ShippedLibrary.Sources), []);
        _tags = SlangHandleTags.FromSources(ShippedLibrary.Sources);
    }

    private List<ShapeChain> Find(string code, Func<string, float[]?>? values = null) =>
        SlangShapeChains.Find(code, _tags, values ?? (_ => null));

    private ChainHandles Handles(ShapeChain chain) => SlangHandles.Of(chain, _tags, _completions.ParameterNames, Canvas)!;

    [Test]
    public void TheLibraryTagsItsShapesAndPlacements()
    {
        Assert.That(_tags.IsShape("Circle") && _tags.IsShape("Box"), Is.True);
        Assert.That(_tags.IsShape("Placement"), Is.False);
        Assert.That(_tags.Of("Box").Select(tag => tag.Kind), Is.EqualTo(new[] { HandleKind.Size, HandleKind.Corner }));
        Assert.That(_tags.MethodFor(HandleKind.Move), Is.EqualTo("moved"));
        Assert.That(_tags.MethodFor(HandleKind.Turn), Is.EqualTo("rotated"));
    }

    [Test]
    public void AChainIsItsConstructorAndSteps()
    {
        List<ShapeChain> chains = Find("float d = Circle(60).moved(float2(40, -12)).rotated(0.5).filled(); // Box(9)");
        Assert.That(chains, Has.Count.EqualTo(1));
        ShapeChain circle = chains[0];
        Assert.That(circle.Shape, Is.EqualTo("Circle"));
        Assert.That(circle.Arguments[0].Value, Is.EqualTo(new[] { 60f }));
        Assert.That(circle.Steps.Select(step => step.Name), Is.EqualTo(new[] { "moved", "rotated", "filled" }));
        Assert.That(circle.Steps[0].Arguments[0].Value, Is.EqualTo(new[] { 40f, -12f }));
        Assert.That(circle.Steps[0].Kind, Is.EqualTo(HandleKind.Move));
    }

    [Test]
    public void APlacedShapesHandlesFollowItsPlacement()
    {
        ChainHandles circle = Handles(Find("Circle(60).moved(float2(40, 10)).scaled(2)")[0]);
        Assert.That(circle.Origin, Is.EqualTo(new Vector2(40, 10)));
        ChainHandle radius = circle.Handles.Single(handle => handle.Kind == HandleKind.Radius);
        Assert.That(radius.At, Is.EqualTo(new Vector2(160, 10)));
        Assert.That(circle.Handles.Select(handle => handle.Kind), Does.Contain(HandleKind.Turn).And.Contain(HandleKind.Scale));
    }

    [Test]
    public void AShapeGivenToAJoinSitsWhereTheJoinIs()
    {
        List<ShapeChain> chains = Find("Circle(60).melted(Box(float2(80)).moved(float2(50, 0)), 20).moved(float2(0, 30))");
        Assert.That(chains.Select(chain => chain.Shape), Is.EqualTo(new[] { "Circle", "Box" }));
        Assert.That(Handles(chains[0]).Origin, Is.EqualTo(new Vector2(0, 30)), "the move after the join moves both");
        Assert.That(Handles(chains[1]).Origin, Is.EqualTo(new Vector2(50, 30)));
    }

    [Test]
    public void AWorkedOutPlacementHasNoHandles()
    {
        ShapeChain chain = Find("Circle(4).moved(s.points[k])")[0];
        Assert.That(SlangHandles.Of(chain, _tags, _completions.ParameterNames, Canvas), Is.Null);
    }

    [Test]
    public void ATunedValueIsKnownAndWritable()
    {
        ShapeChain chain = Find("Circle(size * 2).moved(at)", name => name switch { "size" => [10f], "at" => [5f, 6f], _ => null })[0];
        Assert.That(chain.Arguments[0].Value, Is.EqualTo(new[] { 20f }));
        Assert.That(chain.Arguments[0].Writable, Is.False);
        Assert.That(chain.Steps[0].Arguments[0].Uniform, Is.EqualTo("at"));
        ChainHandles handles = Handles(chain);
        Assert.That(handles.Origin, Is.EqualTo(new Vector2(5, 6)));
        Assert.That(handles.Handles.Select(handle => handle.Kind), Does.Not.Contain(HandleKind.Radius));
    }

    [Test]
    public void DraggingTheBodyAddsAMove()
    {
        string code = "Circle(60).filled()";
        ShapeChain chain = Find(code)[0];
        ChainHandles handles = Handles(chain);
        ChainHandle move = handles.Handles.Single(handle => handle.Kind == HandleKind.Move);
        SlangHandleDrag drag = SlangHandleDrag.Take(handles, move, Vector2.Zero, _completions.ParameterNames);
        HandleEdit edit = drag.Edit(chain, drag.Pull(new Vector2(30.4f, -12.6f), Canvas), _completions.ParameterNames)!.Value;
        Assert.That(Apply(code, edit), Is.EqualTo("Circle(60).moved(float2(30, -13)).filled()"));
    }

    [Test]
    public void DraggingATurnedBoxsEdgeStretchesOneAxis()
    {
        string code = "Box(float2(80, 40)).rotated(1.5707964)";
        ShapeChain chain = Find(code)[0];
        ChainHandles handles = Handles(chain);
        ChainHandle edge = handles.Handles.Single(handle => handle is { Kind: HandleKind.Size, Axis: 0 });
        Assert.That(Vector2.Distance(edge.At, new Vector2(0, 40)), Is.LessThan(1e-3f), "turned a quarter clockwise, its right edge faces down");
        SlangHandleDrag drag = SlangHandleDrag.Take(handles, edge, edge.At, _completions.ParameterNames);
        HandleEdit edit = drag.Edit(chain, drag.Pull(edge.At + new Vector2(0, 10), Canvas), _completions.ParameterNames)!.Value;
        Assert.That(Apply(code, edit), Is.EqualTo("Box(float2(100, 40)).rotated(1.5707964)"));
    }

    [Test]
    public void ACornerNotWrittenYetIsAdded()
    {
        string code = "Box(float2(80, 40))";
        ShapeChain chain = Find(code)[0];
        ChainHandles handles = Handles(chain);
        ChainHandle corner = handles.Handles.Single(handle => handle.Kind == HandleKind.Corner);
        Assert.That(corner.At, Is.EqualTo(new Vector2(40, -20)));
        SlangHandleDrag drag = SlangHandleDrag.Take(handles, corner, corner.At, _completions.ParameterNames);
        HandleEdit edit = drag.Edit(chain, drag.Pull(corner.At - new Vector2(8, 0), Canvas), _completions.ParameterNames)!.Value;
        Assert.That(Apply(code, edit), Is.EqualTo("Box(float2(80, 40), 8)"));
    }

    [Test]
    public void TurningWritesRadians()
    {
        string code = "Circle(20).rotated(0.0)";
        ShapeChain chain = Find(code)[0];
        ChainHandles handles = Handles(chain);
        ChainHandle turn = handles.Handles.Single(handle => handle.Kind == HandleKind.Turn);
        SlangHandleDrag drag = SlangHandleDrag.Take(handles, turn, turn.At, _completions.ParameterNames);
        // From straight up round to the right: a quarter turn clockwise.
        HandleEdit edit = drag.Edit(chain, drag.Pull(new Vector2(48, 0), Canvas), _completions.ParameterNames)!.Value;
        Assert.That(Apply(code, edit), Is.EqualTo("Circle(20).rotated(1.57)"));
    }

    [Test]
    public void MirrorIsTwoSwitchesOnItsStep()
    {
        (string method, IReadOnlyList<HandleTag> flags) = _tags.Switches().Single();
        Assert.That(method, Is.EqualTo("mirrored"));
        Assert.That(flags.Select(flag => (flag.Parameter, flag.On, flag.Label)), Is.EqualTo(new[] { ("x", true, "MIRROR X"), ("y", false, "MIRROR Y") }));
    }

    [Test]
    public void SwitchingMirrorWritesAddsAndTakesOutItsStep()
    {
        IReadOnlyList<HandleTag> flags = _tags.Of("mirrored");
        string Flip(string code, int flag) => Apply(code, SlangShapeSwitches.Flip(Find(code)[0], "mirrored", flags, flag)!.Value);

        string on = Flip("Circle(20).moved(float2(30, 0)).filled()", 0);
        Assert.That(on, Is.EqualTo("Circle(20).moved(float2(30, 0)).mirrored().filled()"), "after the shape's own placement, defaults left out");
        Assert.That(SlangShapeSwitches.IsOn(Find(on)[0], "mirrored", flags, 0), Is.True);
        string both = Flip(on, 1);
        Assert.That(both, Is.EqualTo("Circle(20).moved(float2(30, 0)).mirrored(true, true).filled()"));
        Assert.That(Flip(both, 0), Is.EqualTo("Circle(20).moved(float2(30, 0)).mirrored(false, true).filled()"));
        Assert.That(Flip(on, 0), Is.EqualTo("Circle(20).moved(float2(30, 0)).filled()"), "every switch off takes the step out");
    }

    [Test]
    public void AWorkedOutSwitchIsLeftAlone() =>
        Assert.That(SlangShapeSwitches.Flip(Find("Circle(20).mirrored(wide)")[0], "mirrored", _tags.Of("mirrored"), 1), Is.Null);

    private static string Apply(string code, HandleEdit edit) => code[..edit.From] + edit.Text + code[edit.To..];
}
