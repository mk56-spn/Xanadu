// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Composer
{
    public partial class ComposerVisuals(ComposerRenderMaster composer) : Node2D
    {
        private Label infoLabel = new Label();

        public override void _Ready() =>  AddChild(infoLabel);
        public override void _PhysicsProcess(double delta)
        {
            infoLabel.Text = "Canvas transform: " +  GetViewport().CanvasTransform.Origin;
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 center = Vector2.Zero;

            foreach (var rid in composer.SelectedAreas)
            {
                Element element = composer.Dictionary[rid.Item1].Element;
                center += element.Position;

                DrawSetTransformMatrix(element.Transform.Translated(GetViewport().CanvasTransform.Origin));

                DrawRect(new Rect2(-element.Size() / 2, element.Size()), Colors.DeepPink with { A = 0.3f });
                DrawRect(new Rect2(-element.Size() / 2, element.Size()), Colors.DeepPink, false, -0.1f);
            }

            DrawSetTransformMatrix(Transform2D.Identity);
        }
    }
}
