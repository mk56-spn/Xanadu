// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;

namespace XanaduProject.Factories.ShaderFactoryHelpers
{
    public class ShaderCache
    {
        public static ShaderCache Instance { get; } = new();

        private readonly Dictionary<string, ShaderMaterial> cache = new();

        public ShaderMaterial GetOrCreate(string key, Func<ShaderMaterial> builder)
        {
            if (cache.TryGetValue(key, out var material))
            {
                return material;
            }

            var newMaterial = builder();
            cache.Add(key, newMaterial);
            return newMaterial;
        }
    }
}
