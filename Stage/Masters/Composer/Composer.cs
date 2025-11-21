// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using Microsoft.Extensions.DependencyInjection;
using Xanadu.Singletons;
using XanaduProject.Audio;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems;
using XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;
using XanaduProject.IO;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using XanaduProject.Singleton;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class Composer : Player, IComposer
    {
        public StageData Data { get; set; }
        /// <summary>
        /// Tells notes that we place what direction component they will have. If any.
        /// </summary>
        public Direction? SelectedDirection { get; set; }

        public NoteType SelectedNoteType { get; set; } = NoteType.Main;

        public BaseInputHandler.InputState State { get; set; } = BaseInputHandler.InputState.Idle;

        /// <summary>
        /// Gets or sets a value indicating whether a placed object will be snapped to a specific grid or alignment.
        /// </summary>
        public bool Snapped { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the composer is currently in rotation mode.
        /// </summary>
        public bool Rotating { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether blocks should be added/removed during drag operations.
        /// </summary>
        public bool AddOnDrag { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is currently drag selecting.
        /// </summary>
        public bool IsDragSelecting { get; set; }

        /// <summary>
        /// Which entity template is currently selected for placement.
        /// </summary>
        public Entity SelectedTemplateEntity { get; set; } = EntityTemplate.GetDefault();

        public BlockShaderId? SelectedBlockShaderId { get; set; }

        public ArchetypeQuery<ElementEcs, SelectionEcs> Selected { get; }
        public Vector2 ViewportSize { get; private set; }
        public Vector2 MousePosLocal { get; private set; } = Vector2.Zero;
        public Vector2 LastClickedMousePosLocal { get; set; } = Vector2.Zero;
        public Vector2 RelativeMouseMotion { get; private set; } = Vector2.Zero;
        public CanvasLayer ComposerUiCanvas { get; }

        public Composer(StageData stageData) : base(stageData)
        {
            ComposerVisuals visuals = new ComposerVisuals();
            DiProvider.Register(collection =>
            {
                collection.AddSingleton<IComposer>(this);
                collection.AddSingleton<IEditorClock>(StageConductor.Clock);
                collection.AddSingleton<IComposerVisuals>(visuals);
            });

            Data = stageData;
            AddChild(new ComposerInput());
            AddChild(new ComposerMacros(this));

            ComposerUiCanvas = visuals;

            AddChild(visuals);

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

            StageConductor.Root.Add(new SelectionShapeSystem());

            StageConductor.Root.Add(new PlaybackButtonSystem());
            StageConductor.Root.Add(new DebugSystem(StageConductor.Root));
            StageConductor.Root.Add(new ComposerVisualsGroup("visuals"));
            StageConductor.Root.Add(new EntityTypeSelectorButtons());
            StageConductor.Root.Add(new KeyFrameEditSystem<Vector2,VectorArrayEcs>());
            StageConductor.Root.Add(new KeyFrameEditSystem<float,AngleArrayEcs>());
            StageConductor.Root.Add(new KeyFrameEditSystem<Color,ColorArrayEcs>());

            Early.Add(new ColourTrackSystem());
            GameServices.Canvas.AddChild(control);
        }

        private Control control = new();
        public override void _Process(double delta)
        {

            var mousePos = control.GetLocalMousePosition();
            RelativeMouseMotion =  MousePosLocal -mousePos;
            MousePosLocal = mousePos;
        }
    }
}

