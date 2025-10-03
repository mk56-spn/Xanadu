// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Singletons;
using XanaduProject.Buttons;
using XanaduProject.ECSComponents.Animation2;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Singleton;
using XanaduProject.Stage.Masters.Composer.TrackVisualiser;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class BoneInputHandler : BaseInputHandler
    {
        private readonly EntityStore store = GameServices.Store;
        private readonly Control parent;
        private readonly RenderRid canvas;

        private Entity? target;
        private Vector2 getOffset() => Size / 2;

        private HBoxContainer container = new();

        private AnimatedHoverButton addKeyButton = new("+", 25);

        public BoneInputHandler(Control parent, Container buttons)
        {
            CustomMinimumSize = new Vector2(500, 500);
            Position = -CustomMinimumSize / 2;
            this.parent = parent;
            canvas = RenderRid.Create(this).SetTransform(new Transform2D(0, CustomMinimumSize / 2));


            buttons.AddChild(addKeyButton);


            addKeyButton.Pressed += () =>
            {
                Logger.AddLog(LogCategory.General, "Add keyfrsssame");

                ref AnimationInfo time = ref PoseAnimatingScreen.Info;

                if (target == null) return;

                Logger.AddLog(LogCategory.General, "Add keyframe");

                var v =  target.Value.GetIncomingLinks<AnimationTarget>().Single();

                var value = target.Value.TryGetComponent(out IkTargetComponent ikTargetComponent) ?
                    ikTargetComponent.TargetPosition : target.Value.GetComponent<RootEcs>().Position;
                KeyframeManager<Vector2>.AddFrame(ref v.Entity.GetComponent<VectorArrayEcs>().Points, ref v.Entity.GetComponent<FloatArrayEcs>().Points, time.AnimationPos , value);
            };
        }
        protected override void HandleLeftPress(bool multiSelect)
        {
            canvas.Clear();
            target = null;

            // Check IK targets
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component1, Entity entity) =>
            {
                canvas.AddCircle(10, component1.TargetPosition, Colors.DarkGreen);
                if ((component1.TargetPosition - GetLocalMousePosition() + getOffset()).Length() < 10)
                {
                    target = entity;
                    canvas.AddCircle(10, component1.TargetPosition, Colors.White.Darkened(0.3f));
                }
            }));

            // Check Root entities
            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                canvas.AddCircle(10, component.Position, Colors.Blue);
                if (!((component.Position - GetLocalMousePosition() + getOffset()).Length() < 10)) return;
                target = entity;
                canvas.AddCircle(10, component.Position, Colors.White.Darkened(0.3f));
            }));

            PoseAnimatingScreen.Info.AnimationActive = AnimationState.Disabled;

        }

        protected override void OnRightClick()
        {
        }

        protected override void OnDrag(Vector2 delta)
        {
            if (target == null) return;

            if (target.Value.TryGetComponent(out RootEcs _))
            {
                target.Value.GetComponent<RootEcs>().Position += delta;
            }
            else
            {
                target.Value.GetComponent<IkTargetComponent>().TargetPosition += delta;
            }

            canvas.Clear();

            // Redraw IK targets
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component1, Entity entity) =>
            {
                canvas.AddCircle(10, component1.TargetPosition, Colors.DarkGreen);
                if (target == entity)
                {
                    canvas.AddCircle(10, component1.TargetPosition, Colors.White.Darkened(0.3f));
                }
            }));

            // Redraw Root handles
            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                canvas.AddCircle(10, component.Position, Colors.Blue);
                if (target == entity)
                {
                    canvas.AddCircle(10, component.Position, Colors.White.Darkened(0.3f));
                }
            }));
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Green with { A = 0.1f }, filled: false);

            // Draw all handles
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component, Entity _) =>
            {
                canvas.AddCircle(10, component.TargetPosition, Colors.DarkGreen);
            }));

            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity _) =>
            {
                canvas.AddCircle(10, component.Position, Colors.Blue);
            }));
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            addKeyButton.Disabled = target == null;

        }
    }
}
