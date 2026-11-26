namespace Xanadu.Slang;

/// <summary>
/// A stack's lists reached through pointers instead of bindings (the device layer, Rendering/DeviceLayer.cs): every
/// list a stage declares is a property returning a <c>Ptr&lt;T&gt;</c> (xanadu.store's <c>List&lt;T&gt;</c> under
/// <see cref="Define"/>) from its address in one uniform block at <see cref="Slot"/>, so a stage binds none of them
/// and SDL_GPU's 8 storage buffers a pass stop counting lists. The host pushes every list's address in
/// <see cref="StackBuild.Buffers"/> order, as a ulong each.
/// </summary>
public static class ListReach
{
    /// <summary>The uniform slot the addresses take, in every stage's uniform set: the four a stage has, the last.</summary>
    public const int Slot = 3;

    /// <summary>The most lists a stack keeps: the addresses' block is 512 bytes.</summary>
    public const int MaxLists = 64;

    /// <summary>The define that makes xanadu.store's List&lt;T&gt; a pointer.</summary>
    public const string Define = "XANADU_LIST_POINTERS=1";

    /// <summary>Whether stacks compile their lists as pointers: set by the host once the device layer gives buffers addresses.</summary>
    public static bool On { get; set; }

    /// <summary>The addresses' block and the function reading list k's: one line, for the first declaration to carry.</summary>
    public static string Block(int lists, int set) =>
        $"struct XanaduLists {{ uint4 at[{(lists + 1) / 2}]; }}; [[vk::binding({Slot}, {set})]] ConstantBuffer<XanaduLists> xanaduLists; "
        + "uint64_t xanaduList(uint k) { uint4 v = xanaduLists.at[k / 2]; uint2 a = (k % 2) == 1 ? v.zw : v.xy; return uint64_t(a.x) | (uint64_t(a.y) << 32); } ";

    /// <summary>List <paramref name="k"/> declared as its pointer, by its name.</summary>
    public static string Declared(string type, string name, int k) =>
        $"property List<{type}> {name} {{ get {{ return List<{type}>(xanaduList({k})); }} }}";
}
