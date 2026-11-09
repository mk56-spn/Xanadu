using System;
using Godot;
using Xanadu.Timeline;

namespace Xanadu.Animation;

public enum XafTargetShape
{
    /// <summary>A fixed spot, such as a planted foot.</summary>
    Spot,

    /// <summary>Round an ellipse, such as a fist's arc or the sway of the hips.</summary>
    Ellipse,
}

/// <summary>
/// A path the end of a limb follows over part of a clip. The file keeps this as
/// the source and also the rotation keys baked from it, so it stays editable while
/// the game just plays keys and never solves anything. Those keys belong to the
/// target: baking replaces them inside its span.
/// </summary>
public sealed record XafLimbTarget
{
    public const float MinRadius = 0.5f;

    public string BoneId = "";

    /// <summary>The bone it travels with, or empty for fixed in the rig: a fist goes with the torso, a foot stays on the ground.</summary>
    public string AnchorId = "";

    public XafTargetShape Shape;

    /// <summary>The spot, or the ellipse's centre, in the anchor's space.</summary>
    public Vector2 Center;

    public float RadiusX;
    public float RadiusY;

    /// <summary>The ellipse's turn in the anchor's space, in radians.</summary>
    public float Tilt;

    /// <summary>Where on the ellipse it starts, in radians.</summary>
    public float StartAngle;

    /// <summary>How far round it goes, in radians; negative goes the other way, a full turn loops.</summary>
    public float Sweep = Mathf.Tau;

    public float From;
    public float To;

    /// <summary>How the time across the span maps to the way round the ellipse.</summary>
    public EasePreset Ease = EasePreset.Linear;

    /// <summary>Keep the end bone facing one way in the rig, as a planted foot does.</summary>
    public bool HoldFacing;
    public float Facing;

    /// <summary>Which way the middle joint folds unless it has a bend lock, so baking is repeatable.</summary>
    public bool BendPositive;

    public bool Covers(float time) => time >= From - XafTracks.TimeTolerance && time <= To + XafTracks.TimeTolerance;

    /// <summary>A point on the ellipse in the anchor's space, at an angle round it.</summary>
    public Vector2 OnEllipse(float angle) =>
        Center + new Vector2(RadiusX * MathF.Cos(angle), RadiusY * MathF.Sin(angle)).Rotated(Tilt);

    /// <summary>Where it is in the anchor's space at a time; clamped to the span.</summary>
    public Vector2 LocalAt(float time)
    {
        if (Shape == XafTargetShape.Spot)
            return Center;

        float share = To > From ? Math.Clamp((time - From) / (To - From), 0f, 1f) : 0f;
        return OnEllipse(StartAngle + Sweep * EasePresets.Progress(Ease, share));
    }
}
