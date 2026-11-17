using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Ecs.Components;

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
