// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;

namespace XanaduProject.Composer.ComposerUI.PropertyGroups
{
    [SuperNode(typeof(Dependent))]
    public abstract partial class PropertyGroup : VBoxContainer
    {
        public override partial void _Notification(int what);

        [Dependency]
        protected List<Node> Selected => DependOn<List<Node>>();

        private Label headerLabel { get; set; } = new Label();

        protected abstract string GroupName { get; }
        protected abstract Color GroupColour { get; }

        protected PropertyGroup ()
        {
            AddChild(headerLabel);
        }

        public override void _Ready()
        {
            base._Ready();

            headerLabel.Modulate = GroupColour;
            headerLabel.Text = GroupName;
        }
    }
}
