// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the
// GNU General Public Licence (2.0).  See the LICENCE file for details.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace XanaduProject.GameDependencies
{
    /// <summary>
    /// Tiny DI façade used in game code.
    /// Call <see cref="Register"/> as many times as you like; every call
    /// appends services and rebuilds the provider so the new ones are
    /// resolvable straight away.
    /// </summary>
    public static class DiProvider
    {
        private static readonly ServiceCollection services = new();
        private static IServiceProvider provider = services.BuildServiceProvider();

        /// <summary>
        /// Adds or overrides registrations.
        /// Suitable for plugging in extra services when the level-editor
        /// becomes active.
        /// </summary>
        public static void Register(Action<IServiceCollection> registrations)
        {
            ArgumentNullException.ThrowIfNull(registrations);

            registrations(services);
            provider = services.BuildServiceProvider();
        }

        // kept for back-compat with existing calls
        public static void Configure(Action<IServiceCollection> registrations) => Register(registrations);

        /// <summary>
        /// Resolve a service that has been registered earlier.
        /// </summary>
        public static T Get<T>() where T : notnull => provider.GetRequiredService<T>();

        /// <summary>
        /// Test-only helper – wipe everything so the next test starts clean.
        /// </summary>
        internal static void ResetForTests()
        {
            services.Clear();
            provider = services.BuildServiceProvider();
        }
    }
}
