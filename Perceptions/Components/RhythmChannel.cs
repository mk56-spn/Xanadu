// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Composer;

namespace XanaduProject.Perceptions.Components
{
    public class RhythmChannel
    {
        /// <summary>
        /// Which of the three possible lines this corresponds to.
        /// </summary>
        public  RhythmInstance Instance { get; set; }

        /// <summary>
        /// Whether this Rhythm channel is currently in use, for now that means it is currently attached to a <see cref="NoteLink"/>
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// Returns the input associated with the created rhythm channel's instance.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public string GetRhythmInput()
        {
            return Instance switch
            {
                RhythmInstance.RLine => "R1",
                RhythmInstance.BLine => "R1",
                RhythmInstance.YLine => "R3",
                _ => throw new ArgumentOutOfRangeException(nameof(Instance), Instance, null)
            };
        }

        /// <summary>
        /// Returns the colour associated with the created rhythm channel's instance.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Color GetRhythmColour()
        {
            return Instance switch
            {
                RhythmInstance.RLine => Colors.Red,
                RhythmInstance.BLine => Colors.LightBlue,
                RhythmInstance.YLine => Colors.Yellow,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    /// <summary>
    /// The three possible types of rhythm instance.
    /// </summary>
    public enum RhythmInstance
    {
        RLine,
        BLine,
        YLine
    }
}
