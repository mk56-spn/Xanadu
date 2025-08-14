// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.ECSComponents.EntitySystem
{
    /// <summary>
    /// A custom Label control that mirrors the visibility
    /// state of a specified target control. Automatically
    /// updated whenever the target's visibility changes.
    /// </summary>
    public partial class VisibilityLabel : Label
    {
        public VisibilityLabel(Control target)
        {
            target.VisibilityChanged += () => Visible = target.Visible;
        }
    }
}
