// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.Screens;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : Control, IProvide<Stage>, IProvide<TrackHandler>
    {
        public override partial void _Notification(int what);

        public Stage Value() => stage;
        TrackHandler IProvide<TrackHandler>.Value() => trackHandler;

        private Stage stage = null!;
        public StageInfo StageInfo = null!;

        private TrackHandler trackHandler = new TrackHandler();

        public override void _Ready()
        {
            base._Ready();

            trackHandler.SetTrack(StageInfo.TrackInfo);

            // Makes sure that the Composer's ready function is called after the core has loaded, avoiding the physics process being turned on automatically from there
            LayoutMode = 1;
            AnchorsPreset = 8;
            setUpChildren();

            Provide();
        }

        private void setUpChildren()
        {
            stage = StageInfo.Stage.Instantiate<Stage>();

            AddChild(trackHandler);
            trackHandler.StartTrack();

            AddChild(stage);
            stage.Core.SetPhysicsProcess(false);

            AddChild(new PanningCamera());
        }

        private partial class PanningCamera : Camera2D
        {
            public override void _UnhandledInput(InputEvent @event)
            {
                base._UnhandledInput(@event);

                if (@event is InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouse)
                    Position -= mouse.Relative;
            }
        }
    }
}
