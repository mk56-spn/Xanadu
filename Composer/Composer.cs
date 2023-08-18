// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.DataStructure;
using XanaduProject.Screens;
using XanaduProject.Singletons;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Provider))]
    public partial class Composer : Control, IProvide<Stage>
    {
        public override partial void _Notification(int what);

        public Stage Value() => stage;

        private AudioSource audioSource = null!;
        private Stage stage = null!;
        public StageInfo StageInfo = null!;

        public override void _Ready()
        {
            base._Ready();

            audioSource = SingletonSource.GetAudioSource();
            audioSource.SetTrack(StageInfo.TrackInfo);

            // Makes sure that the Composer's ready function is called after the core has loaded, avoiding the physics process being turned on automatically from there
            LayoutMode = 1;
            AnchorsPreset = 8;
            setUpChildren();

            Provide();
        }

        private void setUpChildren()
        {
            stage = StageInfo.Stage.Instantiate<Stage>();
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
