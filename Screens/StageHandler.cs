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
    public partial class StageHandler : CanvasLayer, IProvide<TrackHandler>, IProvide<Stage>
    {
        public override partial void _Notification(int what);

        protected Stage Stage = null!;

        private StageInfo stageInfo = null!;

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

        private readonly TrackHandler trackHandler = new TrackHandler();

        TrackHandler IProvide<TrackHandler>.Value() => trackHandler;
        public Stage Value() => Stage;

        public override void _Ready()
        {
            base._Ready();

            GetTree().Paused = true;
            ProcessMode = ProcessModeEnum.Always;

            AddChild(Stage);
            AddChild(trackHandler);

            trackHandler.SetTrack(StageInfo.TrackInfo);
            trackHandler.StartTrack();
        }
    }
}
