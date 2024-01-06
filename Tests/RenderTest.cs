// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using Godot;
using XanaduProject.Rendering;
using XanaduProject.Serialization;
using XanaduProject.Serialization.SerialisedObjects;

namespace XanaduProject.Tests
{
    public partial class RenderTest : Control
    {
        [Export]
        private Button button = null!;

        [Export] private Label fps = null!;
        [Export] private Slider rotationSlider = null!;
        [Export] private Slider rotationLocalSlider = null!;

        private RenderMaster? renderMaster;

        public override void _Ready()
        {
            base._Ready();

            renderMaster = new RenderMaster(new TestSerializableStage());
            AddChild(renderMaster);

            button.Pressed += () =>
            {
                SerializableStage stage  =  new TestSerializableStage();
                StageSerializer.Serialize(stage);
                AddChild(new RenderMaster(stage));
            };

        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            fps.Text = Engine.GetFramesPerSecond().ToString(CultureInfo.InvariantCulture);
        }
    }
}
