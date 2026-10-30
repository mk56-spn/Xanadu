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

    /// <summary>The playfield background, or null when the level uses the theme default.</summary>
    public Color? Background = null;
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

/// <summary>
/// What a note does. The numeric values are serialized, so kinds are appended
/// rather than reordered; <c>Dash</c> keeps the old <c>Movement</c> value so
/// existing maps still load.
/// </summary>
public enum NoteKind
{
    Hit,
    Dash,
    Contact,
    CharacterSwitch,
    Pair,
    Momentum,
    WeaponSwitch,
    Channel,
    Rail,
    ChannelEnd,
    Construction,
    ConstructionStep,
}

/// <summary>The two Splitline channels, and the side a rail layer belongs to.</summary>
public enum ChannelSide
{
    Left,
    Right,
}

/// <summary>
/// Which pair a pair note belongs to. L and R of the same number share it, so
/// the note is side-agnostic; only the number is compared.
/// </summary>
public enum PairIndex
{
    One = 1,
    Two = 2,
    Three = 3,
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

/// <summary>How far a movement note carries the player: one, two or three dash lengths.</summary>
public enum DashStrength
{
    Single,
    Double,
    Triple,
}

public struct Note(
    long timeMs,
    Vector2 position,
    KeybindAction target = KeybindAction.M,
    NoteKind kind = NoteKind.Hit,
    DashDirection dashDirection = DashDirection.Up,
    int contactRequired = 1,
    CharacterKind character = CharacterKind.Contact,
    DashStrength dashStrength = DashStrength.Single,
    PairIndex pairIndex = PairIndex.One,
    float momentum = 600f,
    int contactLeft = 0,
    int contactRight = 0,
    ChannelSide channelSide = ChannelSide.Left,
    long holdMs = 0,
    int shapeId = 0,
    int shapePiece = 0) : IComponent
{
    /// <summary>
    /// A momentum note's push, expressed as the velocity change it imparts in
    /// playfield units per second. The tuning default is a single dash-length
    /// shove; charts can author any value.
    /// </summary>
    public const float DefaultMomentum = 600f;

    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public KeybindAction Target = target;
    public NoteKind Kind = kind;
    public DashDirection DashDirection = dashDirection;
    public int ContactRequired = contactRequired;
    public CharacterKind Character = character;
    public DashStrength DashStrength = dashStrength;

    /// <summary>The pair a pair note belongs to; ignored by every other kind.</summary>
    public PairIndex PairIndex = pairIndex;

    /// <summary>A momentum note's push in playfield units per second; ignored by other kinds.</summary>
    public float Momentum = momentum;

    /// <summary>The left-side charge a contact barrier spends.</summary>
    public int ContactLeft = contactLeft;

    /// <summary>The right-side charge a contact barrier spends.</summary>
    public int ContactRight = contactRight;

    /// <summary>The side a channel start, channel end or rail note belongs to.</summary>
    public ChannelSide ChannelSide = channelSide;

    /// <summary>How long a rail hold lasts; zero for a tap.</summary>
    public long HoldMs = holdMs;

    /// <summary>The Form construction shape a construction/step note belongs to.</summary>
    public int ShapeId = shapeId;

    /// <summary>Which attached block a construction step reveals.</summary>
    public int ShapePiece = shapePiece;
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
public struct Block(
    Vector2 position,
    Vector2 size,
    int shapeId = 0,
    int shapePiece = 0) : IComponent
{
    /// <summary>Top-left corner, so both edges can sit on grid lines.</summary>
    public Vector2 Position = position;

    public Vector2 Size = size;

    /// <summary>The Form construction shape this block belongs to, or 0 for permanent geometry.</summary>
    public int ShapeId = shapeId;

    /// <summary>The order this block is revealed in its shape.</summary>
    public int ShapePiece = shapePiece;

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
