using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

/// <summary>
/// The view controls in the bar at the top centre of the editor: the grid
/// button and its drop-down (show the grid, snap to it), and the music button
/// and its drop-down (music snap on or off, and the beat division).
/// </summary>
public struct EditorViewBar(
    Entity gridButton,
    Entity gridMenu,
    Entity musicButton,
    Entity musicMenu,
    Entity[] divisions) : IComponent
{
    /// <summary>
    /// The beat snap divisors the music menu offers, as in osu!: the straight
    /// ones (a row of their own), then the triplets.
    /// </summary>
    public static readonly (int Division, string Label)[] Divisions =
    {
        (1, "1/1"),
        (2, "1/2"),
        (4, "1/4"),
        (8, "1/8"),
        (3, "1/3"),
        (6, "1/6"),
        (12, "1/12"),
    };

    /// <summary>How many of <see cref="Divisions"/> are straight; the rest are triplets.</summary>
    public const int StraightDivisions = 4;

    public Entity GridButton = gridButton;
    public Entity GridMenu = gridMenu;
    public Entity MusicButton = musicButton;
    public Entity MusicMenu = musicMenu;
    public Entity[] DivisionButtons = divisions;

    /// <summary>Which drop-down is open: none, the grid's or the music's.</summary>
    public EditorViewMenu Open;
}

public enum EditorViewMenu : byte
{
    None,
    Grid,
    Music,
}

/// <summary>
/// Runs the bar's drop-downs: a button opens its menu under it (or closes it),
/// a press anywhere else closes it, and the music menu's choices reach the
/// playhead: music snap through <see cref="Playhead.SnapOff"/>, the division
/// through the usual snap command so undo and the other snap controls agree.
/// The toggles inside the menus are ordinary toggles; the toggle system flips them.
/// </summary>
public sealed class EditorViewBarSystem : QuerySystem<EditorViewBar, Playhead, PlayfieldView>
{
    private const float MenuGap = 4f;

    private readonly IPointerInputSource _pointer;
    private readonly EditorCommandBus _commands;
    private readonly ButtonPress _presses = new();
    private bool _wasPressed;

    public EditorViewBarSystem(IPointerInputSource pointer, EditorCommandBus commands)
    {
        _pointer = pointer;
        _commands = commands;

        // The menus are hidden most of the time, and still have to be found to be shown.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool justPressed = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;
        CommandBuffer buffer = CommandBuffer;
        EntityStore store = Query.Store;

        Query.ForEachEntity((ref EditorViewBar bar, ref Playhead playhead, ref PlayfieldView view, Entity _) =>
        {
            bool onButton = false;
            if (_presses.Started(bar.GridButton))
            {
                bar.Open = bar.Open == EditorViewMenu.Grid ? EditorViewMenu.None : EditorViewMenu.Grid;
                onButton = true;
            }

            if (_presses.Started(bar.MusicButton))
            {
                bar.Open = bar.Open == EditorViewMenu.Music ? EditorViewMenu.None : EditorViewMenu.Music;
                onButton = true;
            }

            Entity menu = bar.Open switch
            {
                EditorViewMenu.Grid => bar.GridMenu,
                EditorViewMenu.Music => bar.MusicMenu,
                _ => default,
            };
            Entity trigger = bar.Open == EditorViewMenu.Grid ? bar.GridButton : bar.MusicButton;
            if (justPressed && !onButton && !menu.IsNull &&
                !Contains(menu, input.Position) && !Contains(trigger, input.Position))
            {
                bar.Open = EditorViewMenu.None;
                menu = default;
            }

            Show(bar.GridMenu, bar.Open == EditorViewMenu.Grid, bar.GridButton, view, buffer);
            Show(bar.MusicMenu, bar.Open == EditorViewMenu.Music, bar.MusicButton, view, buffer);
            Tint(bar.GridButton, bar.Open == EditorViewMenu.Grid ? Palette.Primary : Palette.PrimaryMuted, buffer);
            Tint(bar.MusicButton, bar.Open == EditorViewMenu.Music ? Palette.Primary : Palette.PrimaryMuted, buffer);

            playhead.SnapOff = !EditorMusicSnap.IsOn(store);
            for (int i = 0; i < bar.DivisionButtons.Length; i++)
            {
                Entity button = bar.DivisionButtons[i];
                int division = EditorViewBar.Divisions[i].Division;
                bool pressed = bar.Open == EditorViewMenu.Music && button.Tags.Has<Pressed>();
                if (_presses.Started(button, pressed) && playhead.SnapDivision != division)
                    _commands.Enqueue(EditorCommand.SetSnap(division));

                bool current = playhead.SnapDivision == division;
                Tint(button, playhead.SnapOff ? SceneColors.Muted : current ? Palette.Primary : Palette.PrimaryMuted, buffer);
            }
        });
    }

    /// <summary>Shows a menu with its top-left under its button's left edge, or hides it.</summary>
    private static void Show(Entity menu, bool open, Entity trigger, PlayfieldView view, CommandBuffer buffer)
    {
        if (menu.IsNull)
            return;

        UiLayout.SetHidden(menu, buffer, !open);
        if (!open || !menu.HasComponent<Position>() || !trigger.HasComponent<Position>())
            return;

        Vector2 size = Size(menu);
        Vector2 button = UiTransform.WorldPosition(trigger, trigger.GetComponent<Position>().Value);
        float left = button.X - Size(trigger).X / 2f;
        float top = EditorGroupLayout.BarArea(view.ScreenSize).End.Y + MenuGap;
        Vector2 centre = new(left + size.X / 2f, top + size.Y / 2f);
        ref Position position = ref menu.GetComponent<Position>();
        if (position.Value == centre)
            return;

        position.Value = centre;
        buffer.AddTag<Moved>(menu.Id);
    }

    private static bool Contains(Entity entity, Vector2 point)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return false;

        Vector2 centre = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        Vector2 size = Size(entity);
        return new Rect2(centre - size / 2f, size).HasPoint(point);
    }

    private static Vector2 Size(Entity entity) =>
        entity.TryGetComponent(out Rectangle rect) ? rect.Bounds.Size : Vector2.Zero;

    private static void Tint(Entity button, Color color, CommandBuffer buffer)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        buffer.AddTag<Dirty>(button.Id);
    }
}
