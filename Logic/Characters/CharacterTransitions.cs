using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Position = Xanadu.Core.Position;

namespace Xanadu.Logic.Characters;

/// <summary>
/// The character's motion states as clips, and the clip hand-overs a rig can
/// be asked for between them; the animator previews these. The play-time
/// motion system went with the classic game.
/// </summary>
public static class CharacterTransitions
{
    /// <summary>
    /// Every change of state <see cref="Next"/> can make. Rising always jumps,
    /// leaving the floor falls, touching it lands (or goes straight to the
    /// ground without a landing clip), a landing ends on the ground, and the
    /// ground and a slide turn into each other with sideways speed.
    /// </summary>
    private static readonly (CharacterMotionState From, CharacterMotionState To)[] StateChanges =
    [
        (CharacterMotionState.Grounded, CharacterMotionState.Jumping),
        (CharacterMotionState.Landing, CharacterMotionState.Jumping),
        (CharacterMotionState.Falling, CharacterMotionState.Jumping),
        (CharacterMotionState.Jumping, CharacterMotionState.Falling),
        (CharacterMotionState.Grounded, CharacterMotionState.Falling),
        (CharacterMotionState.Landing, CharacterMotionState.Falling),
        (CharacterMotionState.Jumping, CharacterMotionState.Landing),
        (CharacterMotionState.Falling, CharacterMotionState.Landing),
        (CharacterMotionState.Jumping, CharacterMotionState.Grounded),
        (CharacterMotionState.Falling, CharacterMotionState.Grounded),
        (CharacterMotionState.Landing, CharacterMotionState.Grounded),
        (CharacterMotionState.Sliding, CharacterMotionState.Jumping),
        (CharacterMotionState.Sliding, CharacterMotionState.Falling),
        (CharacterMotionState.Jumping, CharacterMotionState.Sliding),
        (CharacterMotionState.Falling, CharacterMotionState.Sliding),
        (CharacterMotionState.Landing, CharacterMotionState.Sliding),
        (CharacterMotionState.Grounded, CharacterMotionState.Sliding),
        (CharacterMotionState.Sliding, CharacterMotionState.Grounded),
    ];

    /// <summary>
    /// Clip for a state. A rig without a falling clip keeps its jump pose
    /// while falling, and one without a jump or slide clip stays idle.
    /// </summary>
    public static ContactClip ClipFor(CharacterMotionState state, AnimatorRuntime runtime) =>
        ClipFor(state, runtime.Package);

    public static ContactClip ClipFor(CharacterMotionState state, RigPackage package) => state switch
    {
        CharacterMotionState.Jumping => First(package, ContactClip.Jumping, ContactClip.Idle),
        CharacterMotionState.Falling => First(package, ContactClip.Falling, ContactClip.Jumping, ContactClip.Idle),
        CharacterMotionState.Landing => First(package, ContactClip.Landing, ContactClip.Idle),
        CharacterMotionState.Sliding => First(package, ContactClip.Sliding, ContactClip.Idle),
        _ => ContactClip.Idle,
    };

    /// <summary>
    /// The clip hand-overs this system can ask a rig for, given the clips it
    /// has. The package's transition table decides whether each one happens
    /// and how it blends; one it does not allow leaves the old clip playing.
    /// </summary>
    public static List<(ContactClip From, ContactClip To)> Transitions(RigPackage package)
    {
        bool landing = package.Has(ContactClip.Landing);
        var pairs = new List<(ContactClip, ContactClip)>();
        foreach ((CharacterMotionState from, CharacterMotionState to) in StateChanges)
        {
            // With a landing clip, touching down always lands first.
            if (landing &&
                from is CharacterMotionState.Jumping or CharacterMotionState.Falling &&
                to is CharacterMotionState.Grounded or CharacterMotionState.Sliding)
            {
                continue;
            }

            ContactClip a = ClipFor(from, package);
            ContactClip b = ClipFor(to, package);
            if (a != b && !pairs.Contains((a, b)))
                pairs.Add((a, b));
        }
        return pairs;
    }

    private static ContactClip First(RigPackage package, params ContactClip[] clips)
    {
        foreach (ContactClip clip in clips)
        {
            if (package.Has(clip))
                return clip;
        }
        return clips[^1];
    }
}
