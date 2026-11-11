using System;
using Xanadu.Animation;

namespace Xanadu.Ecs;

/// <summary>Every animation the game plays on the contact character's rig.</summary>
public enum ContactClip
{
    Idle,
    Jumping,

    /// <summary>Optional: a rig without it keeps its jump pose while falling.</summary>
    Falling,

    Landing,
    Sliding,
    Death,
    AttackLow,
    AttackMid,
    AttackHigh,
    AttackAirLow,
    AttackAirMid,
    AttackAirHigh,
}

/// <summary>
/// Where a <see cref="ContactClip"/> meets the rig file. The .xaf names its
/// clips by string; <see cref="Id"/> is the only place those names are written.
/// </summary>
public static class ContactClips
{
    public static readonly ContactClip[] All = Enum.GetValues<ContactClip>();

    /// <summary>The clip's name in the contact rig's .xaf.</summary>
    public static string Id(this ContactClip clip) => clip switch
    {
        ContactClip.Idle => "idle",
        ContactClip.Jumping => "jumping",
        ContactClip.Falling => "falling",
        ContactClip.Landing => "landing",
        ContactClip.Sliding => "sliding",
        ContactClip.Death => "death",
        ContactClip.AttackLow => "attack_low",
        ContactClip.AttackMid => "attack_mid",
        ContactClip.AttackHigh => "attack_high",
        ContactClip.AttackAirLow => "attack_air_low",
        ContactClip.AttackAirMid => "attack_air_mid",
        ContactClip.AttackAirHigh => "attack_air_high",
        _ => throw new ArgumentOutOfRangeException(nameof(clip), clip, null),
    };

    /// <summary>The clip a rig file name stands for, or null for one the game never plays.</summary>
    public static ContactClip? FromId(string id)
    {
        foreach (ContactClip clip in All)
        {
            if (clip.Id() == id)
                return clip;
        }

        return null;
    }

    public static bool Has(this XafPackage package, ContactClip clip) =>
        package.Clips.Exists(entry => entry.Id == clip.Id());

    /// <summary>The clip's length in seconds, or 0 when the rig lacks it.</summary>
    public static float Length(this XafPackage package, ContactClip clip) =>
        package.Clips.Find(entry => entry.Id == clip.Id())?.Duration ?? 0f;
}
