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
        private StageInfo stageInfo;

        protected Camera2D Camera;
        protected Stage Stage = null!;

        protected readonly TrackHandler TrackHandler = new TrackHandler();
        TrackHandler IProvide<TrackHandler>.Value() => TrackHandler;
        public Stage Value() => Stage;

        protected StageHandler (Camera2D camera, StageInfo stageInfo)
        {
            Camera = camera;
            this.stageInfo = stageInfo;
            AddChild(camera);
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _EnterTree()
        {
            base._EnterTree();

            TrackHandler.OnPreemptComplete += (_, _) => Stage.Core.Movable = true;

            Stage = stageInfo.GetStage();
            AddChild(Stage);
            TrackHandler.SetTrack(stageInfo.TrackInfo);
            AddChild(TrackHandler);

            Provide();
        }
    }
}
