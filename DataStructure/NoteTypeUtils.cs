// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;
using XanaduProject.Tools;

namespace XanaduProject.DataStructure
{
    public static class NoteTypeUtils
    {
        public static readonly List<(NoteType noteType, Shape2D Shape, StringName text)> ACTION_SHAPES =
        [
            (NoteType.Main, new CapsuleShape2D { Radius = 32, Height = 128 }, "main"),
            (NoteType.R, new CircleShape2D { Radius = 20 }, "R1"),
            (NoteType.C, new CircleShape2D { Radius = 20 }, "C1"),
            (NoteType.L, new CircleShape2D { Radius = 20 }, "L1"),
            (NoteType.R, new CircleShape2D { Radius = 20 }, "R2"),
            (NoteType.C, new CircleShape2D { Radius = 20 }, "C2"),
            (NoteType.L, new CircleShape2D { Radius = 20 }, "L2")
        ];


        public static Color NoteColor(this NoteType note)
        {
            return note switch
            {
                NoteType.Main => XanaduColors.XanaduPink,
                NoteType.R => XanaduColors.XanaduRed,
                NoteType.C => XanaduColors.XanaduYellow,
                NoteType.L => XanaduColors.XanaduGreen,
                _ => throw new ArgumentOutOfRangeException(nameof(note), note, null)
            };
        }
    }
}
