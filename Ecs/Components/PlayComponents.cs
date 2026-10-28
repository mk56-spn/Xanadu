using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

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

    /// <summary>Killed by a spike; the run ends as a failure rather than a finish.</summary>
    public bool Died;

    public float Elapsed;
    public float FadeElapsed;
    public float FadeDuration = 1.5f;
    public long StartTimeMs = startTimeMs;
}

/// <summary>
/// The player character's box. A play scene stores it in screen pixels for the
/// playfield's view, so the character is drawn at the same scale as the level.
/// </summary>
public struct Player(Vector2 size) : IComponent
{
    public Vector2 Size = size;

    /// <summary>
    /// Contact.xaf's art is 209 px tall at rest and the rig is drawn scaled to
    /// the player's height, so this height makes it a little smaller than
    /// authored. It is in playfield units; the playfield's scale turns it into
    /// screen pixels.
    /// </summary>
    public const float DefaultHeight = 200;

    public const float DefaultWidth = 30;

    public static readonly Vector2 DefaultSize = new(DefaultWidth, DefaultHeight);

    /// <summary>The box's size on screen for a playfield drawn at <paramref name="scale"/>.</summary>
    public static Vector2 ScaledSize(float scale) => DefaultSize * scale;
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

    public readonly float Get(Xanadu.Settings.KeybindAction action) =>
        action switch
        {
            Xanadu.Settings.KeybindAction.L1 => L1,
            Xanadu.Settings.KeybindAction.L2 => L2,
            Xanadu.Settings.KeybindAction.L3 => L3,
            Xanadu.Settings.KeybindAction.M => M,
            Xanadu.Settings.KeybindAction.R1 => R1,
            Xanadu.Settings.KeybindAction.R2 => R2,
            Xanadu.Settings.KeybindAction.R3 => R3,
            _ => 0f,
        };

    public void Set(Xanadu.Settings.KeybindAction action, float value)
    {
        switch (action)
        {
            case Xanadu.Settings.KeybindAction.L1: L1 = value; break;
            case Xanadu.Settings.KeybindAction.L2: L2 = value; break;
            case Xanadu.Settings.KeybindAction.L3: L3 = value; break;
            case Xanadu.Settings.KeybindAction.M: M = value; break;
            case Xanadu.Settings.KeybindAction.R1: R1 = value; break;
            case Xanadu.Settings.KeybindAction.R2: R2 = value; break;
            case Xanadu.Settings.KeybindAction.R3: R3 = value; break;
        }
    }
}

/// <summary>A press/release segment in the key-input history trail.</summary>
public struct KeyPressTrail(
    Xanadu.Settings.KeybindAction action,
    long startMs) : IComponent
{
    public Xanadu.Settings.KeybindAction Action = action;
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

/// <summary>
/// A note the moment it was hit: the note keeps its shape and colour, swells,
/// and fades away from where it was, so a hit reads as a pop rather than the
/// note simply vanishing. Screen space, like <see cref="HitTrail"/>.
/// </summary>
public struct NotePop(
    Note note,
    Vector2 position,
    Color color,
    float radius,
    float lifetime = 0.22f) : IComponent
{
    public Note Note = note;
    public Vector2 Position = position;
    public Color Color = color;
    public float Radius = radius;
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

    public readonly int Get(Xanadu.Settings.KeybindAction action) =>
        Counts.Length == 0 ? 0 : Counts[(int)action];

    public void Add(Xanadu.Settings.KeybindAction action, int amount = 1)
    {
        int index = (int)action;
        if ((uint)index < (uint)Counts.Length)
            Counts[index] += amount;
    }

    public bool Consume(Xanadu.Settings.KeybindAction action, int amount)
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
