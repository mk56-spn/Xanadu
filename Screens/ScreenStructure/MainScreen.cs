// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;

namespace XanaduProject.Screens.ScreenStructure
{
    public partial class MainScreen : Screen
    {
        public string? DisplayName { get; set; }

        public override void _Ready()
        {
            showPopup();
        }

        protected MainScreen? CloseTargetScreen = null;

        private void showPopup()
        {
            var popupControl = new Control();
            AddChild(popupControl);
            popupControl.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, margin: 300);

            var textureRect = new TextureRect
            {
                Texture = createGlowTexture(),
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Size = new Vector2(200, 50)
            };
            popupControl.AddChild(textureRect);

            var label = new Label
            {
                Text = DisplayName ?? GetType().Name,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Size = textureRect.Size
            };
            popupControl.AddChild(label);

            var timer = new Timer { OneShot = true, WaitTime = 2 };
            AddChild(timer);
            timer.Timeout += () =>
            {
                popupControl.QueueFree();
                timer.QueueFree();
            };
            timer.Start();
        }

        private GradientTexture2D createGlowTexture()
        {
            var gradient = new Gradient
            {
                Offsets = [0f, 1f],
                Colors = [Colors.White, Colors.Transparent]
            };

            var texture = new GradientTexture2D
            {
                Gradient = gradient,
                Width = 200,
                Height = 50,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 1f)
            };

            return texture;
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventKey { Keycode: Key.Escape }) return;

            if (CloseTargetScreen != null)
                ScreenManager.RequestChangeScreen(() => CloseTargetScreen);
        }
    }
}
