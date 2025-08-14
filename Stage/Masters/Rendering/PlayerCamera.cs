// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Rendering
{
        public partial class PlayerCamera : Camera2D
        {
            private IVisualsMaster visualsMaster = null!;
            public override void _EnterTree()
            {
                base._EnterTree();
                 visualsMaster   = DiProvider.Get<IVisualsMaster>();
            }

            public override void _Process(double delta)
            {

                Position = Position.Lerp(DiProvider.Get<IPlayerCharacter>().Position, (float)(10f * delta));
                visualsMaster.CameraPosition = Position;
            }
        }
}
