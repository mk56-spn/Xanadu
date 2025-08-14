// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Stage.Masters.Rendering;

namespace XanaduProject.ECSComponents.EntitySystem.ComposerSystems.Widgets
{
    public class BlockMaterialWidget : ComposerSystem
    {
        private readonly HBoxContainer container = new();
        public BlockMaterialWidget()
        {
            Visuals.AddToFloatingBar(container);

            foreach (var v in Enum.GetValues<BlockShaderId>())
            {
                Button b;
                container.AddChild(b = new Button
                {
                    Text = v.ToString(),

                });
                b.Pressed += () => Composer.SelectedBlockShaderId = v;
            }
        }

        protected override void OnUpdate()
        {
            container.Visible = Composer.SelectedTemplateEntity.HasComponent<BlockEcs>();
        }
    }
}
