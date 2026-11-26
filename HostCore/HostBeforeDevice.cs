using Xanadu.Rendering;

namespace Xanadu.Host;

/// <summary>What has to be in place before SDL makes the graphics device: our Vulkan layers and the late-tick log.</summary>
internal static class HostBeforeDevice
{
    public static void Install()
    {
        GpuTimes.Install();
        DeviceLayer.Install();
        HostLateTickLog.Install();
    }
}
