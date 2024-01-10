// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Serialization.Elements;

namespace XanaduProject.Composer
{
    public partial class ComposerEditWidget : Control
    {
        [Export] private Slider scaleY = null!;
        [Export] private Slider scaleX = null!;
        [Export] private Slider skew  = null!;
        [Export] private ColorPicker picker = null!;

        private ComposerRenderMaster composer = null!;

        private Rid? target;
        public Rid? Target
        {
            get => target;
            set
            {
                Visible = value != null;
                target = value;

                if (target == null) return;

                Element element = composer.GetElementForArea(target.Value);

                scaleX.SetValueNoSignal(element.Scale.X);
                scaleY.SetValueNoSignal(element.Scale.Y);
                skew.SetValueNoSignal(element.Skew);

                picker.Color = composer.GetElementForArea(target.Value).Colour;
            }
        }

        private Vector2 scale => new Vector2((float)scaleX.Value, (float)scaleY.Value);

        public static ComposerEditWidget Create(ComposerRenderMaster composer)
        {
            var widget = GD.Load<PackedScene>("res://Composer/ComposerEditWidget.tscn").Instantiate<ComposerEditWidget>();
            widget.composer = composer;
            widget.Visible = false;
            return widget;
        }

        public override void _EnterTree()
        {
            AddChild(new RotationWidget(composer, this));
            scaleX.ValueChanged += _ => setScale();
            scaleY.ValueChanged += _ => setScale();
            skew.ValueChanged += value =>
            {
                composer.SkewElement(target!.Value, (float)value);
                composer.QueueRedraw();
            };
            picker.ColorChanged += color => composer.TintElement(target!.Value, color);
        }

        private void setScale()
        {
            composer.QueueRedraw();
            composer.ScaleElement(target!.Value, scale);
        }
    }
}
