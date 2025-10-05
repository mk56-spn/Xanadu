// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
using AnimatedHoverButton = XanaduProject.UiElements.AnimatedHoverButton;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class BoneInputHandler : BaseInputHandler
    {
        private readonly EntityStore store = GameServices.Store;
        private readonly Control parent;
        private readonly RenderRid canvas;

        private readonly List<Entity> targets = new();
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

                if (targets.Count == 0) return;

                Logger.AddLog(LogCategory.General, "Add keyframe");

                foreach (var target in targets)
                {
                    var v =  target.GetIncomingLinks<AnimationTarget>().Single();

                    var value = target.TryGetComponent(out IkTargetComponent ikTargetComponent) ?
                        ikTargetComponent.TargetPosition : target.GetComponent<RootEcs>().Position;
                    KeyframeManager<Vector2>.AddFrame(ref v.Entity.GetComponent<VectorArrayEcs>().Points, ref v.Entity.GetComponent<FloatArrayEcs>().Points, time.AnimationPos , value);
                }
            };
        }
        protected override void HandleLeftPress(bool multiSelect)
        {
            var mousePosition = GetLocalMousePosition() - getOffset();
            Entity? clickedEntity = FindClickedEntity(mousePosition);

            if (!multiSelect)
            {
                targets.Clear();
                if (clickedEntity != null)
                {
                    targets.Add(clickedEntity.Value);
                }
            }
            else
            {
                if (clickedEntity != null)
                {
                    if (targets.Contains(clickedEntity.Value))
                    {
                        targets.Remove(clickedEntity.Value);
                    }
                    else
                    {
                        targets.Add(clickedEntity.Value);
                    }
                }
            }

            QueueRedraw();
            PoseAnimatingScreen.Info.AnimationActive = AnimationState.Disabled;
        }

        private Entity? FindClickedEntity(Vector2 mousePosition)
        {
            Entity? clickedEntity = null;

            // Check IK targets first
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component1, Entity entity) =>
            {
                if ((component1.TargetPosition - mousePosition).Length() < 10)
                {
                    clickedEntity = entity;
                }
            }));

            // Then check Root entities
            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                if ((component.Position - mousePosition).Length() < 10)
                {
                    clickedEntity = entity;
                }
            }));

            return clickedEntity;
        }


        protected override void OnRightClick()
        {
        }

        protected override void OnDrag(Vector2 delta)
        {
            if (targets.Count == 0) return;

            foreach (var target in targets)
            {
                if (target.TryGetComponent(out RootEcs _))
                {
                    target.GetComponent<RootEcs>().Position += delta;
                }
                else if (target.TryGetComponent(out IkTargetComponent _))
                {
                    target.GetComponent<IkTargetComponent>().TargetPosition += delta;
                }
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Colors.Green with { A = 0.1f }, filled: false);

            canvas.Clear();

            // Draw all handles
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component, Entity entity) =>
            {
                var color = targets.Contains(entity) ? Colors.White.Darkened(0.3f) : Colors.DarkGreen;
                canvas.AddCircle(10, component.TargetPosition, color);
            }));

            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                var color = targets.Contains(entity) ? Colors.White.Darkened(0.3f) : Colors.Blue;
                canvas.AddCircle(10, component.Position, color);
            }));
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            addKeyButton.Disabled = targets.Count == 0;

        }
    }
}
