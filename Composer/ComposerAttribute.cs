// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

namespace XanaduProject.Composer
{
    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Field)]
        public class ComposerAttribute(string name) : System.Attribute
    {
        public string Name = name;
    }
}
