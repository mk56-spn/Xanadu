// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.UiElements
{
    public partial class MutiTrackVisualizer : VBoxContainer
    {
        private EntityStore store => GameServices.Store;

        public override void _Process(double delta)
        {
            base._Process(delta);

        }
    }
}
