using Friflo.Engine.ECS;
using Xanadu.Levels;
using Godot;
using Xanadu.Theme;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>Level metadata for the editor and play scene.</summary>
public struct LevelInfo(
    string name,
    LoadedSong? song = null,
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

    /// <summary>
    /// The level's song, opened from the library; null until one is chosen, or
    /// while <see cref="SongId"/> names one the library cannot play.
    /// </summary>
    public LoadedSong? Song = song;

    /// <summary>The audio file the level plays, or empty without a song.</summary>
    public readonly string AudioPath => Song?.AudioFile ?? "";
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

/// <summary>
/// Editor and play transport state. <see cref="Tempo"/> is the song's timing
/// (read-only here: songs are timed globally, on the song timing screen); the
/// constructor's <c>bpm</c> and <c>offsetMs</c> make a one-tempo map.
/// </summary>
public struct Playhead(
    long timeMs,
    double bpm,
    long offsetMs,
    bool playing,
    long durationMs = 0,
    long seekRequestMs = -1,
    int snapDivision = 4,
    int audioRevision = 0) : IComponent
{
    public long TimeMs = timeMs;
    private Xanadu.Levels.TempoMap? _tempo = Xanadu.Levels.TempoMap.Single(bpm, offsetMs);

    /// <summary>The song's tempo map: beat 0, BPM changes and bar lengths.</summary>
    public Xanadu.Levels.TempoMap Tempo
    {
        readonly get => _tempo ?? Xanadu.Levels.TempoMap.Default;
        set => _tempo = value;
    }

    public bool Playing = playing;
    public long DurationMs = durationMs;
    public long SeekRequestMs = seekRequestMs;
    /// <summary>The beat snap divisor: 1 snaps to whole beats, 2 to halves, 4 to quarters (osu! style).</summary>
    public int SnapDivision = snapDivision;
    public int AudioRevision = audioRevision;

    /// <summary>
    /// The editor's music snap is off: placed notes keep the exact time they
    /// were put at instead of landing on the beat grid. Off by default, so a
    /// transport made anywhere snaps.
    /// </summary>
    public bool SnapOff;
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
/// What a note does. Action notes are hit with F, Space or J: a coloured
/// <see cref="Hit"/> note with its own key, a <see cref="Neutral"/> note with any.
/// Directional notes (<see cref="Dash"/>, <see cref="Momentum"/>) are hit with a
/// direction key and carry only their strength; the player picks the way.
/// A <see cref="Contact"/> barrier is a check, not a press.
/// </summary>
public enum NoteKind
{
    Hit,
    Neutral,
    Dash,
    Momentum,
    Contact,
}

/// <summary>The eight ways a directional note can send the player, clockwise from up.</summary>
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

/// <summary>How far a dash note carries the player: one, two or three dash lengths.</summary>
public enum DashStrength
{
    Single,
    Double,
    Triple,
}

/// <summary>
/// What a Contact barrier asks of each bar, one bar per action key. A barrier
/// asks at most <see cref="MaxCost"/> of a bar and of at most
/// <see cref="MaxBars"/> bars.
/// </summary>
public struct BarCosts(int first = 0, int second = 0, int third = 0)
{
    public const int MaxCost = 4;
    public const int MaxBars = 2;

    public int First = first;
    public int Second = second;
    public int Third = third;

    /// <summary>The cost for bar 0, 1 or 2.</summary>
    public int this[int bar]
    {
        readonly get => bar switch { 0 => First, 1 => Second, _ => Third };
        set
        {
            if (bar == 0) First = value;
            else if (bar == 1) Second = value;
            else Third = value;
        }
    }

    /// <summary>How many bars this barrier asks anything of.</summary>
    public readonly int UsedBars => (First > 0 ? 1 : 0) + (Second > 0 ? 1 : 0) + (Third > 0 ? 1 : 0);

    /// <summary>
    /// Sets one bar's cost inside the limits: no more than <see cref="MaxCost"/>,
    /// and a new bar only while fewer than <see cref="MaxBars"/> are in use.
    /// </summary>
    public readonly BarCosts With(int bar, int cost)
    {
        BarCosts next = this;
        cost = System.Math.Clamp(cost, 0, MaxCost);
        if (cost > 0 && this[bar] == 0 && UsedBars >= MaxBars)
            return this;
        next[bar] = cost;
        return next;
    }
}

public struct Note(
    long timeMs,
    Vector2 position,
    KeybindAction target = KeybindAction.Action1,
    NoteKind kind = NoteKind.Hit,
    DashStrength dashStrength = DashStrength.Single,
    float momentum = Note.DefaultMomentum,
    bool diagonal = false,
    BarCosts costs = default) : IComponent
{
    /// <summary>
    /// A momentum note's speed, in playfield units per second. The tuning
    /// default is a single dash-length shove; charts can author any value.
    /// </summary>
    public const float DefaultMomentum = 600f;

    public long TimeMs = timeMs;
    public Vector2 Position = position;

    /// <summary>The action key a coloured <see cref="NoteKind.Hit"/> note asks for; ignored by other kinds.</summary>
    public KeybindAction Target = target;

    public NoteKind Kind = kind;

    /// <summary>A dash note's distance; ignored by other kinds.</summary>
    public DashStrength DashStrength = dashStrength;

    /// <summary>A momentum note's speed in playfield units per second; ignored by other kinds.</summary>
    public float Momentum = momentum;

    /// <summary>
    /// A directional note drawn as a rhombus: it turns the pressed direction 45°
    /// clockwise, so up becomes up-right. Ignored by other kinds.
    /// </summary>
    public bool Diagonal = diagonal;

    /// <summary>What a Contact barrier asks of each bar; ignored by other kinds.</summary>
    public BarCosts Costs = costs;
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
/// Text written into a level: a centred label on the playfield, turned about
/// its centre. It has no timing of its own; groups fade and move it like any
/// other item.
/// </summary>
public struct LevelText(Vector2 position, string text, float size = LevelText.DefaultSize, float rotationDegrees = 0f) : IComponent
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

    /// <summary>How far the text is turned about its centre, clockwise, in degrees.</summary>
    public float RotationDegrees = rotationDegrees;

    /// <summary>
    /// The box the text roughly fills before it is turned, in playfield units: wide enough for its
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
/// A friction bar: an upright line in playfield coordinates. The ground has
/// no friction, so a slide carries on at its speed; crossing a bar turns the
/// ground's friction on, and the player slows to a stop past it. The next
/// momentum push turns friction off again.
/// </summary>
public struct FrictionBar(Vector2 position, float height) : IComponent
{
    /// <summary>How wide the bar is drawn and picked, in playfield units.</summary>
    public const float Thickness = 12f;

    /// <summary>Top-left corner of its box.</summary>
    public Vector2 Position = position;

    public float Height = height;

    /// <summary>The line the player crosses.</summary>
    public readonly float X => Position.X + Thickness / 2;

    public readonly Vector2 Center => Position + new Vector2(Thickness, Height) / 2;

    public readonly Rect2 Bounds => new(Position, new Vector2(Thickness, Height));
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

/// <summary>
/// On a play playfield: notes approach the DIVA+ way from the notes lab — a
/// target with a clock hand, the note flying in onto it, and a spark running
/// the guide line. Always on in play.
/// </summary>
public struct DivaNotes : ITag;

/// <summary>
/// On a play playfield: the hold-to-move ground mode. On the ground the player
/// keeps moving only while holding the direction key of the last directional
/// note it hit (<see cref="MoveKey"/>); let go and it stops. Without it the
/// player skids, and only friction fields stop it. F8 switches.
/// </summary>
public struct GroundHold : ITag;

/// <summary>The play HUD label that names the ground mode.</summary>
public struct GroundModeHud(Entity label) : IComponent
{
    public Entity Label = label;
}

/// <summary>
/// The DIVA+ guide spark's leg: the last hit moment it leaves from and the
/// moment it runs to. Hit notes are deleted, so the moment just hit is
/// remembered here. Positions are playfield units, where the notes were shown.
/// </summary>
public struct DivaSpark : IComponent
{
    public bool HasFrom;
    public long FromMs;
    public Vector2 From;
    public bool FromChord;

    public bool HasNext;
    public long NextMs;
    public Vector2 Next;
    public bool NextChord;
}
