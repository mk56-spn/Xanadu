using Friflo.Engine.ECS;
using Godot;
using Writer.Settings;

namespace Writer.Ecs;

/// <summary>Level metadata for the editor and play scene.</summary>
public struct LevelInfo(
    string name,
    string audioPath,
    string artist = "",
    string difficulty = "") : IComponent
{
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
public struct Note(
    long timeMs,
    Vector2 position,
    long fadeInMs = 600,
    long fadeOutMs = 600,
    KeybindAction target = KeybindAction.M) : IComponent
{
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public long FadeInMs = fadeInMs;
    public long FadeOutMs = fadeOutMs;
    public KeybindAction Target = target;
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
