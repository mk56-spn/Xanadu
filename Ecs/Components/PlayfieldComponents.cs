using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>Level metadata for the editor and play scene.</summary>
public struct LevelInfo(
    string name,
    string audioPath,
    string artist = "",
    string difficulty = "") : IComponent
{
    /// <summary>
    /// The level's stable identity, carried through editing so a re-saved
    /// level is still the level replays and scores were recorded against.
    /// </summary>
    public string LevelId = "";
    public string Slug = "";
    public string SongId = "";
    public string Name = name;
    public string AudioPath = audioPath;
    public string Artist = artist;
    public string Difficulty = difficulty;
}

/// <summary>Logical playfield dimensions.</summary>
public struct Playfield(int width, int height) : IComponent
{
    public int Width = width;
    public int Height = height;
}

/// <summary>Editor and play transport state.</summary>
public struct Playhead(
    long timeMs,
    int bpm,
    long offsetMs,
    bool playing,
    long durationMs = 0,
    long seekRequestMs = -1,
    int snapDivision = 4,
    int audioRevision = 0) : IComponent
{
    public long TimeMs = timeMs;
    public int Bpm = bpm;
    public long OffsetMs = offsetMs;
    public bool Playing = playing;
    public long DurationMs = durationMs;
    public long SeekRequestMs = seekRequestMs;
    public int SnapDivision = snapDivision;
    public int AudioRevision = audioRevision;
}

/// <summary>Screen-space layout of the logical playfield.</summary>
public struct PlayfieldView(Vector2 screenOrigin, float scale, Vector2 screenSize) : IComponent
{
    public Vector2 ScreenOrigin = screenOrigin;
    public float Scale = scale;
    public Vector2 ScreenSize = screenSize;

    public Vector2 PlayfieldToScreen(Vector2 playfieldPosition) =>
        ScreenOrigin + playfieldPosition * Scale;

    public Vector2 ScreenToPlayfield(Vector2 screenPosition) =>
        (screenPosition - ScreenOrigin) / Scale;
}

/// <summary>A note placed in playfield coordinates.</summary>
public enum NoteKind
{
    Hit,
    Movement,
    Contact,
    CharacterSwitch,
}

public enum DashDirection
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
}

public struct Note(
    long timeMs,
    Vector2 position,
    KeybindAction target = KeybindAction.M,
    NoteKind kind = NoteKind.Hit,
    DashDirection dashDirection = DashDirection.Up,
    int contactRequired = 1,
    CharacterKind character = CharacterKind.Contact) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public KeybindAction Target = target;
    public NoteKind Kind = kind;
    public DashDirection DashDirection = dashDirection;
    public int ContactRequired = contactRequired;
    public CharacterKind Character = character;
}

/// <summary>A mine hazard placed in playfield coordinates.</summary>
public struct Mine(
    long timeMs,
    Vector2 position,
    long fadeInMs = 600,
    long fadeOutMs = 600) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public long FadeInMs = fadeInMs;
    public long FadeOutMs = fadeOutMs;
}

/// <summary>
/// A spike tile in playfield coordinates. It is permanent geometry, never
/// timed: touching it kills the player on contact.
/// </summary>
public struct Spike(Vector2 position, Vector2 size) : IComponent
{
    /// <summary>Top-left corner, so both edges can sit on grid lines.</summary>
    public Vector2 Position = position;

    public Vector2 Size = size;

    public readonly Vector2 Center => Position + Size / 2;

    public readonly Rect2 Bounds => new(Position, Size);
}

/// <summary>
/// Static level geometry in playfield coordinates. Blocks never move in play;
/// they are there so the player collides with them.
/// </summary>
public struct Block(Vector2 position, Vector2 size) : IComponent
{
    /// <summary>Top-left corner, so both edges can sit on grid lines.</summary>
    public Vector2 Position = position;

    public Vector2 Size = size;

    public readonly Vector2 Center => Position + Size / 2;

    public readonly Vector2 End => Position + Size;

    public readonly Rect2 Bounds => new(Position, Size);
}

/// <summary>Whether a shared playfield is drawn for editing or playing.</summary>
public enum PlayfieldKind
{
    Editor,
    Play,
}

public struct PlayfieldMode(PlayfieldKind kind) : IComponent
{
    public PlayfieldKind Kind = kind;
}
