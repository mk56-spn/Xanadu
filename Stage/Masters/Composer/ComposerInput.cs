// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under...
// ‑- existing using directives remain unchanged ‑-

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Stateless;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.EntitySystem;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.GameDependencies;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerInput : Control
    {
        internal enum InputState { Idle, Pressed, Dragging }

        internal enum Trigger { LeftDown, LeftUp, RightDown, MoveTreshHolded }

        private readonly StateMachine<InputState, Trigger> state;
        private readonly IComposer composer = DiProvider.Get<IComposer>();
        private const float threshold = 10f;

        private Vector2 startDragPos;

        private readonly StateMachine<InputState, Trigger>.TriggerWithParameters<Vector2> m1;

        public ComposerInput()
        {
            state = new StateMachine<InputState, Trigger>(InputState.Idle);
            m1 = state.SetTriggerParameters<Vector2>(Trigger.MoveTreshHolded);

            state.Configure(InputState.Idle)
                .Permit(Trigger.LeftDown, InputState.Pressed)
                .PermitReentry(Trigger.RightDown);

            state.Configure(InputState.Pressed)
                .OnEntryFrom(Trigger.LeftDown, handleLeftPress)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .Permit(Trigger.MoveTreshHolded, InputState.Dragging)
                .Permit(Trigger.RightDown, InputState.Idle);

            state.Configure(InputState.Dragging)
                .Permit(Trigger.LeftUp, InputState.Idle)
                .Permit(Trigger.RightDown, InputState.Idle)
                .InternalTransition(m1, (d, _) => applyDeltaToSelected(d));

            state.OnTransitioned(t =>
            {
                GD.Print($"Transitioned from {t.Source} to {t.Destination} via {t.Trigger}");
                if (t.Trigger == Trigger.RightDown)
                {
                    RightClickAction();
                }
            });
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button)
            {
                if (button.ButtonIndex == MouseButton.Left)
                {
                    state.Fire(button.Pressed ? Trigger.LeftDown : Trigger.LeftUp);
                }
                else if (button is { ButtonIndex: MouseButton.Right, Pressed: true })
                {
                    state.Fire(Trigger.RightDown);
                }
            }

            if (@event is InputEventMouseMotion motion)
            {
                if (state.State == InputState.Pressed && startDragPos.DistanceTo(GetLocalMousePosition()) > threshold)
                {
                    state.Fire(Trigger.MoveTreshHolded);
                }

                if (state.State == InputState.Dragging)
                {
                    var delta = motion.Relative / GetViewport().GetCamera2D().Zoom;
                    state.Fire(m1, delta);
                }
            }
        }

        private void handleLeftPress()
        {
            startDragPos = GetLocalMousePosition();
            var rids = PhysicsFactory.QuerySelectionAreasPoint();

            // Nothing hit ⇒ either deselect or place new element
            if (rids.Length == 0)
            {
                if (composer.Selected.Count != 0)
                {
                    log("Left-click on empty space ⇒ deselecting all", "yellow");
                    deselect();
                }
                else
                {
                    log("Left-click on empty space ⇒ adding new element", "cyan");
                    composer.RequestAddElement();
                }
                return;
            }

            // Something hit
            if (Input.IsKeyPressed(Key.Shift))
            {
                log("Shift-click ⇒ multi-select", "magenta");
                selectPoint(rids);
                return;
            }

            bool contains = composer.Selected.Entities.Select(c => c.GetComponent<SelectionEcs>().Area)
                .Any(c => rids.Contains(c));

            if (!contains)
            {
                log("Click on different element ⇒ switching selection", "lime");
                deselect();
                selectPoint(rids);
            }
        }

        public void RightClickAction()
        {
            log("Right-click → removing current selection", "orange");
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>())
                .ForEachEntity((ref ElementEcs _, Entity entity) => entity.DeleteEntity());
        }

        private void applyDeltaToSelected(Vector2 delta)
        {
            composer.Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity _) =>
            {
                element.Transform.Origin += delta;
            });

            log($"Dragging selection by {delta}", "skyblue");
        }

        private void selectPoint(Rid[] rids)
        {
            var command = composer.EntityStore.GetCommandBuffer();

            if (Input.IsKeyPressed(Key.Shift))
            {
                foreach (var areaRid in rids)
                    composer.EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
                        .ForEachEntity((ref ElementEcs _, Entity entity) =>
                            command.AddTag<SelectionFlag>(entity.Id));
            }
            else
            {
                var areaRid = rids[0];
                composer.EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
                    .ForEachEntity((ref ElementEcs _, Entity entity) =>
                        command.AddTag<SelectionFlag>(entity.Id));
            }

            log("Element(s) selected", "lime");
            command.Playback();
        }

        private void deselect()
        {
            var batch = new EntityBatch();
            batch.RemoveTag<SelectionFlag>();
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).Entities.ApplyBatch(batch);

            log("Deselected all elements", "yellow");
        }

        // Helper that prints coloured messages to the Godot console.
        private static void log(string msg, string colour = "aqua")
        {

        }
    }
}
