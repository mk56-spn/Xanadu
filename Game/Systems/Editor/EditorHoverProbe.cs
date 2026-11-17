using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Works out whether the current tool would place anything under the pointer,
/// and leaves that point on the editor's ghost for the draw system to show.
/// </summary>
public sealed class EditorHoverProbe(EditorSceneQuery scene, EditorItemPicker picker)
{
    public void Update(Vector2 screenPosition, bool pressed, bool hasPickedSelection)
    {
        Entity editor = scene.Editor();
        if (editor.IsNull || !editor.HasComponent<EditorPlacementGhost>())
            return;

        ref EditorPlacementGhost ghost = ref editor.GetComponent<EditorPlacementGhost>();
        ghost.Screen = screenPosition;
        ghost.Visible = !pressed && WouldPlaceAt(screenPosition, editor, hasPickedSelection);
    }

    private bool WouldPlaceAt(Vector2 screenPosition, Entity editor, bool hasPickedSelection)
    {
        // A curve being laid shows its next point over notes and selections alike.
        bool drafting = editor.TryGetComponent(out EditorRouteDraft draft) && draft.Active;
        if (scene.IsModalOpen() || (!drafting && hasPickedSelection))
        {
            return false;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Vector2 position = view.ScreenToPlayfield(screenPosition);
        if (!EditorViewport.Area(view.ScreenSize).HasPoint(screenPosition) ||
            !PlayfieldGrid.IsInside(position, editor.GetComponent<Playfield>()))
        {
            return false;
        }

        if (editor.TryGetComponent(out EditorSidebar sidebar) &&
            sidebar.Bounds.HasPoint(screenPosition))
        {
            return false;
        }

        return !scene.IsOverNoteBar(screenPosition) &&
               !scene.IsOverPanel(screenPosition) &&
               (drafting || picker.FindItemAt(position, editor.GetComponent<Playhead>().TimeMs).IsNull);
    }
}
