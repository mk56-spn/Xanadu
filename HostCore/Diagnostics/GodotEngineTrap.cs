using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Xanadu.Host.Diagnostics;

/// <summary>
/// Without the engine, every Godot native call jumps through a null pointer and
/// the process dies with no trace (docs/moonworks-port.md §3). This fills
/// GodotSharp's native function table with one stub that prints the managed
/// stack of the offending call and exits, so leftover engine calls name
/// themselves while the port removes them.
/// </summary>
internal static unsafe class GodotEngineTrap
{
    public static void Install()
    {
        Type? funcs = typeof(Godot.Rid).Assembly.GetType("Godot.NativeInterop.NativeFuncs");
        FieldInfo? table = funcs?.GetField("_unmanagedCallbacks", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? initialize = funcs?.GetMethod("Initialize", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (table is null || initialize is null)
        {
            Console.Error.WriteLine("GodotEngineTrap: GodotSharp's native table has moved; trap not installed.");
            return;
        }

        int size = Marshal.SizeOf(table.FieldType);
        IntPtr block = Marshal.AllocHGlobal(size);
        var entries = new Span<IntPtr>((void*)block, size / IntPtr.Size);
        entries.Fill((IntPtr)(delegate* unmanaged<void>)&Trap);
        initialize.Invoke(null, [block, size]);
    }

    [UnmanagedCallersOnly]
    private static void Trap()
    {
        Console.Error.WriteLine("Godot engine call outside the engine:");
        Console.Error.WriteLine(Environment.StackTrace);
        Console.Error.Flush();
        Environment.Exit(70);
    }
}
