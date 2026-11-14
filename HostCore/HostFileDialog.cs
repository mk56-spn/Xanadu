using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MoonWorks;
using SDL3;

namespace Xanadu.Host;

/// <summary>
/// SDL's native open-file dialog (the desktop portal on Linux). SDL may call
/// back on another thread, so the picked path is queued and handed over on the
/// main thread in <see cref="Poll"/>.
/// </summary>
internal sealed unsafe class HostFileDialog : IDisposable
{
    private readonly Window _window;
    private readonly ConcurrentQueue<(string Path, Action<string> Then)> _picked = new();
    private readonly SDL.SDL_DialogFileCallback _callback;

    // SDL reads the filters until the callback runs, so they (the array and its strings)
    // live in native memory as long as the dialog object.
    private readonly SDL.SDL_DialogFileFilter* _audio, _shader;
    private volatile Action<string>? _pending;

    public HostFileDialog(Window window)
    {
        _window = window;
        _callback = OnPicked;
        _audio = Filter("Audio files", "mp3;wav;ogg;flac");
        _shader = Filter("Slang shaders", "slang");
    }

    /// <summary>True from asking until the picker closes, picked or cancelled.</summary>
    public bool IsOpen => _pending is not null;

    /// <summary>Asks for one audio file; <paramref name="then"/> runs on the main thread if one is picked.</summary>
    public void OpenAudio(Action<string> then) => Open(_audio, then);

    /// <summary>Asks for one Slang shader; <paramref name="then"/> runs on the main thread if one is picked.</summary>
    public void OpenShader(Action<string> then) => Open(_shader, then);

    private void Open(SDL.SDL_DialogFileFilter* filter, Action<string> then)
    {
        if (_pending is not null)
            return;

        _pending = then;
        var filters = new Span<SDL.SDL_DialogFileFilter>(filter, 1);
        SDL.SDL_ShowOpenFileDialog(_callback, IntPtr.Zero, _window.Handle, filters, 1, null!, false);
    }

    private static SDL.SDL_DialogFileFilter* Filter(string name, string pattern)
    {
        var filter = (SDL.SDL_DialogFileFilter*)NativeMemory.Alloc((nuint)sizeof(SDL.SDL_DialogFileFilter));
        filter->name = (byte*)Marshal.StringToCoTaskMemUTF8(name);
        filter->pattern = (byte*)Marshal.StringToCoTaskMemUTF8(pattern);
        return filter;
    }

    private void OnPicked(IntPtr userdata, IntPtr files, int filter)
    {
        Action<string>? then = _pending;
        _pending = null;
        if (then is null || files == IntPtr.Zero)
            return;

        // A null-terminated list of UTF-8 paths; empty when cancelled.
        IntPtr first = Marshal.ReadIntPtr(files);
        if (first != IntPtr.Zero && Marshal.PtrToStringUTF8(first) is { Length: > 0 } path)
            _picked.Enqueue((path, then));
    }

    /// <summary>Runs the follow-up for any file picked since the last call.</summary>
    public void Poll()
    {
        while (_picked.TryDequeue(out var picked))
            picked.Then(picked.Path);
    }

    public void Dispose()
    {
        Free(_audio);
        Free(_shader);
    }

    private static void Free(SDL.SDL_DialogFileFilter* filter)
    {
        Marshal.FreeCoTaskMem((IntPtr)filter->name);
        Marshal.FreeCoTaskMem((IntPtr)filter->pattern);
        NativeMemory.Free(filter);
    }
}
