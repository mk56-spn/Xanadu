using System;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Armed's hitboxes. The spear is a long, thin forward box; the axe is a
/// forward half-plane with an inner deadzone, so it hits an arc rather than
/// what the character is standing on.
/// </summary>
public static class WeaponHitRules
{
    public const float SpearLength = 280f;
    public const float SpearThickness = 26f;
    public const float AxeInner = 60f;
    public const float AxeOuter = 260f;

    /// <summary>The backward velocity change an axe hit gives the player.</summary>
    public const float AxeKnockbackSpeed = 350f;

    public static bool Contains(
        WeaponKind weapon,
        Vector2 playerScreen,
        bool facesLeft,
        Vector2 noteScreen,
        float scale)
    {
        float dx = noteScreen.X - playerScreen.X;
        float dy = noteScreen.Y - playerScreen.Y;

        if (weapon == WeaponKind.Spear)
        {
            float forward = facesLeft ? -dx : dx;
            return forward >= 0f &&
                   forward <= SpearLength * scale &&
                   Math.Abs(dy) <= SpearThickness * scale * 0.5f;
        }

        float distanceSq = dx * dx + dy * dy;
        float inner = AxeInner * scale;
        float outer = AxeOuter * scale;
        if (distanceSq < inner * inner || distanceSq > outer * outer)
            return false;

        return facesLeft ? dx <= 0f : dx >= 0f;
    }
}
