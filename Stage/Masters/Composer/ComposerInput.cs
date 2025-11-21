using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.Editor.Input;
using Stateless;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerInput : BaseInputHandler
    {
        private readonly IComposer composer = DiProvider.Get<IComposer>();


        private readonly EntityStore store = GameServices.Store;

        private bool isDragSelecting = false;
        private bool dragMultiSelect = false;

        public bool IsDragSelecting => isDragSelecting;
        protected override void OnStateChanged(InputState state)
        {
            composer.State = state;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey { Keycode: Key.Q, Pressed: true, Echo: false })
            {
                composer.Rotating = !composer.Rotating;
            }

            if (@event is InputEventKey { Keycode: Key.S, Pressed: true, Echo: false })
            {
                composer.Snapped = !composer.Snapped;
            }

            if (@event is InputEventKey { Keycode: Key.D, Pressed: true, Echo: false })
            {
                composer.AddOnDrag = !composer.AddOnDrag;
            }

            ProcessInput(@event);
        }

        protected override void HandleLeftPress(bool multiSelect)
        {

            composer.LastClickedMousePosLocal = composer.MousePosLocal;
            var rids = PhysicsFactory.QuerySelectionAreasPoint();


                // Nothing hit ⇒ either deselect, place new element, or start drag selection
                if (rids.Length == 0)
                {
                    if (!composer.AddOnDrag)
                    {
                        if (Input.IsKeyPressed(Key.Shift))
                        {
                            // Start drag selection
                            isDragSelecting = true;
                            composer.IsDragSelecting = true;
                            dragMultiSelect = multiSelect;
                        }
                        else
                        {
                            composer.RequestAddElement();
                        }


                        // If not multi-select, deselect everything first
                        if (!multiSelect)
                        {
                            deselect();
                        }
                    }
                    else if (!multiSelect && composer.Selected.Count > 0)
                    {
                        deselect();
                    }
                    else if (composer.Selected.Count == 0)
                    {
                        composer.RequestAddElement();
                    }

                    return;
                }

                // Something hit
                if (multiSelect)
                {
                    selectPoint(rids, true);
                    return;
                }

                bool contains = composer.Selected.Entities.Select(c => c.GetComponent<SelectionEcs>().Area)
                    .Any(c => rids.Contains(c));

                if (contains) return;
                deselect();
                selectPoint(rids, false);

        }

        protected override void OnRightClick()
        {
            // When AddOnDrag is disabled, delete entities under cursor AND all selected entities
            if (composer.AddOnDrag) return;

            // Delete entities under cursor
            var rids = PhysicsFactory.QuerySelectionAreasPoint();
            if (rids.Length > 0)
            {
                var command = composer.EntityStore.GetCommandBuffer();
                foreach (var areaRid in rids)
                {
                    composer.EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
                        .ForEachEntity((ref ElementEcs _, Entity entity) =>
                            command.DeleteEntity(entity.Id));
                }
                command.Playback();
            }

            // Also delete all selected entities
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>())
                .ForEachEntity((ref ElementEcs _, Entity entity) => entity.DeleteEntity());
        }

        protected override void OnDrag(Vector2 delta)
        {

            // If we're drag selecting, don't do anything - just let the visual show
            if (isDragSelecting)
            {
                return;
            }

            // If AddOnDrag is enabled, add blocks as you drag (only if no existing entity underneath)
            if (composer.AddOnDrag)
            {
                var rids = PhysicsFactory.QuerySelectionAreasPoint();
                if (rids.Length == 0)
                {
                    composer.RequestAddElement();
                }
                return;
            }

            // Normal drag behavior for selected elements
            composer.Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity _) =>
            {
                element.Transform.Origin += delta;
            });
        }

        protected override void OnRightDrag(Vector2 delta)
        {
            // If AddOnDrag is enabled, remove blocks at cursor position
            if (composer.AddOnDrag)
            {
                var rids = PhysicsFactory.QuerySelectionAreasPoint();
                if (rids.Length > 0)
                {
                    var command = composer.EntityStore.GetCommandBuffer();
                    foreach (var areaRid in rids)
                    {
                        composer.EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
                            .ForEachEntity((ref ElementEcs _, Entity entity) =>
                                command.DeleteEntity(entity.Id));
                    }
                    command.Playback();
                }
            }
        }

        private void selectPoint(Rid[] rids, bool multiSelect)
        {
            var command = composer.EntityStore.GetCommandBuffer();

            if (multiSelect)
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

            command.Playback();
        }

        protected override void OnDragEnd()
        {
            if (isDragSelecting)
            {
                // Calculate the drag rectangle
                var startPos = composer.LastClickedMousePosLocal;
                var endPos = composer.MousePosLocal;

                var rect = new Rect2(
                    new Vector2(Mathf.Min(startPos.X, endPos.X), Mathf.Min(startPos.Y, endPos.Y)),
                    new Vector2(Mathf.Abs(endPos.X - startPos.X), Mathf.Abs(endPos.Y - startPos.Y))
                );

                selectDragRect(rect, dragMultiSelect);

                isDragSelecting = false;
                composer.IsDragSelecting = false;
                dragMultiSelect = false;
            }
        }

        private void deselect()
        {
            var batch = new EntityBatch();
            batch.RemoveTag<SelectionFlag>();
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).Entities.ApplyBatch(batch);
        }

        private void selectDragRect(Rect2 rect, bool multiSelect)
        {
            var rids = PhysicsFactory.QuerySelectionAreasRect(rect);

            if (rids.Length == 0)
                return;

            var command = composer.EntityStore.GetCommandBuffer();

            if (!multiSelect)
            {
                deselect();
            }

            foreach (var areaRid in rids)
            {
                composer.EntityStore.Query<ElementEcs>().HasValue<SelectionEcs, Rid>(areaRid)
                    .ForEachEntity((ref ElementEcs _, Entity entity) =>
                        command.AddTag<SelectionFlag>(entity.Id));
            }

            command.Playback();
        }
    }
}
