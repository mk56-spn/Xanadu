// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Dependent))]
    public partial class XanaduColorPicker : ColorPickerButton
    {
        public override partial void _Notification(int what);

        [Dependency]
        private List<Node> selected => DependOn<List<Node>>();

        public XanaduColorPicker ()
        {
            ColorChanged += color => selected.ForEach(t => ((Node2D)t).SelfModulate = color);
        }
    }
}
