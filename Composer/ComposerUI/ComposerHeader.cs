// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Screens;

namespace XanaduProject.Composer.ComposerUI
{
    [GlobalClass]
    [SuperNode(typeof(Dependent))]
    public partial class ComposerHeader : PanelContainer
    {
        public override partial void _Notification(int what);

        [Export]
        private Label titleText = null!;

        [Export]
        private Label songText = null!;

        [Dependency] private Stage stage => DependOn<Stage>();

        public void OnResolved()
        {
            songText.Text = stage.Info.TrackInfo.SongTitle;
            titleText.Text = stage.Info.Title;
        }
    }
}
