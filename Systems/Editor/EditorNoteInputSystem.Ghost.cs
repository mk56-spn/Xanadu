using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>Works out where a click would place the current tool's item, for the ghost to show.</summary>
public sealed partial class EditorNoteInputSystem
{
    private void UpdateGhost(Vector2 screenPosition, bool pressed)
    {
        Entity editor = FindEditor();
        if (editor.IsNull || !editor.HasComponent<EditorPlacementGhost>())
            return;

        ref EditorPlacementGhost ghost = ref editor.GetComponent<EditorPlacementGhost>();
        ghost.Screen = screenPosition;
        ghost.Visible = !pressed && WouldPlaceAt(screenPosition, editor);
    }

    private bool WouldPlaceAt(Vector2 screenPosition, Entity editor)
    {
        if (IsGroupTargetEditing() ||
            IsWarningOpen() ||
            IsGroupOverlayOpen() ||
            IsSceneConfirmationOpen() ||
            EditorPlacementNote.NeedsShape(editor))
        {
            return false;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Vector2 position = view.ScreenToPlayfield(screenPosition);
        if (!EditorViewport.Area(view.ScreenSize).HasPoint(screenPosition) ||
            !IsInside(position, editor.GetComponent<Playfield>()))
        {
            return false;
        }

        if (editor.TryGetComponent(out EditorSidebar sidebar) &&
            sidebar.Bounds.HasPoint(screenPosition))
        {
            return false;
        }

        return !IsOverNoteBar(screenPosition) &&
               !IsOverPanel(screenPosition) &&
               FindItemAt(position, editor.GetComponent<Playhead>().TimeMs).IsNull;
    }

    /// <summary>The item a press at this point would pick, or null.</summary>
    private Entity FindItemAt(Vector2 position, long playheadMs)
    {
        Entity hit = FindNoteAt(position, playheadMs);
        if (hit.IsNull)
            hit = FindMineAt(position, playheadMs);
        if (hit.IsNull)
            hit = FindStartAt(position);
        if (hit.IsNull)
            hit = FindSpikeAt(position);
        if (hit.IsNull)
            hit = FindBlockAt(position);
        return hit;
    }
}
