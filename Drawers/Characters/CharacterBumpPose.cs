using System;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;

namespace Xanadu.Drawers.Characters;

/// <summary>
/// What a <see cref="CharacterBump"/> does to the drawn player: her upper body
/// jolts away from the block with a springy wobble and her body squashes
/// briefly along the hit. Only the drawing reacts; the physics never feels it.
/// </summary>
public static class CharacterBumpPose
{
    /// <summary>How far the whole body squashes along the hit at the fullest, as a share of its size.</summary>
    public const float Squash = 0.1f;

    /// <summary>Upper-body bones and how far each turns at the fullest flinch, in radians; the chain whips more towards the ends.</summary>
    private static readonly (string Bone, float Turn)[] Flinch =
    {
        ("spine", 0.12f),
        ("chest", 0.16f),
        ("head", 0.28f),
        ("upper_arm_l", 0.45f),
        ("forearm_l", 0.35f),
        ("upper_arm_r", 0.45f),
        ("forearm_r", 0.35f),
    };

    /// <summary>
    /// How much of the flinch shows <paramref name="age"/> seconds after the bump:
    /// a near-instant jolt, then a spring back that overshoots once and settles.
    /// </summary>
    public static float Envelope(float age)
    {
        if (age < 0f || age >= CharacterBump.Seconds)
            return 0f;

        float attack = Math.Min(1f, age / 0.03f);
        float tail = 1f - age / CharacterBump.Seconds;
        return attack * MathF.Exp(-7f * age) * MathF.Cos(18f * age) * tail;
    }

    /// <summary>
    /// Turns <paramref name="pose"/>'s upper body away from the bump. Rig
    /// space faces right, where a positive turn tips the top forwards, so the
    /// hit's direction is mirrored for a player facing left.
    /// </summary>
    public static void ApplyFlinch(XafPackage package, XafPose pose, in CharacterBump bump, bool faceLeft)
    {
        float amount = Envelope(bump.Age) * bump.Strength;
        if (amount == 0f)
            return;

        float across = faceLeft ? -bump.Normal.X : bump.Normal.X;
        // Away from a wall, and ducked forwards from a ceiling.
        float lean = across + 0.7f * Math.Max(bump.Normal.Y, 0f);
        foreach ((string bone, float turn) in Flinch)
        {
            int index = package.Skeleton.Bones.FindIndex(entry => entry.Id == bone);
            if (index >= 0 && index < pose.Bones.Length)
                pose.Bones[index].Rotation += turn * lean * amount;
        }
    }

    /// <summary>
    /// The squash the bump puts on the drawn body, about <paramref name="centre"/>
    /// on screen: shorter along the hit, a little wider across it.
    /// </summary>
    public static Transform2D SquashAbout(in CharacterBump bump, Vector2 centre)
    {
        float amount = Envelope(bump.Age) * bump.Strength * Squash;
        if (amount == 0f)
            return Transform2D.Identity;

        float angle = MathF.Atan2(bump.Normal.Y, bump.Normal.X);
        var toCentre = new Transform2D(0f, -centre);
        var turn = new Transform2D(-angle, Vector2.Zero);
        var squash = new Transform2D(0f, new Vector2(1f - amount, 1f + amount * 0.5f), 0f, Vector2.Zero);
        var back = new Transform2D(angle, centre);
        return back * squash * turn * toCentre;
    }
}
