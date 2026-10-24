using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>Play-session progress and its status text entity.</summary>
public struct PlaySession(
    Entity status,
    Entity countdown = default,
    long lastNoteMs = -1,
    long startTimeMs = 0) : IComponent
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
    public long StartTimeMs = startTimeMs;
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
    public KeyTrail() { }

    public Vector2 Offset;
    public bool Visible = true;

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

/// <summary>A short-lived line from a dash start to its landing point.</summary>
public struct DashTrail(
    Vector2 from,
    Vector2 to,
    float lifetime = 0.35f) : IComponent
{
    public Vector2 From = from;
    public Vector2 To = to;
    public float Age;
    public float Lifetime = lifetime;
}

/// <summary>Playable character families. Modules add behavior to the player.</summary>
public enum CharacterKind
{
    Contact,
    Armed,
    SplitLine,
    Mage,
}

/// <summary>Identifies which character rules are active for the player.</summary>
public struct PlayerCharacter(CharacterKind kind) : IComponent
{
    public CharacterKind Kind = kind;
}

/// <summary>Marks a character that performs a dash on movement notes.</summary>
public struct DashMovement : ITag;

/// <summary>Marks a character that owns contact counters.</summary>
public struct ContactCharacter : ITag;

/// <summary>Accumulated hits per input for the Contact character.</summary>
public struct ContactCounters : IComponent
{
    public int[] Counts;

    public ContactCounters()
    {
        Counts = new int[7];
    }

    public readonly int Get(Writer.Settings.KeybindAction action) =>
        Counts.Length == 0 ? 0 : Counts[(int)action];

    public void Add(Writer.Settings.KeybindAction action, int amount = 1)
    {
        int index = (int)action;
        if ((uint)index < (uint)Counts.Length)
            Counts[index] += amount;
    }

    public bool Consume(Writer.Settings.KeybindAction action, int amount)
    {
        int index = (int)action;
        if ((uint)index >= (uint)Counts.Length ||
            Counts[index] < amount)
        {
            return false;
        }

        Counts[index] -= amount;
        return true;
    }
}

/// <summary>HUD labels for the Contact counter row.</summary>
public struct ContactCounterHud(Entity[] labels) : IComponent
{
    public Entity[] Labels = labels;
}
