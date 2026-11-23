using System;
using System.Runtime.CompilerServices;

// This file contains extra members for the Mathf class that aren't part of Godot's Core API.
// Math API that is also part of Core should go into Mathf.cs.

namespace Xanadu.Core.Maths;

public static partial class Mathf
{
    /// <summary>
    /// A very small number used for float comparison with error tolerance (1e-06).
    /// Not <see cref="float.Epsilon"/>, which is the smallest denormal.
    /// </summary>
    public const float Epsilon = EpsilonF;

    private const float EpsilonF = 1e-06f;
    private const double EpsilonD = 1e-14;

    /// <summary>
    /// Rounds <paramref name="s"/> upward (towards positive infinity).
    ///
    /// This is the same as <see cref="Ceil(float)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to ceil.</param>
    /// <returns>The smallest whole number that is not less than <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CeilToInt(float s)
    {
        return (int)MathF.Ceiling(s);
    }

    /// <summary>
    /// Rounds <paramref name="s"/> upward (towards positive infinity).
    ///
    /// This is the same as <see cref="Ceil(double)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to ceil.</param>
    /// <returns>The smallest whole number that is not less than <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CeilToInt(double s)
    {
        return (int)Math.Ceiling(s);
    }

    /// <summary>
    /// Rounds <paramref name="s"/> downward (towards negative infinity).
    ///
    /// This is the same as <see cref="Floor(float)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to floor.</param>
    /// <returns>The largest whole number that is not more than <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorToInt(float s)
    {
        return (int)MathF.Floor(s);
    }

    /// <summary>
    /// Rounds <paramref name="s"/> downward (towards negative infinity).
    ///
    /// This is the same as <see cref="Floor(double)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to floor.</param>
    /// <returns>The largest whole number that is not more than <paramref name="s"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorToInt(double s)
    {
        return (int)Math.Floor(s);
    }

    /// <summary>
    /// Rounds <paramref name="s"/> to the nearest whole number.
    ///
    /// This is the same as <see cref="Round(float)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to round.</param>
    /// <returns>The rounded number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundToInt(float s)
    {
        return (int)MathF.Round(s);
    }

    /// <summary>
    /// Rounds <paramref name="s"/> to the nearest whole number.
    ///
    /// This is the same as <see cref="Round(double)"/>, but returns an <see langword="int"/>.
    /// </summary>
    /// <param name="s">The number to round.</param>
    /// <returns>The rounded number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RoundToInt(double s)
    {
        return (int)Math.Round(s);
    }

    /// <summary>
    /// Returns the sine and cosine of angle <paramref name="s"/> in radians.
    /// </summary>
    /// <param name="s">The angle in radians.</param>
    /// <returns>The sine and cosine of that angle.</returns>
    public static (float Sin, float Cos) SinCos(float s)
    {
        return MathF.SinCos(s);
    }

    /// <summary>
    /// Returns the sine and cosine of angle <paramref name="s"/> in radians.
    /// </summary>
    /// <param name="s">The angle in radians.</param>
    /// <returns>The sine and cosine of that angle.</returns>
    public static (double Sin, double Cos) SinCos(double s)
    {
        return Math.SinCos(s);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> are approximately
    /// equal to each other.
    /// The comparison is done using the provided tolerance value.
    /// If you want the tolerance to be calculated for you, use <see cref="IsEqualApprox(float, float)"/>.
    /// </summary>
    /// <param name="a">One of the values.</param>
    /// <param name="b">The other value.</param>
    /// <param name="tolerance">The pre-calculated tolerance value.</param>
    /// <returns>A <see langword="bool"/> for whether or not the two values are equal.</returns>
    public static bool IsEqualApprox(float a, float b, float tolerance)
    {
        // Check for exact equality first, required to handle "infinity" values.
        if (a == b)
        {
            return true;
        }
        // Then check for approximate equality.
        return Math.Abs(a - b) < tolerance;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> are approximately
    /// equal to each other.
    /// The comparison is done using the provided tolerance value.
    /// If you want the tolerance to be calculated for you, use <see cref="IsEqualApprox(double, double)"/>.
    /// </summary>
    /// <param name="a">One of the values.</param>
    /// <param name="b">The other value.</param>
    /// <param name="tolerance">The pre-calculated tolerance value.</param>
    /// <returns>A <see langword="bool"/> for whether or not the two values are equal.</returns>
    public static bool IsEqualApprox(double a, double b, double tolerance)
    {
        // Check for exact equality first, required to handle "infinity" values.
        if (a == b)
        {
            return true;
        }
        // Then check for approximate equality.
        return Math.Abs(a - b) < tolerance;
    }
}
