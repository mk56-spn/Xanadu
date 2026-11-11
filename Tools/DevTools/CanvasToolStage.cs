using Xanadu.Host.Input;
using Xanadu.Host.Tools;
using Xanadu.Systems;
using Xanadu.Systems.Canvas;

namespace Xanadu.Tools.DevTools;

/// <summary>What the dev tools lend a tab's stage: the keyboard and typed text.</summary>
internal sealed record DevToolsContext(HostKeyboard Keyboard, HostTextInput TextInput);

/// <summary>
/// A dev tool drawn straight onto a canvas item, laid out for the whole
/// 1920×1080 design space: the stage scales it to fit its tab's area and maps
/// the pointer back, so the tool keeps its own coordinates. Each frame it gets
/// <see cref="Mouse"/>, <see cref="LeftDown"/> and the wheel from the shell
/// and draws in <see cref="Tick"/>.
/// </summary>
internal abstract class CanvasToolStage : IToolStage
{
    protected const float Width = 1920, Height = 1080;

    private readonly Vector2 _origin;
    private readonly float _scale;
    private int _wheel;

    protected CanvasToolStage(DevToolsContext context, Handle item, Rect2 area)
    {
        Context = context;
        Item = item;
        // Not a field initializer: those run before the host has set up its text canvas.
        Canvas = HostPlatform.NewCanvas();
        _scale = System.MathF.Min(area.Size.X / Width, area.Size.Y / Height);
        _origin = area.Position + (area.Size - new Vector2(Width, Height) * _scale) / 2;
        CanvasServer.Current.SetTransform(item, new Transform2D(new Vector2(_scale, 0), new Vector2(0, _scale), _origin));
    }

    protected DevToolsContext Context { get; }
    protected HostKeyboard Keyboard => Context.Keyboard;
    protected HostTextInput TextInput => Context.TextInput;
    protected IRectCanvas Canvas { get; }
    protected Handle Item { get; }

    /// <summary>The pointer in the tool's own 1920×1080 space.</summary>
    protected Vector2 Mouse { get; private set; }

    protected bool LeftDown { get; private set; }

    /// <summary>This frame's wheel notches, once: later calls get 0.</summary>
    protected int TakeWheel()
    {
        int wheel = _wheel;
        _wheel = 0;
        return wheel;
    }

    public void Update(in ToolFrame frame)
    {
        Mouse = (frame.Mouse - _origin) / _scale;
        LeftDown = frame.Held;
        _wheel = frame.Wheel;
        Tick(frame.Delta, frame.Time);
    }

    /// <summary>Draws the tool and acts on this frame's pointer.</summary>
    protected abstract void Tick(float delta, float time);

    /// <summary>Its tab opened (first time, or back to a kept stage).</summary>
    public virtual void Shown() { }

    /// <summary>Its tab closed; a kept stage waits, out of sight.</summary>
    public virtual void Hidden() { }

    public virtual void Dispose() => Canvas.Clear(Item);
}
