using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Ecs.Components;

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

    /// <summary>When the player drops in (see <c>PlayerDropIn</c>); hidden and without a body before it.</summary>
    public long DropStartMs = startTimeMs;

    /// <summary>The downward speed, in metres per second, the player drops in with.</summary>
    public float DropSpeed;

    /// <summary>Where skipping the song's entrance lands, or -1 when nothing follows.</summary>
    public long SkipToMs = skipToMs;

    public Entity SkipButton = skipButton;
    public bool Skipped;

    /// <summary>The skip key went down; <c>PlaySkipSystem</c> takes it on its next update.</summary>
    public bool SkipRequested;
}

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

/// <summary>
/// The player's beam: a short-lived line dropped from the player straight
/// down (or up) into the judgement line on an action key press. Screen space.
/// </summary>
public struct HitTrail(
    Vector2 from,
    Vector2 position,
    Color color,
    float lifetime = 0.22f) : IComponent, IAgingTrail
{
    /// <summary>Where the beam leaves: the player.</summary>
    public Vector2 From = from;

    /// <summary>Where it strikes the judgement line.</summary>
    public Vector2 Position = position;
    public Color Color = color;
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
    float lifetime = 0.22f,
    int chartIndex = -1) : IComponent
{
    public Note Note = note;

    /// <summary>The note's place in the run's chart (<see cref="ChartIndex"/>), for effects drawn from it; -1 for none.</summary>
    public int ChartIndex = chartIndex;
    public Vector2 Position = position;
    public Color Color = color;
    public float Radius = radius;
    public float Age;
    public float Lifetime = lifetime;
}

/// <summary>
/// A note's place in the run's chart (<c>ShaderChart</c>, <c>notes[i]</c> in
/// shaders): set on each note when a level is played, so judging it can write
/// its result there.
/// </summary>
public struct ChartIndex(int index) : IComponent
{
    public int Index = index;

    /// <summary>The note's place in the chart, or -1 for a note that isn't in it.</summary>
    public static int Of(Entity note) =>
        !note.IsNull && note.TryGetComponent(out ChartIndex chart) ? chart.Index : -1;
}

/// <summary>Marks a play note that has already passed its timing window.</summary>
public struct NoteMissed : ITag { }

/// <summary>Marks a playfield entity whose audio is paused by the pause screen.</summary>
public struct PlayPaused : ITag { }
