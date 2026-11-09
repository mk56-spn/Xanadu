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

    // SDL reads the filters until the callback runs, so they live as long as the dialog object.
    private readonly (IntPtr Name, IntPtr Pattern) _audio, _shader;
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

    private void Open((IntPtr Name, IntPtr Pattern) filter, Action<string> then)
    {
        if (_pending is not null)
            return;

        _pending = then;
        Span<SDL.SDL_DialogFileFilter> filters =
            [new SDL.SDL_DialogFileFilter { name = (byte*)filter.Name, pattern = (byte*)filter.Pattern }];
        SDL.SDL_ShowOpenFileDialog(_callback, IntPtr.Zero, _window.Handle, filters, 1, null!, false);
    }

    private static (IntPtr, IntPtr) Filter(string name, string pattern) =>
        (Marshal.StringToCoTaskMemUTF8(name), Marshal.StringToCoTaskMemUTF8(pattern));

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
        foreach ((IntPtr name, IntPtr pattern) in new[] { _audio, _shader })
        {
            Marshal.FreeCoTaskMem(name);
            Marshal.FreeCoTaskMem(pattern);
        }
    }
}
