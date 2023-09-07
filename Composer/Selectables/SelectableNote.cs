// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer.Notes;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectableNote : SelectableHandle
    {
        protected override Color HighlightColor => Colors.Blue;

        public SelectableNote ()
        {
            Radius = 31;
            OnPositionChanged += () => GetParent<Note>().GlobalPosition = GlobalPosition;
        }

        public override void _Draw()
        {
            base._Draw();
            DrawCircle(Position, Radius + 1, Colors.White);
            DrawArc
            (
                Position,
                Radius,
                Mathf.DegToRad(0),
                Mathf.DegToRad(360),
                (int)(Radius * 0.5 + 5),
                Colors.White,
                2
            );
        }
    }
}
