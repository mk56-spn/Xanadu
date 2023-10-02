// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer.Selectables;

namespace XanaduProject.Composer.Composable.Decorations
{
    public partial class XanaduLine : Line2D, IComposable
    {
        public Selectable Selectable => new SelectableLine(this);

        public XanaduLine ()
        {
            Points = new []
            {
                Vector2.Zero,
                new Vector2(0, 50),
            };
        }
    }
}
