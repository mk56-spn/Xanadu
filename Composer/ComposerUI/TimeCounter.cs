// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;
using XanaduProject.Audio;

namespace XanaduProject.Composer.ComposerUI
{
    [SuperNode(typeof(Dependent))]
    public partial class TimeCounter : Label
    {
        public override partial void _Notification(int what);

        [Dependency]
        private TrackHandler trackHandler => DependOn<TrackHandler>();

        public void OnResolved()
        {
            Text = new TimeSpan().ToString(@"mm\:ss\.ff");

            trackHandler.SongPositionChanged += position =>
                Text = TimeSpan.FromSeconds(position).ToString(@"mm\:ss\.ff");
        }
    }
}
