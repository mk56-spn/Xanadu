// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Character;
using XanaduProject.DataStructure;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.IO.Indexes;
using XanaduProject.Screens;
using XanaduProject.Screens.Result;
using XanaduProject.Stage.Masters.Rendering;
using Screen = XanaduProject.Screens.ScreenStructure.Screen;

namespace XanaduProject.Stage
{
    public partial class Player : Screen, IPlayer
    {
        public EntityStore EntityStore { get; }

        public TrackInfo TrackInfo { get; }
        public StageConductor StageConductor;

        public Player(StageData data)
        {
            TrackInfo = TrackIndex.GetTrackInfo(data.StageInfo.SongIndex);
            DiProvider.Configure(c=>c.AddSingleton<IPlayer>(this));
            AddChild(StageConductor = new StageConductor(TrackInfo,EntityStore = data.Store));
            setup();
        }

        private void setup()
        {
            if (this is Masters.Composer.Composer)
            {
                IsComposer = true;
                return;
            }

            StageConductor.AddChild(new PlayerCamera());

            Ready += () =>
            {
                StageConductor.Clock.Restart();
                StageConductor.Clock.Resume();
            };
        }

        public override void _EnterTree()
        {
            base._EnterTree();
            Manager = DiProvider.Get<ScreenManager>();

            if (this is Masters.Composer.Composer) return;
            Manager.ChangeSubScreen(new Pause(this));
        }

        public ScreenManager Manager { get; set; } = null!;
        public bool IsComposer { get; private set; }
        public void RequestResults() => Manager.RequestChangeScreen(new ResultScreen(this));
    }
}
