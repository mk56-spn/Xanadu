using System.Globalization;
using System.Linq;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The animator's command-line set-up, read once into typed values so an agent
/// can check the tool without a desktop capture. Rig names (bones, clips,
/// parts) stay names here, as typed; <see cref="AnimatorTool"/> looks them up
/// in the loaded package once, when it applies them.
/// </summary>
/// <remarks>
/// <c>--resolution=WxH</c> window size (default 1600x900);
/// <c>--shot=path</c> capture there after <c>--shot-frames=n</c> (default 60) or <c>--shot-seconds=s</c>, then quit;
/// <c>--zoom=x</c> preview zoom once fitted; <c>--timeline-zoom=x</c> key timeline zoom;
/// <c>--flow=from&gt;to</c> open the FLOW tab on that hand-over and preview it;
/// <c>--clip=name@seconds</c> open that clip paused there;
/// <c>--streak=bone</c>, <c>--sparks=bone</c> hang a streak or particle stream off that bone;
/// <c>--dress=bone</c> add a dress hanging from that bone, selected;
/// <c>--edit-xif=item</c> or <c>item@base.slang+layer.slang</c> open the XIF editor on a part or cloth (empty: the first part with art);
/// <c>--part=index</c>, <c>--cloth=index</c> select that part or cloth;
/// <c>--paint=bone</c> paint the selected part's weights for that bone;
/// <c>--paths=bone,bone</c> (or <c>bone,bone@clip</c>) motion paths on, those bones selected;
/// <c>--plant=bone,bone</c> plant those bones; <c>--orbit=bone,bone</c> send them round an ellipse;
/// <c>--add-curve=index</c> give that part a curve shape;
/// <c>--perf-drag=bone</c> or <c>bone,ik</c> drag that joint round for 300 frames and print what each system cost;
/// and the switches <c>--layers</c>, <c>--full-keys</c>, <c>--rig</c>, <c>--shortcuts</c>, <c>--key</c>,
/// <c>--easing</c>, <c>--attacks</c>, <c>--preview</c>.
/// </remarks>
internal sealed class AnimatorLaunch
{
    public readonly record struct Handover(string From, string To);

    public readonly record struct ClipAt(string Clip, float? Seconds);

    /// <summary>What the XIF editor opens on, and optionally a common shader for it with layers stacked over.</summary>
    public readonly record struct XifTarget(string Item, string? Shader, string[] Layers);

    public readonly record struct MotionPaths(string[] Bones, string? Clip);

    public readonly record struct PerfDrag(string Bone, bool Ik);

    public uint Width { get; private set; } = 1600;
    public uint Height { get; private set; } = 900;

    public string? ShotPath { get; private set; }
    public int ShotFrames { get; private set; } = 60;
    public float? ShotSeconds { get; private set; }
    public float? Zoom { get; private set; }
    public float? TimelineZoom { get; private set; }

    public Handover? Flow { get; private set; }
    public ClipAt? Clip { get; private set; }
    public string? Streak { get; private set; }
    public string? Sparks { get; private set; }
    public string? Dress { get; private set; }
    public XifTarget? EditXif { get; private set; }
    public int? Part { get; private set; }
    public int? Cloth { get; private set; }
    public string? Paint { get; private set; }
    public MotionPaths? Paths { get; private set; }
    public string[] Plant { get; private set; } = [];
    public string[] Orbit { get; private set; } = [];
    public int? AddCurve { get; private set; }
    public PerfDrag? Perf { get; private set; }

    public bool Layers { get; private set; }
    public bool FullKeys { get; private set; }
    public bool EditRig { get; private set; }
    public bool Shortcuts { get; private set; }
    public bool KeyNow { get; private set; }
    public bool Easing { get; private set; }
    public bool Attacks { get; private set; }
    public bool Preview { get; private set; }

    public static AnimatorLaunch Read(string[] args)
    {
        var launch = new AnimatorLaunch();
        foreach (string argument in args)
            launch.ReadOne(argument);
        return launch;
    }

    private void ReadOne(string argument)
    {
        switch (argument)
        {
            case "--layers": Layers = true; return;
            case "--full-keys": FullKeys = true; return;
            case "--rig": EditRig = true; return;
            case "--shortcuts": Shortcuts = true; return;
            case "--key": KeyNow = true; return;
            case "--easing": Easing = true; return;
            case "--attacks": Attacks = true; return;
            case "--preview": Preview = true; return;
        }

        int equals = argument.IndexOf('=');
        if (equals < 0)
            return;
        string value = argument[(equals + 1)..];
        switch (argument[..equals])
        {
            case "--resolution":
                string[] size = value.Split('x');
                if (size.Length == 2 && uint.TryParse(size[0], out uint width) && uint.TryParse(size[1], out uint height))
                    (Width, Height) = (width, height);
                break;
            case "--shot":
                ShotPath = System.IO.Path.GetFullPath(value);
                break;
            case "--shot-frames" when int.TryParse(value, out int frames):
                ShotFrames = Math.Max(2, frames);
                break;
            case "--shot-seconds" when Number(value) is { } seconds:
                ShotSeconds = Math.Max(0f, seconds);
                break;
            case "--zoom":
                Zoom = Number(value);
                break;
            case "--timeline-zoom":
                TimelineZoom = Number(value);
                break;
            case "--flow":
                string[] clips = value.Split('>');
                if (clips.Length == 2)
                    Flow = new Handover(clips[0], clips[1]);
                break;
            case "--clip":
                (string clip, string? at) = Split(value, '@');
                Clip = new ClipAt(clip, at is null ? null : Number(at));
                break;
            case "--streak":
                Streak = value;
                break;
            case "--sparks":
                Sparks = value;
                break;
            case "--dress":
                Dress = value;
                break;
            case "--edit-xif":
                (string item, string? shaders) = Split(value, '@');
                string[] stack = shaders?.Split('+') ?? [];
                EditXif = new XifTarget(item, stack.Length > 0 && stack[0].Length > 0 ? stack[0] : null, [.. stack.Skip(1)]);
                break;
            case "--part":
                Part = int.TryParse(value, out int part) ? part : null;
                break;
            case "--cloth":
                Cloth = int.TryParse(value, out int cloth) ? cloth : null;
                break;
            case "--paint":
                Paint = value;
                break;
            case "--paths":
                (string bones, string? pathsClip) = Split(value, '@');
                Paths = new MotionPaths(Names(bones), pathsClip);
                break;
            case "--plant":
                Plant = Names(value);
                break;
            case "--orbit":
                Orbit = Names(value);
                break;
            case "--add-curve":
                AddCurve = int.TryParse(value, out int curvePart) ? curvePart : null;
                break;
            case "--perf-drag":
                bool ik = value.EndsWith(",ik", StringComparison.Ordinal);
                Perf = new PerfDrag(ik ? value[..^3] : value, ik);
                break;
        }
    }

    private static float? Number(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : null;

    private static (string Head, string? Tail) Split(string text, char at) =>
        text.IndexOf(at) is var i and >= 0 ? (text[..i], text[(i + 1)..]) : (text, null);

    private static string[] Names(string list) => list.Split(',', StringSplitOptions.RemoveEmptyEntries);
}
