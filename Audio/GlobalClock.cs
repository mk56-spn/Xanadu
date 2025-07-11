// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;

namespace XanaduProject.Audio
{
    public static class GlobalClock
    {
        public static IClock Instance
        {
            get
            {
                if (instance == null)
                    throw new InvalidOperationException("The GlobalClock.Instance has not been set. " +
                                                        "Ensure a clock implementation has been initialized and registered itself.");
                return instance;
            }
        }

        private static IClock? instance;

        /// <summary>
        /// Internal method for a clock implementation to register itself.
        /// </summary>
        internal static void Register(IClock clock)
        {

            if (instance != null)
                // This prevents multiple clocks from being active, which could cause confusion.
                throw new InvalidOperationException("An IClock instance is already registered.");
            instance = clock;

        }

        /// <summary>
        /// Internal method for a clock implementation to unregister itself.
        /// </summary>
        internal static void Unregister(IClock clock)
        {
            if (instance == clock) instance = null;
        }
    }
}
