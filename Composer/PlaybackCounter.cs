// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Dependent))]
    public partial class PlaybackCounter : HBoxContainer
    {
        public override partial void _Notification(int what);

        private Button playbackButton = new Button { Text = "Playback state" };
        private Button stopButton = new Button { Text = "Stop" };

        [Dependency] private TrackHandler trackHandler => DependOn<TrackHandler>();

        public void OnResolved()
        {
            AddChild(playbackButton);
            AddChild(stopButton);

            playbackButton.Pressed += () =>
            {
                if (trackHandler.TrackPosition.Equals(0))
                {
                    trackHandler.StartTrack();
                    return;
                }
                trackHandler.TogglePlayback();
            };

            stopButton.Pressed += () => trackHandler.StopTrack();
        }
    }
}
