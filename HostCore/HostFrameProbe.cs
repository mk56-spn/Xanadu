using System;
using System.Diagnostics;
using SDL3;

namespace Xanadu.Host;

/// <summary>
/// Diagnostic for the focused-window slowdown: once a second, prints where the
/// frame went (our Update, our Draw split at swapchain acquire, and everything
/// outside our code: MoonWorks' pacing sleep, WaitForSwapchain and event
/// pumping) and how many SDL events arrived, by kind.
/// On with <c>XANADU_FRAME_PROBE=1</c>; <c>XANADU_NO_MOTION=1</c> also turns
/// off SDL's mouse-motion events (the position still comes from SDL_GetMouseState).
/// </summary>
internal sealed class HostFrameProbe
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("XANADU_FRAME_PROBE") == "1";

    private readonly Stopwatch _second = Stopwatch.StartNew();
    private readonly Stopwatch _phase = new();
    private readonly SDL.SDL_EventFilter _watch;
    private long _frameStart = Stopwatch.GetTimestamp();
    private int _frames;
    private double _update, _acquire, _draw, _outside, _worstFrame;
    private int _motion, _buttons, _keys, _other;

    public unsafe HostFrameProbe()
    {
        if (Environment.GetEnvironmentVariable("XANADU_NO_MOTION") == "1")
            SDL.SDL_SetEventEnabled((uint)SDL.SDL_EventType.SDL_EVENT_MOUSE_MOTION, false);

        _watch = Count;
        SDL.SDL_AddEventWatch(_watch, IntPtr.Zero);
    }

    private unsafe bool Count(IntPtr userdata, SDL.SDL_Event* evt)
    {
        switch ((SDL.SDL_EventType)evt->type)
        {
            case SDL.SDL_EventType.SDL_EVENT_MOUSE_MOTION: _motion++; break;
            case SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or SDL.SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP
                or SDL.SDL_EventType.SDL_EVENT_MOUSE_WHEEL: _buttons++; break;
            case SDL.SDL_EventType.SDL_EVENT_KEY_DOWN or SDL.SDL_EventType.SDL_EVENT_KEY_UP: _keys++; break;
            default: _other++; break;
        }
        return true;
    }

    public void BeginPhase() => _phase.Restart();

    public void EndUpdate() => _update += _phase.Elapsed.TotalMilliseconds;

    public void EndAcquire() => _acquire += _phase.Elapsed.TotalMilliseconds;

    /// <summary>End of Draw: closes the frame and prints once a second.</summary>
    public void EndDraw()
    {
        _draw += _phase.Elapsed.TotalMilliseconds;
        long now = Stopwatch.GetTimestamp();
        double frame = Stopwatch.GetElapsedTime(_frameStart, now).TotalMilliseconds;
        _frameStart = now;
        _frames++;
        _worstFrame = Math.Max(_worstFrame, frame);
        _outside += frame;

        if (_second.Elapsed.TotalSeconds < 1)
            return;

        double n = _frames;
        double inside = _update + _draw;
        Console.WriteLine(
            $"probe: {n / _second.Elapsed.TotalSeconds:F0} fps | frame {_outside / n:F2} ms (worst {_worstFrame:F2}) | " +
            $"update {_update / n:F2} | acquire {_acquire / n:F2} | draw total {_draw / n:F2} | " +
            $"outside our code {(_outside - inside) / n:F2} | events/s motion {_motion} buttons {_buttons} keys {_keys} other {_other}");
        _frames = 0;
        _update = _acquire = _draw = _outside = _worstFrame = 0;
        _motion = _buttons = _keys = _other = 0;
        _second.Restart();
    }
}
