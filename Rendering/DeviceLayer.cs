using System.IO;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Buffer = MoonWorks.Graphics.Buffer;

namespace Xanadu.Rendering;

/// <summary>
/// The Vulkan SDL_GPU leaves out, from our layer under it (HostCore/Native/device_layer.c):
/// the API version raised to 1.4, every device feature switched on (64-bit ints and atomics,
/// float atomics, shader clock, mesh shaders, device-generated commands…), storage buffers with
/// GPU addresses, and the barriers SDL can't know a pointer needs. Always on where it's built
/// (Linux, with External/fetch-vulkan-headers.sh); <c>XANADU_DEVICE_LAYER=0</c> leaves it out,
/// <c>=1</c> prints what the device was made with.
/// </summary>
internal static unsafe class DeviceLayer
{
    private const string LayerName = "VK_LAYER_XANADU_device";
    private const string LayerFile = "libxanadu_device.so";
    private const string Command = "xdv:";

    private static delegate* unmanaged<nint, ulong> _address;
    private static delegate* unmanaged<byte*> _report;
    private static delegate* unmanaged<int> _active;
    private static bool _print;

    /// <summary>True once the layer is in the chain and saw the device made.</summary>
    public static bool Active => _active != null && _active() != 0;

    /// <summary>What the device was made with, one line each ("" without the layer).</summary>
    public static string Report => _report == null ? "" : Marshal.PtrToStringUTF8((nint)_report()) ?? "";

    /// <summary>Puts the layer in Vulkan's chain. Must run before the graphics device is made.</summary>
    public static void Install()
    {
        string? setting = Environment.GetEnvironmentVariable("XANADU_DEVICE_LAYER");
        if (setting == "0" || !OperatingSystem.IsLinux())
            return;
        _print = setting == "1";

        string folder = AppContext.BaseDirectory.TrimEnd('/');
        string library = Path.Combine(folder, LayerFile);
        if (!File.Exists(library))
        {
            Console.WriteLine($"Device layer: {LayerFile} isn't built; run External/fetch-vulkan-headers.sh and rebuild");
            return;
        }

        VulkanLayerChain.Add(LayerName, folder);

        // The loader opens the same file, so this handle reaches the layer's own tables.
        nint handle = NativeLibrary.Load(library);
        _address = (delegate* unmanaged<nint, ulong>)NativeLibrary.GetExport(handle, "xdv_address");
        _report = (delegate* unmanaged<byte*>)NativeLibrary.GetExport(handle, "xdv_report");
        _active = (delegate* unmanaged<int>)NativeLibrary.GetExport(handle, "xdv_active");
    }

    /// <summary>Prints the report when asked for (<c>XANADU_DEVICE_LAYER=1</c>), once the device is made.</summary>
    public static void Announce()
    {
        if (!_print)
            return;
        Console.WriteLine(Active ? "Device layer:\n  " + Report.TrimEnd().Replace("\n", "\n  ") : "Device layer: not in the device chain");
    }

    /// <summary>
    /// A storage buffer's GPU address, for a shader to reach it through a pointer. Ask each
    /// frame: SDL swaps the buffer underneath when it cycles or defragments. Throws without the
    /// layer, or for a buffer made without storage usage.
    /// </summary>
    public static ulong Address(Buffer buffer)
    {
        if (!Active)
            throw new InvalidOperationException("Buffer addresses need the device layer (HostCore/Native/device_layer.c); it isn't in the device chain.");
        ulong address = _address(buffer.Handle);
        return address != 0 ? address
            : throw new InvalidOperationException("The device layer has no address for this buffer: is it a storage buffer, and is SDL still 3.2?");
    }

    /// <summary>A memory barrier recorded into <paramref name="commands"/> (outside a render pass), ordering pointer writes before what follows.</summary>
    public static void Barrier(CommandBuffer commands) => Send(commands, "barrier");

    /// <summary>
    /// Turns the barrier the layer puts before every dispatch, render pass and copy (once any
    /// address is handed out) off or back on, from this point in recording.
    /// </summary>
    public static void AutoSync(CommandBuffer commands, bool on) => Send(commands, on ? "sync on" : "sync off");

    private static void Send(CommandBuffer commands, string what)
    {
        if (Active)
            commands.InsertDebugLabel(Command + what);
    }
}
