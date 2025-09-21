using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.ECSComponents.EntitySystem.Components.Physics;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;
using XanaduProject.GameDependencies;
using XanaduProject.Scenes.Editor.Input;

namespace XanaduProject.Stage.Masters.Composer
{
    public partial class ComposerInput : BaseInputHandler
    {
        private readonly IComposer composer = DiProvider.Get<IComposer>();

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

            ProcessInput(@event);
        }

        protected override void HandleLeftPress(bool multiSelect)
        {
            var rids = PhysicsFactory.QuerySelectionAreasPoint();

            // Nothing hit ⇒ either deselect or place new element
            if (rids.Length == 0)
            {
                if (!multiSelect && composer.Selected.Count > 0)
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
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>())
                .ForEachEntity((ref ElementEcs _, Entity entity) => entity.DeleteEntity());
        }

        protected override void OnDrag(Vector2 delta)
        {
            composer.Selected.ForEachEntity((ref ElementEcs element, ref SelectionEcs _, Entity _) =>
            {
                element.Transform.Origin += delta;
            });
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

        private void deselect()
        {
            var batch = new EntityBatch();
            batch.RemoveTag<SelectionFlag>();
            composer.EntityStore.Query<ElementEcs>().AllTags(Tags.Get<SelectionFlag>()).Entities.ApplyBatch(batch);
        }
    }
}
