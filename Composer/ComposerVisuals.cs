// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Composer
{
    public partial class ComposerVisuals(ComposerRenderMaster composer) : Control
    {
        private Label infoLabel = new Label { Visible = false, Modulate = Colors.GreenYellow };

        public override void _Ready()
        {
            AddChild(infoLabel);
            infoLabel.Position = new Vector2(30, 100);
        }

        public override void _Process(double delta)
        {
            infoLabel.Text = $"Canvas transform: {GetViewport().CanvasTransform.Origin} \nSelected count: {composer.SelectedAreas.Count} ";
            QueueRedraw();
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { KeyLabel: Key.F10 , Pressed: true })
                infoLabel.Visible = !infoLabel.Visible;
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
