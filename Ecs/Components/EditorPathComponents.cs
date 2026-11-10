using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>Saved practice start state: time, position, momentum, and facing.</summary>
public struct StartPoint(
    string name,
    long timeMs,
    Vector2 position,
    System.Numerics.Vector2 momentum,
    Vector2 direction,
    uint groups = 0) : IComponent
{
    public string Name = name;
    public long TimeMs = timeMs;
    public Vector2 Position = position;
    public System.Numerics.Vector2 Momentum = momentum;
    public Vector2 Direction = direction;

    /// <summary>Groups this start point belongs to, copied so a play launch can offset the spawn.</summary>
    public uint Groups = groups;
}

/// <summary>
/// Moving the editor's view of the playfield. Input asks for a zoom or a fit
/// here; <c>EditorViewSystem</c> applies it to the <see cref="PlayfieldView"/>,
/// pans while the middle button is held, and reopens the view when the field
/// changes size (a level with a different field was loaded).
/// </summary>
public struct EditorViewNavigation(Vector2 fieldSize) : IComponent
{
    /// <summary>The field size the view was opened for.</summary>
    public Vector2 FieldSize = fieldSize;

    /// <summary>Zoom notches asked for since the last frame, positive in and negative out, about the pointer.</summary>
    public int ZoomSteps;

    /// <summary>Asks for the whole field to be fitted in view.</summary>
    public bool FitRequested;

    /// <summary>Where the pointer was last frame while panning, or null when not panning.</summary>
    public Vector2? PanFrom;
}

/// <summary>Where the player's hitbox was at one moment of an editor preview, in field units.</summary>
/// <param name="Position">The hitbox's centre.</param>
/// <param name="TimeMs">The song time.</param>
/// <param name="Jump">Whether it got here instantly, by a dash or a wrap, rather than by moving.</param>
/// <param name="Hit">Whether a note was hit on this frame.</param>
public readonly record struct PlayerPathSample(Vector2 Position, long TimeMs, bool Jump, bool Hit = false);

/// <summary>Records the player's hitbox during an editor preview, for the editor to show afterwards.</summary>
public struct PlayerPathRecorder() : IComponent
{
    public List<PlayerPathSample> Samples = new();

    /// <summary>The score's hit count at the last look, to spot the frame a note is hit.</summary>
    public int NotesHit;
}

/// <summary>The path the player's hitbox took in the last preview, shown over the editor's field.</summary>
public struct PlayerPath(List<PlayerPathSample> samples, Vector2 hitboxSize) : IComponent
{
    public List<PlayerPathSample> Samples = samples;

    /// <summary>The hitbox's size in field units.</summary>
    public Vector2 HitboxSize = hitboxSize;
}

/// <summary>Marks the header button that shows or hides the preview's path.</summary>
public struct PlayerPathToggle : ITag;

/// <summary>
/// The switch for the preview's path. The header button's <see cref="Toggle"/>
/// is the state; it starts on, so coming back from a preview always shows the
/// path just made.
/// </summary>
public static class PlayerPathSwitch
{
    public const string Label = "PATH";
    public const float ToggleWidth = 64f;

    public static Entity Attach(Entity button)
    {
        button.AddComponent(new Toggle(Label, Label, true));
        button.AddTag<PlayerPathToggle>();
        button.AddComponent(new UiTooltip("Show or hide the player's path from the last preview"));
        return button;
    }

    /// <summary>Whether to draw the path; an editor with no button draws it.</summary>
    public static bool IsVisible(EntityStore store)
    {
        foreach (Entity toggle in store.Query<Toggle>().AllTags(Tags.Get<PlayerPathToggle>()).Entities)
            return toggle.GetComponent<Toggle>().Value;

        return true;
    }
}
