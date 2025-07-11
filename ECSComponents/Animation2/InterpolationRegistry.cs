// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;

namespace XanaduProject.ECSComponents.Animation2
{
    public static class InterpolationRegistry
    {
        private static readonly Dictionary<Type, Delegate> interpolators = new();

        static InterpolationRegistry()
        {
            // Just a quick example for Vector2
            register<Vector2>((from, to, factor) => from.Lerp(to, factor));
            register<Color>((from, to, factor) => from.Lerp(to, factor));
        }
        /// <summary>
        /// Register a Func that interpolates between two objects of type T.
        /// </summary>
        private static void register<T>(Func<T, T, float, T> interpolator)=>
            interpolators[typeof(T)] = interpolator;

        /// <summary>
        /// Retrieves the interpolator for the given type.
        /// </summary>
        public static Func<T, T, float, T> Get<T>() =>
            (Func<T, T, float, T>)interpolators[typeof(T)];
    }

    public static class InterpolatorCache<T>
    {
        public static readonly Func<T, T, float, T> LERP = InterpolationRegistry.Get<T>();
    }
}
