// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : StageHandler, IProvide<Camera2D>
    {
        public override partial void _Notification(int what);
        Camera2D IProvide<Camera2D>.Value() => Camera;

        public Composer() : base(new PanningCamera()) { }

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
