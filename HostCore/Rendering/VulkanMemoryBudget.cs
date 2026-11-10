using System;
using System.Runtime.InteropServices;
using Xanadu.Systems.Debug;

namespace Xanadu.Host.Rendering;

/// <summary>
/// Video memory use from Vulkan's <c>VK_EXT_memory_budget</c>: per heap, what
/// this process is using and what the driver will let it use. SDL_GPU keeps its
/// own Vulkan handles to itself, so this opens a second, query-only instance
/// (no device, nothing allocated) the first time it is read; the driver counts
/// usage per process, so it sees everything the renderer allocated. Works the
/// same on NVIDIA, AMD and Intel; reads null where there's no Vulkan loader or
/// the extension is missing.
/// </summary>
internal sealed unsafe class VulkanMemoryBudget : IGpuMemorySource, IDisposable
{
    private const int StructureTypeApplicationInfo = 0;
    private const int StructureTypeInstanceCreateInfo = 1;
    private const int StructureTypeMemoryProperties2 = 1000059006;
    private const int StructureTypeMemoryBudget = 1000237000;
    private const uint ApiVersion11 = (1u << 22) | (1u << 12);
    private const uint HeapDeviceLocal = 1;
    private const int MaxHeaps = 16;
    private const int DeviceTypeIntegrated = 1, DeviceTypeDiscrete = 2;

    private delegate* unmanaged<void*, void*, nint*, int> _createInstance;
    private delegate* unmanaged<nint, void*, void> _destroyInstance;
    private delegate* unmanaged<nint, uint*, nint*, int> _enumerateDevices;
    private delegate* unmanaged<nint, byte*, void> _properties;
    private delegate* unmanaged<nint, byte*, uint*, byte*, int> _extensions;
    private delegate* unmanaged<nint, byte*, void> _memoryProperties2;

    private nint _library, _instance, _device;
    private bool _tried, _ready;

    public GpuMemoryReading? Read()
    {
        if (!_tried)
        {
            _tried = true;
            try
            {
                _ready = Open();
            }
            catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
            {
                _ready = false;
            }
        }

        return _ready ? Query() : null;
    }

    private bool Open()
    {
        if (!NativeLibrary.TryLoad("libvulkan.so.1", out _library)
            && !NativeLibrary.TryLoad("vulkan-1", out _library)
            && !NativeLibrary.TryLoad("libvulkan.1.dylib", out _library))
            return false;

        _createInstance = (delegate* unmanaged<void*, void*, nint*, int>)NativeLibrary.GetExport(_library, "vkCreateInstance");
        _destroyInstance = (delegate* unmanaged<nint, void*, void>)NativeLibrary.GetExport(_library, "vkDestroyInstance");
        _enumerateDevices = (delegate* unmanaged<nint, uint*, nint*, int>)NativeLibrary.GetExport(_library, "vkEnumeratePhysicalDevices");
        _properties = (delegate* unmanaged<nint, byte*, void>)NativeLibrary.GetExport(_library, "vkGetPhysicalDeviceProperties");
        _extensions = (delegate* unmanaged<nint, byte*, uint*, byte*, int>)NativeLibrary.GetExport(_library, "vkEnumerateDeviceExtensionProperties");
        _memoryProperties2 = (delegate* unmanaged<nint, byte*, void>)NativeLibrary.GetExport(_library, "vkGetPhysicalDeviceMemoryProperties2");

        // VkApplicationInfo and VkInstanceCreateInfo, laid out by hand (64-bit).
        byte* app = stackalloc byte[48];
        new Span<byte>(app, 48).Clear();
        *(int*)app = StructureTypeApplicationInfo;
        *(uint*)(app + 44) = ApiVersion11;

        byte* create = stackalloc byte[64];
        new Span<byte>(create, 64).Clear();
        *(int*)create = StructureTypeInstanceCreateInfo;
        *(byte**)(create + 24) = app;

        nint instance;
        if (_createInstance(create, null, &instance) != 0)
            return false;
        _instance = instance;

        uint count = 0;
        _enumerateDevices(_instance, &count, null);
        if (count == 0)
            return false;
        nint* devices = stackalloc nint[(int)count];
        _enumerateDevices(_instance, &count, devices);

        // The renderer's GPU: SDL_GPU prefers a discrete card, then an integrated one.
        int best = -1, bestRank = -1;
        byte* properties = stackalloc byte[1024];
        for (int i = 0; i < count; i++)
        {
            _properties(devices[i], properties);
            int type = *(int*)(properties + 16);
            int rank = type == DeviceTypeDiscrete ? 2 : type == DeviceTypeIntegrated ? 1 : 0;
            if (rank > bestRank && HasBudget(devices[i]))
            {
                best = i;
                bestRank = rank;
            }
        }

        if (best < 0)
            return false;
        _device = devices[best];
        return true;
    }

    private bool HasBudget(nint device)
    {
        const int size = 260; // VkExtensionProperties: char[256] name, uint version
        uint count = 0;
        _extensions(device, null, &count, null);
        byte[] buffer = new byte[size * count];
        fixed (byte* extensions = buffer)
        {
            _extensions(device, null, &count, extensions);
            for (int i = 0; i < count; i++)
            {
                if (Marshal.PtrToStringUTF8((nint)(extensions + i * size)) == "VK_EXT_memory_budget")
                    return true;
            }
        }

        return false;
    }

    private GpuMemoryReading Query()
    {
        // VkPhysicalDeviceMemoryBudgetPropertiesEXT: sType, pNext, heapBudget[16], heapUsage[16].
        byte* budget = stackalloc byte[16 + MaxHeaps * 16];
        new Span<byte>(budget, 16 + MaxHeaps * 16).Clear();
        *(int*)budget = StructureTypeMemoryBudget;

        // VkPhysicalDeviceMemoryProperties2: sType, pNext, then VkPhysicalDeviceMemoryProperties
        // (memoryTypeCount, 32 types of 8 bytes, memoryHeapCount, 16 heaps of size + flags, 16 bytes each).
        const int properties = 16;
        byte* memory = stackalloc byte[properties + 520];
        new Span<byte>(memory, properties + 520).Clear();
        *(int*)memory = StructureTypeMemoryProperties2;
        *(byte**)(memory + 8) = budget;

        _memoryProperties2(_device, memory);

        uint heaps = Math.Min(*(uint*)(memory + properties + 260), MaxHeaps);
        long used = 0, allowed = 0, total = 0;
        for (int i = 0; i < heaps; i++)
        {
            byte* heap = memory + properties + 264 + i * 16;
            if ((*(uint*)(heap + 8) & HeapDeviceLocal) == 0)
                continue;
            total += *(long*)heap;
            allowed += *(long*)(budget + 16 + i * 8);
            used += *(long*)(budget + 16 + MaxHeaps * 8 + i * 8);
        }

        return new GpuMemoryReading(used, allowed, total);
    }

    public void Dispose()
    {
        if (_instance != 0)
            _destroyInstance(_instance, null);
        _instance = 0;
        if (_library != 0)
            NativeLibrary.Free(_library);
        _library = 0;
    }
}
