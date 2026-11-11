using Friflo.Engine.ECS;
using Xanadu.Ecs;

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
        // The polygon tool lays points over items and selections alike.
        bool polygon = editor.HasComponent<EditorPlacement>() &&
                       editor.GetComponent<EditorPlacement>().Mode == EditorPlacementMode.Polygon;
        if (scene.IsGroupTargetEditing() ||
            scene.IsModalOpen() ||
            (!polygon && hasPickedSelection))
        {
            return false;
        }

        PlayfieldView view = editor.GetComponent<PlayfieldView>();
        Vector2 position = view.ScreenToPlayfield(screenPosition);
        if (!EditorViewport.Area(view.ScreenSize).HasPoint(screenPosition) ||
            !EditorItemPicker.IsInside(position, editor.GetComponent<Playfield>()))
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
               (polygon || picker.FindItemAt(position, editor.GetComponent<Playhead>().TimeMs).IsNull);
    }
}
