using Friflo.Engine.ECS;
using Xanadu.Model.Levels;
using Xanadu.Model.Settings;

namespace Xanadu.Model.Ecs.Components;

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
    /// <summary>The audio clock, ms: what judging goes by. It moves in uneven chunks, so nothing is drawn from it while playing.</summary>
    public long TimeMs = timeMs;

    /// <summary>
    /// The smooth song clock, ms: frame time while the song plays, eased toward
    /// <see cref="TimeMs"/> and snapped to it on a jump (<c>PlayfieldPlaybackSystem</c>).
    /// </summary>
    public double SmoothMs = timeMs;

    /// <summary>The time to draw and move things by: the smooth clock while playing, the audio clock while held.</summary>
    public readonly double ShownMs => Playing ? SmoothMs : TimeMs;

    /// <summary>The audio clock at its last read while playing, ms, unrounded.</summary>
    public double ClockMs;

    /// <summary>When that read was, a <see cref="System.Diagnostics.Stopwatch"/> timestamp; 0 while held.</summary>
    public long ClockTicks;

    /// <summary>A key change this long before the clock's read still maps; older ones (across a pause, say) are held there, ms.</summary>
    public const double LookBackMs = 100;

    /// <summary>The song time at a <see cref="System.Diagnostics.Stopwatch"/> timestamp, from the last audio read.</summary>
    public readonly double SongMsAt(long ticks)
    {
        if (ClockTicks == 0)
            return TimeMs;
        double ms = ClockMs + (ticks - ClockTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        return System.Math.Clamp(ms, ClockMs - LookBackMs, ClockMs);
    }

    /// <summary>The <see cref="System.Diagnostics.Stopwatch"/> timestamp at a song time, from the last audio read; 0 while held.</summary>
    public readonly long TicksAt(double songMs) =>
        ClockTicks == 0 ? 0 : ClockTicks + (long)((songMs - ClockMs) * System.Diagnostics.Stopwatch.Frequency / 1000d);

    /// <summary>
    /// The song time a key change is judged at, ms: when it happened if the
    /// keyboard knows (<paramref name="changedAt"/>), else this frame's audio clock.
    /// </summary>
    public readonly long KeyMs(long? changedAt) =>
        changedAt is { } ticks && ClockTicks != 0 ? (long)System.Math.Round(SongMsAt(ticks)) : TimeMs;

    private TempoMap? _tempo = TempoMap.Single(bpm, offsetMs);

    /// <summary>The song's tempo map: beat 0, BPM changes and bar lengths.</summary>
    public TempoMap Tempo
    {
        readonly get => _tempo ?? TempoMap.Default;
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

    /// <summary>The run's first note, which the view holds on while the player drops in.</summary>
    public Entity DropNote;

    /// <summary>True once the drop-in is over and the view follows the player.</summary>
    public bool DroppedIn;

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

/// <summary>What a note asks of the player.</summary>
public enum NoteKind : byte
{
    /// <summary>Hit with its action key.</summary>
    Tap,

    /// <summary>
    /// A line across the whole chord: any direction key hits it, and the
    /// player dashes the way they pressed, until a wall.
    /// </summary>
    Dash,

    /// <summary>
    /// Where and when the player comes into the level: a rift at its own
    /// position that releases the player on its time exactly, however it is
    /// hit. Any key hits it, judged like any note, because the music is playing.
    /// </summary>
    Entrance,

    /// <summary>
    /// A hold you ride: an orb that crosses at one fixed sideways speed while its
    /// height follows its path (<see cref="Note.Path"/>), from the note's time to
    /// <see cref="Note.EndMs"/>. Any action key grabs it on its head, holding rides
    /// it, and letting go leaves with its motion (<c>Orbs</c> has the shape).
    /// </summary>
    Orb,
}

/// <summary>
/// How an entrance launches the player: its onset. Songs don't all want to
/// start on a fall; some start on a dash, or riding an orb.
/// </summary>
public enum EntranceExit : byte
{
    /// <summary>Out running right at cruise speed, gravity taking over.</summary>
    Fall,

    /// <summary>Out on a dash the way <see cref="Note.Target"/> points (a direction key), until a wall.</summary>
    Dash,

    /// <summary>
    /// Out riding an orb that starts at the rift: the entrance's own
    /// <see cref="Note.EndMs"/> and <see cref="Note.Path"/>, ridden to its tail
    /// with nothing more to judge, leaving with its motion.
    /// </summary>
    Orb,
}

/// <summary>
/// One key of an orb's height path: <see cref="OffsetMs"/> after the head, the
/// orb is <see cref="Rise"/> playfield units from its start height (y down).
/// </summary>
public readonly record struct OrbKey(long OffsetMs, float Rise);

/// <summary>A note: hit on its time, with its action key or, for a dash, any direction key.</summary>
public struct Note(
    long timeMs,
    Vector2 position,
    KeybindAction target = KeybindAction.Action1,
    NoteKind kind = NoteKind.Tap) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;

    /// <summary>The action key the note asks for; unused by a dash.</summary>
    public KeybindAction Target = target;

    public NoteKind Kind = kind;

    /// <summary>A hold's end (an orb's tail), ms; the note's own time for anything else.</summary>
    public long EndMs = timeMs;

    /// <summary>
    /// An orb's height path after its start, by time; null or empty is flat.
    /// Never changed in place: an edit swaps in a new array, so copies (undo,
    /// clipboard, a ride) stay as they were.
    /// </summary>
    public OrbKey[]? Path;

    /// <summary>An entrance's onset: how it launches the player.</summary>
    public EntranceExit Exit;

    public readonly bool IsDash => Kind == NoteKind.Dash;

    public readonly bool IsEntrance => Kind == NoteKind.Entrance;

    public readonly bool IsOrb => Kind == NoteKind.Orb;
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
