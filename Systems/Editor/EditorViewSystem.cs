using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Moves the editor's view of the playfield: pans while the middle button is
/// dragged (starting over the field area), zooms by the notches the overlay
/// queued, fits the field on request, and reopens the view when the field
/// changes size. See <see cref="EditorViewport"/>.
/// </summary>
public sealed class EditorViewSystem(IPointerInputSource pointer)
    : QuerySystem<PlayfieldView, Playfield, EditorViewNavigation, PlayfieldMode>
{
    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        Query.ForEachEntity((ref view, ref playfield, ref navigation, ref mode, _) =>
        {
            if (mode.Kind != PlayfieldKind.Editor)
                return;

            var size = new Vector2(playfield.Width, playfield.Height);
            if (navigation.FieldSize != size)
            {
                navigation.FieldSize = size;
                view = EditorViewport.Open(view.ScreenSize, playfield);
            }

            if (navigation.FitRequested)
            {
                navigation.FitRequested = false;
                view = EditorViewport.Fit(view.ScreenSize, playfield);
            }

            if (navigation.ZoomSteps != 0)
            {
                float factor = Mathf.Pow(EditorViewport.WheelStep, navigation.ZoomSteps);
                view = EditorViewport.Zoom(view, playfield, factor, input.Position);
                navigation.ZoomSteps = 0;
            }

            if (!input.Middle)
            {
                navigation.PanFrom = null;
            }
            else if (navigation.PanFrom is { } from)
            {
                view = EditorViewport.Pan(view, input.Position - from);
                navigation.PanFrom = input.Position;
            }
            else if (EditorViewport.Area(view.ScreenSize).HasPoint(input.Position))
            {
                navigation.PanFrom = input.Position;
            }
        });
    }
}
