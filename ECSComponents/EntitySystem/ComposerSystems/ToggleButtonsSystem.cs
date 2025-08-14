// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.GameDependencies;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class ToggleButtonsSystem : BaseSystem
    {
        private static readonly IComposer composer = DiProvider.Get<IComposer>();
        private static readonly IComposerVisuals visuals = DiProvider.Get<IComposerVisuals>();

        private readonly Button snapped = new() { ToggleMode = true, Text = "Snapped" };
        private readonly Button rotating = new() { ToggleMode = true, Text = "Rotating" };

        public ToggleButtonsSystem()
        {
            snapped.Toggled += on => composer.Snapped = on;
            rotating.Toggled += on => composer.Rotating = on;

            visuals.TopBarAddWidget(snapped);
            visuals.TopBarAddWidget(rotating);
        }
    }
}
