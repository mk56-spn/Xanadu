// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerInputReciever : Control
    {
        public override void _EnterTree()
        {
            base._EnterTree();
            MouseFilter = Control.MouseFilterEnum.Pass;
        }

    }
}
