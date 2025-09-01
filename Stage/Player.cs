// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage
{
    public partial class Player : Screen
    {
        public EntityStore EntityStore { get; }

        public TrackInfo TrackInfo { get; }
        public StageConductor StageConductor;

        public Player(StageData stage)
        {
            TrackInfo = TrackIndex.GetTrackInfo(stage.StageInfo.SongIndex);

            AddChild(StageConductor = new StageConductor(TrackInfo,EntityStore = stage.Store));
            setup();
        }
        public Player(EntityStore entityStore, TrackInfo trackInfo)
        {
            TrackInfo = trackInfo;

            AddChild(StageConductor = new StageConductor(trackInfo, EntityStore = entityStore));
            setup();
        }

        private void setup()
        {
            if (this is Masters.Composer.Composer) return;
            StageConductor.AddChild(new PlayerCamera());

            AddChild(new Pause(this));
            Ready += () =>
            {
                StageConductor.Clock.Restart();
                StageConductor.Clock.Resume();
            };
        }
    }
}
