using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Input;
using Xanadu.Model.Editor;

namespace Xanadu.Logic.Editor;

// Reads the pointer outside UI (BannedSymbols.txt): still to port to a UI concept.
#pragma warning disable RS0030

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
        Query.Each(new Navigate(input));
    }

    private readonly record struct Navigate(PointerInput Input) : IEach<PlayfieldView, Playfield, EditorViewNavigation, PlayfieldMode>
    {
        public void Execute(ref PlayfieldView view, ref Playfield playfield, ref EditorViewNavigation navigation, ref PlayfieldMode mode)
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
                float factor = MathF.Pow(EditorViewport.WheelStep, navigation.ZoomSteps);
                view = EditorViewport.Zoom(view, playfield, factor, Input.Position);
                navigation.ZoomSteps = 0;
            }

            if (!Input.Middle)
            {
                navigation.PanFrom = null;
            }
            else if (navigation.PanFrom is { } from)
            {
                view = EditorViewport.Pan(view, Input.Position - from);
                navigation.PanFrom = Input.Position;
            }
            else if (EditorViewport.Area(view.ScreenSize).HasPoint(Input.Position))
            {
                navigation.PanFrom = Input.Position;
            }
        }
    }
}
