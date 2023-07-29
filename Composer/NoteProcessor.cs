// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;

namespace XanaduProject.Composer
{
    public partial class NoteProcessor : Node
    {
        private Area2D receptor;

        public NoteProcessor(Area2D receptor)
        {
            this.receptor = receptor;
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);

            foreach (var area in  receptor.GetOverlappingAreas())
            {
                if (Input.IsActionJustPressed("R1"))
                    area.GetParent<Note>().Activate();
            }
        }
    }
}
