using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;

namespace Xanadu.Systems.Editor;

/// <summary>
/// What the editor draws for groups on top of the live items: a mark on each
/// item a group has moved at the playhead, and the selected move's target handle.
/// </summary>
public sealed class EditorGroupsDrawer(EditorDrawSurface surface)
{
    /// <summary>The colour of the moved-item marks.</summary>
    public static readonly Color MarkColor = Palette.Info;

    /// <summary>Moves shorter than this many playfield pixels aren't marked.</summary>
    private const float MarkMin = 0.5f;

    /// <summary>
    /// Every item drawn away from where it was placed gets a faint line back to
    /// a small ring at its placed position, so it's
    /// clear the item is shown moved and where edits really land (step 3a).
    /// </summary>
    public void DrawMoved(PlayfieldView view)
    {
        Color badge = MarkColor;
        var line = new Color(badge.R, badge.G, badge.B, 0.35f);
        var ring = new Color(badge.R, badge.G, badge.B, 0.6f);
        foreach (var (offsets, chunk) in surface.Entities.Cached<GroupOffset>().Chunks)
        {
            System.Span<GroupOffset> span = offsets.Span;
            for (int i = 0; i < span.Length; i++)
            {
                Vector2 offset = span[i].Offset;
                if (offset.LengthSquared() < MarkMin * MarkMin ||
                    !EditorGroupTarget.TryPosition(chunk.EntityAt(i), out Vector2 placed))
                {
                    continue;
                }

                Vector2 from = view.PlayfieldToScreen(placed);
                Vector2 to = view.PlayfieldToScreen(placed + offset);
                surface.Canvas.DrawLine(surface.Target, from, to, line, 1);
                surface.Canvas.DrawCircleOutline(surface.Target, from, 4f, ring, 1.5f);
            }
        }
    }

    /// <summary>The draggable handle for the selected move's full offset.</summary>
    public void DrawTarget(
        PlayfieldView view,
        Playfield playfield,
        LevelGroups groups,
        EditorGroupState state,
        NoteSelection selection)
    {
        if (groups.Groups is null ||
            state.SelectedGroup < 0 ||
            state.SelectedGroup >= groups.Groups.Length)
        {
            return;
        }

        GroupDefinition group = groups.Groups[state.SelectedGroup];
        if (state.SelectedEffect < 0 || state.SelectedEffect >= group.Effects.Count)
            return;

        GroupEffect effect = group.Effects[state.SelectedEffect];
        if (effect.Kind != GroupEffectKind.Move)
            return;

        Entity member = EditorGroupTarget.AnchorMember(
            surface.Entities, selection, state.SelectedGroup);
        Vector2 anchor = EditorGroupTarget.TryPosition(member, out Vector2 position)
            ? position
            : new Vector2(playfield.Width, playfield.Height) / 2f;
        Vector2 from = view.PlayfieldToScreen(anchor);
        Vector2 to = view.PlayfieldToScreen(anchor + effect.Offset);
        Color accent = Palette.Accent;

        surface.Canvas.DrawLine(surface.Target, from, to, new Color(accent.R, accent.G, accent.B, 0.55f), 2);
        surface.Canvas.DrawCircleOutline(surface.Target, to, EditorGroupTarget.HandleRadius, accent, 2);
        surface.Canvas.DrawCircle(surface.Target, to, 5, accent);
        surface.Canvas.DrawTextLeft(
            surface.Target,
            new Rect2(to + new Vector2(16, -26), new Vector2(130, 18)),
            $"({effect.Offset.X:0.#}, {effect.Offset.Y:0.#})",
            accent);

        Rect2 field = PlayfieldRenderer.FieldRect(view, playfield);
        var banner = new Rect2(
            field.Position + new Vector2(8, 8),
            new Vector2(Mathf.Min(field.Size.X - 16, 620), 24));
        surface.Canvas.DrawRect(surface.Target, banner, new Color(0.02f, 0.05f, 0.1f, 0.82f));
        surface.Canvas.DrawTextLeft(
            surface.Target,
            banner.Grow(-4),
            $"TARGET · G{state.SelectedGroup + 1} · MOVE {state.SelectedEffect + 1} · drag · Esc done · T timing",
            accent);
    }
}
