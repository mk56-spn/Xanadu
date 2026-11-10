using System;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace Xanadu.Maths;

// Godot 4.4's Vector2/3/4 methods (MIT, LICENSE.txt), moved onto
// System.Numerics' vectors as C# 14 extension members so code written against
// Godot's API keeps working. Members System.Numerics already has (Length,
// Equals, the arithmetic operators, Zero, One) are left to it.

/// <summary>Godot's Vector2 API on <see cref="System.Numerics.Vector2"/>.</summary>
public static class Vector2Godot
{
    extension(Vector2 self)
    {
        /// <summary>
        /// Helper method for deconstruction into a tuple.
        /// </summary>
        public void Deconstruct(out real_t x, out real_t y)
        {
            x = self.X;
            y = self.Y;
        }

        /// <summary>
        /// Returns a new vector with all components in absolute values (i.e. positive).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Abs(real_t)"/> called on each component.</returns>
        public Vector2 Abs()
        {
            return new Vector2(Mathf.Abs(self.X), Mathf.Abs(self.Y));
        }

        /// <summary>
        /// Returns self vector's angle with respect to the self.X axis, or (1, 0) vector, in radians.
        ///
        /// Equivalent to the result of <see cref="Mathf.Atan2(real_t, real_t)"/> when
        /// called with the vector's <see cref="self.Y"/> and <see cref="self.X"/> as parameters: <c>Mathf.Atan2(v.Y, v.X)</c>.
        /// </summary>
        /// <returns>The angle of self vector, in radians.</returns>
        public real_t Angle()
        {
            return Mathf.Atan2(self.Y, self.X);
        }

        /// <summary>
        /// Returns the angle to the given vector, in radians.
        /// </summary>
        /// <param name="to">The other vector to compare self vector to.</param>
        /// <returns>The angle between the two vectors, in radians.</returns>
        public real_t AngleTo(Vector2 to)
        {
            return Mathf.Atan2(self.Cross(to), self.Dot(to));
        }

        /// <summary>
        /// Returns the angle between the line connecting the two points and the self.X axis, in radians.
        /// </summary>
        /// <param name="to">The other vector to compare self vector to.</param>
        /// <returns>The angle between the two vectors, in radians.</returns>
        public real_t AngleToPoint(Vector2 to)
        {
            return Mathf.Atan2(to.Y - self.Y, to.X - self.X);
        }

        /// <summary>
        /// Returns the aspect ratio of self vector, the ratio of <see cref="self.X"/> to <see cref="self.Y"/>.
        /// </summary>
        /// <returns>The <see cref="self.X"/> component divided by the <see cref="self.Y"/> component.</returns>
        public real_t Aspect()
        {
            return self.X / self.Y;
        }

        /// <summary>
        /// Returns the vector "bounced off" from a plane defined by the given normal.
        /// </summary>
        /// <param name="normal">The normal vector defining the plane to bounce off. Must be normalized.</param>
        /// <returns>The bounced vector.</returns>
        public Vector2 Bounce(Vector2 normal)
        {
            return -self.Reflect(normal);
        }

        /// <summary>
        /// Returns a new vector with all components rounded up (towards positive infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Ceil(real_t)"/> called on each component.</returns>
        public Vector2 Ceil()
        {
            return new Vector2(Mathf.Ceil(self.X), Mathf.Ceil(self.Y));
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// components of <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The vector with minimum allowed values.</param>
        /// <param name="max">The vector with maximum allowed values.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector2 Clamp(Vector2 min, Vector2 max)
        {
            return new Vector2
            (
                Mathf.Clamp(self.X, min.X, max.X),
                Mathf.Clamp(self.Y, min.Y, max.Y)
            );
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector2 Clamp(real_t min, real_t max)
        {
            return new Vector2
            (
                Mathf.Clamp(self.X, min, max),
                Mathf.Clamp(self.Y, min, max)
            );
        }

        /// <summary>
        /// Returns the cross product of self vector and <paramref name="with"/>.
        /// </summary>
        /// <param name="with">The other vector.</param>
        /// <returns>The cross product value.</returns>
        public real_t Cross(Vector2 with)
        {
            return (self.X * with.Y) - (self.Y * with.X);
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated vector.</returns>
        public Vector2 CubicInterpolate(Vector2 b, Vector2 preA, Vector2 postB, real_t weight)
        {
            return new Vector2
            (
                Mathf.CubicInterpolate(self.X, b.X, preA.X, postB.X, weight),
                Mathf.CubicInterpolate(self.Y, b.Y, preA.Y, postB.Y, weight)
            );
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// It can perform smoother interpolation than <see cref="CubicInterpolate"/>
        /// by the time values.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <param name="t"></param>
        /// <param name="preAT"></param>
        /// <param name="postBT"></param>
        /// <returns>The interpolated vector.</returns>
        public Vector2 CubicInterpolateInTime(Vector2 b, Vector2 preA, Vector2 postB, real_t weight, real_t t, real_t preAT, real_t postBT)
        {
            return new Vector2
            (
                Mathf.CubicInterpolateInTime(self.X, b.X, preA.X, postB.X, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.Y, b.Y, preA.Y, postB.Y, weight, t, preAT, postBT)
            );
        }

        /// <summary>
        /// Returns the point at the given <paramref name="t"/> on a one-dimensional Bezier curve defined by self vector
        /// and the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination vector.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated vector.</returns>
        public Vector2 BezierInterpolate(Vector2 control1, Vector2 control2, Vector2 end, real_t t)
        {
            return new Vector2
            (
                Mathf.BezierInterpolate(self.X, control1.X, control2.X, end.X, t),
                Mathf.BezierInterpolate(self.Y, control1.Y, control2.Y, end.Y, t)
            );
        }

        /// <summary>
        /// Returns the derivative at the given <paramref name="t"/> on the Bezier curve defined by self vector
        /// and the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public Vector2 BezierDerivative(Vector2 control1, Vector2 control2, Vector2 end, real_t t)
        {
            return new Vector2(
                Mathf.BezierDerivative(self.X, control1.X, control2.X, end.X, t),
                Mathf.BezierDerivative(self.Y, control1.Y, control2.Y, end.Y, t)
            );
        }

        /// <summary>
        /// Returns the normalized vector pointing from self vector to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">The other vector to point towards.</param>
        /// <returns>The direction from self vector to <paramref name="to"/>.</returns>
        public Vector2 DirectionTo(Vector2 to)
        {
            return new Vector2(to.X - self.X, to.Y - self.Y).Normalized();
        }

        /// <summary>
        /// Returns the squared distance between self vector and <paramref name="to"/>.
        /// This method runs faster than <see cref="DistanceTo"/>, so prefer it if
        /// you need to compare vectors or need the squared distance for some formula.
        /// </summary>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The squared distance between the two vectors.</returns>
        public real_t DistanceSquaredTo(Vector2 to)
        {
            return (self.X - to.X) * (self.X - to.X) + (self.Y - to.Y) * (self.Y - to.Y);
        }

        /// <summary>
        /// Returns the distance between self vector and <paramref name="to"/>.
        /// </summary>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The distance between the two vectors.</returns>
        public real_t DistanceTo(Vector2 to)
        {
            return Mathf.Sqrt((self.X - to.X) * (self.X - to.X) + (self.Y - to.Y) * (self.Y - to.Y));
        }

        /// <summary>
        /// Returns the dot product of self vector and <paramref name="with"/>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The dot product of the two vectors.</returns>
        public real_t Dot(Vector2 with)
        {
            return (self.X * with.X) + (self.Y * with.Y);
        }

        /// <summary>
        /// Returns a new vector with all components rounded down (towards negative infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Floor(real_t)"/> called on each component.</returns>
        public Vector2 Floor()
        {
            return new Vector2(Mathf.Floor(self.X), Mathf.Floor(self.Y));
        }

        /// <summary>
        /// Returns the inverse of self vector. This is the same as <c>new Vector2(1 / v.X, 1 / v.Y)</c>.
        /// </summary>
        /// <returns>The inverse of self vector.</returns>
        public Vector2 Inverse()
        {
            return new Vector2(1 / self.X, 1 / self.Y);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector is finite, by calling
        /// <see cref="Mathf.IsFinite(real_t)"/> on each component.
        /// </summary>
        /// <returns>Whether self vector is finite or not.</returns>
        public bool IsFinite()
        {
            return Mathf.IsFinite(self.X) && Mathf.IsFinite(self.Y);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the vector is normalized, and <see langword="false"/> otherwise.
        /// </summary>
        /// <returns>A <see langword="bool"/> indicating whether or not the vector is normalized.</returns>
        public bool IsNormalized()
        {
            return Mathf.Abs(self.LengthSquared() - 1.0f) < Mathf.Epsilon;
        }

        /// <summary>
        /// Returns the result of the linear interpolation between
        /// self vector and <paramref name="to"/> by amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="to">The destination vector for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting vector of the interpolation.</returns>
        public Vector2 Lerp(Vector2 to, real_t weight)
        {
            return new Vector2
            (
                Mathf.Lerp(self.X, to.X, weight),
                Mathf.Lerp(self.Y, to.Y, weight)
            );
        }

        /// <summary>
        /// Returns the vector with a maximum length by limiting its length to <paramref name="length"/>.
        /// </summary>
        /// <param name="length">The length to limit to.</param>
        /// <returns>The vector with its length limited.</returns>
        public Vector2 LimitLength(real_t length = 1.0f)
        {
            Vector2 v = self;
            real_t l = self.Length();

            if (l > 0 && length < l)
            {
                v /= l;
                v *= length;
            }

            return v;
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector2(Mathf.Max(self.X, with.X), Mathf.Max(self.Y, with.Y))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector2 Max(Vector2 with)
        {
            return new Vector2
            (
                Mathf.Max(self.X, with.X),
                Mathf.Max(self.Y, with.Y)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector2(Mathf.Max(self.X, with), Mathf.Max(self.Y, with))</c>.
        /// </summary>
        /// <param name="with">The other value to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector2 Max(real_t with)
        {
            return new Vector2
            (
                Mathf.Max(self.X, with),
                Mathf.Max(self.Y, with)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise minimum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector2(Mathf.Min(self.X, with.X), Mathf.Min(self.Y, with.Y))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting minimum vector.</returns>
        public Vector2 Min(Vector2 with)
        {
            return new Vector2
            (
                Mathf.Min(self.X, with.X),
                Mathf.Min(self.Y, with.Y)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise minimum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector2(Mathf.Min(self.X, with), Mathf.Min(self.Y, with))</c>.
        /// </summary>
        /// <param name="with">The other value to use.</param>
        /// <returns>The resulting minimum vector.</returns>
        public Vector2 Min(real_t with)
        {
            return new Vector2
            (
                Mathf.Min(self.X, with),
                Mathf.Min(self.Y, with)
            );
        }

        /// <summary>
        /// Moves self vector toward <paramref name="to"/> by the fixed <paramref name="delta"/> amount.
        /// </summary>
        /// <param name="to">The vector to move towards.</param>
        /// <param name="delta">The amount to move towards by.</param>
        /// <returns>The resulting vector.</returns>
        public Vector2 MoveToward(Vector2 to, real_t delta)
        {
            Vector2 v = self;
            Vector2 vd = to - v;
            real_t len = vd.Length();
            if (len <= delta || len < Mathf.Epsilon)
                return to;

            return v + (vd / len * delta);
        }

        /// <summary>
        /// Returns the vector scaled to unit length. Equivalent to <c>v / v.Length()</c>.
        /// </summary>
        /// <returns>A normalized version of the vector.</returns>
        public Vector2 Normalized()
        {
            Vector2 v = self;
            v.Normalize();
            return v;
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="mod"/>.
        /// </summary>
        /// <param name="mod">A value representing the divisor of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="mod"/>.
        /// </returns>
        public Vector2 PosMod(real_t mod)
        {
            Vector2 v;
            v.X = Mathf.PosMod(self.X, mod);
            v.Y = Mathf.PosMod(self.Y, mod);
            return v;
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="modv"/>'s components.
        /// </summary>
        /// <param name="modv">A vector representing the divisors of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="modv"/>'s components.
        /// </returns>
        public Vector2 PosMod(Vector2 modv)
        {
            Vector2 v;
            v.X = Mathf.PosMod(self.X, modv.X);
            v.Y = Mathf.PosMod(self.Y, modv.Y);
            return v;
        }

        /// <summary>
        /// Returns a new vector resulting from projecting self vector onto the given vector <paramref name="onNormal"/>.
        /// The resulting new vector is parallel to <paramref name="onNormal"/>.
        /// See also <see cref="self.Slide(Vector2)"/>.
        /// Note: If the vector <paramref name="onNormal"/> is a zero vector, the components of the resulting new vector will be <see cref="real_t.NaN"/>.
        /// </summary>
        /// <param name="onNormal">The vector to project onto.</param>
        /// <returns>The projected vector.</returns>
        public Vector2 Project(Vector2 onNormal)
        {
            return onNormal * (self.Dot(onNormal) / onNormal.LengthSquared());
        }

        /// <summary>
        /// Returns self vector reflected from a plane defined by the given <paramref name="normal"/>.
        /// </summary>
        /// <param name="normal">The normal vector defining the plane to reflect from. Must be normalized.</param>
        /// <returns>The reflected vector.</returns>
        public Vector2 Reflect(Vector2 normal)
        {
#if DEBUG
            if (!normal.IsNormalized())
            {
                throw new ArgumentException("Argument is not normalized.", nameof(normal));
            }
#endif
            return (2 * self.Dot(normal) * normal) - self;
        }

        /// <summary>
        /// Rotates self vector by <paramref name="angle"/> radians.
        /// </summary>
        /// <param name="angle">The angle to rotate by, in radians.</param>
        /// <returns>The rotated vector.</returns>
        public Vector2 Rotated(real_t angle)
        {
            (real_t sin, real_t cos) = Mathf.SinCos(angle);
            return new Vector2
            (
                self.X * cos - self.Y * sin,
                self.X * sin + self.Y * cos
            );
        }

        /// <summary>
        /// Returns self vector with all components rounded to the nearest integer,
        /// with halfway cases rounded towards the nearest multiple of two.
        /// </summary>
        /// <returns>The rounded vector.</returns>
        public Vector2 Round()
        {
            return new Vector2(Mathf.Round(self.X), Mathf.Round(self.Y));
        }

        /// <summary>
        /// Returns a vector with each component set to one or negative one, depending
        /// on the signs of self vector's components, or zero if the component is zero,
        /// by calling <see cref="Mathf.Sign(real_t)"/> on each component.
        /// </summary>
        /// <returns>A vector with all components as either <c>1</c>, <c>-1</c>, or <c>0</c>.</returns>
        public Vector2 Sign()
        {
            Vector2 v;
            v.X = Mathf.Sign(self.X);
            v.Y = Mathf.Sign(self.Y);
            return v;
        }

        /// <summary>
        /// Returns the result of the spherical linear interpolation between
        /// self vector and <paramref name="to"/> by amount <paramref name="weight"/>.
        ///
        /// This method also handles interpolating the lengths if the input vectors
        /// have different lengths. For the special case of one or both input vectors
        /// having zero length, self method behaves like <see cref="self.Lerp(Vector2, real_t)"/>.
        /// </summary>
        /// <param name="to">The destination vector for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting vector of the interpolation.</returns>
        public Vector2 Slerp(Vector2 to, real_t weight)
        {
            real_t startLengthSquared = self.LengthSquared();
            real_t endLengthSquared = to.LengthSquared();
            if (startLengthSquared == 0.0 || endLengthSquared == 0.0)
            {
                // Zero length vectors have no angle, so the best we can do is either lerp or throw an error.
                return self.Lerp(to, weight);
            }
            real_t startLength = Mathf.Sqrt(startLengthSquared);
            real_t resultLength = Mathf.Lerp(startLength, Mathf.Sqrt(endLengthSquared), weight);
            real_t angle = self.AngleTo(to);
            return self.Rotated(angle * weight) * (resultLength / startLength);
        }

        /// <summary>
        /// Returns a new vector resulting from sliding self vector along a line with normal <paramref name="normal"/>.
        /// The resulting new vector is perpendicular to <paramref name="normal"/>, and is equivalent to self vector minus its projection on <paramref name="normal"/>.
        /// See also <see cref="self.Project(Vector2)"/>.
        /// Note: The vector <paramref name="normal"/> must be normalized. See also <see cref="self.Normalized()"/>.
        /// </summary>
        /// <param name="normal">The normal vector of the plane to slide on.</param>
        /// <returns>The slid vector.</returns>
        public Vector2 Slide(Vector2 normal)
        {
            return self - (normal * self.Dot(normal));
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of the corresponding component in <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">A vector value representing the step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector2 Snapped(Vector2 step)
        {
            return new Vector2(Mathf.Snapped(self.X, step.X), Mathf.Snapped(self.Y, step.Y));
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">The step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector2 Snapped(real_t step)
        {
            return new Vector2(Mathf.Snapped(self.X, step), Mathf.Snapped(self.Y, step));
        }

        /// <summary>
        /// Returns a perpendicular vector rotated 90 degrees counter-clockwise
        /// compared to the original, with the same length.
        /// </summary>
        /// <returns>The perpendicular vector.</returns>
        public Vector2 Orthogonal()
        {
            return new Vector2(self.Y, -self.X);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector and <paramref name="other"/> are approximately equal,
        /// by running <see cref="Mathf.IsEqualApprox(real_t, real_t)"/> on each component.
        /// </summary>
        /// <param name="other">The other vector to compare.</param>
        /// <returns>Whether or not the vectors are approximately equal.</returns>
        public bool IsEqualApprox(Vector2 other)
        {
            return Mathf.IsEqualApprox(self.X, other.X) && Mathf.IsEqualApprox(self.Y, other.Y);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector's values are approximately zero,
        /// by running <see cref="Mathf.IsZeroApprox(real_t)"/> on each component.
        /// This method is faster than using <see cref="IsEqualApprox"/> with one value
        /// as a zero vector.
        /// </summary>
        /// <returns>Whether or not the vector is approximately zero.</returns>
        public bool IsZeroApprox()
        {
            return Mathf.IsZeroApprox(self.X) && Mathf.IsZeroApprox(self.Y);
        }

    }

    extension(ref Vector2 self)
    {
        internal void Normalize()
        {
            real_t lengthsq = self.LengthSquared();

            if (lengthsq == 0)
            {
                self.X = self.Y = 0f;
            }
            else
            {
                real_t length = Mathf.Sqrt(lengthsq);
                self.X /= length;
                self.Y /= length;
            }
        }

    }

    extension(Vector2)
    {
        /// <summary>
        /// Infinity vector, a vector with all components set to <see cref="Mathf.Inf"/>.
        /// </summary>
        /// <value>Equivalent to <c>new Vector2(Mathf.Inf, Mathf.Inf)</c>.</value>
        public static Vector2 Inf => new Vector2(Mathf.Inf, Mathf.Inf);

        /// <summary>
        /// Vector2.Up unit vector. Y is down in 2D, so this vector points -Y.
        /// </summary>
        /// <value>Equivalent to <c>new Vector2(0, -1)</c>.</value>
        public static Vector2 Up => new Vector2(0, -1);

        /// <summary>
        /// Vector2.Down unit vector. Y is down in 2D, so this vector points +Y.
        /// </summary>
        /// <value>Equivalent to <c>new Vector2(0, 1)</c>.</value>
        public static Vector2 Down => new Vector2(0, 1);

        /// <summary>
        /// Vector2.Right unit vector. Represents the direction of right.
        /// </summary>
        /// <value>Equivalent to <c>new Vector2(1, 0)</c>.</value>
        public static Vector2 Right => new Vector2(1, 0);

        /// <summary>
        /// Vector2.Left unit vector. Represents the direction of left.
        /// </summary>
        /// <value>Equivalent to <c>new Vector2(-1, 0)</c>.</value>
        public static Vector2 Left => new Vector2(-1, 0);

        /// <summary>
        /// Creates a unit Vector2 rotated to the given angle. This is equivalent to doing
        /// <c>Vector2(Mathf.Cos(angle), Mathf.Sin(angle))</c> or <c>Vector2.Right.Rotated(angle)</c>.
        /// </summary>
        /// <param name="angle">Angle of the vector, in radians.</param>
        /// <returns>The resulting vector.</returns>
        public static Vector2 FromAngle(real_t angle)
        {
            (real_t sin, real_t cos) = Mathf.SinCos(angle);
            return new Vector2(cos, sin);
        }

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector2"/>
        /// with the components of the given <see cref="real_t"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(real_t)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector2(10, -20) % 7); // Prints "(3, -6)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisor">The divisor value.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector2 operator %(Vector2 vec, real_t divisor)
        {
            vec.X %= divisor;
            vec.Y %= divisor;
            return vec;
        }

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector2"/>
        /// with the components of the given <see cref="Vector2"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(Vector2)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector2(10, -20) % new Vector2(7, 8)); // Prints "(3, -4)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisorv">The divisor vector.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector2 operator %(Vector2 vec, Vector2 divisorv)
        {
            vec.X %= divisorv.X;
            vec.Y %= divisorv.Y;
            return vec;
        }

        /// <summary>
        /// Compares two <see cref="Vector2"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than the right.</returns>
        public static bool operator <(Vector2 left, Vector2 right)
        {
            if (left.X == right.X)
            {
                return left.Y < right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector2"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than the right.</returns>
        public static bool operator >(Vector2 left, Vector2 right)
        {
            if (left.X == right.X)
            {
                return left.Y > right.Y;
            }
            return left.X > right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector2"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than or equal to the right.</returns>
        public static bool operator <=(Vector2 left, Vector2 right)
        {
            if (left.X == right.X)
            {
                return left.Y <= right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector2"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than or equal to the right.</returns>
        public static bool operator >=(Vector2 left, Vector2 right)
        {
            if (left.X == right.X)
            {
                return left.Y >= right.Y;
            }
            return left.X > right.X;
        }

    }
}

/// <summary>Godot's Vector3 API on <see cref="System.Numerics.Vector3"/>.</summary>
public static class Vector3Godot
{
    extension(Vector3 self)
    {
        /// <summary>
        /// Rotates this vector around <paramref name="axis"/> (normalized) by
        /// <paramref name="angle"/> radians, as Godot's <c>Basis(axis, angle) * v</c>.
        /// </summary>
        public Vector3 Rotated(Vector3 axis, real_t angle)
        {
            return Vector3.Transform(self, System.Numerics.Quaternion.CreateFromAxisAngle(axis, angle));
        }

        /// <summary>
        /// Helper method for deconstruction into a tuple.
        /// </summary>
        public void Deconstruct(out real_t x, out real_t y, out real_t z)
        {
            x = self.X;
            y = self.Y;
            z = self.Z;
        }

        /// <summary>
        /// Returns a new vector with all components in absolute values (i.e. positive).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Abs(real_t)"/> called on each component.</returns>
        public Vector3 Abs()
        {
            return new Vector3(Mathf.Abs(self.X), Mathf.Abs(self.Y), Mathf.Abs(self.Z));
        }

        /// <summary>
        /// Returns the unsigned minimum angle to the given vector, in radians.
        /// </summary>
        /// <param name="to">The other vector to compare self vector to.</param>
        /// <returns>The unsigned angle between the two vectors, in radians.</returns>
        public real_t AngleTo(Vector3 to)
        {
            return Mathf.Atan2(self.Cross(to).Length(), self.Dot(to));
        }

        /// <summary>
        /// Returns self vector "bounced off" from a plane defined by the given normal.
        /// </summary>
        /// <param name="normal">The normal vector defining the plane to bounce off. Must be normalized.</param>
        /// <returns>The bounced vector.</returns>
        public Vector3 Bounce(Vector3 normal)
        {
            return -self.Reflect(normal);
        }

        /// <summary>
        /// Returns a new vector with all components rounded up (towards positive infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Ceil(real_t)"/> called on each component.</returns>
        public Vector3 Ceil()
        {
            return new Vector3(Mathf.Ceil(self.X), Mathf.Ceil(self.Y), Mathf.Ceil(self.Z));
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// components of <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The vector with minimum allowed values.</param>
        /// <param name="max">The vector with maximum allowed values.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector3 Clamp(Vector3 min, Vector3 max)
        {
            return new Vector3
            (
                Mathf.Clamp(self.X, min.X, max.X),
                Mathf.Clamp(self.Y, min.Y, max.Y),
                Mathf.Clamp(self.Z, min.Z, max.Z)
            );
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector3 Clamp(real_t min, real_t max)
        {
            return new Vector3
            (
                Mathf.Clamp(self.X, min, max),
                Mathf.Clamp(self.Y, min, max),
                Mathf.Clamp(self.Z, min, max)
            );
        }

        /// <summary>
        /// Returns the cross product of self vector and <paramref name="with"/>.
        /// </summary>
        /// <param name="with">The other vector.</param>
        /// <returns>The cross product vector.</returns>
        public Vector3 Cross(Vector3 with)
        {
            return new Vector3
            (
                (self.Y * with.Z) - (self.Z * with.Y),
                (self.Z * with.X) - (self.X * with.Z),
                (self.X * with.Y) - (self.Y * with.X)
            );
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated vector.</returns>
        public Vector3 CubicInterpolate(Vector3 b, Vector3 preA, Vector3 postB, real_t weight)
        {
            return new Vector3
            (
                Mathf.CubicInterpolate(self.X, b.X, preA.X, postB.X, weight),
                Mathf.CubicInterpolate(self.Y, b.Y, preA.Y, postB.Y, weight),
                Mathf.CubicInterpolate(self.Z, b.Z, preA.Z, postB.Z, weight)
            );
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// It can perform smoother interpolation than <see cref="CubicInterpolate"/>
        /// by the time values.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <param name="t"></param>
        /// <param name="preAT"></param>
        /// <param name="postBT"></param>
        /// <returns>The interpolated vector.</returns>
        public Vector3 CubicInterpolateInTime(Vector3 b, Vector3 preA, Vector3 postB, real_t weight, real_t t, real_t preAT, real_t postBT)
        {
            return new Vector3
            (
                Mathf.CubicInterpolateInTime(self.X, b.X, preA.X, postB.X, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.Y, b.Y, preA.Y, postB.Y, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.Z, b.Z, preA.Z, postB.Z, weight, t, preAT, postBT)
            );
        }

        /// <summary>
        /// Returns the point at the given <paramref name="t"/> on a one-dimensional Bezier curve defined by self vector
        /// and the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination vector.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated vector.</returns>
        public Vector3 BezierInterpolate(Vector3 control1, Vector3 control2, Vector3 end, real_t t)
        {
            return new Vector3
            (
                Mathf.BezierInterpolate(self.X, control1.X, control2.X, end.X, t),
                Mathf.BezierInterpolate(self.Y, control1.Y, control2.Y, end.Y, t),
                Mathf.BezierInterpolate(self.Z, control1.Z, control2.Z, end.Z, t)
            );
        }

        /// <summary>
        /// Returns the derivative at the given <paramref name="t"/> on the Bezier curve defined by self vector
        /// and the given <paramref name="control1"/>, <paramref name="control2"/>, and <paramref name="end"/> points.
        /// </summary>
        /// <param name="control1">Control point that defines the bezier curve.</param>
        /// <param name="control2">Control point that defines the bezier curve.</param>
        /// <param name="end">The destination value for the interpolation.</param>
        /// <param name="t">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting value of the interpolation.</returns>
        public Vector3 BezierDerivative(Vector3 control1, Vector3 control2, Vector3 end, real_t t)
        {
            return new Vector3(
                Mathf.BezierDerivative(self.X, control1.X, control2.X, end.X, t),
                Mathf.BezierDerivative(self.Y, control1.Y, control2.Y, end.Y, t),
                Mathf.BezierDerivative(self.Z, control1.Z, control2.Z, end.Z, t)
            );
        }

        /// <summary>
        /// Returns the normalized vector pointing from self vector to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">The other vector to point towards.</param>
        /// <returns>The direction from self vector to <paramref name="to"/>.</returns>
        public Vector3 DirectionTo(Vector3 to)
        {
            return new Vector3(to.X - self.X, to.Y - self.Y, to.Z - self.Z).Normalized();
        }

        /// <summary>
        /// Returns the squared distance between self vector and <paramref name="to"/>.
        /// This method runs faster than <see cref="DistanceTo"/>, so prefer it if
        /// you need to compare vectors or need the squared distance for some formula.
        /// </summary>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The squared distance between the two vectors.</returns>
        public real_t DistanceSquaredTo(Vector3 to)
        {
            return (to - self).LengthSquared();
        }

        /// <summary>
        /// Returns the distance between self vector and <paramref name="to"/>.
        /// </summary>
        /// <seealso cref="self.DistanceSquaredTo(Vector3)"/>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The distance between the two vectors.</returns>
        public real_t DistanceTo(Vector3 to)
        {
            return (to - self).Length();
        }

        /// <summary>
        /// Returns the dot product of self vector and <paramref name="with"/>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The dot product of the two vectors.</returns>
        public real_t Dot(Vector3 with)
        {
            return (self.X * with.X) + (self.Y * with.Y) + (self.Z * with.Z);
        }

        /// <summary>
        /// Returns a new vector with all components rounded down (towards negative infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Floor(real_t)"/> called on each component.</returns>
        public Vector3 Floor()
        {
            return new Vector3(Mathf.Floor(self.X), Mathf.Floor(self.Y), Mathf.Floor(self.Z));
        }

        /// <summary>
        /// Returns the inverse of self vector. This is the same as <c>new Vector3(1 / v.X, 1 / v.Y, 1 / v.Z)</c>.
        /// </summary>
        /// <returns>The inverse of self vector.</returns>
        public Vector3 Inverse()
        {
            return new Vector3(1 / self.X, 1 / self.Y, 1 / self.Z);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector is finite, by calling
        /// <see cref="Mathf.IsFinite(real_t)"/> on each component.
        /// </summary>
        /// <returns>Whether self vector is finite or not.</returns>
        public bool IsFinite()
        {
            return Mathf.IsFinite(self.X) && Mathf.IsFinite(self.Y) && Mathf.IsFinite(self.Z);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the vector is normalized, and <see langword="false"/> otherwise.
        /// </summary>
        /// <returns>A <see langword="bool"/> indicating whether or not the vector is normalized.</returns>
        public bool IsNormalized()
        {
            return Mathf.Abs(self.LengthSquared() - 1.0f) < Mathf.Epsilon;
        }

        /// <summary>
        /// Returns the result of the linear interpolation between
        /// self vector and <paramref name="to"/> by amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="to">The destination vector for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting vector of the interpolation.</returns>
        public Vector3 Lerp(Vector3 to, real_t weight)
        {
            return new Vector3
            (
                Mathf.Lerp(self.X, to.X, weight),
                Mathf.Lerp(self.Y, to.Y, weight),
                Mathf.Lerp(self.Z, to.Z, weight)
            );
        }

        /// <summary>
        /// Returns the vector with a maximum length by limiting its length to <paramref name="length"/>.
        /// </summary>
        /// <param name="length">The length to limit to.</param>
        /// <returns>The vector with its length limited.</returns>
        public Vector3 LimitLength(real_t length = 1.0f)
        {
            Vector3 v = self;
            real_t l = self.Length();

            if (l > 0 && length < l)
            {
                v /= l;
                v *= length;
            }

            return v;
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector3(Mathf.Max(self.X, with.X), Mathf.Max(self.Y, with.Y), Mathf.Max(self.Z, with.Z))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector3 Max(Vector3 with)
        {
            return new Vector3
            (
                Mathf.Max(self.X, with.X),
                Mathf.Max(self.Y, with.Y),
                Mathf.Max(self.Z, with.Z)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector3(Mathf.Max(self.X, with), Mathf.Max(self.Y, with), Mathf.Max(self.Z, with))</c>.
        /// </summary>
        /// <param name="with">The other value to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector3 Max(real_t with)
        {
            return new Vector3
            (
                Mathf.Max(self.X, with),
                Mathf.Max(self.Y, with),
                Mathf.Max(self.Z, with)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise minimum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector3(Mathf.Min(self.X, with.X), Mathf.Min(self.Y, with.Y), Mathf.Min(self.Z, with.Z))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting minimum vector.</returns>
        public Vector3 Min(Vector3 with)
        {
            return new Vector3
            (
                Mathf.Min(self.X, with.X),
                Mathf.Min(self.Y, with.Y),
                Mathf.Min(self.Z, with.Z)
            );
        }

        /// <summary>
        /// Moves self vector toward <paramref name="to"/> by the fixed <paramref name="delta"/> amount.
        /// </summary>
        /// <param name="to">The vector to move towards.</param>
        /// <param name="delta">The amount to move towards by.</param>
        /// <returns>The resulting vector.</returns>
        public Vector3 MoveToward(Vector3 to, real_t delta)
        {
            Vector3 v = self;
            Vector3 vd = to - v;
            real_t len = vd.Length();
            if (len <= delta || len < Mathf.Epsilon)
                return to;

            return v + (vd / len * delta);
        }

        /// <summary>
        /// Returns the vector scaled to unit length. Equivalent to <c>v / v.Length()</c>.
        /// </summary>
        /// <returns>A normalized version of the vector.</returns>
        public Vector3 Normalized()
        {
            Vector3 v = self;
            v.Normalize();
            return v;
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="mod"/>.
        /// </summary>
        /// <param name="mod">A value representing the divisor of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="mod"/>.
        /// </returns>
        public Vector3 PosMod(real_t mod)
        {
            Vector3 v;
            v.X = Mathf.PosMod(self.X, mod);
            v.Y = Mathf.PosMod(self.Y, mod);
            v.Z = Mathf.PosMod(self.Z, mod);
            return v;
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="modv"/>'s components.
        /// </summary>
        /// <param name="modv">A vector representing the divisors of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="modv"/>'s components.
        /// </returns>
        public Vector3 PosMod(Vector3 modv)
        {
            Vector3 v;
            v.X = Mathf.PosMod(self.X, modv.X);
            v.Y = Mathf.PosMod(self.Y, modv.Y);
            v.Z = Mathf.PosMod(self.Z, modv.Z);
            return v;
        }

        /// <summary>
        /// Returns a new vector resulting from projecting self vector onto the given vector <paramref name="onNormal"/>.
        /// The resulting new vector is parallel to <paramref name="onNormal"/>.
        /// See also <see cref="self.Slide(Vector3)"/>.
        /// Note: If the vector <paramref name="onNormal"/> is a zero vector, the components of the resulting new vector will be <see cref="real_t.NaN"/>.
        /// </summary>
        /// <param name="onNormal">The vector to project onto.</param>
        /// <returns>The projected vector.</returns>
        public Vector3 Project(Vector3 onNormal)
        {
            return onNormal * (self.Dot(onNormal) / onNormal.LengthSquared());
        }

        /// <summary>
        /// Returns self vector reflected from a plane defined by the given <paramref name="normal"/>.
        /// </summary>
        /// <param name="normal">The normal vector defining the plane to reflect from. Must be normalized.</param>
        /// <returns>The reflected vector.</returns>
        public Vector3 Reflect(Vector3 normal)
        {
#if DEBUG
            if (!normal.IsNormalized())
            {
                throw new ArgumentException("Argument is not normalized.", nameof(normal));
            }
#endif
            return (2.0f * self.Dot(normal) * normal) - self;
        }

        /// <summary>
        /// Returns self vector with all components rounded to the nearest integer,
        /// with halfway cases rounded towards the nearest multiple of two.
        /// </summary>
        /// <returns>The rounded vector.</returns>
        public Vector3 Round()
        {
            return new Vector3(Mathf.Round(self.X), Mathf.Round(self.Y), Mathf.Round(self.Z));
        }

        /// <summary>
        /// Returns a vector with each component set to one or negative one, depending
        /// on the signs of self vector's components, or zero if the component is zero,
        /// by calling <see cref="Mathf.Sign(real_t)"/> on each component.
        /// </summary>
        /// <returns>A vector with all components as either <c>1</c>, <c>-1</c>, or <c>0</c>.</returns>
        public Vector3 Sign()
        {
            Vector3 v;
            v.X = Mathf.Sign(self.X);
            v.Y = Mathf.Sign(self.Y);
            v.Z = Mathf.Sign(self.Z);
            return v;
        }

        /// <summary>
        /// Returns the signed angle to the given vector, in radians.
        /// The sign of the angle is positive in a counter-clockwise
        /// direction and negative in a clockwise direction when viewed
        /// from the side specified by the <paramref name="axis"/>.
        /// </summary>
        /// <param name="to">The other vector to compare self vector to.</param>
        /// <param name="axis">The reference axis to use for the angle sign.</param>
        /// <returns>The signed angle between the two vectors, in radians.</returns>
        public real_t SignedAngleTo(Vector3 to, Vector3 axis)
        {
            Vector3 crossTo = self.Cross(to);
            real_t unsignedAngle = Mathf.Atan2(crossTo.Length(), self.Dot(to));
            real_t sign = crossTo.Dot(axis);
            return (sign < 0) ? -unsignedAngle : unsignedAngle;
        }

        /// <summary>
        /// Returns the result of the spherical linear interpolation between
        /// self vector and <paramref name="to"/> by amount <paramref name="weight"/>.
        ///
        /// This method also handles interpolating the lengths if the input vectors
        /// have different lengths. For the special case of one or both input vectors
        /// having zero length, self method behaves like <see cref="self.Lerp(Vector3, real_t)"/>.
        /// </summary>
        /// <param name="to">The destination vector for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting vector of the interpolation.</returns>
        public Vector3 Slerp(Vector3 to, real_t weight)
        {
            real_t startLengthSquared = self.LengthSquared();
            real_t endLengthSquared = to.LengthSquared();
            if (startLengthSquared == 0.0 || endLengthSquared == 0.0)
            {
                // Zero length vectors have no angle, so the best we can do is either lerp or throw an error.
                return self.Lerp(to, weight);
            }
            Vector3 axis = self.Cross(to);
            real_t axisLengthSquared = axis.LengthSquared();
            if (axisLengthSquared == 0.0)
            {
                // Colinear vectors have no rotation axis or angle between them, so the best we can do is lerp.
                return self.Lerp(to, weight);
            }
            axis /= Mathf.Sqrt(axisLengthSquared);
            real_t startLength = Mathf.Sqrt(startLengthSquared);
            real_t resultLength = Mathf.Lerp(startLength, Mathf.Sqrt(endLengthSquared), weight);
            real_t angle = self.AngleTo(to);
            return self.Rotated(axis, angle * weight) * (resultLength / startLength);
        }

        /// <summary>
        /// Returns a new vector resulting from sliding self vector along a plane with normal <paramref name="normal"/>.
        /// The resulting new vector is perpendicular to <paramref name="normal"/>, and is equivalent to self vector minus its projection on <paramref name="normal"/>.
        /// See also <see cref="self.Project(Vector3)"/>.
        /// Note: The vector <paramref name="normal"/> must be normalized. See also <see cref="self.Normalized()"/>.
        /// </summary>
        /// <param name="normal">The normal vector of the plane to slide on.</param>
        /// <returns>The slid vector.</returns>
        public Vector3 Slide(Vector3 normal)
        {
            return self - (normal * self.Dot(normal));
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of the corresponding component in <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">A vector value representing the step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector3 Snapped(Vector3 step)
        {
            return new Vector3
            (
                Mathf.Snapped(self.X, step.X),
                Mathf.Snapped(self.Y, step.Y),
                Mathf.Snapped(self.Z, step.Z)
            );
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">The step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector3 Snapped(real_t step)
        {
            return new Vector3
            (
                Mathf.Snapped(self.X, step),
                Mathf.Snapped(self.Y, step),
                Mathf.Snapped(self.Z, step)
            );
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector and <paramref name="other"/> are approximately equal,
        /// by running <see cref="Mathf.IsEqualApprox(real_t, real_t)"/> on each component.
        /// </summary>
        /// <param name="other">The other vector to compare.</param>
        /// <returns>Whether or not the vectors are approximately equal.</returns>
        public bool IsEqualApprox(Vector3 other)
        {
            return Mathf.IsEqualApprox(self.X, other.X) && Mathf.IsEqualApprox(self.Y, other.Y) && Mathf.IsEqualApprox(self.Z, other.Z);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector's values are approximately zero,
        /// by running <see cref="Mathf.IsZeroApprox(real_t)"/> on each component.
        /// This method is faster than using <see cref="IsEqualApprox"/> with one value
        /// as a zero vector.
        /// </summary>
        /// <returns>Whether or not the vector is approximately zero.</returns>
        public bool IsZeroApprox()
        {
            return Mathf.IsZeroApprox(self.X) && Mathf.IsZeroApprox(self.Y) && Mathf.IsZeroApprox(self.Z);
        }

    }

    extension(ref Vector3 self)
    {
        internal void Normalize()
        {
            real_t lengthsq = self.LengthSquared();

            if (lengthsq == 0)
            {
                self.X = self.Y = self.Z = 0f;
            }
            else
            {
                real_t length = Mathf.Sqrt(lengthsq);
                self.X /= length;
                self.Y /= length;
                self.Z /= length;
            }
        }

    }

    extension(Vector3)
    {
        /// <summary>
        /// Infinity vector, a vector with all components set to <see cref="Mathf.Inf"/>.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(Mathf.Inf, Mathf.Inf, Mathf.Inf)</c>.</value>
        public static Vector3 Inf => new Vector3(Mathf.Inf, Mathf.Inf, Mathf.Inf);

        /// <summary>
        /// Vector3.Up unit vector.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(0, 1, 0)</c>.</value>
        public static Vector3 Up => new Vector3(0, 1, 0);

        /// <summary>
        /// Vector3.Down unit vector.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(0, -1, 0)</c>.</value>
        public static Vector3 Down => new Vector3(0, -1, 0);

        /// <summary>
        /// Vector3.Right unit vector. Represents the local direction of right,
        /// and the global direction of east.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(1, 0, 0)</c>.</value>
        public static Vector3 Right => new Vector3(1, 0, 0);

        /// <summary>
        /// Vector3.Left unit vector. Represents the local direction of left,
        /// and the global direction of west.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(-1, 0, 0)</c>.</value>
        public static Vector3 Left => new Vector3(-1, 0, 0);

        /// <summary>
        /// Vector3.Forward unit vector. Represents the local direction of forward,
        /// and the global direction of north.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(0, 0, -1)</c>.</value>
        public static Vector3 Forward => new Vector3(0, 0, -1);

        /// <summary>
        /// Vector3.Back unit vector. Represents the local direction of back,
        /// and the global direction of south.
        /// </summary>
        /// <value>Equivalent to <c>new Vector3(0, 0, 1)</c>.</value>
        public static Vector3 Back => new Vector3(0, 0, 1);

        /// <summary>
        /// Unit vector pointing towards the left side of imported 3D assets.
        /// </summary>
        public static Vector3 ModelLeft => new Vector3(1, 0, 0);

        /// <summary>
        /// Unit vector pointing towards the right side of imported 3D assets.
        /// </summary>
        public static Vector3 ModelRight => new Vector3(-1, 0, 0);

        /// <summary>
        /// Unit vector pointing towards the top side (up) of imported 3D assets.
        /// </summary>
        public static Vector3 ModelTop => new Vector3(0, 1, 0);

        /// <summary>
        /// Unit vector pointing towards the bottom side (down) of imported 3D assets.
        /// </summary>
        public static Vector3 ModelBottom => new Vector3(0, -1, 0);

        /// <summary>
        /// Unit vector pointing towards the front side (facing forward) of imported 3D assets.
        /// </summary>
        public static Vector3 ModelFront => new Vector3(0, 0, 1);

        /// <summary>
        /// Unit vector pointing towards the rear side (back) of imported 3D assets.
        /// </summary>
        public static Vector3 ModelRear => new Vector3(0, 0, -1);

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector3"/>
        /// with the components of the given <see cref="real_t"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(real_t)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector3(10, -20, 30) % 7); // Prints "(3, -6, 2)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisor">The divisor value.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector3 operator %(Vector3 vec, real_t divisor)
        {
            vec.X %= divisor;
            vec.Y %= divisor;
            vec.Z %= divisor;
            return vec;
        }

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector3"/>
        /// with the components of the given <see cref="Vector3"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(Vector3)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector3(10, -20, 30) % new Vector3(7, 8, 9)); // Prints "(3, -4, 3)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisorv">The divisor vector.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector3 operator %(Vector3 vec, Vector3 divisorv)
        {
            vec.X %= divisorv.X;
            vec.Y %= divisorv.Y;
            vec.Z %= divisorv.Z;
            return vec;
        }

        /// <summary>
        /// Compares two <see cref="Vector3"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors, and then with the Z values.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than the right.</returns>
        public static bool operator <(Vector3 left, Vector3 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    return left.Z < right.Z;
                }
                return left.Y < right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector3"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors, and then with the Z values.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than the right.</returns>
        public static bool operator >(Vector3 left, Vector3 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    return left.Z > right.Z;
                }
                return left.Y > right.Y;
            }
            return left.X > right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector3"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors, and then with the Z values.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than or equal to the right.</returns>
        public static bool operator <=(Vector3 left, Vector3 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    return left.Z <= right.Z;
                }
                return left.Y < right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector3"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y values of the two vectors, and then with the Z values.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than or equal to the right.</returns>
        public static bool operator >=(Vector3 left, Vector3 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    return left.Z >= right.Z;
                }
                return left.Y > right.Y;
            }
            return left.X > right.X;
        }

    }
}

/// <summary>Godot's Vector4 API on <see cref="System.Numerics.Vector4"/>.</summary>
public static class Vector4Godot
{
    extension(Vector4 self)
    {
        /// <summary>
        /// Helper method for deconstruction into a tuple.
        /// </summary>
        public void Deconstruct(out real_t x, out real_t y, out real_t z, out real_t w)
        {
            x = self.X;
            y = self.Y;
            z = self.Z;
            w = self.W;
        }

        /// <summary>
        /// Returns a new vector with all components in absolute values (i.e. positive).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Abs(real_t)"/> called on each component.</returns>
        public Vector4 Abs()
        {
            return new Vector4(Mathf.Abs(self.X), Mathf.Abs(self.Y), Mathf.Abs(self.Z), Mathf.Abs(self.W));
        }

        /// <summary>
        /// Returns a new vector with all components rounded up (towards positive infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Ceil(real_t)"/> called on each component.</returns>
        public Vector4 Ceil()
        {
            return new Vector4(Mathf.Ceil(self.X), Mathf.Ceil(self.Y), Mathf.Ceil(self.Z), Mathf.Ceil(self.W));
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// components of <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The vector with minimum allowed values.</param>
        /// <param name="max">The vector with maximum allowed values.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector4 Clamp(Vector4 min, Vector4 max)
        {
            return new Vector4
            (
                Mathf.Clamp(self.X, min.X, max.X),
                Mathf.Clamp(self.Y, min.Y, max.Y),
                Mathf.Clamp(self.Z, min.Z, max.Z),
                Mathf.Clamp(self.W, min.W, max.W)
            );
        }

        /// <summary>
        /// Returns a new vector with all components clamped between the
        /// <paramref name="min"/> and <paramref name="max"/> using
        /// <see cref="Mathf.Clamp(real_t, real_t, real_t)"/>.
        /// </summary>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>The vector with all components clamped.</returns>
        public Vector4 Clamp(real_t min, real_t max)
        {
            return new Vector4
            (
                Mathf.Clamp(self.X, min, max),
                Mathf.Clamp(self.Y, min, max),
                Mathf.Clamp(self.Z, min, max),
                Mathf.Clamp(self.W, min, max)
            );
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The interpolated vector.</returns>
        public Vector4 CubicInterpolate(Vector4 b, Vector4 preA, Vector4 postB, real_t weight)
        {
            return new Vector4
            (
                Mathf.CubicInterpolate(self.X, b.X, preA.X, postB.X, weight),
                Mathf.CubicInterpolate(self.Y, b.Y, preA.Y, postB.Y, weight),
                Mathf.CubicInterpolate(self.Z, b.Z, preA.Z, postB.Z, weight),
                Mathf.CubicInterpolate(self.W, b.W, preA.W, postB.W, weight)
            );
        }

        /// <summary>
        /// Performs a cubic interpolation between vectors <paramref name="preA"/>, self vector,
        /// <paramref name="b"/>, and <paramref name="postB"/>, by the given amount <paramref name="weight"/>.
        /// It can perform smoother interpolation than <see cref="CubicInterpolate"/>
        /// by the time values.
        /// </summary>
        /// <param name="b">The destination vector.</param>
        /// <param name="preA">A vector before self vector.</param>
        /// <param name="postB">A vector after <paramref name="b"/>.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <param name="t"></param>
        /// <param name="preAT"></param>
        /// <param name="postBT"></param>
        /// <returns>The interpolated vector.</returns>
        public Vector4 CubicInterpolateInTime(Vector4 b, Vector4 preA, Vector4 postB, real_t weight, real_t t, real_t preAT, real_t postBT)
        {
            return new Vector4
            (
                Mathf.CubicInterpolateInTime(self.X, b.X, preA.X, postB.X, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.Y, b.Y, preA.Y, postB.Y, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.Z, b.Z, preA.Z, postB.Z, weight, t, preAT, postBT),
                Mathf.CubicInterpolateInTime(self.W, b.W, preA.W, postB.W, weight, t, preAT, postBT)
            );
        }

        /// <summary>
        /// Returns the normalized vector pointing from self vector to <paramref name="to"/>.
        /// </summary>
        /// <param name="to">The other vector to point towards.</param>
        /// <returns>The direction from self vector to <paramref name="to"/>.</returns>
        public Vector4 DirectionTo(Vector4 to)
        {
            Vector4 ret = new Vector4(to.X - self.X, to.Y - self.Y, to.Z - self.Z, to.W - self.W);
            ret.Normalize();
            return ret;
        }

        /// <summary>
        /// Returns the squared distance between self vector and <paramref name="to"/>.
        /// This method runs faster than <see cref="DistanceTo"/>, so prefer it if
        /// you need to compare vectors or need the squared distance for some formula.
        /// </summary>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The squared distance between the two vectors.</returns>
        public real_t DistanceSquaredTo(Vector4 to)
        {
            return (to - self).LengthSquared();
        }

        /// <summary>
        /// Returns the distance between self vector and <paramref name="to"/>.
        /// </summary>
        /// <param name="to">The other vector to use.</param>
        /// <returns>The distance between the two vectors.</returns>
        public real_t DistanceTo(Vector4 to)
        {
            return (to - self).Length();
        }

        /// <summary>
        /// Returns the dot product of self vector and <paramref name="with"/>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The dot product of the two vectors.</returns>
        public real_t Dot(Vector4 with)
        {
            return (self.X * with.X) + (self.Y * with.Y) + (self.Z * with.Z) + (self.W * with.W);
        }

        /// <summary>
        /// Returns a new vector with all components rounded down (towards negative infinity).
        /// </summary>
        /// <returns>A vector with <see cref="Mathf.Floor(real_t)"/> called on each component.</returns>
        public Vector4 Floor()
        {
            return new Vector4(Mathf.Floor(self.X), Mathf.Floor(self.Y), Mathf.Floor(self.Z), Mathf.Floor(self.W));
        }

        /// <summary>
        /// Returns the inverse of self vector. This is the same as <c>new Vector4(1 / v.X, 1 / v.Y, 1 / v.Z, 1 / v.W)</c>.
        /// </summary>
        /// <returns>The inverse of self vector.</returns>
        public Vector4 Inverse()
        {
            return new Vector4(1 / self.X, 1 / self.Y, 1 / self.Z, 1 / self.W);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector is finite, by calling
        /// <see cref="Mathf.IsFinite(real_t)"/> on each component.
        /// </summary>
        /// <returns>Whether self vector is finite or not.</returns>
        public bool IsFinite()
        {
            return Mathf.IsFinite(self.X) && Mathf.IsFinite(self.Y) && Mathf.IsFinite(self.Z) && Mathf.IsFinite(self.W);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the vector is normalized, and <see langword="false"/> otherwise.
        /// </summary>
        /// <returns>A <see langword="bool"/> indicating whether or not the vector is normalized.</returns>
        public bool IsNormalized()
        {
            return Mathf.Abs(self.LengthSquared() - 1.0f) < Mathf.Epsilon;
        }

        /// <summary>
        /// Returns the result of the linear interpolation between
        /// self vector and <paramref name="to"/> by amount <paramref name="weight"/>.
        /// </summary>
        /// <param name="to">The destination vector for interpolation.</param>
        /// <param name="weight">A value on the range of 0.0 to 1.0, representing the amount of interpolation.</param>
        /// <returns>The resulting vector of the interpolation.</returns>
        public Vector4 Lerp(Vector4 to, real_t weight)
        {
            return new Vector4
            (
                Mathf.Lerp(self.X, to.X, weight),
                Mathf.Lerp(self.Y, to.Y, weight),
                Mathf.Lerp(self.Z, to.Z, weight),
                Mathf.Lerp(self.W, to.W, weight)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector4(Mathf.Max(self.X, with.X), Mathf.Max(self.Y, with.Y), Mathf.Max(self.Z, with.Z), Mathf.Max(self.W, with.W))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector4 Max(Vector4 with)
        {
            return new Vector4
            (
                Mathf.Max(self.X, with.X),
                Mathf.Max(self.Y, with.Y),
                Mathf.Max(self.Z, with.Z),
                Mathf.Max(self.W, with.W)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise maximum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector4(Mathf.Max(self.X, with), Mathf.Max(self.Y, with), Mathf.Max(self.Z, with), Mathf.Max(self.W, with))</c>.
        /// </summary>
        /// <param name="with">The other value to use.</param>
        /// <returns>The resulting maximum vector.</returns>
        public Vector4 Max(real_t with)
        {
            return new Vector4
            (
                Mathf.Max(self.X, with),
                Mathf.Max(self.Y, with),
                Mathf.Max(self.Z, with),
                Mathf.Max(self.W, with)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise minimum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector4(Mathf.Min(self.X, with.X), Mathf.Min(self.Y, with.Y), Mathf.Min(self.Z, with.Z), Mathf.Min(self.W, with.W))</c>.
        /// </summary>
        /// <param name="with">The other vector to use.</param>
        /// <returns>The resulting minimum vector.</returns>
        public Vector4 Min(Vector4 with)
        {
            return new Vector4
            (
                Mathf.Min(self.X, with.X),
                Mathf.Min(self.Y, with.Y),
                Mathf.Min(self.Z, with.Z),
                Mathf.Min(self.W, with.W)
            );
        }

        /// <summary>
        /// Returns the result of the component-wise minimum between
        /// self vector and <paramref name="with"/>.
        /// Equivalent to <c>new Vector4(Mathf.Min(self.X, with), Mathf.Min(self.Y, with), Mathf.Min(self.Z, with), Mathf.Min(self.W, with))</c>.
        /// </summary>
        /// <param name="with">The other value to use.</param>
        /// <returns>The resulting minimum vector.</returns>
        public Vector4 Min(real_t with)
        {
            return new Vector4
            (
                Mathf.Min(self.X, with),
                Mathf.Min(self.Y, with),
                Mathf.Min(self.Z, with),
                Mathf.Min(self.W, with)
            );
        }

        /// <summary>
        /// Returns the vector scaled to unit length. Equivalent to <c>v / v.Length()</c>.
        /// </summary>
        /// <returns>A normalized version of the vector.</returns>
        public Vector4 Normalized()
        {
            Vector4 v = self;
            v.Normalize();
            return v;
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="mod"/>.
        /// </summary>
        /// <param name="mod">A value representing the divisor of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="mod"/>.
        /// </returns>
        public Vector4 PosMod(real_t mod)
        {
            return new Vector4(
                Mathf.PosMod(self.X, mod),
                Mathf.PosMod(self.Y, mod),
                Mathf.PosMod(self.Z, mod),
                Mathf.PosMod(self.W, mod)
            );
        }

        /// <summary>
        /// Returns a vector composed of the <see cref="Mathf.PosMod(real_t, real_t)"/> of self vector's components
        /// and <paramref name="modv"/>'s components.
        /// </summary>
        /// <param name="modv">A vector representing the divisors of the operation.</param>
        /// <returns>
        /// A vector with each component <see cref="Mathf.PosMod(real_t, real_t)"/> by <paramref name="modv"/>'s components.
        /// </returns>
        public Vector4 PosMod(Vector4 modv)
        {
            return new Vector4(
                Mathf.PosMod(self.X, modv.X),
                Mathf.PosMod(self.Y, modv.Y),
                Mathf.PosMod(self.Z, modv.Z),
                Mathf.PosMod(self.W, modv.W)
            );
        }

        /// <summary>
        /// Returns self vector with all components rounded to the nearest integer,
        /// with halfway cases rounded towards the nearest multiple of two.
        /// </summary>
        /// <returns>The rounded vector.</returns>
        public Vector4 Round()
        {
            return new Vector4(Mathf.Round(self.X), Mathf.Round(self.Y), Mathf.Round(self.Z), Mathf.Round(self.W));
        }

        /// <summary>
        /// Returns a vector with each component set to one or negative one, depending
        /// on the signs of self vector's components, or zero if the component is zero,
        /// by calling <see cref="Mathf.Sign(real_t)"/> on each component.
        /// </summary>
        /// <returns>A vector with all components as either <c>1</c>, <c>-1</c>, or <c>0</c>.</returns>
        public Vector4 Sign()
        {
            Vector4 v;
            v.X = Mathf.Sign(self.X);
            v.Y = Mathf.Sign(self.Y);
            v.Z = Mathf.Sign(self.Z);
            v.W = Mathf.Sign(self.W);
            return v;
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of the corresponding component in <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">A vector value representing the step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector4 Snapped(Vector4 step)
        {
            return new Vector4(
                Mathf.Snapped(self.X, step.X),
                Mathf.Snapped(self.Y, step.Y),
                Mathf.Snapped(self.Z, step.Z),
                Mathf.Snapped(self.W, step.W)
            );
        }

        /// <summary>
        /// Returns a new vector with each component snapped to the nearest multiple of <paramref name="step"/>.
        /// This can also be used to round to an arbitrary number of decimals.
        /// </summary>
        /// <param name="step">The step size to snap to.</param>
        /// <returns>The snapped vector.</returns>
        public Vector4 Snapped(real_t step)
        {
            return new Vector4(
                Mathf.Snapped(self.X, step),
                Mathf.Snapped(self.Y, step),
                Mathf.Snapped(self.Z, step),
                Mathf.Snapped(self.W, step)
            );
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector and <paramref name="other"/> are approximately equal,
        /// by running <see cref="Mathf.IsEqualApprox(real_t, real_t)"/> on each component.
        /// </summary>
        /// <param name="other">The other vector to compare.</param>
        /// <returns>Whether or not the vectors are approximately equal.</returns>
        public bool IsEqualApprox(Vector4 other)
        {
            return Mathf.IsEqualApprox(self.X, other.X) && Mathf.IsEqualApprox(self.Y, other.Y) && Mathf.IsEqualApprox(self.Z, other.Z) && Mathf.IsEqualApprox(self.W, other.W);
        }

        /// <summary>
        /// Returns <see langword="true"/> if self vector's values are approximately zero,
        /// by running <see cref="Mathf.IsZeroApprox(real_t)"/> on each component.
        /// This method is faster than using <see cref="IsEqualApprox"/> with one value
        /// as a zero vector.
        /// </summary>
        /// <returns>Whether or not the vector is approximately zero.</returns>
        public bool IsZeroApprox()
        {
            return Mathf.IsZeroApprox(self.X) && Mathf.IsZeroApprox(self.Y) && Mathf.IsZeroApprox(self.Z) && Mathf.IsZeroApprox(self.W);
        }

    }

    extension(ref Vector4 self)
    {
        internal void Normalize()
        {
            real_t lengthsq = self.LengthSquared();

            if (lengthsq == 0)
            {
                self.X = self.Y = self.Z = self.W = 0f;
            }
            else
            {
                real_t length = Mathf.Sqrt(lengthsq);
                self.X /= length;
                self.Y /= length;
                self.Z /= length;
                self.W /= length;
            }
        }

    }

    extension(Vector4)
    {
        /// <summary>
        /// Infinity vector, a vector with all components set to <see cref="Mathf.Inf"/>.
        /// </summary>
        /// <value>Equivalent to <c>new Vector4(Mathf.Inf, Mathf.Inf, Mathf.Inf, Mathf.Inf)</c>.</value>
        public static Vector4 Inf => new Vector4(Mathf.Inf, Mathf.Inf, Mathf.Inf, Mathf.Inf);

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector4"/>
        /// with the components of the given <see cref="real_t"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(real_t)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector4(10, -20, 30, 40) % 7); // Prints "(3, -6, 2, 5)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisor">The divisor value.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector4 operator %(Vector4 vec, real_t divisor)
        {
            vec.X %= divisor;
            vec.Y %= divisor;
            vec.Z %= divisor;
            vec.W %= divisor;
            return vec;
        }

        /// <summary>
        /// Gets the remainder of each component of the <see cref="Vector4"/>
        /// with the components of the given <see cref="Vector4"/>.
        /// This operation uses truncated division, which is often not desired
        /// as it does not work well with negative numbers.
        /// Consider using <see cref="PosMod(Vector4)"/> instead
        /// if you want to handle negative numbers.
        /// </summary>
        /// <example>
        /// <code>
        /// GD.Print(new Vector4(10, -20, 30, 10) % new Vector4(7, 8, 9, 10)); // Prints "(3, -4, 3, 0)"
        /// </code>
        /// </example>
        /// <param name="vec">The dividend vector.</param>
        /// <param name="divisorv">The divisor vector.</param>
        /// <returns>The remainder vector.</returns>
        public static Vector4 operator %(Vector4 vec, Vector4 divisorv)
        {
            vec.X %= divisorv.X;
            vec.Y %= divisorv.Y;
            vec.Z %= divisorv.Z;
            vec.W %= divisorv.W;
            return vec;
        }

        /// <summary>
        /// Compares two <see cref="Vector4"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y, Z and finally W values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than the right.</returns>
        public static bool operator <(Vector4 left, Vector4 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    if (left.Z == right.Z)
                    {
                        return left.W < right.W;
                    }
                    return left.Z < right.Z;
                }
                return left.Y < right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector4"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y, Z and finally W values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than the right.</returns>
        public static bool operator >(Vector4 left, Vector4 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    if (left.Z == right.Z)
                    {
                        return left.W > right.W;
                    }
                    return left.Z > right.Z;
                }
                return left.Y > right.Y;
            }
            return left.X > right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector4"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is less than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y, Z and finally W values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is less than or equal to the right.</returns>
        public static bool operator <=(Vector4 left, Vector4 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    if (left.Z == right.Z)
                    {
                        return left.W <= right.W;
                    }
                    return left.Z < right.Z;
                }
                return left.Y < right.Y;
            }
            return left.X < right.X;
        }

        /// <summary>
        /// Compares two <see cref="Vector4"/> vectors by first checking if
        /// the X value of the <paramref name="left"/> vector is greater than
        /// or equal to the X value of the <paramref name="right"/> vector.
        /// If the X values are exactly equal, then it repeats this check
        /// with the Y, Z and finally W values of the two vectors.
        /// This operator is useful for sorting vectors.
        /// </summary>
        /// <param name="left">The left vector.</param>
        /// <param name="right">The right vector.</param>
        /// <returns>Whether or not the left is greater than or equal to the right.</returns>
        public static bool operator >=(Vector4 left, Vector4 right)
        {
            if (left.X == right.X)
            {
                if (left.Y == right.Y)
                {
                    if (left.Z == right.Z)
                    {
                        return left.W >= right.W;
                    }
                    return left.Z > right.Z;
                }
                return left.Y > right.Y;
            }
            return left.X > right.X;
        }

    }
}
