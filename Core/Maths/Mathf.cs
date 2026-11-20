using System;
using System.Runtime.CompilerServices;

namespace Xanadu.Core.Maths
{
    /// <summary>
    /// Provides constants and static methods for common mathematical functions.
    /// </summary>
    public static partial class Mathf
    {
        // Define constants with Decimal precision and cast down to double or float.

        /// <summary>
        /// The circle constant, the circumference of the unit circle in radians.
        /// </summary>
        // 6.2831855f and 6.28318530717959
        /// <summary>
        /// Constant that represents how many times the diameter of a circle
        /// fits around its perimeter. This is equivalent to <c>Mathf.Tau / 2</c>.
        /// </summary>
        // 3.1415927f and 3.14159265358979
        // 0.0174532924f and 0.0174532925199433
        // 57.29578f and 57.2957795130823
        /// <summary>
        /// Returns the difference between the two angles,
        /// in range of -<see cref="Pi"/>, <see cref="Pi"/>.
        /// When <paramref name="from"/> and <paramref name="to"/> are opposite,
        /// returns -<see cref="Pi"/> if <paramref name="from"/> is smaller than <paramref name="to"/>,
        /// or <see cref="Pi"/> otherwise.
        /// </summary>
        /// <param name="from">The start angle.</param>
        /// <param name="to">The destination angle.</param>
        /// <returns>The difference between the two angles.</returns>
        public static float AngleDifference(float from, float to)
        {
            float difference = (to - from) % MathF.Tau;
            return ((2.0f * difference) % MathF.Tau) - difference;
        }

        /// <summary>
        /// Returns the difference between the two angles,
        /// in range of -<see cref="Pi"/>, <see cref="Pi"/>.
        /// When <paramref name="from"/> and <paramref name="to"/> are opposite,
        /// returns -<see cref="Pi"/> if <paramref name="from"/> is smaller than <paramref name="to"/>,
        /// or <see cref="Pi"/> otherwise.
        /// </summary>
        /// <param name="from">The start angle.</param>
        /// <param name="to">The destination angle.</param>
        /// <returns>The difference between the two angles.</returns>
        public static double AngleDifference(double from, double to)
        {
            double difference = (to - from) % Math.Tau;
            return ((2.0 * difference) % Math.Tau) - difference;
        }

        /// <summary>
        /// Returns the arc tangent of <paramref name="y"/> and <paramref name="x"/> in radians.
        /// Use to get the angle of the tangent of <c>y/x</c>. To compute the value, the method takes into
        /// account the sign of both arguments in order to determine the quadrant.
        ///
        /// Important note: The Y coordinate comes first, by convention.
        /// </summary>
        /// <param name="y">The Y coordinate of the point to find the angle to.</param>
        /// <param name="x">The X coordinate of the point to find the angle to.</param>
        /// <returns>
        /// An angle that would result in the given tangent value. On the range <c>-Tau/2</c> to <c>Tau/2</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x)
        {
            return MathF.Atan2(y, x);
        }

        /// <summary>
        /// Returns the arc tangent of <paramref name="y"/> and <paramref name="x"/> in radians.
        /// Use to get the angle of the tangent of <c>y/x</c>. To compute the value, the method takes into
        /// account the sign of both arguments in order to determine the quadrant.
        ///
        /// Important note: The Y coordinate comes first, by convention.
        /// </summary>
        /// <param name="y">The Y coordinate of the point to find the angle to.</param>
        /// <param name="x">The X coordinate of the point to find the angle to.</param>
        /// <returns>
        /// An angle that would result in the given tangent value. On the range <c>-Tau/2</c> to <c>Tau/2</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Atan2(double y, double x)
        {
            return Math.Atan2(y, x);
        }

        /// <summary>
        /// Cubic interpolates between two values by the factor defined in <paramref name="weight"/>
        /// with pre and post values.
        /// </summary>
        /// <param name="from">The start value for interpolation.</param>
        /// <param name="to">The destination value for interpolation.</param>
        /// <param name="pre">The value which before "from" value for interpolation.</param>
        /// <param name="post">The value which after "to" value for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static float CubicInterpolate(float from, float to, float pre, float post, float weight)
        {
            return 0.5f *
                    ((from * 2.0f) +
                            (-pre + to) * weight +
                            (2.0f * pre - 5.0f * from + 4.0f * to - post) * (weight * weight) +
                            (-pre + 3.0f * from - 3.0f * to + post) * (weight * weight * weight));
        }

        /// <summary>
        /// Cubic interpolates between two values by the factor defined in <paramref name="weight"/>
        /// with pre and post values.
        /// </summary>
        /// <param name="from">The start value for interpolation.</param>
        /// <param name="to">The destination value for interpolation.</param>
        /// <param name="pre">The value which before "from" value for interpolation.</param>
        /// <param name="post">The value which after "to" value for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static double CubicInterpolate(double from, double to, double pre, double post, double weight)
        {
            return 0.5 *
                    ((from * 2.0) +
                            (-pre + to) * weight +
                            (2.0 * pre - 5.0 * from + 4.0 * to - post) * (weight * weight) +
                            (-pre + 3.0 * from - 3.0 * to + post) * (weight * weight * weight));
        }

        /// <summary>
        /// Cubic interpolates between two values by the factor defined in <paramref name="weight"/>
        /// with pre and post values.
        /// It can perform smoother interpolation than
        /// <see cref="CubicInterpolate(float, float, float, float, float)"/>
        /// by the time values.
        /// </summary>
        /// <param name="from">The start value for interpolation.</param>
        /// <param name="to">The destination value for interpolation.</param>
        /// <param name="pre">The value which before "from" value for interpolation.</param>
        /// <param name="post">The value which after "to" value for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <param name="toT"></param>
        /// <param name="preT"></param>
        /// <param name="postT"></param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static float CubicInterpolateInTime(float from, float to, float pre, float post, float weight, float toT, float preT, float postT)
        {
            /* Barry-Goldman method */
            float t = float.Lerp(0.0f, toT, weight);
            float a1 = float.Lerp(pre, from, preT == 0 ? 0.0f : (t - preT) / -preT);
            float a2 = float.Lerp(from, to, toT == 0 ? 0.5f : t / toT);
            float a3 = float.Lerp(to, post, postT - toT == 0 ? 1.0f : (t - toT) / (postT - toT));
            float b1 = float.Lerp(a1, a2, toT - preT == 0 ? 0.0f : (t - preT) / (toT - preT));
            float b2 = float.Lerp(a2, a3, postT == 0 ? 1.0f : t / postT);
            return float.Lerp(b1, b2, toT == 0 ? 0.5f : t / toT);
        }

        /// <summary>
        /// Cubic interpolates between two values by the factor defined in <paramref name="weight"/>
        /// with pre and post values.
        /// It can perform smoother interpolation than
        /// <see cref="CubicInterpolate(double, double, double, double, double)"/>
        /// by the time values.
        /// </summary>
        /// <param name="from">The start value for interpolation.</param>
        /// <param name="to">The destination value for interpolation.</param>
        /// <param name="pre">The value which before "from" value for interpolation.</param>
        /// <param name="post">The value which after "to" value for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <param name="toT"></param>
        /// <param name="preT"></param>
        /// <param name="postT"></param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static double CubicInterpolateInTime(double from, double to, double pre, double post, double weight, double toT, double preT, double postT)
        {
            /* Barry-Goldman method */
            double t = double.Lerp(0.0, toT, weight);
            double a1 = double.Lerp(pre, from, preT == 0 ? 0.0 : (t - preT) / -preT);
            double a2 = double.Lerp(from, to, toT == 0 ? 0.5 : t / toT);
            double a3 = double.Lerp(to, post, postT - toT == 0 ? 1.0 : (t - toT) / (postT - toT));
            double b1 = double.Lerp(a1, a2, toT - preT == 0 ? 0.0 : (t - preT) / (toT - preT));
            double b2 = double.Lerp(a2, a3, postT == 0 ? 1.0 : t / postT);
            return double.Lerp(b1, b2, toT == 0 ? 0.5 : t / toT);
        }

        /// <summary>
        /// Returns the point at the given <paramref name="t"/> on a one-dimensional Bezier curve defined by
        /// the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="start">The start value for the interpolation.</param>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static float BezierInterpolate(float start, float control1, float control2, float end, float t)
        {
            // Formula from Wikipedia article on Bezier curves
            float omt = 1.0f - t;
            float omt2 = omt * omt;
            float omt3 = omt2 * omt;
            float t2 = t * t;
            float t3 = t2 * t;

            return start * omt3 + control1 * omt2 * t * 3.0f + control2 * omt * t2 * 3.0f + end * t3;
        }

        /// <summary>
        /// Returns the point at the given <paramref name="t"/> on a one-dimensional Bezier curve defined by
        /// the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="start">The start value for the interpolation.</param>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static double BezierInterpolate(double start, double control1, double control2, double end, double t)
        {
            // Formula from Wikipedia article on Bezier curves
            double omt = 1.0 - t;
            double omt2 = omt * omt;
            double omt3 = omt2 * omt;
            double t2 = t * t;
            double t3 = t2 * t;

            return start * omt3 + control1 * omt2 * t * 3.0 + control2 * omt * t2 * 3.0 + end * t3;
        }

        /// <summary>
        /// Returns the derivative at the given <paramref name="t"/> on a one dimensional Bezier curve defined by
        /// the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="start">The start value for the interpolation.</param>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static float BezierDerivative(float start, float control1, float control2, float end, float t)
        {
            // Formula from Wikipedia article on Bezier curves
            float omt = 1.0f - t;
            float omt2 = omt * omt;
            float t2 = t * t;

            float d = (control1 - start) * 3.0f * omt2 + (control2 - control1) * 6.0f * omt * t + (end - control2) * 3.0f * t2;
            return d;
        }

        /// <summary>
        /// Returns the derivative at the given <paramref name="t"/> on a one dimensional Bezier curve defined by
        /// the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="start">The start value for the interpolation.</param>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public static double BezierDerivative(double start, double control1, double control2, double end, double t)
        {
            // Formula from Wikipedia article on Bezier curves
            double omt = 1.0 - t;
            double omt2 = omt * omt;
            double t2 = t * t;

            double d = (control1 - start) * 3.0 * omt2 + (control2 - control1) * 6.0 * omt * t + (end - control2) * 3.0 * t2;
            return d;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> are approximately equal
        /// to each other.
        /// The comparison is done using a tolerance calculation with <see cref="Epsilon"/>.
        /// </summary>
        /// <param name="a">One of the values.</param>
        /// <param name="b">The other value.</param>
        /// <returns>A <see langword="bool"/> for whether or not the two values are approximately equal.</returns>
        public static bool IsEqualApprox(float a, float b)
        {
            // Check for exact equality first, required to handle "infinity" values.
            if (a == b)
            {
                return true;
            }
            // Then check for approximate equality.
            float tolerance = EpsilonF * Math.Abs(a);
            if (tolerance < EpsilonF)
            {
                tolerance = EpsilonF;
            }
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="a"/> and <paramref name="b"/> are approximately equal
        /// to each other.
        /// The comparison is done using a tolerance calculation with <see cref="Epsilon"/>.
        /// </summary>
        /// <param name="a">One of the values.</param>
        /// <param name="b">The other value.</param>
        /// <returns>A <see langword="bool"/> for whether or not the two values are approximately equal.</returns>
        public static bool IsEqualApprox(double a, double b)
        {
            // Check for exact equality first, required to handle "infinity" values.
            if (a == b)
            {
                return true;
            }
            // Then check for approximate equality.
            double tolerance = EpsilonD * Math.Abs(a);
            if (tolerance < EpsilonD)
            {
                tolerance = EpsilonD;
            }
            return Math.Abs(a - b) < tolerance;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="s"/> is zero or almost zero.
        /// The comparison is done using a tolerance calculation with <see cref="Epsilon"/>.
        ///
        /// This method is faster than using <see cref="IsEqualApprox(float, float)"/> with
        /// one value as zero.
        /// </summary>
        /// <param name="s">The value to check.</param>
        /// <returns>A <see langword="bool"/> for whether or not the value is nearly zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZeroApprox(float s)
        {
            return Math.Abs(s) < EpsilonF;
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="s"/> is zero or almost zero.
        /// The comparison is done using a tolerance calculation with <see cref="Epsilon"/>.
        ///
        /// This method is faster than using <see cref="IsEqualApprox(double, double)"/> with
        /// one value as zero.
        /// </summary>
        /// <param name="s">The value to check.</param>
        /// <returns>A <see langword="bool"/> for whether or not the value is nearly zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZeroApprox(double s)
        {
            return Math.Abs(s) < EpsilonD;
        }

        /// <summary>
        /// Linearly interpolates between two angles (in radians) by a normalized value.
        ///
        /// Similar to <see cref="Lerp(float, float, float)"/>,
        /// but interpolates correctly when the angles wrap around <see cref="Tau"/>.
        /// </summary>
        /// <param name="from">The start angle for interpolation.</param>
        /// <param name="to">The destination angle for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting angle of the interpolation.</returns>
        public static float LerpAngle(float from, float to, float weight)
        {
            return from + AngleDifference(from, to) * weight;
        }

        /// <summary>
        /// Linearly interpolates between two angles (in radians) by a normalized value.
        ///
        /// Similar to <see cref="Lerp(double, double, double)"/>,
        /// but interpolates correctly when the angles wrap around <see cref="Tau"/>.
        /// </summary>
        /// <param name="from">The start angle for interpolation.</param>
        /// <param name="to">The destination angle for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting angle of the interpolation.</returns>
        public static double LerpAngle(double from, double to, double weight)
        {
            return from + AngleDifference(from, to) * weight;
        }

        /// <summary>
        /// Performs a canonical Modulus operation, where the output is on the range [0, <paramref name="b"/>).
        /// </summary>
        /// <param name="a">The dividend, the primary input.</param>
        /// <param name="b">The divisor. The output is on the range [0, <paramref name="b"/>).</param>
        /// <returns>The resulting output.</returns>
        public static int PosMod(int a, int b)
        {
            int c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))
            {
                c += b;
            }
            return c;
        }

        /// <summary>
        /// Performs a canonical Modulus operation, where the output is on the range [0, <paramref name="b"/>).
        /// </summary>
        /// <param name="a">The dividend, the primary input.</param>
        /// <param name="b">The divisor. The output is on the range [0, <paramref name="b"/>).</param>
        /// <returns>The resulting output.</returns>
        public static float PosMod(float a, float b)
        {
            float c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))
            {
                c += b;
            }
            return c;
        }

        /// <summary>
        /// Performs a canonical Modulus operation, where the output is on the range [0, <paramref name="b"/>).
        /// </summary>
        /// <param name="a">The dividend, the primary input.</param>
        /// <param name="b">The divisor. The output is on the range [0, <paramref name="b"/>).</param>
        /// <returns>The resulting output.</returns>
        public static double PosMod(double a, double b)
        {
            double c = a % b;
            if ((c < 0 && b > 0) || (c > 0 && b < 0))
            {
                c += b;
            }
            return c;
        }

        /// <summary>
        /// Snaps float value <paramref name="s"/> to a given <paramref name="step"/>.
        /// This can also be used to round a floating point number to an arbitrary number of decimals.
        /// </summary>
        /// <param name="s">The value to snap.</param>
        /// <param name="step">The step size to snap to.</param>
        /// <returns>The snapped value.</returns>
        public static float Snapped(float s, float step)
        {
            if (step != 0f)
            {
                return MathF.Floor((s / step) + 0.5f) * step;
            }

            return s;
        }

        /// <summary>
        /// Snaps float value <paramref name="s"/> to a given <paramref name="step"/>.
        /// This can also be used to round a floating point number to an arbitrary number of decimals.
        /// </summary>
        /// <param name="s">The value to snap.</param>
        /// <param name="step">The step size to snap to.</param>
        /// <returns>The snapped value.</returns>
        public static double Snapped(double s, double step)
        {
            if (step != 0f)
            {
                return Math.Floor((s / step) + 0.5f) * step;
            }

            return s;
        }

        /// <summary>
        /// Wraps <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.
        /// Usable for creating loop-alike behavior or infinite surfaces.
        /// If <paramref name="min"/> is <c>0</c>, this is equivalent
        /// to <see cref="PosMod(int, int)"/>, so prefer using that instead.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <param name="min">The minimum allowed value and lower bound of the range.</param>
        /// <param name="max">The maximum allowed value and upper bound of the range.</param>
        /// <returns>The wrapped value.</returns>
        public static int Wrap(int value, int min, int max)
        {
            int range = max - min;
            if (range == 0)
                return min;

            return min + ((((value - min) % range) + range) % range);
        }

        /// <summary>
        /// Wraps <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.
        /// Usable for creating loop-alike behavior or infinite surfaces.
        /// If <paramref name="min"/> is <c>0</c>, this is equivalent
        /// to <see cref="PosMod(float, float)"/>, so prefer using that instead.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <param name="min">The minimum allowed value and lower bound of the range.</param>
        /// <param name="max">The maximum allowed value and upper bound of the range.</param>
        /// <returns>The wrapped value.</returns>
        public static float Wrap(float value, float min, float max)
        {
            float range = max - min;
            if (IsZeroApprox(range))
            {
                return min;
            }
            return min + ((((value - min) % range) + range) % range);
        }

        /// <summary>
        /// Wraps <paramref name="value"/> between <paramref name="min"/> and <paramref name="max"/>.
        /// Usable for creating loop-alike behavior or infinite surfaces.
        /// If <paramref name="min"/> is <c>0</c>, this is equivalent
        /// to <see cref="PosMod(double, double)"/>, so prefer using that instead.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <param name="min">The minimum allowed value and lower bound of the range.</param>
        /// <param name="max">The maximum allowed value and upper bound of the range.</param>
        /// <returns>The wrapped value.</returns>
        public static double Wrap(double value, double min, double max)
        {
            double range = max - min;
            if (IsZeroApprox(range))
            {
                return min;
            }
            return min + ((((value - min) % range) + range) % range);
        }

    }
}
