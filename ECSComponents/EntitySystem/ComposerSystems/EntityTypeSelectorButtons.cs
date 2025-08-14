// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.Stage.Masters.Composer;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems
{
    public class EntityTypeSelectorButtons : ComposerSystem
    {
        private readonly VBoxContainer container = new();
        public EntityTypeSelectorButtons()
        {
            Visuals.LeftBarAddWidget(container);
            populateButtons();
        }

        private void populateButtons()
        {
            EntityTemplate.GET_ENTITIES.ForEachEntity(((ref NameEcs component1, Entity entity) =>
            {
                var button = new Button { Text = component1.Name };
                button.Pressed += () => Composer.SelectedTemplateEntity = entity;
                container.AddChild(button);
            }));
        }

        protected override void OnUpdate()
        {
        }
    }
}
