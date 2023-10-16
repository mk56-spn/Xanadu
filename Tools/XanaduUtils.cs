// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Perceptions.Components;
using static XanaduProject.Perceptions.Components.RhythmLine;

namespace XanaduProject.Tools
{
    public static class XanaduUtils
    {
        /// <summary>
        /// Returns the colour associated with this line.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static Color GetLineColour(RhythmLine line)
            => line switch
            {
                RLine => Colors.Red,
                BLine => Colors.LightBlue,
                YLine => Colors.Yellow,
                _ => throw new ArgumentOutOfRangeException(nameof(line), line, null)
            };

        /// <summary>
        /// Returns the input string associated with this <see cref="RhythmLine"/>.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static string GetLineInput(RhythmLine line)
        {
            return line switch
            {
                RLine => "R1",
                BLine => "R2",
                YLine => "R3",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
