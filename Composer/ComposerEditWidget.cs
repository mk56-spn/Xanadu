// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Rendering;

namespace XanaduProject.Composer
{
    public partial class ComposerEditWidget : Control
    {
        [Export] private Slider scaleY = null!;
        [Export] private Slider scaleX = null!;
        [Export] private Slider skew  = null!;
        [Export] private ColorPicker picker = null!;
        [Export] private SpinBox depth = null!;

        private ComposerRenderMaster composer = null!;

        private RenderElement? target;
        public RenderElement? Target
        {
            get => target;
            set
            {
                Visible = value != null;
                target = value;

                if (Target == null) return;

                var element = Target.Element;

                GD.Print(element.Zindex);
                depth.SetValueNoSignal(element.Zindex);
                scaleX.SetValueNoSignal(element.Scale.X);
                scaleY.SetValueNoSignal(element.Scale.Y);
                skew.SetValueNoSignal(element.Skew);

                picker.Color = element.Colour;
            }
        }

        private Vector2 scale => new Vector2((float)scaleX.Value, (float)scaleY.Value);

        public static ComposerEditWidget Create(ComposerRenderMaster composer)
        {
            var widget = GD.Load<PackedScene>("res://Composer/ComposerEditWidget.tscn")
                .Instantiate<ComposerEditWidget>();
            widget.composer = composer;
            widget.Visible = false;
            return widget;
        }

        public override void _EnterTree()
        {
            AddChild(new RotationWidget(this));

            scaleX.ValueChanged += value => target?.SetScale(target.Element.Scale with { X = (float)value });
            scaleY.ValueChanged += value => target?.SetScale(target.Element.Scale with { Y = (float)value });
            skew.ValueChanged += value =>
            {
                target?.SetSkew((float)value);
                composer.QueueRedraw();
            };
            picker.ColorChanged += value => target?.SetTint(value);
            depth.ValueChanged += value => target?.SetDepth((int)value);
        }
    }
}
