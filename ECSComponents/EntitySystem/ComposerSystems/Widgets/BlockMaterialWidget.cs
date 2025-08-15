// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Factories;
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
                var size = new Vector2(50, 50);
                Button b;
                container.AddChild(b = new Button { CustomMinimumSize = size });

                RenderRid.Create(b.GetCanvasItem())
                    .SetTransform(new Transform2D(0, b.CustomMinimumSize / 2))
                    .SetMaterial(Materials.BLOCKS.Get(v))
                    .AddRect(new Vector2(40, 40));

                b.Pressed += () => Composer.SelectedBlockShaderId = v;
            }
        }

        protected override void OnUpdate()
        {
            container.Visible = Composer.SelectedTemplateEntity.HasComponent<BlockEcs>();
        }
    }
}
