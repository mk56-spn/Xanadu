// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;

namespace XanaduProject.Composer
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
    public class ComposerAttribute(string name) : Attribute
    {
        public string Name = name;
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
    public class SliderAttribute(string name, int min, int max) : Attribute
    {
        public string Name = name;
    }
}
