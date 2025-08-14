// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.Audio;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class PlaybackButtonSystem : BaseSystem
    {
        private readonly IEditorClock clock = DiProvider.Get<IEditorClock>();
        private readonly IComposerVisuals composer = DiProvider.Get<IComposerVisuals>();
        private readonly Button buttonToggle = new() { Text = "Toggle playback" };
        private readonly Button buttonRestart = new() { Text = "Restart" };


        public PlaybackButtonSystem()
        {
            buttonToggle.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            buttonRestart.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

            composer.AddTabToLeft(buttonRestart);
            composer.AddTabToLeft(buttonToggle);
            buttonToggle.Pressed += () => clock.TogglePause();
            buttonRestart.Pressed += () => clock.Restart();
        }
    }
}
