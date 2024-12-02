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

        private bool isHit;
        public bool IsHit
        {
            get => isHit;
            set
            {
                isHit = value;
                RenderingServer.CanvasItemSetModulate(Canvas, value ? Colors.Red : Colors.White );
            }
        }

        public float HitTime = 0;


        public int CompareTo(Note? other)
        {
            return Element.TimingPoint.CompareTo(other!.Element.TimingPoint);
        }
    }
}
