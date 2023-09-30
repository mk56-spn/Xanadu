// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer.Composable.Notes;

namespace XanaduProject.Composer.Selectables
{
    public partial class SelectableNote : SelectableHandle
    {
        protected override Color HighlightColor => Colors.Blue;

        public SelectableNote ()
        {
            Radius = 31;
            OnDragged += () => GetParent<Note>().GlobalPosition = GetTruePosition();
        }
    }
}
