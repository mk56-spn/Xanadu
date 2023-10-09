// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Composer.Selectables;
using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : StageHandler, IProvide<bool>, IProvide<List<Node>>
    {
        public override partial void _Notification(int what);

        public const int GRID_SIZE = 32;

        private bool snapped;
        bool IProvide<bool>.Value() => snapped;

        private List<Node> selectedNodes = new List<Node>();
        List<Node> IProvide<List<Node>>.Value() => selectedNodes;

        public Composer() : base(new PanningCamera())
        {
            AddChild(new SelectionHandler());
        }

        public override void _EnterTree()
        {
            AddChild(new MouseGrid());
            GetNode<Button>("%SnapButton").Toggled += on => snapped = on;
            GetTree().NodeAdded += node =>
            {
                if (node is not IComposable composable) return;

                Selectable selectable = composable.Selectable;

                node.AddChild(selectable);
                selectable.SelectionStateChanged += selected =>
                {
                    if (selected)
                        selectedNodes.Add(node);
                    else
                        selectedNodes.Remove(node);
                };
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
