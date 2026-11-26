using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Xanadu.Slang;

/// <summary>
/// Slang's C API, called directly: target code for a command line, and a module container
/// for one (which Slangc.NET doesn't carry). The native library is the one Slangc.NET's
/// package ships.
/// </summary>
/// <remarks>
/// Sessions are made on first use and kept, a few at most (<see cref="MostSessions"/>). Making one
/// takes about 200 ms and 200 MB (it reads Slang's whole core module), and Slang gives none of that
/// back when a session is destroyed, so one is made only when every other is busy: a session isn't
/// safe to use from two threads, so each call borrows one for itself. Compiles from one thread at a
/// time (the editor's) keep one session; compiles side by side (the tests) get up to
/// <see cref="MostSessions"/>. A launch that finds everything in the shader cache never makes one.
/// </remarks>
internal static class SlangNative
{
    private const string Library = "slang-compiler";
    private const int ModuleContainer = 1;

    [DllImport(Library)] private static extern IntPtr spCreateSession(IntPtr unused);
    [DllImport(Library)] private static extern IntPtr spCreateCompileRequest(IntPtr session);
    [DllImport(Library)] private static extern void spDestroyCompileRequest(IntPtr request);
    [DllImport(Library)] private static extern int spProcessCommandLineArguments(IntPtr request, IntPtr[] args, int count);
    [DllImport(Library)] private static extern void spSetOutputContainerFormat(IntPtr request, int format);
    [DllImport(Library)] private static extern int spCompile(IntPtr request);
    [DllImport(Library)] private static extern IntPtr spGetDiagnosticOutput(IntPtr request);
    [DllImport(Library)] private static extern IntPtr spGetCompileRequestCode(IntPtr request, out UIntPtr size);
    [DllImport(Library)] private static extern int spGetContainerCode(IntPtr request, out IntPtr blob);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Release(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr BufferPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate UIntPtr BufferSize(IntPtr self);

    /// <summary>
    /// The most sessions ever made: compiles past this many at once wait for one. 4 unless the process sets
    /// <c>XANADU_SLANG_SESSIONS</c> before its first compile (the test suites take more).
    /// </summary>
    public static int MostSessions { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("XANADU_SLANG_SESSIONS"), out int asked) && asked > 0 ? asked : 4;

    private static readonly System.Threading.SemaphoreSlim Free = new(MostSessions, MostSessions);
    private static readonly Stack<IntPtr> Idle = [];

    static SlangNative() => NativeLibrary.SetDllImportResolver(typeof(SlangNative).Assembly, Resolve);

    /// <summary>Slang's own native library, from where Slangc.NET's package puts it.</summary>
    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? paths)
    {
        if (name != Library)
            return IntPtr.Zero;
        string file = OperatingSystem.IsWindows() ? "slang-compiler.dll"
            : OperatingSystem.IsMacOS() ? "libslang-compiler.dylib" : "libslang-compiler.so";
        string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        foreach (string candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native", file),
                     Path.Combine(AppContext.BaseDirectory, file),
                 })
        {
            if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    /// <summary>Makes the session now if it isn't there, so the first compile doesn't wait for it.</summary>
    public static void Warm() => Give(Borrow());

    /// <summary>A session for this call alone: an idle one, or a new one while fewer than <see cref="MostSessions"/> exist.</summary>
    private static IntPtr Borrow()
    {
        Free.Wait();
        lock (Idle)
            if (Idle.TryPop(out IntPtr idle))
                return idle;
        return spCreateSession(IntPtr.Zero);
    }

    private static void Give(IntPtr session)
    {
        lock (Idle)
            Idle.Push(session);
        Free.Release();
    }

    /// <summary>The target code a <c>slangc</c> command line gives; throws with Slang's diagnostics when it refuses.</summary>
    public static byte[] Compile(IReadOnlyList<string> args)
    {
        IntPtr session = Borrow();
        try
        {
            IntPtr request = spCreateCompileRequest(session);
            IntPtr[] strings = [.. args.Select(Marshal.StringToCoTaskMemUTF8)];
            try
            {
                int result = spProcessCommandLineArguments(request, strings, strings.Length);
                if (result >= 0)
                    result = spCompile(request);
                if (result < 0)
                    // A plain Exception, as Slangc.NET threw: its message is what callers read.
                    throw new Exception(Diagnostics(request) is { Length: > 0 } said ? said : "Slang refused it");

                IntPtr code = spGetCompileRequestCode(request, out UIntPtr size);
                byte[] bytes = new byte[checked((int)size.ToUInt64())];
                if (bytes.Length > 0)
                    Marshal.Copy(code, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                foreach (IntPtr text in strings)
                    Marshal.FreeCoTaskMem(text);
                spDestroyCompileRequest(request);
            }
        }
        finally
        {
            Give(session);
        }
    }

    /// <summary>One module's container (a <c>.slang-module</c>'s bytes), or null with Slang's diagnostics.</summary>
    public static byte[]? BuildModule(IReadOnlyList<string> args, out string diagnostics)
    {
        IntPtr session = Borrow();
        try
        {
            IntPtr request = spCreateCompileRequest(session);
            IntPtr[] strings = [.. args.Select(Marshal.StringToCoTaskMemUTF8)];
            try
            {
                int result = spProcessCommandLineArguments(request, strings, strings.Length);
                if (result >= 0)
                {
                    spSetOutputContainerFormat(request, ModuleContainer);
                    result = spCompile(request);
                }

                diagnostics = Diagnostics(request);
                if (result < 0 || spGetContainerCode(request, out IntPtr blob) < 0 || blob == IntPtr.Zero)
                    return null;
                return Read(blob);
            }
            finally
            {
                foreach (IntPtr text in strings)
                    Marshal.FreeCoTaskMem(text);
                spDestroyCompileRequest(request);
            }
        }
        finally
        {
            Give(session);
        }
    }

    private static string Diagnostics(IntPtr request) => Marshal.PtrToStringUTF8(spGetDiagnosticOutput(request)) ?? "";

    /// <summary>An ISlangBlob's bytes, then the blob released (its vtable: query, add, release, pointer, size).</summary>
    private static byte[] Read(IntPtr blob)
    {
        IntPtr table = Marshal.ReadIntPtr(blob);
        IntPtr Slot(int index) => Marshal.ReadIntPtr(table, index * IntPtr.Size);
        try
        {
            IntPtr data = Marshal.GetDelegateForFunctionPointer<BufferPointer>(Slot(3))(blob);
            int size = checked((int)Marshal.GetDelegateForFunctionPointer<BufferSize>(Slot(4))(blob).ToUInt64());
            byte[] bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.GetDelegateForFunctionPointer<Release>(Slot(2))(blob);
        }
    }
}
