// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;
using XanaduProject.DataStructure;

namespace XanaduProject.Screens
{
    /// <summary>
    /// Base class for any classes that are tasked with managing a stage.
    /// </summary>
    [SuperNode(typeof(Provider))]
    public abstract partial class StageHandler : Control, IProvide<TrackHandler>, IProvide<Stage>
    {
        public override partial void _Notification(int what);

        /// <summary>
        /// A resource containing information about a stage that may be useful outside of gameplay.
        /// </summary>
        public StageInfo StageInfo
        {
            get => stageInfo;
            set
            {
                Stage = value.GetStage();
                stageInfo = value;
            }
        }

        protected Camera2D Camera;
        protected Stage Stage = null!;
        private StageInfo stageInfo = null!;

        private readonly TrackHandler trackHandler = new TrackHandler();
        TrackHandler IProvide<TrackHandler>.Value() => trackHandler;
        public Stage Value() => Stage;

        protected StageHandler (Camera2D camera)
        {
            Camera = camera;
            ProcessMode = ProcessModeEnum.Always;

            AddChild(trackHandler);
            AddChild(camera);
        }

        public override void _Ready()
        {
            base._Ready();

            GetTree().Paused = true;

            AddChild(Stage);

            trackHandler.SetTrack(StageInfo.TrackInfo);
            trackHandler.StartTrack();
        }
    }
}
