// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Animation;
using XanaduProject.ECSComponents.EntitySystem.Components.Bones;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Singleton;
using AnimatedHoverButton = XanaduProject.UiElements.AnimatedHoverButton;

namespace XanaduProject.Screens.AssetCreation.PoseAnimating
{
    public partial class BoneInputHandler : BaseInputHandler
    {
        private readonly EntityStore store = GameServices.Store;
        private readonly Control parent;
        private readonly RenderRid canvas;

        private readonly List<Entity> targets = new();
        private Vector2 getOffset() => CustomMinimumSize / 2;

        private HBoxContainer container = new();

        private AnimatedHoverButton addKeyButton = new("+", 25);

        private bool isDraggingSelection;
        private Vector2 dragStartPosition;
        private Rect2 selectionRectangle;

        public BoneInputHandler(Control parent, Container buttons)
        {
            CustomMinimumSize = new Vector2(500, 500);
            Position = -CustomMinimumSize / 2;
            this.parent = parent;
            canvas = RenderRid.Create();

            var v = GameServices.Store.Query<RootEcs>().Entities.Single().GetComponent<RootEcs>();
            canvas.SetParent(v.Canvas);
            buttons.AddChild(addKeyButton);


            addKeyButton.Pressed += () =>
            {
                Logger.AddLog(LogCategory.General, "Add keyfrsssame");

                if (targets.Count == 0) return;

                Logger.AddLog(LogCategory.General, "Add keyframe");

                foreach (var target in targets)
                {
                    var v =  target.GetIncomingLinks<AnimationTarget>().Single();

                    var value = target.TryGetComponent(out IkTargetComponent ikTargetComponent) ?
                        ikTargetComponent.TargetPosition : target.GetComponent<RootEcs>().Position;
                    v.Entity.AddComponent(new AddKeyFrame(PoseAnimatingScreen.Info.AnimationPos, value));
                }
            };
        }


        protected override void HandleLeftPress(bool multiSelect)
        {
            var mousePosition = GetLocalMousePosition() - getOffset();
            Entity? clickedEntity = findClickedEntity(mousePosition);

            if (clickedEntity == null)
            {
                isDraggingSelection = true;
                dragStartPosition = mousePosition;
                if (!multiSelect) targets.Clear();
            }
            else
            {
                if (!multiSelect)
                {
                    if (!targets.Contains(clickedEntity.Value))
                    {
                        targets.Clear();
                        targets.Add(clickedEntity.Value);
                    }
                }
                else
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

        protected override void HandleLeftRelease()
        {
            if (!isDraggingSelection) return;

            isDraggingSelection = false;

            var entitiesInRect = findEntitiesInRect(selectionRectangle.Abs());

            foreach (var entity in entitiesInRect)
            {
                if (!targets.Contains(entity)) targets.Add(entity);
            }

            selectionRectangle = new Rect2();
            QueueRedraw();
        }

        private Entity? findClickedEntity(Vector2 mousePosition)
        {
            Entity? clickedEntity = null;

            // Check IK targets first
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component1, Entity entity) =>
            {

                var finalBone =  component1.LowerBoneEntity;
                var v = finalBone.GetComponent<BoneGlobalTransform>().GlobalPosition;

                // Calculate the end position of the current bone, which is the start position for its children.
                var rotation = Vector2.Right.Rotated(finalBone.GetComponent<BoneGlobalTransform>().GlobalAngle);
                Vector2 currentEndPosition = v + rotation * finalBone.GetComponent<BoneEcs>().Length;
                if ((currentEndPosition - mousePosition).Length() < 10)
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

        private List<Entity> findEntitiesInRect(Rect2 rect)
        {
            var entities = new List<Entity>();

            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component, Entity entity) =>
            {
                if (rect.HasPoint(component.TargetPosition))
                {
                    entities.Add(entity);
                }
            }));

            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                if (rect.HasPoint(component.Position))
                {
                    entities.Add(entity);
                }
            }));

            return entities;
        }


        protected override void OnRightClick()
        {
        }

        protected override void OnDrag(Vector2 delta)
        {
            if (isDraggingSelection)
            {
                var mousePosition = GetLocalMousePosition() - getOffset();
                selectionRectangle = new Rect2(dragStartPosition, mousePosition - dragStartPosition);
                QueueRedraw();
                return;
            }

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

            if (isDraggingSelection)
            {
                var rect = selectionRectangle.Abs();
                rect.Position += getOffset();
                DrawRect(rect, Colors.White with { A = 0.2f });
            }

            canvas.Clear();

            // Draw all handles
            store.Query<IkTargetComponent>().ForEachEntity(((ref IkTargetComponent component, Entity entity) =>
            {
                var color = targets.Contains(entity) ? Colors.White.Darkened(0.3f) : Colors.DarkGreen;
                if (selectionRectangle.Abs().HasPoint(component.TargetPosition)) color = Colors.Orange;

                var finalBone =  component.LowerBoneEntity;
                var v = finalBone.GetComponent<BoneGlobalTransform>().GlobalPosition;

                // Calculate the end position of the current bone, which is the start position for its children.
                var rotation = Vector2.Right.Rotated(finalBone.GetComponent<BoneGlobalTransform>().GlobalAngle);
                Vector2 currentEndPosition = v + rotation * finalBone.GetComponent<BoneEcs>().Length;

                canvas.AddCircle(10, currentEndPosition,
                    color);
            }));

            store.Query<RootEcs>().ForEachEntity(((ref RootEcs component, Entity entity) =>
            {
                var color = targets.Contains(entity) ? Colors.White.Darkened(0.3f) : Colors.Blue;
                if (selectionRectangle.Abs().HasPoint(component.Position)) color = Colors.Orange;
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
