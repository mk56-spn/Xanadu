// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Audio;
using XanaduProject.Composer;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Serialization.SerialisedObjects;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class Composer : Player, IComposer
    {
        public Direction? SelectedDirection { get; set; }
        public bool Snapped { get; set; }
        public Entity SelectedTemplateEntity { get; set; } = EntityTemplate.GetDefault();
        public ArchetypeQuery<ElementEcs, SelectionEcs> Selected { get; }
        public Vector2 MousePosLocal { get; private set; } = Vector2.Zero;
        public CanvasLayer ComposerUiCanvas { get; } = new();

        public Composer(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage, trackInfo)
        {
            DiProvider.Register(collection =>
                {
                    collection.AddSingleton<IComposer>(this);
                    collection.AddSingleton<IEditorClock>(StageConductor.Clock);
                });

            AddChild(new ComposerInput());

            Selected = EntityStore.Query<ElementEcs, SelectionEcs>();

            AddChild(new ComposerMacros(this));

            AddChild(ComposerUiCanvas);
            ComposerUiCanvas.AddChild(new PanningCamera());
            ComposerUiCanvas.AddChild(ComposerVisuals.Create());

            Selected = EntityStore.Query<ElementEcs, SelectionEcs>().AllTags(Tags.Get<SelectionFlag>());
        }


        public override void _Process(double delta)
        {
            MousePosLocal = GetLocalMousePosition();
        }

        public void RequestAddElement()
        {
            Entity ent = EntityStore.CreateEntity();

            // 1. Copy the base properties from the selected template (note or block).
            SelectedTemplateEntity.CopyEntity(ent);

            // 2. If it's a note and a direction is selected, add the direction component.
            if (ent.TryGetComponent(out NoteEcs note) && SelectedDirection.HasValue)
            {
                ent.AddComponent(new DirectionEcs(SelectedDirection.Value));
                note.TimingPoint = 3;
            }

            ent.AddTag<UnInitialized>();
            ent.AddTag<SelectionFlag>();

            Vector2 size = new(32, 32);
            if (ent.TryGetComponent(out RectEcs rect)) size = rect.Extents;

            ent.AddComponent(new ElementEcs { Transform = Transform2D.Identity with { Origin = position(size) } });
        }

        private Vector2 position(Vector2 size)
        {
            return (MousePosLocal + size / 2).Snapped(size) - size / 2;
        }
    }
}
