// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Composer.ComposerUI.PropertyGroups;

namespace XanaduProject.Composer.ComposerUI
{
    public partial class PropertyContainer : VBoxContainer
    {
        public PropertyContainer ()
        {
            AddChild(new GlobalPropertyGroup());
            AddChild(new LinePropertyGroup());
        }
    }
}
