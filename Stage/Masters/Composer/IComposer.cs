// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.Screens;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.Stage.Masters.Composer
{
    public interface IComposer
    {
        public Direction? SelectedDirection { get; set; }
        public NoteType SelectedNoteType { get; set; }

        public ComposerInput.InputState State { get; set; }

        public bool Snapped { get; set; }

        public bool Rotating { get; set; }

        public Entity SelectedTemplateEntity { get; set; }

        public BlockShaderId? SelectedBlockShaderId { get; set; }

        public ArchetypeQuery<ElementEcs, SelectionEcs> Selected { get; }

        public EntityStore EntityStore { get; }

        public Vector2 ViewportSize { get; }
        public Vector2 MousePosLocal { get; }

        public Vector2 RelativeMouseMotion { get; }

        public ScreenManager ScreenManager { get; }

        public TrackInfo TrackInfo { get; }

        public void RequestAddElement() => ComposerEntityInstantiator.RequestAddElement(this);
    }
}
