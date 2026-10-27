using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Animation;

/// <summary>Versioned, self-contained character animation package.</summary>
public sealed class XafPackage
{
    public const ushort MajorVersion = 1;
    public const ushort MinorVersion = 1;

    public ushort Major = MajorVersion;
    public ushort Minor = MinorVersion;
    public XafSkeleton Skeleton = new();
    public List<XafAttachment> Attachments = new();
    public List<XafClip> Clips = new();
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
}

/// <summary>Individual scalar transform channel; allows independent curves.</summary>
public enum XafChannelProperty
{
    TranslationX,
    TranslationY,
    Rotation,
    ScaleX,
    ScaleY,
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
