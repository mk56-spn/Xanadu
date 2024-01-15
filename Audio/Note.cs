// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Audio
{
    public record Note(NoteElement Element, Rid Canvas) : IComparable<Note>
    {
        public Rid Canvas = Canvas;
        public readonly NoteElement Element = Element;
        public bool IsHit;
        public float HitTime = 0;

        public Color GetColour(float time)
        {
            return IsHit == false ? Element.Colour : Element.Colour.Darkened(1 - (time - HitTime));
        }

        public int CompareTo(Note? other)
        {
            return Element.TimingPoint.CompareTo(other!.Element.TimingPoint);
        }
    }
}
