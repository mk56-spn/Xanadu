using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Levels;
using Xanadu.Model.Settings;
using Xanadu.Ui.Ecs.Ui;

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
    public long TimeMs = timeMs;
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

/// <summary>The three speed buttons: which fixed speed a route runs at (<see cref="NoteRoute.SpeedOf"/>).</summary>
public enum DashStrength
{
    Single,
    Double,
    Triple,
}

/// <summary>
/// A hold note's tail: when it ends, ms after the head, and where the tail sits
/// from the head. The bar between them is only for looks; the hold only checks the key stays down.
/// </summary>
public readonly record struct NoteHold(long TailMs, Vector2 Tail)
{
    /// <summary>The shortest hold, ms.</summary>
    public const long MinTailMs = 1;
}

/// <summary>
/// A note: an action note, hit with its key on its time. A route note also
/// carries a <see cref="NoteRoute"/>: hitting its head with a direction key sends the body down it.
/// A dash note (<see cref="DashLength"/> above 0) is hit with any direction key and teleports the
/// body that far that way from the note. A hold note (<see cref="Hold"/>) is held with its key to its tail.
/// </summary>
public struct Note(
    long timeMs,
    Vector2 position,
    KeybindAction target = KeybindAction.Action1,
    NoteRoute? route = null,
    float dashLength = 0f,
    NoteHold? hold = null) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;

    /// <summary>The action key the note asks for (a hold's key too); a dash takes any direction key instead.</summary>
    public KeybindAction Target = target;

    /// <summary>
    /// A route note's turns: hitting the head (this note) with a direction key
    /// sends the body down the route that way from <see cref="TimeMs"/>; a miss
    /// leaves the body alone. Null on a plain note.
    /// </summary>
    public NoteRoute? Route = route;

    /// <summary>A dash note's teleport distance, playfield units; 0 on every other note.</summary>
    public float DashLength = dashLength;

    /// <summary>A hold note's tail; null on every other note.</summary>
    public NoteHold? Hold = hold;

    public readonly bool IsRoute => Route is not null;

    public readonly bool IsDash => DashLength > 0f;

    public readonly bool IsHold => Hold is not null;

    /// <summary>When the note is over: a route's end, a hold's tail, else the note's own time.</summary>
    public readonly long EndMs =>
        Route is { } route ? TimeMs + route.DurationMs :
        Hold is { } hold ? TimeMs + hold.TailMs :
        TimeMs;

    /// <summary>Where a hold's tail sits on the field; the note itself for any other note.</summary>
    public readonly Vector2 TailPosition => Hold is { } hold ? Position + hold.Tail : Position;
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
