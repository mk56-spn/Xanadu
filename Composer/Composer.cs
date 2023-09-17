// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    public partial class Composer : StageHandler
    {
        public Composer() : base(new PanningCamera())
        {
            AddChild(new SelectionArea());
        }

        public override void _EnterTree()
        {
            GetTree().NodeAdded += node =>
            {
                if (node is not IComposable composable) return;
                node.AddChild(composable.Selectable);
            };

            Provide();

            base._EnterTree();
        }
    }
}
