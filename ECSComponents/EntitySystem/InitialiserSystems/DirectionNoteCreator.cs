// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public class DirectionNoteCreator : BaseCreatorSystem<NoteEcs, DirectionEcs>
    {
        private readonly DirectionNotes n = new();
        protected override void OnUpdate()
        {
            Query.Each(n);
        }
        private static string directionToArrow(Direction dir) => dir switch
        {
            Direction.Up        => "↑",
            Direction.Down      => "↓",
            Direction.Left      => "←",
            Direction.Right     => "→",
            Direction.UpLeft    => "↖",
            Direction.UpRight   => "↗",
            _                   => dir.ToString().Substring(0, 1) // Fallback
        };

        private struct DirectionNotes : IEach<ElementEcs, NoteEcs, DirectionEcs>
        {
            private static readonly Vector2 note_size = new(70, 25);

            public void Execute(ref ElementEcs element, ref NoteEcs note, ref DirectionEcs direction)
            {
                RenderRid.Create()
                    .AddString(Vector2.Zero, directionToArrow(direction.Direction), 50)
                    .SetParent(note.NoteCanvas);
            }
        }
    }
}
