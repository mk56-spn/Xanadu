using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Animation;

namespace Xanadu.Tools.Animator;

/// <summary>
/// One channel key captured by COPY KEYS. The time is stored relative to the
/// earliest copied key so a paste can put the whole set down at the playhead.
/// </summary>
public readonly record struct AnimatorKeySample(
    XafChannelTarget Target,
    string Id,
    XafChannelProperty Property,
    float Offset,
    float Value,
    XafInterpolation Interpolation,
    float TangentIn,
    float TangentOut);

/// <summary>
/// Key sets copied out of a clip. The samples carry their channel and curve
/// data, so pasting works across times, clips and even rig edits that kept the
/// same bone ids.
/// </summary>
public struct AnimatorKeyClipboard : IComponent
{
    public List<AnimatorKeySample>? Keys;

    /// <summary>Seconds from the first copied key to the last, so a paste can fit before the clip end.</summary>
    public float Length;

    /// <summary>The copied rows and times, so a paste can select exactly what it made.</summary>
    public List<AnimatorKeyRef>? Refs;

    /// <summary>The earliest copied time; refs are offset from it.</summary>
    public float Anchor;
}
