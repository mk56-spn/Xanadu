// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace XanaduProject.Composer
{
    public partial class ComposerEditWidget : Control
    {
        [Export] private Slider scaleY = null!;
        [Export] private Slider scaleX = null!;
        [Export] private ColorPicker picker = null!;

        private ComposerRenderMaster composer = null!;

        private Rid? target;
        public Rid? Target
        {
            set
            {
                Visible = value != null;
                target = value;

                if (target == null) return;

                Vector2 vector2 = composer.GetElementForArea(target.Value).Scale;

                scaleX.SetValueNoSignal(vector2.X);
                scaleY.SetValueNoSignal(vector2.Y);

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
            scaleX.ValueChanged += _ => setScale();
            scaleY.ValueChanged += _ => setScale();
            picker.ColorChanged += color => composer.TintElement(target!.Value, color);
        }

        private void setScale()
        {
            composer.QueueRedraw();
            composer.ScaleElement(target!.Value, scale);
        }


        private partial class RotationWidget : Control
        {
            private readonly List<(Rid, Vector2)> areas;

            public RotationWidget(List<(Rid, Vector2)> areas)
            {
                this.areas = areas;
            }

            public override void _Process(double delta) => QueueRedraw();

            public override void _Draw()
            {
                Vector2 averagePoint = areas.Aggregate(Vector2.Zero, (current, element) => current + element.Item2);
                averagePoint /= areas.Count;
                Position = averagePoint;

                DrawSetTransform(Vector2.Zero, Mathf.DegToRad(-90));
                DrawArc(Vector2.Zero, 100, Mathf.DegToRad(10), Mathf.DegToRad(350), 3000, Colors.Aqua);
            }
        }
    }
}
