// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Chickensoft.AutoInject;
using Godot;
using SuperNodes.Types;

namespace XanaduProject.Composer
{
    [SuperNode(typeof(Dependent))]
    public partial class MouseGrid : Sprite2D
    {
        public override partial void _Notification(int what);

        [Dependency]
        private bool snapped => DependOn<bool>();

        public MouseGrid ()
        {
            Texture = new GradientTexture2D
            {
                Height = Composer.GRID_SIZE * 4,
                Width = Composer.GRID_SIZE * 4,
                Gradient = new Gradient()
            };

            Material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Shaders/Grid.gdshader")
            };
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            Visible = snapped;

            if (@event is not InputEventMouseMotion mouse) return;

            Position = (mouse.GlobalPosition + GetViewport().GetCamera2D().Offset)
                .Snapped(new Vector2(Composer.GRID_SIZE, Composer.GRID_SIZE));
        }
    }
}
