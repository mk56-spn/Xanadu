using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Play-session progress and its status text entity.</summary>
public struct PlaySession(
    Entity status,
    Entity countdown = default,
    long lastNoteMs = -1) : IComponent
{
    public Entity Status = status;
    public Entity Countdown = countdown;
    public long LastNoteMs = lastNoteMs;
    public bool Started;
    public bool CountdownDone;
    public float CountdownElapsed;
    public bool Ending;
    public bool Finished;
    public float Elapsed;
    public float FadeElapsed;
    public float FadeDuration = 1.5f;
}

/// <summary>The player character's pixel dimensions in play mode.</summary>
public struct Player(Vector2 size) : IComponent
{
    public Vector2 Size = size;
    public static readonly Vector2 DefaultSize = new(30, 100);
}

/// <summary>HUD rows for per-judgement counters.</summary>
public struct JudgementCounter(
    Entity flawlessPlus,
    Entity flawless,
    Entity clean,
    Entity fair,
    Entity deficient,
    Entity terrible,
    Entity miss) : IComponent
{
    public Entity FlawlessPlus = flawlessPlus;
    public Entity Flawless = flawless;
    public Entity Clean = clean;
    public Entity Fair = fair;
    public Entity Deficient = deficient;
    public Entity Terrible = terrible;
    public Entity Miss = miss;
}

/// <summary>A non-field movement prompt that falls onto the player.</summary>
public struct MovementEvent(long timeMs) : IComponent
{
    public long TimeMs = timeMs;
}
