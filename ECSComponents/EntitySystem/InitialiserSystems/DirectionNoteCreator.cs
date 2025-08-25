// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.Factories;

namespace XanaduProject.ECSComponents.EntitySystem.InitialiserSystems
{
    public class DirectionNoteCreator : BaseCreatorSystem<NoteEcs, DirectionEcs>
    {
        private readonly DirectionNotes n = new();
        protected override void OnUpdate() => Query.Each(n);

        private static float directionToRotation(Direction dir) => dir switch
        {
            Direction.Up        => -Mathf.Pi / 2,
            Direction.Down      => Mathf.Pi / 2,
            Direction.Left      => Mathf.Pi,
            Direction.Right     => 0,
            Direction.UpLeft    => -3 * Mathf.Pi / 4,
            Direction.UpRight   => -Mathf.Pi / 4,
            _                   => 0
        };
        private struct DirectionNotes : IEach<ElementEcs, NoteEcs, DirectionEcs>
        {
            public void Execute(ref ElementEcs element, ref NoteEcs note, ref DirectionEcs direction)
            {
                RenderRid.Create()
                    .SetModulate(note.NoteType.NoteColor())
                    .AddSetTransform(new Transform2D(directionToRotation(direction.Direction),Vector2.Zero))
                    .AddMesh(MeshFactory.CreateStar(6,30,0.2f).GetRid(), modulate: Colors.White.Darkened(0.5f))
                    .AddMesh(MeshFactory.CreateStar(3,50,0.2f).GetRid())
                    .AddMesh(MeshFactory.CreateStar(3,40,0.2f).GetRid(), modulate: Colors.White.Darkened(0.8f))
                    .SetParent(note.NoteCanvas);

                note.NoteCanvas.AsRenderRid()
                    .AddSetTransform(new Transform2D(directionToRotation(direction.Direction),Vector2.Zero))
                    .AddMesh(MeshFactory.CreateStar(3, 50, 0.2f).GetRid())
                    .AddSetTransform(new Transform2D(directionToRotation(direction.Direction)+float.Pi,Vector2.Zero))
                    .AddMesh(MeshFactory.CreateStar(3, 50, 0.2f).GetRid());
            }
        }
    }
}
