using MoonWorks.Graphics;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using CompiledShader = Xanadu.Slang.CompiledShader;

namespace Xanadu.Rendering.Passes;

/// <summary>
/// A stack's lists by address, as a stage that reaches them through pointers reads them (<see cref="ListReach"/>):
/// one ulong a list in <see cref="StackBuild.Buffers"/> order, pushed at uniform <see cref="ListReach.Slot"/>.
/// </summary>
internal unsafe struct ListAddresses
{
    private fixed ulong _at[ListReach.MaxLists];

    /// <summary>The addresses of <paramref name="buffers"/> now: asked each time, as SDL may have moved one since.</summary>
    private static ListAddresses Of(Buffer[]? buffers)
    {
        var addresses = new ListAddresses();
        if (buffers is not null)
            for (int k = 0; k < buffers.Length && k < ListReach.MaxLists; k++)
                addresses._at[k] = DeviceLayer.Address(buffers[k]);
        return addresses;
    }

    /// <summary>Pushes the lists' addresses for a compute stage that reaches them.</summary>
    public static void Compute(CommandBuffer commands, CompiledShader stage, Buffer[]? buffers)
    {
        if (stage.Reached)
            commands.PushComputeUniformData(Of(buffers), ListReach.Slot);
    }

    /// <summary>Pushes the lists' addresses for a vertex stage that reaches them.</summary>
    public static void Vertex(CommandBuffer commands, CompiledShader stage, Buffer[]? buffers)
    {
        if (stage.Reached)
            commands.PushVertexUniformData(Of(buffers), ListReach.Slot);
    }

    /// <summary>Pushes the lists' addresses for a fragment stage that reaches them.</summary>
    public static void Fragment(CommandBuffer commands, CompiledShader stage, Buffer[]? buffers)
    {
        if (stage.Reached)
            commands.PushFragmentUniformData(Of(buffers), ListReach.Slot);
    }
}
