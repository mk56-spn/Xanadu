using System;
using Friflo.Engine.ECS;
using Xanadu.Settings;

namespace Xanadu.Ecs;

/// <summary>A trail whose life runs out and that is then removed, age by age.</summary>
public interface IAgingTrail
{
    float Age { get; set; }
    float Lifetime { get; }
}

/// <summary>Play-session progress and its status text entity.</summary>
public struct PlaySession(
    Entity status,
    Entity countdown = default,
    long lastNoteMs = -1,
    long startTimeMs = 0,
    long skipToMs = -1,
    Entity skipButton = default) : IComponent
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

    /// <summary>Where skipping the song's entrance lands, or -1 when nothing follows.</summary>
    public long SkipToMs = skipToMs;

    public Entity SkipButton = skipButton;
    public bool Skipped;

    /// <summary>The skip key went down; <c>PlaySkipSystem</c> takes it on its next update.</summary>
    public bool SkipRequested;
}

/// <summary>
/// The player hit a spike and burst apart: neither its art nor its box is
/// drawn, and its body is out of the simulation until the level restarts.
/// </summary>
public struct PlayerShattered : ITag;

/// <summary>
/// The player character's box. A play scene stores it in screen pixels for the
/// playfield's view, so the character is drawn at the same scale as the level.
/// </summary>
public struct Player(Vector2 size) : IComponent
{
    public Vector2 Size = size;

    /// <summary>
    /// The box's height in playfield units; the playfield's scale turns it into
    /// screen pixels. It is shorter than the art so its top meets the top of the
    /// character's head rather than the top of the rig's bounds.
    /// </summary>
    public const float DefaultHeight = 180;

    /// <summary>
    /// How tall the character art is drawn, in playfield units. Contact.xaf's
    /// art is 209 px tall at rest, so this makes it a little smaller than
    /// authored. The art's feet sit on the bottom of the box.
    /// </summary>
    public const float DefaultArtHeight = 200;

    public const float DefaultWidth = 60;

    public static readonly Vector2 DefaultSize = new(DefaultWidth, DefaultHeight);

    /// <summary>The box's size on screen for a playfield drawn at <paramref name="scale"/>.</summary>
    public static Vector2 ScaledSize(float scale) => DefaultSize * scale;

    /// <summary>How tall the art is drawn for a box <paramref name="boxHeight"/> tall, in the same units.</summary>
    public static float ArtHeight(float boxHeight) => boxHeight * (DefaultArtHeight / DefaultHeight);
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

/// <summary>
/// The key trail's HUD entities: one keycap per lane, and per lane a pool of
/// segment rects (an outline and its bright core) laid out in that lane's
/// history column, whose local space puts 0 at its centre.
/// </summary>
public struct KeyTrailHud(
    Entity[] keys,
    Entity[] outlines,
    Entity[] cores,
    int slotsPerLane,
    Vector2 historySize) : IComponent
{
    public Entity[] Keys = keys;
    public Entity[] Outlines = outlines;
    public Entity[] Cores = cores;
    public int SlotsPerLane = slotsPerLane;
    public Vector2 HistorySize = historySize;
}

/// <summary>Transient key-press intensities for the play HUD key trail.</summary>
public struct KeyTrail : IComponent
{
    public KeyTrail() { }

    /// <summary>One intensity per key, in <see cref="Xanadu.Settings.KeybindAction"/> order.</summary>
    private float[]? _values;

    public readonly float Get(Xanadu.Settings.KeybindAction action) =>
        _values is null ? 0f : _values[(int)action];

    public void Set(Xanadu.Settings.KeybindAction action, float value)
    {
        _values ??= new float[Xanadu.Settings.KeybindSettings.Actions.Length];
        _values[(int)action] = value;
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
    float lifetime = 0.55f) : IComponent, IAgingTrail
{
    public Vector2 Position = position;
    public Color Color = color;
    public string Label = label;
    public float Age;
    public float Lifetime = lifetime;

    float IAgingTrail.Age
    {
        readonly get => Age;
        set => Age = value;
    }

    float IAgingTrail.Lifetime => Lifetime;
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

/// <summary>
/// A see-through copy of the character left where it stood when it hit a note,
/// fading out over <see cref="Lifetime"/>. Layout space, like the player, so it
/// stays put while the camera scrolls. The rig draw system poses it from the
/// character on the frame it appears; a player without a rig leaves its box.
/// </summary>
public struct HitGhost(Entity player, Vector2 center, Vector2 size, float lifetime = HitGhost.DefaultLifetime) : IComponent
{
    public const float DefaultLifetime = 0.5f;

    /// <summary>How opaque the copy starts.</summary>
    public const float StartAlpha = 0.55f;

    public Entity Player = player;
    public Vector2 Center = center;
    public Vector2 Size = size;
    public float Age;
    public float Lifetime = lifetime;

    /// <summary>The copy's opacity now: <see cref="StartAlpha"/>, easing out to nothing.</summary>
    public readonly float Alpha
    {
        get
        {
            float left = Lifetime <= 0f ? 0f : Mathf.Clamp(1f - Age / Lifetime, 0f, 1f);
            return StartAlpha * left * left;
        }
    }
}

/// <summary>The rig draw system has posed this <see cref="HitGhost"/> as the character's art, so no box is drawn for it.</summary>
public struct HitGhostRigged : ITag;

/// <summary>A short-lived line from a dash start to its landing point.</summary>
public struct DashTrail(
    Vector2 from,
    Vector2 to,
    float lifetime = 0.35f) : IComponent, IAgingTrail
{
    public Vector2 From = from;
    public Vector2 To = to;
    public float Age;
    public float Lifetime = lifetime;

    float IAgingTrail.Age
    {
        readonly get => Age;
        set => Age = value;
    }

    float IAgingTrail.Lifetime => Lifetime;
}

/// <summary>Playable character families. v2 has Contact only; more come later.</summary>
public enum CharacterKind
{
    Contact,
}

/// <summary>Identifies which character rules are active for the player.</summary>
public struct PlayerCharacter(CharacterKind kind) : IComponent
{
    public CharacterKind Kind = kind;
}

/// <summary>
/// Contact's three bars, one per action key. A neutral note hit with an action
/// key loads that key's bar; barriers spend them. Bars never decay and hold at
/// most <see cref="Cap"/> each.
/// </summary>
public struct ContactCounters : IComponent
{
    public const int Cap = 5;
    public const int Bars = 3;

    public int First;
    public int Second;
    public int Third;

    /// <summary>The bar for 0, 1 or 2.</summary>
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

    /// <summary>The bar an action key loads, or -1 for a direction key.</summary>
    public static int BarOf(Xanadu.Settings.KeybindAction action) => action switch
    {
        Xanadu.Settings.KeybindAction.Action1 => 0,
        Xanadu.Settings.KeybindAction.Action2 => 1,
        Xanadu.Settings.KeybindAction.Action3 => 2,
        _ => -1,
    };

    /// <summary>Loads the action key's bar by one, up to the cap.</summary>
    public void Add(Xanadu.Settings.KeybindAction action)
    {
        int bar = BarOf(action);
        if (bar >= 0)
            this[bar] = Math.Min(Cap, this[bar] + 1);
    }

    /// <summary>
    /// Pays a barrier: when every bar holds its cost, spends them all and
    /// returns true; when any is short, spends nothing.
    /// </summary>
    public bool Consume(BarCosts costs)
    {
        for (int bar = 0; bar < Bars; bar++)
        {
            if (this[bar] < costs[bar])
                return false;
        }

        for (int bar = 0; bar < Bars; bar++)
            this[bar] -= costs[bar];
        return true;
    }
}

/// <summary>HUD labels for Contact's three bars, one per action key.</summary>
public struct ContactCounterHud(Entity first, Entity second, Entity third) : IComponent
{
    public Entity First = first;
    public Entity Second = second;
    public Entity Third = third;

    public readonly Entity this[int bar] => bar switch { 0 => First, 1 => Second, _ => Third };
}

/// <summary>
/// The direction key the player last hit a directional note with. In the
/// hold-to-move ground mode, holding it keeps the player moving on the ground.
/// </summary>
public struct MoveKey(Xanadu.Settings.KeybindAction key) : IComponent
{
    public Xanadu.Settings.KeybindAction Key = key;
}
