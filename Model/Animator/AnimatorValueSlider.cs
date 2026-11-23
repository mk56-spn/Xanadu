using Friflo.Engine.ECS;

namespace Xanadu.Model.Animator;

/// <summary>A number a panel value field shows and sets; <see cref="AnimatorValueFieldSystem"/> binds each.</summary>
public enum AnimatorSliderValue
{
    /// <summary>The selected joints' depth toward the viewer; keyed on release.</summary>
    Depth,

    /// <summary>The low end of the selected limited bones' turn, in degrees.</summary>
    LimitMin,

    /// <summary>The high end of the selected limited bones' turn, in degrees.</summary>
    LimitMax,
    BrushSize,
    BrushStrength,

    /// <summary>How wide a zone two bones share the selected mesh over, in pixels.</summary>
    MeshBlend,

    /// <summary>The selected effect's opacity at the playhead, in percent; keyed as it moves.</summary>
    EffectOpacity,
    TargetRadiusX,
    TargetRadiusY,
    TargetTilt,
    TargetStart,
    TargetSweep,

    /// <summary>The selected transition's fade, in seconds.</summary>
    TransitionFade,
}


