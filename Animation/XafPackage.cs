using System;
using System.Collections.Generic;
using Godot;
using Xanadu.Items;

namespace Xanadu.Animation;

/// <summary>Versioned, self-contained character animation package.</summary>
public sealed class XafPackage
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 6;

    public ushort Major = MajorVersion;
    public ushort Minor = MinorVersion;
    public XafSkeleton Skeleton = new();
    public List<XafAttachment> Attachments = new();
    public List<XafChain> Chains = new();
    public List<XafClip> Clips = new();

    /// <summary>Which clips may fade into which, and how; see <see cref="XafTransitions"/>.</summary>
    public List<XafTransition> Transitions = new();

    /// <summary>Items drawn on the rig only while a clip keys them visible, such as streaks and flashes.</summary>
    public List<XafEffect> Effects = new();

    /// <summary>Simulated sheets pinned to the body, such as a dress; see <see cref="XafCloth"/>.</summary>
    public List<XafCloth> Cloths = new();
}

/// <summary>
/// An item that rides on a bone and appears only where a clip keys it
/// visible: a speed streak, an impact flash. Its look is a <see cref="Xif"/>;
/// clips key its transform, opacity and visibility like an attachment's.
/// </summary>
public sealed class XafEffect
{
    public string Id = "";
    public string BoneId = "";

    /// <summary>Where it sits on its bone when no clip moves it.</summary>
    public Transform2D Transform = Transform2D.Identity;

    public int ZOrder;
    public Xif Item = new();
}

/// <summary>Bone hierarchy with one root and stable string IDs.</summary>
public sealed class XafSkeleton
{
    public List<XafBone> Bones = new();
}

public sealed class XafBone
{
    public string Id = "";
    public int Parent = -1;
    public Transform2D Rest = Transform2D.Identity;
    public int ZOrder;

    /// <summary>
    /// Lets animation translate this bone away from its rest offset. Bones are
    /// fixed length by default; the root always moves.
    /// </summary>
    public bool AllowTranslation;

    public bool CanTranslate => Parent < 0 || AllowTranslation;
}

/// <summary>Rigid SVG group attached to a bone.</summary>
public sealed class XafAttachment
{
    /// <summary>The range of draw layers, which is what a canvas item's z index allows.</summary>
    public const int MinLayer = -4096;
    public const int MaxLayer = 4096;

    public string SvgPath = "";
    public string Group = "";
    public string BoneId = "";
    public Transform2D Transform = Transform2D.Identity;
    public int ZOrder;
}

/// <summary>
/// A Verlet chain hanging off a bone, for hair, tails and cloth edges. The file
/// holds only the settings; the points are simulated where the rig is shown.
/// </summary>
public sealed class XafChain
{
    public const int MinSegments = 1;
    public const int MaxSegments = 64;
    public const float MinSegmentLength = 1f;
    public const float MaxSegmentLength = 500f;

    /// <summary>The golden rhombus a new chain is built from, one per segment.</summary>
    public const string DefaultSvgPath = "res://Assets/SVG/Char/hair_segment.svg";
    public const string DefaultGroup = "hair_segment";

    public string Id = "";

    /// <summary>The bone the first point is pinned to.</summary>
    public string BoneId = "";

    /// <summary>Where the first point sits, in the bone's own space.</summary>
    public Vector2 Offset;

    public int Segments = 6;
    public float SegmentLength = 20f;

    /// <summary>Downward pull in pixels per second squared.</summary>
    public float Gravity = 900f;

    /// <summary>Share of velocity kept each step; 1 never slows down, 0 stops dead.</summary>
    public float Damping = 0.96f;

    /// <summary>
    /// The SVG group drawn once per segment, long side along the chain; empty
    /// draws no art. The art is stretched to fit each segment, so any shape works.
    /// </summary>
    public string SvgPath = DefaultSvgPath;
    public string Group = DefaultGroup;

    /// <summary>Width of the art at the first segment; it tapers towards the tip.</summary>
    public float Width = 10f;

    /// <summary>
    /// Sideways pull in pixels per second squared, positive towards the way the
    /// character faces. A little of it sends a chain that lands on the collider
    /// off the front of it, over the shoulder, rather than behind.
    /// </summary>
    public float Lean;

    /// <summary>
    /// A circle on a bone that the chain cannot enter, so it rests on it instead
    /// of passing through. Radius 0 means no collider.
    /// </summary>
    public string CollideBoneId = "";
    public Vector2 CollideOffset;
    public float CollideRadius;

    public bool HasArt => SvgPath.Length > 0 && Group.Length > 0;
    public bool HasCollider => CollideRadius > 0f && CollideBoneId.Length > 0;
}

public sealed class XafClip
{
    public string Id = "";
    public float Duration;
    public bool Loop;
    public float LoopStart;
    public float LoopEnd;
    public List<XafChannel> Channels = new();
}

public enum XafChannelTarget
{
    Bone,
    Attachment,

    /// <summary>An <see cref="XafEffect"/>, by id.</summary>
    Effect,
}

/// <summary>Individual scalar transform channel; allows independent curves.</summary>
public enum XafChannelProperty
{
    TranslationX,
    TranslationY,
    Rotation,
    ScaleX,
    ScaleY,

    /// <summary>An effect's opacity, 0 to 1.</summary>
    Opacity,

    /// <summary>Whether an effect shows: 1 from a key on, 0 from a key off. Always held, never blended.</summary>
    Visible,
}

public enum XafInterpolation
{
    Step,
    Linear,
    CubicBezier,
}

public sealed class XafChannel
{
    public XafChannelTarget Target;
    public string TargetId = "";
    public XafChannelProperty Property;
    public List<XafKeyframe> Keyframes = new();
}

public sealed class XafKeyframe
{
    public float Time;
    public float Value;
    public XafInterpolation Interpolation = XafInterpolation.Linear;

    /// <summary>Reserved for cubic curves; zero until bezier authoring exists.</summary>
    public float TangentIn;
    public float TangentOut;
}

/// <summary>Thrown when an XAF package is structurally invalid.</summary>
public sealed class XafFormatException(string message) : Exception(message);
