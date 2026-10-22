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

/// <summary>Labels for the play HUD key trail, using the current bindings.</summary>
public struct KeyTrailLabels(string[] labels) : IComponent
{
    public string[] Labels = labels;
}

/// <summary>Transient key-press intensities for the play HUD key trail.</summary>
public struct KeyTrail : IComponent
{
    public float L1;
    public float L2;
    public float L3;
    public float M;
    public float R1;
    public float R2;
    public float R3;

    public readonly float Get(Writer.Settings.KeybindAction action) =>
        action switch
        {
            Writer.Settings.KeybindAction.L1 => L1,
            Writer.Settings.KeybindAction.L2 => L2,
            Writer.Settings.KeybindAction.L3 => L3,
            Writer.Settings.KeybindAction.M => M,
            Writer.Settings.KeybindAction.R1 => R1,
            Writer.Settings.KeybindAction.R2 => R2,
            Writer.Settings.KeybindAction.R3 => R3,
            _ => 0f,
        };

    public void Set(Writer.Settings.KeybindAction action, float value)
    {
        switch (action)
        {
            case Writer.Settings.KeybindAction.L1: L1 = value; break;
            case Writer.Settings.KeybindAction.L2: L2 = value; break;
            case Writer.Settings.KeybindAction.L3: L3 = value; break;
            case Writer.Settings.KeybindAction.M: M = value; break;
            case Writer.Settings.KeybindAction.R1: R1 = value; break;
            case Writer.Settings.KeybindAction.R2: R2 = value; break;
            case Writer.Settings.KeybindAction.R3: R3 = value; break;
        }
    }
}

/// <summary>A press/release segment in the key-input history trail.</summary>
public struct KeyPressTrail(
    Writer.Settings.KeybindAction action,
    long startMs) : IComponent
{
    public Writer.Settings.KeybindAction Action = action;
    public long StartMs = startMs;
    public long EndMs = -1;
}

/// <summary>A short-lived key trail left at a successful hit position.</summary>
public struct HitTrail(
    Vector2 position,
    Color color,
    string label,
    float lifetime = 0.55f) : IComponent
{
    public Vector2 Position = position;
    public Color Color = color;
    public string Label = label;
    public float Age;
    public float Lifetime = lifetime;
}
