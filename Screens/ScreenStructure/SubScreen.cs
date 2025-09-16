// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using XanaduProject.Character;

namespace XanaduProject.Screens.ScreenStructure
{
    public abstract partial class SubScreen : Screen
    {
        public SubScreen()
        {
            var colorRect = new ColorRect();
            colorRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            colorRect.Color = Colors.Black.Darkened(0.5f);
            AddChild(colorRect);

            var quitButton = new Button { Text = "X" };
            AddChild(quitButton);
            quitButton.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 10);

            quitButton.Pressed += () =>
            {
                OnHide();
                Visible = false;
            };

            Visible = false;
        }

        protected Key ActivatorKey = Key.Escape;


        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (@event is not InputEventKey { Pressed: true } key) return;

            if (ActivatorKey != key.Keycode) return;

            if (Visible)
            {
                OnHide();
                Visible = false;
            }
            else
            {
                OnShow();
                Visible = true;
            }
        }

        protected virtual void OnShow() {}

        protected virtual void OnHide() { }
    }
}
