using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Local rectangle centered on the entity canvas item origin.</summary>
public struct Rectangle(Rect2 bounds) : IComponent
{
    public Rect2 Bounds = bounds;
}

/// <summary>Marks an entity as renderable and defines its draw order.</summary>
public struct Drawable(int zIndex) : IComponent
{
    public int ZIndex = zIndex;
}

/// <summary>Fill color for a drawable rectangle.</summary>
public struct FillColor(Color value) : IComponent
{
    public Color Value = value;
}

/// <summary>Canvas item position in screen space.</summary>
public struct Position(Vector2 value) : IComponent
{
    public Vector2 Value = value;
}

/// <summary>Text content for a drawable entity.</summary>
public struct Text(string value) : IComponent
{
    public string Value = value;
}

public enum StackDirection
{
    Horizontal,
    Vertical,
}

/// <summary>Container entity that stacks its child entities.</summary>
public struct UiContainer(
    StackDirection direction,
    float spacing = 0,
    bool clip = false) : IComponent
{
    public StackDirection Direction = direction;
    public float Spacing = spacing;
    public bool Clip = clip;
}

/// <summary>Playback state for a miniaudio-backed test track.</summary>
public struct TrackProgress(
    string audioPath,
    long durationMs,
    float bpm,
    long offsetMs = 0,
    int speedPercent = 100) : IComponent
{
    public string AudioPath = audioPath;
    public long DurationMs = durationMs;
    public float Bpm = bpm;
    public long OffsetMs = offsetMs;
    public int SpeedPercent = speedPercent;
    public long NowMs;
    public long AudioMs;
    public bool Started;
    public bool Finished;
}

/// <summary>Box2D body attached to an ECS entity.</summary>
public struct PhysicsBody(Box2DSharp.Dynamics.Body body) : IComponent
{
    public Box2DSharp.Dynamics.Body Body = body;
}

/// <summary>Marks an entity as a character with a mass in kilograms.</summary>
public struct Character(float mass) : IComponent
{
    public float Mass = mass;
}

/// <summary>Linear momentum in kg*m/s.</summary>
public struct Momentum(System.Numerics.Vector2 value) : IComponent
{
    public System.Numerics.Vector2 Value = value;
}

/// <summary>Level metadata for the editor.</summary>
public struct LevelInfo(string name, string audioPath) : IComponent
{
    public string Name = name;
    public string AudioPath = audioPath;
}

/// <summary>Logical editor playfield dimensions.</summary>
public struct Playfield(int width, int height) : IComponent
{
    public int Width = width;
    public int Height = height;
}

/// <summary>Editor transport state.</summary>
public struct EditorPlayhead(
    long timeMs,
    int bpm,
    long offsetMs,
    bool playing,
    long durationMs = 0,
    long seekRequestMs = -1) : IComponent
{
    public long TimeMs = timeMs;
    public int Bpm = bpm;
    public long OffsetMs = offsetMs;
    public bool Playing = playing;
    public long DurationMs = durationMs;
    public long SeekRequestMs = seekRequestMs;
}

/// <summary>Screen-space layout of the logical playfield.</summary>
public struct EditorView(Vector2 screenOrigin, float scale, Vector2 screenSize) : IComponent
{
    public Vector2 ScreenOrigin = screenOrigin;
    public float Scale = scale;
    public Vector2 ScreenSize = screenSize;

    public Vector2 PlayfieldToScreen(Vector2 playfieldPosition) =>
        ScreenOrigin + playfieldPosition * Scale;

    public Vector2 ScreenToPlayfield(Vector2 screenPosition) =>
        (screenPosition - ScreenOrigin) / Scale;
}

/// <summary>An editor note placed in playfield coordinates.</summary>
public struct Note(
    long timeMs,
    Vector2 position,
    long fadeInMs = 300,
    long fadeOutMs = 200) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public long FadeInMs = fadeInMs;
    public long FadeOutMs = fadeOutMs;
}

/// <summary>Layer entity component. ZIndex determines draw order between layers.</summary>
public struct Layer(int zIndex) : IIndexedComponent<int>
{
    public int ZIndex = zIndex;

    public int GetIndexedValue() => ZIndex;
}

/// <summary>RenderingServer canvas item owned by an entity.</summary>
public struct VisualRid : IComponent
{
    public Rid Value;

    public VisualRid() => Value = RenderingServer.CanvasItemCreate();

    public VisualRid(Rid value) => Value = value;
}
