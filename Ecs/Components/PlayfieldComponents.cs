using Friflo.Engine.ECS;
using Godot;
using Xanadu.Theme;
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

    /// <summary>The colour around the playfield, or null when the level uses the theme default.</summary>
    public Color? Outside = null;

    /// <summary>The playfield's frame and border line, or null when the level uses the theme default.</summary>
    public Color? Frame = null;
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
/// The play camera, for a playfield bigger than the screen. Gameplay works in
/// layout pixels (the <see cref="PlayfieldView"/> space the player, notes and
/// physics share); the camera only decides which part of that is shown in
/// <see cref="Viewport"/>, by scrolling the playfield layer by <see cref="Shift"/>.
/// A field smaller than the viewport sits centred and never scrolls.
/// </summary>
public struct PlayCamera(Rect2 viewport) : IComponent
{
    /// <summary>The screen area the playfield is shown in.</summary>
    public Rect2 Viewport = viewport;

    /// <summary>How far the view has scrolled from the layout, following the player.</summary>
    public Vector2 Offset;

    /// <summary>False until the camera first jumps onto the player.</summary>
    public bool Placed;

    /// <summary>A hit's kick, 1 at its peak, easing to 0.</summary>
    public float Kick;

    /// <summary>Where the kick has knocked the view this frame.</summary>
    public Vector2 KickOffset;

    /// <summary>Everything the playfield layer is moved by: subtract it from a layout position to get the screen.</summary>
    public readonly Vector2 Shift => Offset + KickOffset;

    /// <summary>The whole-pixel shift the layer is actually moved by, so the field stays crisp.</summary>
    public readonly Vector2 LayerShift => Shift.Round();

    /// <summary>Added to something smoothly moving on the layer, it undoes the rounding of <see cref="LayerShift"/>.</summary>
    public readonly Vector2 Subpixel => LayerShift - Shift;

    /// <summary>The part of the layout on screen.</summary>
    public readonly Rect2 Visible => new(Viewport.Position + Offset, Viewport.Size);
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
/// Text written into a level: a centred label on the playfield. It has no
/// timing of its own; groups fade and move it like any other item.
/// </summary>
public struct LevelText(Vector2 position, string text, float size = LevelText.DefaultSize) : IComponent
{
    public const float DefaultSize = 64f;
    public const float MinSize = 16f;
    public const float MaxSize = 512f;

    /// <summary>The text placed with the text tool, until it is retyped.</summary>
    public const string Placeholder = "TEXT";

    /// <summary>The centre of the text, in playfield units.</summary>
    public Vector2 Position = position;

    public string Text = text;

    /// <summary>The height of a line, in playfield units.</summary>
    public float Size = size;

    /// <summary>
    /// The box the text roughly fills, in playfield units: wide enough for its
    /// characters at an average glyph width, so it can be picked and outlined.
    /// </summary>
    public readonly Rect2 Bounds
    {
        get
        {
            int length = string.IsNullOrEmpty(Text) ? 1 : Text.Length;
            var size = new Vector2(Size * 0.66f * length, Size * 1.25f);
            return new Rect2(Position - size / 2, size);
        }
    }
}

/// <summary>
/// A spike tile in playfield coordinates. It is permanent geometry, never
/// timed: touching it kills the player on contact.
/// </summary>
public struct Spike(
    Vector2 position,
    Vector2 size,
    float rotationDegrees = 0f,
    float outlineWidth = Block.DefaultOutlineWidth,
    Color? outlineColor = null) : IComponent
{
    /// <summary>Top-left corner, so both edges can sit on grid lines.</summary>
    public Vector2 Position = position;

    public Vector2 Size = size;

    /// <summary>Where the tip points, in degrees clockwise from up. Any angle: a spike is not a physics body.</summary>
    public float RotationDegrees = rotationDegrees;

    /// <summary>Outline thickness in playfield units, drawn inside the triangle; 0 is no outline.</summary>
    public float OutlineWidth = outlineWidth;

    /// <summary>The outline's own colour, as with a block's. The interior's is the spike's <see cref="SelfModulate"/>.</summary>
    public Color OutlineColor = outlineColor ?? Block.DefaultOutlineColor;

    public readonly Vector2 Center => Position + Size / 2;

    public readonly Rect2 Bounds => new(Position, Size);

    /// <summary>Whether a playfield point is inside the spike's turned box.</summary>
    public readonly bool Contains(Vector2 point) =>
        Bounds.HasPoint(Center + (point - Center).Rotated(-Mathf.DegToRad(RotationDegrees)));
}

/// <summary>
/// The one side of a block the player collides with; every other side lets it
/// through. Up is the usual platform: land on it, jump up through it. The
/// directions run clockwise from Up, the same order as spike turns.
/// </summary>
public enum BlockOneWay
{
    Off,
    Up,
    Right,
    Down,
    Left,
}

/// <summary>
/// Static level geometry in playfield coordinates. Blocks never move in play;
/// they are there so the player collides with them.
/// </summary>
public struct Block(
    Vector2 position,
    Vector2 size,
    int shapeId = 0,
    int shapePiece = 0,
    BlockOneWay oneWay = BlockOneWay.Off,
    float outlineWidth = Block.DefaultOutlineWidth,
    Color? outlineColor = null,
    uint fillGroups = Block.AllGroups,
    uint outlineGroups = Block.AllGroups) : IComponent
{
    public const float DefaultOutlineWidth = 3f;
    public const float MaxOutlineWidth = 32f;

    /// <summary>A colour link mask that follows every group the block is in.</summary>
    public const uint AllGroups = uint.MaxValue;

    /// <summary>A slightly darker white than the default white body, shared with spikes.</summary>
    public static readonly Color DefaultOutlineColor = new(0.84f, 0.84f, 0.86f);

    /// <summary>Top-left corner, so both edges can sit on grid lines.</summary>
    public Vector2 Position = position;

    public Vector2 Size = size;

    /// <summary>The Form construction shape this block belongs to, or 0 for permanent geometry.</summary>
    public int ShapeId = shapeId;

    /// <summary>The order this block is revealed in its shape.</summary>
    public int ShapePiece = shapePiece;

    /// <summary>The only side that collides, or Off for a block solid all round.</summary>
    public BlockOneWay OneWay = oneWay;

    /// <summary>Outline thickness in playfield units, drawn inside the block; 0 is no outline.</summary>
    public float OutlineWidth = outlineWidth;

    /// <summary>The outline's own colour. The interior's is the block's <see cref="SelfModulate"/>.</summary>
    public Color OutlineColor = outlineColor ?? DefaultOutlineColor;

    /// <summary>The groups whose colour fades reach the interior; only groups the block is in count.</summary>
    public uint FillGroups = fillGroups;

    /// <summary>The groups whose colour fades reach the outline; only groups the block is in count.</summary>
    public uint OutlineGroups = outlineGroups;

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
