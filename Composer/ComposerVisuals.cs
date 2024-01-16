// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Composer
{
    public partial class ComposerVisuals(ComposerRenderMaster composer) : Control
    {
        private Label infoLabel = new Label { Visible = false, Modulate = Colors.GreenYellow };

        private Viewport viewport = null!;

        public override void _Ready()
        {
            viewport = GetViewport();
            AddChild(infoLabel);
            infoLabel.Position = new Vector2(30, 100);
        }

        public override void _Process(double delta)
        {
            infoLabel.Text = $"Canvas transform: {viewport.CanvasTransform.Origin} " +
                             $"\nSelected count: {composer.SelectedAreas.Count} " +
                             $"\nZoom: {viewport.CanvasTransform.Scale}";
        }

        public override void _PhysicsProcess(double delta) => QueueRedraw();

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { KeyLabel: Key.F10 , Pressed: true })
                infoLabel.Visible = !infoLabel.Visible;
        }

        public override void _Draw()
        {
            Vector2 center = Vector2.Zero;

            foreach (var pair in composer.SelectedAreas)
            {

                DrawSetTransformMatrix(pair.renderElement.Element.Transform.Scaled(viewport.CanvasTransform.Scale).Translated(viewport.CanvasTransform.Origin));

                Element element = pair.Item1.Element;
                center += element.Position;
                switch (element)
                {
                    case NoteElement:
                        DrawCircle(Vector2.Zero, NoteElement.RADIUS, element.ComposerColour with { A = 0.3f });
                        DrawArc(Vector2.Zero, NoteElement.RADIUS, 0, Mathf.Tau, 50, element.ComposerColour);
                        break;
                    default:
                        DrawRect(new Rect2(-element.Size() / 2, element.Size()), element.ComposerColour with { A = 0.3f });
                        DrawRect(new Rect2(-element.Size() / 2, element.Size()),  element.ComposerColour, false);
                        break;
                }
            }

            DrawSetTransformMatrix(Transform2D.Identity);
            DrawSetTransform(viewport.CanvasTransform.Origin.Snapped(new Vector2(10, 10)));
            DrawLine(new Vector2(0, 0), new Vector2(0, 100), Colors.Aqua);
        }
    }
}
