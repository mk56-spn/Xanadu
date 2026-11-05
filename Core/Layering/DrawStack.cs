using System;
using System.Collections.Generic;

namespace Xanadu.Layering;

/// <summary>How moving forward or back past a neighbour changes the slots.</summary>
public enum DrawStackRule
{
    /// <summary>
    /// The layer is the only order there is (the animator): past a neighbour on
    /// the same layer, step one layer; past one on another layer, swap layers.
    /// </summary>
    Layers,

    /// <summary>
    /// Layers are fixed and z orders inside them (levels): past a neighbour on
    /// the same layer, take its z; a neighbour on another layer is a wall, since
    /// changing layer is done on purpose, not by nudging.
    /// </summary>
    ZInLayer,
}

/// <summary>
/// A stack of draw slots listed front first, as a layers panel shows them, and
/// moving one entry a place forward or back through it.
/// </summary>
public static class DrawStack
{
    /// <summary>Sorts <paramref name="rows"/> front first.</summary>
    public static void SortFrontFirst<T>(List<T> rows, Func<T, DrawSlot> slot) =>
        rows.Sort((a, b) => slot(b).CompareTo(slot(a)));

    /// <summary>
    /// The row a move from <paramref name="at"/> passes, in a front-first list:
    /// forward (+1) is towards the start. -1 when there is none.
    /// </summary>
    public static int Neighbour(int at, int direction, int count)
    {
        int next = at - Math.Sign(direction);
        return at < 0 || at >= count || next < 0 || next >= count ? -1 : next;
    }

    /// <summary>
    /// Moves <paramref name="row"/> one place forward (+1) or back (-1) past
    /// <paramref name="neighbour"/>, giving both slots afterwards. False when the
    /// rule doesn't allow the move; the slots are then unchanged.
    /// </summary>
    public static bool TryMove(
        DrawSlot row,
        DrawSlot neighbour,
        int direction,
        DrawStackRule rule,
        out DrawSlot movedRow,
        out DrawSlot movedNeighbour)
    {
        int step = Math.Sign(direction);
        movedRow = row;
        movedNeighbour = neighbour;
        if (step == 0)
            return false;

        bool sameLayer = row.Layer == neighbour.Layer;
        switch (rule)
        {
            case DrawStackRule.Layers when sameLayer:
                movedRow = row with { Layer = DrawOrder.ClampLayer(row.Layer + step) };
                return true;
            case DrawStackRule.Layers:
                movedRow = row with { Layer = neighbour.Layer };
                movedNeighbour = neighbour with { Layer = row.Layer };
                return true;
            case DrawStackRule.ZInLayer when !sameLayer:
                return false;
            default:
                if (row.Z != neighbour.Z)
                {
                    movedRow = row with { Z = neighbour.Z };
                    movedNeighbour = neighbour with { Z = row.Z };
                }
                else
                {
                    // Tied on z, the list order decides; stepping z past the
                    // neighbour's puts the row on the far side of it.
                    movedRow = row with { Z = neighbour.Z + step };
                }

                return true;
        }
    }
}
