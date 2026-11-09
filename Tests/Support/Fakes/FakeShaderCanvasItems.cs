using Godot;
using Xanadu.Rendering;
using Xanadu.Systems.Shaders;

namespace Xanadu.Tests.Support.Fakes;

/// <summary>
/// Shader canvas items for draw systems under test. The real ones call the
/// rendering server, which outside the engine is a null native pointer that
/// crashes the test host instead of failing a test.
/// </summary>
public sealed class FakeShaderCanvasItems : IShaderCanvasItems
{
    private ulong _next = 1_000;

    public Rid CreateItem(Rid parent) => TestRid.Make(_next++);

    public void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend) { }
}
