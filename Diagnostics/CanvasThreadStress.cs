using System;
using System.Threading.Tasks;
using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// Answers one question with numbers: can the per-frame RenderingServer canvas
/// calls be issued from worker threads, and does that take the cost off the main
/// thread?
/// <para>
/// Creates <see cref="TriangleCount"/> independent canvas items (one RID each) and
/// issues one command per item every frame, cycling through
/// <see cref="CommandKinds"/> different RenderingServer calls so the workload is
/// not a single repeated call the renderer can batch and fast-path. Draw commands
/// are preceded by a clear, the way a real UI clears and redraws.
/// </para>
/// <para>
/// The transform maths runs on the main thread in both modes — only the command
/// submission differs. F9 toggles serial submission against
/// <see cref="Parallel.For"/>; F10 collapses the mix back to one command kind so
/// the two baselines can be compared.
/// </para>
/// </summary>
public partial class CanvasThreadStress : Node2D
{
    private const int SampleCount = 60;
    private const int WarmupFrames = 30;
    private const int CommandKinds = 30;

    [Export]
    public int TriangleCount { get; set; } = 3000;

    /// <summary>Cap on parallelism for the worker path; 0 means let the pool decide.</summary>
    [Export]
    public int Workers { get; set; } = 4;

    /// <summary>Toggled at runtime with F9.</summary>
    [Export]
    public bool UseWorkers { get; set; }

    /// <summary>Cycle all <see cref="CommandKinds"/> commands, or repeat one. Toggled with F10.</summary>
    [Export]
    public bool VariedCommands { get; set; } = true;

    private static readonly Vector2 Size = new(14, 14);

    private readonly double[] _samples = new double[SampleCount];
    private readonly Color[] _palette =
    {
        new(0.95f, 0.55f, 0.2f),
        new(0.35f, 0.8f, 1f),
        new(0.6f, 0.95f, 0.55f),
        new(1f, 0.9f, 0.35f),
        new(0.85f, 0.55f, 1f),
        new(1f, 0.45f, 0.55f),
        new(0.5f, 0.7f, 0.95f),
        new(0.95f, 0.95f, 0.95f),
    };

    private Vector2[] _triangle = Array.Empty<Vector2>();
    private Vector2[] _line = Array.Empty<Vector2>();
    private Color[] _triangleColors = Array.Empty<Color>();
    private Color[] _lineColors = Array.Empty<Color>();
    private int[] _indices = Array.Empty<int>();

    private Rid[] _items = Array.Empty<Rid>();
    private Transform2D[] _transforms = Array.Empty<Transform2D>();
    private ImageTexture _texture = null!;
    private Rid _textureRid;
    private Label _hud = null!;
    private ParallelOptions _options = new();
    private int _next;
    private int _warmup = WarmupFrames;

    public override void _Ready()
    {
        // The comparison is meaningless capped: with vsync on both modes just sit
        // at the refresh rate. Do it here as well as on the command line so this
        // behaves the same when run straight from the Godot editor.
        try
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        }
        catch (Exception)
        {
            // A headless run has no window to configure.
        }

        Engine.MaxFps = 0;

        _options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Workers > 0 ? Workers : -1,
        };

        var image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        _texture = ImageTexture.CreateFromImage(image);
        _textureRid = _texture.GetRid();

        _triangle = new[]
        {
            new Vector2(0, -7),
            new Vector2(7, 7),
            new Vector2(-7, 7),
        };
        _line = new[] { new Vector2(-7, 0), new Vector2(7, 0) };
        _triangleColors = new[] { Colors.White, Colors.White, Colors.White };
        _lineColors = new[] { Colors.White, Colors.White };
        _indices = new[] { 0, 1, 2 };

        _items = new Rid[TriangleCount];
        _transforms = new Transform2D[TriangleCount];

        Rid canvas = GetCanvasItem();
        for (int i = 0; i < TriangleCount; i++)
        {
            Rid item = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(item, canvas);
            RenderingServer.CanvasItemAddPolygon(item, _triangle, _triangleColors);
            _items[i] = item;
        }

        _hud = new Label
        {
            Position = new Vector2(16, 12),
            Text = "starting",
        };
        _hud.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_hud);
    }

    public override void _Process(double delta)
    {
        ulong frameStart = Time.GetTicksUsec();
        float time = frameStart * 1e-6f;
        Vector2 center = GetViewportRect().Size / 2f;

        // Identical work in both modes: only the RenderingServer calls differ.
        float step = Mathf.Tau / Mathf.Max(1, TriangleCount);
        for (int i = 0; i < TriangleCount; i++)
        {
            float angle = step * i + time * 0.7f;
            float radius = 240f + 120f * Mathf.Sin(time * 1.7f + i * 0.013f);
            var position = new Vector2(
                center.X + Mathf.Cos(angle) * radius,
                center.Y + Mathf.Sin(angle * 1.31f) * radius * 0.6f);
            _transforms[i] = new Transform2D(0f, position);
        }

        ulong submitStart = Time.GetTicksUsec();
        double prepMs = (submitStart - frameStart) / 1000.0;

        if (UseWorkers)
        {
            Parallel.For(0, TriangleCount, _options, Submit);
        }
        else
        {
            for (int i = 0; i < TriangleCount; i++)
                Submit(i);
        }

        double issueMs = (Time.GetTicksUsec() - submitStart) / 1000.0;

        if (_warmup > 0)
        {
            _warmup--;
        }
        else
        {
            _samples[_next++ % SampleCount] = issueMs;
        }

        double average = 0;
        foreach (double sample in _samples)
            average += sample;
        average /= SampleCount;

        string kinds = VariedCommands ? $"{CommandKinds} kinds" : "1 kind";
        _hud.Text =
            $"{(UseWorkers ? $"WORKERS x{_options.MaxDegreeOfParallelism}" : "MAIN THREAD")}   " +
            $"items {TriangleCount}   {kinds}   vsync {VsyncLabel()}\n" +
            $"submit {issueMs:0.000} ms   avg {average:0.000} ms   " +
            $"prep {prepMs:0.000} ms   fps {Engine.GetFramesPerSecond()}\n" +
            "F9 threads   F10 command mix";
    }

    /// <summary>Issues the command for item <paramref name="index"/>.</summary>
    private void Submit(int index)
    {
        Rid item = _items[index];
        Transform2D transform = _transforms[index];
        Color tint = _palette[index & 7];
        Vector2 at = transform.Origin;
        var body = new Rect2(at - Size / 2f, Size);

        int kind = VariedCommands ? index % CommandKinds : 0;
        switch (kind)
        {
            case 0:
                RenderingServer.CanvasItemSetTransform(item, transform);
                break;

            case 1:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddRect(item, body, tint);
                break;

            case 2:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddCircle(item, at, 7f, tint);
                break;

            case 3:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddLine(
                    item, at - new Vector2(7, 0), at + new Vector2(7, 0), tint, 2f, true);
                break;

            case 4:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddPolygon(
                    item, _triangle, _triangleColors, null, default);
                break;

            case 5:
                RenderingServer.CanvasItemSetModulate(item, tint);
                break;

            case 6:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddRect(item, body, tint, false);
                break;

            case 7:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddCircle(item, at, 7f, tint, true);
                break;

            case 8:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddPolyline(
                    item, _line, _lineColors, 2f, true);
                break;

            case 9:
                RenderingServer.CanvasItemSetSelfModulate(item, tint);
                break;

            case 10:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddTextureRect(
                    item, body, _textureRid, false, null, false);
                break;

            case 11:
                RenderingServer.CanvasItemSetZIndex(item, index & 15);
                break;

            case 12:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddPrimitive(
                    item, _triangle, _triangleColors, null, default);
                break;

            case 13:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddTextureRectRegion(
                    item, body, _textureRid, new Rect2(0, 0, 8, 8), null, false, false);
                break;

            case 14:
                RenderingServer.CanvasItemSetVisible(item, true);
                break;

            case 15:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddNinePatch(
                    item, body, new Rect2(0, 0, 16, 16), _textureRid,
                    new Vector2(4, 4), new Vector2(4, 4),
                    RenderingServer.NinePatchAxisMode.Stretch,
                    RenderingServer.NinePatchAxisMode.Stretch,
                    true, null);
                break;

            case 16:
                RenderingServer.CanvasItemSetDefaultTextureFilter(
                    item, RenderingServer.CanvasItemTextureFilter.Linear);
                break;

            case 17:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddTriangleArray(
                    item, _indices, _triangle, _triangleColors,
                    null, null, null, default, 3);
                break;

            case 18:
                RenderingServer.CanvasItemSetDefaultTextureRepeat(
                    item, RenderingServer.CanvasItemTextureRepeat.Disabled);
                break;

            case 19:
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemAddMultiline(
                    item, _triangle, _triangleColors, 2f);
                break;

            case 20:
                RenderingServer.CanvasItemSetLightMask(item, 1);
                break;

            case 21:
                RenderingServer.CanvasItemAddSetTransform(item, transform);
                break;

            case 22:
                RenderingServer.CanvasItemSetSortChildrenByY(item, false);
                break;

            case 23:
                RenderingServer.CanvasItemAddAnimationSlice(item, 1.0, 0.0, 1.0, 1.0);
                break;

            case 24:
                RenderingServer.CanvasItemSetZAsRelativeToParent(item, true);
                break;

            case 25:
                RenderingServer.CanvasItemAddClipIgnore(item, false);
                break;

            case 26:
                RenderingServer.CanvasItemSetDrawBehindParent(item, false);
                break;

            case 27:
                RenderingServer.CanvasItemSetDistanceFieldMode(item, false);
                break;

            case 28:
                RenderingServer.CanvasItemSetInterpolated(item, true);
                break;

            default:
                RenderingServer.CanvasItemSetDrawIndex(item, 0);
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.F9:
                UseWorkers = !UseWorkers;
                break;
            case Key.F10:
                VariedCommands = !VariedCommands;
                break;
            default:
                return;
        }

        Array.Clear(_samples);
        _warmup = WarmupFrames;
    }

    public override void _ExitTree()
    {
        foreach (Rid item in _items)
        {
            if (item.IsValid)
                RenderingServer.FreeRid(item);
        }

        _texture?.Dispose();
    }

    private static string VsyncLabel()
    {
        try
        {
            return DisplayServer.WindowGetVsyncMode().ToString();
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}
