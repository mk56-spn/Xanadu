// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : StageHandler, IProvide<bool>
    {
        public override partial void _Notification(int what);

        public const int GRID_SIZE = 32;

        private bool snapped;
        bool IProvide<bool>.Value() => snapped;

        public Composer() : base(new PanningCamera())
        {
            AddChild(new SelectionArea());
        }

        public override void _EnterTree()
        {
            AddChild(new MouseGrid());
            GetNode<CheckButton>("%SnapButton").Toggled += on => snapped = on;
            GetTree().NodeAdded += node =>
            {
                if (node is not IComposable composable) return;
                node.AddChild(composable.Selectable);
            };

            Provide();

            base._EnterTree();

            TrackHandler.SongPositionChanged += position =>
            {
                if (position != 0) return;

                Stage.Core.Rotation = 0;
                Stage.Core.Position = Vector2.Zero;
            };
        }
    }
}
