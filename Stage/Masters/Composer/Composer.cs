// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.Serialization.SerialisedObjects;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class Composer : Player, IComposer
    {
        /// <summary>
        /// Tells notes that we place what direction component they will have. If any.
        /// </summary>
        public Direction? SelectedDirection { get; set; }

        public NoteType SelectedNoteType { get; set; } = NoteType.Main;

        public ComposerInput.InputState State { get; set; } = ComposerInput.InputState.Idle;

        /// <summary>
        /// Gets or sets a value indicating whether a placed object will be snapped to a specific grid or alignment.
        /// </summary>
        public bool Snapped { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the composer is currently in rotation mode.
        /// </summary>
        public bool Rotating { get; set; }

        /// <summary>
        /// Which entity template is currently selected for placement.
        /// </summary>
        public Entity SelectedTemplateEntity { get; set; } = EntityTemplate.GetDefault();

        public BlockShaderId? SelectedBlockShaderId { get; set; }

        public ArchetypeQuery<ElementEcs, SelectionEcs> Selected { get; }
        public Vector2 ViewportSize { get; private set; }
        public Vector2 MousePosLocal { get; private set; } = Vector2.Zero;
        public Vector2 RelativeMouseMotion { get; private set; } = Vector2.Zero;
        public CanvasLayer ComposerUiCanvas { get; } = new();

        public Composer(SerializableStage serializableStage, TrackInfo trackInfo) : base(serializableStage, trackInfo)
        {
            ComposerVisuals visuals = new ComposerVisuals();
            DiProvider.Register(collection =>
            {
                collection.AddSingleton<IComposer>(this);
                collection.AddSingleton<IEditorClock>(StageConductor.Clock);
                collection.AddSingleton<IComposerVisuals>(visuals);
            });

            AddChild(new ComposerInput());
            AddChild(new ComposerMacros(this));

            AddChild(visuals);

            StageConductor.AddChild(new PanningCamera());
            Selected = EntityStore.Query<ElementEcs, SelectionEcs>().AllTags(Tags.Get<SelectionFlag>());

            addComposerSystem();
        }

        public override void _Ready()
        {
            base._Ready();
            var v = GetViewport();
            ViewportSize = GetViewportRect().Size;
            v.SizeChanged += () => ViewportSize = GetViewportRect().Size;

        }

        private void addComposerSystem()
        {
            StageConductor.Root.Add(new RotationSystem());
            StageConductor.Root.Add(new GridSystem());
            StageConductor.Root.Add(new SelectionShapeSystem());
            StageConductor.Root.Add(new NoteButtonsSystem());
            StageConductor.Root.Add(new ToggleButtonsSystem());
            StageConductor.Root.Add(new MouseEntityRepresentation());
            StageConductor.Root.Add(new NoteTimelineSystem());
            StageConductor.Root.Add(new PlaybackButtonSystem());
            StageConductor.Root.Add(new NoteDirectionWidgetSystem());
            StageConductor.Root.Add(new NoteTypeWidgetSystem());
            StageConductor.Root.Add(new BlockMaterialWidget());
            StageConductor.Root.Add(new EntityTypeSelectorButtons());
        }
        public override void _Process(double delta)
        {
            var mousePos = GetLocalMousePosition();
            RelativeMouseMotion =  MousePosLocal -mousePos;
            MousePosLocal = mousePos;
        }
    }
}

