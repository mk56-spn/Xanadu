using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Xanadu.Rendering;

/// <summary>
/// Puts our Vulkan layers in the loader's chain (VK_ADD_LAYER_PATH, VK_INSTANCE_LAYERS) before SDL
/// makes its instance. One owner for the two variables: .NET doesn't see what libc's setenv wrote,
/// so a layer reading the variable back would drop the one added before it.
/// </summary>
internal static class VulkanLayerChain
{
    private static readonly List<string> Layers = [];
    private static readonly List<string> Folders = [];

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetEnv(string name, string value, int overwrite);

    /// <summary>Adds a layer, its manifest in <paramref name="folder"/>, after the ones added already (and the user's own).</summary>
    public static void Add(string layer, string folder)
    {
        if (!Layers.Contains(layer))
            Layers.Add(layer);
        if (!Folders.Contains(folder))
            Folders.Add(folder);
        SetEnv("VK_ADD_LAYER_PATH", Join("VK_ADD_LAYER_PATH", Folders), 1);
        SetEnv("VK_INSTANCE_LAYERS", Join("VK_INSTANCE_LAYERS", Layers), 1);
    }

    private static string Join(string variable, List<string> ours)
    {
        string? theirs = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(theirs) ? string.Join(':', ours) : theirs + ":" + string.Join(':', ours);
    }
}
